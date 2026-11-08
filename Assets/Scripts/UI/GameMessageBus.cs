using UnityEngine;

namespace SheltreSim.UI
{
    /// <summary>
    /// Centralizes player-facing one-off feedback ("Found 3 Wood", "Hit Raider_1 for 20
    /// damage", "Cannot afford to craft...") so a single call site reaches BOTH the Console
    /// (for debugging) and the on-screen message feed (for actually playing the game without
    /// the Console open). Gameplay scripts call Post(...) instead of Debug.Log(...) directly;
    /// SurvivalHUD subscribes to OnMessage to display it.
    /// </summary>
    public static class GameMessageBus
    {
        public static event System.Action<string> OnMessage;

        public static void Post(string message)
        {
            Debug.Log(message);
            OnMessage?.Invoke(message);
        }
    }
}
