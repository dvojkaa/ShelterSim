using System;
using UnityEngine.InputSystem;

namespace SheltreSim.Player
{
    /// <summary>
    /// Code-constructed input action map for player control. Built entirely in code
    /// (no .inputactions asset) so it can be authored and reviewed without the Unity Editor.
    /// </summary>
    public class PlayerInputActions : IDisposable
    {
        private readonly InputActionMap map;

        public InputAction Move { get; }
        public InputAction Look { get; }
        public InputAction Jump { get; }
        public InputAction Sprint { get; }
        public InputAction Interact { get; }
        public InputAction Crouch { get; }

        public PlayerInputActions()
        {
            map = new InputActionMap("Player");

            Move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            Move.AddBinding("<Gamepad>/leftStick");

            Look = map.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
            Look.AddBinding("<Mouse>/delta");
            Look.AddBinding("<Gamepad>/rightStick");

            Jump = map.AddAction("Jump", InputActionType.Button);
            Jump.AddBinding("<Keyboard>/space");
            Jump.AddBinding("<Gamepad>/buttonSouth");

            Sprint = map.AddAction("Sprint", InputActionType.Button);
            Sprint.AddBinding("<Keyboard>/leftShift");
            Sprint.AddBinding("<Gamepad>/leftStickPress");

            // Value only read/polled here if needed; interact logic (raycast + triggering
            // IInteractable.Interact) lives in PlayerInteractor, which subscribes to
            // this action's `.performed` callback.
            Interact = map.AddAction("Interact", InputActionType.Button);
            Interact.AddBinding("<Keyboard>/e");
            Interact.AddBinding("<Gamepad>/buttonWest");

            // Held, not toggled: release to stand (if there's headroom).
            Crouch = map.AddAction("Crouch", InputActionType.Button);
            Crouch.AddBinding("<Keyboard>/leftCtrl");
            Crouch.AddBinding("<Gamepad>/rightStickPress");
        }

        public void Enable() => map.Enable();

        public void Disable() => map.Disable();

        public void Dispose()
        {
            map?.Dispose();
        }
    }
}
