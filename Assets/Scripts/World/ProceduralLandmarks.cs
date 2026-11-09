using System;
using System.Collections.Generic;
using SheltreSim.Items;
using SheltreSim.Objectives;
using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>Seeded expedition destinations. Decks bridge terrain without modifying the landscape.</summary>
    public static class ProceduralLandmarks
    {
        static readonly string[] Kinds = { "Hospital", "Factory", "FuelStation", "Farm", "MilitaryOutpost", "AbandonedBunker" };
        static readonly string[] Titles = { "Field hospital", "Ironworks factory", "Last stop fuel station", "Overgrown farm", "Military relay outpost", "Abandoned bunker" };
        static readonly string[] ShortNames = { "Hospital", "Factory", "Fuel", "Farm", "Military", "Bunker" };
        static readonly string[] Tables = { "hospital", "factory", "fuel", "farm", "military", "bunker" };
        const float HalfWidth = 21f, HalfDepth = 18f, RampLength = 30f;
        public const int KindHospital = 0, KindFactory = 1, KindFuel = 2, KindFarm = 3, KindMilitary = 4, KindBunker = 5, KindCount = 6;

        // What each kind is known for: literal, independent of every random draw, added after the rolled rows.
        static readonly (string id, int amount)[][] FirstCacheRewards = {
            new[] { ("first_aid", 2) },
            new[] { ("electronics", 3) },
            new[] { ("fuel", 6) },
            new[] { ("seeds", 6) },
            new[] { ("ammo_rifle", 12), ("expedition_parts", 1) },
            new (string, int)[0],
        };

        public static string KindName(int kind) => kind >= 0 && kind < KindCount ? Kinds[kind] : "";
        public static string ShortName(int kind) => kind >= 0 && kind < KindCount ? ShortNames[kind] : "";
        public static string Title(int kind) => kind >= 0 && kind < KindCount ? Titles[kind] : "";
        public static string TableName(int kind) => kind >= 0 && kind < KindCount ? Tables[kind] : "";
        /// <summary>The rows added to a kind's first cache (Cache_00). Slot 3 of factories and bunkers also holds one part.</summary>
        public static IReadOnlyList<(string id, int amount)> GuaranteedRewards(int kind) =>
            kind >= 0 && kind < KindCount ? FirstCacheRewards[kind] : new (string, int)[0];

        /// <summary>Pure metadata: answers from the seed alone and never builds a chunk.</summary>
        public static bool TryGetKind(Vector2Int coordinate, int seed, out int kind)
        {
            kind = -1;
            if (!HasLandmark(coordinate, seed)) return false;
            kind = KindAt(coordinate, seed);
            return true;
        }

        public static string StableId(Vector2Int coordinate, int seed) => "landmark:" + seed + ":" + coordinate.x + ":" + coordinate.y;

        public static bool TryParseStableId(string id, out Vector2Int coordinate, out int seed)
        {
            coordinate = default;
            seed = 0;
            if (string.IsNullOrEmpty(id)) return false;
            string[] parts = id.Split(':');
            if (parts.Length != 4 || parts[0] != "landmark" || !int.TryParse(parts[1], out seed) ||
                !int.TryParse(parts[2], out int x) || !int.TryParse(parts[3], out int z)) return false;
            coordinate = new Vector2Int(x, z);
            return true;
        }

        public static Vector3 WorldCenter(Vector2Int coordinate) => new Vector3(coordinate.x * 128f + 64f, 0f, coordinate.y * 128f + 64f);

        public static Vector2Int? FindNearest(int kind, Vector3 fromPosition, int seed, int maxChunkRadius, Func<string, bool> exclude = null) =>
            FindNearest(new[] { kind }, fromPosition, seed, maxChunkRadius, exclude);

        /// <summary>
        /// The nearest matching landmark, by rings outward, from hash functions only: no chunk is generated.
        /// Null kinds means any kind. Exclude receives the stable id and returns true to skip that landmark.
        /// </summary>
        public static Vector2Int? FindNearest(int[] kinds, Vector3 fromPosition, int seed, int maxChunkRadius, Func<string, bool> exclude = null)
        {
            Vector2Int center = WorldLayout.Coordinate(fromPosition);
            Vector2 origin = new Vector2(fromPosition.x, fromPosition.z);
            Vector2Int? best = null;
            float bestDistance = float.PositiveInfinity;
            for (int ring = 0; ring <= maxChunkRadius; ring++)
            {
                // A chunk centre in this ring is at least this far along one axis, so nothing further out can win.
                if (best.HasValue && ring * 128f - 64f > bestDistance) break;
                for (int dz = -ring; dz <= ring; dz++)
                {
                    int step = dz == -ring || dz == ring ? 1 : ring * 2;
                    for (int dx = -ring; dx <= ring; dx += Mathf.Max(1, step))
                    {
                        var coordinate = new Vector2Int(center.x + dx, center.y + dz);
                        if (Math.Abs((long)coordinate.x) > WorldLayout.MaximumChunkCoordinate ||
                            Math.Abs((long)coordinate.y) > WorldLayout.MaximumChunkCoordinate) continue;
                        if (!IsForced(coordinate) && WorldLayout.Hash(coordinate.x, coordinate.y, seed + 4069) % 13 != 0) continue;
                        Vector3 world = WorldCenter(coordinate);
                        float distance = Vector2.Distance(origin, new Vector2(world.x, world.z));
                        if (distance >= bestDistance) continue;
                        if (kinds != null && Array.IndexOf(kinds, KindAt(coordinate, seed)) < 0) continue;
                        if (exclude != null && exclude(StableId(coordinate, seed))) continue;
                        if (!HasLandmark(coordinate, seed)) continue;
                        best = coordinate;
                        bestDistance = distance;
                    }
                }
            }
            return best;
        }

        static bool IsForced(Vector2Int coordinate) => coordinate == new Vector2Int(-2, -3) || coordinate == new Vector2Int(3, -4) ||
            coordinate == new Vector2Int(-4, -5);

        static int KindAt(Vector2Int coordinate, int seed) =>
            coordinate == new Vector2Int(-2, -3) ? 0 : coordinate == new Vector2Int(3, -4) ? 2 :
            coordinate == new Vector2Int(-4, -5) ? 1 : WorldLayout.Hash(coordinate.x, coordinate.y, seed + 809) % Kinds.Length;

        public static bool HasLandmark(Vector2Int coordinate, int seed)
        {
            if (WorldLayout.IsTownChunk(coordinate, seed)) return false;
            if (!IsForced(coordinate) && WorldLayout.Hash(coordinate.x, coordinate.y, seed + 4069) % 13 != 0) return false;
            return TryDeck(coordinate, seed, out _, out _);
        }

        public static GameObject Build(Transform parent, Vector2Int coordinate, int seed, ProceduralWorldAssets assets, bool ruined)
        {
            if (assets == null || !HasLandmark(coordinate, seed) || !TryDeck(coordinate, seed, out float deck, out float toe)) return null;
            int kind = KindAt(coordinate, seed);
            var root = new GameObject("Landmark_" + Kinds[kind]);
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(coordinate.x * 128f + 64f, deck, coordinate.y * 128f + 64f);
            Transform t = root.transform;
            Material wall = assets.structureMaterial, dark = assets.roadMaterial;
            Box(t, "Foundation", new Vector3(0, -.4f, 0), new Vector3(42, .8f, 36), wall);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    float ground = WorldLayout.HeightAt(t.position.x + x * 19, t.position.z + z * 16, seed);
                    float height = Mathf.Max(.5f, deck - ground);
                    Box(t, "Footing_" + x + "_" + z, new Vector3(x * 19, -height * .5f, z * 16), new Vector3(2, height, 2), wall);
                }
            float rise = deck - toe;
            float angle = Mathf.Atan2(rise, RampLength) * Mathf.Rad2Deg;
            var ramp = Box(t, "EntranceRamp", new Vector3(0, -rise * .5f - .15f * Mathf.Cos(angle * Mathf.Deg2Rad), -33),
                new Vector3(4.2f, .3f, Mathf.Sqrt(RampLength * RampLength + rise * rise)), wall);
            ramp.transform.localRotation = Quaternion.Euler(-angle, 0, 0);
            // The entrance axis and ramp remain clear: rooms open towards negative Z.
            switch (kind)
            {
                case 0: Hospital(t, wall, dark); break;
                case 1: Factory(t, wall, dark); break;
                case 2: Fuel(t, wall, dark); break;
                case 3: Farm(t, wall, dark); break;
                case 4: Military(t, wall, dark); break;
                default: Bunker(t, wall, dark); break;
            }
            // Before the bombs this is just an open business: nothing to search, nothing to discover.
            if (ruined)
            {
                var random = new System.Random(WorldLayout.Hash(coordinate.x, coordinate.y, seed + 1237));
                // Four interior caches; names and draws stay fixed across streaming and save reloads.
                for (int i = 0; i < 4; i++)
                {
                    Vector3 position = new Vector3(i < 2 ? -4 : 4, .05f, 7 + (i % 2) * 3);
                    Cache(t, "Cache_" + i.ToString("D2"), position, kind, i, assets, random);
                }
                var discovery = root.AddComponent<LandmarkDiscovery>();
                discovery.stableId = StableId(coordinate, seed);
                discovery.title = Titles[kind];
                // Consumes no draw: the relay is the same object on every military landmark.
                if (kind == KindMilitary) RelayPoint(t, discovery.stableId, assets);
            }
            root.SetActive(true);
            return root;
        }

        static bool TryDeck(Vector2Int c, int seed, out float deck, out float toe)
        {
            float x = c.x * 128f + 64f, z = c.y * 128f + 64f;
            deck = float.NegativeInfinity;
            toe = WorldLayout.HeightAt(x, z - 48f, seed) + .12f;
            // Every destination fits within its own chunk, outside roads and preserved starting ground.
            for (int ix = -1; ix <= 1; ix++)
                for (int iz = -1; iz <= 1; iz++)
                    if (WorldLayout.IsHomePlot(x + ix * HalfWidth, z + iz * 48f) ||
                        WorldLayout.IsRoad(x + ix * HalfWidth, z + iz * 48f, 2f)) return false;
            deck = HighestGround(x - HalfWidth, x + HalfWidth, z - HalfDepth, z + HalfDepth, seed) + .3f;
            // Raise the deck enough for the entire ramp surface to clear the terrain underneath it.
            // Includes both ramp edges; terrain is piecewise linear on a 16m grid.
            for (int step = 1; step <= 30; step++)
            {
                float fraction = step / 30f;
                for (int edge = -1; edge <= 1; edge++)
                {
                    float height = WorldLayout.HeightAt(x + edge * 2.1f, z - 48 + step, seed) + .12f;
                    deck = Mathf.Max(deck, toe + (height - toe) / fraction);
                }
            }
            return deck - toe >= 0 && deck - toe <= RampLength * Mathf.Tan(35f * Mathf.Deg2Rad);
        }

        // HeightAt is linear on each 16m triangle. Check clipped grid corners and diagonal
        // intersections so a narrow summit cannot poke through between arbitrary sample points.
        static float HighestGround(float minX, float maxX, float minZ, float maxZ, int seed)
        {
            float maximum = float.NegativeInfinity;
            for (float cellX = Mathf.Floor(minX / 16f) * 16f; cellX <= maxX; cellX += 16f)
                for (float cellZ = Mathf.Floor(minZ / 16f) * 16f; cellZ <= maxZ; cellZ += 16f)
                {
                    for (int ix = 0; ix < 2; ix++)
                        for (int iz = 0; iz < 2; iz++)
                            maximum = Mathf.Max(maximum, WorldLayout.HeightAt(
                                Mathf.Clamp(cellX + ix * 16f, minX, maxX),
                                Mathf.Clamp(cellZ + iz * 16f, minZ, maxZ), seed));
                    float first = Mathf.Max(0, Mathf.Max(minX - cellX, minZ - cellZ));
                    float last = Mathf.Min(16, Mathf.Min(maxX - cellX, maxZ - cellZ));
                    if (first <= last)
                    {
                        maximum = Mathf.Max(maximum, WorldLayout.HeightAt(cellX + first, cellZ + first, seed));
                        maximum = Mathf.Max(maximum, WorldLayout.HeightAt(cellX + last, cellZ + last, seed));
                    }
                }
            return maximum;
        }

        static void Room(Transform t, Vector3 center, float width, float depth, float height, Material wall, Material dark)
        {
            Box(t, "BackWall", center + new Vector3(0, height * .5f, depth * .5f), new Vector3(width, height, .4f), wall);
            Box(t, "WestWall", center + new Vector3(-width * .5f, height * .5f, 0), new Vector3(.4f, height, depth), wall);
            Box(t, "EastWall", center + new Vector3(width * .5f, height * .5f, 0), new Vector3(.4f, height, depth), wall);
            float wing = (width - 3.4f) * .5f;
            for (int side = -1; side <= 1; side += 2)
                Box(t, "EntranceWing", center + new Vector3(side * (width * .5f - wing * .5f), height * .5f, -depth * .5f),
                    new Vector3(wing, height, .4f), wall);
            Box(t, "EntranceLintel", center + new Vector3(0, (height + 2.8f) * .5f, -depth * .5f), new Vector3(3.4f, height - 2.8f, .4f), wall);
            Box(t, "Roof", center + Vector3.up * (height + .15f), new Vector3(width + .8f, .3f, depth + .8f), wall);
            for (int i = -1; i <= 1; i += 2)
                Box(t, "Window", center + new Vector3(i * (width * .5f - 1.8f), 1.9f, -depth * .5f - .22f), new Vector3(1.8f, 1.1f, .06f), dark, false);
        }

        static void Hospital(Transform t, Material w, Material d)
        {
            Room(t, new Vector3(0, 0, 7), 16, 18, 4, w, d);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(t, "WardWing", new Vector3(side * 13, 2.5f, 8), new Vector3(9, 5, 14), w);
                for (int i = 0; i < 3; i++)
                {
                    Box(t, "WardWindow", new Vector3(side * 13, 3, .95f), new Vector3(1.8f, 1.8f, .1f), d, false);
                    Box(t, "Bed", new Vector3(side * 5.6f, .55f, i * 3.5f + 1), new Vector3(1.6f, 1.1f, 2.4f), w);
                }
            }
            Box(t, "MedicalCrossVertical", new Vector3(0, 6, -2.3f), new Vector3(1, 4, .5f), d, false);
            Box(t, "MedicalCrossHorizontal", new Vector3(0, 6, -2.3f), new Vector3(3.4f, 1, .5f), d, false);
            Box(t, "AmbulanceCanopy", new Vector3(0, 3, -8), new Vector3(12, .3f, 8), w);
        }

        static void Factory(Transform t, Material w, Material d)
        {
            Room(t, new Vector3(0, 0, 7), 20, 18, 7, w, d);
            for (int i = 0; i < 2; i++)
            {
                Cylinder(t, "Smokestack", new Vector3(-15 + i * 5, 10, 10), new Vector3(3, 10, 3), w);
                Cylinder(t, "StackRim", new Vector3(-15 + i * 5, 20, 10), new Vector3(3.6f, .5f, 3.6f), d);
            }
            for (int i = 0; i < 4; i++)
            {
                Box(t, "MachineBase", new Vector3(i % 2 == 0 ? -7 : 7, .8f, -3 + i * 3), new Vector3(3, 1.6f, 2), d);
                Box(t, "MachineColumn", new Vector3(i % 2 == 0 ? -7 : 7, 2.3f, -3 + i * 3), new Vector3(.8f, 1.5f, .8f), w);
            }
            Box(t, "LoadingAwning", new Vector3(13, 4, -6), new Vector3(10, .4f, 10), d);
            for (int i = 0; i < 3; i++) Box(t, "Pallet", new Vector3(13, .4f + i * .8f, -6), new Vector3(4, .7f, 3), w);
        }

        static void Fuel(Transform t, Material w, Material d)
        {
            Room(t, new Vector3(0, 0, 8), 16, 16, 3.6f, w, d);
            Box(t, "ForecourtCanopy", new Vector3(0, 4.5f, -8), new Vector3(32, .5f, 12), w);
            for (int x = -1; x <= 1; x += 2)
            {
                Box(t, "CanopyPost", new Vector3(x * 14, 2.2f, -8), new Vector3(.45f, 4.4f, .45f), d);
                for (int i = 0; i < 2; i++)
                {
                    Box(t, "PumpIsland", new Vector3(x * 7, .15f, -11 + i * 5), new Vector3(3, .3f, 2.5f), w);
                    Box(t, "FuelPump", new Vector3(x * 7, 1.1f, -11 + i * 5), new Vector3(.9f, 1.6f, .7f), d);
                    Box(t, "PumpDisplay", new Vector3(x * 7, 1.5f, -11.4f + i * 5), new Vector3(.7f, .4f, .06f), w, false);
                }
            }
            Box(t, "RoadSignPole", new Vector3(18, 5, 8), new Vector3(.5f, 10, .5f), w);
            Box(t, "RoadSign", new Vector3(18, 9, 8), new Vector3(4, 3, .4f), d);
            Cylinder(t, "StorageTank", new Vector3(-15, 2, 10), new Vector3(5, 2, 5), d);
        }

        static void Farm(Transform t, Material w, Material d)
        {
            Room(t, new Vector3(0, 0, 8), 16, 16, 4, w, d);
            for (int side = -1; side <= 1; side += 2)
            {
                var roof = Box(t, "BarnRoof", new Vector3(side * 4.2f, 5.7f, 8), new Vector3(9.5f, .35f, 18), d);
                roof.transform.localRotation = Quaternion.Euler(0, 0, side * -24);
                for (int row = 0; row < 4; row++)
                {
                    Box(t, "RaisedGrowingBed", new Vector3(side * 13.5f, .35f, -10 + row * 6), new Vector3(6, .7f, 3), w);
                    for (int plant = 0; plant < 2; plant++)
                        Box(t, "Crop", new Vector3(side * 13.5f + plant - .5f, 1, -10 + row * 6), new Vector3(.6f, .7f, 2), d, false);
                }
            }
            Cylinder(t, "Silo", new Vector3(-15, 4, 12), new Vector3(5, 4, 5), w);
            Cylinder(t, "SiloCap", new Vector3(-15, 8.2f, 12), new Vector3(5.4f, .25f, 5.4f), d);
        }

        static void Military(Transform t, Material w, Material d)
        {
            Room(t, new Vector3(0, 0, 8), 16, 16, 3.5f, w, d);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int post = 0; post < 4; post++)
                    Box(t, "FencePost", new Vector3(side * 19, 1.2f, -12 + post * 8), new Vector3(.4f, 2.4f, .4f), w);
                Box(t, "FenceRail", new Vector3(side * 19, 1.7f, 0), new Vector3(.2f, .3f, 30), d);
                Box(t, "Barricade", new Vector3(side * 10, .8f, -12), new Vector3(12, 1.6f, 2), w);
            }
            for (int x = 0; x < 2; x++)
                for (int z = 0; z < 2; z++)
                    Box(t, "RelayLeg", new Vector3(12 + x * 4, 4, 8 + z * 4), new Vector3(.5f, 8, .5f), d);
            Box(t, "RelayPlatform", new Vector3(14, 8, 10), new Vector3(6, .5f, 6), w);
            Cylinder(t, "RelayMast", new Vector3(14, 12, 10), new Vector3(.5f, 4, .5f), d);
            Box(t, "RelayAerial", new Vector3(14, 14, 10), new Vector3(7, .3f, .3f), w, false);
        }

        static void Bunker(Transform t, Material w, Material d)
        {
            Room(t, new Vector3(0, 0, 7), 16, 18, 3.2f, w, d);
            Box(t, "ArmouredRoof", new Vector3(0, 4, 7), new Vector3(24, 1.3f, 24), w);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(t, "BlastButtress", new Vector3(side * 10, 1.9f, 7), new Vector3(4, 3.8f, 21), w);
                Box(t, "EntranceBaffle", new Vector3(side * 4.5f, 1.5f, -7), new Vector3(3, 3, 8), w);
                Cylinder(t, "AirIntake", new Vector3(side * 6, 5.4f, 11), new Vector3(1.5f, .8f, 1.5f), d);
                Box(t, "AirIntakeCap", new Vector3(side * 6, 6.3f, 11), new Vector3(2, .3f, 2), w);
            }
            Box(t, "EntranceHeader", new Vector3(0, 3.2f, -9), new Vector3(12, .5f, 2), d);
            for (int i = 0; i < 3; i++) Box(t, "SupplyShelf", new Vector3(-6, .6f + i * .9f, 5), new Vector3(1, .15f, 6), d);
        }

        static void Cache(Transform parent, string name, Vector3 position, int kind, int slot, ProceduralWorldAssets assets, System.Random random)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            Box(root.transform, "Case", Vector3.up * .4f, new Vector3(1.1f, .8f, .8f), assets.structureMaterial, false);
            Box(root.transform, "Lid", Vector3.up * .84f, new Vector3(1.2f, .1f, .9f), assets.roadMaterial, false);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = Vector3.up * .45f;
            collider.size = new Vector3(1.2f, .9f, .9f);
            var loot = root.AddComponent<LootContainer>();
            SheltreSim.Crafting.LootTables.Fill(loot, Tables[kind], random);
            // One guaranteed component in industrial/bunker caches; others occasionally have a salvageable part.
            if ((slot == 3 && (kind == 1 || kind == 5)) || random.NextDouble() < .12)
                Add(loot, assets.expeditionParts, 1);
            // The key reward is guaranteed: appended after the rolled rows, so it draws nothing and shifts nothing.
            if (slot == 0)
                foreach (var reward in FirstCacheRewards[kind])
                {
                    ResourceDefinition resource = SheltreSim.Crafting.ItemCatalog.Def(reward.id);
                    if (resource == null && reward.id == "expedition_parts") resource = assets.expeditionParts;
                    Add(loot, resource, reward.amount);
                }
            loot.hasLoot = loot.entries.Count > 0;
        }

        // Beside the mast's legs, on the open east side of the outpost.
        static void RelayPoint(Transform parent, string landmarkId, ProceduralWorldAssets assets)
        {
            var root = new GameObject("RelayRepairPoint");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(14f, 0f, 6.4f);
            Box(root.transform, "RelayCabinet", Vector3.up * .75f, new Vector3(1.3f, 1.5f, .7f), assets.structureMaterial, false);
            Box(root.transform, "RelayPanel", new Vector3(0f, .95f, -.37f), new Vector3(.9f, .6f, .05f), assets.roadMaterial, false);
            GameObject lamp = Box(root.transform, "RelayLamp", Vector3.up * 1.58f, Vector3.one * .16f, assets.roadMaterial, false);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = Vector3.up * .8f;
            collider.size = new Vector3(1.4f, 1.6f, .8f);
            var point = root.AddComponent<RelayRepairPoint>();
            point.landmarkId = landmarkId;
            point.lamp = lamp.GetComponent<Renderer>();
        }

        static void Add(LootContainer loot, ResourceDefinition resource, int amount)
        {
            if (resource != null) loot.entries.Add(new LootEntry { resource = resource, amount = amount });
        }

        static GameObject Box(Transform t, string name, Vector3 p, Vector3 size, Material material, bool solid = true)
            => Primitive(t, name, p, size, material, PrimitiveType.Cube, solid);
        static GameObject Cylinder(Transform t, string name, Vector3 p, Vector3 size, Material material)
            => Primitive(t, name, p, size, material, PrimitiveType.Cylinder, true);
        static GameObject Primitive(Transform t, string name, Vector3 p, Vector3 size, Material material, PrimitiveType type, bool solid)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(t, false);
            go.transform.localPosition = p;
            go.transform.localScale = size;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid)
            {
                var collider = go.GetComponent<Collider>();
                collider.enabled = false;
                UnityEngine.Object.Destroy(collider);
            }
            return go;
        }
    }
}
