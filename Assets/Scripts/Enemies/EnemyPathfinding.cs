using System.Collections.Generic;
using SheltreSim.Bestiary;
using SheltreSim.Interaction;
using SheltreSim.Player;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Enemies
{
    /// <summary>
    /// Small, on-demand A* detours for enemies that have actually stalled. Local steering remains
    /// the cheap default. Each plan covers a bounded patch around the actor and is cached while it
    /// is followed, so normal chases do not scan the scene or allocate navigation data every frame.
    /// </summary>
    internal static class EnemyPathfinding
    {
        private const int Side = 19;
        private const int Half = Side / 2;
        private const float Cell = .9f;
        private const float PlanCooldown = 2f;
        private const float TargetRefreshDistance = 3f;
        private const float WaypointReach = .62f;
        private const int MaxNodes = Side * Side;

        private sealed class Route
        {
            public readonly List<Vector3> points = new List<Vector3>(Side);
            public int next;
            public Vector3 target;
            public float lastPlan = float.NegativeInfinity;
        }

        private static readonly Dictionary<EntityId, Route> routes = new Dictionary<EntityId, Route>();
        private static readonly Collider[] overlaps = new Collider[48];
        private static readonly bool[] blocked = new bool[MaxNodes];
        private static readonly float[] costs = new float[MaxNodes];
        private static readonly int[] previous = new int[MaxNodes];
        private static readonly bool[] closed = new bool[MaxNodes];
        private static readonly List<int> open = new List<int>(MaxNodes);
        internal static string LastBuildDiagnostic { get; private set; } = "not built";
        private static int routeBuildCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { routes.Clear(); LastBuildDiagnostic = "not built"; routeBuildCount = 0; }

        public static void Forget(EntityId entityId) => routes.Remove(entityId);

        internal static string Describe(EnemyAI actor)
        {
            if (actor == null || !routes.TryGetValue(actor.GetEntityId(), out Route route)) return $"none; builds={routeBuildCount}";
            Vector3 next = route.next < route.points.Count ? route.points[route.next] : Vector3.zero;
            return $"{route.next}/{route.points.Count}, target={route.target}, next={next}, builds={routeBuildCount}";
        }

        public static bool TryGetDirection(EnemyAI actor, Vector3 destination, float blockedSeconds, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (actor == null) return false;

            EntityId id = actor.GetEntityId();
            Vector3 position = actor.transform.position;
            destination.y = position.y;
            if (routes.TryGetValue(id, out Route route))
            {
                if ((destination - route.target).sqrMagnitude > TargetRefreshDistance * TargetRefreshDistance)
                {
                    routes.Remove(id);
                    route = null;
                }
                else if (Advance(route, position, out direction)) return true;
            }

            if (blockedSeconds < 1.05f || Time.time < (route?.lastPlan ?? float.NegativeInfinity) + PlanCooldown)
                return false;

            route ??= new Route();
            route.lastPlan = Time.time;
            route.target = destination;
            route.next = 0;
            route.points.Clear();
            routes[id] = route;
            routeBuildCount++;

            if (!BuildRoute(actor, destination, route.points)) return false;
            return Advance(route, position, out direction);
        }

        private static bool Advance(Route route, Vector3 position, out Vector3 direction)
        {
            while (route.next < route.points.Count && Flat(route.points[route.next] - position).sqrMagnitude <= WaypointReach * WaypointReach)
                route.next++;
            if (route.next >= route.points.Count)
            {
                direction = Vector3.zero;
                return false;
            }
            direction = Flat(route.points[route.next] - position).normalized;
            return direction.sqrMagnitude > .001f;
        }

        private static bool BuildRoute(EnemyAI actor, Vector3 destination, List<Vector3> points)
        {
            Transform transform = actor.transform;
            CharacterController body = actor.Body;
            float radius = Mathf.Max(.2f, body.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)));
            float height = Mathf.Max(radius * 2f, body.height * Mathf.Abs(transform.lossyScale.y));
            // Raid models scale their root so the transform origin sits above the CharacterController feet.
            // Grid heights must match the capsule's actual ground contact, not that visual/model origin.
            Vector3 start = actor.FeetPosition;
            Vector3 desired = Flat(destination - start);
            if (desired.sqrMagnitude < .01f) return false;
            Vector3 goal = start + desired.normalized * Mathf.Min(desired.magnitude, Half * Cell - .25f);
            float originX = start.x - Half * Cell;
            float originZ = start.z - Half * Cell;
            int startIndex = Index(Half, Half);
            int goalX = Mathf.Clamp(Mathf.RoundToInt((goal.x - originX) / Cell), 0, Side - 1);
            int goalZ = Mathf.Clamp(Mathf.RoundToInt((goal.z - originZ) / Cell), 0, Side - 1);
            int goalIndex = Index(goalX, goalZ);
            if (goalIndex == startIndex) return false;

            for (int i = 0; i < MaxNodes; i++)
            {
                blocked[i] = false;
                costs[i] = float.PositiveInfinity;
                previous[i] = -1;
                closed[i] = false;
            }
            open.Clear();
            for (int z = 0; z < Side; z++)
            for (int x = 0; x < Side; x++)
            {
                int index = Index(x, z);
                if (index == startIndex) continue;
                Vector3 sample = new Vector3(originX + x * Cell, start.y, originZ + z * Cell);
                blocked[index] = IsBlocked(actor, sample, radius, height, body.stepOffset);
            }
            blocked[startIndex] = false;
            int blockedCells = 0;
            for (int i = 0; i < MaxNodes; i++) if (blocked[i]) blockedCells++;
            LastBuildDiagnostic = $"blocked={blockedCells}, front={blocked[Index(Half, Half + 1)]}, r={radius:F2}, h={height:F2}, start={start}, goal={goal}";

            if (blocked[goalIndex])
            {
                int nearest = -1;
                float nearestDistance = float.PositiveInfinity;
                for (int z = Mathf.Max(0, goalZ - 3); z <= Mathf.Min(Side - 1, goalZ + 3); z++)
                for (int x = Mathf.Max(0, goalX - 3); x <= Mathf.Min(Side - 1, goalX + 3); x++)
                {
                    int candidate = Index(x, z);
                    if (!blocked[candidate] && candidate != startIndex)
                    {
                        float d = (new Vector2(x - goalX, z - goalZ)).sqrMagnitude;
                        if (d < nearestDistance) { nearestDistance = d; nearest = candidate; }
                    }
                }
                if (nearest < 0) return false;
                goalIndex = nearest;
            }

            costs[startIndex] = 0f;
            open.Add(startIndex);
            int reached = -1;
            while (open.Count > 0)
            {
                int bestSlot = 0;
                float bestScore = float.PositiveInfinity;
                for (int i = 0; i < open.Count; i++)
                {
                    int n = open[i];
                    float score = costs[n] + Heuristic(n, goalIndex);
                    if (score < bestScore) { bestScore = score; bestSlot = i; }
                }
                int current = open[bestSlot];
                open.RemoveAt(bestSlot);
                if (current == goalIndex) { reached = current; break; }
                if (closed[current]) continue;
                closed[current] = true;
                int cx = current % Side, cz = current / Side;
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int x = cx + dx, z = cz + dz;
                    if (x < 0 || x >= Side || z < 0 || z >= Side) continue;
                    int next = Index(x, z);
                    if (blocked[next] || closed[next]) continue;
                    if (dx != 0 && dz != 0 && (blocked[Index(cx + dx, cz)] || blocked[Index(cx, cz + dz)])) continue;
                    float candidate = costs[current] + (dx == 0 || dz == 0 ? 1f : 1.4142f);
                    if (candidate >= costs[next]) continue;
                    costs[next] = candidate;
                    previous[next] = current;
                    if (!open.Contains(next)) open.Add(next);
                }
            }
            if (reached < 0) return false;

            int cursor = reached;
            while (cursor != startIndex && cursor >= 0)
            {
                int x = cursor % Side, z = cursor / Side;
                points.Add(new Vector3(originX + x * Cell, start.y, originZ + z * Cell));
                cursor = previous[cursor];
            }
            points.Reverse();
            return points.Count > 0;
        }

        private static bool IsBlocked(EnemyAI actor, Vector3 feet, float radius, float height, float stepOffset)
        {
            float r = radius * .9f;
            float half = Mathf.Max(r, height * .5f);
            Vector3 bottom = feet + Vector3.up * Mathf.Max(r, half - r - stepOffset - .03f);
            Vector3 top = feet + Vector3.up * Mathf.Max(r, half - r - .03f);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, r, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider collider = overlaps[i];
                if (collider == null || collider.isTrigger || collider.transform == actor.transform || collider.transform.IsChildOf(actor.transform)) continue;
                if (collider.GetComponentInParent<EnemyAI>() != null || collider.GetComponentInParent<SpeciesActor>() != null) continue;
                if (collider.GetComponentInParent<FirstPersonController>() != null || collider.GetComponentInParent<WorldWildlife>() != null) continue;
                // Closed doors retain their dedicated approach/break behavior in EnemyAI.
                if (collider.GetComponentInParent<InteractableDoor>() != null) continue;
                // Terrain and walkable floor are below the capsule's feet and should not block a cell.
                if (collider.GetComponent<ProceduralTerrain>() != null) continue;
                if (collider.bounds.max.y <= feet.y + stepOffset + .05f) continue;
                return true;
            }
            return false;
        }

        private static float Heuristic(int a, int b)
        {
            int dx = Mathf.Abs(a % Side - b % Side), dz = Mathf.Abs(a / Side - b / Side);
            return Mathf.Max(dx, dz) + .4142f * Mathf.Min(dx, dz);
        }

        private static int Index(int x, int z) => z * Side + x;
        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
