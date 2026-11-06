using SheltreSim.Interaction;
using UnityEngine;

namespace SheltreSim.Items
{
    public class ResourceNode : MonoBehaviour, IInteractable
    {
        public ResourceDefinition resourceType;
        public int quantityPerHarvest = 1;
        public int remainingCharges = 3;

        public string InteractPrompt => remainingCharges > 0
            ? $"Press E to gather {(resourceType != null ? resourceType.displayName : "resource")} ({remainingCharges} left)"
            : "Depleted";

        public void Interact(GameObject interactor)
        {
            if (remainingCharges <= 0)
            {
                Debug.Log("This resource node is depleted.");
                return;
            }

            if (resourceType == null)
            {
                Debug.LogError($"ResourceNode on '{name}' has no resourceType assigned.");
                return;
            }

            Inventory inv = interactor.GetComponent<Inventory>();
            if (inv == null)
            {
                inv = interactor.GetComponentInParent<Inventory>();
            }

            if (inv == null)
            {
                Debug.LogError($"'{interactor.name}' has no Inventory component — cannot gather.");
                return;
            }

            inv.Add(resourceType.id, quantityPerHarvest);
            remainingCharges--;

            if (remainingCharges <= 0)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
