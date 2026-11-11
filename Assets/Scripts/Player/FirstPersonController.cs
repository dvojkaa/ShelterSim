using UnityEngine;
using UnityEngine.InputSystem;

namespace SheltreSim.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public static FirstPersonController Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActive() => Active = null;

        [Header("Movement")]
        public float walkSpeed = 4f;
        public float sprintSpeed = 7f;
        public float jumpHeight = 1.2f;
        public float gravity = -18f;
        /// <summary>Set by other systems (encumbrance). 1 is normal. Never zero: the player can always walk.</summary>
        [System.NonSerialized] public float sprintMultiplier = 1f;

        [Header("Crouch (hold Left Ctrl)")]
        public float crouchHeight = 1.1f;
        public float crouchSpeed = 2f;
        public float crouchCameraSpeed = 12f;

        [Header("Look")]
        public float lookSensitivity = 0.12f;
        public float cameraPitchMin = -85f;
        public float cameraPitchMax = 85f;

        [Header("References")]
        // Assigned in the Inspector (or by the scene builder): the child transform
        // holding the Camera. This script rotates cameraPivot for pitch and the root
        // transform for yaw.
        public Transform cameraPivot;

        private CharacterController controller;
        private PlayerInputActions input;
        private Vector3 verticalVelocity;
        private float pitch;
        private Camera viewCamera;
        private float standingHeight;
        private Vector3 standingCenter;
        private float standingEyeY;
        private float eyeY;
        private bool crouched;
        private bool warnedNoHeadroom;
        private readonly Collider[] headroomHits = new Collider[16];
        private float jumpSuppressedUntil;
        private bool vehicleTransition;
        private bool vehicleSlopeAssist;
        private float preVehicleSlopeLimit, preVehicleStepOffset;

        public float CameraPitch => pitch;
        public bool IsCrouching => crouched;
        public Vector2 MoveInput => input != null ? Vector2.ClampMagnitude(input.Move.ReadValue<Vector2>(), 1f) : Vector2.zero;
        private bool bicycleMounted;
        public bool IsRiding => bicycleMounted;

        public void SetBicycleMounted(bool mounted)
        {
            if (mounted && !vehicleSlopeAssist && controller != null)
            {
                preVehicleSlopeLimit = controller.slopeLimit;
                preVehicleStepOffset = controller.stepOffset;
                // A mounted rider is moved by the vehicle, not by foot locomotion. The
                // default 45° CharacterController limit otherwise pins the rider on
                // terrain slopes even while VehicleTerrain has a valid grounded pose.
                controller.slopeLimit = Mathf.Max(preVehicleSlopeLimit, SheltreSim.World.VehicleTerrain.MaximumDriveSlope);
                controller.stepOffset = Mathf.Max(preVehicleStepOffset, SheltreSim.World.VehicleTerrain.MountedStepOffset);
                vehicleSlopeAssist = true;
            }
            else if (!mounted && vehicleSlopeAssist && controller != null)
            {
                controller.slopeLimit = preVehicleSlopeLimit;
                controller.stepOffset = preVehicleStepOffset;
                vehicleSlopeAssist = false;
            }
            bicycleMounted = mounted;
            // Dismount and jump share Space. Vehicle Update may dismount before this
            // controller processes the same frame, so guard briefly after unmount too.
            if (!mounted) jumpSuppressedUntil = Time.time + 0.15f;
            if (mounted && crouched) SetCrouched(false);
        }

        // Car controls (buggy, van): the mouse only looks around, W/S drive along the vehicle's heading and A/D steer it (no strafing).
        private bool carSteering;
        private float carTurnRate = 65f;
        private float carLookYaw, carLookIdle;
        public bool IsCarSteering => carSteering;

        /// <summary>Switches the rider to realistic car controls: A/D steer while moving, mouse = free look (yaw clamped, recentres when idle).</summary>
        public void SetCarSteering(bool active, float turnRateDegreesPerSecond = 65f)
        {
            carSteering = active;
            carTurnRate = turnRateDegreesPerSecond;
            carLookYaw = 0f;
            carLookIdle = 0f;
            if (cameraPivot != null) cameraPivot.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }

        /// <summary>Temporarily locks look and movement while a vehicle mount animation plays.</summary>
        public void SetVehicleTransition(bool active) => vehicleTransition = active;

        public void RestoreView(float cameraPitch)
        {
            pitch = Mathf.Clamp(cameraPitch, cameraPitchMin, cameraPitchMax);
            verticalVelocity = Vector3.zero;
            if (cameraPivot != null)
            {
                cameraPivot.localEulerAngles = new Vector3(pitch, 0f, 0f);
            }
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            // Keep displacement independent of frame rate. At high FPS the default
            // 1 mm cutoff silently discards otherwise valid walking and gravity steps.
            controller.minMoveDistance = 0f;
            input = new PlayerInputActions();

            if (cameraPivot == null)
            {
                Debug.LogError($"{nameof(FirstPersonController)} on '{name}' has no cameraPivot assigned — camera pitch will not work.");
            }

            standingHeight = controller.height;
            standingCenter = controller.center;
            standingEyeY = eyeY = cameraPivot != null ? cameraPivot.localPosition.y : 1.6f;
            viewCamera = cameraPivot != null ? cameraPivot.GetComponentInChildren<Camera>(true) : null;
            // A restart reloads the scene without resetting statics: this player starts standing.
            PlayerStealth.Crouching = false;
            ApplySettings();
            GameplaySettings.Changed += ApplySettings;

            // M24 player kit. Each creates what it needs; the checks keep a rebuilt scene from doubling them.
            if (GetComponent<Flashlight>() == null) gameObject.AddComponent<Flashlight>();
            if (GetComponent<QuickSlots>() == null) gameObject.AddComponent<QuickSlots>();
            if (GetComponent<SheltreSim.UI.PlayerStatusHUD>() == null) gameObject.AddComponent<SheltreSim.UI.PlayerStatusHUD>();
        }

        /// <summary>Look sensitivity and field of view from the pause menu (PlayerPrefs).</summary>
        private void ApplySettings()
        {
            lookSensitivity = GameplaySettings.LookSensitivity;
            if (viewCamera != null) viewCamera.fieldOfView = GameplaySettings.FieldOfView;
        }

        private void OnEnable()
        {
            Active = this;
            input.Enable();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
            if (crouched) SetCrouched(false);
            eyeY = standingEyeY;
            if (cameraPivot != null)
            {
                Vector3 local = cameraPivot.localPosition;
                local.y = standingEyeY;
                cameraPivot.localPosition = local;
            }
            input.Disable();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Disposal belongs here, not in OnDisable. This component is genuinely disabled and
        // re-enabled during normal play — SurvivalStats.Die() switches it off, and loading a
        // save taken while alive switches it back on — and a disposed InputActionMap is not
        // something OnEnable can safely re-Enable(). OnDestroy still runs on scene unload and
        // on application quit, so nothing leaks.
        private void OnDestroy()
        {
            GameplaySettings.Changed -= ApplySettings;
            PlayerStealth.Crouching = false;
            input?.Dispose();
        }

        private void Update()
        {
            // Time-frozen means the pause menu is open (it's the only thing in the project
            // that touches timeScale). Movement is already dead at deltaTime 0, but look is
            // driven by raw mouse delta and is NOT — without this the player's view spins
            // around behind the pause panel while they move the mouse to click Resume.
            if (Time.timeScale == 0f || !controller.enabled || vehicleTransition)
            {
                return;
            }

            HandleLook();
            if (!bicycleMounted) HandleCrouch();
            HandleMove();
        }

        /// <summary>
        /// Exposes the Interact action so PlayerInteractor (a separate component on the
        /// camera child) can subscribe to it without owning a competing input map.
        /// </summary>
        public InputAction GetInteractAction() => input.Interact;

        private void HandleLook()
        {
            Vector2 lookDelta = input.Look.ReadValue<Vector2>() * lookSensitivity;

            if (carSteering)
            {
                // Mouse looks around the cabin; the body (and with it the vehicle) only turns by steering.
                carLookYaw = Mathf.Clamp(carLookYaw + lookDelta.x, -120f, 120f);
                carLookIdle = lookDelta.sqrMagnitude > 0.0001f ? 0f : carLookIdle + Time.deltaTime;
                if (carLookIdle > 1.5f) carLookYaw = Mathf.MoveTowards(carLookYaw, 0f, 90f * Time.deltaTime);
                Vector2 drive = Vector2.ClampMagnitude(input.Move.ReadValue<Vector2>(), 1f);
                if (Mathf.Abs(drive.y) > 0.1f && Mathf.Abs(drive.x) > 0.01f)
                {
                    float direction = drive.y > 0f ? 1f : -1f; // reversing flips the steering like a real car
                    float turn = drive.x * direction * carTurnRate * Time.deltaTime;
                    transform.Rotate(Vector3.up, turn);
                    carLookYaw = Mathf.Clamp(carLookYaw - turn * 0.35f, -120f, 120f); // the view lags a little behind the turn
                }
                pitch = Mathf.Clamp(pitch - lookDelta.y, cameraPitchMin, cameraPitchMax);
                if (cameraPivot != null) cameraPivot.localEulerAngles = new Vector3(pitch, carLookYaw, 0f);
                return;
            }

            transform.Rotate(Vector3.up, lookDelta.x);

            pitch = Mathf.Clamp(pitch - lookDelta.y, cameraPitchMin, cameraPitchMax);
            if (cameraPivot != null)
            {
                cameraPivot.localEulerAngles = new Vector3(pitch, 0f, 0f);
            }
        }

        private void HandleMove()
        {
            Vector2 moveInput = Vector2.ClampMagnitude(input.Move.ReadValue<Vector2>(), 1f);
            bool sprinting = !crouched && input.Sprint.IsPressed();
            float speed = crouched ? Mathf.Min(walkSpeed, crouchSpeed)
                : sprinting ? Mathf.Max(walkSpeed, sprintSpeed * Mathf.Clamp01(sprintMultiplier)) : walkSpeed;

            Vector3 horizontal = carSteering
                ? transform.forward * moveInput.y * speed // cars never slide sideways
                : (transform.right * moveInput.x + transform.forward * moveInput.y) * speed;

            if (controller.isGrounded)
            {
                // Small downward bias instead of 0 keeps isGrounded reporting true on flat ground.
                verticalVelocity.y = -2f;

                if (!bicycleMounted && Time.time >= jumpSuppressedUntil && !crouched && input.Jump.WasPressedThisFrame())
                {
                    verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }

            verticalVelocity.y += gravity * Time.deltaTime;

            Vector3 motion = (horizontal + new Vector3(0f, verticalVelocity.y, 0f)) * Time.deltaTime;
            controller.Move(motion);
            if (bicycleMounted)
            {
                // At vehicle speed, gravity alone cannot keep the rider attached to a
                // descending grade. Sample the same walkable support used by the vehicle
                // and close a bounded downward gap every frame; upward terrain remains
                // governed by CharacterController's mounted slope/step limits.
                float supportY = SheltreSim.World.VehicleTerrain.RiderSupportHeight(
                    transform.position.x, transform.position.z, transform.position.y, SheltreSim.World.VehicleTerrain.CurrentSeed);
                float supportDelta = supportY - transform.position.y;
                if (supportDelta < -.02f && supportDelta >= -1.25f)
                {
                    controller.Move(Vector3.up * supportDelta);
                    verticalVelocity.y = -2f;
                }
            }
        }

        /// <summary>
        /// Hold to crouch: the capsule shrinks from the top (feet stay put), walk speed drops, no sprint or jump,
        /// and enemies notice the player at half range (PlayerStealth). Standing up needs headroom.
        /// </summary>
        private void HandleCrouch()
        {
            bool want = input.Crouch.IsPressed();
            if (want && !crouched)
            {
                SetCrouched(true);
            }
            else if (!want && crouched)
            {
                if (HasHeadroom())
                {
                    SetCrouched(false);
                }
                else if (!warnedNoHeadroom)
                {
                    warnedNoHeadroom = true;
                    SheltreSim.UI.GameMessageBus.Post("No room to stand up here.");
                }
            }

            if (cameraPivot != null)
            {
                float target = crouched ? standingEyeY - (standingHeight - CrouchHeight) : standingEyeY;
                eyeY = Mathf.Lerp(eyeY, target, 1f - Mathf.Exp(-crouchCameraSpeed * Time.deltaTime));
                if (Mathf.Abs(eyeY - target) < 0.001f) eyeY = target;
                Vector3 local = cameraPivot.localPosition;
                local.y = eyeY;
                cameraPivot.localPosition = local;
            }
        }

        private float CrouchHeight => Mathf.Clamp(crouchHeight, controller.radius * 2f + 0.05f, standingHeight);

        private void SetCrouched(bool value)
        {
            crouched = value;
            warnedNoHeadroom = false;
            PlayerStealth.Crouching = value;
            float height = value ? CrouchHeight : standingHeight;
            // Keep the bottom of the capsule where it is.
            float feet = standingCenter.y - standingHeight * 0.5f;
            controller.height = height;
            controller.center = new Vector3(standingCenter.x, feet + height * 0.5f, standingCenter.z);
        }

        /// <summary>Is the space between the crouched head and the standing head free of solid colliders (other than the player's own)?</summary>
        private bool HasHeadroom()
        {
            float scale = Mathf.Abs(transform.lossyScale.y);
            float radius = controller.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            Vector3 feet = transform.TransformPoint(new Vector3(standingCenter.x, standingCenter.y - standingHeight * 0.5f, standingCenter.z));
            Vector3 bottom = feet + Vector3.up * (CrouchHeight * scale);
            Vector3 top = feet + Vector3.up * Mathf.Max(CrouchHeight * scale, standingHeight * scale - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius * 0.95f, headroomHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = headroomHits[i];
                if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform) || hit == controller) continue;
                if (hit.GetComponentInParent<CharacterController>() == controller) continue;
                return false;
            }
            return true;
        }
    }
}
