using UnityEngine;

namespace SheltreSim.Core
{
    public class ApocalypseTrigger : MonoBehaviour, SheltreSim.Interaction.IInteractable
    {
        public GameStateManager gameStateManager;

        public string InteractPrompt =>
            gameStateManager != null && gameStateManager.CurrentPhase == GamePhase.Building
                ? "Press E to trigger the apocalypse (point of no return)"
                : "Already triggered.";

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
