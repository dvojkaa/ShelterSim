using SheltreSim.Audio;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>
    /// A scavenger keeps its ordinary jab at close range, but throws a crude chemical flask when it has room.
    /// The long inhale/overhand telegraph leaves time to dodge; walls block the projectile.
    /// </summary>
    public sealed class ScavengerBrain : SpeciesBrain
    {
        private const float FlaskRange = 11f;
        private Attack jab;
        private Attack flask;

        protected override void OnStart()
        {
            jab = Melee("jab", 0.5f, Spec.AttackRange, 45f, 1.0f, 1.2f,
                PoseKind.ArmsUp, PoseKind.Slam, Sfx.RaiderGrowl);

            flask = Melee("chemical flask", 0.85f, FlaskRange, 30f, 1.45f, 1.8f,
                PoseKind.ArmsUp, PoseKind.Slam, Sfx.RaiderGrowl);
            flask.StartRange = FlaskRange;
            flask.ProjectileSpeed = 19f;
            flask.ProjectileScale = 0.55f;
            flask.StrikeSound = null;
        }

        protected override Attack ChooseAttack(float distance)
        {
            return distance > Spec.AttackRange + 0.65f && distance <= FlaskRange ? flask : jab;
        }
    }
}
