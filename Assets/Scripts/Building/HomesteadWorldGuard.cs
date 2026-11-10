using System.Collections;
using System.Collections.Generic;
using SheltreSim.Items;
using SheltreSim.Saving;
using SheltreSim.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SheltreSim.Building
{
    /// <summary>
    /// Pieces are global scene objects, chunks are rebuilt from their seed. This removes what a rebuilt chunk puts inside a
    /// player's building: trees, rocks, bushes, scrap, rubble, cars, street furniture. Never terrain, roads, buildings or the bunker.
    /// Removing a generated object changes no seeded draw: its slot was already rolled.
    /// It also brings down what stood on a piece raiders destroyed, so a raid can't leave a floating roof. Creates itself.
    /// </summary>
    public sealed class HomesteadWorldGuard : MonoBehaviour
    {
        private const float VerticalMargin = 1.5f;
        private const float TreeCanopyMargin = .3f;
        private static HomesteadWorldGuard instance;
        private static bool hooked;
        private static readonly List<Bounds> footprints = new List<Bounds>();

        private struct CollapseSeed
        {
            public Vector3 position;
            public float bottom;

            public CollapseSeed(Vector3 position, float bottom)
            {
                this.position = position;
                this.bottom = bottom;
            }
        }

        private readonly Queue<CollapseSeed> collapseQueue = new Queue<CollapseSeed>();
        private readonly HashSet<PlacedPiece> collapsing = new HashSet<PlacedPiece>();
        private readonly List<PlacedPiece> collapseCandidates = new List<PlacedPiece>();
        private bool processingCollapse;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; hooked = false; footprints.Clear(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneLoaded += (scene, mode) => Ensure();
            }
            Ensure();
        }

        private static void Ensure()
        {
            if (instance == null) new GameObject("HomesteadWorldGuard").AddComponent<HomesteadWorldGuard>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        private void OnEnable()
        {
            ProceduralWorld.ChunkBuilt += OnChunkBuilt;
            StructureHealth.Destroyed += OnStructureDestroyed;
        }

        private void OnDisable()
        {
            ProceduralWorld.ChunkBuilt -= OnChunkBuilt;
            StructureHealth.Destroyed -= OnStructureDestroyed;
        }

        private void OnDestroy() { if (instance == this) instance = null; }

        private static void OnChunkBuilt(Transform chunk)
        {
            float size = WorldLayout.ChunkSize;
            if (!TryParseCoordinate(chunk.name, out Vector2Int coordinate)) return;
            Rect area = new Rect(coordinate.x * size - 4f, coordinate.y * size - 4f, size + 8f, size + 8f);
            footprints.Clear();
            foreach (PlacedPiece piece in PlacedPieceRegistry.All)
            {
                Vector3 position = piece.transform.position;
                if (!area.Contains(new Vector2(position.x, position.z))) continue;
                footprints.Add(HomesteadPlacement.BoundsOf(piece));
            }
            if (footprints.Count > 0) Sweep(chunk);
        }

        /// <summary>A piece just went in: nothing generated may stay or regrow inside it. Depleted nodes have no collider, so placement can't see them.</summary>
        public static int ClearFootprint(GameObject placed)
        {
            PlacedPiece piece = placed != null ? placed.GetComponent<PlacedPiece>() : null;
            ProceduralWorld world = ProceduralWorld.Active;
            if (piece == null || world == null) return 0;
            Bounds bounds = HomesteadPlacement.BoundsOf(piece);
            footprints.Clear();
            footprints.Add(bounds);
            Vector2Int from = WorldLayout.Coordinate(bounds.min - Vector3.one * 3f);
            Vector2Int to = WorldLayout.Coordinate(bounds.max + Vector3.one * 3f);
            int removed = 0;
            for (int x = from.x; x <= to.x; x++)
                for (int z = from.y; z <= to.y; z++)
                {
                    Transform chunk = world.transform.Find($"Chunk_{x}_{z}");
                    if (chunk != null) removed += Sweep(chunk);
                }
            return removed;
        }

        private static int Sweep(Transform chunk)
        {
            int removed = 0;
            foreach (ResourceNode node in chunk.GetComponentsInChildren<ResourceNode>(true))
                if (Overlaps(node.transform)) { Remove(node.gameObject); removed++; }

            Transform content = chunk.Find("Content");
            if (content == null) return removed;
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform child = content.GetChild(i);
                if (!child.gameObject.activeSelf && child.GetComponent<ResourceNode>() == null) continue;
                if (IsGeneratedProp(child.name))
                {
                    if (child.gameObject.activeSelf && Overlaps(child)) { Remove(child.gameObject); removed++; }
                }
                else if (child.name.StartsWith("House_"))
                {
                    // A house's parked car stands on the lot, outside its walls.
                    Transform car = child.Find("Car");
                    if (car != null && car.gameObject.activeSelf && Overlaps(car)) { Remove(car.gameObject); removed++; }
                }
            }
            return removed;
        }

        /// <summary>Names from ProceduralContent. Before the bombs trees are Node_ objects with no ResourceNode on them.</summary>
        private static bool IsGeneratedProp(string name) =>
            name.StartsWith("Node_") || name == "Rubble" || name.StartsWith("Car_") || name == "StreetLamp" || name == "PowerPole";

        private static bool Overlaps(Transform candidate)
        {
            Vector3 position = candidate.position;
            float radius = RadiusOf(candidate);
            foreach (Bounds bounds in footprints)
            {
                if (position.y < bounds.min.y - VerticalMargin || position.y > bounds.max.y + VerticalMargin) continue;
                float dx = Mathf.Max(bounds.min.x - position.x, 0f, position.x - bounds.max.x);
                float dz = Mathf.Max(bounds.min.z - position.z, 0f, position.z - bounds.max.z);
                if (dx * dx + dz * dz <= radius * radius) return true;
            }
            return false;
        }

        // Works on a depleted node too, whose collider is off, so reads shapes and not world bounds.
        private static float RadiusOf(Transform candidate)
        {
            Collider collider = candidate.GetComponent<Collider>();
            float scale = Mathf.Max(Mathf.Abs(candidate.lossyScale.x), Mathf.Abs(candidate.lossyScale.z));
            if (collider is CapsuleCollider capsule) return capsule.radius * scale + TreeCanopyMargin;
            if (collider is BoxCollider box) return Mathf.Max(box.size.x, box.size.z) * .5f * scale;
            return 1f;
        }

        private static void Remove(GameObject target)
        {
            target.SetActive(false);
            Destroy(target);
        }

        private static bool TryParseCoordinate(string name, out Vector2Int coordinate)
        {
            coordinate = default;
            string[] parts = name.Split('_');
            if (parts.Length != 3 || !int.TryParse(parts[1], out int x) || !int.TryParse(parts[2], out int z)) return false;
            coordinate = new Vector2Int(x, z);
            return true;
        }

        // ---------- A piece was destroyed ----------

        private void OnStructureDestroyed(StructureHealth destroyed)
        {
            PlacedPiece piece = destroyed != null ? destroyed.GetComponent<PlacedPiece>() : null;
            if (piece == null) return;
            collapseQueue.Enqueue(new CollapseSeed(piece.transform.position, HomesteadPlacement.BottomOf(piece)));
            if (!processingCollapse) StartCoroutine(ProcessCollapseQueue());
        }

        // Defer until the destroyed piece has left the registry. Nested collapses enqueue more seeds for this same
        // pass instead of recursively starting coroutines from StructureHealth.Destroyed.
        private IEnumerator ProcessCollapseQueue()
        {
            processingCollapse = true;
            yield return null;

            while (collapseQueue.Count > 0)
            {
                CollapseSeed seed = collapseQueue.Dequeue();
                CollectCollapseCandidates(seed);
                collapseCandidates.Sort((a, b) => CompareCollapseOrder(a, b, seed.position));

                for (int i = 0; i < collapseCandidates.Count; i++)
                {
                    PlacedPiece piece = collapseCandidates[i];
                    if (piece == null || !piece.isActiveAndEnabled || collapsing.Contains(piece)) continue;
                    Vector3 offset = piece.transform.position - seed.position;
                    if (offset.x * offset.x + offset.z * offset.z > 16f
                        || HomesteadPlacement.BottomOf(piece) < seed.bottom - .1f) continue;

                    // Earlier, lower supports are handled first. Re-check support immediately before destruction so
                    // a piece that still has an independent support is never removed by an unrelated collapse.
                    if (HomesteadPlacement.IsSupported(piece)) continue;
                    collapsing.Add(piece);
                    DestroyUnsupportedPiece(piece);
                }
            }

            collapsing.Clear();
            processingCollapse = false;
        }

        private void CollectCollapseCandidates(CollapseSeed seed)
        {
            collapseCandidates.Clear();
            foreach (PlacedPiece piece in PlacedPieceRegistry.All)
            {
                if (piece == null || collapsing.Contains(piece)) continue;
                Vector3 offset = piece.transform.position - seed.position;
                if (offset.x * offset.x + offset.z * offset.z <= 16f
                    && HomesteadPlacement.BottomOf(piece) >= seed.bottom - .1f)
                    collapseCandidates.Add(piece);
            }
        }

        private static int CompareCollapseOrder(PlacedPiece a, PlacedPiece b, Vector3 origin)
        {
            if (a == null) return b == null ? 0 : 1;
            if (b == null) return -1;
            int order = HomesteadPlacement.BottomOf(a).CompareTo(HomesteadPlacement.BottomOf(b));
            if (order != 0) return order;
            order = (a.transform.position - origin).sqrMagnitude.CompareTo((b.transform.position - origin).sqrMagnitude);
            if (order != 0) return order;
            order = a.transform.position.x.CompareTo(b.transform.position.x);
            if (order != 0) return order;
            order = a.transform.position.z.CompareTo(b.transform.position.z);
            if (order != 0) return order;
            return string.CompareOrdinal(a.pieceId, b.pieceId);
        }

        private void DestroyUnsupportedPiece(PlacedPiece piece)
        {
            StructureHealth health = piece.GetComponent<StructureHealth>();
            if (health != null && health.Health > 0f)
            {
                // A single final hit preserves the normal sound, message, loot and event path.
                health.TakeDamage(health.Health + 1f);
                return;
            }

            // A piece without usable health has no destruction event, so enqueue its footprint explicitly to keep
            // upper pieces from floating after the next support in a cascade disappears.
            collapseQueue.Enqueue(new CollapseSeed(piece.transform.position, HomesteadPlacement.BottomOf(piece)));
            piece.gameObject.SetActive(false);
            Destroy(piece.gameObject);
        }
    }
}
