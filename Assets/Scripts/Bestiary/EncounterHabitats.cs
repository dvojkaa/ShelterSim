using System.Collections.Generic;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    public enum Habitat { None, Town, Suburb, Road, Wasteland, Frozen, Forest, ForestEdge, Wetlands, WetlandsWater, Highlands, RedDesert,
        Hospital, Factory, Fuel, Farm, Military, OldBunker }

    /// <summary>
    /// Ruined world only: small groups of one species chosen by place, replacing the old raider patrol.
    /// Each chunk draws from its own System.Random (never a shared stream), and placement is checked against the chunk's
    /// real colliders, which are themselves deterministic. Nothing within 200 m of the bunker.
    /// </summary>
    public static class EncounterHabitats
    {
        public const string RootName = "Encounters";

        static System.Random ChunkRandom(Vector2Int c, int seed) => new System.Random(WorldLayout.Hash(c.x, c.y, seed + 5521));

        /// <summary>Which habitat a point is, from the seed alone.</summary>
        public static Habitat HabitatAt(Vector2Int c, int seed, float x, float z)
        {
            if (ProceduralLandmarks.TryGetKind(c, seed, out int kind))
                switch (kind)
                {
                    case ProceduralLandmarks.KindHospital: return Habitat.Hospital;
                    case ProceduralLandmarks.KindFactory: return Habitat.Factory;
                    case ProceduralLandmarks.KindFuel: return Habitat.Fuel;
                    case ProceduralLandmarks.KindFarm: return Habitat.Farm;
                    case ProceduralLandmarks.KindMilitary: return Habitat.Military;
                    default: return Habitat.OldBunker;
                }
            if (WorldLayout.IsTownChunk(c, seed)) return Habitat.Town;
            if (WorldLayout.IsSuburbChunk(c, seed)) return Habitat.Suburb;
            if (WorldLayout.IsRoad(x, z, 14f) || WorldLayout.IsConnectorRoad(x, z, seed, 14f)) return Habitat.Road;
            int biome = WorldLayout.BiomeIndexAt(x, z, seed);
            switch (biome)
            {
                case 1:
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * .5f;
                        if (WorldLayout.BiomeIndexAt(x + Mathf.Cos(a) * 70f, z + Mathf.Sin(a) * 70f, seed) != 1) return Habitat.ForestEdge;
                    }
                    return Habitat.Forest;
                case 2: return Habitat.RedDesert;
                case 3: return Habitat.Frozen;
                case 4: return NearWater(x, z, seed, 25f) ? Habitat.WetlandsWater : Habitat.Wetlands;
                case 5: return Habitat.Highlands;
                default: return Habitat.Wasteland;
            }
        }

        public static bool NearWater(float x, float z, int seed, float range)
        {
            if (WorldLayout.IsWater(x, z, seed)) return true;
            for (float r = 6f; r <= range; r += 6f)
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * .25f;
                    if (WorldLayout.IsWater(x + Mathf.Cos(a) * r, z + Mathf.Sin(a) * r, seed)) return true;
                }
            return false;
        }

        /// <summary>The species that live in a habitat. A group is one species, picked from this list.</summary>
        public static SpeciesId[] Pool(Habitat h)
        {
            switch (h)
            {
                case Habitat.Town: return new[] { SpeciesId.Scavenger, SpeciesId.Wanderer };
                case Habitat.Factory: return new[] { SpeciesId.Scavenger, SpeciesId.Wanderer, SpeciesId.Plated };
                case Habitat.Fuel: case Habitat.ForestEdge: return new[] { SpeciesId.Runner };
                case Habitat.Hospital: case Habitat.Wetlands: return new[] { SpeciesId.Screamer };
                case Habitat.WetlandsWater: return new[] { SpeciesId.Mirelurker };
                case Habitat.Highlands: return new[] { SpeciesId.Plated, SpeciesId.IronBoar };
                case Habitat.RedDesert: return new[] { SpeciesId.IronBoar };
                case Habitat.Forest: case Habitat.Farm: return new[] { SpeciesId.Rootwalker };
                case Habitat.Military: return new[] { SpeciesId.Scavenger, SpeciesId.Plated };
                case Habitat.OldBunker: return new[] { SpeciesId.Scavenger, SpeciesId.Wanderer };
                case Habitat.Frozen: case Habitat.Wasteland: return new[] { SpeciesId.Wanderer, SpeciesId.Runner };
                default: return new[] { SpeciesId.Wanderer }; // suburbs, roads
            }
        }

        static int GroupSize(SpeciesId s, System.Random r)
        {
            switch (s)
            {
                case SpeciesId.Wanderer: return 2 + r.Next(2);
                case SpeciesId.Plated: case SpeciesId.Rootwalker: case SpeciesId.Screamer: return 1;
                default: return 1 + r.Next(2);
            }
        }

        /// <summary>Groups per chunk, from the chunk's own random. Roughly the old patrol density, a little more in towns.</summary>
        static int GroupCount(Vector2Int c, int seed, System.Random r)
        {
            double roll = r.NextDouble();
            if (ProceduralLandmarks.HasLandmark(c, seed)) return 1;
            if (WorldLayout.IsTownChunk(c, seed)) return roll < .45 ? 2 : roll < .8 ? 1 : 0;
            if (WorldLayout.IsSuburbChunk(c, seed)) return roll < .16 ? 1 : 0;
            return roll < .10 ? 1 : 0;
        }

        /// <summary>How many groups a chunk will try to place (the chunk's first draw). Seed only; camps have none.</summary>
        public static int PlannedGroups(Vector2Int c, int seed) => EnemyCamps.IsCamp(c, seed) ? 0 : GroupCount(c, seed, ChunkRandom(c, seed));

        public static void Build(Transform chunk, Vector2Int c, int seed)
        {
            System.Random r = ChunkRandom(c, seed);
            int groups = GroupCount(c, seed, r);
            if (groups == 0) return;
            bool landmark = ProceduralLandmarks.HasLandmark(c, seed);
            Transform root = null;
            float ox = c.x * 128f, oz = c.y * 128f;
            var placed = new List<Vector3>();
            for (int g = 0; g < groups; g++)
            {
                // Every draw for this group happens up front, so a rejected spot never shifts the next group.
                var spots = new Vector2[10];
                for (int i = 0; i < spots.Length; i++) spots[i] = new Vector2(ox + 14f + (float)r.NextDouble() * 100f, oz + 14f + (float)r.NextDouble() * 100f);
                double pick = r.NextDouble();
                var memberRandom = new System.Random(r.Next());
                foreach (Vector2 spot in spots)
                {
                    Vector3 center = new Vector3(spot.x, 0f, spot.y);
                    if (EncounterPlacement.NearBunker(center, EncounterPlacement.SafeRadius + 6f) || WorldLayout.IsHomePlot(spot.x, spot.y)) continue;
                    if (landmark && Mathf.Abs(spot.x - (ox + 64f)) < 28f && spot.y - (oz + 64f) < 24f && spot.y - (oz + 64f) > -60f) continue;
                    Habitat h = HabitatAt(c, seed, spot.x, spot.y);
                    SpeciesId[] pool = Pool(h);
                    SpeciesId species = pool[(int)(pick * pool.Length) % pool.Length];
                    bool aquatic = species == SpeciesId.Mirelurker;
                    if (WorldLayout.IsWater(spot.x, spot.y, seed, aquatic ? 1.5f : 3f)) continue;
                    if (root == null)
                    {
                        root = new GameObject(RootName).transform;
                        root.SetParent(chunk, false);
                    }
                    if (TryGroup(root, chunk, c, seed, g, h, species, center, memberRandom, placed)) break;
                }
            }
        }

        static bool TryGroup(Transform root, Transform chunk, Vector2Int c, int seed, int g, Habitat h, SpeciesId species, Vector3 center,
            System.Random r, List<Vector3> placed)
        {
            SpeciesSpec spec = SpeciesSpec.Of(species);
            int size = GroupSize(species, r);
            float yaw0 = (float)r.NextDouble() * 360f;
            var feetList = new List<Vector3>();
            for (int m = 0; m < size; m++)
            {
                float a = (yaw0 + m * 137f) * Mathf.Deg2Rad;
                float d = m == 0 ? 0f : 3.2f;
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    float x = center.x + Mathf.Cos(a) * (d + attempt * 1.5f), z = center.z + Mathf.Sin(a) * (d + attempt * 1.5f);
                    if (!EncounterPlacement.TryStand(x, z, spec.Radius, spec.Height, seed, out Vector3 feet)) continue;
                    if (EncounterPlacement.NearBunker(feet) || WorldLayout.Coordinate(feet) != c) continue;
                    if (TooClose(feet, placed, spec.Radius + 1.4f) || TooClose(feet, feetList, spec.Radius + 1.4f)) continue;
                    feetList.Add(feet);
                    break;
                }
                if (m == 0 && feetList.Count == 0) return false; // the leader's spot is blocked: try the next spot
            }
            var group = new GameObject("Group_" + g).transform;
            group.SetParent(root, false);
            group.position = feetList[0];
            group.gameObject.AddComponent<EncounterGroupInfo>().Set(h, Pool(h));
            for (int m = 0; m < feetList.Count; m++)
            {
                placed.Add(feetList[m]);
                EncounterPlacement.SpawnSlot(group, chunk, "Slot_" + g + "_" + m, species, feetList[m], yaw0 + m * 40f, c, seed, feetList[0], 0f);
            }
            return true;
        }

        static bool TooClose(Vector3 p, List<Vector3> others, float distance)
        {
            foreach (Vector3 o in others)
            {
                float dx = p.x - o.x, dz = p.z - o.z;
                if (dx * dx + dz * dz < distance * distance) return true;
            }
            return false;
        }
    }

    /// <summary>What a habitat group is, for probes and debugging.</summary>
    public sealed class EncounterGroupInfo : MonoBehaviour
    {
        public Habitat habitat;
        public SpeciesId[] pool;
        public void Set(Habitat h, SpeciesId[] p) { habitat = h; pool = p; }
    }
}
