using SheltreSim.Interaction;
using UnityEngine;

namespace SheltreSim.Items
{
    /// <summary>
    /// A single-use searchable container (furniture, a car trunk, etc.) placed at a
    /// "LootSpawn" marker by the scene-population tool. Unlike ResourceNode, it isn't a
    /// repeatable/charge-limited gather point — it's searched exactly once, and the
    /// outcome (empty or not) is pre-rolled externally via hasLoot/resource/amount rather
    /// than decided by this component.
    /// </summary>
    public class LootContainer : MonoBehaviour, IInteractable
    {
        public ResourceDefinition resource;
        public int amount = 1;
        public bool hasLoot = true;

        private bool searched;

        public string InteractPrompt => searched
            ? "Already searched"
            : "Press E to search";

        public void Interact(GameObject interactor)
        {
            if (searched)
            {
                Debug.Log("Already searched — nothing left.");
                return;
            }

            searched = true;

            if (!hasLoot || resource == null)
            {
                Debug.Log("Searched — found nothing.");
                return;
            }

            Inventory inv = interactor.GetComponent<Inventory>();
            if (inv == null)
            {
                inv = interactor.GetComponentInParent<Inventory>();
            }

            if (inv == null)
            {
                Debug.LogError($"'{interactor.name}' has no Inventory component — cannot loot.");
                return;
            }

            inv.Add(resource.id, amount);
            Debug.Log($"Found {amount} {resource.displayName}.");
        }
    }
}
