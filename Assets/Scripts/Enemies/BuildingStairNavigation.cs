using System.Collections.Generic;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Enemies
{
    /// <summary>Finds the next point on real generated stair ramps when an enemy and its target occupy different storeys.</summary>
    public static class BuildingStairNavigation
    {
        private sealed class Flight
        {
            public Vector3 low;
            public Vector3 high;
            public string name;
        }

        private sealed class Site
        {
            public BuildingInterior building;
            public Bounds bounds;
            public readonly List<Flight> flights = new List<Flight>();
        }

        private sealed class Transition
        {
            public BuildingInterior building;
            public bool ascending;
            public Vector3 endpoint, travelAxis, portal, crossing;
            public float clearDistance, lastUsed;
            public bool crossingDivider;
        }

        private static readonly List<Site> sites = new List<Site>();
        private static readonly List<EntityId> staleTransitions = new List<EntityId>();
        private static readonly Dictionary<EntityId, Transition> activeTransitions = new Dictionary<EntityId, Transition>();
        private static float nextRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            sites.Clear();
            activeTransitions.Clear();
            nextRefresh = 0f;
        }

        /// <summary>
        /// Returns the next ascending or descending ramp waypoint only when both points are inside the same generated
        /// building. The caller can keep using its existing steering and CharacterController collision code.
        /// </summary>
        public static bool TryGetNextWaypoint(EntityId actorId, Vector3 actor, Vector3 target, out Vector3 waypoint)
        {
            waypoint = default;
            float verticalDelta = target.y - actor.y;
            // A half-flight can leave the raider about 1.6–1.7 m below a player on the
            // next storey. A 1.75 m cutoff drops guidance right at that landing and leaves
            // the actor facing a solid divider; same-floor actors have nearly zero delta.
            if (Mathf.Abs(verticalDelta) < 1.25f) return false;

            RefreshSites();
            Site source = FindSite(actor);
            if (source == null || source != FindSite(target) || source.flights.Count == 0)
            {
                activeTransitions.Remove(actorId);
                return false;
            }

            bool ascending = verticalDelta > 0f;
            if (TryGetSwitchbackTransition(actorId, source, actor, ascending, out waypoint)) return true;
            float direction = ascending ? 1f : -1f;
            float remaining = Mathf.Abs(verticalDelta);
            float bestProgress = float.PositiveInfinity;
            float bestDistance = float.PositiveInfinity;
            bool found = false;

            foreach (Flight flight in source.flights)
            {
                for (int step = 1; step <= 4; step++)
                {
                    float t = step * 0.2f;
                    Vector3 point = Vector3.Lerp(flight.low, flight.high, ascending ? t : 1f - t);
                    float progress = (point.y - actor.y) * direction;
                    if (progress < 0.35f || progress >= remaining - 0.15f) continue;
                    float distance = FlatSqr(point - actor);
                    if (progress > bestProgress + 0.02f || (Mathf.Abs(progress - bestProgress) <= 0.02f && distance >= bestDistance)) continue;
                    bestProgress = progress;
                    bestDistance = distance;
                    waypoint = point;
                    found = true;
                }
            }
            return found;
        }

        private static void RefreshSites()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.5f;
            sites.Clear();
            staleTransitions.Clear();
            foreach (KeyValuePair<EntityId, Transition> pair in activeTransitions)
                if (Time.unscaledTime - pair.Value.lastUsed > 10f) staleTransitions.Add(pair.Key);
            foreach (EntityId id in staleTransitions) activeTransitions.Remove(id);
            foreach (BuildingInterior building in BuildingInterior.All)
            {
                if (building == null || !building.isActiveAndEnabled) continue;
                var site = new Site { building = building };
                bool hasBounds = false;
                foreach (Collider collider in building.GetComponentsInChildren<Collider>(true))
                {
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                    if (!hasBounds) { site.bounds = collider.bounds; hasBounds = true; }
                    else site.bounds.Encapsulate(collider.bounds);

                    if (!(collider is BoxCollider ramp) || ramp.name != "Ramp") continue;
                    Vector3 halfDepth = Vector3.forward * (ramp.size.z * 0.46f);
                    Vector3 top = Vector3.up * (ramp.size.y * 0.5f);
                    Vector3 a = ramp.transform.TransformPoint(ramp.center + top - halfDepth);
                    Vector3 b = ramp.transform.TransformPoint(ramp.center + top + halfDepth);
                    site.flights.Add(a.y <= b.y
                        ? new Flight { low = a, high = b, name = ramp.transform.parent != null ? ramp.transform.parent.name : string.Empty }
                        : new Flight { low = b, high = a, name = ramp.transform.parent != null ? ramp.transform.parent.name : string.Empty });
                }
                if (hasBounds && site.flights.Count > 0) sites.Add(site);
            }
        }

        /// <summary>
        /// The switchback flights are separated by a divider that ends at each landing edge.
        /// A direct diagonal from one flight to the next runs into that divider. Carry actors
        /// past the divider along their current flight's axis before asking them to cross to the
        /// next flight. The landing waypoint is ignored once reached, allowing normal ramp samples
        /// to take over on the next call.
        /// </summary>
        private static bool TryGetSwitchbackTransition(EntityId actorId, Site site, Vector3 actor, bool ascending, out Vector3 waypoint)
        {
            waypoint = default;
            if (activeTransitions.TryGetValue(actorId, out Transition active))
            {
                if (active.building != site.building || active.ascending != ascending || Time.unscaledTime - active.lastUsed > 10f)
                {
                    activeTransitions.Remove(actorId);
                }
                else
                {
                    active.lastUsed = Time.unscaledTime;
                    // A CharacterController may stop a few centimetres short of a portal against
                    // the divider's end. Advance once it is close enough to the safe portal, or
                    // the actor keeps pushing into that blocker forever instead of turning across.
                    if (!active.crossingDivider &&
                        (Vector3.Dot(actor - active.endpoint, active.travelAxis) >= active.clearDistance - 0.45f ||
                         FlatSqr(actor - active.portal) <= 0.45f * 0.45f))
                        active.crossingDivider = true;
                    waypoint = active.crossingDivider ? active.crossing : active.portal;
                    waypoint.y = active.endpoint.y + 0.45f;
                    if (active.crossingDivider && FlatSqr(actor - waypoint) <= 0.45f * 0.45f)
                    {
                        activeTransitions.Remove(actorId);
                        waypoint = default;
                        return false;
                    }
                    return true;
                }
            }

            Flight current = null;
            float closest = float.PositiveInfinity;
            float currentT = 0f;
            foreach (Flight flight in site.flights)
            {
                if (flight.name != "FlightA" && flight.name != "FlightB") continue;
                Vector3 segment = flight.high - flight.low;
                float t = Mathf.Clamp01(Vector3.Dot(actor - flight.low, segment) / Mathf.Max(segment.sqrMagnitude, 0.001f));
                float distance = (actor - Vector3.Lerp(flight.low, flight.high, t)).sqrMagnitude;
                if (distance >= closest) continue;
                closest = distance;
                current = flight;
                currentT = t;
            }
            if (current == null || closest > 1.6f * 1.6f) return false;

            Vector3 endpoint = ascending ? current.high : current.low;
            float endpointT = ascending ? currentT : 1f - currentT;
            if (endpointT < 0.78f || FlatSqr(actor - endpoint) > 1.8f * 1.8f) return false;

            Flight nextFlight = FindConnectedFlight(site, current, endpoint, ascending);
            if (nextFlight == null) return false;

            Vector3 along = current.high - current.low;
            along.y = 0f;
            if (along.sqrMagnitude < 0.01f) return false;
            along.Normalize();

            // A->B transitions are just beyond the far edge of the divider. B->next A
            // transitions pass its near edge; the mirrored rule handles descent too.
            float clearDistance = current.name == "FlightA" ? 0.85f : 3.2f;
            if (!ascending) clearDistance = current.name == "FlightA" ? 3.2f : 0.85f;
            Vector3 travelAxis = along * (ascending ? 1f : -1f);
            Vector3 portal = endpoint + travelAxis * clearDistance;
            Vector3 nextEndpoint = ascending ? nextFlight.low : nextFlight.high;
            Vector3 crossing = new Vector3(nextEndpoint.x, endpoint.y, portal.z);
            var transition = new Transition
            {
                building = site.building,
                ascending = ascending,
                endpoint = endpoint,
                travelAxis = travelAxis,
                portal = portal,
                crossing = crossing,
                clearDistance = clearDistance,
                lastUsed = Time.unscaledTime,
                crossingDivider = Vector3.Dot(actor - endpoint, travelAxis) >= clearDistance - 0.45f ||
                    FlatSqr(actor - portal) <= 0.45f * 0.45f
            };
            activeTransitions[actorId] = transition;
            waypoint = transition.crossingDivider ? crossing : portal;
            waypoint.y = endpoint.y + 0.45f;
            return true;
        }

        private static Flight FindConnectedFlight(Site site, Flight current, Vector3 endpoint, bool ascending)
        {
            string wanted = current.name == "FlightA" ? "FlightB" : "FlightA";

            Flight best = null;
            float bestScore = float.PositiveInfinity;
            foreach (Flight candidate in site.flights)
            {
                if (candidate == current || candidate.name != wanted) continue;
                Vector3 candidateEndpoint = ascending ? candidate.low : candidate.high;
                if (Mathf.Abs(candidateEndpoint.y - endpoint.y) > 0.35f) continue;
                float score = FlatSqr(candidateEndpoint - endpoint);
                if (score < bestScore) { bestScore = score; best = candidate; }
            }
            return best;
        }

        private static Site FindSite(Vector3 point)
        {
            Site best = null;
            float bestScore = float.PositiveInfinity;
            foreach (Site site in sites)
            {
                if (site.building == null || !site.building.gameObject.activeInHierarchy) continue;
                Bounds bounds = site.bounds;
                if (point.x < bounds.min.x - 0.6f || point.x > bounds.max.x + 0.6f ||
                    point.z < bounds.min.z - 0.6f || point.z > bounds.max.z + 0.6f ||
                    point.y < bounds.min.y - 0.6f || point.y > bounds.max.y + 1.2f) continue;
                if (!Physics.Raycast(point + Vector3.up * 0.25f, Vector3.down, out RaycastHit floor, 0.9f,
                        ~0, QueryTriggerInteraction.Ignore) || floor.collider.GetComponentInParent<BuildingInterior>() != site.building) continue;
                Vector3 closest = bounds.ClosestPoint(point);
                float score = FlatSqr(point - closest) + Mathf.Pow(Mathf.Max(0f, bounds.min.y - point.y), 2f);
                if (score < bestScore) { bestScore = score; best = site; }
            }
            return best;
        }

        private static float FlatSqr(Vector3 value)
        {
            value.y = 0f;
            return value.sqrMagnitude;
        }
    }
}
