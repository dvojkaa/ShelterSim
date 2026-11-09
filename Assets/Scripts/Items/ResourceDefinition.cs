using UnityEngine;

namespace SheltreSim.Items
{
    public enum ItemCategory { Raw = 0, Refined = 1, Food = 2, Medicine = 3, Tool = 4, Weapon = 5, Armor = 6, Endgame = 7 }

    public enum ToolKind { None = 0, Axe = 1, Pickaxe = 2 }

    public enum GearSlot { None = 0, Weapon = 1, Armor = 2, Coat = 3, Ranged = 4 }

    /// <summary>
    /// One asset per item. Written by M21CraftingSetup from the code table in
    /// SheltreSim.Crafting.ItemCatalog (the single source of truth); do not hand-edit values.
    /// </summary>
    [CreateAssetMenu(fileName = "NewResource", menuName = "SheltreSim/Resource Definition")]
    public class ResourceDefinition : ScriptableObject
    {
        /// <summary>Stable identifier used for save-file lookup and inventory dictionary keys.</summary>
        public string id = "";

        public string displayName = "Resource";

        public ItemCategory category = ItemCategory.Raw;

        [TextArea] public string description = "";

        /// <summary>Marks this resource as edible (Eat button).</summary>
        public bool isFood = false;
        public float hungerRestored = 0f;

        /// <summary>Marks this resource as drinkable (Drink button).</summary>
        public bool isDrink = false;
        public float thirstRestored = 0f;

        /// <summary>Marks this resource as medicine (Use button).</summary>
        public bool isMedicine = false;
        public float healAmount = 0f;

        /// <summary>Equippable melee weapon: equipping SETS damage to weaponDamage; never consumed.</summary>
        public bool isWeapon = false;
        public float weaponDamage = 0f;

        /// <summary>Inventory action permanently adds a tier to carried capacity.</summary>
        public bool isBackpackUpgrade = false;

        /// <summary>Legacy (pre-M21 stacking weapon bonus). Unused; kept so old assets deserialize.</summary>
        public float damageBonus = 0f;

        /// <summary>Gather tool: best-owned tier applies, never consumed.</summary>
        public ToolKind toolKind = ToolKind.None;
        public int toolTier = 0;
        public int toolBonus = 0;

        /// <summary>Wearable armor / coat. damageReduction is a 0..1 fraction.</summary>
        public GearSlot gearSlot = GearSlot.None;
        public float damageReduction = 0f;
        /// <summary>Fraction of cold exposure removed (coat slot); exposed via PlayerGear for the weather system.</summary>
        public float warmth = 0f;
    }
}
