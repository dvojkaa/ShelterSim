using System;
using UnityEngine.InputSystem;

namespace SheltreSim.Building
{
    /// <summary>
    /// Code-constructed input action map for build mode. Built entirely in code
    /// (no .inputactions asset), same pattern as SheltreSim.Player.PlayerInputActions.
    /// </summary>
    public class BuildInputActions : IDisposable
    {
        private readonly InputActionMap map;

        public InputAction ToggleBuildMode { get; }
        public InputAction PlacePiece { get; }
        public InputAction CancelBuild { get; }
        public InputAction RotatePiece { get; }
        public InputAction CyclePiece { get; }

        public BuildInputActions()
        {
            map = new InputActionMap("Build");

            ToggleBuildMode = map.AddAction("ToggleBuildMode", InputActionType.Button);
            ToggleBuildMode.AddBinding("<Keyboard>/b");
            ToggleBuildMode.AddBinding("<Gamepad>/buttonNorth");

            PlacePiece = map.AddAction("PlacePiece", InputActionType.Button);
            PlacePiece.AddBinding("<Mouse>/leftButton");
            PlacePiece.AddBinding("<Gamepad>/rightTrigger");

            CancelBuild = map.AddAction("CancelBuild", InputActionType.Button);
            CancelBuild.AddBinding("<Keyboard>/escape");
            CancelBuild.AddBinding("<Gamepad>/buttonEast");

            RotatePiece = map.AddAction("RotatePiece", InputActionType.Button);
            RotatePiece.AddBinding("<Keyboard>/r");
            RotatePiece.AddBinding("<Gamepad>/rightShoulder");

            // Value only — sign (positive/negative) indicates cycle direction. The consumer
            // reads the sign and applies its own debounce; this map only exposes the raw axis.
            CyclePiece = map.AddAction("CyclePiece", InputActionType.Value, expectedControlLayout: "Axis");
            CyclePiece.AddBinding("<Mouse>/scroll/y");
        }

        public void Enable() => map.Enable();

        public void Disable() => map.Disable();

        public void Dispose()
        {
            map?.Dispose();
        }
    }
}
