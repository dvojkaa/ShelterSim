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
                "Assets/Prefabs/Building/WallSegmentPiece.prefab",
                new Color(0.3f, 0.5f, 0.7f));

            GameObject floorPrefab = CreatePrimitivePrefab(
                "FloorTilePiece",
                "floor_tile",
                new Vector3(2f, 0.2f, 2f),
                "Assets/Prefabs/Building/FloorTilePiece.prefab",
                new Color(0.4f, 0.6f, 0.75f));

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

            CreateExtraPieces(controller);
            AddStartingFurnishings();

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("M2 build system ready. 7 buildable pieces available (Wall, Floor, Workbench, Bed, Storage Crate, Water Collector, Generator). Press B to toggle build mode, scroll to cycle, R to rotate 90°, left-click to place, Escape to cancel.");
        }

        /// <summary>
        /// Playtest feedback: "let the bunker have something in it at the start" — an empty
        /// room with just a door felt bare. Places a few of the same pieces players can later
        /// build, already furnished, using the prefabs CreateExtraPieces just created. Positions
        /// are hand-picked to clear the M1 room's walls, the player spawn point (0,1,1.5), and
        /// the M7 apocalypse button (2.3,1,2.6) — M7 runs after this tool in the setup chain, so
        /// this just has to avoid that fixed spot, not look it up.
        /// </summary>
        private static void AddStartingFurnishings()
        {
            InstantiateFurnishing("Assets/Prefabs/Building/BedPiece.prefab", new Vector3(-2.2f, 0.25f, -1.0f), Quaternion.identity);
            InstantiateFurnishing("Assets/Prefabs/Building/StorageCratePiece.prefab", new Vector3(2.2f, 0.5f, -2.2f), Quaternion.identity);
            InstantiateFurnishing("Assets/Prefabs/Building/WorkbenchPiece.prefab", new Vector3(2.3f, 0.5f, 0.3f), Quaternion.Euler(0f, 90f, 0f));
        }

        private static void InstantiateFurnishing(string prefabPath, Vector3 position, Quaternion rotation)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"Starting furnishing prefab not found at {prefabPath} — skipping.");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>
        /// Playtest feedback: "add more things to the build menu, not just walls." These are
        /// placeable decoration for now — no unique gameplay function (no sleep mechanic, no
        /// crafting-at-station requirement, no actual power generation). Adding that would be
        /// new mechanics beyond what was asked; this is purely build-menu variety. Resource
        /// costs for these are assigned separately by M5CraftingSetup.
        /// </summary>
        private static void CreateExtraPieces(BuildModeController controller)
        {
            AddPiece(controller, "Workbench", "workbench", PrimitiveType.Cube,
                new Vector3(1.5f, 1.0f, 0.8f), new Color(0.5f, 0.35f, 0.2f),
                "Assets/Prefabs/Building/WorkbenchPiece.prefab", "Assets/Data/BuildPieces/Workbench.asset");

            AddPiece(controller, "Bed", "bed", PrimitiveType.Cube,
                new Vector3(1.0f, 0.5f, 2.0f), new Color(0.3f, 0.35f, 0.5f),
                "Assets/Prefabs/Building/BedPiece.prefab", "Assets/Data/BuildPieces/Bed.asset");

            AddPiece(controller, "Storage Crate", "storage_crate", PrimitiveType.Cube,
                new Vector3(1.0f, 1.0f, 1.0f), new Color(0.45f, 0.32f, 0.18f),
                "Assets/Prefabs/Building/StorageCratePiece.prefab", "Assets/Data/BuildPieces/StorageCrate.asset");

            AddPiece(controller, "Water Collector", "water_collector", PrimitiveType.Cylinder,
                new Vector3(0.8f, 1.2f, 0.8f), new Color(0.4f, 0.55f, 0.6f),
                "Assets/Prefabs/Building/WaterCollectorPiece.prefab", "Assets/Data/BuildPieces/WaterCollector.asset");

            AddPiece(controller, "Generator", "generator", PrimitiveType.Cube,
                new Vector3(1.2f, 1.2f, 1.2f), new Color(0.6f, 0.55f, 0.2f),
                "Assets/Prefabs/Building/GeneratorPiece.prefab", "Assets/Data/BuildPieces/Generator.asset");
        }

        private static void AddPiece(BuildModeController controller, string displayName, string id, PrimitiveType primitiveType,
            Vector3 footprintSize, Color color, string prefabPath, string assetPath)
        {
            GameObject prefab = CreatePrimitivePrefab(displayName.Replace(" ", "") + "Piece", id, footprintSize, prefabPath, color, primitiveType);
            BuildPieceDefinition definition = CreatePieceDefinition(id, displayName, prefab, footprintSize, assetPath);
            controller.availablePieces.Add(definition);
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static GameObject CreatePrimitivePrefab(string name, string pieceId, Vector3 scale, string path, Color color, PrimitiveType primitiveType = PrimitiveType.Cube)
        {
            GameObject temp = GameObject.CreatePrimitive(primitiveType);
            temp.name = name;
            temp.transform.localScale = scale;
            // Blue-ish tones so player-built pieces read as distinct from the grey/brown
            // world geometry. BuildModeController's ghost tint still overrides this during
            // preview (green/red), so there's no conflict with that feedback.
            EditorMaterialUtility.Apply(temp, color);

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
            // craftingCost defaults to an empty list (free) until M5's setup tool assigns a real recipe.

            AssetDatabase.CreateAsset(definition, path);

            return definition;
        }
    }
}
