using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>One radiation hot zone: rads per second at its centre, falling linearly to zero at its radius.</summary>
    public struct RadiationZone
    {
        public Vector2 centre;
        public float radius;
        public float peak;

        public float IntensityAt(float x, float z)
        {
            float distance = Vector2.Distance(centre, new Vector2(x, z));
            return distance >= radius ? 0f : peak * (1f - distance / radius);
        }
    }

    /// <summary>
    /// Where the fallout lies after the bombs. A pure function of the seed, like WorldLayout: it never builds a chunk,
    /// never touches a chunk's random, and gives the same answer in any visit order. Callers decide that it only
    /// applies in the ruined world and above ground.
    /// Every town took a hit somewhere in its downtown blocks; roughly one wild chunk in seven holds a smaller hot spot.
    /// </summary>
    public static class RadiationZones
    {
        public const float TownPeak = 2.2f, WildPeak = 1.2f;   // rads per second at the very centre
        public const float MacroSize = 512f;
        private const float HomeClearance = 150f;              // from the home plot to a zone's edge
        private static readonly Vector2 Home = new Vector2(72f, -225f);

        /// <summary>Rads per second at a point, 0 outside every zone. Zones are at most 45 m across, so neighbours suffice.</summary>
        public static float IntensityAt(float x, float z, int seed)
        {
            float total = 0f;
            int mx = Mathf.FloorToInt(x / MacroSize), mz = Mathf.FloorToInt(z / MacroSize);
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                    if (TownZone(mx + dx, mz + dz, seed, out RadiationZone zone)) total += zone.IntensityAt(x, z);
            int cx = Mathf.FloorToInt(x / WorldLayout.ChunkSize), cz = Mathf.FloorToInt(z / WorldLayout.ChunkSize);
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                    if (WildZone(new Vector2Int(cx + dx, cz + dz), seed, out RadiationZone zone)) total += zone.IntensityAt(x, z);
            return total;
        }

        /// <summary>The bomb site of the town in macro cell (mx, mz), if that cell is a town.</summary>
        public static bool TownZone(int mx, int mz, int seed, out RadiationZone zone)
        {
            zone = default;
            if (!WorldLayout.IsTownChunk(new Vector2Int(mx * 4 + 1, mz * 4 + 1), seed)) return false;
            int hash = WorldLayout.Hash(mx, mz, seed + 7717);
            Vector2 townCentre = new Vector2(mx * MacroSize + MacroSize * 0.5f, mz * MacroSize + MacroSize * 0.5f);
            float radius = 30f + hash % 16;                                      // 30..45 m
            Vector2 offset = new Vector2(hash % 181 - 90f, (hash / 181) % 181 - 90f);
            Vector2 centre = townCentre + offset;
            // The player's own town took its hit on the far side, never on the doorstep.
            if (Vector2.Distance(centre, Home) < radius + HomeClearance)
                centre = townCentre + (townCentre - Home).normalized * 60f;
            if (Vector2.Distance(centre, Home) < radius + HomeClearance) return false;
            zone = new RadiationZone { centre = centre, radius = radius, peak = TownPeak };
            return true;
        }

        /// <summary>A smaller hot spot in open country: one in seven wild chunks, none near home.</summary>
        public static bool WildZone(Vector2Int chunk, int seed, out RadiationZone zone)
        {
            zone = default;
            if (WorldLayout.IsTownChunk(chunk, seed) || WorldLayout.IsSuburbChunk(chunk, seed)) return false;
            int hash = WorldLayout.Hash(chunk.x, chunk.y, seed + 5531);
            if (hash % 7 != 0) return false;
            float size = WorldLayout.ChunkSize;
            Vector2 centre = new Vector2(chunk.x * size + 24f + (hash / 7) % 81, chunk.y * size + 24f + (hash / 567) % 81);
            float radius = 20f + (hash / 45927) % 16;                            // 20..35 m
            if (Vector2.Distance(centre, Home) < radius + HomeClearance) return false;
            zone = new RadiationZone { centre = centre, radius = radius, peak = WildPeak };
            return true;
        }
    }
}
