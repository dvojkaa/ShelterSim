using UnityEngine;

namespace SheltreSim.Building
{
    [CreateAssetMenu(fileName = "NewBuildPiece", menuName = "SheltreSim/Build Piece Definition")]
    public class BuildPieceDefinition : ScriptableObject
    {
        public string displayName = "Piece";

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
        /// Stubbed to 0 for M2 — resources are free for now. Real costs arrive once a
        /// resource/inventory system exists (per the milestone plan: M4 resource
        /// gathering, M5 crafting).
        /// </summary>
        public int resourceCost = 0;
    }
}
