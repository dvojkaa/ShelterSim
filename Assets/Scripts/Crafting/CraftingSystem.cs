using System.Collections.Generic;
using SheltreSim.Items;
using UnityEngine;

namespace SheltreSim.Crafting
{
    /// <summary>
    /// Replaces the M2 ResourceStub now that a real Inventory exists (M4). The single
    /// integration seam for "can we afford this, and if so take the materials" — route
    /// every build-piece affordability check through here rather than growing ad-hoc
    /// inventory math elsewhere.
    /// </summary>
    public static class CraftingSystem
    {
        public static bool TryAfford(Inventory inventory, IReadOnlyList<CraftingCost> cost)
        {
            if (inventory == null)
            {
                Debug.LogError("CraftingSystem.TryAfford called with a null Inventory.");
                return false;
            }

            foreach (CraftingCost line in cost)
            {
                if (line.resource == null)
                {
                    continue;
                }

                if (inventory.GetCount(line.resource.id) < line.amount)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Deducts the recipe's cost. Caller must have already confirmed <see cref="TryAfford"/>
        /// — this does not re-check, it just subtracts (clamped to 0 by Inventory.SetCount).
        /// </summary>
        public static void Spend(Inventory inventory, IReadOnlyList<CraftingCost> cost)
        {
            if (inventory == null)
            {
                Debug.LogError("CraftingSystem.Spend called with a null Inventory.");
                return;
            }

            foreach (CraftingCost line in cost)
            {
                if (line.resource == null)
                {
                    continue;
                }

                inventory.SetCount(line.resource.id, inventory.GetCount(line.resource.id) - line.amount);
            }
        }
    }
}
