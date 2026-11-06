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
