using SheltreSim.Interaction;
using SheltreSim.Objectives;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Building
{
    /// <summary>End-of-game piece: interacting broadcasts the rescue call. The game keeps running afterwards.</summary>
    public class RadioBeaconPiece : MonoBehaviour, IInteractable
    {
        /// <summary>Saved with the game by ObjectiveTracker.</summary>
        public static bool VictoryAnnounced => ObjectiveTracker.Victory;

        public string InteractPrompt => "Press E to broadcast the rescue signal";

        public void Interact(GameObject interactor)
        {
            if (interactor == null) return;
            ObjectiveTracker.MarkVictory();
            SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.Upgrade, 0.9f);
            GameMessageBus.Post("VICTORY: Your radio beacon is broadcasting. Rescue is on its way. You survived the apocalypse. You can keep playing.");
        }
    }
}
