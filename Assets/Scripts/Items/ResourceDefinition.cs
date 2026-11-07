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
    }
}
