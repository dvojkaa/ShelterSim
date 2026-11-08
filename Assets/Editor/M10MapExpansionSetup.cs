using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// Builds the M10 map expansion: a much larger zone beyond the M4 outdoor area's far
    /// boundary, scattered with ruined building shells and abandoned cars. This tool only
    /// builds geometry and drops bare "LootSpawn" marker Transforms inside each building and
    /// car — a separate pass (not this file) is responsible for turning those markers into
    /// actual lootable containers with roll tables. Run after
    /// "Tools > SheltreSim > Setup M4 Outdoor Area" has already produced a scene with an
    /// "OutdoorArea" GameObject.
    ///
    /// All "random" placement here is seeded `System.Random` picking among hand-defined
    /// candidate ranges with a reject-if-too-close spacing check — not `UnityEngine.Random`
    /// and not per-frame procedural generation — consistent with the project's decision that
    /// everything is deterministic content rolled once by an editor tool at scene-construction
    /// time.
    /// </summary>
    public static class M10MapExpansionSetup
    {
        private const int Seed = 20261108;

        // New ground: X in [-60,60] (120m wide), Z in [-239,-39] (200m deep), picking up
        // exactly where the M4 ground's far edge (Z=-39) left off. Footprint = 120*200 =
        // 24,000 m^2, versus the original 40*36 = 1,440 m^2 — about 16.7x, comfortably past
        // the requested "10x bigger".
        private const float GroundWidth = 120f;
        private const float GroundDepth = 200f;
        private const float GroundCenterX = 0f;
        private const float NearEdgeZ = -39f; // matches M4 OutdoorGround's far edge — no wall here, it's the connection seam.
        private const float FarEdgeZ = NearEdgeZ - GroundDepth; // -239
        private const float GroundCenterZ = (NearEdgeZ + FarEdgeZ) / 2f; // -139

        private const float BoundaryHeight = 3f;
        private const float BoundaryThickness = 0.2f;

        // Candidate placement area for buildings/cars: inset from the perimeter walls and
        // from the connection seam so nothing spawns clipped into a wall or straddling the
        // join with the original zone. Internal (not private): M12BiomeBlendSetup reuses the
        // exact same rectangle to scatter resource nodes/raiders within the same zone bounds.
        internal const float PlaceMinX = -55f;
        internal const float PlaceMaxX = 55f;
        internal const float PlaceMinZ = -235f;
        internal const float PlaceMaxZ = -45f;

        private const int BuildingCount = 12;
        private const int CarCount = 15;

        private const float BuildingWallHeight = 3f;
        private const float BuildingWallThickness = 0.2f;
        // Widened from 1.2m for the same reason M1SceneBuilder.DoorGap was (see its comment):
        // a raider's CharacterController is 1.0m across and needs ~1.16m of clear width with
        // its skinWidth, so the original 1.2m opening admitted the 0.7m player and essentially
        // nothing else. These ruins are where M12 put both the loot crates and 8 more raiders,
        // so a chase has to be able to follow the player inside one. No door leaf here, so the
        // full gap is the clear width.
        private const float BuildingDoorGap = 1.8f;

        // Car body dimensions are needed both to build the prop and to park the car's loot
        // marker clear of it (see CreateLootSpawn's call site in BuildCars), so they live here
        // rather than as locals inside BuildCarProp.
        private const float CarBodyWidth = 1.8f;
        private const float CarBodyHeight = 1.4f;
        private const float CarBodyLength = 4.2f;
        private const float CarWheelRadius = 0.35f;

        /// <summary>
        /// Y of a loot marker, chosen so the 0.4m crate the loot-population pass puts there
        /// rests on the ground/building floor (both of whose top faces are y = 0) instead of
        /// hovering above it. Markers used to sit at y = 1, which floated the crate 0.8m in
        /// mid-air inside buildings; for cars it was worse, see BuildCars.
        /// </summary>
        private const float LootCrateCenterY = 0.2f;

        private struct PlacedFootprint
        {
            public Vector3 Position;
            public float Radius;
            public bool IsBuilding;
        }

        [MenuItem("Tools/SheltreSim/Setup M10 Map Expansion")]
        public static void SetupMapExpansion()
        {
            GameObject outdoorArea = GameObject.Find("OutdoorArea");
            if (outdoorArea == null)
            {
                Debug.LogError("No 'OutdoorArea' GameObject found — run Tools > SheltreSim > Setup M4 Outdoor Area first.");
                return;
            }

            Transform farBoundary = outdoorArea.transform.Find("Boundary_Far");
            if (farBoundary != null)
            {
                UnityEngine.Object.DestroyImmediate(farBoundary.gameObject);
            }

            // Re-running this tool by hand from the Tools menu must not stack a second copy of
            // the whole zone (and a second set of LootSpawn markers / crates) on top of the
            // first. BatchSetupAll never hits this — M1SceneBuilder opens a brand-new empty
            // scene first — but the menu item is the path a human actually uses.
            Transform previousWasteland = outdoorArea.transform.Find("ExpandedWasteland");
            if (previousWasteland != null)
            {
                UnityEngine.Object.DestroyImmediate(previousWasteland.gameObject);
            }

            GameObject expandedWasteland = new GameObject("ExpandedWasteland");
            expandedWasteland.transform.SetParent(outdoorArea.transform, false);

            BuildGroundAndBoundary(expandedWasteland.transform);

            GameObject buildingsContainer = new GameObject("Buildings");
            buildingsContainer.transform.SetParent(expandedWasteland.transform, false);

            GameObject carsContainer = new GameObject("Cars");
            carsContainer.transform.SetParent(expandedWasteland.transform, false);

            System.Random rnd = new System.Random(Seed);
            List<PlacedFootprint> placed = new List<PlacedFootprint>();
            int lootSpawnCount = 0;

            int buildingsBuilt = BuildBuildings(rnd, placed, buildingsContainer.transform, ref lootSpawnCount);
            int carsBuilt = BuildCars(rnd, placed, carsContainer.transform, ref lootSpawnCount);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            float newGroundAreaSqm = GroundWidth * GroundDepth;
            Debug.Log($"M10 map expansion ready. New ground area: {newGroundAreaSqm:F0} m^2 (~{newGroundAreaSqm / 1440f:F1}x the original outdoor area). Buildings: {buildingsBuilt}. Cars: {carsBuilt}. Total LootSpawn markers: {lootSpawnCount}.");
        }

        private static void BuildGroundAndBoundary(Transform parent)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "ExpandedGround";
            ground.transform.SetParent(parent, false);
            ground.transform.localPosition = new Vector3(GroundCenterX, -0.1f, GroundCenterZ);
            ground.transform.localScale = new Vector3(GroundWidth, 0.2f, GroundDepth);
            EditorMaterialUtility.Apply(ground, new Color(0.3f, 0.27f, 0.2f));

            Color boundaryColor = new Color(0.28f, 0.27f, 0.25f);
            float halfWidth = GroundWidth / 2f;

            // Left and right perimeter walls run the full depth of the new zone.
            CreateBlock("Boundary_Expanded_Left", parent, new Vector3(-halfWidth, BoundaryHeight / 2f, GroundCenterZ), new Vector3(BoundaryThickness, BoundaryHeight, GroundDepth), boundaryColor);
            CreateBlock("Boundary_Expanded_Right", parent, new Vector3(halfWidth, BoundaryHeight / 2f, GroundCenterZ), new Vector3(BoundaryThickness, BoundaryHeight, GroundDepth), boundaryColor);

            // Far wall caps the zone at its deepest point. The near edge (Z = NearEdgeZ,
            // where this ground meets the M4 OutdoorGround) deliberately gets no wall — that's
            // the connection seam into the original area.
            CreateBlock("Boundary_Expanded_Far", parent, new Vector3(GroundCenterX, BoundaryHeight / 2f, FarEdgeZ), new Vector3(GroundWidth, BoundaryHeight, BoundaryThickness), boundaryColor);
        }

        private static int BuildBuildings(System.Random rnd, List<PlacedFootprint> placed, Transform buildingsContainer, ref int lootSpawnCount)
        {
            Color buildingBaseColor = new Color(0.42f, 0.38f, 0.32f);
            int built = 0;

            for (int i = 0; i < BuildingCount; i++)
            {
                float width = 4f + (float)rnd.NextDouble() * 4f;   // 4 - 8 m
                float depth = 4f + (float)rnd.NextDouble() * 2f;   // 4 - 6 m
                float radius = Mathf.Sqrt((width / 2f) * (width / 2f) + (depth / 2f) * (depth / 2f));

                if (!TryFindPosition(rnd, placed, radius, true, out Vector3 position))
                {
                    Debug.LogWarning($"M10: could not find a clear spot for building #{i + 1} after max attempts — skipping it.");
                    continue;
                }

                placed.Add(new PlacedFootprint { Position = position, Radius = radius, IsBuilding = true });

                int rotationSteps = rnd.Next(0, 4);
                float rotationY = rotationSteps * 90f;

                GameObject buildingRoot = new GameObject($"Building_{i + 1}");
                buildingRoot.transform.SetParent(buildingsContainer, false);
                buildingRoot.transform.localPosition = position;
                buildingRoot.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);

                Color wallColor = JitterColor(buildingBaseColor, rnd, 0.06f);
                BuildBuildingShell(buildingRoot.transform, width, depth, wallColor);

                int lootCount = rnd.Next(1, 4); // 1-3 inclusive
                for (int j = 0; j < lootCount; j++)
                {
                    float localX = ((float)rnd.NextDouble() * 2f - 1f) * Mathf.Max(0.1f, width / 2f - 0.6f);
                    float localZ = ((float)rnd.NextDouble() * 2f - 1f) * Mathf.Max(0.1f, depth / 2f - 0.6f);
                    CreateLootSpawn(buildingRoot.transform, new Vector3(localX, LootCrateCenterY, localZ));
                    lootSpawnCount++;
                }

                built++;
            }

            return built;
        }

        private static void BuildBuildingShell(Transform buildingRoot, float width, float depth, Color wallColor)
        {
            float halfWidth = width / 2f;
            float halfDepth = depth / 2f;

            CreateBlock("Floor", buildingRoot, new Vector3(0f, -BuildingWallThickness / 2f, 0f), new Vector3(width, BuildingWallThickness, depth), new Color(0.32f, 0.31f, 0.29f));

            CreateBlock("Wall_North", buildingRoot, new Vector3(0f, BuildingWallHeight / 2f, halfDepth), new Vector3(width, BuildingWallHeight, BuildingWallThickness), wallColor);
            CreateBlock("Wall_East", buildingRoot, new Vector3(halfWidth, BuildingWallHeight / 2f, 0f), new Vector3(BuildingWallThickness, BuildingWallHeight, depth), wallColor);
            CreateBlock("Wall_West", buildingRoot, new Vector3(-halfWidth, BuildingWallHeight / 2f, 0f), new Vector3(BuildingWallThickness, BuildingWallHeight, depth), wallColor);

            // South wall split around a doorway gap, same idiom as M1SceneBuilder.BuildRoom.
            float sideSpan = (width - BuildingDoorGap) / 2f;
            float sideCenterOffset = BuildingDoorGap / 2f + sideSpan / 2f;
            CreateBlock("Wall_South_A", buildingRoot, new Vector3(-sideCenterOffset, BuildingWallHeight / 2f, -halfDepth), new Vector3(sideSpan, BuildingWallHeight, BuildingWallThickness), wallColor);
            CreateBlock("Wall_South_B", buildingRoot, new Vector3(sideCenterOffset, BuildingWallHeight / 2f, -halfDepth), new Vector3(sideSpan, BuildingWallHeight, BuildingWallThickness), wallColor);
        }

        private static int BuildCars(System.Random rnd, List<PlacedFootprint> placed, Transform carsContainer, ref int lootSpawnCount)
        {
            Color[] carPalette =
            {
                new Color(0.55f, 0.25f, 0.18f), // rust red
                new Color(0.25f, 0.35f, 0.45f), // faded blue
                new Color(0.3f, 0.38f, 0.28f),  // dull green
                new Color(0.45f, 0.45f, 0.45f), // grey
            };

            // Bounding-circle radius for a ~1.8 x 4.2m body (half-width 0.9, half-length 2.1).
            const float carRadius = 2.3f;
            int built = 0;

            for (int i = 0; i < CarCount; i++)
            {
                if (!TryFindPosition(rnd, placed, carRadius, false, out Vector3 position))
                {
                    Debug.LogWarning($"M10: could not find a clear spot for car #{i + 1} after max attempts — skipping it.");
                    continue;
                }

                placed.Add(new PlacedFootprint { Position = position, Radius = carRadius, IsBuilding = false });

                float rotationY = (float)(rnd.NextDouble() * 360.0);

                GameObject carRoot = new GameObject($"Car_{i + 1}");
                carRoot.transform.SetParent(carsContainer, false);
                carRoot.transform.localPosition = position;
                carRoot.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);

                Color bodyColor = carPalette[rnd.Next(0, carPalette.Length)];
                BuildCarProp(carRoot.transform, bodyColor);

                // Behind the rear bumper, on the ground — NOT at the car's own centre, which is
                // where this marker started out. The car body is a solid 1.8 x 1.4 x 4.2 cube
                // with a BoxCollider spanning local Y 0.25-1.65, so a marker at (0, 0.9, 0) put
                // the resulting 0.4m crate *entirely inside* that collider: hidden behind an
                // opaque mesh and impossible to raycast (PlayerInteractor's ray stops at the
                // body, and the crate is the body's sibling, not its child, so the
                // GetComponentInParent fallback never reaches the LootContainer either). All 15
                // car crates were dead content. Measured clearance now: crate spans local
                // Z -2.75..-2.35 against a body that ends at -2.1, and Y 0..0.4 on the ground.
                CreateLootSpawn(carRoot.transform, new Vector3(0f, LootCrateCenterY, -(CarBodyLength / 2f + 0.45f)));
                lootSpawnCount++;

                built++;
            }

            return built;
        }

        private static void BuildCarProp(Transform carRoot, Color bodyColor)
        {
            const float bodyWidth = CarBodyWidth;
            const float bodyHeight = CarBodyHeight;
            const float bodyLength = CarBodyLength;
            const float wheelRadius = CarWheelRadius;
            const float wheelThickness = 0.3f;

            float bodyCenterY = wheelRadius + bodyHeight / 2f - 0.1f;

            CreateBlock("Body", carRoot, new Vector3(0f, bodyCenterY, 0f), new Vector3(bodyWidth, bodyHeight, bodyLength), bodyColor);

            // Cabin/windshield block, offset slightly forward and narrower than the body.
            Color cabinColor = JitterColor(bodyColor, new System.Random(bodyColor.GetHashCode()), 0.03f);
            CreateBlock("Cabin", carRoot, new Vector3(0f, bodyCenterY + bodyHeight / 2f + 0.25f, 0.3f), new Vector3(bodyWidth * 0.75f, 0.5f, bodyLength * 0.45f), cabinColor);

            Color wheelColor = new Color(0.08f, 0.08f, 0.08f);
            float wheelX = bodyWidth / 2f - 0.05f;
            float wheelZ = bodyLength / 2f - 0.7f;
            CreateWheel("Wheel_FL", carRoot, new Vector3(-wheelX, wheelRadius, wheelZ), wheelRadius, wheelThickness, wheelColor);
            CreateWheel("Wheel_FR", carRoot, new Vector3(wheelX, wheelRadius, wheelZ), wheelRadius, wheelThickness, wheelColor);
            CreateWheel("Wheel_RL", carRoot, new Vector3(-wheelX, wheelRadius, -wheelZ), wheelRadius, wheelThickness, wheelColor);
            CreateWheel("Wheel_RR", carRoot, new Vector3(wheelX, wheelRadius, -wheelZ), wheelRadius, wheelThickness, wheelColor);
        }

        /// <summary>
        /// Picks a candidate point within the shared placement rectangle using the seeded
        /// RNG, rejecting any candidate that falls within the required clearance of an
        /// already-placed building or car. Buildings require >=4m clear of anything else;
        /// car-to-car only requires >=3m. Deterministic given the same seed and call order.
        /// </summary>
        private static bool TryFindPosition(System.Random rnd, List<PlacedFootprint> placed, float objRadius, bool isBuilding, out Vector3 position)
        {
            const int maxAttempts = 500;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                float x = PlaceMinX + (float)rnd.NextDouble() * (PlaceMaxX - PlaceMinX);
                float z = PlaceMinZ + (float)rnd.NextDouble() * (PlaceMaxZ - PlaceMinZ);
                Vector3 candidate = new Vector3(x, 0f, z);

                bool clear = true;
                for (int k = 0; k < placed.Count; k++)
                {
                    PlacedFootprint entry = placed[k];
                    float requiredGap = (isBuilding || entry.IsBuilding) ? 4f : 3f;
                    float minDistance = objRadius + entry.Radius + requiredGap;
                    if (Vector3.Distance(candidate, entry.Position) < minDistance)
                    {
                        clear = false;
                        break;
                    }
                }

                if (clear)
                {
                    position = candidate;
                    return true;
                }
            }

            position = Vector3.zero;
            return false;
        }

        private static Color JitterColor(Color baseColor, System.Random rnd, float amount)
        {
            float r = Mathf.Clamp01(baseColor.r + (float)(rnd.NextDouble() * 2.0 - 1.0) * amount);
            float g = Mathf.Clamp01(baseColor.g + (float)(rnd.NextDouble() * 2.0 - 1.0) * amount);
            float b = Mathf.Clamp01(baseColor.b + (float)(rnd.NextDouble() * 2.0 - 1.0) * amount);
            return new Color(r, g, b);
        }

        private static void CreateLootSpawn(Transform parent, Vector3 localPosition)
        {
            GameObject lootSpawn = new GameObject("LootSpawn");
            lootSpawn.transform.SetParent(parent, false);
            lootSpawn.transform.localPosition = localPosition;
        }

        private static GameObject CreateBlock(string name, Transform parent, Vector3 localPosition, Vector3 scale, Color color)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localScale = scale;
            EditorMaterialUtility.Apply(block, color);
            return block;
        }

        private static GameObject CreateWheel(string name, Transform parent, Vector3 localPosition, float radius, float thickness, Color color)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = name;
            wheel.transform.SetParent(parent, false);
            wheel.transform.localPosition = localPosition;
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.transform.localScale = new Vector3(radius * 2f, thickness / 2f, radius * 2f);
            EditorMaterialUtility.Apply(wheel, color);
            return wheel;
        }
    }
}
