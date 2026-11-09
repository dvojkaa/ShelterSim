using System.Collections.Generic;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Raids
{
    /// <summary>
    /// The hand-laid path through the home site, numbers from M20HomeSetup. There's no navmesh, and the lot has
    /// a fence with a gate on the south side (x 66..78, z -246) and a stair hut with its open end facing south.
    /// Heights are irrelevant: arrival is judged on the ground plane.
    /// </summary>
    public static class RaidRoute
    {
        public static readonly Vector3 StairHeadCenter = new Vector3(72f, 6f, -238f);
        public static readonly Vector3 GateOutside = new Vector3(72f, 6f, -249f);
        public static readonly Vector3 GateInside = new Vector3(72f, 6f, -243.5f);
        public static readonly Vector3 StairHead = new Vector3(72f, 6f, -239.5f);
        public static readonly Vector3 RampTop = new Vector3(72f, 6f, -237f);
        public static readonly Vector3 StairFoot = new Vector3(72f, 0.1f, -227f);
        public static readonly Vector3 DoorFront = new Vector3(72f, 0.1f, -225.2f);
        public static readonly Vector3 DoorBack = new Vector3(72f, 0.1f, -222.4f);

        /// <summary>Works for a feet or a body-centre position. Above the ramp means within head height of it, which rules out the yard around it.</summary>
        public static bool InShaft(Vector3 p)
        {
            if (p.x <= 70.2f || p.x >= 73.8f || p.z <= -238.2f || p.z >= -223.6f) return false;
            float floor = p.z >= -227.5f ? 0f : 6.02f * (-227.5f - p.z) / 10.5f;
            return p.y < floor + 2.6f;
        }

        public static bool InRoom(Vector3 p) => p.z >= -223.8f && p.y < 3.6f && p.x > 64f && p.x < 88f && p.z < -210f;
        public static bool Underground(Vector3 p) => InShaft(p) || InRoom(p);
        public static bool InLot(Vector3 p) => WorldLayout.IsHomePlot(p.x, p.z);

        /// <summary>From anywhere to just inside the bunker door.</summary>
        public static List<Vector3> Inward(Vector3 from)
        {
            var route = new List<Vector3>();
            if (InRoom(from)) return route;
            if (!InShaft(from))
            {
                if (!InLot(from))
                {
                    route.Add(GateOutside);
                    route.Add(GateInside);
                }
                else if (from.z > -238.5f)
                {
                    // North of the stair head: go round the hut, whose walls reach x = 72 +- 2.2.
                    route.Add(new Vector3(from.x < 72f ? 68.6f : 75.4f, 6f, -240f));
                }
                route.Add(StairHead);
                route.Add(RampTop);
            }
            else if (from.z < RampTop.z) route.Add(RampTop);
            foreach (Vector3 point in new[] { StairFoot, DoorFront, DoorBack })
                if (point.z > from.z + 0.2f) route.Add(point);
            return route;
        }

        /// <summary>From the room or shaft back out to the surface.</summary>
        public static List<Vector3> Outward(Vector3 from)
        {
            var route = new List<Vector3>();
            if (!Underground(from)) return route;
            foreach (Vector3 point in new[] { DoorBack, DoorFront, StairFoot, RampTop })
                if (point.z < from.z - 0.2f) route.Add(point);
            route.Add(StairHead);
            return route;
        }
    }
}
