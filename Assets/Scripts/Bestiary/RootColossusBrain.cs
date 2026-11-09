using SheltreSim.Audio;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>
    /// Boss 2, the Root Colossus: alternating left and right sweeps, each with a clear shoulder turn first; after the pair it
    /// buries both arms in the ground for a long counter-attack window.
    /// </summary>
    public sealed class RootColossusBrain : SpeciesBrain
    {
        public const float SweepWindup = 1.4f;
        public const float BuriedSeconds = 3.2f;
        public int Pairs { get; private set; }
        public bool Buried => State == BrainState.Recover && CurrentAttack == right && burying;
        private Attack left, right;
        private bool nextIsRight;
        private bool burying;

        protected override void OnStart()
        {
            left = Melee("left sweep", SweepWindup, Spec.AttackRange, 75f, 0.7f, 0.7f, PoseKind.WindLeft, PoseKind.SweepLeft, Sfx.RaiderGrowl);
            right = Melee("right sweep", SweepWindup, Spec.AttackRange, 75f, BuriedSeconds, BuriedSeconds, PoseKind.WindRight, PoseKind.SweepRight, Sfx.RaiderGrowl);
            left.TurnRate = right.TurnRate = 60f;
            left.WindVolume = right.WindVolume = 1f;
            right.RecoverPose = PoseKind.Buried;
            chaseTurnRate = 120f;
        }

        protected override Attack ChooseAttack(float distance) => nextIsRight ? right : left;

        protected override void OnStruck(Attack attack, bool hit)
        {
            burying = attack == right;
            nextIsRight = attack == left;
            if (!burying) return;
            Pairs++;
            Detail = "arms buried";
            GameAudio.PlayAt(Sfx.Quake, transform.position, 0.9f, 60f);
        }

        protected override void OnRecovered() => burying = false;

        protected override void TickChase(Vector3 toPlayer, float distance)
        {
            // The second sweep follows the first straight away, from where it stands.
            if (nextIsRight && Time.time >= nextAttack && PlayerVisible && distance <= right.Reach + 2f) { BeginAttack(right); return; }
            base.TickChase(toPlayer, distance);
        }

        protected override void OnDisengaged() => nextIsRight = false;
    }
}
