using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.UI;
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
        [Min(0.1f)] public float secondsPerUnit = 90f;
        [Min(0)] public int maxStored = 5;

        private float timer;
        private int stored;

        public string InteractPrompt => stored > 0
            ? $"Press E to collect {stored} Water"
            : $"Collecting water... ({Mathf.CeilToInt(Mathf.Max(0f, Interval - timer))}s)";

        /// <summary>Exposed for SaveManager, which persists this as an extra field on the piece's PlacedPieceData.</summary>
        public int Stored => stored;
        public float CollectionElapsed => timer;
        private float Interval => Mathf.Max(0.1f, secondsPerUnit);

        /// <summary>Restores save-loaded state. Called by SaveManager right after this piece is re-Instantiated.</summary>
        public void SetStored(int value)
        {
            RestoreState(value, 0f);
        }

        public void RestoreState(int value, float collectionElapsed)
        {
            stored = Mathf.Clamp(value, 0, Mathf.Max(0, maxStored));
            timer = stored < maxStored && !float.IsNaN(collectionElapsed) && !float.IsInfinity(collectionElapsed)
                ? Mathf.Clamp(collectionElapsed, 0f, Interval)
                : 0f;
        }

        private void Update()
        {
            if (stored >= maxStored)
            {
                return;
            }

            timer += Time.deltaTime;

            while (timer >= Interval && stored < maxStored)
            {
                timer -= Interval;
                stored++;
            }

            if (stored >= maxStored)
            {
                timer = 0f;
            }
        }

        public void Interact(GameObject interactor)
        {
            if (interactor == null)
            {
                return;
            }
            if (stored <= 0)
            {
                GameMessageBus.Post("No water is ready yet.");
                return;
            }

            if (waterResource == null || string.IsNullOrEmpty(waterResource.id))
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
            GameMessageBus.Post($"Collected {stored} Water.");
            stored = 0;
        }
    }
}
