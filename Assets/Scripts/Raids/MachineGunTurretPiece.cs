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
}
