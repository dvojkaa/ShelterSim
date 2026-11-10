using System.Collections.Generic;
using System.Diagnostics;
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
        public int PendingChunkCount => pending.Count;
        public int PendingBuildingChunkCount => buildingChunks.Count;
        public int RememberedChunkCount => history.Count;
        public bool HasGroundAt(Vector3 position) => chunks.ContainsKey(WorldLayout.Coordinate(position));
        /// <summary>False: the normal world before the bombs, houses and trees only. True: loot, raiders, animals and wreckage.</summary>
        public bool Ruined { get; private set; }
        private readonly HashSet<Vector2Int> stale = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, GameObject> chunks = new Dictionary<Vector2Int, GameObject>();
        private readonly Dictionary<Vector2Int, ChunkBuildWork> buildingChunks = new Dictionary<Vector2Int, ChunkBuildWork>();
        private readonly Dictionary<Vector2Int, ProceduralWorldState.ChunkState> history = new Dictionary<Vector2Int, ProceduralWorldState.ChunkState>();
        private readonly List<Vector2Int> pending = new List<Vector2Int>();
        private readonly Queue<ChunkBuildWork> buildingQueue = new Queue<ChunkBuildWork>();
        private readonly List<ChunkBuildWork> buildingWorkScratch = new List<ChunkBuildWork>(81);
        private Transform player;
        private Vector2Int lastCenter = new Vector2Int(int.MinValue, int.MinValue);
        private bool initialized;
        private int generationSerial;
        // Set only by the opt-in worldgen diagnostic to split synchronous Generate cost by stage.
        internal Vector2Int? performanceTimingTarget;
        internal string lastPerformanceTiming;
        internal bool HasDeferredBuildingWork(Vector2Int coordinate) => buildingChunks.ContainsKey(coordinate);
        internal int PendingBuildingActionCount(Vector2Int coordinate) =>
            buildingChunks.TryGetValue(coordinate, out ChunkBuildWork work) ? work.actions.Count - work.nextAction : 0;

        private sealed class ChunkBuildWork
        {
            public Vector2Int coordinate;
            public GameObject chunk;
            public List<System.Action> actions;
            public int nextAction;
            public float totalMs, maxActionMs;
            public float terrainMs, roadsMs, waterwaysMs, contentMs, encountersMs;
        }

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
            int serialAtStart = generationSerial;
            ReplaceStale(center, 2);
            if (pending.Count > 0)
            {
                Vector2Int coordinate = pending[0];
                pending.RemoveAt(0);
                Generate(coordinate);
            }
            // Keep a chunk's synchronous setup and a deferred building from landing in the
            // same frame. Each Update spends its generation slot on one of those operations.
            if (serialAtStart == generationSerial) ProcessBuildingWork();
        }

        private void Plan(Vector2Int center)
        {
            lastCenter = center;
            int radius = Mathf.Clamp(loadRadius, 1, 4);
            foreach (var entry in chunks.ToArray())
            {
                if (Mathf.Abs(entry.Key.x - center.x) <= radius + 1 && Mathf.Abs(entry.Key.y - center.y) <= radius + 1) continue;
                Remember(entry.Key, entry.Value);
                CancelBuildingWork(entry.Key);
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
                    CancelBuildingWork(coordinate);
                    old.SetActive(false);
                    Destroy(old);
                }
            }
            if (!chunks.TryGetValue(coordinate, out var chunk)) chunk = Generate(coordinate);
            CompleteBuildingWork(coordinate);
            return chunk;
        }

        private GameObject Generate(Vector2Int coordinate)
        {
            if (chunks.TryGetValue(coordinate, out var existing)) return existing;
            generationSerial++;
            bool measure = performanceTimingTarget.HasValue && performanceTimingTarget.Value == coordinate;
            long generateStart = measure ? Stopwatch.GetTimestamp() : 0L;
            long stageStart = generateStart;
            float terrainMs = 0f, roadsMs = 0f, waterwaysMs = 0f, contentMs = 0f, encountersMs = 0f, stateMs = 0f;
            var chunk = new GameObject($"Chunk_{coordinate.x}_{coordinate.y}");
            chunk.transform.SetParent(transform, false);
            ProceduralTerrain.Build(chunk.transform, coordinate, Seed, assets.biomeMaterials);
            if (measure) { terrainMs = ElapsedMs(stageStart); stageStart = Stopwatch.GetTimestamp(); }
            BuildRoads(chunk.transform, coordinate);
            if (measure) { roadsMs = ElapsedMs(stageStart); stageStart = Stopwatch.GetTimestamp(); }
            WorldWaterways.Build(chunk.transform, coordinate, Seed, assets.waterMaterial, assets.roadMaterial);
            if (measure) { waterwaysMs = ElapsedMs(stageStart); stageStart = Stopwatch.GetTimestamp(); }
            bool landmark = ProceduralLandmarks.HasLandmark(coordinate, Seed);
            var deferredBuildings = new List<System.Action>();
            ProceduralContent.CapturePerformanceTimings = measure && !landmark;
            if (landmark) ProceduralLandmarks.Build(chunk.transform, coordinate, Seed, assets, Ruined);
            else ProceduralContent.Build(chunk.transform, coordinate, Seed, assets, Ruined, deferredBuildings);
            ProceduralContent.CapturePerformanceTimings = false;
            if (measure)
            {
                contentMs = ElapsedMs(stageStart);
                string stateTiming = deferredBuildings.Count > 0 ? "deferred" : stateMs.ToString("F2");
                lastPerformanceTiming = $"terrain={terrainMs:F2} roads={roadsMs:F2} waterways={waterwaysMs:F2} content={contentMs:F2} (streets={ProceduralContent.LastTownStreetMs:F2}, buildings={ProceduralContent.LastTownBuildingMs:F2}, combine={ProceduralContent.LastStaticCombineMs:F2}, furniture={ProceduralContent.LastFurnitureVisualMs:F2}) encounters={encountersMs:F2} state={stateTiming} ms";
                stageStart = Stopwatch.GetTimestamp();
            }
            if (Ruined)
            {
                if (!WorldLayout.IsTownChunk(coordinate, Seed) && !ProceduralLandmarks.HasLandmark(coordinate, Seed))
                    WorldWildlife.Build(chunk.transform, coordinate, Seed, assets);
                ChunkEncounters.Build(chunk.transform, coordinate, Seed, assets);
            }
            if (measure) { encountersMs = ElapsedMs(stageStart); stageStart = Stopwatch.GetTimestamp(); }
            chunks.Add(coordinate, chunk);
            stale.Remove(coordinate);
            if (deferredBuildings.Count > 0)
            {
                var work = new ChunkBuildWork
                {
                    coordinate = coordinate, chunk = chunk, actions = deferredBuildings,
                    terrainMs = terrainMs, roadsMs = roadsMs, waterwaysMs = waterwaysMs,
                    contentMs = contentMs, encountersMs = encountersMs
                };
                buildingChunks[coordinate] = work;
                buildingQueue.Enqueue(work);
            }
            else FinalizeChunk(coordinate, chunk, measure, generateStart, terrainMs, roadsMs,
                waterwaysMs, contentMs, encountersMs, ref stateMs);
            if (measure)
            {
                float generateWallMs = ElapsedMs(generateStart);
                lastPerformanceTiming += deferredBuildings.Count > 0
                    ? $"; town buildings queued={deferredBuildings.Count}; synchronous Generate() wall={generateWallMs:F2} ms"
                    : $"; Generate() wall={generateWallMs:F2} ms";
            }
            return chunk;
        }

        private void ProcessBuildingWork()
        {
            buildingWorkScratch.Clear();
            while (buildingQueue.Count > 0)
            {
                ChunkBuildWork work = buildingQueue.Dequeue();
                if (work != null && buildingChunks.TryGetValue(work.coordinate, out ChunkBuildWork current) && current == work &&
                    chunks.TryGetValue(work.coordinate, out GameObject chunk) && chunk == work.chunk)
                    buildingWorkScratch.Add(work);
            }
            if (buildingWorkScratch.Count == 0) return;

            Vector2Int center = WorldLayout.Coordinate(player.position);
            int selectedIndex = 0;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < buildingWorkScratch.Count; i++)
            {
                int distance = (buildingWorkScratch[i].coordinate - center).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                selectedIndex = i;
            }

            ChunkBuildWork selected = buildingWorkScratch[selectedIndex];
            for (int i = 0; i < buildingWorkScratch.Count; i++)
                if (i != selectedIndex) buildingQueue.Enqueue(buildingWorkScratch[i]);
            buildingWorkScratch.Clear();

            // A streamed chunk can reach its final deferred action as it is being
            // cancelled/requeued around a teleport. Always finalize a drained queue item.
            if (selected.nextAction >= selected.actions.Count)
            {
                buildingChunks.Remove(selected.coordinate);
                FinalizeChunk(selected.coordinate, selected.chunk, selected.coordinate == performanceTimingTarget,
                    0L, selected.terrainMs, selected.roadsMs, selected.waterwaysMs, selected.contentMs,
                    selected.encountersMs, ref stateMsScratch);
                return;
            }

            long started = Stopwatch.GetTimestamp();
            selected.actions[selected.nextAction++]();
            float elapsed = ElapsedMs(started);
            selected.totalMs += elapsed;
            selected.maxActionMs = Mathf.Max(selected.maxActionMs, elapsed);
            if (selected.nextAction < selected.actions.Count) buildingQueue.Enqueue(selected);
            else
            {
                buildingChunks.Remove(selected.coordinate);
                FinalizeChunk(selected.coordinate, selected.chunk, selected.coordinate == performanceTimingTarget,
                    0L, selected.terrainMs, selected.roadsMs, selected.waterwaysMs, selected.contentMs,
                    selected.encountersMs, ref stateMsScratch);
                if (selected.coordinate == performanceTimingTarget)
                    lastPerformanceTiming += $"; deferred building frames={selected.actions.Count} total={selected.totalMs:F2} max/frame={selected.maxActionMs:F2} ms";
            }
        }

        private float stateMsScratch;

        private void CompleteBuildingWork(Vector2Int coordinate)
        {
            if (!buildingChunks.TryGetValue(coordinate, out ChunkBuildWork work)) return;
            while (work.nextAction < work.actions.Count)
            {
                work.actions[work.nextAction++]();
            }
            buildingChunks.Remove(coordinate);
            FinalizeChunk(coordinate, work.chunk, false, 0L, 0f, 0f, 0f, 0f, 0f, ref stateMsScratch);
        }

        private void CancelBuildingWork(Vector2Int coordinate)
        {
            buildingChunks.Remove(coordinate);
        }

        private void FinalizeChunk(Vector2Int coordinate, GameObject chunk, bool measure, long generateStart,
            float terrainMs, float roadsMs, float waterwaysMs, float contentMs,
            float encountersMs, ref float stateMs)
        {
            long started = measure ? Stopwatch.GetTimestamp() : 0L;
            ProceduralWorldState.Initialize(chunk);
            if (history.TryGetValue(coordinate, out var state)) ProceduralWorldState.Apply(chunk, state);
            if (measure)
            {
                stateMs = ElapsedMs(started);
                lastPerformanceTiming += $"; state={stateMs:F2}; total stages={terrainMs + roadsMs + waterwaysMs + contentMs + encountersMs + stateMs:F2} ms";
            }
            ChunkBuilt?.Invoke(chunk.transform);
        }

        private static float ElapsedMs(long startTicks) =>
            (float)((Stopwatch.GetTimestamp() - startTicks) * 1000.0 / Stopwatch.Frequency);

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
                CancelBuildingWork(nearest);
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
            if (state.nodes.Count + state.crates.Count + state.encounters.Count + state.destroyedStructures.Count > 0) history[coordinate] = state;
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
            buildingChunks.Clear();
            buildingQueue.Clear();
            buildingWorkScratch.Clear();
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
            // Build the ground directly under the player before play continues, then let
            // Update stream the surrounding radius one chunk per frame through Plan().
            Generate(center);
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
