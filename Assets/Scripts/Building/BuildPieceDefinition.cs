using System.Collections.Generic;
using SheltreSim.Crafting;
using UnityEngine;

namespace SheltreSim.Building
{
    /// <summary>How build mode positions a piece. Free is the original grid-on-a-level-surface behaviour.</summary>
    public enum BuildPlacementRule
    {
        Free = 0,        // furniture and machines: 2 m grid on any level surface
        Foundation = 1,  // into a 2 m cell on terrain; checks all four corners
        CellFloor = 2,   // into a 2 m cell: on level ground, or between wall tops as an upper floor
        Wall = 3,        // on a cell edge, on a foundation, floor or wall
        Roof = 4,        // into a 2 m cell at the top of walls
        DoorInFrame = 5, // into the opening of a door frame
        Perimeter = 6,   // on a cell edge, on the ground or a foundation: fences and barricades
        Access = 7,      // ramps and stairs: into cells, against a foundation or on the ground
    }

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

        /// <summary>M22: set from HomesteadCatalog by M22HomesteadSetup. Free for everything not in that table.</summary>
        public BuildPlacementRule placementRule = BuildPlacementRule.Free;
    }
}
