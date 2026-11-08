using System.Collections.Generic;
using SheltreSim.Crafting;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Building
{
    /// <summary>
    /// Generic crafting station (Workbench, Stove, Forge, Chem Lab, Electronics Bench). Interacting opens
    /// CraftingUI filtered to this station's recipes. Attached to the piece prefab, so every placed copy works.
    /// </summary>
    public class WorkbenchStation : MonoBehaviour, IInteractable
    {
        public StationKind kind = StationKind.Workbench;

        // Legacy single-recipe fields (pre-M21). Unused now; kept so older setup tools and scenes still compile and load.
        [HideInInspector] public List<CraftingCost> craftingCost = new List<CraftingCost>();
        [HideInInspector] public ResourceDefinition output;
        [HideInInspector] public int outputAmount = 1;

        public string InteractPrompt => $"Press E to use the {RecipeBook.StationName(kind)}";

        public void Interact(GameObject interactor)
        {
            if (interactor == null) return;
            CraftingUI ui = CraftingUI.Find();
            if (ui == null)
            {
                Debug.LogError($"No CraftingUI in the scene — '{name}' cannot open.");
                return;
            }
            if (ui.inventory == null)
            {
                Inventory inv = interactor.GetComponentInParent<Inventory>();
                if (inv != null) ui.inventory = inv;
            }
            ui.Open(kind);
        }
    }
}
