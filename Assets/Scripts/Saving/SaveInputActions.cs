using System;
using UnityEngine.InputSystem;

namespace SheltreSim.Saving
{
    /// <summary>
    /// Code-constructed input action map for save/load. Built entirely in code (no
    /// .inputactions asset), same pattern as PlayerInputActions / BuildInputActions.
    /// Keyboard-only: these are debug/meta actions, not core gameplay, so gamepad
    /// bindings are deliberately skipped.
    /// </summary>
    public class SaveInputActions : IDisposable
    {
        private readonly InputActionMap map;

        public InputAction SaveGame { get; }
        public InputAction LoadGame { get; }

        public SaveInputActions()
        {
            map = new InputActionMap("Save");

            SaveGame = map.AddAction("SaveGame", InputActionType.Button);
            SaveGame.AddBinding("<Keyboard>/f5");

            LoadGame = map.AddAction("LoadGame", InputActionType.Button);
            LoadGame.AddBinding("<Keyboard>/f9");
        }

        public void Enable() => map.Enable();

        public void Disable() => map.Disable();

        public void Dispose()
        {
            map?.Dispose();
        }
    }
}
