using SheltreSim.Audio;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>
    /// Boss 3, the Shift Commander: shield and mace. A wide swing after two telegraphed steps forward; after a miss it rests
    /// its shield for 2 s and is open. It turns at 90 degrees a second, so the player can really circle it. Front hits do 40%
    /// (the shield) unless the shield is resting.
    /// </summary>
    public sealed class ShiftCommanderBrain : SpeciesBrain
    {
        public const float TurnRate = 90f;
        public const float ShieldFactor = 0.4f;
        public const float ShieldHalfAngle = 70f;
        public const float ShieldRestSeconds = 2f;
        public const float StepLength = 0.8f;
        private const float Windup = 1.3f;
        private Attack advance;
        private bool shieldDown;

        public bool ShieldResting => shieldDown && State == BrainState.Recover;

        protected override string WakeSound => Sfx.RaidHorn;

        protected override void OnStart()
        {
            advance = Melee("advance and swing", Windup, Spec.AttackRange + 0.2f, 70f, 1.2f, ShieldRestSeconds, PoseKind.Guard, PoseKind.SweepRight, Sfx.RaiderGrowl);
            advance.StartRange = Spec.AttackRange + 2f * StepLength;
            advance.TurnRate = TurnRate;
            advance.WindVolume = 1f;
            advance.RecoverPose = PoseKind.Guard;
            chaseTurnRate = TurnRate;
        }

        protected override Attack ChooseAttack(float distance) => advance;

        /// <summary>Two steps: 0.15-0.45 s and 0.6-0.9 s into the wind-up, each StepLength forward.</summary>
        protected override Vector3 WindupMotion(float t)
        {
            bool stepping = (t >= 0.15f && t < 0.45f) || (t >= 0.6f && t < 0.9f);
            if (!stepping) return Vector3.zero;
            if (Flat(PlayerCenter - Enemy.CenterPosition).magnitude < Spec.Radius + 0.9f) return Vector3.zero;
            return Flat(transform.forward).normalized * (StepLength / 0.3f);
        }

        protected override void OnStruck(Attack attack, bool hit)
        {
            shieldDown = !hit;
            if (shieldDown) { pose?.Set(PoseKind.ShieldRest, 300f); Detail = "shield resting"; }
        }

        protected override void TickEngaged(Vector3 toPlayer, float distance)
        {
            base.TickEngaged(toPlayer, distance);
            if (ShieldResting) pose?.Set(PoseKind.ShieldRest, 300f);
        }

        protected override void OnRecovered() => shieldDown = false;

        public override float FilterDamage(float amount, Vector3? from)
        {
            if (!ShieldResting && FromFront(from, ShieldHalfAngle))
            {
                GameAudio.PlayAt(Sfx.HitBlock, transform.position + Vector3.up, 0.9f, 25f);
                return amount * ShieldFactor;
            }
            return amount;
        }
    }
}
