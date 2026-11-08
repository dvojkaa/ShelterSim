using System;
using System.Collections.Generic;
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
        public static GameObject Build(Transform parent, Vector2Int coordinate, int seed, ProceduralWorldAssets assets, bool ruined)
        {
            var root = new GameObject("Content");
            root.transform.SetParent(parent, false);
            if (assets == null) return root;
            var random = new System.Random(ContentSeed(coordinate, seed));
            float x = coordinate.x * (float)WorldLayout.ChunkSize;
            float z = coordinate.y * (float)WorldLayout.ChunkSize;
            if (WorldLayout.IsTownChunk(coordinate, seed))
                Town(root.transform, x, z, seed, assets, coordinate, ruined);
            else if (WorldLayout.IsSuburbChunk(coordinate, seed))
                Suburb(root.transform, x, z, seed, assets, coordinate, ruined);
            else
                Nature(root.transform, x, z, seed, assets, random, ruined);
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

        static void Town(Transform parent, float originX, float originZ, int seed,
            ProceduralWorldAssets assets, Vector2Int coordinate, bool ruined)
        {
            // Nine separated lots with two crossing streets; these roads join the boundary ring.
            for (int line = 0; line < 4; line++)
            {
                float offset = line == 0 ? 3.5f : line == 1 ? 44 : line == 2 ? 84 : 124.5f;
                Street(parent, originX + offset, originZ + 64, true, seed, assets);
                Street(parent, originX + 64, originZ + offset, false, seed, assets);
            }
            for (int slot = 0; slot < 9; slot++)
            {
                var random = SlotRandom(coordinate, seed, slot);
                // Corner lots are guaranteed; middle lots vary so districts have open spaces.
                bool corner = slot % 3 != 1 && slot / 3 != 1;
                bool occupied = corner || random.NextDouble() < .65;
                float x = originX + 24 + (slot % 3) * 40;
                float z = originZ + 24 + (slot / 3) * 40;
                int floors = random.Next(1, 6);
                if (!occupied || !Clear(x, z, 9, seed) || !LevelLot(x, z, seed)) continue;
                Building(parent, "Building_" + slot.ToString("D2"), x, z, floors, seed, assets, random, ruined);
                if (ruined && floors >= 3 && slot % 4 == 0)
                    Smoke(parent, new Vector3(x, WorldLayout.HeightAt(x, z, seed) + floors * 3.4f + .5f, z));
            }
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
            ProceduralWorldAssets assets, Vector2Int coordinate, bool ruined)
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
                House(parent, "House_" + slot.ToString("D2"), x, z, north ? 0f : 180f, seed, assets, lot, ruined);
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

        /// <summary>A 10 x 12 m family house that can be walked into. Front faces local -Z; its doorway stays raider-wide.</summary>
        static void House(Transform parent, string name, float x, float z, float yaw, int seed,
            ProceduralWorldAssets assets, System.Random random, bool ruined)
        {
            var house = new GameObject(name);
            house.transform.SetParent(parent, false);
            house.transform.SetPositionAndRotation(new Vector3(x, WorldLayout.HeightAt(x, z, seed), z), Quaternion.Euler(0, yaw, 0));
            Transform t = house.transform;
            // Fresh paint before the bombs, the same house cracked and blackened after them.
            int paint = random.Next(1000);
            Material wall = paint % 3 == 0 || assets.plasterMaterial == null ? assets.structureMaterial : assets.plasterMaterial;
            if (!ruined && paint % 3 != 0 && assets.houseCleanMaterials != null && assets.houseCleanMaterials.Length > 0)
                wall = assets.houseCleanMaterials[paint % assets.houseCleanMaterials.Length];
            const float height = 3f;
            Box(t, "Floor", new Vector3(0, .05f, 0), new Vector3(10, .1f, 12), wall);
            Box(t, "Back", new Vector3(0, height * .5f, 5.85f), new Vector3(10, height, .3f), wall);
            Box(t, "West", new Vector3(-4.85f, height * .5f, 0), new Vector3(.3f, height, 12), wall);
            Box(t, "East", new Vector3(4.85f, height * .5f, 0), new Vector3(.3f, height, 12), wall);
            Box(t, "FrontWest", new Vector3(-2.95f, height * .5f, -5.85f), new Vector3(4.1f, height, .3f), wall);
            Box(t, "FrontEast", new Vector3(2.95f, height * .5f, -5.85f), new Vector3(4.1f, height, .3f), wall);
            Box(t, "DoorLintel", new Vector3(0, 2.6f, -5.85f), new Vector3(1.8f, .8f, .3f), wall);
            Box(t, "Ceiling", new Vector3(0, height + .05f, 0), new Vector3(10, .1f, 12), wall);
            // A dark frame, so the doorway reads as a doorway and not as more wall.
            foreach (int side in new[] { -1, 1 })
                Box(t, "DoorPost", new Vector3(side * .95f, 1.1f, -6.02f), new Vector3(.12f, 2.2f, .06f), assets.roadMaterial, false);
            Box(t, "DoorHead", new Vector3(0, 2.25f, -6.02f), new Vector3(2.02f, .12f, .06f), assets.roadMaterial, false);

            int roofIndex = random.Next(2);
            bool burnedOut = ruined && random.NextDouble() < .35;
            GameObject roof = assets.houseRoofs != null && assets.houseRoofs.Length > 0 ? assets.houseRoofs[roofIndex % assets.houseRoofs.Length] : null;
            if (!burnedOut)
            {
                if (roof != null) Place(t, roof, "Roof", new Vector3(0, height + .1f, 0), 0f, assets);
                else Box(t, "Roof", new Vector3(0, height + .3f, 0), new Vector3(11, .4f, 13), assets.roadMaterial, false);
            }
            GameObject window = ruined ? assets.houseWindowBroken : assets.houseWindow;
            if (window != null)
            {
                foreach (float wx in new[] { -2.9f, 2.9f })
                {
                    Place(t, window, "Window", new Vector3(wx, 1f, -6.02f), 0f, assets);
                    Place(t, window, "Window", new Vector3(wx, 1f, 6.02f), 180f, assets);
                }
                foreach (int side in new[] { -1, 1 })
                    Place(t, window, "Window", new Vector3(side * 5.02f, 1f, 1f), side * -90f, assets);
            }
            // Stood open against the inside wall, so it never narrows the doorway.
            if (assets.houseDoor != null) Place(t, assets.houseDoor, "FrontDoor", new Vector3(-1.45f, 0f, -5.4f), ruined ? 70f : 90f, assets);

            // Front garden, towards the street.
            for (int i = -1; i <= 1; i += 2)
                for (int section = 0; section < 2; section++)
                    if (assets.hedge != null) Place(t, assets.hedge, "Hedge", new Vector3(i * (2.2f + section * 2f), 0f, -11f), 0f, assets);
            if (assets.mailbox != null) Place(t, assets.mailbox, "Mailbox", new Vector3(1.3f, 0f, -11.2f), 0f, assets);
            if (assets.gardenShed != null && random.NextDouble() < .4)
                Place(t, assets.gardenShed, "Shed", new Vector3(3.5f, 0f, 9f), 0f, assets, true);

            Vector3 parking = t.TransformPoint(new Vector3(-7f, 0f, -6f));
            if (Clear(parking.x, parking.z, 3f, seed) && random.NextDouble() < .7)
                Car(t, "Car", parking.x, parking.z, seed, assets, random, ruined, "car", yaw + (ruined ? 25f : 0f));

            if (ruined)
            {
                Crate(t, "Crate_00", new Vector3(-2.8f, .1f, 3.6f), assets, "house", random);
                if (random.NextDouble() < .5) Crate(t, "Crate_01", new Vector3(2.8f, .1f, 3.6f), assets, "house", random);
                if (burnedOut && random.NextDouble() < .4) Smoke(t, t.position + Vector3.up * 3.2f);
            }
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

        static void Street(Transform parent, float x, float z, bool northSouth, int seed, ProceduralWorldAssets assets)
        {
            // Town terrain is flattened by WorldLayout. Surface strips have no gameplay collider.
            for (int part = 0; part < 8; part++)
            {
                float px = northSouth ? x : x - 56 + part * 16;
                float pz = northSouth ? z - 56 + part * 16 : z;
                if (WorldLayout.IsRoad(px, pz) || WorldLayout.IsConnectorRoad(px, pz, seed) || WorldLayout.IsHomePlot(px, pz) ||
                    WorldLayout.IsHomePlot(px - 8, pz - 8) ||
                    WorldLayout.IsHomePlot(px + 8, pz + 8)) continue;
                // A flat strip on a slope would float. Streets stop where the town's flat ground does.
                if (Mathf.Abs(WorldLayout.HeightAt(px - 8, pz - 8, seed) - WorldLayout.HeightAt(px + 8, pz + 8, seed)) > .05f ||
                    Mathf.Abs(WorldLayout.HeightAt(px - 8, pz + 8, seed) - WorldLayout.HeightAt(px + 8, pz - 8, seed)) > .05f ||
                    Mathf.Abs(WorldLayout.HeightAt(px, pz, seed) - WorldLayout.HeightAt(px + 8, pz + 8, seed)) > .05f) continue;
                var surface = Box(parent, "Street", Vector3.zero,
                    northSouth ? new Vector3(7, .04f, 16) : new Vector3(16, .04f, 7), assets.roadMaterial, false);
                surface.transform.position = new Vector3(px, WorldLayout.HeightAt(px, pz, seed) + .025f, pz);
            }
        }

        static void Building(Transform parent, string name, float x, float z, int floors, int seed,
            ProceduralWorldAssets assets, System.Random random, bool ruined)
        {
            var building = new GameObject(name);
            building.transform.SetParent(parent, false);
            building.transform.position = new Vector3(x, WorldLayout.HeightAt(x, z, seed), z);
            Transform t = building.transform;
            Material wall = assets.structureMaterial;
            const float storey = 3.4f;
            Box(t, "Floor", new Vector3(0, .02f, 0), new Vector3(12, .2f, 12), wall);
            Box(t, "Back", new Vector3(0, 1.8f, 5.85f), new Vector3(12, storey, .3f), wall);
            Box(t, "West", new Vector3(-5.85f, 1.8f, 0), new Vector3(.3f, storey, 12), wall);
            Box(t, "East", new Vector3(5.85f, 1.8f, 0), new Vector3(.3f, storey, 12), wall);
            // 2.8 m clear width, 2.6 m clear height; crates stay at the back of the room.
            Box(t, "FrontWest", new Vector3(-3.7f, 1.8f, -5.85f), new Vector3(4.6f, storey, .3f), wall);
            Box(t, "FrontEast", new Vector3(3.7f, 1.8f, -5.85f), new Vector3(4.6f, storey, .3f), wall);
            Box(t, "DoorLintel", new Vector3(0, 3.05f, -5.85f), new Vector3(2.8f, .9f, .3f), wall);
            Box(t, "Ceiling", new Vector3(0, 3.5f, 0), new Vector3(12, .2f, 12), wall);
            if (floors > 1)
                Box(t, "UpperFloors", new Vector3(0, 3.6f + (floors - 1) * storey * .5f, 0),
                    new Vector3(12, (floors - 1) * storey, 12), wall);
            float roofY = floors * storey + .2f;
            Box(t, "Roof", new Vector3(0, roofY, 0), new Vector3(12.5f, .25f, 12.5f), wall);
            Box(t, "RoofVent", new Vector3(2, roofY + .7f, 2), new Vector3(2, 1.2f, 2), assets.roadMaterial, false);
            Box(t, "Parapet", new Vector3(0, roofY + .45f, 5.8f), new Vector3(12, .7f, .25f), wall, false);
            for (int floor = 0; floor < floors; floor++)
            {
                float y = floor * storey + 1.9f;
                for (int window = -1; window <= 1; window++)
                {
                    if (floor == 0 && window == 0) continue;
                    Box(t, "Window", new Vector3(window * 3.6f, y, -6.025f),
                        new Vector3(1.5f, 1.4f, .08f), assets.roadMaterial, false);
                    Box(t, "Sill", new Vector3(window * 3.6f, y - .75f, -6.12f),
                        new Vector3(1.8f, .12f, .35f), wall, false);
                }
            }
            Box(t, "DoorCanopy", new Vector3(0, 2.85f, -6.5f), new Vector3(3.6f, .12f, 1.3f), wall, false);
            if (!ruined) return;
            Crate(t, "Crate_00", new Vector3(-3.6f, .12f, 3.4f), assets, "town", random);
            if (random.NextDouble() < .5)
                Crate(t, "Crate_01", new Vector3(3.6f, .12f, 3.4f), assets, "town", random);
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
            LootTables.Fill(loot, table, random);
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
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = material;
            // Assets are collider-free FBXs; imported root conversion is baked in their meshes.
        }

        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size,
            Material material, bool solid = true)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            if (material != null) box.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid)
            {
                var collider = box.GetComponent<Collider>();
                collider.enabled = false;
                UnityEngine.Object.Destroy(collider);
            }
            return box;
        }
    }
}
