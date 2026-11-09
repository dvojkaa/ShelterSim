using SheltreSim.Audio;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>
    /// Screamer: a long visible inhale (1.5 s) when it engages. A hit interrupts it. Otherwise it screams and
    /// SpeciesBehaviours.Alert(position, 25) wakes the group already there. It never spawns anything.
    /// </summary>
    public sealed class ScreamerBrain : SpeciesBrain
    {
        public const float InhaleSeconds = 1.5f;
        public const float AlertRadius = 25f;
        public int Screams { get; private set; }
        public int ScreamsInterrupted { get; private set; }
        private Attack scream, claw;
        private float nextScream;

        protected override void OnStart()
        {
            scream = Melee("scream", InhaleSeconds, 99f, 180f, 0.8f, 0.8f, PoseKind.Inhale, PoseKind.Scream, Sfx.RaiderGrowl);
            scream.Windup = InhaleSeconds;
            scream.StartRange = Spec.Detection + 5f;
            scream.WindVolume = 0.5f;
            scream.StrikeSound = null;
            scream.TurnRate = 90f;
            claw = Melee("claw", 0.5f, Spec.AttackRange, 45f, 1.0f, 1.3f);
        }

        protected override Attack ChooseAttack(float distance) => Time.time >= nextScream ? scream : claw;

        protected override void Resolve(Attack attack)
        {
            if (attack != scream) { base.Resolve(attack); return; }
            pose?.Set(PoseKind.Scream, 600f);
            GameAudio.PlayAt(Sfx.RaiderAlert, transform.position + Vector3.up * 1.5f, 1f, 70f);
            Screams++;
            nextScream = Time.time + 20f;
            SpeciesBehaviours.Alert(transform.position, AlertRadius);
            UI.GameMessageBus.Post("A Screamer shrieks. Something answers nearby.");
            Recover(attack.RecoverHit);
        }

        protected override void OnInterrupted(Attack attack)
        {
            if (attack != scream) return;
            ScreamsInterrupted++;
            nextScream = Time.time + 6f;
        }
    }
}
