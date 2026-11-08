using UnityEngine;

namespace SheltreSim.Core
{
    public class ApocalypseTrigger : MonoBehaviour, SheltreSim.Interaction.IInteractable
    {
        public GameStateManager gameStateManager;

        public string InteractPrompt =>
            gameStateManager != null && gameStateManager.CurrentPhase == GamePhase.Building
                ? "E  Begin apocalypse  /  prepare supplies first"
                : "Emergency console  /  apocalypse already begun";

        public void Interact(GameObject interactor)
        {
            if (gameStateManager == null)
            {
                Debug.LogError($"ApocalypseTrigger on '{name}' has no gameStateManager assigned.");
                return;
            }

            gameStateManager.TriggerApocalypse();
        }
    }
}
