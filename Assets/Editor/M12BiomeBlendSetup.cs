using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SheltreSim.Enemies;
using SheltreSim.Items;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// M12 — scatters M9's nature resource nodes (trees/stone/iron ore/food bushes) and more
    /// raiders throughout M10's ExpandedWasteland zone, so the whole map reads as one
    /// continuous, mixed environment instead of "nature near the bunker, then a separate
    /// building/car-only zone with nothing growing in it and nothing dangerous in it".
    /// Playtester feedback (verbatim, translated): "it needs one big map where everything is —
    /// cars, houses, as well as trees, food, etc. — or biome style." This tool is the "biome
    /// style" resolution: rather than merging the two zones' content into one undifferentiated
    /// scatter (which would also mean re-authoring M10's already-verified building/car
    /// placement), it keeps both zones as they are and blends a second biome's content into
    /// each — nature spreads into the ruins, and the ruins' danger (raiders) spreads into what
    /// was a pressure-free 24,000m² — so exploring anywhere in the whole map encounters a mix,
    /// not a hard cutover at the Z=-39 seam.
    ///
    /// Doesn't touch the original M4/M9 zone, and doesn't touch M10's buildings/cars/loot
    /// crates — purely additive, placed in the gaps between the existing geometry.
    ///
    /// Run after Setup M9 World Resources (needs the Wood/Stone/IronOre/Berries assets it
    /// created), after Setup M10 Map Expansion (needs ExpandedWasteland plus its
    /// buildings/cars already built, so this tool can read their real placed bounds instead
    /// of re-deriving M10's own RNG state), and after Setup M10 Consumables And Loot (so the
    /// LootSpawn markers are already gone, replaced by crates with real colliders, by the
    /// time this tool scans for things to avoid).
    ///
    /// Placement is a seeded System.Random with a reject-if-too-close check against the
    /// ACTUAL renderer bounds of every Building_i/Car_i already in the scene — not a replay of
    /// M10's own RNG state, which this tool has no access to and shouldn't depend on — plus
    /// every node/raider this tool itself has already placed. Same deterministic-content
    /// philosophy as every other "randomized" pass in this project (M9's/M10's placement
    /// comments): seeded, rolled once at editor-tool-run time, not runtime randomness.
    /// </summary>
    public static class M12BiomeBlendSetup
    {
        private const int Seed = 20261109;

        private const int TreeCount = 12;
        private const int StoneCount = 8;
        private const int IronCount = 8;
        private const int FoodCount = 10;
        private const int RaiderCount = 8;

        // Uniform clearance added on top of the two footprints' own radii — generous enough
        // that a tree doesn't sprout against a building wall or a raider doesn't spawn
        // standing inside a car.
        private const float ClearanceGap = 3f;

        private const float TreeRadius = 1.2f;
        private const float RockRadius = 1.2f;
        private const float FoodRadius = 1.0f;
        private const float RaiderRadius = 1.0f;

        private struct PlacedFootprint
        {
            public Vector3 Position;
            public float Radius;
        }

        [MenuItem("Tools/SheltreSim/Setup M12 Biome Blend")]
        public static void SetupBiomeBlend()
        {
            GameObject outdoorArea = GameObject.Find("OutdoorArea");
            if (outdoorArea == null)
            {
                Debug.LogError("No 'OutdoorArea' GameObject found — run Tools > SheltreSim > Setup M4 Outdoor Area first.");
                return;
            }

            Transform expandedWasteland = outdoorArea.transform.Find("ExpandedWasteland");
            if (expandedWasteland == null)
            {
                Debug.LogError("No 'ExpandedWasteland' found under OutdoorArea — run Tools > SheltreSim > Setup M10 Map Expansion first.");
                return;
            }

            ResourceDefinition wood = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Data/Resources/Wood.asset");
            ResourceDefinition stone = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Data/Resources/Stone.asset");
            ResourceDefinition ironOre = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Data/Resources/IronOre.asset");
            ResourceDefinition berries = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Data/Resources/Berries.asset");

            if (wood == null || stone == null || ironOre == null || berries == null)
            {
                Debug.LogError("M12: could not load one or more resource assets (Wood/Stone/IronOre/Berries) — run Tools > SheltreSim > Setup M9 World Resources first.");
                return;
            }

            // Re-running this tool by hand must not stack a second copy on top of the first —
            // same idiom M10MapExpansionSetup already uses for its own re-run guard.
            Transform previous = outdoorArea.transform.Find("BiomeBlend");
            if (previous != null)
            {
                Object.DestroyImmediate(previous.gameObject);
            }

            GameObject biomeBlend = new GameObject("BiomeBlend");
            biomeBlend.transform.SetParent(outdoorArea.transform, false);

            GameObject natureContainer = new GameObject("ScatteredNature");
            natureContainer.transform.SetParent(biomeBlend.transform, false);

            GameObject raidersContainer = new GameObject("ScatteredRaiders");
            raidersContainer.transform.SetParent(biomeBlend.transform, false);

            List<PlacedFootprint> placed = CollectExistingFootprints(expandedWasteland);

            System.Random rnd = new System.Random(Seed);

            int treesBuilt = ScatterNodes(rnd, placed, natureContainer.transform, TreeCount, TreeRadius,
                (name, pos, parent) => M9WorldResourcesSetup.BuildTree(name, pos, wood, parent), "Tree");

            int stoneBuilt = ScatterNodes(rnd, placed, natureContainer.transform, StoneCount, RockRadius,
                (name, pos, parent) => M9WorldResourcesSetup.BuildRockCluster(name, pos, stone, new Color(0.5f, 0.5f, 0.5f), parent), "Stone");

            int ironBuilt = ScatterNodes(rnd, placed, natureContainer.transform, IronCount, RockRadius,
                (name, pos, parent) => M9WorldResourcesSetup.BuildRockCluster(name, pos, ironOre, new Color(0.45f, 0.25f, 0.2f), parent), "Iron");

            int foodBuilt = ScatterNodes(rnd, placed, natureContainer.transform, FoodCount, FoodRadius,
                (name, pos, parent) => M9WorldResourcesSetup.BuildFoodBush(name, pos, berries, parent), "Food");

            // Raiders need their pivot at y=1 (CharacterController default height 2, centered
            // on the pivot) to rest on the ground — M6EnemySetup's own spawn positions all use
            // y=1 for the same reason. Resource nodes build their own ground-relative visuals
            // internally and expect y=0, so this offset is raider-specific.
            int raidersBuilt = ScatterNodes(rnd, placed, raidersContainer.transform, RaiderCount, RaiderRadius,
                (name, pos, parent) => M6EnemySetup.SpawnEnemy(name, pos + Vector3.up, parent), "Raider_Wasteland");

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log($"M12 biome blend ready: {treesBuilt} Tree, {stoneBuilt} Stone, {ironBuilt} Iron Ore, {foodBuilt} Food Bush nodes and {raidersBuilt} raiders scattered through the ExpandedWasteland zone (requested {TreeCount}/{StoneCount}/{IronCount}/{FoodCount}/{RaiderCount}).");
        }

        /// <summary>
        /// Reads the real, already-built scene rather than replaying M10's RNG: every
        /// Building_i/Car_i under ExpandedWasteland gets a bounding-circle footprint derived
        /// from its actual renderer bounds (correct regardless of the building's per-instance
        /// rotation, width or depth, none of which this tool has access to otherwise).
        /// </summary>
        private static List<PlacedFootprint> CollectExistingFootprints(Transform expandedWasteland)
        {
            List<PlacedFootprint> result = new List<PlacedFootprint>();

            Transform buildings = expandedWasteland.Find("Buildings");
            if (buildings != null)
            {
                foreach (Transform building in buildings)
                {
                    result.Add(BoundingFootprint(building));
                }
            }

            Transform cars = expandedWasteland.Find("Cars");
            if (cars != null)
            {
                foreach (Transform car in cars)
                {
                    result.Add(BoundingFootprint(car));
                }
            }

            return result;
        }

        private static PlacedFootprint BoundingFootprint(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new PlacedFootprint { Position = root.position, Radius = 2f };
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float radius = Mathf.Sqrt(bounds.extents.x * bounds.extents.x + bounds.extents.z * bounds.extents.z);
            Vector3 center = bounds.center;
            center.y = 0f;
            return new PlacedFootprint { Position = center, Radius = radius };
        }

        /// <summary>
        /// Picks `count` clear positions within M10's own placement rectangle (so everything
        /// stays inset from the boundary walls and the connection seam, exactly like M10's
        /// buildings/cars already are) and invokes `build` at each. Every successfully placed
        /// item is appended to `placed` so later categories in the same call (and later calls
        /// in this same tool run) avoid it too.
        /// </summary>
        private static int ScatterNodes(System.Random rnd, List<PlacedFootprint> placed, Transform parent, int count, float objRadius, System.Action<string, Vector3, Transform> build, string namePrefix)
        {
            const int maxAttempts = 300;
            int built = 0;

            for (int i = 0; i < count; i++)
            {
                bool found = false;
                Vector3 position = Vector3.zero;

                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    float x = M10MapExpansionSetup.PlaceMinX + (float)rnd.NextDouble() * (M10MapExpansionSetup.PlaceMaxX - M10MapExpansionSetup.PlaceMinX);
                    float z = M10MapExpansionSetup.PlaceMinZ + (float)rnd.NextDouble() * (M10MapExpansionSetup.PlaceMaxZ - M10MapExpansionSetup.PlaceMinZ);
                    Vector3 candidate = new Vector3(x, 0f, z);

                    bool clear = true;
                    for (int k = 0; k < placed.Count; k++)
                    {
                        float minDistance = objRadius + placed[k].Radius + ClearanceGap;
                        if (Vector3.Distance(candidate, placed[k].Position) < minDistance)
                        {
                            clear = false;
                            break;
                        }
                    }

                    if (clear)
                    {
                        position = candidate;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    Debug.LogWarning($"M12: could not find a clear spot for {namePrefix}_{i + 1} after {maxAttempts} attempts — skipping it.");
                    continue;
                }

                build($"{namePrefix}_{i + 1}", position, parent);
                placed.Add(new PlacedFootprint { Position = position, Radius = objRadius });
                built++;
            }

            return built;
        }
    }
}
