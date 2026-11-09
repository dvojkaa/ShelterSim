using SheltreSim.Audio;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>Runner: crouches and growls, then a straight lunge that doesn't home once it starts. Sidestep it.</summary>
    public sealed class RunnerBrain : SpeciesBrain
    {
        public const float LungeSpeed = 8.5f;
        private Attack lunge;

        protected override void OnStart()
        {
            lunge = Melee("lunge", 0.55f, Spec.AttackRange, 60f, 0.9f, 1.3f, PoseKind.Crouch, PoseKind.Lunge, Sfx.RaiderGrowl);
            lunge.StartRange = 4f;
            lunge.TurnRate = 200f;
        }

        protected override Attack ChooseAttack(float distance) => lunge;

        protected override void Resolve(Attack attack)
        {
            float distance = Flat(PlayerCenter - Enemy.CenterPosition).magnitude;
            BeginDash(LungeSpeed, Mathf.Clamp(distance + 1.2f, 2f, 5f), 0.55f, 1f, attack.RecoverHit, attack.RecoverMiss, false, PoseKind.Lunge);
        }
    }
}
