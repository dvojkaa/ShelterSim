using SheltreSim.Audio;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>
    /// Boss 1, the Gatekeeper: a long overhead slam (1.3 s telegraph, 3 m reach, Quake on impact; a miss leaves it open for
    /// 2 s) and a short straight rush when the player keeps their distance. Super armour: hits don't stop a wind-up.
    /// </summary>
    public sealed class GatekeeperBrain : SpeciesBrain
    {
        public const float SlamWindup = 1.3f;
        public const float SlamReach = 3f;
        public const float MissRecovery = 2f;
        private Attack slam, rush;
        private float nextRush;

        protected override void OnStart()
        {
            slam = Melee("slam", SlamWindup, SlamReach, 40f, 1.2f, MissRecovery, PoseKind.ArmsUp, PoseKind.Slam, Sfx.RaiderGrowl);
            slam.StrikeSound = Sfx.Quake;
            slam.TurnRate = 70f;
            slam.WindVolume = 1f;
            slam.StartRange = 2.6f;
            rush = Melee("rush", 0.8f, 7f, 60f, 1.4f, 1.6f, PoseKind.Crouch, PoseKind.Lunge, Sfx.RaiderAlert);
            rush.StartRange = 10f;
            rush.TurnRate = 120f;
            rush.WindVolume = 1f;
            chaseTurnRate = 180f;
        }

        protected override Attack ChooseAttack(float distance)
        {
            if (distance <= slam.StartRange) return slam;
            if (distance >= 5f && Time.time >= nextRush) return rush;
            return null;
        }

        protected override void Resolve(Attack attack)
        {
            if (attack != rush) { base.Resolve(attack); return; }
            nextRush = Time.time + 6f;
            float distance = Flat(PlayerCenter - Enemy.CenterPosition).magnitude;
            BeginDash(6.5f, Mathf.Min(distance + 0.5f, 7f), 0.9f, 0.6f, attack.RecoverHit, attack.RecoverMiss, false, PoseKind.Lunge);
        }
    }
}
