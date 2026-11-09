using System.Collections.Generic;
using UnityEngine;

namespace SheltreSim.Storage
{
    /// <summary>A place that holds items outside the player's inventory (a storage crate).</summary>
    public interface IItemStore
    {
        Vector3 StorePosition { get; }
        int GetCount(string id);
        void Add(string id, int amount);
        bool TryTake(string id, int amount);
        IReadOnlyDictionary<string, int> Contents { get; }
    }

    /// <summary>
    /// How automation and crafting reach crates. Register in OnEnable, unregister in OnDisable. Always local:
    /// nothing here reaches further than the radius the caller passes, there is no global pool.
    /// </summary>
    public static class StorageRegistry
    {
        private static readonly List<IItemStore> stores = new List<IItemStore>();

        public static IReadOnlyList<IItemStore> All => stores;
        public static void Register(IItemStore store) { if (!stores.Contains(store)) stores.Add(store); }
        public static void Unregister(IItemStore store) => stores.Remove(store);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => stores.Clear();

        public static IItemStore Nearest(Vector3 position, float radius)
        {
            IItemStore best = null;
            float bestDistance = radius * radius;
            foreach (IItemStore store in stores)
            {
                float d = (store.StorePosition - position).sqrMagnitude;
                if (d <= bestDistance) { bestDistance = d; best = store; }
            }
            return best;
        }

        public static int CountNearby(Vector3 position, float radius, string id)
        {
            long total = 0;
            float r = radius * radius;
            foreach (IItemStore store in stores)
                if ((store.StorePosition - position).sqrMagnitude <= r) total += store.GetCount(id);
            return (int)System.Math.Min(total, int.MaxValue);
        }

        /// <summary>All or nothing, across every crate in range.</summary>
        public static bool TryTakeNearby(Vector3 position, float radius, string id, int amount)
        {
            if (amount <= 0) return amount == 0;
            if (CountNearby(position, radius, id) < amount) return false;
            float r = radius * radius;
            int left = amount;
            foreach (IItemStore store in stores)
            {
                if (left <= 0) break;
                if ((store.StorePosition - position).sqrMagnitude > r) continue;
                int take = Mathf.Min(left, store.GetCount(id));
                if (take > 0 && store.TryTake(id, take)) left -= take;
            }
            return left <= 0;
        }

        /// <summary>Puts items in the nearest crate. Returns false, adding nothing, when no crate is in range.</summary>
        public static bool TryAddNearby(Vector3 position, float radius, string id, int amount)
        {
            IItemStore store = Nearest(position, radius);
            if (store == null || amount <= 0) return false;
            store.Add(id, amount);
            return true;
        }
    }
}
