using System.Collections.Generic;
using SheltreSim.Crafting;
using UnityEngine;

namespace SheltreSim.Items
{
    /// <summary>
    /// Inventory icons: Resources/Icons/&lt;itemId&gt;.png (rendered from ArtSource/icons/&lt;itemId&gt;.py by ss_art.finish_icon).
    /// Returns null when an item has no icon so callers can show a category-coloured placeholder instead.
    /// </summary>
    public static class ItemIcons
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cache.Clear();

        public static Sprite Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (cache.TryGetValue(id, out Sprite cached)) return cached;
            Sprite sprite = Resources.Load<Sprite>("Icons/" + id);
            if (sprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>("Icons/" + id);
                if (texture != null)
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
            }
            cache[id] = sprite;
            return sprite;
        }

        public static Color CategoryColor(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Weapon: return new Color(.55f, .25f, .22f);
                case ItemCategory.Armor: return new Color(.30f, .38f, .50f);
                case ItemCategory.Tool: return new Color(.50f, .40f, .22f);
                case ItemCategory.Medicine: return new Color(.28f, .52f, .40f);
                case ItemCategory.Food: return new Color(.46f, .50f, .24f);
                case ItemCategory.Refined: return new Color(.40f, .40f, .44f);
                default: return new Color(.34f, .30f, .26f);
            }
        }
    }
}
