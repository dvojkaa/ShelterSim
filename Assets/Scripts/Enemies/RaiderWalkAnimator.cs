using UnityEngine;

namespace SheltreSim.Enemies
{
    /// <summary>
    /// Code-driven walk cycle for the raider's rigid limb parts, keyed off real movement speed.
    /// Purely cosmetic: it rotates only the named children of the "Visual" model and never reads
    /// or writes gameplay state, so EnemyAI needs no knowledge of it.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class RaiderWalkAnimator : MonoBehaviour
    {
        private const float MaxSwingDegrees = 32f;
        private const float StepsPerMeter = 1.5f;
        private const float BlendSpeed = 10f;
        private const float FullSwingSpeed = 2.2f;

        private CharacterController controller;
        private Transform legL, legR, armL, armR, torso;
        private Quaternion legLRest, legRRest, armLRest, armRRest, torsoRest;
        private float phase;
        private float amount;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            Transform visual = transform.Find("Visual");
            if (visual == null)
            {
                enabled = false;
                return;
            }
            legL = visual.Find("Leg_L");
            legR = visual.Find("Leg_R");
            armL = visual.Find("Arm_L");
            armR = visual.Find("Arm_R");
            torso = visual.Find("Torso");
            legLRest = Rest(legL);
            legRRest = Rest(legR);
            armLRest = Rest(armL);
            armRRest = Rest(armR);
            torsoRest = Rest(torso);
        }

        private static Quaternion Rest(Transform t) => t != null ? t.localRotation : Quaternion.identity;

        private void Update()
        {
            Vector3 velocity = controller.enabled ? controller.velocity : Vector3.zero;
            velocity.y = 0f;
            float speed = velocity.magnitude;

            amount = Mathf.MoveTowards(amount, Mathf.Clamp01(speed / FullSwingSpeed), BlendSpeed * Time.deltaTime);
            phase += speed * StepsPerMeter * Mathf.PI * Time.deltaTime;

            float swing = Mathf.Sin(phase) * MaxSwingDegrees * amount;
            Pose(legL, legLRest, swing);
            Pose(legR, legRRest, -swing);
            Pose(armL, armLRest, -swing * 0.8f);
            // The weapon arm is held up, so it only sways a little.
            Pose(armR, armRRest, swing * 0.2f);
            Pose(torso, torsoRest, Mathf.Abs(Mathf.Sin(phase)) * 2.5f * amount);
        }

        private static void Pose(Transform limb, Quaternion rest, float degreesAboutX)
        {
            if (limb != null) limb.localRotation = rest * Quaternion.Euler(degreesAboutX, 0f, 0f);
        }
    }
}
