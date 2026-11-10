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
                }
            }
            if (any && hitTime)
            {
                nextHit = Time.time + interval;
                GameAudio.PlayAt(Sfx.TrapSnap, transform.position, 0.85f, 25f, 0.1f);
            }
        }
    }
}
