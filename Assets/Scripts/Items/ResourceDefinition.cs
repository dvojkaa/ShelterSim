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
    }
}
