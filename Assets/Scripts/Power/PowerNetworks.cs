using SheltreSim.Items;
using UnityEngine;

namespace SheltreSim.Power
{
    /// <summary>Lookups and small helpers shared by the power devices.</summary>
    public static class PowerNetworks
    {
        /// <summary>
        /// A network's lists are only rebuilt on a grid tick, and the grid doesn't tick while paused,
        /// so they can briefly hold a device that has been destroyed.
        /// </summary>
        public static bool IsAlive(object device)
        {
            if (device == null) return false;
            return !(device is Object unityObject) || unityObject != null;
        }

        public static PowerNetwork NetworkOf(IPowerConsumer consumer)
        {
            var networks = PowerGrid.Networks;
            for (int i = 0; i < networks.Count; i++)
                if (networks[i].Consumers.Contains(consumer)) return networks[i];
            return null;
        }

        /// <summary>Like PowerGrid.NetworkAt, but safe against destroyed entries in a stale network.</summary>
        public static PowerNetwork NetworkNear(Vector3 position)
        {
            PowerNetwork best = null;
            float bestDistance = PowerGrid.LinkRange * PowerGrid.LinkRange;
            foreach (PowerNetwork network in PowerGrid.Networks)
            {
                foreach (IPowerSource device in network.Sources) if (IsAlive(device)) Closer(network, device.PowerPosition, position, ref best, ref bestDistance);
                foreach (IPowerConsumer device in network.Consumers) if (IsAlive(device)) Closer(network, device.PowerPosition, position, ref best, ref bestDistance);
                foreach (IPowerStorage device in network.Storage) if (IsAlive(device)) Closer(network, device.PowerPosition, position, ref best, ref bestDistance);
            }
            return best;
        }

        private static void Closer(PowerNetwork network, Vector3 node, Vector3 position, ref PowerNetwork best, ref float bestDistance)
        {
            float distance = (node - position).sqrMagnitude;
            if (distance <= bestDistance) { bestDistance = distance; best = network; }
        }

        public static Inventory InventoryOf(GameObject interactor) =>
            interactor != null ? interactor.GetComponentInParent<Inventory>() : null;

        /// <summary>"2:05". Rounds up, so a running device never reads 0:00.</summary>
        public static string FormatTime(float seconds)
        {
            int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        /// <summary>Saved numbers can come from a hand-edited or damaged file.</summary>
        public static float Sane(float value, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return min;
            return Mathf.Clamp(value, min, max);
        }

        /// <summary>Null for missing or damaged state, which then behaves like a new device.</summary>
        public static T ParseState<T>(string state) where T : class
        {
            if (string.IsNullOrEmpty(state)) return null;
            try { return JsonUtility.FromJson<T>(state); }
            catch (System.ArgumentException) { return null; }
        }
    }
}
