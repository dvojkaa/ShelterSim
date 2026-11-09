using SheltreSim.Audio;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>
    /// Iron Boar: paws the ground for a second, then charges straight at 9 m/s for up to 12 m. Running into something solid
    /// stuns it for 2 s: the opening. Close in, a gore with its own wind-up.
    /// </summary>
    public sealed class IronBoarBrain : SpeciesBrain
    {
        public const float ChargeSpeed = 9f;
        public const float ChargeDistance = 12f;
        public const float PawSeconds = 1f;
        private Attack charge, gore;

        protected override void OnStart()
        {
            charge = Melee("charge", PawSeconds, ChargeDistance, 60f, 1.0f, 1.4f, PoseKind.Paw, PoseKind.Charge, Sfx.BoarGrunt);
            charge.Windup = Mathf.Max(PawSeconds, Spec.AttackWindup);
            charge.StartRange = ChargeDistance;
            charge.TurnRate = 120f;
            charge.WindVolume = 1f;
            gore = Melee("gore", 0.9f, Spec.AttackRange, 45f, 1.0f, 1.4f, PoseKind.Paw, PoseKind.Gore, Sfx.BoarGrunt);
        }

        protected override Attack ChooseAttack(float distance) => distance >= 3.5f ? charge : gore;

        protected override void Resolve(Attack attack)
        {
            if (attack != charge) { base.Resolve(attack); return; }
            BeginDash(ChargeSpeed, ChargeDistance, 0.6f, 1f, attack.RecoverHit, attack.RecoverMiss, true, PoseKind.Charge);
        }
    }
}
