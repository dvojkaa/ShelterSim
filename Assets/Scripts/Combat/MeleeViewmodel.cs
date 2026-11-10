using System.Collections.Generic;
using UnityEngine;
using SheltreSim.Crafting;

namespace SheltreSim.Combat
{
    /// <summary>
    /// Puts the model of the equipped melee weapon in the player's hand and holds it upright and angled
    /// forward, instead of lying flat. Lives on the WeaponVisual grip that MeleeAttacker swings, so the
    /// swing code is untouched: only the child models change. Models follow the held-weapon convention:
    /// grip at the origin, length along Blender +Z (Unity +Y), cutting edge toward Blender +Y.
    /// Unarmed and the Wooden Club keep the original bat.
    /// </summary>
    public sealed class MeleeViewmodel : MonoBehaviour
    {
        // The grip object is turned 90 degrees about X, so child +Y points at the camera's forward.
        // Tilting back about X lifts the tip up, and a roll about the weapon's own axis shows the flat of a blade.
        public static readonly Quaternion HeldRotation = Quaternion.Euler(-54f, 0f, 0f) * Quaternion.Euler(0f, -40f, 0f);

        private static readonly Dictionary<string, string> ModelFor = new Dictionary<string, string>
        {
            { "weapon_upgrade", "melee_makeshift_blade" },
            { "spiked_club", "melee_spiked_club" },
            { "machete", "melee_machete" },
            { "fire_axe", "melee_fire_axe" },
            { "steel_sword", "melee_steel_sword" },
        };

        private PlayerGear gear;
        private string shownWeapon = "\u0001";
        private Transform baseVisual;
        private GameObject extra;
        private readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        /// <summary>The model id currently shown, or null for the original bat. For probes.</summary>
        public string ShownModelId { get; private set; }


        private void LateUpdate()
        {
            if (gear == null) gear = Object.FindAnyObjectByType<PlayerGear>();
            string id = gear != null ? gear.WeaponId ?? "" : "";
            if (id == shownWeapon) return;
            shownWeapon = id;
            Apply(id);
        }

        private void Apply(string weaponId)
        {
            if (baseVisual == null) baseVisual = transform.Find("Visual");
            if (extra != null) extra.SetActive(false);
            ShownModelId = null;
            GameObject model = null;
            if (ModelFor.TryGetValue(weaponId, out string modelId))
            {
                if (!cache.TryGetValue(modelId, out model))
                {
                    GameObject prefab = Resources.Load<GameObject>("Props/" + modelId);
                    if (prefab != null)
                    {
                        model = Instantiate(prefab, transform);
                        model.name = modelId;
                        model.transform.localPosition = Vector3.zero;
                        model.transform.localScale = Vector3.one;
                        foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);
                        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                    cache[modelId] = model;
                }
            }
            if (model != null)
            {
                model.transform.localRotation = HeldRotation;
                model.SetActive(true);
                extra = model;
                ShownModelId = model.name;
            }
            if (baseVisual != null)
            {
                baseVisual.gameObject.SetActive(model == null);
                baseVisual.localRotation = HeldRotation;
            }
        }
    }
}
