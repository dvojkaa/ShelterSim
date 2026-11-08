using SheltreSim.Interaction;
using SheltreSim.Items;
using UnityEngine;

namespace SheltreSim.Building
{
    /// <summary>
    /// Passive water accumulator for the placeable Water Collector build piece. There's no
    /// real weather simulation in this project, so this is a simple timer-based stand-in for
    /// "collecting rainwater/condensation": once placed, it fills up over time and the player
    /// collects the stored water via E.
    /// </summary>
    public class WaterCollectorPiece : MonoBehaviour, IInteractable
    {
        public ResourceDefinition waterResource;
        public float secondsPerUnit = 30f;
        public int maxStored = 5;

        private float timer;
        private int stored;

        public string InteractPrompt => stored > 0
            ? $"Press E to collect {stored} Water"
            : "Collecting water... (none ready yet)";

        private void Update()
        {
            if (stored >= maxStored)
            {
                return;
            }

            timer += Time.deltaTime;

            while (timer >= secondsPerUnit && stored < maxStored)
            {
                timer -= secondsPerUnit;
                stored++;
            }

            if (stored >= maxStored)
            {
                timer = 0f;
            }
        }

        public void Interact(GameObject interactor)
        {
            if (stored <= 0)
            {
                Debug.Log("Nothing collected yet.");
                return;
            }

            if (waterResource == null)
            {
                Debug.LogError($"WaterCollectorPiece on '{name}' has no waterResource assigned.");
                return;
            }

            Inventory inventory = interactor.GetComponent<Inventory>();
            if (inventory == null)
            {
                inventory = interactor.GetComponentInParent<Inventory>();
            }

            if (inventory == null)
            {
                Debug.LogError($"'{interactor.name}' has no Inventory component — cannot collect.");
                return;
            }

            inventory.Add(waterResource.id, stored);
            Debug.Log($"Collected {stored} Water.");
            stored = 0;
        }
    }
}
