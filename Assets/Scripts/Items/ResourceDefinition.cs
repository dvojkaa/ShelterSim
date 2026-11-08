using UnityEngine;

namespace SheltreSim.Items
{
    [CreateAssetMenu(fileName = "NewResource", menuName = "SheltreSim/Resource Definition")]
    public class ResourceDefinition : ScriptableObject
    {
        /// <summary>
        /// Stable identifier used for save-file lookup and inventory dictionary keys.
        /// Not shown to the player.
        /// </summary>
        public string id = "";

        public string displayName = "Resource";

        /// <summary>
        /// M9 — marks this resource as edible via InventoryUI's Eat button. Separate bool
        /// rather than inferring from hungerRestored > 0 so a future 0-hunger "junk food"
        /// item (e.g. a quest trinket) isn't accidentally eatable.
        /// </summary>
        public bool isFood = false;

        public float hungerRestored = 25f;

        /// <summary>M10 — marks this resource as drinkable via InventoryUI's Drink button.</summary>
        public bool isDrink = false;

        public float thirstRestored = 30f;

        /// <summary>M10 — marks this resource as usable-as-medicine via InventoryUI's Use button.</summary>
        public bool isMedicine = false;

        public float healAmount = 40f;

        /// <summary>
        /// M10 — marks this resource as an equippable weapon upgrade via InventoryUI's Equip
        /// button. Permanent: equipping consumes the item and adds damageBonus to
        /// MeleeAttacker.attackDamage once, there's no un-equip.
        /// </summary>
        public bool isWeapon = false;

        public float damageBonus = 10f;
    }
}
