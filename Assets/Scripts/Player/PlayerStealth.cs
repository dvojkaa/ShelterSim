using UnityEngine;

namespace SheltreSim.Player
{
    /// <summary>
    /// How easy the player is to notice. Enemy brains multiply their detection radius by DetectionFactor
    /// when deciding whether they newly see the player (not when deciding to give up a chase).
    /// FirstPersonController owns the crouch state; nothing else writes it.
    /// </summary>
    public static class PlayerStealth
    {
        public const float CrouchFactor = 0.5f;

        public static bool Crouching { get; internal set; }

        /// <summary>1 standing, 0.5 crouched.</summary>
        public static float DetectionFactor => Crouching ? CrouchFactor : 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Crouching = false;
    }
}
