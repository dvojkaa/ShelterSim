using System;
using UnityEngine.InputSystem;

namespace SheltreSim.Combat
{
    /// <summary>
    /// Code-constructed input action map for melee attack. Built entirely in code
    /// (no .inputactions asset), same pattern as PlayerInputActions / BuildInputActions.
    /// </summary>
    public class MeleeInputActions : IDisposable
    {
        private readonly InputActionMap map;

        public InputAction Attack { get; }

        public MeleeInputActions()
        {
            map = new InputActionMap("Melee");

            Attack = map.AddAction("Attack", InputActionType.Button);
            Attack.AddBinding("<Mouse>/leftButton");
            Attack.AddBinding("<Gamepad>/rightTrigger");
        }

        public void Enable() => map.Enable();

        public void Disable() => map.Disable();

        public void Dispose()
        {
            map?.Dispose();
        }
    }
}
