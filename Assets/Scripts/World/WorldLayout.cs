using System;
using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>Pure seeded layout functions: independent of visit order and Unity's global random state.</summary>
    public static class WorldLayout
    {
        public const int ChunkSize = 128;
        /// <summary>Bump when terrain or placement generation changes, so a save can tell it was made on a different world.</summary>
        public const int GenerationVersion = 1;
        public const int MaximumChunkCoordinate = 100000;
        public static readonly string[] BiomeNames = { "Wasteland", "Pine forest", "Red desert", "Frozen wilderness", "Wetlands", "Highlands" };

        public static int Hash(int x, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)seed ^ (uint)x * 374761393u ^ (uint)z * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return (int)((h ^ (h >> 16)) & 0x7fffffff);
            }
        }

        public static Vector2Int Coordinate(Vector3 position) => new Vector2Int(
            Mathf.FloorToInt(position.x / ChunkSize), Mathf.FloorToInt(position.z / ChunkSize));

        /// <summary>The player's own lot. Generators keep out of it; the site and bunker are scene objects.</summary>
        public static bool IsHomePlot(float x, float z) => x >= 48f && x <= 96f && z >= -246f && z <= -204f;

        /// <summary>The one terrain cell left out so a stairway can go down. Scene objects cover the rest of it.</summary>
        public static bool IsTerrainHoleCell(float x, float z) => x >= 64f && x < 80f && z >= -240f && z < -224f;

        // The home plot: a suburban lot on the west edge of the first town, with the bunker under it.
        public const float HomeGroundY = 6f;
        public static readonly Vector3 BunkerCenter = new Vector3(72f, 0f, -217.5f);

        /// <summary>0 on the surface, 1 inside the bunker, in between on the stairs. For presentation: light, fog, rain, wind.</summary>
        public static float UndergroundAmount(Vector3 p)
        {
            if (p.x < 58f || p.x > 92f || p.z < -242f || p.z > -205f) return 0f;
            return Mathf.InverseLerp(HomeGroundY - 0.5f, HomeGroundY - 3.5f, p.y);
        }

        public static int BiomeIndexAt(float x, float z, int seed)
        {
            float offset = (seed & 65535) * 0.07f;
            float temperature = Mathf.PerlinNoise(x / 1700f + offset, z / 1700f + 932.1f);
            float moisture = Mathf.PerlinNoise(x / 1100f + 712.6f, z / 1100f + offset);
            if (temperature < 0.35f) return 3;
            if (temperature > 0.63f && moisture < 0.53f) return 2;
            if (moisture > 0.63f) return 4;
            if (moisture > 0.48f) return 1;
            if (temperature < 0.48f) return 5;
            return 0;
        }

        public static bool IsRoad(float x, float z, float margin = 0f) =>
            DistanceToGrid(x, 512f) < 6f + margin || DistanceToGrid(z, 512f) < 6f + margin;

        public static bool IsConnectorRoad(float x,float z,int seed,float margin=0f)
        {
            int mx=Mathf.FloorToInt(x/512f), mz=Mathf.FloorToInt(z/512f);
            return TownMacro(mx,mz,seed) && (Mathf.Abs(x-(mx*512f+256f))<6f+margin || Mathf.Abs(z-(mz*512f+256f))<6f+margin);
        }

        private static float DistanceToGrid(float value, float spacing) =>
            Mathf.Abs(value - Mathf.Round(value / spacing) * spacing);

        public static bool IsTownChunk(Vector2Int coordinate, int seed)
        {
            int mx = Mathf.FloorToInt(coordinate.x / 4f);
            int mz = Mathf.FloorToInt(coordinate.y / 4f);
            int lx = coordinate.x - mx * 4;
            int lz = coordinate.y - mz * 4;
            return TownMacro(mx, mz, seed) && lx >= 1 && lx <= 2 && lz >= 1 && lz <= 2;
        }

        /// <summary>The ring of family houses around a town's centre blocks.</summary>
        public static bool IsSuburbChunk(Vector2Int coordinate, int seed) =>
            TownMacro(Mathf.FloorToInt(coordinate.x / 4f), Mathf.FloorToInt(coordinate.y / 4f), seed) && !IsTownChunk(coordinate, seed);

        private static bool TownMacro(int x, int z, int seed) =>
            (x == 0 && z == -1) || Hash(x, z, seed + 937) % 4 == 0;

        private static float TownDistance(float x, float z, int seed)
        {
            int mx = Mathf.FloorToInt(x / 512f), mz = Mathf.FloorToInt(z / 512f);
            return TownMacro(mx, mz, seed)
                ? Mathf.Max(Mathf.Abs(x - (mx * 512f + 256f)), Mathf.Abs(z - (mz * 512f + 256f)))
                : 10000f;
        }

        private static float RiverDistance(float x, float z, int seed)
        {
            float phase = (seed & 65535) * 0.013f;
            float bend = Mathf.Sin(z / 390f + phase) * 115f + Mathf.Sin(z / 167f + phase) * 28f;
            return DistanceToGrid(x - 330f - bend, 1536f);
        }

        // Lake basins repeat in sparse seeded districts, independently of chunk load order.
        private static float LakeDistance(float x, float z, int seed)
        {
            int gx = Mathf.FloorToInt(x / 1024f), gz = Mathf.FloorToInt(z / 1024f);
            int hash = Hash(gx, gz, seed + 4729);
            if (hash % 3 != 0) return 10000f;
            float cx = gx * 1024f + 256f + hash % 481;
            float cz = gz * 1024f + 256f + (hash / 17) % 481;
            // An elliptical shore softens the visual repetition of circular ponds.
            return new Vector2((x - cx) * 0.85f, (z - cz) * 1.15f).magnitude;
        }

        private static float WaterNodeDepth(float x, float z, int seed)
        {
            // Keep town pads and their approaches dry.
            if (TownDistance(x, z, seed) < 240f) return 0f;
            float river = Mathf.Clamp01((30f - RiverDistance(x, z, seed)) / 18f);
            float lake = Mathf.Clamp01((90f - LakeDistance(x, z, seed)) / 30f);
            return Mathf.Max(river, lake) * 0.42f;
        }

        private static float RawHeight(float x, float z, int seed)
        {
            float offset = (seed & 65535) * 0.031f;
            float broad = Mathf.PerlinNoise(x / 620f + offset, z / 620f + 391f);
            float detail = Mathf.PerlinNoise(x / 140f + 81f, z / 140f + offset);
            float climateOffset = (seed & 65535) * 0.07f;
            float temperature = Mathf.PerlinNoise(x / 1700f + climateOffset, z / 1700f + 932.1f);
            float moisture = Mathf.PerlinNoise(x / 1100f + 712.6f, z / 1100f + climateOffset);
            // Continuous climate weights prevent cliffs at categorical biome boundaries.
            float cold = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.31f, 0.39f, temperature));
            float highlands = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.44f, 0.52f, temperature)))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.44f, 0.52f, moisture)));
            float wet = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.59f, 0.67f, moisture)) * (1f - cold);
            float amplitude = Mathf.Lerp(Mathf.Lerp(35f, 75f, Mathf.Max(cold, highlands)), 12f, wet);
            // Broad saddles cut traversable passes through mountain country.
            float passDistance = DistanceToGrid(z + Mathf.Sin(x / 570f + offset) * 95f, 1024f);
            float pass = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(35f, 165f, passDistance));
            amplitude = Mathf.Lerp(amplitude, 24f, pass * Mathf.Max(cold, highlands));
            float height = (broad - 0.38f) * amplitude + detail * 7f;
            float riverValley = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(22f, 130f, RiverDistance(x, z, seed)));
            float lakeBasin = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(100f, 180f, LakeDistance(x, z, seed)));
            height -= riverValley * 5f;
            if (lakeBasin > 0f)
            {
                int gx = Mathf.FloorToInt(x / 1024f), gz = Mathf.FloorToInt(z / 1024f);
                int hash = Hash(gx, gz, seed + 4729);
                float cx = gx * 1024f + 256f + hash % 481;
                float cz = gz * 1024f + 256f + (hash / 17) % 481;
                float lakeLevel = (Mathf.PerlinNoise(cx / 620f + offset, cz / 620f + 391f) - 0.38f) * 35f
                    + Mathf.PerlinNoise(cx / 140f + 81f, cz / 140f + offset) * 7f - 4f;
                // Flatten the entire wet basin before lowering its shallow bed. The lake
                // surface is level; a broad dry shore joins it back to surrounding hills.
                height = Mathf.Lerp(height, lakeLevel, lakeBasin);
            }
            height -= WaterNodeDepth(x, z, seed);
            int mx = Mathf.FloorToInt(x / 512f);
            int mz = Mathf.FloorToInt(z / 512f);
            if (TownMacro(mx, mz, seed))
            {
                float distance = TownDistance(x, z, seed);
                // Flat out to the suburbs, which reach to about 215 m from the town centre.
                float blend = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(215f, 252f, distance));
                float padHeight = mx == 0 && mz == -1 ? HomeGroundY : 3f + Hash(mx, mz, seed + 11) % 12;
                height = Mathf.Lerp(height, padHeight, blend);
            }
            return height;
        }

        /// <summary>Shallow water follows the same triangles as the collision terrain (maximum 42cm).</summary>
        public static float WaterDepthAt(float x, float z, int seed)
        {
            float x0 = Mathf.Floor(x / 16f) * 16f, z0 = Mathf.Floor(z / 16f) * 16f;
            float u = (x - x0) / 16f, v = (z - z0) / 16f;
            float a = WaterNodeDepth(x0, z0, seed), b = WaterNodeDepth(x0, z0 + 16f, seed);
            float c = WaterNodeDepth(x0 + 16f, z0 + 16f, seed), d = WaterNodeDepth(x0 + 16f, z0, seed);
            return v >= u ? a + (b - a) * v + (c - b) * u : a + (d - a) * u + (c - d) * v;
        }

        public static float WaterSurfaceAt(float x, float z, int seed) => WaterDepthAt(x, z, seed) > 0.015f
            ? HeightAt(x, z, seed) + WaterDepthAt(x, z, seed) : float.NaN;

        public static bool IsWater(float x, float z, int seed, float margin = 0f)
        {
            if (WaterDepthAt(x, z, seed) > 0.015f) return true;
            if (margin <= 0f) return false;
            return WaterDepthAt(x - margin, z, seed) > 0.015f || WaterDepthAt(x + margin, z, seed) > 0.015f
                || WaterDepthAt(x, z - margin, seed) > 0.015f || WaterDepthAt(x, z + margin, seed) > 0.015f;
        }

        /// <summary>Exact interpolation on the terrain's 16m triangles, for grounded content and seamless chunk borders.</summary>
        public static float HeightAt(float x, float z, int seed)
        {
            const float spacing = 16f;
            float x0 = Mathf.Floor(x / spacing) * spacing;
            float z0 = Mathf.Floor(z / spacing) * spacing;
            float u = (x - x0) / spacing;
            float v = (z - z0) / spacing;
            float a = RawHeight(x0, z0, seed);
            float b = RawHeight(x0, z0 + spacing, seed);
            float c = RawHeight(x0 + spacing, z0 + spacing, seed);
            float d = RawHeight(x0 + spacing, z0, seed);
            return v >= u ? a + (b - a) * v + (c - b) * u : a + (d - a) * u + (c - d) * v;
        }
    }

    [Serializable]
    public class ProceduralWorldAssets
    {
        public Material[] biomeMaterials;
        public Material atlasMaterial;
        public Material roadMaterial;
        public Material waterMaterial;
        public GameObject raiderPrefab;
        public SheltreSim.Items.ResourceDefinition expeditionParts;
        public Material structureMaterial;
        public GameObject[] trees, rocks, bushes, cars;
        public GameObject lootModel;
        public SheltreSim.Items.ResourceDefinition wood, stone, iron, berries, water, scrap, medicine;
        // Normal-world town dressing. Every one may be null: generation falls back to plain boxes or skips it.
        public GameObject[] houseRoofs, carsClean, rubble, scrapPiles;
        public GameObject houseWindow, houseWindowBroken, houseDoor, fenceWood, hedge, streetLamp, powerPole, mailbox, gardenShed, busStop, dumpster;
        public Material plasterMaterial;
        public Material[] houseCleanMaterials;
    }
}
