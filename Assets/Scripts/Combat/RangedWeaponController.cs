using System;
using System.Collections.Generic;
using SheltreSim.Audio;
using SheltreSim.Building;
using SheltreSim.Crafting;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Saving;
using SheltreSim.Survival;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SheltreSim.Combat
{
    public enum WeaponHand { Melee = 0, Ranged = 1 }

    /// <summary>
    /// The player's guns: which hand is raised, magazines, heat, reloading and firing. Creates itself on the player's
    /// camera pivot after every scene load. Ammo only ever moves when a reload finishes, in one TryConsume.
    /// Magazines stay with the weapon when swapping. If the last copy of a firearm leaves the inventory, its rounds return.
    /// Rounds in an energy magazine are charge, so those stay with the weapon.
    /// </summary>
    public sealed class RangedWeaponController : MonoBehaviour, ISaveableSystem
    {
        public const float MaxHeat = 100f;
        public const float HeatRecoverAt = 35f;
        public const float CoolPerSecond = 12f;
        /// <summary>Cooling starts this long after the last shot. Without it no energy weapon could overheat within one magazine.</summary>
        public const float CoolDelaySeconds = 0.5f;

        public static RangedWeaponController Active { get; private set; }
        private static bool hooked;

        [Serializable]
        private sealed class Magazine
        {
            public string id;
            public int rounds;
            public float heat;
            public bool locked;
            [NonSerialized] public float lastShotTime = float.NegativeInfinity;
        }

        [Serializable]
        private sealed class SavedState
        {
            public List<Magazine> magazines = new List<Magazine>();
            public int hand;
        }

        /// <summary>Test hook: lets probes fire without the locked-cursor requirement.</summary>
        public bool ignoreCursorLock;
        /// <summary>Test hook: behaves like the fire button being held, through the same trigger logic.</summary>
        public bool debugTriggerHeld;

        private readonly Dictionary<string, Magazine> magazines = new Dictionary<string, Magazine>();
        private RangedInputActions input;
        private FirstPersonController playerController;
        private SurvivalStats stats;
        private Inventory inventory;
        private PlayerGear gear;
        private MeleeAttacker melee;
        private BuildModeController build;
        private Transform aim;
        private RangedWeaponFx fx;
        private RangedViewmodel viewmodel;
        private RangedWeaponHud hud;
        private RangedCombat combat;
        private WeaponHand hand = WeaponHand.Melee;
        private string weaponId = "";
        private ItemSpec spec;
        private float nextFireTime, reloadLeft, nextDryClick, nextImpactSound;
        private bool reloading, debugHeldLastFrame;

        public string SaveKey => "weapons";
        public WeaponHand Hand => hand;
        public string WeaponId => weaponId;
        public ItemSpec Spec => spec;
        public bool IsReloading => reloading;
        public float ReloadRemaining => reloading ? reloadLeft : 0f;
        public int ShotsFired { get; private set; }
        public int LastShotPellets { get; private set; }
        public int LastShotHits { get; private set; }
        public int ActiveBolts => combat != null ? combat.ActiveBolts : 0;
        public RangedWeaponHud Hud => hud;
        public RangedViewmodel Viewmodel => viewmodel;
        public RangedWeaponFx Fx => fx;

        public int RoundsIn(string id) => magazines.TryGetValue(id, out Magazine m) ? m.rounds : 0;
        public float HeatOf(string id) => magazines.TryGetValue(id, out Magazine m) ? m.heat : 0f;
        public bool IsOverheated(string id) => magazines.TryGetValue(id, out Magazine m) && m.locked;
        public int ReserveFor(string id)
        {
            ItemSpec s = RangedSpec(id);
            return s != null && inventory != null ? inventory.GetCount(s.ammoId) : 0;
        }

        // ---------- Lifecycle ----------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
            hooked = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneLoaded += (scene, mode) => Ensure();
            }
            Ensure();
        }

        private static void Ensure()
        {
            if (Active != null) return;
            FirstPersonController player = FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (player != null && player.cameraPivot != null) player.cameraPivot.gameObject.AddComponent<RangedWeaponController>();
        }

        private void Awake()
        {
            if (Active != null && Active != this) { Destroy(this); return; }
            Active = this;
            input = new RangedInputActions();
            playerController = GetComponentInParent<FirstPersonController>(true);
            stats = GetComponentInParent<SurvivalStats>(true);
            inventory = GetComponentInParent<Inventory>(true);
            melee = GetComponentInChildren<MeleeAttacker>(true);
            build = melee != null && melee.buildModeController != null ? melee.buildModeController : FindFirstObjectByType<BuildModeController>(FindObjectsInactive.Include);
            Camera view = GetComponentInChildren<Camera>(true);
            aim = view != null ? view.transform : transform;
            gear = PlayerGear.Ensure(playerController != null ? playerController.gameObject : transform.root.gameObject);
            gear.ItemEquipped += OnItemEquipped;

            fx = new RangedWeaponFx();
            hud = new RangedWeaponHud();
            viewmodel = new RangedViewmodel(transform, view);
            combat = new RangedCombat(playerController != null ? playerController.transform : transform.root, fx, kill => hud.ShowHit(kill));
            SaveableSystems.Register(this);
        }

        private void OnEnable() => input?.Enable();
        private void OnDisable() => input?.Disable();

        private void OnDestroy()
        {
            if (Active != this) return;
            Active = null;
            SaveableSystems.Unregister(this);
            if (gear != null) gear.ItemEquipped -= OnItemEquipped;
            input?.Dispose();
            fx?.Destroy();
            hud?.Destroy();
        }

        private void OnItemEquipped(GearSlot slot, string id)
        {
            if (slot == GearSlot.Ranged)
            {
                SyncWeapon();
                ChangeHand(WeaponHand.Ranged);
            }
            else if (slot == GearSlot.Weapon) ChangeHand(WeaponHand.Melee);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            SyncWeapon();
            RecoverRoundsOfLostFirearms();
            CoolAll(dt);
            TickReload(dt);
            combat.TickBolts(dt);
            ReadInput();
            viewmodel.Tick(dt, reloading);
            fx.Tick(dt);
            RefreshHud();
        }

        private void RefreshHud()
        {
            bool visible = hand == WeaponHand.Ranged && (stats == null || !stats.IsDead);
            Magazine m = spec != null ? MagazineOf(weaponId) : null;
            hud.Refresh(visible, spec, m != null ? m.rounds : 0, spec != null ? ReserveFor(weaponId) : 0,
                m != null ? m.heat : 0f, m != null && m.locked, reloading, reloadLeft);
        }

        // ---------- Hands ----------

        private static ItemSpec RangedSpec(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            ItemSpec s = ItemCatalog.Spec(id);
            return s != null && s.ranged ? s : null;
        }

        private void SyncWeapon()
        {
            string id = gear != null ? gear.RangedId : "";
            if (id == weaponId) return;
            CancelReload();
            weaponId = id;
            spec = RangedSpec(id);
            if (spec == null) { weaponId = ""; hand = WeaponHand.Melee; }
            ApplyHand();
        }

        private void ChangeHand(WeaponHand next)
        {
            if (next != hand) CancelReload();
            hand = next == WeaponHand.Ranged && spec != null ? WeaponHand.Ranged : WeaponHand.Melee;
            ApplyHand();
        }

        private void ApplyHand()
        {
            if (melee != null) melee.suppressed = hand == WeaponHand.Ranged;
            viewmodel.Show(hand == WeaponHand.Ranged ? weaponId : "");
        }

        /// <summary>The 1 and 2 keys. Refused while paused, dead or building.</summary>
        public bool SelectHand(WeaponHand next)
        {
            if (Time.timeScale <= 0f || IsDead || InBuildMode) return false;
            if (next == hand) return true;
            SyncWeapon();
            if (next == WeaponHand.Ranged && spec == null)
            {
                GameMessageBus.Post("No ranged weapon equipped. Equip one in your inventory (I).");
                return false;
            }
            ChangeHand(next);
            GameAudio.Play2D(Sfx.Equip, 0.45f);
            return true;
        }

        private bool IsDead => stats != null && (stats.IsDead || stats.health <= 0f);
        private bool InBuildMode => build != null && build.IsBuildModeActive;

        private bool CanAct() => isActiveAndEnabled && Time.timeScale > 0f && !IsDead && !InBuildMode &&
            (ignoreCursorLock || Cursor.lockState == CursorLockMode.Locked) &&
            (playerController == null || playerController.isActiveAndEnabled);

        // ---------- Input ----------

        private void ReadInput()
        {
            if (input.MeleeHand.WasPressedThisFrame()) SelectHand(WeaponHand.Melee);
            else if (input.RangedHand.WasPressedThisFrame()) SelectHand(WeaponHand.Ranged);

            bool held = input.Fire.IsPressed() || debugTriggerHeld;
            bool pressed = input.Fire.WasPressedThisFrame() || (debugTriggerHeld && !debugHeldLastFrame);
            debugHeldLastFrame = debugTriggerHeld;
            if (hand != WeaponHand.Ranged || spec == null) return;

            // R belongs to build mode's rotate while building: CanAct refuses it there.
            if (input.Reload.WasPressedThisFrame()) TryReload();
            if (RangedWeaponProfile.IsAutomatic(spec) ? held : pressed) TryFire();
        }

        // ---------- Magazines ----------

        private Magazine MagazineOf(string id)
        {
            if (!magazines.TryGetValue(id, out Magazine m))
            {
                m = new Magazine { id = id };
                magazines[id] = m;
            }
            return m;
        }

        private void RecoverRoundsOfLostFirearms()
        {
            if (inventory == null) return;
            foreach (Magazine m in magazines.Values)
            {
                if (m.rounds <= 0 || inventory.GetCount(m.id) > 0) continue;
                ItemSpec s = RangedSpec(m.id);
                if (s == null || s.energy) continue;
                inventory.Add(s.ammoId, m.rounds);
                m.rounds = 0;
            }
        }

        private void CoolAll(float dt)
        {
            if (dt <= 0f) return;
            foreach (Magazine m in magazines.Values)
            {
                if (m.heat <= 0f) continue;
                if (m.locked || Time.time - m.lastShotTime >= CoolDelaySeconds) m.heat = Mathf.Max(0f, m.heat - CoolPerSecond * dt);
                if (m.locked && m.heat <= HeatRecoverAt) m.locked = false;
            }
        }

        // ---------- Reload ----------

        /// <summary>Starts a reload. Nothing is consumed until it finishes, so cancelling or saving midway loses nothing.</summary>
        public bool TryReload()
        {
            if (!CanAct() || hand != WeaponHand.Ranged || spec == null || reloading) return false;
            Magazine m = MagazineOf(weaponId);
            if (m.rounds >= spec.magazine) return false;
            if (spec.energy && m.rounds * 2 >= spec.magazine)
            {
                GameMessageBus.Post("A cell refills the whole magazine. Fire it under half first.");
                return false;
            }
            if (inventory == null || inventory.GetCount(spec.ammoId) < 1)
            {
                GameMessageBus.Post($"No {ItemCatalog.NameOf(spec.ammoId)} left.");
                DryClick();
                return false;
            }
            reloading = true;
            reloadLeft = RangedWeaponProfile.ReloadSeconds(weaponId);
            GameAudio.Play2D(Sfx.GunReload, 0.8f, 0.02f);
            return true;
        }

        private void CancelReload()
        {
            reloading = false;
            reloadLeft = 0f;
        }

        private void TickReload(float dt)
        {
            if (!reloading) return;
            if (IsDead || spec == null) { CancelReload(); return; }
            reloadLeft -= dt;
            if (reloadLeft > 0f) return;
            CancelReload();
            Magazine m = MagazineOf(weaponId);
            if (spec.energy)
            {
                if (inventory.TryConsume(spec.ammoId, 1)) m.rounds = spec.magazine;
                return;
            }
            int take = Mathf.Min(spec.magazine - m.rounds, inventory.GetCount(spec.ammoId));
            if (take > 0 && inventory.TryConsume(spec.ammoId, take)) m.rounds += take;
        }

        // ---------- Firing ----------

        /// <summary>One shot through every real gate. Returns false when gated, empty, overheated or still cycling.</summary>
        public bool TryFire()
        {
            if (!CanAct() || hand != WeaponHand.Ranged || spec == null || reloading || Time.time < nextFireTime) return false;
            Magazine m = MagazineOf(weaponId);
            if (m.locked) return false;
            if (m.rounds <= 0)
            {
                DryClick();
                return false;
            }

            m.rounds--;
            ShotsFired++;
            // Carry the frame's remainder so held fire keeps its real rate at any frame rate.
            float interval = 60f / Mathf.Max(1f, spec.roundsPerMinute);
            nextFireTime = Time.time - nextFireTime < interval ? nextFireTime + interval : Time.time + interval;

            if (spec.energy)
            {
                m.lastShotTime = Time.time;
                m.heat = Mathf.Min(MaxHeat, m.heat + spec.heatPerShot);
                if (m.heat >= MaxHeat && !m.locked)
                {
                    m.locked = true;
                    GameAudio.Play2D(Sfx.EnergyOverheat, 0.9f, 0.02f);
                }
            }

            Color color = RangedWeaponProfile.ShotColor(spec);
            Vector3 muzzle = viewmodel.MuzzleWorld;
            GameAudio.Play2D(RangedWeaponProfile.ShotSound(weaponId), 0.85f, 0.05f);
            fx.Flash(muzzle, color);
            viewmodel.AddKick(RangedWeaponProfile.Kick(spec));

            if (RangedWeaponProfile.IsPlasma(spec))
            {
                LastShotPellets = 1;
                LastShotHits = 0;
                combat.LaunchBolt(aim.position, SpreadDirection(), muzzle, spec.damage, spec.splashRadius, spec.rangeMeters, color);
            }
            else FireHitscan(muzzle, color);
            return true;
        }

        private void FireHitscan(Vector3 muzzle, Color color)
        {
            int pellets = Mathf.Max(1, spec.pellets);
            int hits = 0;
            bool kill = false, flesh = false, world = false;
            Vector3 fleshPoint = Vector3.zero, worldPoint = Vector3.zero;
            for (int i = 0; i < pellets; i++)
            {
                Vector3 direction = SpreadDirection();
                Vector3 end = aim.position + direction * spec.rangeMeters;
                if (combat.FindHit(aim.position, direction, spec.rangeMeters, out RaycastHit hit))
                {
                    end = hit.point;
                    RangedCombat.HitKind result = combat.ApplyDamage(hit.collider, spec.damage, aim.position);
                    if (result == RangedCombat.HitKind.World)
                    {
                        if (!world) worldPoint = hit.point;
                        world = true;
                    }
                    else
                    {
                        if (!flesh) fleshPoint = hit.point;
                        flesh = true;
                        hits++;
                        kill |= result == RangedCombat.HitKind.Kill;
                    }
                }
                if (spec.energy) fx.Beam(muzzle, end, color);
                else fx.Tracer(muzzle, end, color);
            }
            LastShotPellets = pellets;
            LastShotHits = hits;

            if (flesh)
            {
                hud.ShowHit(kill);
                GameAudio.PlayAt(Sfx.HitFlesh, fleshPoint, 0.8f, 30f);
            }
            else if (world && Time.time >= nextImpactSound)
            {
                nextImpactSound = Time.time + 0.05f;
                GameAudio.PlayAt(Sfx.BulletImpact, worldPoint, 0.7f, 35f);
            }
        }

        private Vector3 SpreadDirection()
        {
            // Cosmetic random is allowed here: nothing about a shot's outcome is saved or generated.
            float angle = spec.spreadDegrees * Mathf.Sqrt(UnityEngine.Random.value);
            float roll = UnityEngine.Random.value * 360f;
            return aim.rotation * (Quaternion.AngleAxis(roll, Vector3.forward) * Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward);
        }

        private void DryClick()
        {
            if (Time.unscaledTime < nextDryClick) return;
            nextDryClick = Time.unscaledTime + 0.35f;
            GameAudio.Play2D(Sfx.GunEmpty, 0.75f, 0.02f);
        }

        // ---------- Save ----------

        public string CaptureState()
        {
            var state = new SavedState { hand = (int)hand };
            foreach (Magazine m in magazines.Values)
                if (m.rounds > 0 || m.heat > 0f) state.magazines.Add(m);
            return JsonUtility.ToJson(state);
        }

        public void RestoreState(string json)
        {
            SavedState state = null;
            if (!string.IsNullOrEmpty(json))
            {
                // A broken section must not stop the rest of the load: treat it as a save without weapons state.
                try { state = JsonUtility.FromJson<SavedState>(json); }
                catch (ArgumentException) { state = null; }
            }

            magazines.Clear();
            CancelReload();
            combat.ClearBolts();
            nextFireTime = 0f;
            if (state != null && state.magazines != null)
            {
                foreach (Magazine saved in state.magazines)
                {
                    ItemSpec s = saved != null ? RangedSpec(saved.id) : null;
                    if (s == null) continue;
                    Magazine m = MagazineOf(saved.id);
                    m.rounds = Mathf.Clamp(saved.rounds, 0, s.magazine);
                    float heat = float.IsNaN(saved.heat) || float.IsInfinity(saved.heat) ? 0f : saved.heat;
                    m.heat = s.energy ? Mathf.Clamp(heat, 0f, MaxHeat) : 0f;
                    m.locked = s.energy && saved.locked && m.heat > HeatRecoverAt;
                }
            }

            string id = gear != null ? gear.RangedId : "";
            spec = RangedSpec(id);
            weaponId = spec != null ? id : "";
            hand = state != null && state.hand == (int)WeaponHand.Ranged && spec != null ? WeaponHand.Ranged : WeaponHand.Melee;
            ApplyHand();
        }

        // ---------- Test hooks ----------

        public void DebugSetRounds(string id, int rounds) => MagazineOf(id).rounds = rounds;
        public void DebugResetCooldown() => nextFireTime = 0f;
        public void DebugFinishReload() { if (reloading) reloadLeft = 0f; }
    }
}
