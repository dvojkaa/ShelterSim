using System;
using UnityEngine.InputSystem;

namespace SheltreSim.Combat
{
    /// <summary>Code-constructed input for firearms and hand selection. Same pattern as MeleeInputActions.</summary>
    public sealed class RangedInputActions : IDisposable
    {
        private readonly InputActionMap map = new InputActionMap("Ranged");

        public InputAction Fire { get; }
        public InputAction Reload { get; }
        public InputAction MeleeHand { get; }
        public InputAction RangedHand { get; }

        public RangedInputActions()
        {
            Fire = map.AddAction("Fire", InputActionType.Button);
            Fire.AddBinding("<Mouse>/leftButton");
            Fire.AddBinding("<Gamepad>/rightTrigger");
            Reload = map.AddAction("Reload", InputActionType.Button);
            Reload.AddBinding("<Keyboard>/r");
            MeleeHand = map.AddAction("MeleeHand", InputActionType.Button);
            MeleeHand.AddBinding("<Keyboard>/1");
            RangedHand = map.AddAction("RangedHand", InputActionType.Button);
            RangedHand.AddBinding("<Keyboard>/2");
        }

        public void Enable() => map.Enable();
        public void Disable() => map.Disable();
        public void Dispose() => map.Dispose();
    }
}
