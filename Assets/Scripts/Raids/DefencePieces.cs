using SheltreSim.Audio;
using SheltreSim.Building;
using SheltreSim.Enemies;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Power;
using SheltreSim.Saving;
using SheltreSim.Storage;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Raids
{
    /// <summary>Where a turret shoots from: its Head if the model has one (barrel along forward), otherwise a fixed spot.</summary>
    internal static class TurretRig
    {
        public static Transform FindHead(Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name == "Head") return t;
            return null;
        }

        public static Vector3 Muzzle(Transform root, Transform head, float fallbackHeight)
        {
            return head != null ? head.position + head.forward * 0.55f : root.position + Vector3.up * fallbackHeight;
        }

        public static void Aim(Transform head, Vector3 target, float dt)
        {
            if (head == null) return;
            Vector3 flat = target - head.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) return;
            head.rotation = Quaternion.RotateTowards(head.rotation, Quaternion.LookRotation(flat), 360f * dt);
        }
    }

    /// <summary>
    /// Fires at the nearest raider it can see. Needs power and rifle rounds. Damage is dealt to the selected target only, and the
    /// player in the line of fire counts as a blocker, so it cannot hurt the player.
    /// </summary>
    public sealed class MachineGunTurretPiece : MonoBehaviour, IPowerConsumer, IInteractable, IPieceState, IRaidPriorityTarget
    {
        public const string AmmoId = "ammo_rifle";
        public float range = 22f;
        public float damage = 9f;
        public float roundsPerSecond = 6f;
        public float burstSeconds = 1.5f;
        public float burstPause = 0.6f;
        public int magazine = 60;
        public float crateRange = 8f;
        public float powerDemand = 0.4f;

        public int Rounds { get; private set; }
        public bool Powered { get; private set; }
        public int ShotsFired { get; private set; }
        public EnemyAI Target { get; private set; }

        private Transform head;
        private float nextSearch;
        private float nextShot;
        private float burstEnds;
        private float pauseEnds;
        private float nextCrateCheck;

        public Vector3 PowerPosition => transform.position;
        public float PowerDemand => powerDemand;
        public int PowerPriority => 0;
        public float HeatOutput => 0f;
        public float CoolingOutput => 0f;
        public string PowerName => "Machine Gun Turret";
        public void SetPowered(bool powered) => Powered = powered;

        /// <summary>Probes: a charged magazine without going through the pack.</summary>
        public void DebugSetRounds(int rounds) => Rounds = Mathf.Clamp(rounds, 0, magazine);

        private void Awake() => head = TurretRig.FindHead(transform);
        private void OnEnable() => PowerGrid.Register(this);
        private void OnDisable()
        {
            PowerGrid.Unregister(this);
            Powered = false;
        }

        public string InteractPrompt
        {
            get
            {
                if (Rounds <= 0) return Powered ? "Turret: no ammo. Press E to load Rifle Rounds" : "Turret: no ammo, no power. Press E to load Rifle Rounds";
                if (!Powered) return $"Turret: no power ({Rounds}/{magazine} rounds). Press E to load";
                return Rounds < magazine ? $"Turret: {Rounds}/{magazine} rounds. Press E to load" : $"Turret: ready, {Rounds}/{magazine} rounds";
            }
        }

        public void Interact(GameObject interactor)
        {
            if (Time.timeScale <= 0f || interactor == null) return;
            Inventory inventory = interactor.GetComponentInParent<Inventory>();
            if (inventory == null) return;
            int want = Mathf.Min(magazine - Rounds, inventory.GetCount(AmmoId));
            if (want <= 0)
            {
                GameAudio.Play2D(Sfx.UiDeny, 0.35f);
                GameMessageBus.Post(Rounds >= magazine ? "The magazine is full." : "You have no Rifle Rounds.");
                return;
            }
            if (!inventory.TryConsume(AmmoId, want)) return;
            Rounds += want;
            GameAudio.PlayAt(Sfx.GunReload, transform.position, 0.8f, 18f);
            GameMessageBus.Post($"Loaded {want} rounds ({Rounds}/{magazine}).");
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (Rounds < magazine / 2 && Time.time >= nextCrateCheck)
            {
                nextCrateCheck = Time.time + 2f;
                LoadFromCrates();
            }
            if (!Powered || Rounds <= 0)
            {
                Target = null;
                return;
            }
            Vector3 muzzle = TurretRig.Muzzle(transform, head, 1.2f);
            if (Time.time >= nextSearch)
            {
                nextSearch = Time.time + 0.2f;
                Target = RaidSight.NearestTarget(muzzle, range, transform);
                if (Target == null) burstEnds = 0f;
            }
            if (Target == null || Target.IsDead) { Target = null; return; }
            TurretRig.Aim(head, Target.CenterPosition, Time.deltaTime);

            if (Time.time < pauseEnds) return;
            if (burstEnds <= 0f) burstEnds = Time.time + burstSeconds;
            if (Time.time >= burstEnds)
            {
                burstEnds = 0f;
                pauseEnds = Time.time + burstPause;
                return;
            }
            if (Time.time < nextShot) return;
            nextShot = Time.time + 1f / Mathf.Max(0.1f, roundsPerSecond);
            // Sight was checked up to 0.2 s ago, and a wall or a player can step in. Check it for this shot.
            if (!RaidSight.Clear(muzzle, Target.CenterPosition, transform, Target.transform))
            {
                Target = null;
                return;
            }
            Rounds--;
            ShotsFired++;
            GameAudio.PlayAt(Sfx.TurretFire, muzzle, 0.9f, 60f, 0.08f);
            RaidTracer.Show(muzzle, Target.CenterPosition, new Color(1f, 0.9f, 0.55f), 0.05f);
            Target.TakeDamage(damage);
        }

        private void LoadFromCrates()
        {
            int available = StorageRegistry.CountNearby(transform.position, crateRange, AmmoId);
            int want = Mathf.Min(magazine - Rounds, available);
            if (want > 0 && StorageRegistry.TryTakeNearby(transform.position, crateRange, AmmoId, want)) Rounds += want;
        }

        [System.Serializable] private class Saved { public int rounds; }
        public string CapturePieceState() => JsonUtility.ToJson(new Saved { rounds = Rounds });
        public void RestorePieceState(string state)
        {
            if (string.IsNullOrEmpty(state)) return;
            Rounds = Mathf.Clamp(JsonUtility.FromJson<Saved>(state).rounds, 0, magazine);
        }
    }

    /// <summary>A continuous beam. No ammunition but it draws 2 kW and heats the network, and it shuts off while that network is overheated.</summary>
    public sealed class LaserTurretPiece : MonoBehaviour, IPowerConsumer, IInteractable, IRaidPriorityTarget
    {
        public float range = 22f;
        public float damagePerSecond = 24f;
        public float firingDemand = 2f;
        public float idleDemand = 0.2f;
        public float firingHeat = 6f;

        public bool Powered { get; private set; }
        public bool Firing { get; private set; }
        public bool Overheated { get; private set; }
        public EnemyAI Target { get; private set; }
        public float DamageDealt { get; private set; }

        private Transform head;
        private LineRenderer beam;
        private float nextSearch;
        private float nextSound;
        private float pendingDamage;

        public Vector3 PowerPosition => transform.position;
        public float PowerDemand => Target != null && !Overheated ? firingDemand : idleDemand;
        public int PowerPriority => 0;
        public float HeatOutput => Firing ? firingHeat : 0f;
        public float CoolingOutput => 0f;
        public string PowerName => "Laser Turret";
        public void SetPowered(bool powered) => Powered = powered;

        public string InteractPrompt
        {
            get
            {
                if (Overheated) return "Laser Turret: shut down, the power network is overheated";
                if (!Powered) return "Laser Turret: no power";
                return Firing ? "Laser Turret: firing" : "Laser Turret: ready";
            }
        }

        public void Interact(GameObject interactor) => GameMessageBus.Post(InteractPrompt);

        private void Awake() => head = TurretRig.FindHead(transform);
        private void OnEnable() => PowerGrid.Register(this);
        private void OnDisable()
        {
            PowerGrid.Unregister(this);
            Powered = false;
            SetFiring(false);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            PowerNetwork network = PowerGrid.NetworkAt(transform.position);
            Overheated = network != null && network.Overheated;
            Vector3 muzzle = TurretRig.Muzzle(transform, head, 1.3f);
            if (Time.time >= nextSearch)
            {
                nextSearch = Time.time + 0.2f;
                Target = Overheated ? null : RaidSight.NearestTarget(muzzle, range, transform);
            }
            if (Target != null && Target.IsDead) Target = null;
            // The target decides what it asks for, so it's only firing once the grid has served the higher demand.
            if (Target == null || !Powered || Overheated)
            {
                SetFiring(false);
                return;
            }
            TurretRig.Aim(head, Target.CenterPosition, Time.deltaTime);
            SetFiring(true);
            beam.SetPosition(0, muzzle);
            beam.SetPosition(1, Target.CenterPosition);
            if (Time.time >= nextSound)
            {
                nextSound = Time.time + 0.5f;
                GameAudio.PlayAt(Sfx.TurretLaser, muzzle, 0.85f, 45f, 0.04f);
            }
            pendingDamage += damagePerSecond * Time.deltaTime;
            if (pendingDamage < 2f) return;
            float dealt = pendingDamage;
            pendingDamage = 0f;
            DamageDealt += dealt;
            Target.TakeDamage(dealt);
        }

        private void SetFiring(bool value)
        {
            if (value && beam == null)
            {
                var obj = new GameObject("Beam");
                obj.transform.SetParent(transform, false);
                beam = obj.AddComponent<LineRenderer>();
                beam.material = RaidTracer.LineMaterial();
                beam.useWorldSpace = true;
                beam.positionCount = 2;
                beam.startWidth = 0.06f;
                beam.endWidth = 0.03f;
                beam.startColor = new Color(1f, 0.25f, 0.2f);
                beam.endColor = new Color(1f, 0.5f, 0.3f, 0.7f);
                beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beam.receiveShadows = false;
            }
            if (beam != null && beam.enabled != value) beam.enabled = value;
            Firing = value;
        }
    }

    /// <summary>
    /// What makes a flat trap something you walk across: every solid collider it came with becomes a trigger, and a 4 cm plate
    /// (well under the step height) stays solid for the build tools' raycasts. Traps find their victims in EnemyAI.Live,
    /// which holds no player, instead of relying on trigger events.
    /// </summary>
    internal static class TrapRig
    {
        public static Vector3 MakeWalkable(GameObject piece)
        {
            Vector3 size = new Vector3(2f, 0.3f, 2f);
            BoxCollider rootBox = piece.GetComponent<BoxCollider>();
            if (rootBox != null) size = rootBox.size;
            foreach (Collider collider in piece.GetComponentsInChildren<Collider>(true)) collider.isTrigger = true;
            var plate = piece.AddComponent<BoxCollider>();
            plate.center = Vector3.up * 0.02f;
            plate.size = new Vector3(size.x, 0.04f, size.z);
            return size;
        }

        public static bool IsOnTrap(Transform trap, Vector3 size, EnemyAI enemy)
        {
            Vector3 local = trap.InverseTransformPoint(enemy.FeetPosition);
            return Mathf.Abs(local.x) <= size.x * 0.5f + 0.2f && Mathf.Abs(local.z) <= size.z * 0.5f + 0.2f && Mathf.Abs(local.y) < 1f;
        }
    }

    /// <summary>Hurts and slows raiders standing on it. Each hit wears it through StructureHealth: about 300 uses.</summary>
    public sealed class SpikeTrapPiece : MonoBehaviour, IRaidIgnored
    {
        public float damage = 15f;
        public float interval = 1f;
        public float slowFactor = 0.45f;
        public int uses = 300;

        public int HitsDealt { get; private set; }

        private Vector3 size;
        private StructureHealth structure;
        private float nextHit;

        private void Awake()
        {
            size = TrapRig.MakeWalkable(gameObject);
            structure = GetComponent<StructureHealth>();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            bool hitTime = Time.time >= nextHit;
            bool any = false;
            var live = EnemyAI.Live;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                EnemyAI enemy = live[i];
                if (enemy == null || enemy.IsDead || !TrapRig.IsOnTrap(transform, size, enemy)) continue;
                any = true;
                enemy.ApplySlow(slowFactor, 0.4f);
                if (hitTime)
                {
                    HitsDealt++;
                    enemy.TakeDamage(damage);
                    if (structure != null && uses > 0) structure.TakeDamage(structure.maxHealth / uses);
                    if (this == null) return;
                }
            }
            if (any && hitTime)
            {
                nextHit = Time.time + interval;
                GameAudio.PlayAt(Sfx.TrapSnap, transform.position, 0.85f, 25f, 0.1f);
            }
        }
    }

    /// <summary>Stuns and burns whoever is on it, when powered, then recharges.</summary>
    public sealed class ShockTrapPiece : MonoBehaviour, IPowerConsumer, IRaidIgnored
    {
        public float damage = 25f;
        public float stunSeconds = 2f;
        public float cooldown = 5f;
        public float powerDemand = 0.5f;

        public bool Powered { get; private set; }
        public int Fired { get; private set; }

        private Vector3 size;
        private float ready;

        public Vector3 PowerPosition => transform.position;
        public float PowerDemand => powerDemand;
        public int PowerPriority => 0;
        public float HeatOutput => 0f;
        public float CoolingOutput => 0f;
        public string PowerName => "Shock Trap";
        public void SetPowered(bool powered) => Powered = powered;

        private void Awake() => size = TrapRig.MakeWalkable(gameObject);
        private void OnEnable() => PowerGrid.Register(this);
        private void OnDisable()
        {
            PowerGrid.Unregister(this);
            Powered = false;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || !Powered || Time.time < ready) return;
            var live = EnemyAI.Live;
            bool any = false;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                EnemyAI enemy = live[i];
                if (enemy == null || enemy.IsDead || !TrapRig.IsOnTrap(transform, size, enemy)) continue;
                any = true;
                enemy.Stun(stunSeconds);
                enemy.TakeDamage(damage);
            }
            if (!any) return;
            Fired++;
            ready = Time.time + cooldown;
            GameAudio.PlayAt(Sfx.TrapShock, transform.position, 0.95f, 30f, 0.06f);
        }
    }

    /// <summary>With power: the warning comes earlier, a klaxon sounds as a raid begins, and the bunker door shuts if nobody's in it.</summary>
    public sealed class AlarmPiece : MonoBehaviour, IPowerConsumer, IInteractable
    {
        private static readonly System.Collections.Generic.List<AlarmPiece> all = new System.Collections.Generic.List<AlarmPiece>();

        public float powerDemand = 0.1f;
        public bool Powered { get; private set; }

        public Vector3 PowerPosition => transform.position;
        public float PowerDemand => powerDemand;
        public int PowerPriority => 0;
        public float HeatOutput => 0f;
        public float CoolingOutput => 0f;
        public string PowerName => "Alarm";
        public void SetPowered(bool powered) => Powered = powered;

        public string InteractPrompt => Powered ? "Alarm: armed" : "Alarm: no power";
        public void Interact(GameObject interactor) => GameMessageBus.Post(Powered ? "The alarm is armed: you get twice the warning before a raid." : "The alarm has no power.");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        private void OnEnable()
        {
            all.Add(this);
            PowerGrid.Register(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
            PowerGrid.Unregister(this);
            Powered = false;
        }

        public static bool AnyPoweredNear(Vector3 position, float radius)
        {
            foreach (AlarmPiece alarm in all)
                if (alarm.Powered && (alarm.transform.position - position).sqrMagnitude <= radius * radius) return true;
            return false;
        }

        public static void NotifyRaidBegan()
        {
            bool sounded = false;
            foreach (AlarmPiece alarm in all)
            {
                if (!alarm.Powered) continue;
                sounded = true;
                GameAudio.PlayAt(Sfx.AlarmKlaxon, alarm.transform.position, 1f, 80f);
            }
            if (!sounded) return;
            foreach (InteractableDoor door in Object.FindObjectsByType<InteractableDoor>(FindObjectsSortMode.None))
                if (door.TryAutoClose()) GameMessageBus.Post("The alarm shut the bunker door.");
        }
    }
}
