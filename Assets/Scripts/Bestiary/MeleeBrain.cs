using SheltreSim.Audio;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>
    /// The plain fighters. Wanderer: slow, raises both arms, stops, strikes. Scavenger: a raider with a pack, backs off briefly
    /// after a miss. Plated: a front plate (front hits do 40%), a heavy swing that leaves its flank open. Rootwalker: plants
    /// itself and swings in a 120 degree arc at 2.4 m; walk out of it and come back.
    /// </summary>
    public sealed class MeleeBrain : SpeciesBrain
    {
        public const float PlateFactor = 0.4f;
        public const float PlateHalfAngle = 60f;
        private Attack attack;
        private float backOffUntil;

        protected override void OnStart()
        {
            switch (Spec.Id)
            {
                case SpeciesId.Scavenger:
                    attack = Melee("jab", 0.5f, Spec.AttackRange, 45f, 1.0f, 1.2f, PoseKind.ArmsUp, PoseKind.Slam, Sfx.RaiderGrowl);
                    break;
                case SpeciesId.Plated:
                    attack = Melee("heavy swing", 0.8f, Spec.AttackRange, 50f, 1.4f, 1.8f, PoseKind.WindRight, PoseKind.SweepRight, Sfx.RaiderGrowl);
                    chaseTurnRate = 240f;
                    break;
                case SpeciesId.Rootwalker:
                    attack = Melee("wide swing", 1.0f, 2.4f, 60f, 1.3f, 1.7f, PoseKind.Planted, PoseKind.WideSwing, Sfx.RaiderGrowl);
                    attack.TurnRate = 0f; // planted: the swing goes where it faced when it rooted
                    attack.StartRange = 2.1f;
                    wanderRadius = 3f;
                    break;
                default:
                    attack = Melee("overhead", 0.8f, Spec.AttackRange, 45f, 1.0f, 1.4f, PoseKind.ArmsUp, PoseKind.Slam, Sfx.RaiderGrowl);
                    attack.TurnRate = 120f;
                    break;
            }
        }

        protected override Attack ChooseAttack(float distance) => attack;

        public override float FilterDamage(float amount, Vector3? from)
        {
            if (Spec.Id == SpeciesId.Plated && FromFront(from, PlateHalfAngle))
            {
                GameAudio.PlayAt(Sfx.HitBlock, transform.position + Vector3.up, 0.8f, 25f);
                return amount * PlateFactor;
            }
            return amount;
        }

        protected override void OnStruck(Attack a, bool hit)
        {
            if (Spec.Id == SpeciesId.Scavenger && !hit) backOffUntil = RecoverUntil;
        }

        protected override void TickEngaged(Vector3 toPlayer, float distance)
        {
            // The scavenger's miss: it backs off towards its pack during the recovery instead of standing.
            if (State == BrainState.Recover && Time.time < backOffUntil)
            {
                Detail = "backing off";
                Vector3 away = Flat(-toPlayer).normalized;
                WalkTo(transform.position + away * 2f, 0f, Spec.Speed * 0.7f);
                Face(toPlayer, 360f);
                if (Time.time >= RecoverUntil) backOffUntil = 0f;
                return;
            }
            base.TickEngaged(toPlayer, distance);
        }
    }
}
