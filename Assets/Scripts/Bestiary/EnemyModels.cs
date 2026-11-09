using System.Collections.Generic;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>
    /// Puts a bestiary model on an enemy built from the raider prefab. The model becomes the enemy's "Visual" child, feet on
    /// the controller's floor, so the walk animator finds its named parts (Torso, Head, Arm_L/R, Leg_L/R, or Foreleg_L/R and
    /// Hindleg_L/R on four-legged ones). Animation is GPT's work: this only places the parts. Returns false and leaves the old
    /// raider model when the prefab isn't there.
    /// </summary>
    public static class EnemyModels
    {
        private static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cache.Clear();

        public static bool Apply(GameObject enemy, string modelId, bool keepWeapon)
        {
            if (enemy == null || string.IsNullOrEmpty(modelId)) return false;
            if (!cache.TryGetValue(modelId, out GameObject asset)) cache[modelId] = asset = Resources.Load<GameObject>("Enemies/" + modelId);
            if (asset == null) return false;

            Transform old = enemy.transform.Find("Visual");
            Material material = null;
            if (old != null)
            {
                Renderer r = old.GetComponentInChildren<Renderer>();
                if (r != null) material = r.sharedMaterial; // SS_Raider: the emission keyword the damage flash needs
                old.gameObject.SetActive(false);
                old.name = "Visual_Old";
                Object.Destroy(old.gameObject);
            }
            // "Visual" is a holder at the feet; the model sits inside it, because the bosses' takes animate the model's own root
            // and would otherwise overwrite the feet offset (the boss floated a metre up).
            var holder = new GameObject("Visual").transform;
            holder.SetParent(enemy.transform, false);
            var controller = enemy.GetComponent<CharacterController>();
            float feet = controller != null ? controller.center.y - controller.height * 0.5f - controller.skinWidth / Mathf.Max(0.01f, Mathf.Abs(enemy.transform.lossyScale.y)) : -1f;
            holder.localPosition = new Vector3(0f, feet, 0f);
            GameObject model = Object.Instantiate(asset, holder, false);
            model.name = modelId;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            if (material != null) foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = material;

            EnemyAnimationDriver.Attach(enemy);
            Transform weapon = enemy.transform.Find("WeaponProp");
            if (weapon != null && !keepWeapon) weapon.gameObject.SetActive(false);
            return true;
        }
    }
}
