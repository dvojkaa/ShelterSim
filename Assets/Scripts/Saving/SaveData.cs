using System.Collections.Generic;
using UnityEngine;

namespace SheltreSim.Saving
{
    [System.Serializable]
    public class SaveData
    {
        public Vector3 playerPosition;
        public float playerYaw;

        public float hunger;
        public float thirst;
        public float health;

        /// <summary>
        /// M10 QC — MeleeAttacker.attackDamage, the one piece of permanent player progression
        /// this project has. Equipping a looted weapon upgrade raises it for good, so leaving
        /// it out of the save made F5 -> Equip -> F9 hand the item back while keeping the
        /// bonus, stacking damage without limit.
        ///
        /// Initialised to -1 rather than 0 on purpose: JsonUtility leaves fields absent from
        /// the JSON at whatever the default constructor set, so a save file written before
        /// this field existed deserialises to -1, and the load path treats any non-positive
        /// value as "this save predates weapon upgrades, leave the current damage alone"
        /// instead of silently zeroing the player's melee damage.
        /// </summary>
        public float attackDamage = -1f;

        public List<PlacedPieceData> placedPieces = new List<PlacedPieceData>();
        public List<InventoryEntry> inventory = new List<InventoryEntry>();

        [System.Serializable]
        public class PlacedPieceData
        {
            public string pieceId;
            public Vector3 position;
            public Quaternion rotation;
        }

        [System.Serializable]
        public class InventoryEntry
        {
            public string resourceId;
            public int count;
        }
    }
}
