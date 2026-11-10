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
}
