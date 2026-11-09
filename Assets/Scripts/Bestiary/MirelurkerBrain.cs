using SheltreSim.Audio;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>
    /// Mirelurker: lies low near its spawn and never goes more than 30 m from it. When it engages it emerges first,
    /// a visible pause, and only then attacks. Lure it onto firm ground and leave the leash to lose it.
    /// </summary>
    public sealed class MirelurkerBrain : SpeciesBrain
    {
        public const float EmergeSeconds = 0.9f;
        public const float Range = 30f;
        private Attack bite;

        protected override float LeashOverride => Range;
        protected override PoseKind IdlePose => PoseKind.Low;

        protected override void OnStart()
        {
            bite = Melee("bite", 0.7f, Spec.AttackRange, 45f, 1.0f, 1.4f, PoseKind.Paw, PoseKind.Gore, Sfx.BoarGrunt);
            wanderRadius = 0f;
        }

        protected override Attack ChooseAttack(float distance) => bite;

        protected override void OnEngaged()
        {
            SetState(BrainState.Emerge);
            pose?.Set(PoseKind.Rest, 120f);
            GameAudio.PlayAt(Sfx.RaiderGrowl, transform.position, 0.7f, 30f, 0.1f, 0f, 0.7f);
        }

        protected override bool TickSpecial(Vector3 toPlayer, float distance)
        {
            if (State != BrainState.Emerge) return false;
            Detail = "emerging";
            Move(Vector3.zero);
            Face(toPlayer, 120f);
            if (InState >= EmergeSeconds) { SetState(BrainState.Chasing); nextAttack = Mathf.Max(nextAttack, Time.time + 0.2f); }
            return true;
        }
    }
}
