using SheltreSim.Building;
using SheltreSim.Player;
using SheltreSim.Saving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// Wires the M2 building system into an already-open M1 scene: creates two stub
    /// placeable prefabs + piece definitions, and attaches a BuildModeController to the
    /// Player. Run this after "Tools > SheltreSim > Build M1 Bunker Scene" has already
    /// produced a scene with a "Player" GameObject.
    /// </summary>
    public static class M2BuildSystemSetup
    {
        [MenuItem("Tools/SheltreSim/Setup M2 Build System")]
        public static void SetupBuildSystem()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("No 'Player' GameObject found — run Tools > SheltreSim > Build M1 Bunker Scene first.");
                return;
            }

            FirstPersonController firstPersonController = player.GetComponent<FirstPersonController>();
            if (firstPersonController == null)
            {
                Debug.LogError("'Player' GameObject has no FirstPersonController — run Tools > SheltreSim > Build M1 Bunker Scene first.");
                return;
            }

            Transform cameraPivot = player.transform.Find("CameraPivot");
            Camera camera = cameraPivot != null ? cameraPivot.GetComponent<Camera>() : null;
            if (camera == null)
            {
                Debug.LogError("'Player/CameraPivot' GameObject with a Camera component not found.");
                return;
            }

            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Building");
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "BuildPieces");

            GameObject wallPrefab = CreatePrimitivePrefab(
                "WallSegmentPiece",
                "wall_segment",
                new Vector3(2f, 3f, 0.2f),
                "Assets/Prefabs/Building/WallSegmentPiece.prefab");

            GameObject floorPrefab = CreatePrimitivePrefab(
                "FloorTilePiece",
                "floor_tile",
                new Vector3(2f, 0.2f, 2f),
                "Assets/Prefabs/Building/FloorTilePiece.prefab");

            BuildPieceDefinition wallPiece = CreatePieceDefinition(
                "wall_segment",
                "Wall Segment",
                wallPrefab,
                new Vector3(2f, 3f, 0.2f),
                "Assets/Data/BuildPieces/WallSegment.asset");

            BuildPieceDefinition floorPiece = CreatePieceDefinition(
                "floor_tile",
                "Floor Tile",
                floorPrefab,
                new Vector3(2f, 0.2f, 2f),
                "Assets/Data/BuildPieces/FloorTile.asset");

            GameObject buildSystemObject = new GameObject("BuildSystem");
            buildSystemObject.transform.SetParent(player.transform, false);

            BuildModeController controller = buildSystemObject.AddComponent<BuildModeController>();
            controller.raycastCamera = camera;
            controller.availablePieces.Add(wallPiece);
            controller.availablePieces.Add(floorPiece);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("M2 build system ready. Press B to toggle build mode, scroll to cycle piece (Wall/Floor), R to rotate 90°, left-click to place, Escape to cancel.");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static GameObject CreatePrimitivePrefab(string name, string pieceId, Vector3 scale, string path)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            temp.name = name;
            temp.transform.localScale = scale;

            // Baked onto the prefab so every real instantiated copy carries a stable id for
            // save/load (see SheltreSim.Saving.PlacedPiece/PlacedPieceRegistry). Ghost previews
            // are built from this same prefab, so BuildModeController explicitly disables this
            // component on ghost instances — otherwise a ghost would self-register as a "placed"
            // piece while just being previewed.
            temp.AddComponent<PlacedPiece>().pieceId = pieceId;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);

            return prefab;
        }

        private static BuildPieceDefinition CreatePieceDefinition(string id, string displayName, GameObject prefab, Vector3 footprintSize, string path)
        {
            BuildPieceDefinition definition = ScriptableObject.CreateInstance<BuildPieceDefinition>();
            definition.id = id;
            definition.displayName = displayName;
            definition.prefab = prefab;
            definition.footprintSize = footprintSize;
            definition.resourceCost = 0;

            AssetDatabase.CreateAsset(definition, path);

            return definition;
        }
    }
}
