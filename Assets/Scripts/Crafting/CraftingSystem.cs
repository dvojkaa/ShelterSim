using System.Collections.Generic;
using SheltreSim.Items;

namespace SheltreSim.Crafting
{
    /// <summary>Shared affordability and atomic spending for construction and stations.</summary>
    public static class CraftingSystem
    {
        public static bool TryAfford(Inventory inventory, IReadOnlyList<CraftingCost> cost)
        {
            if (inventory == null || !TryGetTotals(cost, out var totals)) return false;
            foreach (var line in totals)
                if (inventory.GetCount(line.Key) < line.Value) return false;
            return true;
        }

        public static bool TrySpend(Inventory inventory, IReadOnlyList<CraftingCost> cost)
        {
            if (inventory == null || !TryGetTotals(cost, out var totals)) return false;
            foreach (var line in totals)
                if (inventory.GetCount(line.Key) < line.Value) return false;
            foreach (var line in totals)
                inventory.SetCount(line.Key, inventory.GetCount(line.Key) - line.Value);
            return true;
        }

        public static void Spend(Inventory inventory, IReadOnlyList<CraftingCost> cost)
        {
            TrySpend(inventory, cost);
        }

        private static bool TryGetTotals(IReadOnlyList<CraftingCost> cost, out Dictionary<string, int> totals)
        {
            totals = new Dictionary<string, int>();
            if (cost == null) return true;
            foreach (var line in cost)
            {
                if (line == null || line.resource == null) continue;
                if (line.amount < 0 || string.IsNullOrWhiteSpace(line.resource.id)) return false;
                totals.TryGetValue(line.resource.id, out int previous);
                long total = (long)previous + line.amount;
                if (total > int.MaxValue) return false;
                totals[line.resource.id] = (int)total;
            }
            return true;
        }
    }
}
