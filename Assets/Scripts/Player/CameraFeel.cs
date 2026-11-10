using SheltreSim.Survival;
using SheltreSim.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SheltreSim.Player
{
    /// <summary>
    /// Subtle procedural camera motion: head-bob, landing dip, hit flinch. The offset is applied to the
    /// camera pivot's local pose after FirstPersonController has set it (LateUpdate) and removed again before
    /// the controller runs next frame (Update, very early), so the controller's absolute pitch/eye height writes
    /// are never disturbed. Outputs nothing on a vehicle, during a mount transition, when paused or dead.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class CameraFeel : MonoBehaviour
    {
        /// <summary>Master switch and 0..1 strength, for a future settings panel.</summary>
        public static bool Enabled = true;
        public static float Intensity = 1f;

        private FirstPersonController player;
        private CharacterController body;
        private SurvivalStats stats;
        private VehicleMountView mountView;
        private Transform pivot;

        private Vector3 cleanPosition, writtenPosition;
        private Quaternion cleanRotation, writtenRotation;
        private bool hasApplied;

        private float bobPhase, bobBlend;
        private bool wasGrounded = true;
        private float fastestFall, airborneSeconds;
        private Vector3 previousPosition;
        private float dipTime = 99f, dipDepth;
        private float lastHealth = -1f;
        private float flinchTime = 99f, flinchRoll, flinchPitch;
        private float nextFlinchSign = 1f;

        // Runtime initialization is called once per process, whereas Restart reloads the scene.
        // Subscribe before the first scene so a fresh player gets the component after every reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= Install;
            SceneManager.sceneLoaded += Install;
        }

        private static void Install(Scene scene, LoadSceneMode mode)
        {
            FirstPersonController fpc = Object.FindFirstObjectByType<FirstPersonController>();
            if (fpc == null || fpc.cameraPivot == null || fpc.GetComponent<CameraFeel>() != null) return;
            fpc.gameObject.AddComponent<CameraFeel>();
        }

        private void Awake()
        {
            player = GetComponent<FirstPersonController>();
            body = GetComponent<CharacterController>();
            stats = GetComponent<SurvivalStats>();
            mountView = GetComponent<VehicleMountView>();
            pivot = player != null ? player.cameraPivot : null;
        }

        private void OnEnable() => ResetMotion();

        private void OnDisable()
        {
            Undo();
            ResetMotion();
        }

        private void Undo()
        {
            if (!hasApplied) return;
            hasApplied = false;
            if (pivot == null) return;
            // Another owner may have restored the view (save/load, death or a vehicle) since
            // our last write. Only restore channels which still contain our exact offset.
            Vector3 current = pivot.localPosition;
            if (Mathf.Approximately(current.x, writtenPosition.x)) current.x = cleanPosition.x;
            if (Mathf.Approximately(current.y, writtenPosition.y)) current.y = cleanPosition.y;
            if (Mathf.Approximately(current.z, writtenPosition.z)) current.z = cleanPosition.z;
            pivot.localPosition = current;
            if (Quaternion.Angle(pivot.localRotation, writtenRotation) < 0.001f)
                pivot.localRotation = cleanRotation;
        }

        // Runs before FirstPersonController.Update (execution order), so the pose is clean for it.
        private void Update() => Undo();

        private bool Suppressed()
        {
            if (!Enabled || float.IsNaN(Intensity) || float.IsInfinity(Intensity) || Intensity <= 0f ||
                pivot == null || player == null || !player.isActiveAndEnabled || body == null || !body.enabled) return true;
            if (Time.timeScale <= 0f) return true;
            if (stats != null && (stats.IsDead || stats.health <= 0f)) return true;
            if (mountView == null) mountView = GetComponent<VehicleMountView>();
            if (mountView != null && mountView.IsBusy) return true;
            if (pivot.parent != player.transform) return true;
            return false;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (Suppressed())
            {
                ResetMotion();
                return;
            }
            // Teleports and save loads are not landings, even if the controller's last
            // grounded sample and velocity describe the location being replaced.
            if ((transform.position - previousPosition).sqrMagnitude > 25f) ResetMotion();
            TrackEvents(dt);
            previousPosition = transform.position;

            float k = Mathf.Clamp01(Intensity);
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;

            // Head-bob: amplitude/frequency by horizontal speed, only on the ground.
            Vector3 v = body != null ? body.velocity : Vector3.zero;
            float speed = new Vector2(v.x, v.z).magnitude;
            bool moving = body != null && body.isGrounded && speed > 0.5f;
            bobBlend = Mathf.MoveTowards(bobBlend, moving ? 1f : 0f, dt * 6f);
            if (bobBlend > 0f)
            {
                float t = Mathf.InverseLerp(player.walkSpeed, Mathf.Max(player.walkSpeed + .1f, player.sprintSpeed), speed);
                // Distance, rather than elapsed time, keeps the bob tied to walking pace.
                float strideLength = Mathf.Lerp(2.05f, 2.9f, t);
                bobPhase = Mathf.Repeat(bobPhase + speed * dt * Mathf.PI / strideLength, Mathf.PI * 2f);
                float amp = 0.035f * Mathf.Lerp(1f, 1.4f, t) * bobBlend * (player.IsCrouching ? .5f : 1f);
                pos.y += Mathf.Sin(bobPhase * 2f) * amp;
                pos.x += Mathf.Cos(bobPhase) * amp * (0.02f / 0.035f);
                rot *= Quaternion.Euler(0f, 0f, Mathf.Cos(bobPhase) * amp * 25f);
            }

            // Landing dip: quick drop, eased recovery over 0.25 s.
            if (dipTime < 0.25f)
            {
                float u = dipTime / 0.25f;
                float curve = u < 0.25f ? u / 0.25f : 1f - (u - 0.25f) / 0.75f;
                pos.y -= dipDepth * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(curve));
                dipTime += dt;
            }

            // Hit flinch: small roll/pitch kick decaying over 0.2 s.
            if (flinchTime < 0.2f)
            {
                float fade = 1f - flinchTime / 0.2f;
                fade *= fade;
                rot *= Quaternion.Euler(flinchPitch * fade, 0f, flinchRoll * fade);
                flinchTime += dt;
            }

            pos *= k;
            rot = Quaternion.Slerp(Quaternion.identity, rot, k);
            cleanPosition = pivot.localPosition;
            cleanRotation = pivot.localRotation;
            writtenPosition = cleanPosition + pos;
            writtenRotation = cleanRotation * rot;
            pivot.SetLocalPositionAndRotation(writtenPosition, writtenRotation);
            hasApplied = true;
        }

        private void ResetMotion()
        {
            bobPhase = bobBlend = 0f;
            dipTime = flinchTime = 99f;
            fastestFall = airborneSeconds = 0f;
            wasGrounded = body != null && body.isGrounded;
            lastHealth = stats != null ? stats.health : -1f;
            previousPosition = transform.position;
        }

        private void TrackEvents(float dt)
        {
            if (body != null)
            {
                bool grounded = body.isGrounded;
                if (!grounded)
                {
                    airborneSeconds += dt;
                    fastestFall = Mathf.Max(fastestFall, -body.velocity.y);
                }
                if (grounded && !wasGrounded)
                {
                    if (airborneSeconds > .08f && fastestFall > 3f)
                    {
                        dipDepth = Mathf.Lerp(.05f, .15f, Mathf.InverseLerp(4f, 12f, fastestFall));
                        dipTime = 0f;
                    }
                }
                if (grounded) fastestFall = airborneSeconds = 0f;
                wasGrounded = grounded;
            }

            if (stats != null)
            {
                float h = stats.health;
                if (lastHealth >= 0f && lastHealth - h > 1f)
                {
                    float s = Mathf.Clamp01((lastHealth - h) / 25f) * 0.6f + 0.4f;
                    flinchPitch = 1.4f * s;
                    flinchRoll = nextFlinchSign * 2.5f * s;
                    nextFlinchSign = -nextFlinchSign;
                    flinchTime = 0f;
                }
                lastHealth = h;
            }
        }
    }
}
