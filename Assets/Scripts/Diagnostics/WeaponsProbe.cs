using System;
using System.Collections;
using System.Collections.Generic;
using SheltreSim.Building;
using SheltreSim.Combat;
using SheltreSim.Crafting;
using SheltreSim.Enemies;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Survival;
using SheltreSim.World;
using UnityEngine;
using UnityEngine.UI;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// `--weapons-probe`: fires the real weapons through RangedWeaponController's real gates in the built player, on a test
    /// platform high above the world so no scenery interferes. Never saves: state goes through CaptureState/RestoreState only.
    /// Keys and mouse are the one thing not exercised (there is no input device): debugTriggerHeld goes through the same trigger logic.
    /// </summary>
    public sealed class WeaponsProbe : MonoBehaviour
    {
        private const float Top = 400f;
        private const float RaiderHealth = 5000f;
        private static readonly string[] AllWeapons = { "pistol", "smg", "shotgun", "hunting_rifle", "assault_rifle", "laser_pistol", "laser_rifle", "plasma_rifle" };

        private int failures;
        private FirstPersonController player;
        private CharacterController body;
        private SurvivalStats stats;
        private Inventory inventory;
        private PlayerGear gear;
        private MeleeAttacker melee;
        private BuildModeController build;
        private RangedWeaponController gun;
        private Camera view;
        private Vector3 origin;
        private float healthAtStart;
        private readonly List<GameObject> spawned = new List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--weapons-probe") < 0) return;
            new GameObject("WeaponsProbe").AddComponent<WeaponsProbe>();
        }

        private IEnumerator Start()
        {
            StartCoroutine(Watchdog());
            yield return null;
            yield return null;
            yield return Run(Setup());
            if (gun != null)
            {
                yield return Run(CheckNoAmmo());
                yield return Run(CheckAccounting());
                yield return Run(CheckRatesAndViewmodels());
                yield return Run(CheckDamage());
                yield return Run(CheckHands());
                yield return Run(CheckGates());
                yield return Run(CheckEnergy());
                yield return Run(CheckPlasma());
                yield return Run(CheckSaves());
                yield return Run(CheckLostWeapon());
                Check(Mathf.Approximately(stats.health, healthAtStart), $"No shot of any kind hurt the player ({stats.health:0.#}/{healthAtStart:0.#})");
                yield return Run(CheckDeath());
            }
            Time.timeScale = 1f;
            Debug.Log(failures == 0 ? "[WeaponsProbe] ALL CHECKS PASSED" : $"[WeaponsProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private IEnumerator Run(IEnumerator routine)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext()) break;
                    current = routine.Current;
                }
                catch (Exception error)
                {
                    Check(false, "Exception: " + error);
                    break;
                }
                yield return current;
            }
            if (gun != null) gun.debugTriggerHeld = false;
            ClearSpawned();
            yield return null;
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(115f);
            Debug.LogError("[WeaponsProbe] FAIL: timed out");
            Application.Quit(1);
        }

        // ---------- Test stage ----------

        private IEnumerator Setup()
        {
            player = FindFirstObjectByType<FirstPersonController>();
            Check(player != null, "Player exists");
            if (player == null) yield break;
            body = player.GetComponent<CharacterController>();
            stats = player.GetComponent<SurvivalStats>();
            inventory = player.GetComponent<Inventory>();
            gear = PlayerGear.Ensure(player.gameObject);
            melee = player.GetComponentInChildren<MeleeAttacker>(true);
            build = melee != null ? melee.buildModeController : null;
            view = player.GetComponentInChildren<Camera>();
            RangedWeaponController found = RangedWeaponController.Active;
            Check(found != null && found.transform == player.cameraPivot, "Controller created itself on the camera pivot");
            Check(melee != null && build != null && inventory != null && stats != null, "Melee, build mode, inventory and stats exist");
            Check(ProceduralWorld.Active != null && ProceduralWorld.Active.assets != null && ProceduralWorld.Active.assets.raiderPrefab != null, "A raider prefab exists");
            Check(Time.timeScale > 0f, "Game is running");
            if (found == null || melee == null || build == null || inventory == null || stats == null) yield break;

            stats.decayPaused = true;
            stats.RestoreVitals(100f, 100f, 100f);
            healthAtStart = stats.health;
            inventory.Clear();
            gear.ResetAll();
            melee.ignoreCursorLock = true;
            found.ignoreCursorLock = true;

            origin = new Vector3(player.transform.position.x, Top, player.transform.position.z);
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "ProbePlatform";
            platform.transform.localScale = new Vector3(600f, 1f, 600f);
            platform.transform.position = origin + Vector3.down * 0.5f;
            body.enabled = false;
            player.transform.SetPositionAndRotation(origin + Vector3.up * 0.05f, Quaternion.identity);
            body.enabled = true;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.25f);
            Check(Mathf.Abs(player.transform.position.y - Top) < 0.2f, $"Player stands on the test platform (y={player.transform.position.y:F2})");

            gun = found;
            Check(gun.Hand == WeaponHand.Melee && !melee.suppressed, "Starts with the melee hand");
            Check(!gun.Hud.CardVisible, "Ammo HUD is hidden with the melee hand");
            Check(gun.Hud.Root.GetComponentInChildren<GraphicRaycaster>(true) == null, "Ammo HUD has no raycaster");
            bool anyTarget = false;
            foreach (Graphic graphic in gun.Hud.Root.GetComponentsInChildren<Graphic>(true)) anyTarget |= graphic.raycastTarget;
            Check(!anyTarget, "Ammo HUD graphics can't take clicks");
            Check(!gun.SelectHand(WeaponHand.Ranged) && gun.Hand == WeaponHand.Melee, "Can't raise a ranged hand with nothing equipped");
            Check(!gun.TryFire(), "Can't fire with nothing equipped");
            Check(gun.SaveKey == "weapons", "Save key");
            foreach (string id in AllWeapons)
            {
                ItemSpec spec = ItemCatalog.Spec(id);
                float reload = RangedWeaponProfile.ReloadSeconds(id);
                Check(spec != null && spec.ranged && reload >= 1.2f && reload <= 2.8f, $"{id} is a ranged item with a reload of {reload} s");
            }
        }

        private Vector3 At(float x, float z, float y = 0f) => origin + new Vector3(x, y, z);

        private EnemyAI Raider(float x, float z, float health = RaiderHealth)
        {
            GameObject go = Instantiate(ProceduralWorld.Active.assets.raiderPrefab);
            go.name = "ProbeRaider";
            foreach (MonoBehaviour behaviour in go.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            CharacterController controller = go.GetComponent<CharacterController>();
            controller.enabled = true;
            go.transform.position = At(x, z, 0.05f - (controller.center.y - controller.height * 0.5f));
            EnemyAI enemy = go.GetComponent<EnemyAI>();
            enemy.maxHealth = health;
            enemy.health = health;
            spawned.Add(go);
            Physics.SyncTransforms();
            return enemy;
        }

        private GameObject Wall(float x, float z, Vector3 size)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "ProbeWall";
            wall.transform.localScale = size;
            wall.transform.position = At(x, z, size.y * 0.5f);
            spawned.Add(wall);
            Physics.SyncTransforms();
            return wall;
        }

        private void ClearSpawned()
        {
            foreach (GameObject go in spawned)
                if (go != null) { go.SetActive(false); Destroy(go); }
            spawned.Clear();
            Physics.SyncTransforms();
        }

        private void Aim(Vector3 target)
        {
            Vector3 direction = target - view.transform.position;
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude > 0.0001f) player.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
            player.RestoreView(-Mathf.Asin(direction.normalized.y) * Mathf.Rad2Deg);
            Physics.SyncTransforms();
        }

        private void AimAt(EnemyAI enemy) => Aim(enemy.GetComponent<CharacterController>().bounds.center);

        private void AimAtNothing() => Aim(At(0f, 300f, 60f));

        private void Give(string id, int ammo = -1)
        {
            inventory.SetCount(id, 1);
            ItemSpec spec = ItemCatalog.Spec(id);
            if (ammo >= 0) inventory.SetCount(spec.ammoId, ammo);
            gear.Equip(id, out _);
        }

        private bool Shot()
        {
            gun.DebugResetCooldown();
            return gun.TryFire();
        }

        private IEnumerator WaitReload()
        {
            for (float t = 0f; gun.IsReloading && t < 5f; t += Time.deltaTime) yield return null;
        }

        private IEnumerator WaitBolts()
        {
            for (float t = 0f; gun.ActiveBolts > 0 && t < 4f; t += Time.deltaTime) yield return null;
        }

        private static bool Same(float a, float b) => Mathf.Abs(a - b) < 0.01f;

        // ---------- Sections ----------

        private IEnumerator CheckNoAmmo()
        {
            Give("pistol", 0);
            Check(gun.Hand == WeaponHand.Ranged && gun.WeaponId == "pistol", "Equipping a ranged weapon raises it");
            Check(melee.suppressed, "Melee is suppressed while it's raised");
            int before = gun.ShotsFired;
            Check(!Shot() && gun.RoundsIn("pistol") == 0 && gun.ShotsFired == before, "Can't fire with an empty magazine");
            Check(!gun.TryReload() && !gun.IsReloading, "Can't reload with no ammo");
            yield return null;
            Check(gun.Hud.CardVisible, "Ammo HUD shows with the ranged hand");
            Check(gun.Hud.AmmoText == "Pistol 0/12 (0)", $"Ammo text ({gun.Hud.AmmoText})");
        }

        private IEnumerator CheckAccounting()
        {
            AimAtNothing();
            Give("pistol", 31);
            float started = Time.time;
            Check(gun.TryReload() && gun.IsReloading, "Reload starts");
            Check(!gun.TryReload(), "A second reload doesn't stack");
            Check(inventory.GetCount("ammo_9mm") == 31, "Nothing is taken until the reload finishes");
            Check(!Shot(), "Can't fire while reloading");
            body.Move(Vector3.right * 0.3f);
            yield return null;
            Check(gun.IsReloading, "Moving doesn't cancel a reload");
            yield return WaitReload();
            Check(Time.time - started >= 1.3f && Time.time - started < 1.9f, $"Pistol reload took its time ({Time.time - started:F2} s)");
            Check(gun.RoundsIn("pistol") == 12 && inventory.GetCount("ammo_9mm") == 19, $"Reload moved exactly 12 rounds ({gun.RoundsIn("pistol")}, {inventory.GetCount("ammo_9mm")})");
            Check(!gun.TryReload(), "Can't reload a full magazine");

            for (int i = 0; i < 5; i++) Check(Shot(), "Pistol fires");
            Check(gun.RoundsIn("pistol") == 7 && inventory.GetCount("ammo_9mm") == 19, "Firing only spends the magazine");
            Check(gun.TryReload(), "Top-up starts");
            yield return WaitReload();
            Check(gun.RoundsIn("pistol") == 12 && inventory.GetCount("ammo_9mm") == 14, "A top-up takes exactly what's missing");

            Check(gun.TryReload() == false, "Full again");
            Shot();
            Check(gun.TryReload(), "Reload before swapping");
            Give("smg");
            Check(!gun.IsReloading && inventory.GetCount("ammo_9mm") == 14, "Swapping weapon cancels a reload at no cost");
            Check(gun.RoundsIn("pistol") == 11, "Swapping keeps the magazine");
            Check(gun.TryReload(), "SMG reload starts");
            yield return WaitReload();
            Check(gun.RoundsIn("smg") == 14 && inventory.GetCount("ammo_9mm") == 0, "A short reserve loads what there is");
            Give("pistol");
            Check(gun.RoundsIn("pistol") == 11 && gun.RoundsIn("smg") == 14, "Both magazines survive the swap");
            Check(gun.RoundsIn("pistol") + gun.RoundsIn("smg") + inventory.GetCount("ammo_9mm") == 31 - 6, "Total rounds add up: 31 less the 6 fired");

            inventory.SetCount("ammo_9mm", 10);
            Check(gun.TryReload(), "Reload before switching hand");
            Check(gun.SelectHand(WeaponHand.Melee) && !gun.IsReloading, "Switching hand cancels a reload");
            yield return new WaitForSeconds(0.1f);
            Check(inventory.GetCount("ammo_9mm") == 10 && gun.RoundsIn("pistol") == 11, "A cancelled reload moved nothing");
            Check(gun.SelectHand(WeaponHand.Ranged), "Raise it again");

            inventory.SetCount("ammo_9mm", 10);
            Check(gun.TryReload(), "Reload with the reserve about to vanish");
            inventory.SetCount("ammo_9mm", 0);
            yield return WaitReload();
            Check(gun.RoundsIn("pistol") == 11 && inventory.GetCount("ammo_9mm") == 0, "A reserve lost mid-reload creates no rounds");
        }

        private IEnumerator CheckRatesAndViewmodels()
        {
            const float Seconds = 2.2f;
            AimAtNothing();
            foreach (string id in AllWeapons)
            {
                ItemSpec spec = ItemCatalog.Spec(id);
                Give(id);
                yield return null;
                yield return null;
                RangedViewmodel model = gun.Viewmodel;
                Check(model.ShownId == id && model.Root.gameObject.activeSelf && model.Root.GetComponentsInChildren<Renderer>().Length > 0, $"{id} has a visible model");
                Check(model.Root.GetComponentsInChildren<Collider>(true).Length == 0, $"{id} model has no colliders");
                Check(model.NearestViewDepth >= view.nearClipPlane, $"{id} model stays past the near plane ({model.NearestViewDepth:F2} >= {view.nearClipPlane:F2})");
                bool shadows = false;
                foreach (Renderer renderer in model.Root.GetComponentsInChildren<Renderer>()) shadows |= renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off;
                Check(!shadows, $"{id} model casts no shadow");

                gun.DebugSetRounds(id, spec.magazine);
                gun.DebugResetCooldown();
                bool automatic = RangedWeaponProfile.IsAutomatic(spec);
                Check(automatic == (spec.roundsPerMinute >= 300f), $"{id} automatic = {automatic}");
                float start = Time.time;
                gun.debugTriggerHeld = automatic;
                while (Time.time - start < Seconds)
                {
                    if (!automatic) gun.TryFire();
                    yield return null;
                }
                gun.debugTriggerHeld = false;
                float elapsed = Time.time - start;
                int shots = spec.magazine - gun.RoundsIn(id);
                float expected = spec.roundsPerMinute / 60f * elapsed;
                Check(Mathf.Abs(shots - expected) <= 1.5f + expected * 0.15f, $"{id} rate: {shots} shots in {elapsed:F2} s, expected about {expected:F1}");
                yield return WaitBolts();
            }

            // A semi-automatic trigger gives one shot per press however long it's held.
            Give("pistol");
            gun.DebugSetRounds("pistol", 12);
            gun.DebugResetCooldown();
            gun.debugTriggerHeld = true;
            yield return new WaitForSeconds(0.8f);
            gun.debugTriggerHeld = false;
            Check(gun.RoundsIn("pistol") == 11, $"Holding the pistol's trigger fires once ({12 - gun.RoundsIn("pistol")})");
            yield return null;
        }

        private IEnumerator CheckDamage()
        {
            Give("pistol");
            gun.DebugSetRounds("pistol", 12);
            inventory.SetCount("ammo_9mm", 3);

            EnemyAI raider = Raider(0f, 6f);
            AimAt(raider);
            Check(Shot() && Same(raider.health, RaiderHealth - 22f), $"A pistol round does 22 ({RaiderHealth - raider.health})");
            Check(gun.LastShotHits == 1 && gun.Fx.ActiveLines > 0 && gun.Fx.FlashVisible, "Hit is counted, a tracer and a muzzle flash show");
            yield return null;
            Check(gun.Hud.MarkerVisible, "Hit marker shows");
            Check(gun.Hud.AmmoText == "Pistol 11/12 (3)", $"Ammo text after a shot ({gun.Hud.AmmoText})");
            yield return new WaitForSecondsRealtime(0.2f);
            Check(!gun.Hud.MarkerVisible, "Hit marker goes away");
            Check(gun.Fx.ActiveLines == 0 && !gun.Fx.FlashVisible, "Tracer and flash go away");
            ClearSpawned();
            yield return null;

            // Range.
            raider = Raider(0f, 70f);
            AimAt(raider);
            Shot();
            Check(Same(raider.health, RaiderHealth), "A pistol doesn't reach 70 m");
            Give("hunting_rifle");
            gun.DebugSetRounds("hunting_rifle", 5);
            Check(Shot() && Same(raider.health, RaiderHealth - 68f), "A hunting rifle does");
            ClearSpawned();
            yield return null;

            // Walls.
            Give("pistol");
            gun.DebugSetRounds("pistol", 12);
            Wall(0f, 3f, new Vector3(3f, 4f, 0.3f));
            raider = Raider(0f, 6f);
            AimAt(raider);
            for (int i = 0; i < 3; i++) Shot();
            Check(Same(raider.health, RaiderHealth) && gun.RoundsIn("pistol") == 9, "A wall stops rounds, and they're still spent");
            ClearSpawned();
            yield return null;

            Wall(1.9f, 3f, new Vector3(2f, 4f, 0.3f));
            raider = Raider(0f, 6f);
            AimAt(raider);
            Shot();
            Check(Same(raider.health, RaiderHealth - 22f), "A raider beside a wall is hit");
            ClearSpawned();
            yield return null;

            Wall(0f, 6f, new Vector3(3f, 4f, 0.3f));
            raider = Raider(0f, 4.5f);
            AimAt(raider);
            Shot();
            Check(Same(raider.health, RaiderHealth - 22f), "A raider in front of a wall is hit");
            ClearSpawned();
            yield return null;

            // Nearest wins, and a corpse doesn't stop the next round.
            EnemyAI front = Raider(0f, 4f, 10f);
            EnemyAI back = Raider(0f, 7f);
            AimAt(front);
            Shot();
            Check(front.IsDead && gun.LastShotHits == 1 && Same(back.health, RaiderHealth), "The nearest raider takes the round and dies");
            Shot();
            Check(Same(back.health, RaiderHealth - 22f), "The next round passes the fresh corpse");
            ClearSpawned();
            yield return null;

            // Pellets.
            Give("shotgun");
            gun.DebugSetRounds("shotgun", 6);
            raider = Raider(0f, 3f);
            AimAt(raider);
            Check(Shot() && gun.LastShotPellets == 8 && gun.LastShotHits == 8 && Same(raider.health, RaiderHealth - 104f),
                $"A shotgun fires 8 pellets of 13 at 3 m ({gun.LastShotHits} hits, {RaiderHealth - raider.health} damage)");
            Check(gun.RoundsIn("shotgun") == 5, "A shotgun blast spends one shell");
            ClearSpawned();
            yield return null;

            // Animals.
            WorldWildlife deer = WorldWildlife.SpawnForTest(null, At(0f, 5f), 0);
            spawned.Add(deer.gameObject);
            Physics.SyncTransforms();
            Give("pistol");
            gun.DebugSetRounds("pistol", 12);
            Aim(deer.GetComponent<Collider>().bounds.center);
            Shot();
            Check(Same(deer.Health, 38f), $"A deer takes pistol rounds ({deer.Health})");
            Give("hunting_rifle");
            gun.DebugSetRounds("hunting_rifle", 5);
            Shot();
            Check(deer.IsDead, "A deer can be killed at range");
            yield return null;

            // Structures have no damage path.
            ClearSpawned();
            yield return null;
            GameObject piece = Wall(0f, 4f, new Vector3(3f, 4f, 0.3f));
            StructureHealth structure = piece.AddComponent<StructureHealth>();
            Give("assault_rifle");
            gun.DebugSetRounds("assault_rifle", 25);
            Aim(At(0f, 4f, 1.5f));
            for (int i = 0; i < 5; i++) Shot();
            Give("plasma_rifle");
            gun.DebugSetRounds("plasma_rifle", 12);
            Shot();
            yield return WaitBolts();
            Check(structure.Health < structure.maxHealth, "Gunfire and plasma damage build pieces (M27-M28 structure damage rules)");
        }

        private IEnumerator CheckHands()
        {
            melee.attackDamage = MeleeAttacker.DefaultDamage;
            Give("machete");
            Check(Same(melee.attackDamage, 46f) && gun.Hand == WeaponHand.Melee, "Equipping a melee weapon sets its damage and raises it");
            Give("pistol");
            gun.DebugSetRounds("pistol", 12);
            Check(Same(melee.attackDamage, 46f) && gear.WeaponId == "machete" && gear.RangedId == "pistol", "Equipping a ranged weapon leaves melee alone");
            Check(gear.AttackDamage == 46f, "Melee damage is still the machete's");

            EnemyAI raider = Raider(0f, 2f);
            AimAt(raider);
            Transform prop = melee.weaponVisual;
            Check(prop == null || !prop.gameObject.activeSelf, "The melee prop is hidden with the ranged hand");
            raider.enabled = true; // MeleeAttacker only targets an enabled AI; no frame passes before it's off again.
            bool swung = melee.TryAttack();
            raider.enabled = false;
            Check(!swung && Same(raider.health, RaiderHealth), "Melee does nothing while the ranged hand is raised");

            Check(gun.SelectHand(WeaponHand.Melee) && gun.Hand == WeaponHand.Melee && !melee.suppressed, "1 goes back to melee");
            Check(prop == null || prop.gameObject.activeSelf, "The melee prop is back");
            Check(!gun.Viewmodel.Root.gameObject.activeSelf, "The gun is put away");
            yield return null;
            Check(!gun.Hud.CardVisible, "Ammo HUD hides");
            int rounds = gun.RoundsIn("pistol");
            Check(!Shot() && gun.RoundsIn("pistol") == rounds && Same(raider.health, RaiderHealth), "Guns do nothing with the melee hand");
            gun.debugTriggerHeld = true;
            yield return null;
            yield return null;
            gun.debugTriggerHeld = false;
            Check(gun.RoundsIn("pistol") == rounds, "The trigger does nothing with the melee hand");
            raider.enabled = true;
            swung = melee.TryAttack();
            raider.enabled = false;
            Check(swung && Same(raider.health, RaiderHealth - 46f), $"Melee works again and does 46 ({RaiderHealth - raider.health})");

            Check(gun.SelectHand(WeaponHand.Ranged) && melee.suppressed, "2 raises the gun");
            gear.Unequip(GearSlot.Ranged);
            yield return null;
            yield return null;
            Check(gun.Hand == WeaponHand.Melee && !melee.suppressed && gun.WeaponId == "", "Unequipping drops back to melee");
            Check(Same(melee.attackDamage, 46f), "Unequipping a gun leaves melee damage alone");
            gear.Unequip(GearSlot.Weapon);
            Check(Same(melee.attackDamage, MeleeAttacker.DefaultDamage), "Melee slot still unequips");
        }

        private IEnumerator CheckGates()
        {
            Give("pistol", 5);
            gun.DebugSetRounds("pistol", 6);
            AimAtNothing();

            Time.timeScale = 0f;
            Check(!Shot() && !gun.TryReload() && !gun.SelectHand(WeaponHand.Melee), "Paused: no fire, reload or hand switch");
            gun.debugTriggerHeld = true;
            yield return null;
            yield return null;
            gun.debugTriggerHeld = false;
            Time.timeScale = 1f;
            yield return null;
            Check(gun.RoundsIn("pistol") == 6, "Paused: the trigger does nothing");

            Check(build.BeginBuild(0) && build.IsBuildModeActive, "Enter build mode");
            Check(!Shot() && !gun.TryReload() && !gun.SelectHand(WeaponHand.Melee), "Build mode: no fire, reload or hand switch");
            gun.debugTriggerHeld = true;
            yield return null;
            yield return null;
            gun.debugTriggerHeld = false;
            Check(gun.RoundsIn("pistol") == 6 && !gun.IsReloading, "Build mode: the trigger does nothing");
            build.ExitBuildMode();
            yield return null;
            Check(Shot() && gun.RoundsIn("pistol") == 5, "Fires again after leaving build mode");

            gun.ignoreCursorLock = false;
            CursorLockMode previous = Cursor.lockState;
            Cursor.lockState = CursorLockMode.None;
            if (Cursor.lockState != CursorLockMode.Locked) Check(!Shot() && gun.RoundsIn("pistol") == 5, "No fire with a free cursor");
            Cursor.lockState = previous;
            gun.ignoreCursorLock = true;
        }

        private IEnumerator CheckEnergy()
        {
            AimAtNothing();
            Give("laser_pistol", 3);
            gun.DebugSetRounds("laser_pistol", 0);
            Check(gun.TryReload(), "Energy reload starts");
            yield return WaitReload();
            Check(gun.RoundsIn("laser_pistol") == 20 && inventory.GetCount("energy_cell") == 2, "One cell fills the whole magazine");
            for (int i = 0; i < 3; i++) Shot();
            Check(gun.RoundsIn("laser_pistol") == 17 && !gun.TryReload() && inventory.GetCount("energy_cell") == 2, "No reload at over half: it would waste a cell");
            gun.DebugSetRounds("laser_pistol", 10);
            Check(!gun.TryReload(), "No reload at exactly half");
            gun.DebugSetRounds("laser_pistol", 9);
            Check(gun.TryReload(), "Reload under half");
            yield return WaitReload();
            Check(gun.RoundsIn("laser_pistol") == 20 && inventory.GetCount("energy_cell") == 1, "Spends exactly one more cell");
            inventory.SetCount("energy_cell", 0);
            gun.DebugSetRounds("laser_pistol", 0);
            Check(!gun.TryReload(), "No cells, no reload");
            Check(!Shot(), "No charge, no shot");

            gun.DebugSetRounds("laser_pistol", 20);
            EnemyAI raider = Raider(0f, 10f);
            AimAt(raider);
            float heatBefore = gun.HeatOf("laser_pistol");
            Check(Shot() && Same(raider.health, RaiderHealth - 27f), "A laser does its damage instantly");
            Check(gun.HeatOf("laser_pistol") > heatBefore, "A shot adds heat");
            ClearSpawned();
            yield return new WaitForSeconds(0.1f);

            AimAtNothing();
            Give("laser_rifle");
            gun.DebugSetRounds("laser_rifle", 30);
            gun.DebugResetCooldown();
            gun.debugTriggerHeld = true;
            for (float t = 0f; !gun.IsOverheated("laser_rifle") && t < 8f; t += Time.deltaTime) yield return null;
            gun.debugTriggerHeld = false;
            Check(gun.IsOverheated("laser_rifle"), "Sustained fire overheats a laser rifle");
            Check(gun.RoundsIn("laser_rifle") == 10, $"It locks after 20 shots of 5 heat ({30 - gun.RoundsIn("laser_rifle")})");
            int rounds = gun.RoundsIn("laser_rifle");
            Check(!Shot() && gun.RoundsIn("laser_rifle") == rounds, "An overheated weapon refuses to fire");
            yield return null;
            Check(gun.Hud.StateText == "OVERHEATED", $"HUD says so ({gun.Hud.StateText})");
            float start = Time.time;
            for (float t = 0f; gun.IsOverheated("laser_rifle") && t < 9f; t += Time.deltaTime) yield return null;
            float took = Time.time - start;
            Check(!gun.IsOverheated("laser_rifle") && gun.HeatOf("laser_rifle") <= RangedWeaponController.HeatRecoverAt + 0.5f, $"Recovers at 35 heat ({gun.HeatOf("laser_rifle"):F1})");
            Check(took > 4.5f && took < 6.5f, $"Cools at about 12 per second ({took:F1} s for 65 heat)");
            Check(Shot(), "Fires again after recovering");
        }

        private IEnumerator CheckPlasma()
        {
            Give("plasma_rifle", 2);
            gun.DebugSetRounds("plasma_rifle", 12);

            EnemyAI first = Raider(0f, 8f);
            EnemyAI second = Raider(1.6f, 8f);
            AimAt(first);
            Check(Shot() && gun.ActiveBolts == 1, "A plasma bolt is launched");
            Check(Same(first.health, RaiderHealth), "It's a travelling bolt: nothing is hit yet");
            yield return WaitBolts();
            Check(gun.ActiveBolts == 0, "The bolt lands");
            Check(Same(first.health, RaiderHealth - 85f), $"Direct hit does full damage once ({RaiderHealth - first.health})");
            float splash = RaiderHealth - second.health;
            Check(splash > 40f && splash <= 85f, $"Splash hits the second raider ({splash:F1})");
            yield return null;
            Check(gun.Hud.MarkerVisible, "Hit marker shows for a burst");
            ClearSpawned();
            yield return null;

            first = Raider(0f, 8f);
            second = Raider(1.8f, 8f);
            Wall(0.9f, 8f, new Vector3(0.2f, 4f, 6f));
            AimAt(first);
            Shot();
            yield return WaitBolts();
            Check(Same(first.health, RaiderHealth - 85f) && Same(second.health, RaiderHealth), $"A wall shields a raider from splash ({RaiderHealth - second.health})");
            ClearSpawned();
            yield return null;

            Wall(0f, 6f, new Vector3(4f, 4f, 0.3f));
            EnemyAI behind = Raider(0f, 7.2f);
            EnemyAI beside = Raider(1.2f, 5f);
            Aim(At(0f, 6f, 1.2f));
            Shot();
            yield return WaitBolts();
            Check(Same(behind.health, RaiderHealth), "A burst on a wall doesn't reach behind it");
            Check(beside.health < RaiderHealth, "A burst on a wall hits a raider on the near side");
            ClearSpawned();
            yield return null;

            // At the player's own feet: the design choice is no self damage.
            float health = stats.health;
            Aim(At(0f, 1.2f));
            Shot();
            yield return WaitBolts();
            Check(Same(stats.health, health), "A burst at your feet doesn't hurt you");

            // Out of range: no burst, no damage, the bolt doesn't live forever.
            Give("plasma_rifle");
            first = Raider(0f, 90f);
            AimAt(first);
            Shot();
            yield return WaitBolts();
            Check(gun.ActiveBolts == 0 && Same(first.health, RaiderHealth), "A bolt fizzles at its range");
        }

        private IEnumerator CheckSaves()
        {
            AimAtNothing();
            Give("pistol");
            gun.DebugSetRounds("pistol", 7);
            inventory.SetCount("smg", 1);
            gun.DebugSetRounds("smg", 14);
            Give("laser_rifle");
            gun.DebugSetRounds("laser_rifle", 30);
            for (int i = 0; i < 5; i++) Shot();
            float heat = gun.HeatOf("laser_rifle");
            string json = gun.CaptureState();
            gun.DebugSetRounds("pistol", 0);
            gun.DebugSetRounds("smg", 0);
            gun.DebugSetRounds("laser_rifle", 1);
            gun.SelectHand(WeaponHand.Melee);
            gun.RestoreState(json);
            Check(gun.RoundsIn("pistol") == 7 && gun.RoundsIn("smg") == 14 && gun.RoundsIn("laser_rifle") == 25, "Restore brings every magazine back");
            Check(Same(gun.HeatOf("laser_rifle"), heat) && heat > 20f, $"Restore brings heat back ({heat:F1})");
            Check(gun.Hand == WeaponHand.Ranged && melee.suppressed && gun.WeaponId == "laser_rifle", "Restore raises the saved hand");
            yield return null;
            Check(gun.Viewmodel.Root.gameObject.activeSelf, "The gun is shown after a restore");

            Give("pistol", 5);
            Check(gun.TryReload(), "Reload before a load");
            gun.RestoreState(json);
            Check(!gun.IsReloading && inventory.GetCount("ammo_9mm") == 5, "A load cancels a reload at no cost");

            Give("plasma_rifle");
            gun.DebugSetRounds("plasma_rifle", 12);
            Shot();
            gun.RestoreState(json);
            Check(gun.ActiveBolts == 0, "A load clears bolts in flight");

            gun.RestoreState("{\"magazines\":[{\"id\":\"pistol\",\"rounds\":999,\"heat\":50.0,\"locked\":true},{\"id\":\"nonsense\",\"rounds\":3},{\"id\":\"laser_pistol\",\"rounds\":-4,\"heat\":999.0,\"locked\":true}],\"hand\":1}");
            Check(gun.RoundsIn("pistol") == 12 && Same(gun.HeatOf("pistol"), 0f) && !gun.IsOverheated("pistol"), "Restore clamps a bad firearm entry");
            Check(gun.RoundsIn("nonsense") == 0, "Restore ignores an unknown weapon");
            Check(gun.RoundsIn("laser_pistol") == 0 && Same(gun.HeatOf("laser_pistol"), 100f) && gun.IsOverheated("laser_pistol"), "Restore clamps a bad energy entry");

            gun.RestoreState("{not json");
            Check(gun.RoundsIn("pistol") == 0 && gun.Hand == WeaponHand.Melee, "A broken section means a clean state, not an exception");
            Give("pistol");
            gun.DebugSetRounds("pistol", 3);
            gun.RestoreState(null);
            Check(gun.RoundsIn("pistol") == 0 && gun.Hand == WeaponHand.Melee && !melee.suppressed, "A save with no section resets the system");

            List<string> ids = gear.CaptureEquipped();
            Check(ids.Contains("pistol"), "Gear saves the ranged slot");
            float meleeDamage = melee.attackDamage;
            gear.Unequip(GearSlot.Ranged);
            gear.RestoreEquipped(ids);
            Check(gear.RangedId == "pistol" && Same(melee.attackDamage, meleeDamage), "Gear restores the ranged slot, melee is unchanged");
            gun.RestoreState(null);
            Check(gun.Hand == WeaponHand.Melee, "Restoring gear alone doesn't raise a hand");
        }

        private IEnumerator CheckLostWeapon()
        {
            Give("pistol", 4);
            gun.DebugSetRounds("pistol", 9);
            inventory.SetCount("pistol", 0);
            yield return null;
            yield return null;
            Check(gear.RangedId == "" && gun.Hand == WeaponHand.Melee && !melee.suppressed, "A ranged weapon that's gone is unequipped");
            Check(gun.RoundsIn("pistol") == 0 && inventory.GetCount("ammo_9mm") == 13, $"Its loaded rounds come back ({inventory.GetCount("ammo_9mm")})");

            Give("laser_pistol");
            gun.DebugSetRounds("laser_pistol", 8);
            inventory.SetCount("laser_pistol", 0);
            yield return null;
            yield return null;
            Check(gear.RangedId == "" && gun.RoundsIn("laser_pistol") == 8, "An energy weapon keeps its charge");
        }

        private IEnumerator CheckDeath()
        {
            Give("pistol", 5);
            gun.DebugSetRounds("pistol", 6);
            Check(gun.TryReload(), "Reload before dying");
            stats.TakeDamage(1000000f);
            Check(stats.IsDead, "Player is dead");
            yield return null;
            yield return null;
            Check(!gun.IsReloading && inventory.GetCount("ammo_9mm") == 5, "Death cancels a reload at no cost");
            Check(!Shot() && gun.RoundsIn("pistol") == 6 && !gun.TryReload(), "Dead: no fire, no reload");
            Check(!gun.SelectHand(WeaponHand.Melee), "Dead: no hand switch");
            Check(!gun.Hud.CardVisible, "Dead: no ammo HUD");
            stats.RestoreVitals(100f, 100f, 100f);
            yield return new WaitForSecondsRealtime(0.1f);
            Check(Time.timeScale > 0f && Shot() && gun.RoundsIn("pistol") == 5, "Revived by a load: fires again");
        }

        private void Check(bool condition, string message)
        {
            if (!condition) failures++;
            Debug.Log((condition ? "[WeaponsProbe] PASS: " : "[WeaponsProbe] FAIL: ") + message);
        }
    }
}
