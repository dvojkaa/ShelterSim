using System.Collections.Generic;
using System.Text;
using SheltreSim.Crafting;
using SheltreSim.Interaction;
using SheltreSim.Items;
using UnityEngine;

namespace SheltreSim.Building
{
    /// <summary>
    /// General-purpose crafting station: interacting with it crafts a single configured
    /// output item from a configured list of resource costs, using the same
    /// CraftingSystem.TryAfford/Spend seam BuildModeController uses for build pieces.
    /// Unlike ResourceNode/LootContainer this is not single-use — it can be crafted from
    /// repeatedly as long as the player can afford the cost each time.
    /// </summary>
    public class WorkbenchStation : MonoBehaviour, IInteractable
    {
        public List<CraftingCost> craftingCost = new List<CraftingCost>();
        public ResourceDefinition output;
        public int outputAmount = 1;

        public string InteractPrompt
        {
            get
            {
                if (output == null)
                {
                    return "Workbench (not configured)";
                }

                if (craftingCost == null || craftingCost.Count == 0)
                {
                    return $"Press E to craft {output.displayName}";
                }

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < craftingCost.Count; i++)
                {
                    CraftingCost line = craftingCost[i];
                    if (line == null || line.resource == null)
                    {
                        continue;
                    }

                    if (sb.Length > 0)
                    {
                        sb.Append(", ");
                    }

                    sb.Append(line.amount).Append(' ').Append(line.resource.displayName);
                }

                return $"Press E to craft {output.displayName} ({sb})";
            }
        }

        public void Interact(GameObject interactor)
        {
            Inventory inv = interactor.GetComponent<Inventory>();
            if (inv == null)
            {
                inv = interactor.GetComponentInParent<Inventory>();
            }

            if (inv == null)
            {
                Debug.LogError($"'{interactor.name}' has no Inventory component — cannot craft.");
                return;
            }

            if (output == null)
            {
                Debug.LogError($"WorkbenchStation on '{name}' has no output resource assigned.");
                return;
            }

            if (!CraftingSystem.TryAfford(inv, craftingCost))
            {
                Debug.Log($"Cannot afford to craft {output.displayName}.");
                return;
            }

            CraftingSystem.Spend(inv, craftingCost);
            inv.Add(output.id, outputAmount);
            Debug.Log($"Crafted {outputAmount} {output.displayName}.");
        }
    }
}
