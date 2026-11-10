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
}
