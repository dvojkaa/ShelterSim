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

        // Retuned alongside SurvivalStats' M12 decay-rate rebalance: thirst now drains at
        // 0.42/s instead of 1.7/s, so the old 30s-per-unit rate would have one collector
        // covering ~280% of thirst drain instead of the intended "roughly covers it alone,
        // two is comfortable" — 90s/unit puts one collector's 35-thirst-per-90s at ~93% of the
        // new drain rate, keeping the same relative balance as before the rebalance.
        public float secondsPerUnit = 90f;
        public int maxStored = 5;

        private float timer;
        private int stored;

        public string InteractPrompt => stored > 0
            ? $"Press E to collect {stored} Water"
            : "Collecting water... (none ready yet)";

        /// <summary>Exposed for SaveManager, which persists this as an extra field on the piece's PlacedPieceData.</summary>
        public int Stored => stored;

        /// <summary>Restores save-loaded state. Called by SaveManager right after this piece is re-Instantiated.</summary>
        public void SetStored(int value)
        {
            stored = Mathf.Clamp(value, 0, maxStored);
        }

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
