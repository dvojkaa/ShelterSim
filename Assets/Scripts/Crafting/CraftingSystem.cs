using System.Collections.Generic;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Storage;
using UnityEngine;

namespace SheltreSim.Crafting
{
    /// <summary>
    /// Shared affordability and atomic spending for construction and stations. For the player's inventory this also
    /// counts crates within NearbyCrateRadius of the player, spending the pack first and then crates, all or nothing.
    /// Any other inventory only ever counts itself. UI, crafting and building all come through here.
    /// </summary>
    public static class CraftingSystem
    {
        /// <summary>Shared base stock across the 48x42 m homestead and connected bunker levels.</summary>
        public const float NearbyCrateRadius = 25f;

        public static bool TryAfford(Inventory inventory, IReadOnlyList<CraftingCost> cost)
        {
            if (inventory == null || !TryGetTotals(cost, out var totals)) return false;
            return HasAll(inventory, totals);
        }

        public static bool TrySpend(Inventory inventory, IReadOnlyList<CraftingCost> cost)
        {
            if (inventory == null || !TryGetTotals(cost, out var totals) || !HasAll(inventory, totals)) return false;

            // Crates first because a crate's TryTake is the only step that could refuse. The pack is touched
            // last, once nothing can fail. A refusal puts back everything taken so far.
            List<(IItemStore store, string id, int amount)> taken = null;
            foreach (var line in totals)
            {
                int fromCrates = line.Value - Mathf.Min(inventory.GetCount(line.Key), line.Value);
                if (fromCrates <= 0) continue;
                if (taken == null) taken = new List<(IItemStore, string, int)>();
                if (TakeFromCrates(inventory.transform.position, line.Key, fromCrates, taken)) continue;
                foreach (var entry in taken) entry.store.Add(entry.id, entry.amount);
                return false;
            }

            foreach (var line in totals)
            {
                int fromPack = Mathf.Min(inventory.GetCount(line.Key), line.Value);
                inventory.SetCount(line.Key, inventory.GetCount(line.Key) - fromPack);
            }
            return true;
        }

        public static void Spend(Inventory inventory, IReadOnlyList<CraftingCost> cost)
        {
            TrySpend(inventory, cost);
        }

        /// <summary>True when this inventory may draw on nearby crates: the player's, not a loot or test fixture.</summary>
        public static bool UsesCrates(Inventory inventory)
        {
            return inventory != null && inventory.GetComponent<FirstPersonController>() != null;
        }

        /// <summary>How much of an item the crates in reach hold. Always 0 for an inventory that isn't the player's.</summary>
        public static int CountInCrates(Inventory inventory, string id)
        {
            if (!UsesCrates(inventory)) return 0;
            return StorageRegistry.CountNearby(inventory.transform.position, NearbyCrateRadius, id);
        }

        /// <summary>Pack plus crates in reach: exactly what TryAfford and TrySpend count.</summary>
        public static int CountAvailable(Inventory inventory, string id)
        {
            if (inventory == null) return 0;
            return (int)System.Math.Min(int.MaxValue, (long)inventory.GetCount(id) + CountInCrates(inventory, id));
        }

        /// <summary>How many crates are in reach of the player.</summary>
        public static int NearbyCrateCount(Inventory inventory)
        {
            if (!UsesCrates(inventory)) return 0;
            int count = 0;
            float range = NearbyCrateRadius * NearbyCrateRadius;
            Vector3 position = inventory.transform.position;
            foreach (IItemStore store in StorageRegistry.All)
                if ((store.StorePosition - position).sqrMagnitude <= range) count++;
            return count;
        }

        private static bool HasAll(Inventory inventory, Dictionary<string, int> totals)
        {
            foreach (var line in totals)
                if (CountAvailable(inventory, line.Key) < line.Value) return false;
            return true;
        }

        private static bool TakeFromCrates(Vector3 position, string id, int amount, List<(IItemStore store, string id, int amount)> taken)
        {
            float range = NearbyCrateRadius * NearbyCrateRadius;
            int left = amount;
            foreach (IItemStore store in StorageRegistry.All)
            {
                if (left <= 0) break;
                if ((store.StorePosition - position).sqrMagnitude > range) continue;
                int take = Mathf.Min(left, store.GetCount(id));
                if (take <= 0 || !store.TryTake(id, take)) continue;
                left -= take;
                taken.Add((store, id, take));
            }
            return left <= 0;
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
