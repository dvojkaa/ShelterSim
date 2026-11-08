using System.Collections.Generic;
using UnityEngine;

namespace SheltreSim.Items
{
    public class Inventory : MonoBehaviour
    {
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>();

        /// <summary>Gains an amount of a resource. There's no inventory UI yet, so the
        /// log is the only player-visible feedback for now.</summary>
        public void Add(string resourceId, int amount)
        {
            if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0) return;
            int newTotal = (int)System.Math.Min(int.MaxValue, (long)GetCount(resourceId) + amount);
            counts[resourceId] = newTotal;
            Debug.Log($"+{amount} {resourceId} (total: {newTotal})");
        }

        public int GetCount(string resourceId)
        {
            return resourceId != null && counts.TryGetValue(resourceId, out int count) ? count : 0;
        }

        public IReadOnlyDictionary<string, int> GetAllCounts()
        {
            return counts;
        }

        /// <summary>Directly sets a stored count — used by save/load to restore state.
        /// Bypasses the Add log since this isn't a "the player gained something" event.</summary>
        public void SetCount(string resourceId, int amount)
        {
            if (string.IsNullOrWhiteSpace(resourceId)) return;
            counts[resourceId] = Mathf.Max(0, amount);
        }

        /// <summary>Empties all counts — used by save/load before restoring saved counts.</summary>
        public void Clear()
        {
            counts.Clear();
        }

        /// <summary>
        /// M9 — atomic check-and-deduct for consumable use (eating food). Unlike
        /// CraftingSystem's separate TryAfford/Spend pair, there's a single call site for
        /// this (InventoryUI's Eat button) so there's no risk of the two steps racing apart.
        /// </summary>
        public bool TryConsume(string resourceId, int amount)
        {
            if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0 || GetCount(resourceId) < amount)
            {
                return false;
            }

            counts[resourceId] = GetCount(resourceId) - amount;
            return true;
        }
    }
}
