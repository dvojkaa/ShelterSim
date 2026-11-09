using SheltreSim.Enemies;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>
    /// The four-legged counterpart of RaiderWalkAnimator: a diagonal trot of Foreleg_L/R and Hindleg_L/R keyed off real movement
    /// speed, plus a small torso bob. Cosmetic only, never touches gameplay state. GPT's animation clips can replace it.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class CreatureWalkAnimator : MonoBehaviour
    {
        private const float MaxSwingDegrees = 28f;
        private const float StepsPerMeter = 1.1f;
        private const float BlendSpeed = 10f;
        private const float FullSwingSpeed = 2.5f;

        private CharacterController controller;
        private Transform foreL, foreR, hindL, hindR, torso;
        private Quaternion foreLRest, foreRRest, hindLRest, hindRRest, torsoRest;
        private float phase, amount;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            Rebind();
        }

        /// <summary>Finds the legs again after the "Visual" child was swapped.</summary>
        public void Rebind()
        {
            Transform visual = transform.Find("Visual");
            enabled = visual != null;
            if (visual == null) return;
            foreL = RaiderWalkAnimator.Part(visual, "Foreleg_L");
            foreR = RaiderWalkAnimator.Part(visual, "Foreleg_R");
            hindL = RaiderWalkAnimator.Part(visual, "Hindleg_L");
            hindR = RaiderWalkAnimator.Part(visual, "Hindleg_R");
            torso = RaiderWalkAnimator.Part(visual, "Torso");
            foreLRest = Rest(foreL); foreRRest = Rest(foreR); hindLRest = Rest(hindL); hindRRest = Rest(hindR); torsoRest = Rest(torso);
            phase = 0f;
            amount = 0f;
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
            // Diagonal pairs move together: front left with hind right.
            Pose(foreL, foreLRest, swing);
            Pose(hindR, hindRRest, swing);
            Pose(foreR, foreRRest, -swing);
            Pose(hindL, hindLRest, -swing);
            Pose(torso, torsoRest, Mathf.Sin(phase * 2f) * 1.5f * amount);
        }

        private static void Pose(Transform limb, Quaternion rest, float degreesAboutX)
        {
            if (limb != null) limb.localRotation = rest * Quaternion.Euler(degreesAboutX, 0f, 0f);
        }
    }
}
