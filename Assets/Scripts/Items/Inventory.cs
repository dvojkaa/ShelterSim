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
            int newTotal = GetCount(resourceId) + amount;
            counts[resourceId] = newTotal;
            Debug.Log($"+{amount} {resourceId} (total: {newTotal})");
        }

        public int GetCount(string resourceId)
        {
            return counts.TryGetValue(resourceId, out int count) ? count : 0;
        }

        public IReadOnlyDictionary<string, int> GetAllCounts()
        {
            return counts;
        }

        /// <summary>Directly sets a stored count — used by save/load to restore state.
        /// Bypasses the Add log since this isn't a "the player gained something" event.</summary>
        public void SetCount(string resourceId, int amount)
        {
            counts[resourceId] = Mathf.Max(0, amount);
        }

        /// <summary>Empties all counts — used by save/load before restoring saved counts.</summary>
        public void Clear()
        {
            counts.Clear();
        }
    }
}
