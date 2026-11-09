using System.Collections.Generic;
using SheltreSim.Items;
using SheltreSim.Saving;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Building
{
    /// <summary>The outcome of aiming a structural piece. A pose with valid == false is shown as a red preview, with the reason.</summary>
    public struct HomesteadPose
    {
        public bool hasPose, valid;
        public Vector3 position;
        public Quaternion rotation;
        public string reason;
    }

    /// <summary>
    /// Where a structural piece goes, and why it can't. All pieces keep their pivot at their footprint's centre.
    /// Cells are centred on even coordinates, so edges are on odd ones (see BuildGrid). Support is a yes or no rule, with no statics.
    /// </summary>
    public static class HomesteadPlacement
    {
        public const float MaxFoundationStep = 1f;
        public const float FoundationHeight = .4f;
        /// <summary>How far a foundation may sink into rising ground, leaving its top above the highest corner.</summary>
        private const float MaxEmbed = .35f;
        private const float MaxFloorTileStep = .3f;
        private const float MaxRampLip = .3f;
        private const float Skin = .01f;

        private static readonly Collider[] overlaps = new Collider[256];
        private static readonly RaycastHit[] rayHits = new RaycastHit[32];
        private static readonly List<PlacedPiece> nearby = new List<PlacedPiece>();

        public static HomesteadPose Evaluate(BuildPieceDefinition piece, Ray ray, float range, float yaw, int groundMask, int obstructionMask)
        {
            bool hasHit = Physics.Raycast(ray, out RaycastHit hit, range, groundMask, QueryTriggerInteraction.Ignore);
            CollectNearby(ray.origin, range + 6f);
            switch (piece.placementRule)
            {
                case BuildPlacementRule.Foundation: return Foundation(piece, hasHit, hit, groundMask, obstructionMask);
                case BuildPlacementRule.CellFloor: return CellPiece(piece, ray, range, yaw, hasHit, hit, true, groundMask, obstructionMask);
                case BuildPlacementRule.Roof: return CellPiece(piece, ray, range, yaw, hasHit, hit, false, groundMask, obstructionMask);
                case BuildPlacementRule.Wall: return EdgePiece(piece, yaw, hasHit, hit, false, groundMask, obstructionMask);
                case BuildPlacementRule.Perimeter: return EdgePiece(piece, yaw, hasHit, hit, true, groundMask, obstructionMask);
                case BuildPlacementRule.DoorInFrame: return Door(piece, ray, range, yaw, obstructionMask);
                case BuildPlacementRule.Access: return Access(piece, yaw, hasHit, hit, groundMask, obstructionMask);
                default: return new HomesteadPose { reason = "This piece has no structural placement rule." };
            }
        }

        // ---------- Rules ----------

        private static HomesteadPose Foundation(BuildPieceDefinition piece, bool hasHit, RaycastHit hit, int groundMask, int obstructionMask)
        {
            if (!hasHit) return new HomesteadPose { reason = "Aim at the ground within build range." };
            Vector3 cell = BuildGrid.SnapPosition(Nudged(hit));
            Quaternion rotation = Quaternion.identity;
            float half = piece.footprintSize.y / 2f;
            Vector3 fallback = new Vector3(cell.x, hit.point.y + half, cell.z);

            if (InWater(cell)) return Refused(fallback, rotation, "Foundations can't stand in water.");
            if (!SampleGround(cell, BuildGrid.CellSize / 2f, BuildGrid.CellSize / 2f, hit.point.y + 3f, groundMask, out float min, out float max))
                return Refused(fallback, rotation, "A foundation needs solid ground under all 4 corners.");
            if (max - min > MaxFoundationStep)
                return Refused(fallback, rotation, $"Too steep: the corners differ by {max - min:F1} m, the limit is {MaxFoundationStep:F0} m.");

            float bottom;
            if (NeighbourFoundationTop(cell, (min + max) / 2f, out float neighbourTop))
            {
                // Rooms need one level, so a new slab joins its neighbour's. The ground decides whether that's possible.
                bottom = neighbourTop - piece.footprintSize.y;
                if (neighbourTop < max)
                    return Refused(new Vector3(cell.x, bottom + half, cell.z), rotation,
                        "The ground rises above the next foundation: start on the uphill side and build down.");
                if (bottom - min > MaxFoundationStep)
                    return Refused(new Vector3(cell.x, bottom + half, cell.z), rotation,
                        $"Too high above the ground here: {bottom - min:F1} m, the limit is {MaxFoundationStep:F0} m.");
            }
            else
            {
                // Flat ground: the slab rests on it. On a slope it sinks into the high side, so its legs on the low side stay short.
                bottom = Mathf.Max(min, max - MaxEmbed);
            }

            Vector3 position = new Vector3(cell.x, bottom + half, cell.z);
            return Finish(position, rotation, piece.footprintSize / 2f - Vector3.one * Skin, obstructionMask);
        }

        /// <summary>Floor tiles and roofs: into a cell at the level of the walls around it. Floor tiles also lie on level ground.</summary>
        private static HomesteadPose CellPiece(BuildPieceDefinition piece, Ray ray, float range, float yaw, bool hasHit, RaycastHit hit, bool canLieOnGround, int groundMask, int obstructionMask)
        {
            Quaternion rotation = BuildGrid.SnapRotation(yaw);
            float half = piece.footprintSize.y / 2f;
            Vector3 clearance = new Vector3(piece.footprintSize.x / 2f - .02f, half - Skin, piece.footprintSize.z / 2f - .02f);

            if (FindSupportedCell(ray, range, hasHit, hit, out Vector3 cell, out float level))
                return Finish(new Vector3(cell.x, level + half, cell.z), rotation, clearance, obstructionMask);

            if (!hasHit) return new HomesteadPose { reason = canLieOnGround ? "Aim at level ground or between wall tops." : "Aim between wall tops within build range." };

            Vector3 aimed = BuildGrid.SnapPosition(Nudged(hit));
            if (!canLieOnGround)
                return Refused(new Vector3(aimed.x, hit.point.y + half, aimed.z), rotation, "Roofs rest on the top of walls, or next to another roof.");

            PlacedPiece under = hit.collider.GetComponentInParent<PlacedPiece>();
            if (under != null)
                return Refused(new Vector3(aimed.x, hit.point.y + half, aimed.z), rotation, HomesteadCatalog.IsWallBase(under.pieceId)
                    ? "There's already a floor here." : "Floor tiles need level ground, or walls to rest on.");
            if (InWater(aimed)) return Refused(new Vector3(aimed.x, hit.point.y + half, aimed.z), rotation, "Floor tiles can't lie in water.");
            if (!SampleGround(aimed, BuildGrid.CellSize / 2f, BuildGrid.CellSize / 2f, hit.point.y + 3f, groundMask, out float min, out float max) || max - min > MaxFloorTileStep)
                return Refused(new Vector3(aimed.x, hit.point.y + half, aimed.z), rotation, "Floor tiles need level ground, or walls to rest on. Use a foundation on a slope.");
            return Finish(new Vector3(aimed.x, max + half, aimed.z), rotation, clearance, obstructionMask);
        }

        /// <summary>Walls and perimeter pieces, on the edge nearest the aim. R turns to the other direction of edge, then the other way round.</summary>
        private static HomesteadPose EdgePiece(BuildPieceDefinition piece, float yaw, bool hasHit, RaycastHit hit, bool canStandOnGround, int groundMask, int obstructionMask)
        {
            if (!hasHit) return new HomesteadPose { reason = canStandOnGround ? "Aim at the ground within build range." : "Aim at a foundation, floor or wall within build range." };

            Vector3 aim = hit.point + Horizontal(hit.normal) * .02f;
            bool alongX = Mathf.Abs(aim.z - BuildGrid.NearestEdgeCoordinate(aim.z)) <= Mathf.Abs(aim.x - BuildGrid.NearestEdgeCoordinate(aim.x));
            int quarter = Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 90f) % 4;
            if (quarter % 2 == 1) alongX = !alongX;
            Quaternion rotation = Quaternion.Euler(0f, (alongX ? 0f : 90f) + (quarter >= 2 ? 180f : 0f), 0f);
            Vector3 mid = alongX
                ? new Vector3(BuildGrid.NearestCellCoordinate(aim.x), 0f, BuildGrid.NearestEdgeCoordinate(aim.z))
                : new Vector3(BuildGrid.NearestEdgeCoordinate(aim.x), 0f, BuildGrid.NearestCellCoordinate(aim.z));
            float half = piece.footprintSize.y / 2f;
            Vector3 preview = new Vector3(mid.x, aim.y + half, mid.z);

            bool supported;
            float baseY = 0f;
            if (canStandOnGround)
            {
                // A fence stands on a foundation only when aimed at one; otherwise on the ground.
                supported = EdgeBase(mid, alongX, aim.y - .5f, aim.y + .5f, aim.y, false, out baseY);
                if (!supported)
                {
                    if (InWater(mid)) return Refused(preview, rotation, $"{piece.displayName} can't stand in water.");
                    if (!SampleGround(mid, alongX ? .9f : .05f, alongX ? .05f : .9f, aim.y + 2f, groundMask, out float min, out float max))
                        return Refused(preview, rotation, $"{piece.displayName} needs solid ground under both ends.");
                    if (max - min > MaxFoundationStep)
                        return Refused(preview, rotation, $"Too steep here for {piece.displayName}.");
                    // A gate must clear the ground to swing; the rest follow the low end and sink into the high end.
                    baseY = piece.id == "piece_gate" ? max + .03f : min;
                    supported = true;
                }
            }
            else
            {
                supported = EdgeBase(mid, alongX, aim.y - 3.4f, aim.y + 1.5f, aim.y, true, out baseY);
                if (!supported && WorldLayout.UndergroundAmount(new Vector3(mid.x, hit.point.y + 1f, mid.z)) > .5f && hit.normal.y >= .7f && hit.collider.GetComponentInParent<PlacedPiece>() == null)
                {
                    // The bunker's own concrete floor counts as a foundation.
                    baseY = hit.point.y;
                    supported = true;
                }
            }
            if (!supported) return Refused(preview, rotation, "Walls need a foundation, floor or wall below.");

            // Walls meet at corners, so their ends legitimately share a post's worth of volume.
            float thickness = Mathf.Max(piece.footprintSize.z, .2f);
            Vector3 clearance = new Vector3(piece.footprintSize.x / 2f - thickness / 2f - .02f, half - Skin, piece.footprintSize.z / 2f - Skin);
            return Finish(new Vector3(mid.x, baseY + half, mid.z), rotation, clearance, obstructionMask);
        }

        private static HomesteadPose Door(BuildPieceDefinition piece, Ray ray, float range, float yaw, int obstructionMask)
        {
            PlacedPiece frame = null;
            float bestDistance = 1.3f;
            foreach (PlacedPiece candidate in nearby)
            {
                if (candidate.pieceId != "piece_door_frame") continue;
                Vector3 opening = candidate.transform.position + Vector3.down * .4f;
                Vector3 toOpening = opening - ray.origin;
                float along = Vector3.Dot(toOpening, ray.direction);
                if (along < 0f || along > range) continue;
                float distance = Vector3.Distance(ray.GetPoint(along), opening);
                if (distance < bestDistance) { bestDistance = distance; frame = candidate; }
            }
            if (frame == null) return new HomesteadPose { reason = "Doors go into a door frame: aim at the opening of one." };

            float half = piece.footprintSize.y / 2f;
            Vector3 position = frame.transform.position + Vector3.up * (half - FullHeight(frame) / 2f);
            bool flipped = Mathf.Repeat(yaw, 360f) >= 180f;
            Quaternion rotation = frame.transform.rotation * Quaternion.Euler(0f, flipped ? 180f : 0f, 0f);
            foreach (PlacedPiece other in nearby)
                if (other.GetComponent<HomesteadDoorPiece>() != null && (other.transform.position - position).sqrMagnitude < .25f)
                    return Refused(position, rotation, "This frame already has a door.");
            return Finish(position, rotation, piece.footprintSize / 2f - Vector3.one * Skin, obstructionMask);
        }

        /// <summary>Ramps rise towards local +Z. A ramp finishes level with a foundation ahead of it; stairs start from whatever they stand on.</summary>
        private static HomesteadPose Access(BuildPieceDefinition piece, float yaw, bool hasHit, RaycastHit hit, int groundMask, int obstructionMask)
        {
            if (!hasHit) return new HomesteadPose { reason = "Aim at the ground or a foundation within build range." };
            Quaternion rotation = BuildGrid.SnapRotation(yaw);
            Vector3 forward = rotation * Vector3.forward;
            bool alongZ = Mathf.Abs(forward.z) > .5f;
            Vector3 aim = Nudged(hit);
            float length = piece.footprintSize.z;
            // A piece two cells long is centred on the edge between them.
            bool twoCells = length > BuildGrid.CellSize + .1f;
            Vector3 centre = new Vector3(
                twoCells && !alongZ ? BuildGrid.NearestEdgeCoordinate(aim.x) : BuildGrid.NearestCellCoordinate(aim.x), 0f,
                twoCells && alongZ ? BuildGrid.NearestEdgeCoordinate(aim.z) : BuildGrid.NearestCellCoordinate(aim.z));
            Vector3 low = centre - forward * (length / 2f - .05f);
            Vector3 high = centre + forward * (length / 2f);
            float half = piece.footprintSize.y / 2f;
            Vector3 preview = new Vector3(centre.x, hit.point.y + half, centre.z);

            float groundAtLow = 0f;
            bool hasGroundAtLow = GroundAt(low, hit.point.y + 3f, groundMask, out groundAtLow);
            float baseY;
            PlacedPiece standingOn = hit.collider.GetComponentInParent<PlacedPiece>();
            if (standingOn != null && HomesteadCatalog.IsWallBase(standingOn.pieceId) && hit.normal.y > .7f)
            {
                baseY = hit.point.y;
            }
            else if (!twoCells && FloorTopNear(high + forward * .5f, hit.point.y - .8f, hit.point.y + 1.6f, out float top))
            {
                baseY = top - piece.footprintSize.y;
                if (!hasGroundAtLow || Mathf.Abs(groundAtLow - baseY) > MaxRampLip)
                    return Refused(new Vector3(centre.x, baseY + half, centre.z), rotation, "The ground at the low end of the ramp is too far from the foundation's level.");
            }
            else
            {
                if (!hasGroundAtLow) return Refused(preview, rotation, $"{piece.displayName} needs solid ground at its low end.");
                if (InWater(low)) return Refused(preview, rotation, $"{piece.displayName} can't stand in water.");
                baseY = groundAtLow;
            }

            Vector3 clearance = new Vector3(piece.footprintSize.x / 2f - .12f, half - Skin, length / 2f - .12f);
            return Finish(new Vector3(centre.x, baseY + half, centre.z), rotation, clearance, obstructionMask);
        }

        // ---------- Support ----------

        /// <summary>Whether a placed structural piece still has what the rules require under it. Other rules always answer yes.</summary>
        public static bool IsSupported(PlacedPiece piece, int groundMask = ~0)
        {
            Vector3 position = piece.transform.position;
            CollectNearby(position, 8f);
            float bottom = BottomOf(piece);
            switch (HomesteadCatalog.RuleFor(piece.pieceId))
            {
                case BuildPlacementRule.Wall:
                    if (WorldLayout.UndergroundAmount(position) > .5f) return true;
                    bool alongX = Mathf.Abs(piece.transform.right.x) > .5f;
                    return EdgeBase(new Vector3(position.x, 0f, position.z), alongX, bottom - .05f, bottom + .05f, bottom, true, out _, piece);
                case BuildPlacementRule.Roof:
                    return CellHasSupport(position, bottom, piece);
                case BuildPlacementRule.CellFloor:
                    if (CellHasSupport(position, bottom, piece)) return true;
                    return GroundAt(position, bottom + 1f, groundMask, out float ground) && Mathf.Abs(ground - bottom) < MaxFloorTileStep + .05f;
                case BuildPlacementRule.DoorInFrame:
                    foreach (PlacedPiece other in nearby)
                        if (other != piece && other.pieceId == "piece_door_frame" && Vector3.ProjectOnPlane(other.transform.position - position, Vector3.up).sqrMagnitude < .01f
                            && Mathf.Abs(BottomOf(other) - bottom) < .05f) return true;
                    return false;
                default: return true;
            }
        }

        /// <summary>The nearest surface a wall could stand on along this edge, within a height window.</summary>
        private static bool EdgeBase(Vector3 mid, bool alongX, float minY, float maxY, float aimY, bool wallsCount, out float baseY, PlacedPiece except = null)
        {
            baseY = 0f;
            bool found = false;
            float best = float.MaxValue;
            foreach (PlacedPiece piece in nearby)
            {
                if (piece == except) continue;
                bool isBase = HomesteadCatalog.IsWallBase(piece.pieceId);
                bool isWall = wallsCount && HomesteadCatalog.IsFullWall(piece.pieceId);
                if (!isBase && !isWall) continue;
                Vector3 offset = piece.transform.position - mid;
                float along = Mathf.Abs(alongX ? offset.x : offset.z);
                float across = Mathf.Abs(alongX ? offset.z : offset.x);
                if (isBase ? (along > .5f || across > 1.05f) : (along > .05f || across > .05f)) continue;
                if (isWall && Mathf.Abs(piece.transform.right.x) > .5f != alongX) continue;
                float top = TopOf(piece);
                if (top < minY || top > maxY) continue;
                float score = Mathf.Abs(top - aimY);
                if (score < best) { best = score; baseY = top; found = true; }
            }
            return found;
        }

        /// <summary>Tries the level each nearby wall top defines where the aim ray crosses it, nearest first, then the surface the aim hit.</summary>
        private static bool FindSupportedCell(Ray ray, float range, bool hasHit, RaycastHit hit, out Vector3 cell, out float level)
        {
            cell = Vector3.zero;
            level = 0f;
            List<float> levels = new List<float>();
            foreach (PlacedPiece piece in nearby)
            {
                float candidate;
                if (HomesteadCatalog.IsFullWall(piece.pieceId)) candidate = TopOf(piece);
                else if (HomesteadCatalog.RuleFor(piece.pieceId) == BuildPlacementRule.Roof || piece.pieceId == "floor_tile") candidate = BottomOf(piece);
                else continue;
                bool known = false;
                foreach (float existing in levels) if (Mathf.Abs(existing - candidate) < .03f) known = true;
                if (!known) levels.Add(candidate);
            }
            float bestT = float.MaxValue;
            foreach (float candidate in levels)
            {
                if (Mathf.Abs(ray.direction.y) < .001f) break;
                float t = (candidate - ray.origin.y) / ray.direction.y;
                if (t < .05f || t > range || t >= bestT) continue;
                if (hasHit && t > hit.distance + .3f) continue;
                Vector3 point = BuildGrid.SnapPosition(ray.GetPoint(t));
                if (!CellHasSupport(point, candidate, null)) continue;
                bestT = t; cell = point; level = candidate;
            }
            if (bestT < float.MaxValue) return true;
            if (!hasHit) return false;

            // Aiming at the side of a wall near its top: the cell on the viewer's side.
            Vector3 aimed = BuildGrid.SnapPosition(Nudged(hit));
            float bestGap = float.MaxValue;
            foreach (float candidate in levels)
            {
                float gap = candidate - hit.point.y;
                if (gap < -.5f || gap > 1.5f || Mathf.Abs(gap) >= bestGap || !CellHasSupport(aimed, candidate, null)) continue;
                bestGap = Mathf.Abs(gap); cell = aimed; level = candidate;
            }
            return bestGap < float.MaxValue;
        }

        private static bool CellHasSupport(Vector3 cell, float level, PlacedPiece except)
        {
            foreach (PlacedPiece piece in nearby)
            {
                if (piece == except) continue;
                float dx = piece.transform.position.x - cell.x, dz = piece.transform.position.z - cell.z;
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                if (HomesteadCatalog.IsFullWall(piece.pieceId))
                {
                    if (Mathf.Abs(distance - 1f) < .05f && Mathf.Abs(TopOf(piece) - level) < .03f) return true;
                }
                else if (HomesteadCatalog.RuleFor(piece.pieceId) == BuildPlacementRule.Roof || piece.pieceId == "floor_tile")
                {
                    if (Mathf.Abs(distance - 2f) < .05f && Mathf.Abs(BottomOf(piece) - level) < .03f) return true;
                }
            }
            return false;
        }

        private static bool NeighbourFoundationTop(Vector3 cell, float groundY, out float top)
        {
            top = 0f;
            float best = float.MaxValue;
            foreach (PlacedPiece piece in nearby)
            {
                if (piece.pieceId != "piece_foundation") continue;
                Vector3 offset = piece.transform.position - cell;
                if (Mathf.Abs(offset.x) > 2.1f || Mathf.Abs(offset.z) > 2.1f || (Mathf.Abs(offset.x) < .1f && Mathf.Abs(offset.z) < .1f)) continue;
                float candidate = TopOf(piece);
                float gap = Mathf.Abs(candidate - groundY);
                if (gap > 2.5f || gap >= best) continue;
                best = gap; top = candidate;
            }
            return best < float.MaxValue;
        }

        private static bool FloorTopNear(Vector3 point, float minY, float maxY, out float top)
        {
            top = 0f;
            foreach (PlacedPiece piece in nearby)
            {
                if (piece.pieceId != "piece_foundation" && piece.pieceId != "floor_tile") continue;
                Vector3 offset = piece.transform.position - point;
                if (Mathf.Abs(offset.x) >= 1f || Mathf.Abs(offset.z) >= 1f) continue;
                float candidate = TopOf(piece);
                if (candidate < minY || candidate > maxY) continue;
                top = candidate;
                return true;
            }
            return false;
        }

        // ---------- Measuring ----------

        public static Vector3 SizeOf(PlacedPiece piece)
        {
            HomesteadEntry entry = HomesteadCatalog.Find(piece.pieceId);
            if (entry != null) return entry.size;
            Bounds bounds = BoundsOf(piece);
            return bounds.size;
        }

        private static float FullHeight(PlacedPiece piece) => SizeOf(piece).y;
        public static float TopOf(PlacedPiece piece) => piece.transform.position.y + FullHeight(piece) / 2f;
        public static float BottomOf(PlacedPiece piece) => piece.transform.position.y - FullHeight(piece) / 2f;

        /// <summary>World bounds of a piece's enabled colliders, or its position if it has none.</summary>
        public static Bounds BoundsOf(PlacedPiece piece)
        {
            Bounds bounds = new Bounds(piece.transform.position, Vector3.zero);
            bool first = true;
            foreach (Collider collider in piece.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled) continue;
                if (first) { bounds = collider.bounds; first = false; }
                else bounds.Encapsulate(collider.bounds);
            }
            return bounds;
        }

        private static void CollectNearby(Vector3 origin, float radius)
        {
            nearby.Clear();
            float limit = radius * radius;
            foreach (PlacedPiece piece in PlacedPieceRegistry.All)
                if ((piece.transform.position - origin).sqrMagnitude <= limit) nearby.Add(piece);
        }

        private static bool InWater(Vector3 point)
        {
            ProceduralWorld world = ProceduralWorld.Active;
            return world != null && WorldLayout.IsWater(point.x, point.z, world.Seed);
        }

        /// <summary>Aiming at a side face means the cell in front of it, on the viewer's side.</summary>
        private static Vector3 Nudged(RaycastHit hit) => hit.point + Horizontal(hit.normal) * .3f;

        private static Vector3 Horizontal(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > .0001f ? v.normalized : Vector3.zero;
        }

        private static bool SampleGround(Vector3 centre, float halfX, float halfZ, float fromY, int mask, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            float insetX = Mathf.Max(0f, halfX - .02f), insetZ = Mathf.Max(0f, halfZ - .02f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 point = centre + new Vector3(sx * insetX, 0f, sz * insetZ);
                    if (!GroundAt(point, fromY, mask, out float height)) return false;
                    min = Mathf.Min(min, height);
                    max = Mathf.Max(max, height);
                }
            if (GroundAt(centre, fromY, mask, out float middle))
            {
                min = Mathf.Min(min, middle);
                max = Mathf.Max(max, middle);
            }
            return true;
        }

        /// <summary>The height of the world itself under a point: the first surface that isn't a build piece, a resource or a character.</summary>
        private static bool GroundAt(Vector3 point, float fromY, int mask, out float height)
        {
            height = 0f;
            int count = Physics.RaycastNonAlloc(new Vector3(point.x, fromY, point.z), Vector3.down, rayHits, 10f, mask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = rayHits[i];
                if (candidate.distance >= nearest || !IsGround(candidate.collider)) continue;
                nearest = candidate.distance;
                height = candidate.point.y;
            }
            return nearest < float.MaxValue;
        }

        private static bool IsGround(Collider collider) =>
            !(collider is CharacterController) && collider.GetComponentInParent<PlacedPiece>() == null && collider.GetComponentInParent<ResourceNode>() == null;

        private static HomesteadPose Refused(Vector3 position, Quaternion rotation, string reason) =>
            new HomesteadPose { hasPose = true, valid = false, position = position, rotation = rotation, reason = reason };

        private static HomesteadPose Finish(Vector3 position, Quaternion rotation, Vector3 clearance, int mask)
        {
            string blocked = Obstruction(position, clearance, rotation, mask);
            return new HomesteadPose { hasPose = true, valid = blocked == null, position = position, rotation = rotation, reason = blocked };
        }

        /// <summary>Terrain doesn't count: foundations sink into it on purpose, and a wall's edge may pass over rising ground.</summary>
        private static string Obstruction(Vector3 centre, Vector3 halfExtents, Quaternion rotation, int mask)
        {
            int count = Physics.OverlapBoxNonAlloc(centre, Vector3.Max(halfExtents, Vector3.one * Skin), overlaps, rotation, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider other = overlaps[i];
                if (other.GetComponent<ProceduralTerrain>() != null) continue;
                if (other is CharacterController)
                    return other.GetComponent<SheltreSim.Player.FirstPersonController>() != null ? "You are standing in the way." : "Someone is standing in the way.";
                PlacedPiece piece = other.GetComponentInParent<PlacedPiece>();
                if (piece != null)
                {
                    HomesteadEntry entry = HomesteadCatalog.Find(piece.pieceId);
                    return $"Blocked by {(entry != null ? entry.displayName : "another piece")}.";
                }
                if (other.GetComponentInParent<ResourceNode>() != null) return "Blocked by a tree or rock: clear it first.";
                return "Blocked: move the preview clear of obstacles.";
            }
            return null;
        }
    }
}
