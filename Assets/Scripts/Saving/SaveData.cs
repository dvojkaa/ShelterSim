using System.Collections.Generic;
using UnityEngine;

namespace SheltreSim.Saving
{
    [System.Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 6;
        /// <summary>Version 5 has the same world, just no state for the M22 systems, so it stays readable.</summary>
        public const int MinimumVersion = 5;

        // Zero identifies the unversioned M3-M15 format. Missing required values remain
        // invalid because SaveManager deserializes by overwriting a constructed instance.
        public int version;
        public string sceneName;
        public Vector3 playerPosition = new Vector3(float.NaN, float.NaN, float.NaN);
        public float playerYaw = float.NaN;
        public float playerPitch;
        public float hunger = float.NaN;
        public float thirst = float.NaN;
        public float health = float.NaN;
        public float attackDamage = -1f;
        public int gamePhase;
        public bool hasProceduralWorld;
        public int worldSeed;
        public float worldElapsedSeconds;
        public int expeditionUpgrades;
        public List<string> discoveredLandmarks = new List<string>();
        public int money;
        public int bunkerStages;
        public bool bunkerBuilding;
        public float bunkerBuildElapsed;
        public bool bunkerAnnex;
        public List<string> equippedGear = new List<string>();
        public int worldGenVersion;
        public List<SystemState> systemStates = new List<SystemState>();
        public List<SheltreSim.World.ProceduralWorldState.ChunkState> proceduralChunks
            = new List<SheltreSim.World.ProceduralWorldState.ChunkState>();

        public List<PlacedPieceData> placedPieces;
        public List<InventoryEntry> inventory;
        public List<NodeState> nodeStates;
        public List<CrateState> crateStates;
        public List<DoorState> doorStates;
        public List<SheltreSim.Enemies.EnemySpawner.RaiderState> raiderStates;

        [System.Serializable]
        public class PlacedPieceData
        {
            public string pieceId;
            public Vector3 position;
            public Quaternion rotation;
            public int waterStored = -1;
            public float collectionElapsed;
            public int farmStored = -1;
            public float farmElapsed;
            public string pieceState;
            public float structureHealth = -1f;
        }

        [System.Serializable]
        public class SystemState
        {
            public string key;
            public string json;
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
            public float regrowthElapsed;
        }

        [System.Serializable]
        public class CrateState
        {
            public string id;
            public bool searched;
        }

        [System.Serializable]
        public class DoorState
        {
            public string id;
            public bool open;
        }
    }
}
