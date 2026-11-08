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

        /// <summary>
        /// M-era QC gap: GameStateManager.CurrentPhase wasn't saved at all, so reloading always
        /// put the world back in Building phase with the apocalypse button live again — even
        /// after the player had already triggered it. Stored as a plain int (JsonUtility
        /// doesn't need the enum type available at the call site) and defaults to Building,
        /// which is also the correct behaviour for a save file written before this field
        /// existed (it predates apocalypse-phase persistence, so it was necessarily taken
        /// during Building anyway).
        /// </summary>
        public int gamePhase = (int)SheltreSim.Core.GamePhase.Building;

        public List<PlacedPieceData> placedPieces = new List<PlacedPieceData>();
        public List<InventoryEntry> inventory = new List<InventoryEntry>();

        /// <summary>
        /// Resource nodes/loot crates are static scene objects baked in at editor-tool run
        /// time, never instantiated at runtime — so unlike placedPieces they're matched back up
        /// on load by a stable hierarchy-path id (see SaveManager.ComputeStableId) rather than
        /// being destroyed and recreated.
        /// </summary>
        public List<NodeState> nodeStates = new List<NodeState>();
        public List<CrateState> crateStates = new List<CrateState>();

        [System.Serializable]
        public class PlacedPieceData
        {
            public string pieceId;
            public Vector3 position;
            public Quaternion rotation;

            /// <summary>
            /// Only populated for pieces with a WaterCollectorPiece component. Defaults to -1
            /// rather than 0 for the same reason as attackDamage above: JsonUtility fills
            /// fields absent from older save JSON with this default-constructor value, and -1
            /// distinguishes "this save predates Water Collector persistence, leave the fresh
            /// instance's stored water alone" from "this collector genuinely had 0 stored".
            /// </summary>
            public int waterStored = -1;
        }

        [System.Serializable]
        public class InventoryEntry
        {
            public string resourceId;
            public int count;
        }

        [System.Serializable]
        public class NodeState
        {
            public string id;
            public int remainingCharges;
            public bool depleted;
        }

        [System.Serializable]
        public class CrateState
        {
            public string id;
            public bool searched;
        }
    }
}
