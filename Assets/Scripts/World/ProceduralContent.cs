using System;
using System.Collections.Generic;
using System.Diagnostics;
using SheltreSim.Building;
using SheltreSim.Crafting;
using SheltreSim.Items;
using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>
    /// Deterministic, disposable chunk dressing. Stateful children have stable slot names.
    /// The same chunk is built twice in a game: as the normal world, then ruined after the bombs.
    /// Every house and building draws from its own per-slot random, so what only exists in the ruined
    /// version (crates, rubble) can't change the size or place of anything built after it.
    /// </summary>
    public static class ProceduralContent
    {
        internal static bool CapturePerformanceTimings;
        internal static bool BuildingRuinedPass;
        internal static float LastTownStreetMs, LastTownBuildingMs, LastStaticCombineMs, LastFurnitureVisualMs, LastBoxMs;
        internal static int LastBoxCount;

        private static long StartTiming() => CapturePerformanceTimings ? Stopwatch.GetTimestamp() : 0L;
        private static float TimingElapsedMs(long start) =>
            (float)((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);

        public static GameObject Build(Transform parent, Vector2Int coordinate, int seed, ProceduralWorldAssets assets, bool ruined)
            => Build(parent, coordinate, seed, assets, ruined, null);

        /// <summary>
        /// Builds the immediate chunk dressing and optionally returns town/suburb buildings as
        /// ordered work items. Streaming spreads expensive building interiors across frames;
        /// direct callers keep the original synchronous behavior.
        /// </summary>
        internal static GameObject Build(Transform parent, Vector2Int coordinate, int seed,
            ProceduralWorldAssets assets, bool ruined, List<System.Action> deferredBuildings)
        {
            BuildingRuinedPass = ruined;
            var root = new GameObject("Content");
            root.transform.SetParent(parent, false);
            if (assets == null) return root;
            if (CapturePerformanceTimings)
            {
                LastTownStreetMs = LastTownBuildingMs = LastStaticCombineMs = LastFurnitureVisualMs = LastBoxMs = 0f;
                LastBoxCount = 0;
            }
            var random = new System.Random(ContentSeed(coordinate, seed));
            float x = coordinate.x * (float)WorldLayout.ChunkSize;
            float z = coordinate.y * (float)WorldLayout.ChunkSize;
            if (WorldLayout.IsTownChunk(coordinate, seed))
                Town(root.transform, x, z, seed, assets, coordinate, ruined, deferredBuildings);
            else if (WorldLayout.IsSuburbChunk(coordinate, seed))
                Suburb(root.transform, x, z, seed, assets, coordinate, ruined, deferredBuildings);
            else
                Nature(root.transform, x, z, seed, assets, random, ruined);
            ForagePatches(root.transform, coordinate, seed, assets, ruined);
            return root;
        }

        static int ContentSeed(Vector2Int coordinate, int seed)
        {
            unchecked
            {
                uint value = (uint)seed ^ (uint)coordinate.x * 374761393u ^ (uint)coordinate.y * 668265263u;
                value = (value ^ (value >> 13)) * 1274126177u;
                return (int)(value ^ (value >> 16));
            }
        }

        static System.Random SlotRandom(Vector2Int coordinate, int seed, int slot) =>
            new System.Random(unchecked(ContentSeed(coordinate, seed) + (slot + 1) * 7919));

        static float Range(System.Random random, float min, float max)
            => min + (float)random.NextDouble() * (max - min);

        static bool Clear(float x, float z, float margin, int seed)
        {
            // Check the whole footprint, including the home plot's edge.
            for (int ix = -1; ix <= 1; ix++)
                for (int iz = -1; iz <= 1; iz++)
                    if (WorldLayout.IsHomePlot(x + ix * margin, z + iz * margin) ||
                        WorldLayout.IsRoad(x + ix * margin, z + iz * margin) ||
                        WorldLayout.IsConnectorRoad(x + ix * margin, z + iz * margin, seed)) return false;
            return true;
        }

        static void Nature(Transform parent, float originX, float originZ, int seed,
            ProceduralWorldAssets assets, System.Random random, bool ruined)
        {
            int biome = WorldLayout.BiomeIndexAt(originX + 64, originZ + 64, seed);
            int count = biome == 1 ? 70 : biome == 4 ? 28 : biome == 2 ? 12 : 22;
            // X/Z are world position, Y is the reserved horizontal radius.
            var occupied = new List<Vector3>(count);
            for (int slot = 0; slot < count; slot++)
            {
                float x = originX + Range(random, 5, 123);
                float z = originZ + Range(random, 5, 123);
                float roll = (float)random.NextDouble();
                float scale = Range(random, .8f, 1.3f);
                float yaw = Range(random, 0, 360);
                if (!Clear(x, z, 3, seed) || WorldLayout.IsWater(x, z, seed, 3f)) continue;
                int localBiome = WorldLayout.BiomeIndexAt(x, z, seed);
                float treeChance = localBiome == 1 ? .65f : localBiome == 4 ? .35f : localBiome == 2 ? .04f : .2f;
                float foodChance = localBiome == 1 || localBiome == 4 ? .22f : .1f;
                bool tree = roll < treeChance;
                bool food = !tree && roll < treeChance + foodChance;
                bool ore = slot % 5 == 0;
                ResourceDefinition resource = tree ? assets.wood : food ? assets.berries : ore ? assets.iron : assets.stone;
                GameObject model;
                if (tree || food) model = Pick(tree ? assets.trees : assets.bushes, random);
                else
                {
                    // Keep the visual ore type in step with the resource handed out.
                    int index = (ore ? 2 : 0) + random.Next(2);
                    model = assets.rocks != null && index < assets.rocks.Length ? assets.rocks[index] : null;
                }
                if (resource == null) continue;
                // Reserve enough space for rotated/scaled colliders as well as tree canopies.
                float radius = (tree ? 2f : food ? 1f : 1.05f) * scale;
                bool blocked = false;
                foreach (var previous in occupied)
                {
                    float dx = x - previous.x;
                    float dz = z - previous.z;
                    float separation = radius + previous.y;
                    if (dx * dx + dz * dz < separation * separation)
                    {
                        blocked = true;
                        break;
                    }
                }
                // All slot random draws happen before rejection, preserving deterministic rolls.
                if (blocked) continue;
                occupied.Add(new Vector3(x, radius, z));
                // Before the bombs the countryside is just trees: nothing to gather, nothing to find.
                if (!ruined && !tree) continue;
                var nodeRoot = new GameObject("Node_" + slot.ToString("D2"));
                nodeRoot.SetActive(false);
                nodeRoot.transform.SetParent(parent, false);
                nodeRoot.transform.position = new Vector3(x, WorldLayout.HeightAt(x, z, seed), z);
                nodeRoot.transform.rotation = Quaternion.Euler(0, yaw, 0);
                if (model != null) Visual(model, nodeRoot.transform, Vector3.one * scale, assets.atlasMaterial);
                else Box(nodeRoot.transform, "ResourceVisual", new Vector3(0, .45f, 0),
                    new Vector3(.8f, .9f, .8f), assets.structureMaterial, false);
                // A trunk-sized collider lets the player stand beside a canopy and gather it.
                if (tree)
                {
                    var collider = nodeRoot.AddComponent<CapsuleCollider>();
                    collider.radius = .24f * scale;
                    collider.height = 2.8f * scale;
                    collider.center = Vector3.up * collider.height * .5f;
                }
                else
                {
                    var collider = nodeRoot.AddComponent<BoxCollider>();
                    collider.size = food ? new Vector3(1.0f, 1.5f, 1.0f) * scale : new Vector3(1.5f, .9f, 1.4f) * scale;
                    collider.center = Vector3.up * collider.size.y * .5f;
                }
                if (ruined)
                {
                    var node = nodeRoot.AddComponent<ResourceNode>();
                    node.resourceType = resource;
                    node.remainingCharges = tree ? 5 : food ? 3 : 4;
                    node.quantityPerHarvest = tree ? 2 : 1;
                    node.regrowSeconds = food ? 240 : 360;
                    if (food) { node.bonusId = "plant_fiber"; node.bonusChance = .6f; }
                    else if (!tree) { node.bonusId = "coal"; node.bonusChance = ore ? .35f : .2f; }
                }
                nodeRoot.SetActive(true);
            }
        }

        /// <summary>
        /// Independent seeded slots keep adding grass from changing roads, trees, loot, or existing resource IDs.
        /// Patches are scenery before the bombs and become harvestable fiber sources in the ruined world.
        /// </summary>
        static void ForagePatches(Transform parent, Vector2Int coordinate, int seed,
            ProceduralWorldAssets assets, bool ruined)
        {
            if (assets == null || assets.plantFiber == null || (assets.tallGrass == null && assets.reeds == null)) return;
            float originX = coordinate.x * (float)WorldLayout.ChunkSize;
            float originZ = coordinate.y * (float)WorldLayout.ChunkSize;
            // Dense town cores contain real buildings; leave their streets and interiors to authored urban dressing.
            if (WorldLayout.IsTownChunk(coordinate, seed)) return;
            int centerBiome = WorldLayout.BiomeIndexAt(originX + 64f, originZ + 64f, seed);
            int count = centerBiome == 1 ? 38 : centerBiome == 4 ? 48 : centerBiome == 2 ? 8 : centerBiome == 3 ? 12 : centerBiome == 5 ? 18 : 28;
            var accepted = new List<Vector2>(count);
            for (int slot = 0; slot < count; slot++)
            {
                System.Random random = SlotRandom(coordinate, seed, 4000 + slot);
                float x = originX + Range(random, 3.5f, 124.5f);
                float z = originZ + Range(random, 3.5f, 124.5f);
                float yaw = Range(random, 0f, 360f);
                float scale = Range(random, .85f, 1.2f);
                int charges = 1 + random.Next(2);
                int yield = 1 + random.Next(2);
                float seedChance = .06f + (float)random.NextDouble() * .04f;
                if (WorldLayout.IsHomePlot(x, z) || WorldLayout.IsWater(x, z, seed, 1f) ||
                    WorldLayout.IsRoad(x, z, 2f) || WorldLayout.IsConnectorRoad(x, z, seed, 2f) ||
                    !Clear(x, z, .8f, seed) || NearSuburbHouse(x, z, coordinate, seed)) continue;

                int localBiome = WorldLayout.BiomeIndexAt(x, z, seed);
                bool wetEdge = WorldLayout.IsWater(x, z, seed, 4f);
                bool useReeds = (localBiome == 4 || wetEdge) && !WorldLayout.IsWater(x, z, seed);
                GameObject model = useReeds && assets.reeds != null ? assets.reeds : assets.tallGrass;
                if (model == null) model = assets.reeds;
                if (model == null) continue;
                bool crowded = false;
                float minSeparation = useReeds ? 3.1f : 2.7f;
                foreach (Vector2 previous in accepted)
                    if ((new Vector2(x, z) - previous).sqrMagnitude < minSeparation * minSeparation)
                    { crowded = true; break; }
                if (crowded) continue;
                accepted.Add(new Vector2(x, z));

                var root = new GameObject("Node_Grass" + slot.ToString("D2"));
                root.SetActive(false);
                root.transform.SetParent(parent, false);
                root.transform.SetPositionAndRotation(new Vector3(x, WorldLayout.HeightAt(x, z, seed), z), Quaternion.Euler(0f, yaw, 0f));
                Visual(model, root.transform, Vector3.one * scale, assets.atlasMaterial);
                if (ruined)
                {
                    var collider = root.AddComponent<BoxCollider>();
                    collider.center = Vector3.up * .14f;
                    collider.size = new Vector3(.55f, .28f, .55f); // below the player's step height
                    var node = root.AddComponent<ResourceNode>();
                    node.resourceType = assets.plantFiber;
                    node.remainingCharges = charges;
                    node.quantityPerHarvest = yield;
                    node.regrowSeconds = 420f;
                    node.bonusId = "seeds";
                    node.bonusChance = seedChance;
                    node.cutOnHit = true;
                }
                root.SetActive(true);
            }
        }

        private static bool NearSuburbHouse(float x, float z, Vector2Int coordinate, int seed)
        {
            if (!WorldLayout.IsSuburbChunk(coordinate, seed)) return false;
            float originX = coordinate.x * (float)WorldLayout.ChunkSize;
            float originZ = coordinate.y * (float)WorldLayout.ChunkSize;
            for (int slot = 0; slot < 6; slot++)
            {
                var lot = SlotRandom(coordinate, seed, slot);
                if (lot.NextDouble() >= .8) continue;
                float houseX = originX + 24f + (slot % 3) * 40f;
                float houseZ = originZ + 64f + (slot >= 3 ? 27f : -27f);
                if (Mathf.Abs(x - houseX) < 13f && Mathf.Abs(z - houseZ) < 16f) return true;
            }
            return false;
        }

        static void Town(Transform parent, float originX, float originZ, int seed,
            ProceduralWorldAssets assets, Vector2Int coordinate, bool ruined, List<System.Action> deferredBuildings = null)
        {
            // Nine separated lots with two crossing streets; these roads join the boundary ring.
            long timingStart = StartTiming();
            for (int line = 0; line < 4; line++)
            {
                float offset = line == 0 ? 3.5f : line == 1 ? 44 : line == 2 ? 84 : 124.5f;
                Street(parent, originX + offset, originZ + 64, true, seed, assets, true);
                Street(parent, originX + 64, originZ + offset, false, seed, assets, true);
            }
            if (CapturePerformanceTimings) LastTownStreetMs += TimingElapsedMs(timingStart);
            timingStart = StartTiming();
            int blocks = 0;
            for (int slot = 0; slot < 9; slot++)
            {
                var random = SlotRandom(coordinate, seed, slot);
                // Corner lots are guaranteed; middle lots vary so districts have open spaces.
                bool corner = slot % 3 != 1 && slot / 3 != 1;
                bool occupied = corner || random.NextDouble() < .65;
                float x = originX + 24 + (slot % 3) * 40;
                float z = originZ + 24 + (slot / 3) * 40;
                int floors = random.Next(1, 6);
                // Keep the original occupancy and height draws above unchanged. New type/layout rolls
                // never advance the lot stream, and are identical before and after the bombs.
                var layout = SlotRandom(coordinate, seed, 500 + slot);
                bool block = corner && layout.NextDouble() < .45 && blocks < 2;
                bool office = slot == 4 && layout.NextDouble() < .5;
                float margin = block ? 11f : office ? 9f : 9f;
                // Town chunks are the inner four cells of a 512 m macro. Every corner of
                // these 128 m chunks stays inside WorldLayout's 215 m flattened town pad,
                // so repeating the 3x3 terrain-height slope check can never reject this lot.
                if (!occupied || !Clear(x, z, margin, seed)) continue;
                if (block) blocks++;
                int buildingFloors = block ? 4 + layout.Next(3) : office ? 3 : 2;
                if (deferredBuildings == null)
                    Building(parent, "Building_" + slot.ToString("D2"), x, z, buildingFloors, block, block && floors >= 4,
                        office, seed, coordinate, slot, assets, ruined);
                else
                {
                    int buildingSlot = slot;
                    bool buildingBlock = block, buildingWide = block && floors >= 4, buildingOffice = office;
                    float buildingX = x, buildingZ = z;
                    deferredBuildings.Add(() => Building(parent, "Building_" + buildingSlot.ToString("D2"),
                        buildingX, buildingZ, buildingFloors, buildingBlock, buildingWide, buildingOffice,
                        seed, coordinate, buildingSlot, assets, ruined));
                }
            }
            if (CapturePerformanceTimings) LastTownBuildingMs += TimingElapsedMs(timingStart);
            for (int slot = 0; slot < 3; slot++)
            {
                var random = SlotRandom(coordinate, seed, 100 + slot);
                float x = originX + 35;
                float z = originZ + 23 + slot * 40;
                Car(parent, "Car_" + slot, x, z, seed, assets, random, ruined, "car", 0f);
            }
            if (ruined)
            {
                var random = SlotRandom(coordinate, seed, 200);
                for (int i = 0; i < 8; i++)
                    Rubble(parent, originX + Range(random, 6, 122), originZ + Range(random, 6, 122), seed, assets, random);
                for (int i = 0; i < 3; i++)
                    ScrapPile(parent, "Node_S" + i, originX + 44 + i * 40, originZ + Range(random, 30, 100), seed, assets, random);
            }
        }

        /// <summary>The edge of town: family houses on quiet streets, with hedges, cars, street lamps and poles.</summary>
        static void Suburb(Transform parent, float originX, float originZ, int seed,
            ProceduralWorldAssets assets, Vector2Int coordinate, bool ruined, List<System.Action> deferredBuildings = null)
        {
            float streetZ = originZ + 64f;
            Street(parent, originX + 64, streetZ, false, seed, assets);
            var random = SlotRandom(coordinate, seed, 300);
            for (int i = 0; i < 3; i++)
            {
                float x = originX + 24 + i * 40;
                if (Clear(x, streetZ + 6f, 2f, seed) && !WorldLayout.IsWater(x, streetZ + 6f, seed))
                    Dressing(parent, assets.streetLamp, "StreetLamp", x, streetZ + 6f, seed, assets, 180f, ruined && i == 1 ? 14f : 0f);
                if (Clear(x + 20f, streetZ - 6f, 2f, seed) && !WorldLayout.IsWater(x + 20f, streetZ - 6f, seed))
                    Dressing(parent, assets.powerPole, "PowerPole", x + 20f, streetZ - 6f, seed, assets, 0f, ruined && i == 0 ? 11f : 0f);
            }
            for (int slot = 0; slot < 6; slot++)
            {
                var lot = SlotRandom(coordinate, seed, slot);
                bool occupied = lot.NextDouble() < .8;
                bool north = slot >= 3;
                float x = originX + 24 + (slot % 3) * 40;
                float z = streetZ + (north ? 27f : -27f);
                if (!occupied || !Clear(x, z, 11, seed) || !LevelLot(x, z, seed)) continue;
                if (deferredBuildings == null)
                    House(parent, "House_" + slot.ToString("D2"), x, z, north ? 0f : 180f, seed, coordinate, slot, assets, lot, ruined);
                else
                {
                    int houseSlot = slot;
                    float houseX = x, houseZ = z, houseYaw = north ? 0f : 180f;
                    System.Random houseRandom = lot;
                    deferredBuildings.Add(() => House(parent, "House_" + houseSlot.ToString("D2"),
                        houseX, houseZ, houseYaw, seed, coordinate, houseSlot, assets, houseRandom, ruined));
                }
            }
            // Trees between the lots. Solid, but only harvestable once wood is worth fighting over.
            for (int i = 0; i < 6; i++)
            {
                var tree = SlotRandom(coordinate, seed, 400 + i);
                float x = originX + Range(tree, 6, 122);
                float z = originZ + (i % 2 == 0 ? Range(tree, 2, 12) : Range(tree, 116, 126));
                float scale = Range(tree, .9f, 1.4f);
                float yaw = Range(tree, 0, 360);
                GameObject model = Pick(assets.trees, tree);
                if (model == null || !Clear(x, z, 3, seed) || WorldLayout.IsWater(x, z, seed, 3f)) continue;
                var root = new GameObject("Node_T" + i);
                root.SetActive(false);
                root.transform.SetParent(parent, false);
                root.transform.position = new Vector3(x, WorldLayout.HeightAt(x, z, seed), z);
                root.transform.rotation = Quaternion.Euler(0, yaw, 0);
                Visual(model, root.transform, Vector3.one * scale, assets.atlasMaterial);
                var collider = root.AddComponent<CapsuleCollider>();
                collider.radius = .24f * scale;
                collider.height = 2.8f * scale;
                collider.center = Vector3.up * collider.height * .5f;
                if (ruined && assets.wood != null)
                {
                    var node = root.AddComponent<ResourceNode>();
                    node.resourceType = assets.wood;
                    node.remainingCharges = 5;
                    node.quantityPerHarvest = 2;
                    node.regrowSeconds = 360;
                }
                root.SetActive(true);
            }
            if (ruined)
            {
                for (int i = 0; i < 4; i++)
                    Rubble(parent, originX + Range(random, 6, 122), originZ + Range(random, 6, 122), seed, assets, random);
                for (int i = 0; i < 2; i++)
                    ScrapPile(parent, "Node_S" + i, originX + 44 + i * 40, streetZ + (i == 0 ? 8f : -8f), seed, assets, random);
                if (random.NextDouble() < .3) Smoke(parent, new Vector3(originX + 64, WorldLayout.HeightAt(originX + 64, originZ + 90, seed) + 1f, originZ + 90));
            }
        }

        /// <summary>Two real storeys on the original 10 x 12 m lot. Front faces local -Z.</summary>
        static void House(Transform parent, string name, float x, float z, float yaw, int seed,
            Vector2Int coordinate, int lot, ProceduralWorldAssets assets, System.Random random, bool ruined)
        {
            var house = new GameObject(name);
            house.transform.SetParent(parent, false);
            house.transform.SetPositionAndRotation(new Vector3(x, WorldLayout.HeightAt(x, z, seed), z), Quaternion.Euler(0, yaw, 0));
            Transform t = house.transform;
            Transform structure = Group(t, "Static");
            int paint = random.Next(1000);
            Material wall = paint % 3 == 0 || assets.plasterMaterial == null ? assets.structureMaterial : assets.plasterMaterial;
            if (!ruined && paint % 3 != 0 && assets.houseCleanMaterials != null && assets.houseCleanMaterials.Length > 0)
                wall = assets.houseCleanMaterials[paint % assets.houseCleanMaterials.Length];
            int roofIndex = random.Next(2);
            // Always draw, including in the normal pass: sheds, parking and car models must not jump.
            bool burnRoll = random.NextDouble() < .35;
            bool burnedOut = ruined && burnRoll;
            for (int floor = 0; floor < 2; floor++)
            {
                float y = .1f + floor * 3f;
                Shell(structure, y, 10f, 12f, wall, floor == 0 ? 1.8f : 0f, "bld_door_house_front");
                if (floor == 0) Slab(structure, "Floor", y, -5f, 5f, -6f, 6f, wall);
                else SlabWithVoid(structure, y, 10f, 12f, .8f, 2.2f, -2.2f, 2f, wall);
            }
            if (!burnedOut) Slab(structure, "Ceiling", 6.1f, -5f, 5f, -6f, 6f, wall);
            HouseRooms(structure, wall);
            // A 4.2 m run keeps the 3 m rise below 36 degrees. The upper slab is open above it.
            Stair(structure, "HouseStairs", 1.5f, -2.2f, .1f, 1.4f, 4.2f, 3f, false, wall, assets, true);
            Rail(structure, "VoidWest", new Vector3(.75f, 3.6f, -.1f), new Vector3(.1f, 1f, 4.2f), wall);
            Rail(structure, "VoidFoot", new Vector3(1.5f, 3.6f, -2.25f), new Vector3(1.4f, 1f, .1f), wall);

            Transform ground = Interior(t, 0);
            Transform upper = Interior(t, 1);
            Furniture(ground, "tv_unit", new Vector3(-4.375f, .1f, -3.8f), -90f, new Vector3(1.5f, .6f, .65f), 0, lot, coordinate, seed, assets, ruined);
            Furniture(ground, "bookcase", new Vector3(-4.375f, .1f, .2f), -90f, new Vector3(1.1f, 1.9f, .65f), 1, lot, coordinate, seed, assets, ruined);
            Furniture(ground, "kitchen_run", new Vector3(-2.8f, .1f, 5.375f), 0f, new Vector3(2.4f, 2.2f, .65f), 2, lot, coordinate, seed, assets, ruined);
            Furniture(ground, "fridge", new Vector3(.4f, .1f, 5.375f), 0f, new Vector3(.75f, 1.9f, .65f), 3, lot, coordinate, seed, assets, ruined);
            Furniture(ground, "pantry_shelf", new Vector3(4.375f, .1f, 1f), 90f, new Vector3(1.3f, 1.9f, .65f), 4, lot, coordinate, seed, assets, ruined);
            Furniture(ground, "toolbox", new Vector3(4.375f, .1f, -2.5f), 90f, new Vector3(.9f, .9f, .65f), 5, lot, coordinate, seed, assets, ruined);
            Decor(ground, "dining_set", new Vector3(-3.1f, .1f, .9f), 0f, new Vector3(1.6f, .95f, 1.6f), assets);
            Furniture(upper, "wardrobe", new Vector3(-3.1f, 3.1f, -5.375f), 180f, new Vector3(1.4f, 2.1f, .65f), 6, lot, coordinate, seed, assets, ruined);
            Furniture(upper, "nightstand", new Vector3(-4.375f, 3.1f, -.5f), -90f, new Vector3(.6f, .55f, .65f), 7, lot, coordinate, seed, assets, ruined);
            Furniture(upper, "dresser", new Vector3(-3.1f, 3.1f, 5.375f), 0f, new Vector3(1.3f, .95f, .65f), 8, lot, coordinate, seed, assets, ruined);
            Furniture(upper, "medicine_cabinet", new Vector3(4.575f, 4.2f, -2.1f), 90f, new Vector3(.7f, .7f, .25f), 9, lot, coordinate, seed, assets, ruined);
            Furniture(upper, "desk", new Vector3(4.375f, 3.1f, .5f), 90f, new Vector3(1.4f, .8f, .65f), 10, lot, coordinate, seed, assets, ruined);
            Furniture(upper, "linen_closet", new Vector3(.8f, 3.1f, 5.375f), 0f, new Vector3(1.3f, 1.9f, .65f), 11, lot, coordinate, seed, assets, ruined);
            Decor(ground, "sofa", new Vector3(-2.9f, .1f, -1.5f), 0f, new Vector3(2f, .9f, .8f), assets);
            Decor(upper, "bed_double", new Vector3(-3.7f, 3.1f, -3f), -90f, new Vector3(1.4f, .65f, 2f), assets);
            Decor(upper, "bed_single", new Vector3(-3.7f, 3.1f, 3f), -90f, new Vector3(1f, .65f, 2f), assets);

            GameObject roof = assets.houseRoofs != null && assets.houseRoofs.Length > 0 ? assets.houseRoofs[roofIndex % assets.houseRoofs.Length] : null;
            if (!burnedOut)
            {
                if (roof != null) Place(t, roof, "Roof", new Vector3(0, 6.1f, 0), 0f, assets);
                else Box(structure, "Roof", new Vector3(0, 6.3f, 0), new Vector3(11, .4f, 13), assets.roadMaterial, false);
            }
            else
            {
                foreach (float beamX in new[] { -3f, 0f, 3f })
                    Box(structure, "CharredBeam", new Vector3(beamX, 6.1f, 0), new Vector3(.15f, .2f, 11.5f), assets.roadMaterial, false);
            }
            GameObject window = ruined ? assets.houseWindowBroken : assets.houseWindow;
            if (window != null)
                for (int floor = 0; floor < 2; floor++)
                {
                    foreach (float wx in new[] { -2.9f, 2.9f })
                    {
                        Place(t, window, "Window", new Vector3(wx, 1f + floor * 3f, -6.02f), 0f, assets);
                        Place(t, window, "Window", new Vector3(wx, 1f + floor * 3f, 6.02f), 180f, assets);
                    }
                    foreach (int side in new[] { -1, 1 })
                        Place(t, window, "Window", new Vector3(side * 5.02f, 1f + floor * 3f, 1f), side * -90f, assets);
                }
            for (int side = -1; side <= 1; side += 2)
                for (int section = 0; section < 2; section++)
                    if (assets.hedge != null) Place(t, assets.hedge, "Hedge", new Vector3(side * (2.2f + section * 2f), 0f, -11f), 0f, assets);
            if (assets.mailbox != null) Place(t, assets.mailbox, "Mailbox", new Vector3(1.3f, 0f, -11.2f), 0f, assets);
            if (assets.gardenShed != null && random.NextDouble() < .4)
                Place(t, assets.gardenShed, "Shed", new Vector3(3.5f, 0f, 9f), 0f, assets, true);
            Vector3 parking = t.TransformPoint(new Vector3(-7f, 0f, -6f));
            if (Clear(parking.x, parking.z, 3f, seed) && random.NextDouble() < .7)
                Car(t, "Car", parking.x, parking.z, seed, assets, random, ruined, "car", yaw + (ruined ? 25f : 0f));
            FinishStructure(structure);
            house.gameObject.AddComponent<BuildingInterior>().Configure(10f, 12f, 2);
        }

        static void HouseRooms(Transform t, Material wall)
        {
            const float gf = .1f, uf = 3.1f;
            WallX(t, "LivingWall", -1f, -5.7f, 2.2f, gf, wall, -3.7f);
            WallZ(t, "KitchenWall", 2.3f, -4.7f, 2.2f, gf, wall, -.1f);
            WallX(t, "PantryWall", 2.3f, -1f, 5.7f, gf, wall, 3.9f);
            WallX(t, "UtilityWall", 2.3f, -5.7f, -1f, gf, wall, -4.7f);
            WallZ(t, "UtilityBack", -1f, 2.3f, 4.7f, gf, wall);
            WallX(t, "MasterWall", -1f, -5.7f, .5f, uf, wall, -3.1f);
            WallX(t, "BedroomWall", -1f, .5f, 5.7f, uf, wall, 2.9f);
            WallZ(t, "BedroomDivide", .5f, -4.7f, -1f, uf, wall);
            WallX(t, "BathWall", 2.3f, -5.7f, -1f, uf, wall, -4.5f);
            WallX(t, "StudyWall", 2.3f, -1f, 5.7f, uf, wall, 2.9f);
            WallZ(t, "BathBack", -1f, 2.3f, 4.7f, uf, wall);
            WallZ(t, "LinenWall", 3.9f, -.9f, 2.2f, uf, wall, .1f);
        }

        static void Car(Transform parent, string name, float x, float z, int seed, ProceduralWorldAssets assets,
            System.Random random, bool ruined, string table, float yaw)
        {
            GameObject model = Pick(ruined || assets.carsClean == null || assets.carsClean.Length == 0 ? assets.cars : assets.carsClean, random);
            if (model == null || !Clear(x, z, 3, seed)) return;
            var car = new GameObject(name);
            car.transform.SetParent(parent, false);
            car.transform.SetPositionAndRotation(new Vector3(x, WorldLayout.HeightAt(x, z, seed), z), Quaternion.Euler(0, yaw, 0));
            Visual(model, car.transform, Vector3.one, assets.atlasMaterial);
            var collider = car.AddComponent<BoxCollider>();
            collider.size = new Vector3(2, 1.8f, 4.5f);
            collider.center = new Vector3(0, .9f, 0);
            if (ruined) Crate(car.transform, "Trunk", new Vector3(0, .5f, -2.6f), assets, table, random);
        }

        static void Rubble(Transform parent, float x, float z, int seed, ProceduralWorldAssets assets, System.Random random)
        {
            GameObject model = Pick(assets.rubble, random);
            float yaw = Range(random, 0, 360), scale = Range(random, .5f, 1.5f);
            if (model == null || WorldLayout.IsHomePlot(x, z) || WorldLayout.IsWater(x, z, seed, 1f)) return;
            var root = new GameObject("Rubble");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(x, WorldLayout.HeightAt(x, z, seed) - .05f, z), Quaternion.Euler(0, yaw, 0));
            // Scenery only: small enough to walk over, and never something a raider snags on.
            Visual(model, root.transform, new Vector3(scale, scale * .45f, scale), assets.atlasMaterial);
        }

        /// <summary>Wreckage worth picking through: the town's own source of scrap, on the gaps between lots.</summary>
        static void ScrapPile(Transform parent, string name, float x, float z, int seed, ProceduralWorldAssets assets, System.Random random)
        {
            GameObject model = Pick(assets.scrapPiles, random);
            float yaw = Range(random, 0, 360);
            if (assets.scrap == null || !Clear(x, z, 1.5f, seed) || WorldLayout.IsWater(x, z, seed, 1f)) return;
            var root = new GameObject(name);
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(x, WorldLayout.HeightAt(x, z, seed), z), Quaternion.Euler(0, yaw, 0));
            if (model != null) Visual(model, root.transform, Vector3.one, assets.atlasMaterial);
            else Box(root.transform, "ResourceVisual", new Vector3(0, .45f, 0), new Vector3(1f, .9f, 1f), assets.roadMaterial, false);
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.3f, 1.15f, 1.3f);
            collider.center = Vector3.up * .57f;
            var node = root.AddComponent<ResourceNode>();
            node.resourceType = assets.scrap;
            node.remainingCharges = 4;
            node.quantityPerHarvest = 1;
            node.regrowSeconds = 900;
            root.SetActive(true);
        }

        /// <summary>Roadside props. A knocked-over one tilts by leanDegrees, and none block movement.</summary>
        static void Dressing(Transform parent, GameObject model, string name, float x, float z, int seed,
            ProceduralWorldAssets assets, float yaw, float leanDegrees)
        {
            if (model == null) return;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(x, WorldLayout.HeightAt(x, z, seed), z), Quaternion.Euler(leanDegrees, yaw, 0));
            Visual(model, root.transform, Vector3.one, assets.atlasMaterial);
        }

        static void Place(Transform parent, GameObject model, string name, Vector3 localPosition, float yaw,
            ProceduralWorldAssets assets, bool solid = false)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            Visual(model, root.transform, Vector3.one, assets.atlasMaterial);
            if (solid)
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = Vector3.up * 1.2f;
                box.size = new Vector3(3f, 2.4f, 2.5f);
            }
        }

        static void Smoke(Transform parent, Vector3 position) => WorldEffects.SmokePlume(parent, position);

        static bool LevelLot(float x, float z, int seed)
        {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int ix = -1; ix <= 1; ix++)
                for (int iz = -1; iz <= 1; iz++)
                {
                    float height = WorldLayout.HeightAt(x + ix * 6, z + iz * 6, seed);
                    min = Mathf.Min(min, height);
                    max = Mathf.Max(max, height);
                }
            return max - min < .1f;
        }

        static void Street(Transform parent, float x, float z, bool northSouth, int seed, ProceduralWorldAssets assets,
            bool guaranteedTownLevel = false)
        {
            // Town terrain is flattened by WorldLayout. Surface strips have no gameplay collider.
            for (int part = 0; part < 8; part++)
            {
                float px = northSouth ? x : x - 56 + part * 16;
                float pz = northSouth ? z - 56 + part * 16 : z;
                if (WorldLayout.IsRoad(px, pz) || WorldLayout.IsConnectorRoad(px, pz, seed) || WorldLayout.IsHomePlot(px, pz) ||
                    WorldLayout.IsHomePlot(px - 8, pz - 8) ||
                    WorldLayout.IsHomePlot(px + 8, pz + 8)) continue;
                // Inner town chunks (including this strip's 8 m samples) are fully inside
                // the flat pad. Suburb streets still test their slope before placing a strip.
                if (!guaranteedTownLevel &&
                    (Mathf.Abs(WorldLayout.HeightAt(px - 8, pz - 8, seed) - WorldLayout.HeightAt(px + 8, pz + 8, seed)) > .05f ||
                     Mathf.Abs(WorldLayout.HeightAt(px - 8, pz + 8, seed) - WorldLayout.HeightAt(px + 8, pz - 8, seed)) > .05f ||
                     Mathf.Abs(WorldLayout.HeightAt(px, pz, seed) - WorldLayout.HeightAt(px + 8, pz + 8, seed)) > .05f)) continue;
                var surface = Box(parent, "Street", Vector3.zero,
                    northSouth ? new Vector3(7, .04f, 16) : new Vector3(16, .04f, 7), assets.roadMaterial, false);
                surface.transform.position = new Vector3(px, WorldLayout.HeightAt(px, pz, seed) + .025f, pz);
            }
        }

        /// <summary>Town shells contain real floors; only the independent type stream selects their plan.</summary>
        static void Building(Transform parent, string name, float x, float z, int floors, bool block,
            bool wide, bool office, int seed, Vector2Int coordinate, int lot, ProceduralWorldAssets assets, bool ruined)
        {
            var building = new GameObject(name);
            building.transform.SetParent(parent, false);
            building.transform.position = new Vector3(x, WorldLayout.HeightAt(x, z, seed), z);
            Transform t = building.transform;
            Transform structure = Group(t, "Static");
            if (!block && !office)
            {
                ShopHouse(t, structure, lot, coordinate, seed, assets, ruined);
                FinishStructure(structure);
                building.gameObject.AddComponent<BuildingInterior>().Configure(12f, 12f, 2);
                return;
            }
            float width = office ? 16f : wide ? 20f : 14f;
            float depth = office ? 12f : 14f;
            float back = depth * .5f - .15f;
            float foot = office ? 1.3f : 2.5f;
            float corridorBack = foot - 1.6f;
            Material wall = assets.structureMaterial;
            for (int floor = 0; floor < floors; floor++)
            {
                float y = .1f + floor * 3f;
                Shell(structure, y, width, depth, wall, floor == 0 ? 2.8f : 0f,
                    office ? "bld_door_office" : "bld_door_apt_entrance");
                if (floor == 0) Slab(structure, "Floor", y, -width / 2f, width / 2f, -depth / 2f, depth / 2f, wall);
                else SlabWithVoid(structure, y, width, depth, -1.6f, 1.6f, foot, back, wall);
                StairCore(structure, y, foot, back, wall, assets);
                Transform interior = Interior(t, floor);
                if (office) OfficeFloor(structure, interior, y, floor, lot, coordinate, seed, assets, ruined);
                else if (floor == 0) BlockGround(structure, interior, y, width, lot, coordinate, seed, assets, ruined);
                else BlockFlats(structure, interior, y, width, wide, floor, lot, coordinate, seed, assets, ruined);
                FacadeWindows(structure, y, width, depth, floor == 0, wall, assets);
            }
            float roofY = .1f + floors * 3f;
            SlabWithVoid(structure, roofY, width, depth, -1.6f, 1.6f, foot, back, wall);
            Roof(structure, roofY, width, depth, foot, back, wall);
            Transform roof = Interior(t, floors);
            if (SlotRandom(coordinate, seed, 20000 + lot * 128 + 99).NextDouble() < .6)
                Furniture(roof, "roof", new Vector3(width / 2f - 1.1f, roofY, -depth / 2f + .7f), 180f,
                    new Vector3(1f, .6f, .65f), 99, lot, coordinate, seed, assets, ruined);
            Decor(roof, "roof_hvac", new Vector3(-width / 2f + 1.5f, roofY, back - 1f), 0f, new Vector3(2f, 1.1f, 1.5f), assets);
            Decor(roof, "roof_tank", new Vector3(width / 2f - 2.2f, roofY, back - 1.8f), 0f, new Vector3(2f, 3f, 2f), assets);
            FinishStructure(structure);
            building.gameObject.AddComponent<BuildingInterior>().Configure(width, depth, floors + 1);
        }

        static void StairCore(Transform t, float y, float foot, float back, Material wall, ProceduralWorldAssets assets)
        {
            Stair(t, "FlightA", -.85f, foot, y, 1.5f, 2.4f, 1.5f, false, wall, assets);
            Stair(t, "FlightB", .85f, foot + 2.4f, y + 1.5f, 1.5f, 2.4f, 1.5f, true, wall, assets);
            Slab(t, "MidLanding", y + 1.5f, -1.6f, 1.6f, foot + 2.4f, back, wall);
            // Full-height divider ends at the mid-landing so the 1.95 m turn remains clear.
            WallX(t, "CoreDivider", 0f, foot, foot + 2.4f, y, wall);
            WallX(t, "CoreWest", -1.7f, foot - 1.6f, back, y, wall);
            WallX(t, "CoreEast", 1.7f, foot - 1.6f, back, y, wall);
        }

        static void BlockGround(Transform t, Transform interior, float y, float width, int lot,
            Vector2Int coordinate, int seed, ProceduralWorldAssets assets, bool ruined)
        {
            Material wall = assets.structureMaterial;
            float edge = width * .5f - .15f;
            WallX(t, "LobbyWest", -1.7f, -6.85f, -.9f, y, wall, -3.3f);
            WallX(t, "LobbyEast", 1.7f, -6.85f, -.9f, y, wall, -3.3f);
            foreach (int side in new[] { -1, 1 })
            {
                float lo = side < 0 ? -edge : 1.8f;
                float hi = side < 0 ? -1.8f : edge;
                WallZ(t, "ServiceFront", -.9f, lo, hi, y, wall, side * 3.2f);
                WallZ(t, "ServiceBack", 1f, lo, hi, y, wall, side * 3.2f);
            }
            Furniture(interior, "shop_shelf", new Vector3(-edge + .475f, y, -5.4f), -90f, new Vector3(1.8f, 1.9f, .65f), 0, lot, coordinate, seed, assets, ruined, true);
            Furniture(interior, "shop_shelf", new Vector3(-edge + .475f, y, -1.9f), -90f, new Vector3(1.3f, 1.9f, .65f), 1, lot, coordinate, seed, assets, ruined, true);
            Furniture(interior, "counter", new Vector3(-3.3f, y, -6.375f), 180f, new Vector3(1.8f, 1f, .65f), 2, lot, coordinate, seed, assets, ruined, true);
            Furniture(interior, "toolbox", new Vector3(edge - .475f, y, -5.2f), 90f, new Vector3(1.1f, .9f, .65f), 3, lot, coordinate, seed, assets, ruined, true);
            Furniture(interior, "lockers", new Vector3(edge - .475f, y, -2f), 90f, new Vector3(1.4f, 1.9f, .65f), 4, lot, coordinate, seed, assets, ruined, true);
            Furniture(interior, "mail_lockers", new Vector3(-edge + .475f, y, 2.8f), -90f, new Vector3(1.8f, 1.7f, .65f), 5, lot, coordinate, seed, assets, ruined, true);
            Furniture(interior, "desk", new Vector3(-3.5f, y, 6.375f), 0f, new Vector3(1.4f, .8f, .65f), 6, lot, coordinate, seed, assets, ruined, true);
            Furniture(interior, "lockers", new Vector3(edge - .475f, y, 4.4f), 90f, new Vector3(1.6f, 1.9f, .65f), 7, lot, coordinate, seed, assets, ruined, true);
        }

        static void BlockFlats(Transform t, Transform interior, float y, float width, bool wide, int floor,
            int lot, Vector2Int coordinate, int seed, ProceduralWorldAssets assets, bool ruined)
        {
            Material wall = assets.structureMaterial;
            float edge = width / 2f - .3f;
            if (wide) WallX(t, "FrontFlatDivide", 0f, -6.85f, -1f, y, wall);
            int count = wide ? 4 : 3;
            for (int flat = 0; flat < count; flat++)
            {
                bool north = wide ? flat >= 2 : flat >= 1;
                int side = wide ? (flat % 2 == 0 ? -1 : 1) : flat == 2 ? 1 : -1;
                float minX = side < 0 ? -edge : north ? 1.8f : .1f;
                float maxX = side < 0 ? north ? -1.8f : -.1f : edge;
                if (!wide && !north) maxX = edge;
                float minZ = north ? 1.1f : -6.7f;
                float maxZ = north ? 6.7f : -1.1f;
                float doorX = !wide && !north ? 1.8f : side * 3.2f;
                float doorZ = north ? 1f : -1f;
                WallZ(t, "FlatEntrance", doorZ, minX, maxX, y, wall, doorX);
                int firstSlot = 8 + (floor - 1) * 16 + flat * 4;
                bool sealedRoll = SlotRandom(coordinate, seed, 20000 + lot * 128 + firstSlot).NextDouble() < .4;
                if (ruined && sealedRoll)
                {
                    GameObject sealedDoor = Box(t, "SealedFlat_" + floor + "_" + flat,
                        new Vector3(doorX, y + 1.1f, doorZ), new Vector3(1.8f, 2.2f, .3f), assets.roadMaterial);
                    var health = sealedDoor.AddComponent<StructureHealth>();
                    health.maxHealth = 180f;
                    health.material = HomesteadMaterial.Stone;
                    continue;
                }
                bool studio = !wide && north;
                Flat(t, interior, y, minX, maxX, minZ, maxZ, side, north, studio, firstSlot,
                    lot, coordinate, seed, assets, ruined);
            }
        }

        static void Flat(Transform t, Transform interior, float y, float minX, float maxX, float minZ, float maxZ,
            int side, bool north, bool studio, int firstSlot, int lot, Vector2Int coordinate, int seed,
            ProceduralWorldAssets assets, bool ruined)
        {
            Material wall = assets.structureMaterial;
            float outer = side < 0 ? minX : maxX;
            float roomWidth = studio ? 2.2f : 4.6f;
            float divide = outer - side * roomWidth;
            float bathZ = north ? minZ + 2.2f : maxZ - 2.2f;
            float bathDoor = north ? minZ + 1.05f : maxZ - 1.05f;
            float bedDoor = north ? maxZ - 1.7f : minZ + 1.7f;
            float partMin = Mathf.Min(outer, divide), partMax = Mathf.Max(outer, divide);
            WallZ(t, "BathDivide", bathZ, partMin, partMax, y, wall);
            if (north)
            {
                WallX(t, "BathDoor", divide, minZ, bathZ, y, wall, bathDoor);
                if (!studio) WallX(t, "BedDoor", divide, bathZ, maxZ, y, wall, bedDoor);
            }
            else
            {
                WallX(t, "BathDoor", divide, bathZ, maxZ, y, wall, bathDoor);
                WallX(t, "BedDoor", divide, minZ, bathZ, y, wall, bedDoor);
            }
            float backZ = north ? maxZ - .325f : minZ + .325f;
            float face = north ? 0f : 180f;
            float livingX = side < 0 ? (divide + maxX) * .5f : (minX + divide) * .5f;
            if (studio) livingX = outer - side * 1.4f;
            Furniture(interior, "kitchen_run", new Vector3(livingX, y, backZ), face, new Vector3(2.4f, 2.2f, .65f), firstSlot, lot, coordinate, seed, assets, ruined, true);
            Furniture(interior, "wardrobe", new Vector3(outer - side * (studio ? 3.8f : 2.9f), y, backZ), face,
                new Vector3(1.3f, 2f, .65f), firstSlot + 1, lot, coordinate, seed, assets, ruined, true);
            Furniture(interior, "medicine_cabinet", new Vector3(outer - side * .125f, y + 1.1f, bathDoor), side * 90f,
                new Vector3(.7f, .7f, .25f), firstSlot + 2, lot, coordinate, seed, assets, ruined, true);
            if (!studio)
            {
                float inside = side < 0 ? maxX : minX;
                Furniture(interior, "fridge", new Vector3(inside + side * .325f, y, north ? maxZ - 2f : minZ + 2f), -side * 90f,
                    new Vector3(.75f, 1.9f, .65f), firstSlot + 3, lot, coordinate, seed, assets, ruined, true);
                Decor(interior, "bed_double", new Vector3(outer - side * 1.05f, y, north ? maxZ - 1.8f : minZ + 1.8f), side * 90f,
                    new Vector3(1.4f, .65f, 2f), assets);
            }
        }

        static void OfficeFloor(Transform t, Transform interior, float y, int floor, int lot,
            Vector2Int coordinate, int seed, ProceduralWorldAssets assets, bool ruined)
        {
            Material wall = assets.structureMaterial;
            foreach (int side in new[] { -1, 1 })
            {
                WallZ(t, "OfficeFront", -.3f, side < 0 ? -7.85f : 1.8f, side < 0 ? -1.8f : 7.85f, y, wall, side * 3.3f);
                Furniture(interior, "desk", new Vector3(side * 7.375f, y, 1.8f), side * 90f, new Vector3(1.5f, .8f, .65f), floor * 6 + (side < 0 ? 0 : 3), lot, coordinate, seed, assets, ruined, true);
                Furniture(interior, "desk", new Vector3(side * 7.375f, y, 4.6f), side * 90f, new Vector3(1.5f, .8f, .65f), floor * 6 + (side < 0 ? 1 : 4), lot, coordinate, seed, assets, ruined, true);
                Furniture(interior, "filing_cabinet", new Vector3(side * 4.5f, y, 5.375f), 0f, new Vector3(1f, 1.5f, .65f), floor * 6 + (side < 0 ? 2 : 5), lot, coordinate, seed, assets, ruined, true);
            }
            if (floor == 2) Furniture(interior, "safe", new Vector3(7.375f, y, -4.5f), 90f, new Vector3(.7f, 1f, .65f), 18, lot, coordinate, seed, assets, ruined, true);
        }

        static void ShopHouse(Transform root, Transform t, int lot, Vector2Int coordinate, int seed,
            ProceduralWorldAssets assets, bool ruined)
        {
            Material wall = assets.structureMaterial;
            for (int floor = 0; floor < 2; floor++)
            {
                float y = .1f + floor * 3f;
                Shell(t, y, 12f, 12f, wall, floor == 0 ? 2.8f : 0f, "bld_door_shop_glass");
                if (floor == 0) Slab(t, "Floor", y, -6f, 6f, -6f, 6f, wall);
                else SlabWithVoid(t, y, 12f, 12f, -5.5f, -4.1f, -.3f, 3.9f, wall);
                FacadeWindows(t, y, 12f, 12f, floor == 0, wall, assets);
            }
            Stair(t, "ShopStairs", -4.8f, -.3f, .1f, 1.4f, 4.2f, 3f, false, wall, assets, true);
            Rail(t, "ShopVoidEast", new Vector3(-4.05f, 3.6f, 1.8f), new Vector3(.1f, 1f, 4.2f), wall);
            Rail(t, "ShopVoidFoot", new Vector3(-4.8f, 3.6f, -.35f), new Vector3(1.4f, 1f, .1f), wall);
            WallZ(t, "Backroom", 1.9f, -3.9f, 5.7f, .1f, wall, 0f);
            WallX(t, "Bedroom", 1.3f, -5.7f, 3.4f, 3.1f, wall, -2.1f);
            WallX(t, "Bathroom", 1.3f, 3.4f, 5.7f, 3.1f, wall, 4.5f);
            WallZ(t, "BathDivide", 3.4f, 1.3f, 5.7f, 3.1f, wall);
            Transform ground = Interior(root, 0), upper = Interior(root, 1);
            for (int shelf = 0; shelf < 3; shelf++)
                Furniture(ground, "shop_shelf", new Vector3(5.375f, .1f, -4.8f + shelf * 2.1f), 90f, new Vector3(1.3f, 1.8f, .65f), shelf, lot, coordinate, seed, assets, ruined, true);
            Furniture(ground, "counter", new Vector3(-3f, .1f, -5.375f), 180f, new Vector3(1.8f, 1f, .65f), 3, lot, coordinate, seed, assets, ruined, true);
            Furniture(ground, "toolbox", new Vector3(5.375f, .1f, 4.2f), 90f, new Vector3(1f, .9f, .65f), 4, lot, coordinate, seed, assets, ruined, true);
            Furniture(ground, "pantry_shelf", new Vector3(1.8f, .1f, 5.375f), 0f, new Vector3(1.3f, 1.9f, .65f), 5, lot, coordinate, seed, assets, ruined, true);
            Furniture(upper, "kitchen_run", new Vector3(-1.4f, 3.1f, 5.375f), 0f, new Vector3(2.4f, 2.2f, .65f), 6, lot, coordinate, seed, assets, ruined, true);
            Furniture(upper, "fridge", new Vector3(-2.8f, 3.1f, -5.375f), 180f, new Vector3(.75f, 1.9f, .65f), 7, lot, coordinate, seed, assets, ruined, true);
            Furniture(upper, "wardrobe", new Vector3(3.4f, 3.1f, -5.375f), 180f, new Vector3(1.4f, 2.1f, .65f), 8, lot, coordinate, seed, assets, ruined, true);
            Furniture(upper, "medicine_cabinet", new Vector3(5.575f, 4.2f, 4.5f), 90f, new Vector3(.7f, .7f, .25f), 9, lot, coordinate, seed, assets, ruined, true);
            Decor(upper, "bed_double", new Vector3(4.6f, 3.1f, .1f), 90f, new Vector3(1.4f, .65f, 2f), assets);
            Slab(t, "Roof", 6.1f, -6f, 6f, -6f, 6f, wall);
        }

        static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        static Transform Interior(Transform building, int floor) => Group(Group(building, "Floor_" + floor), "Interior");

        static void Slab(Transform t, string name, float top, float minX, float maxX, float minZ, float maxZ, Material wall)
        {
            if (maxX - minX < .001f || maxZ - minZ < .001f) return;
            Box(t, name, new Vector3((minX + maxX) * .5f, top - .1f, (minZ + maxZ) * .5f),
                new Vector3(maxX - minX, .2f, maxZ - minZ), wall);
        }

        static void SlabWithVoid(Transform t, float top, float width, float depth,
            float minX, float maxX, float minZ, float maxZ, Material wall)
        {
            Slab(t, "SlabWest", top, -width / 2f, minX, -depth / 2f, depth / 2f, wall);
            Slab(t, "SlabEast", top, maxX, width / 2f, -depth / 2f, depth / 2f, wall);
            Slab(t, "SlabFront", top, minX, maxX, -depth / 2f, minZ, wall);
            Slab(t, "SlabBack", top, minX, maxX, maxZ, depth / 2f, wall);
        }

        static void Shell(Transform t, float y, float width, float depth, Material wall, float door, string entranceDoor)
        {
            Box(t, "Back", new Vector3(0, y + 1.4f, depth / 2f - .15f), new Vector3(width, 2.8f, .3f), wall);
            Box(t, "West", new Vector3(-width / 2f + .15f, y + 1.4f, 0), new Vector3(.3f, 2.8f, depth), wall);
            Box(t, "East", new Vector3(width / 2f - .15f, y + 1.4f, 0), new Vector3(.3f, 2.8f, depth), wall);
            WallZ(t, "Front", -depth / 2f + .15f, -width / 2f, width / 2f, y, wall,
                door > 0f ? 0f : float.NaN, door, .3f, entranceDoor);
        }

        // All openings are genuine 1.8 m gaps, with 2.2 m clear height beneath the lintel.
        static void WallX(Transform t, string name, float x, float minZ, float maxZ, float y, Material wall,
            float doorZ = float.NaN, string doorModel = null) => Wall(t, name, true, x, minZ, maxZ, y, wall, doorZ, 1.8f, .2f, doorModel);

        static void WallZ(Transform t, string name, float z, float minX, float maxX, float y, Material wall,
            float doorX = float.NaN, float doorWidth = 1.8f, float thickness = .2f, string doorModel = null)
            => Wall(t, name, false, z, minX, maxX, y, wall, doorX, doorWidth, thickness, doorModel);

        static void Wall(Transform t, string name, bool alongZ, float axis, float min, float max, float y,
            Material wall, float door, float gap, float thickness, string doorModel = null)
        {
            if (float.IsNaN(door)) WallPart(t, name, alongZ, axis, min, max, y, 2.8f, wall, thickness);
            else
            {
                WallPart(t, name, alongZ, axis, min, door - gap / 2f, y, 2.8f, wall, thickness);
                WallPart(t, name, alongZ, axis, door + gap / 2f, max, y, 2.8f, wall, thickness);
                WallPart(t, name + "Lintel", alongZ, axis, door - gap / 2f, door + gap / 2f, y + 2.2f, .6f, wall, thickness);
                string model = doorModel ?? (name.IndexOf("Office", StringComparison.OrdinalIgnoreCase) >= 0 ? "bld_door_office" :
                    name.IndexOf("Service", StringComparison.OrdinalIgnoreCase) >= 0 ? "bld_door_shop_glass" : BuildingDoors.InteriorModel(name));
                BuildingDoors.Place(t, name, alongZ, axis, door, y, gap, thickness, model, 0, BuildingRuinedPass);
            }
        }

        static void WallPart(Transform t, string name, bool alongZ, float axis, float min, float max, float y,
            float height, Material wall, float thickness)
        {
            if (max - min < .001f) return;
            Box(t, name, alongZ ? new Vector3(axis, y + height / 2f, (min + max) / 2f) : new Vector3((min + max) / 2f, y + height / 2f, axis),
                alongZ ? new Vector3(thickness, height, max - min) : new Vector3(max - min, height, thickness), wall);
        }

        static void Rail(Transform t, string name, Vector3 position, Vector3 size, Material wall)
            => Box(t, name, position, size, wall);

        static void Roof(Transform t, float y, float width, float depth, float foot, float back, Material wall)
        {
            Rail(t, "ParapetFront", new Vector3(0, y + .5f, -depth / 2f + .1f), new Vector3(width, 1f, .2f), wall);
            Rail(t, "ParapetBack", new Vector3(0, y + .5f, depth / 2f - .1f), new Vector3(width, 1f, .2f), wall);
            Rail(t, "ParapetWest", new Vector3(-width / 2f + .1f, y + .5f, 0), new Vector3(.2f, 1f, depth), wall);
            Rail(t, "ParapetEast", new Vector3(width / 2f - .1f, y + .5f, 0), new Vector3(.2f, 1f, depth), wall);
            WallX(t, "HutWest", -1.7f, foot - 1.8f, back, y, wall);
            WallX(t, "HutEast", 1.7f, foot - 1.8f, back, y, wall);
            WallZ(t, "HutFront", foot - 1.8f, -1.8f, 1.8f, y, wall, 0f);
            WallZ(t, "HutBack", back, -1.8f, 1.8f, y, wall);
            Slab(t, "HutRoof", y + 3f, -1.8f, 1.8f, foot - 1.8f, back + .15f, wall);
        }

        static void FacadeWindows(Transform t, float y, float width, float depth, bool ground,
            Material wall, ProceduralWorldAssets assets)
        {
            int count = Mathf.FloorToInt(width / 3f);
            for (int index = 0; index < count; index++)
            {
                float x = (index - (count - 1) * .5f) * 3f;
                if (ground && Mathf.Abs(x) < 2f) continue;
                foreach (int side in new[] { -1, 1 })
                    Box(t, "Window", new Vector3(x, y + 1.65f, side * (depth / 2f + .025f)), new Vector3(1.5f, 1.3f, .06f), assets.roadMaterial, false);
            }
        }

        static readonly Dictionary<string, GameObject> furnitureModels = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Mesh> stairWedges = new Dictionary<string, Mesh>();
        static readonly List<Renderer> rendererScratch = new List<Renderer>(16);
        private struct FurnitureModelBounds
        {
            public bool hasBounds;
            public Bounds bounds;
        }
        static readonly Dictionary<GameObject, FurnitureModelBounds> furnitureModelBounds =
            new Dictionary<GameObject, FurnitureModelBounds>();
        static Mesh unitCubeMesh;
        static Material defaultPrimitiveMaterial;

        static GameObject Prop(string id)
        {
            if (!furnitureModels.TryGetValue(id, out var model))
            {
                model = Resources.Load<GameObject>("Props/" + id);
                furnitureModels[id] = model;
            }
            return model;
        }

        static void Stair(Transform parent, string name, float x, float footZ, float bottom, float width,
            float run, float rise, bool reverse, Material material, ProceduralWorldAssets assets, bool house = false)
        {
            Transform root = Group(parent, name);
            root.localPosition = new Vector3(x, bottom, footZ + (reverse ? -1f : 1f) * run / 2f);
            root.localRotation = Quaternion.Euler(0f, reverse ? 180f : 0f, 0f);
            float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            Transform ramp = Group(root, "Ramp");
            ramp.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            ramp.localPosition = Vector3.up * (rise / 2f) - ramp.localRotation * Vector3.up * .06f;
            var slope = ramp.gameObject.AddComponent<BoxCollider>();
            slope.size = new Vector3(width, .12f, Mathf.Sqrt(run * run + rise * rise));
            // Ground flights are filled to the floor, eliminating crawl-under traps. Stacked
            // flights need a sloped underside: a full wedge there would block the flight below.
            // Cache these immutable meshes so chunk unloads cannot leak a Mesh per flight.
            bool groundFlight = bottom < 3f;
            string key = (house ? "house" : "switchback") + (groundFlight ? "_filled" : "_stacked");
            if (!stairWedges.TryGetValue(key, out Mesh mesh))
            {
                float half = width / 2f, halfRun = run / 2f;
                mesh = new Mesh { name = "StairWedge_" + key };
                mesh.vertices = new[] {
                    new Vector3(-half, -.2f, -halfRun), new Vector3(half, -.2f, -halfRun),
                    new Vector3(-half, groundFlight ? -.2f : rise - .2f, halfRun), new Vector3(half, groundFlight ? -.2f : rise - .2f, halfRun),
                    new Vector3(-half, -.1f, -halfRun), new Vector3(half, -.1f, -halfRun),
                    new Vector3(-half, rise - .1f, halfRun), new Vector3(half, rise - .1f, halfRun) };
                mesh.triangles = new[] { 0,2,1, 1,2,3, 4,5,6, 5,7,6, 0,1,4, 1,5,4,
                    2,6,3, 3,6,7, 0,4,2, 2,4,6, 1,3,5, 3,7,5 };
                mesh.RecalculateBounds();
                stairWedges[key] = mesh;
            }
            var wedge = root.gameObject.AddComponent<MeshCollider>();
            wedge.sharedMesh = mesh;
            wedge.convex = true;
            GameObject model = Prop(house ? "bld_stairs_house" : "bld_stairs_flight");
            if (model != null)
                Visual(model, root, new Vector3(width / (house ? 1.4f : 1.5f), rise / (house ? 2.9f : 1.5f), run / (house ? 4f : 2.4f)), assets.atlasMaterial);
            else
                for (int step = 0; step < (house ? 16 : 8); step++)
                {
                    int count = house ? 16 : 8;
                    Box(root, "Tread", new Vector3(0, rise * (step + 1f) / count - .04f, -run / 2f + run * (step + .5f) / count),
                        new Vector3(width, .08f, run / count), material, false);
                }
        }

        static void Furniture(Transform parent, string kind, Vector3 position, float yaw, Vector3 size,
            int slot, int lot, Vector2Int coordinate, int seed, ProceduralWorldAssets assets, bool ruined, bool town = false)
        {
            Transform root = Group(parent, "C" + slot.ToString("D2"));
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            FurnitureVisual(root, kind, size, assets);
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = Vector3.up * (size.y / 2f);
            collider.size = size;
            if (!ruined) return;
            var loot = root.gameObject.AddComponent<LootContainer>();
            var random = SlotRandom(coordinate, seed, 10000 + lot * 128 + slot);
            Vector3 worldPosition = new Vector3(coordinate.x * WorldLayout.ChunkSize + position.x, position.y,
                coordinate.y * WorldLayout.ChunkSize + position.z);
            int ringTier = SheltreSim.Bestiary.EncounterHabitats.DistanceRing(worldPosition) * 2;
            int tier = Mathf.Max(0, Mathf.FloorToInt((position.y - .1f) / 3f)) + (town ? 1 : 0) + ringTier;
            LootTables.FillSlot(loot, kind, random, tier);
        }

        static void Decor(Transform parent, string kind, Vector3 position, float yaw, Vector3 size, ProceduralWorldAssets assets)
        {
            Transform root = Group(parent, "Decor_" + kind);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            FurnitureVisual(root, kind, size, assets);
            // Dressing stays collider-free; the room's fixed wall-side container envelopes define its aisles.
        }

        static void FurnitureVisual(Transform parent, string kind, Vector3 size, ProceduralWorldAssets assets)
        {
            long timingStart = StartTiming();
            string modelId = kind == "pantry_shelf" ? "furn_bookcase" : kind == "linen_closet" || kind == "lockers" ? "furn_wardrobe"
                : kind == "counter" ? "furn_shop_counter" : kind == "roof" ? "furn_toolbox"
                : kind == "roof_hvac" ? "bld_roof_hvac" : kind == "roof_tank" ? "bld_roof_water_tank" : "furn_" + kind;
            GameObject model = Prop(modelId);
            if (model == null)
            {
                Box(parent, kind, Vector3.up * (size.y / 2f), size, assets.roadMaterial != null ? assets.roadMaterial : assets.structureMaterial, false);
                if (CapturePerformanceTimings) LastFurnitureVisualMs += TimingElapsedMs(timingStart);
                return;
            }
            Visual(model, parent, Vector3.one, assets.atlasMaterial);
            Transform visual = parent.GetChild(0);
            // Fit visual geometry into a fixed gameplay envelope. Model bounds can never move a lot,
            // close a doorway, or make normal and ruined colliders differ when an art asset is absent.
            if (!furnitureModelBounds.TryGetValue(model, out FurnitureModelBounds cached))
            {
                cached = new FurnitureModelBounds { hasBounds = false, bounds = new Bounds() };
                rendererScratch.Clear();
                visual.GetComponentsInChildren(true, rendererScratch);
                foreach (Renderer renderer in rendererScratch)
                {
                    Bounds local = renderer.localBounds;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 point = local.center + Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                        // Keep the original parent-space bounds math on the cache miss. The
                        // Visual root has identity local position, rotation and scale, so these
                        // coordinates are also the unscaled bounds in Visual-root local space.
                        point = parent.InverseTransformPoint(renderer.transform.TransformPoint(point));
                        if (!cached.hasBounds) { cached.bounds = new Bounds(point, Vector3.zero); cached.hasBounds = true; }
                        else cached.bounds.Encapsulate(point);
                    }
                }
                rendererScratch.Clear();
                furnitureModelBounds[model] = cached;
            }
            if (!cached.hasBounds) return;
            Bounds bounds = cached.bounds;
            Vector3 scale = new Vector3(size.x / Mathf.Max(.01f, bounds.size.x), size.y / Mathf.Max(.01f, bounds.size.y), size.z / Mathf.Max(.01f, bounds.size.z));
            visual.localScale = scale;
            visual.localPosition = -Vector3.Scale(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), scale);
            if (CapturePerformanceTimings) LastFurnitureVisualMs += TimingElapsedMs(timingStart);
        }

        static void FinishStructure(Transform structure)
        {
            // Animated/searchable furniture lives outside Static. Colliders remain individual.
            long timingStart = StartTiming();
            StaticBatchingUtility.Combine(structure.gameObject);
            if (CapturePerformanceTimings) LastStaticCombineMs += TimingElapsedMs(timingStart);
        }

        static void Crate(Transform parent, string name, Vector3 position,
            ProceduralWorldAssets assets, string table, System.Random random)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            if (assets.lootModel != null) Visual(assets.lootModel, root.transform, Vector3.one * 2, assets.atlasMaterial);
            else Box(root.transform, "Visual", Vector3.up * .4f, Vector3.one * .8f, assets.structureMaterial, false);
            var collider = root.AddComponent<BoxCollider>();
            collider.size = Vector3.one * .8f;
            collider.center = Vector3.up * .4f;
            var loot = root.AddComponent<LootContainer>();
            LootTables.FillAtWorldPosition(loot, table, random, root.transform.position);
            loot.hasLoot = loot.entries.Count > 0;
        }

        static GameObject Pick(GameObject[] models, System.Random random)
            => models != null && models.Length > 0 ? models[random.Next(models.Length)] : null;

        static void Visual(GameObject model, Transform parent, Vector3 scale, Material material)
        {
            var visual = UnityEngine.Object.Instantiate(model, parent, false);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = scale;
            if (material != null)
            {
                rendererScratch.Clear();
                visual.GetComponentsInChildren(true, rendererScratch);
                foreach (var renderer in rendererScratch)
                    if (renderer != null && renderer.sharedMaterial != material) renderer.sharedMaterial = material;
                rendererScratch.Clear();
            }
            // Assets are collider-free FBXs; imported root conversion is baked in their meshes.
        }

        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size,
            Material material, bool solid = true)
        {
            long timingStart = StartTiming();
            if (unitCubeMesh == null)
                unitCubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (material == null && defaultPrimitiveMaterial == null)
                defaultPrimitiveMaterial = Resources.GetBuiltinResource<Material>("Default-Material.mat");

            // Match CreatePrimitive's unit cube (including its default material when no
            // explicit material is supplied), but avoid creating a default collider only
            // to disable and destroy it for scenery-only boxes.
            if (unitCubeMesh == null || (material == null && defaultPrimitiveMaterial == null))
            {
                var fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fallback.name = name;
                fallback.transform.SetParent(parent, false);
                fallback.transform.localPosition = position;
                fallback.transform.localScale = size;
                if (material != null) fallback.GetComponent<Renderer>().sharedMaterial = material;
                if (!solid)
                {
                    var fallbackCollider = fallback.GetComponent<Collider>();
                    fallbackCollider.enabled = false;
                    UnityEngine.Object.Destroy(fallbackCollider);
                }
                if (CapturePerformanceTimings) { LastBoxMs += TimingElapsedMs(timingStart); LastBoxCount++; }
                return fallback;
            }

            var box = new GameObject(name);
            box.transform.SetParent(parent, false);
            box.name = name;
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.AddComponent<MeshFilter>().sharedMesh = unitCubeMesh;
            box.AddComponent<MeshRenderer>().sharedMaterial = material != null ? material : defaultPrimitiveMaterial;
            if (solid)
            {
                var collider = box.AddComponent<BoxCollider>();
                collider.center = Vector3.zero;
                collider.size = Vector3.one;
            }
            if (CapturePerformanceTimings) { LastBoxMs += TimingElapsedMs(timingStart); LastBoxCount++; }
            return box;
        }
    }
}
