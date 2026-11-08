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
            AddCompoundDetail("Assets/Prefabs/Building/WorkbenchPiece.prefab", BuildWorkbenchDetail);

            AddPiece(controller, "Bed", "bed", PrimitiveType.Cube,
                new Vector3(1.0f, 0.5f, 2.0f), new Color(0.3f, 0.35f, 0.5f),
                "Assets/Prefabs/Building/BedPiece.prefab", "Assets/Data/BuildPieces/Bed.asset");
            AddCompoundDetail("Assets/Prefabs/Building/BedPiece.prefab", BuildBedDetail);

            AddPiece(controller, "Storage Crate", "storage_crate", PrimitiveType.Cube,
                new Vector3(1.0f, 1.0f, 1.0f), new Color(0.45f, 0.32f, 0.18f),
                "Assets/Prefabs/Building/StorageCratePiece.prefab", "Assets/Data/BuildPieces/StorageCrate.asset");
            AddCompoundDetail("Assets/Prefabs/Building/StorageCratePiece.prefab", BuildStorageCrateDetail);

            // M11 QC: Unity's Cylinder primitive mesh is 2 units TALL (and 1 unit across) at
            // scale 1, unlike Cube which is 1x1x1 — so a localScale of y=1.2 produced a 2.4m
            // cylinder while footprintSize claimed 1.2m. BuildModeController derives both the
            // ghost's rest-on-the-surface Y offset and the CheckBox overlap extents from
            // footprintSize, so the placed piece ended up sunk 0.6m into the floor with an
            // overlap test covering only its lower half (measured: collider spanned y -0.6 to
            // 1.8 for a placement on y=0). Explicit prefabScale with a halved Y keeps the real
            // world bounds equal to footprintSize, which is what the rest of the build system
            // assumes. This is the only non-Cube piece; any future one needs the same care.
            // Left as a plain barrel cylinder (no added compound detail) — it's already a
            // reasonable non-cube shape, and this is the one piece where a bounding-box mistake
            // has actually shipped before, so it's not worth the added risk for a cosmetic-only gain.
            AddPiece(controller, "Water Collector", "water_collector", PrimitiveType.Cylinder,
                new Vector3(0.8f, 1.2f, 0.8f), new Color(0.4f, 0.55f, 0.6f),
                "Assets/Prefabs/Building/WaterCollectorPiece.prefab", "Assets/Data/BuildPieces/WaterCollector.asset",
                prefabScale: new Vector3(0.8f, 0.6f, 0.8f));

            AddPiece(controller, "Generator", "generator", PrimitiveType.Cube,
                new Vector3(1.2f, 1.2f, 1.2f), new Color(0.6f, 0.55f, 0.2f),
                "Assets/Prefabs/Building/GeneratorPiece.prefab", "Assets/Data/BuildPieces/Generator.asset");
            AddCompoundDetail("Assets/Prefabs/Building/GeneratorPiece.prefab", BuildGeneratorDetail);
        }

        /// <summary>
        /// Opens an already-saved piece prefab asset (via <see cref="AddPiece"/> above) back up
        /// for editing, lets <paramref name="build"/> add purely-cosmetic child geometry to its
        /// root, and saves it back to the same asset path — WITHOUT touching AddPiece's or
        /// CreatePrimitivePrefab's existing signatures/behaviour, so the footprintSize/prefabScale
        /// contract they already implement is untouched. Every <paramref name="build"/> callback
        /// below is required to keep its added geometry's combined bounds within the root's own
        /// existing local unit-cube (-0.5..0.5 per axis before the root's own scale is applied) —
        /// i.e. inside the box that BuildPieceDefinition.footprintSize already describes — so
        /// footprintSize needs no change for any of these pieces. New GameObjects created inside
        /// `build` and parented under `root.transform` are auto-migrated into the prefab's
        /// isolated edit scene by Transform.SetParent; nothing special is needed for that.
        /// </summary>
        private static void AddCompoundDetail(string prefabPath, System.Action<GameObject> build)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                build(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Tabletop slab + 4 legs instead of a bare cube. Replaces the root cube's own
        /// MeshRenderer (disabled, not destroyed — its BoxCollider stays, so the piece's real
        /// world footprint/obstruction-check volume is exactly unchanged) with child geometry
        /// whose combined bounds are verified below to stay within the root's local -0.5..0.5
        /// unit cube on every axis, which is what the root's own BoxCollider already covers.
        /// </summary>
        private static void BuildWorkbenchDetail(GameObject root)
        {
            HideRootMesh(root);

            Color topColor = new Color(0.55f, 0.42f, 0.24f);
            Color legColor = new Color(0.32f, 0.22f, 0.11f);

            // Tabletop: local y 0.4..0.5 (flush with the box top), full X/Z width.
            CreateDetailChild(root.transform, "Tabletop", PrimitiveType.Cube,
                new Vector3(0f, 0.45f, 0f), new Vector3(1f, 0.1f, 1f), topColor);

            // 4 legs: local y -0.5..0.4 (floor to tabletop underside). Cylinder mesh is 2 units
            // tall at scale 1 (see the Water Collector comment above), so localScale.y = desired
            // local-height-fraction / 2 — here 0.9 / 2 = 0.45. X/Z localScale is world-diameter
            // (0.08m) divided by the root's prefabScale component on that axis (1.5, 0.8) so the
            // legs read as round despite the root's non-uniform scale.
            const float legLocalY = -0.05f;
            const float legScaleY = 0.45f;
            Vector3 legScale = new Vector3(0.08f / 1.5f, legScaleY, 0.08f / 0.8f);
            CreateDetailChild(root.transform, "Leg_FL", PrimitiveType.Cylinder, new Vector3(-0.4f, legLocalY, 0.35f), legScale, legColor);
            CreateDetailChild(root.transform, "Leg_FR", PrimitiveType.Cylinder, new Vector3(0.4f, legLocalY, 0.35f), legScale, legColor);
            CreateDetailChild(root.transform, "Leg_BL", PrimitiveType.Cylinder, new Vector3(-0.4f, legLocalY, -0.35f), legScale, legColor);
            CreateDetailChild(root.transform, "Leg_BR", PrimitiveType.Cylinder, new Vector3(0.4f, legLocalY, -0.35f), legScale, legColor);
        }

        /// <summary>
        /// Flat mattress slab + a small raised pillow block, added on top of the (still visible,
        /// still the real-footprint) root cube acting as the bed frame. Both children's local
        /// bounds are kept inside -0.5..0.5 on every axis — verified in the inline comments below
        /// — so footprintSize needs no change.
        /// </summary>
        private static void BuildBedDetail(GameObject root)
        {
            Color mattressColor = new Color(0.78f, 0.78f, 0.82f);
            Color pillowColor = new Color(0.88f, 0.88f, 0.85f);

            // Mattress: localScale y=0.55 (half-extent 0.275) centered at y=0.15 -> spans
            // -0.125..0.425, within -0.5..0.5. X/Z half-extents 0.44/0.46 also within bounds.
            CreateDetailChild(root.transform, "Mattress", PrimitiveType.Cube,
                new Vector3(0f, 0.15f, 0f), new Vector3(0.88f, 0.55f, 0.92f), mattressColor);

            // Pillow: localScale y=0.18 (half-extent 0.09) centered at y=0.40 -> spans
            // 0.31..0.49, within -0.5..0.5 (does not poke through the top face). Z centered at
            // -0.35 (half-extent 0.11) -> spans -0.46..-0.24, within bounds, near one end.
            CreateDetailChild(root.transform, "Pillow", PrimitiveType.Cube,
                new Vector3(0f, 0.40f, -0.35f), new Vector3(0.5f, 0.18f, 0.22f), pillowColor);
        }

        /// <summary>
        /// Two thin "binding strap" bands across the still-visible crate body, for texture. Both
        /// kept well inside the root's -0.5..0.5 unit cube (half-extent 0.06 in Y, centered at
        /// ±0.15, and inset 1% in X/Z to avoid z-fighting with the crate's own faces), so
        /// footprintSize needs no change.
        /// </summary>
        private static void BuildStorageCrateDetail(GameObject root)
        {
            Color strapColor = new Color(0.24f, 0.17f, 0.09f);

            CreateDetailChild(root.transform, "Strap_Lower", PrimitiveType.Cube,
                new Vector3(0f, -0.15f, 0f), new Vector3(0.98f, 0.12f, 0.98f), strapColor);
            CreateDetailChild(root.transform, "Strap_Upper", PrimitiveType.Cube,
                new Vector3(0f, 0.15f, 0f), new Vector3(0.98f, 0.12f, 0.98f), strapColor);
        }

        /// <summary>
        /// Main body + a small exhaust-pipe stub + a vent panel, replacing the root cube's bare
        /// look (MeshRenderer disabled, BoxCollider kept, matching Workbench's approach). The
        /// body is shortened to local y -0.5..0.3 specifically to leave y 0.3..0.5 free for the
        /// pipe, so the combined stack is flush with the original box top at y=0.5 rather than
        /// exceeding it — footprintSize needs no change.
        /// </summary>
        private static void BuildGeneratorDetail(GameObject root)
        {
            HideRootMesh(root);

            Color bodyColor = new Color(0.58f, 0.53f, 0.2f);
            Color pipeColor = new Color(0.25f, 0.25f, 0.25f);
            Color ventColor = new Color(0.15f, 0.15f, 0.15f);

            // Body: local y -0.5..0.3 (half-extent 0.4, centered at -0.1) — flush with the floor,
            // stops short of the box top to leave room for the pipe.
            CreateDetailChild(root.transform, "Body", PrimitiveType.Cube,
                new Vector3(0f, -0.1f, 0f), new Vector3(1f, 0.8f, 1f), bodyColor);

            // Exhaust pipe: local y 0.3..0.5 (half-extent 0.1, centered at 0.4) — flush with the
            // body top and the box top, never exceeding either. Root prefabScale is uniform
            // (1.2, 1.2, 1.2) here, so localScale.y = desired-local-height-fraction / 2 = 0.2 / 2
            // (the /2 is the same Cylinder-mesh-is-2-units-tall factor as the Workbench legs);
            // X/Z localScale = desired world diameter (0.14m) / 1.2.
            CreateDetailChild(root.transform, "ExhaustPipe", PrimitiveType.Cylinder,
                new Vector3(0.25f, 0.4f, -0.25f), new Vector3(0.14f / 1.2f, 0.1f, 0.14f / 1.2f), pipeColor);

            // Vent panel: thin flush plate on the front face (z half-extent 0.01, centered at
            // -0.49 -> spans -0.5..-0.48, flush with but not exceeding the front face).
            CreateDetailChild(root.transform, "VentPanel", PrimitiveType.Cube,
                new Vector3(0f, -0.1f, -0.49f), new Vector3(0.3f, 0.3f, 0.02f), ventColor);
        }

        /// <summary>Disables (not destroys) the root's own MeshRenderer so its BoxCollider — and therefore the real footprint — stays exactly as CreatePrimitivePrefab built it.</summary>
        private static void HideRootMesh(GameObject root)
        {
            MeshRenderer rootRenderer = root.GetComponent<MeshRenderer>();
            if (rootRenderer != null)
            {
                rootRenderer.enabled = false;
            }
        }

        /// <summary>Decorative-only child primitive: collider destroyed immediately so it can never duplicate or shift the root's own obstruction-check volume.</summary>
        private static GameObject CreateDetailChild(Transform parent, string name, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject child = GameObject.CreatePrimitive(primitiveType);
            child.name = name;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = localScale;
            EditorMaterialUtility.Apply(child, color);
            Object.DestroyImmediate(child.GetComponent<Collider>());
            return child;
        }

        /// <summary>
        /// <paramref name="footprintSize"/> is the piece's real WORLD size — the build system
        /// derives the ghost's Y offset and the overlap-test extents from it, so it has to
        /// match what the collider actually occupies. <paramref name="prefabScale"/> defaults
        /// to the same vector because that's true for Cube primitives (1x1x1 at scale 1); pass
        /// it explicitly for any primitive whose unit mesh isn't 1x1x1 (Cylinder is 2 tall).
        /// </summary>
        private static void AddPiece(BuildModeController controller, string displayName, string id, PrimitiveType primitiveType,
            Vector3 footprintSize, Color color, string prefabPath, string assetPath, Vector3? prefabScale = null)
        {
            GameObject prefab = CreatePrimitivePrefab(displayName.Replace(" ", "") + "Piece", id, prefabScale ?? footprintSize, prefabPath, color, primitiveType);
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
