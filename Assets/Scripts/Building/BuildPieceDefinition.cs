using System.Collections.Generic;
using SheltreSim.Crafting;
using UnityEngine;

namespace SheltreSim.Building
{
    [CreateAssetMenu(fileName = "NewBuildPiece", menuName = "SheltreSim/Build Piece Definition")]
    public class BuildPieceDefinition : ScriptableObject
    {
        public string displayName = "Piece";

        /// <summary>
        /// Stable identifier used to look this definition back up by string (e.g. when
        /// restoring placed pieces from a save file). Not shown to the player.
        /// </summary>
        public string id = "";

        /// <summary>
        /// Visual + collider prefab for this piece. Assigned later by an editor tool —
        /// may be null until then.
        /// </summary>
        public GameObject prefab;

        /// <summary>
        /// Width/height/depth in meters. Used for the ghost-preview bounding box and
        /// the placement overlap check.
        /// </summary>
        public Vector3 footprintSize = new Vector3(2f, 3f, 0.2f);

        /// <summary>
        /// Resources required to place this piece (M5 — replaces the old M2 stub cost).
        /// Checked/spent via <see cref="SheltreSim.Crafting.CraftingSystem"/>.
        /// </summary>
        public List<CraftingCost> craftingCost = new List<CraftingCost>();
    }
}
