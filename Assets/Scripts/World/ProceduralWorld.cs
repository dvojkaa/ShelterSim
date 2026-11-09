using System.Collections.Generic;
using System.Linq;
using SheltreSim.Player;
using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>Bounded resident world; deterministic chunks are rebuilt on demand and only changed resources persist.</summary>
    public sealed class ProceduralWorld : MonoBehaviour
    {
        public static ProceduralWorld Active { get; private set; }
        /// <summary>A chunk has just been generated and its saved state applied.</summary>
        public static event System.Action<Transform> ChunkBuilt;
        public int initialSeed = 20261108;
        [Range(1, 4)] public int loadRadius = 3;
        public ProceduralWorldAssets assets;
        public int Seed { get; private set; }
        public int LoadedChunkCount => chunks.Count;
        public int RememberedChunkCount => history.Count;
        public bool HasGroundAt(Vector3 position) => chunks.ContainsKey(WorldLayout.Coordinate(position));
        /// <summary>False: the normal world before the bombs, houses and trees only. True: loot, raiders, animals and wreckage.</summary>
        public bool Ruined { get; private set; }
        private readonly HashSet<Vector2Int> stale = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, GameObject> chunks = new Dictionary<Vector2Int, GameObject>();
        private readonly Dictionary<Vector2Int, ProceduralWorldState.ChunkState> history = new Dictionary<Vector2Int, ProceduralWorldState.ChunkState>();
        private readonly List<Vector2Int> pending = new List<Vector2Int>();
        private Transform player;
        private Vector2Int lastCenter = new Vector2Int(int.MinValue, int.MinValue);
        private bool initialized;

        private void Awake()
        {
            Active = this;
            Seed = initialSeed;
        }

        private void Start()
        {
            var controller = FindFirstObjectByType<FirstPersonController>();
            if (controller != null) player = controller.transform;
            initialized = true;
            EnsureNearPlayer();
        }

        private void Update()
        {
            if (player == null || assets == null || Time.timeScale <= 0f) return;
            Vector2Int center = WorldLayout.Coordinate(player.position);
            if (center != lastCenter) Plan(center);
            ReplaceStale(center, 2);
            if (pending.Count > 0)
            {
                Vector2Int coordinate = pending[0];
                pending.RemoveAt(0);
                Generate(coordinate);
            }
        }

        private void Plan(Vector2Int center)
        {
            lastCenter = center;
            int radius = Mathf.Clamp(loadRadius, 1, 4);
            foreach (var entry in chunks.ToArray())
            {
                if (Mathf.Abs(entry.Key.x - center.x) <= radius + 1 && Mathf.Abs(entry.Key.y - center.y) <= radius + 1) continue;
                Remember(entry.Key, entry.Value);
                entry.Value.SetActive(false);
                Destroy(entry.Value);
                chunks.Remove(entry.Key);
            }
            pending.Clear();
            for (int z = -radius; z <= radius; z++)
                for (int x = -radius; x <= radius; x++)
                {
                    var coordinate = center + new Vector2Int(x, z);
                    if (System.Math.Abs((long)coordinate.x) > WorldLayout.MaximumChunkCoordinate ||
                        System.Math.Abs((long)coordinate.y) > WorldLayout.MaximumChunkCoordinate || chunks.ContainsKey(coordinate)) continue;
                    pending.Add(coordinate);
                }
            pending.Sort((a, b) => (a - center).sqrMagnitude.CompareTo((b - center).sqrMagnitude));
        }

        public GameObject EnsureChunk(Vector2Int coordinate)
        {
            if (stale.Contains(coordinate))
            {
                stale.Remove(coordinate);
                if (chunks.TryGetValue(coordinate, out var old))
                {
                    chunks.Remove(coordinate);
                    old.SetActive(false);
                    Destroy(old);
                }
            }
            if (!chunks.TryGetValue(coordinate, out var chunk)) chunk = Generate(coordinate);
            return chunk;
        }

        private GameObject Generate(Vector2Int coordinate)
        {
            if (chunks.TryGetValue(coordinate, out var existing)) return existing;
            var chunk = new GameObject($"Chunk_{coordinate.x}_{coordinate.y}");
            chunk.transform.SetParent(transform, false);
            ProceduralTerrain.Build(chunk.transform, coordinate, Seed, assets.biomeMaterials);
            BuildRoads(chunk.transform, coordinate);
            WorldWaterways.Build(chunk.transform, coordinate, Seed, assets.waterMaterial, assets.roadMaterial);
            if (ProceduralLandmarks.HasLandmark(coordinate, Seed)) ProceduralLandmarks.Build(chunk.transform, coordinate, Seed, assets, Ruined);
            else ProceduralContent.Build(chunk.transform, coordinate, Seed, assets, Ruined);
            if (Ruined)
            {
                if (!WorldLayout.IsTownChunk(coordinate, Seed) && !ProceduralLandmarks.HasLandmark(coordinate, Seed))
                    WorldWildlife.Build(chunk.transform, coordinate, Seed, assets);
                ChunkEncounters.Build(chunk.transform, coordinate, Seed, assets);
            }
            ProceduralWorldState.Initialize(chunk);
            if (history.TryGetValue(coordinate, out var state)) ProceduralWorldState.Apply(chunk, state);
            chunks.Add(coordinate, chunk);
            stale.Remove(coordinate);
            ChunkBuilt?.Invoke(chunk.transform);
            return chunk;
        }

        /// <summary>The bombs fell. Near ground is replaced at once, under the flash, the rest over the next frames.</summary>
        public void SetRuined(bool ruined)
        {
            if (Ruined == ruined) return;
            Ruined = ruined;
            foreach (var coordinate in chunks.Keys) stale.Add(coordinate);
            if (player != null) ReplaceStale(WorldLayout.Coordinate(player.position), 9);
        }

        private void ReplaceStale(Vector2Int center, int budget)
        {
            while (budget-- > 0 && stale.Count > 0)
            {
                Vector2Int nearest = stale.OrderBy(c => (c - center).sqrMagnitude).First();
                stale.Remove(nearest);
                if (!chunks.TryGetValue(nearest, out var old)) continue;
                chunks.Remove(nearest);
                old.SetActive(false);
                Destroy(old);
                Generate(nearest);
            }
        }

        private void BuildRoads(Transform parent, Vector2Int coordinate)
        {
            float ox = coordinate.x * WorldLayout.ChunkSize;
            float oz = coordinate.y * WorldLayout.ChunkSize;
            // Each road belongs to the chunk on its positive side, avoiding duplicate strips.
            if (coordinate.x % 4 == 0) RoadStrip(parent, ox, oz, true);
            if (coordinate.y % 4 == 0) RoadStrip(parent, ox, oz, false);
            int mx=Mathf.FloorToInt(coordinate.x/4f), mz=Mathf.FloorToInt(coordinate.y/4f);
            bool town=WorldLayout.IsTownChunk(new Vector2Int(mx*4+1,mz*4+1),Seed);
            if(town && coordinate.x-mx*4==2)RoadStrip(parent,ox,oz,true);
            if(town && coordinate.y-mz*4==2)RoadStrip(parent,ox,oz,false);
        }

        private void RoadStrip(Transform parent, float ox, float oz, bool alongZ)
        {
            float minX = alongZ ? ox - 6f : ox;
            float maxX = alongZ ? ox + 6f : ox + 128f;
            float minZ = alongZ ? oz : oz - 6f;
            float maxZ = alongZ ? oz + 128f : oz + 6f;
            // Vertical highways own intersections; horizontal strips stop at their edges.
            if (!alongZ)
            {
                if (Mathf.RoundToInt(ox / 128f) % 4 == 0 || WorldLayout.IsConnectorRoad(ox,oz+8f,Seed)) minX += 6f;
                if ((Mathf.RoundToInt(ox / 128f) + 1) % 4 == 0 || WorldLayout.IsConnectorRoad(ox+128f,oz+8f,Seed)) maxX -= 6f;
            }
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (float z = Mathf.Floor(minZ / 16f) * 16f; z < maxZ; z += 16f)
                for (float x = Mathf.Floor(minX / 16f) * 16f; x < maxX; x += 16f)
                {
                    if (WorldLayout.IsHomePlot(x + 8f, z + 8f)) continue;
                    Vector2 a = new Vector2(x, z), b = new Vector2(x, z + 16f);
                    Vector2 c = new Vector2(x + 16f, z + 16f), d = new Vector2(x + 16f, z);
                    AddRoadTriangle(new List<Vector2> { a, b, c }, minX, maxX, minZ, maxZ, vertices, triangles);
                    AddRoadTriangle(new List<Vector2> { a, c, d }, minX, maxX, minZ, maxZ, vertices, triangles);
                }
            if (vertices.Count == 0) return;
            var mesh = new Mesh { name = "ConformingRoad" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.uv = vertices.Select(v => new Vector2(v.x / 6f, v.z / 6f)).ToArray();
            mesh.RecalculateNormals();
            var road = new GameObject("Road", typeof(MeshFilter), typeof(MeshRenderer), typeof(RuntimeWorldMesh));
            road.transform.SetParent(parent, false);
            road.GetComponent<MeshFilter>().sharedMesh = mesh;
            road.GetComponent<MeshRenderer>().sharedMaterial = assets.roadMaterial;
            road.GetComponent<RuntimeWorldMesh>().mesh = mesh;
        }

        private void AddRoadTriangle(List<Vector2> polygon, float minX, float maxX, float minZ, float maxZ,
            List<Vector3> vertices, List<int> triangles)
        {
            polygon = Clip(polygon, 0, minX, true);
            polygon = Clip(polygon, 0, maxX, false);
            polygon = Clip(polygon, 1, minZ, true);
            polygon = Clip(polygon, 1, maxZ, false);
            if (polygon.Count < 3) return;
            int first = vertices.Count;
            foreach (Vector2 point in polygon)
                vertices.Add(new Vector3(point.x, WorldLayout.HeightAt(point.x, point.y, Seed) + .035f, point.y));
            for (int i = 1; i < polygon.Count - 1; i++)
            {
                triangles.Add(first); triangles.Add(first + i); triangles.Add(first + i + 1);
            }
        }

        private static List<Vector2> Clip(List<Vector2> input, int axis, float boundary, bool keepGreater)
        {
            var output = new List<Vector2>();
            if (input.Count == 0) return output;
            Vector2 previous = input[input.Count - 1];
            bool wasInside = keepGreater ? previous[axis] >= boundary : previous[axis] <= boundary;
            foreach (Vector2 point in input)
            {
                bool inside = keepGreater ? point[axis] >= boundary : point[axis] <= boundary;
                if (inside != wasInside)
                {
                    float t = (boundary - previous[axis]) / (point[axis] - previous[axis]);
                    output.Add(Vector2.Lerp(previous, point, t));
                }
                if (inside) output.Add(point);
                previous = point; wasInside = inside;
            }
            return output;
        }

        private void Remember(Vector2Int coordinate, GameObject chunk)
        {
            var state = ProceduralWorldState.Capture(chunk, coordinate.x, coordinate.y);
            if (state.nodes.Count + state.crates.Count + state.encounters.Count > 0) history[coordinate] = state;
            else history.Remove(coordinate);
        }

        public List<ProceduralWorldState.ChunkState> CaptureWorld()
        {
            foreach (var entry in chunks) Remember(entry.Key, entry.Value);
            return history.OrderBy(entry => entry.Key.x).ThenBy(entry => entry.Key.y).Select(entry => entry.Value).ToList();
        }

        public void RestoreWorld(int seed, List<ProceduralWorldState.ChunkState> states, bool ruined)
        {
            Ruined = ruined;
            stale.Clear();
            foreach (var chunk in chunks.Values)
            {
                chunk.SetActive(false);
                Destroy(chunk);
            }
            chunks.Clear();
            history.Clear();
            pending.Clear();
            Seed = seed;
            if (states != null)
                foreach (var state in states) history[new Vector2Int(state.x, state.z)] = state;
            lastCenter = new Vector2Int(int.MinValue, int.MinValue);
            if (initialized) EnsureNearPlayer();
        }

        private void EnsureNearPlayer()
        {
            if (player == null || assets == null) return;
            Vector2Int center = WorldLayout.Coordinate(player.position);
            for (int z = -1; z <= 1; z++)
                for (int x = -1; x <= 1; x++) Generate(center + new Vector2Int(x, z));
            Plan(center);
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }
    }

    public sealed class RuntimeWorldMesh : MonoBehaviour
    {
        public Mesh mesh;
        private void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }
    }
}
