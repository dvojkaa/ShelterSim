using System.Collections.Generic;
using UnityEngine;

namespace SheltreSim.Items
{
    /// <summary>
    /// Small item models made by M22SystemsSetup into Resources/Props (loot bags, fuel can, ammo boxes, energy cell).
    /// Visual only: colliders are stripped. Returns null if a model isn't there, so callers keep their fallback.
    /// </summary>
    public static class PropModels
    {
        private static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cache.Clear();

        public static GameObject Spawn(string id, Transform parent, Vector3 localPosition, float yaw = 0f, float scale = 1f)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!cache.TryGetValue(id, out GameObject asset))
            {
                asset = Resources.Load<GameObject>("Props/" + id);
                cache[id] = asset;
            }
            if (asset == null) return null;
            GameObject instance = Object.Instantiate(asset, parent, false);
            instance.name = "Visual_" + id;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one * scale;
            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            return instance;
        }

        /// <summary>The model that shows an item lying in the world, or null.</summary>
        public static string ModelFor(string itemId)
        {
            switch (itemId)
            {
                case "fuel": return "item_fuel_can";
                case "ammo_9mm": return "ammo_box_pistol";
                case "ammo_rifle": return "ammo_box_rifle";
                case "ammo_shell": return "ammo_box_shell";
                case "energy_cell": return "energy_cell";
                default: return null;
            }
        }
    }
}
