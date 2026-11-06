using UnityEngine;
using UnityEngine.InputSystem;

namespace SheltreSim.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 4f;
        public float sprintSpeed = 7f;
        public float jumpHeight = 1.2f;
        public float gravity = -18f;

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

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = new PlayerInputActions();

            if (cameraPivot == null)
            {
                Debug.LogError($"{nameof(FirstPersonController)} on '{name}' has no cameraPivot assigned — camera pitch will not work.");
            }
        }

        private void OnEnable()
        {
            input.Enable();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            input.Disable();
            input.Dispose();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            HandleLook();
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

            transform.Rotate(Vector3.up, lookDelta.x);

            pitch = Mathf.Clamp(pitch - lookDelta.y, cameraPitchMin, cameraPitchMax);
            if (cameraPivot != null)
            {
                cameraPivot.localEulerAngles = new Vector3(pitch, 0f, 0f);
            }
        }

        private void HandleMove()
        {
            Vector2 moveInput = input.Move.ReadValue<Vector2>();
            bool sprinting = input.Sprint.IsPressed();
            float speed = sprinting ? sprintSpeed : walkSpeed;

            Vector3 horizontal = (transform.right * moveInput.x + transform.forward * moveInput.y) * speed;

            if (controller.isGrounded)
            {
                // Small downward bias instead of 0 keeps isGrounded reporting true on flat ground.
                verticalVelocity.y = -2f;

                if (input.Jump.WasPressedThisFrame())
                {
                    verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }

            verticalVelocity.y += gravity * Time.deltaTime;

            Vector3 motion = (horizontal + new Vector3(0f, verticalVelocity.y, 0f)) * Time.deltaTime;
            controller.Move(motion);
        }
    }
}
