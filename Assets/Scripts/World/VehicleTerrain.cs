using System;
using System.Collections.Generic;
using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>Heightmap-based support pose and speed scaling shared by all ridden vehicles.</summary>
    public static class VehicleTerrain
    {
        public const float MaximumBodySlope = 24f;
        public const float MaximumDriveSlope = 65f;
        public const float MountedStepOffset = .4f;
        public static float MinimumDriveSurfaceNormalY => Mathf.Cos(MaximumDriveSlope * Mathf.Deg2Rad);
        private const float NormalSampleRadius = 2f;
        private const float MaximumSupportDrop = 2.5f;
        private const float MaximumSupportRise = 0.3f;
        private static readonly RaycastHit[] supportHits = new RaycastHit[64];

        public static void CacheWheels(Transform root, out Transform[] wheels, out Quaternion[] restRotations)
        {
            var found = new List<Transform>();
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child != root && child.name.StartsWith("Wheel_", System.StringComparison.Ordinal)) found.Add(child);
            wheels = found.ToArray();
            restRotations = new Quaternion[wheels.Length];
            for (int i = 0; i < wheels.Length; i++) restRotations[i] = wheels[i].localRotation;
        }

        public static void SpinWheels(Transform[] wheels, Quaternion[] restRotations, ref float angleDegrees,
            float speed, float radius, float deltaTime)
        {
            if (wheels == null || restRotations == null || wheels.Length != restRotations.Length || wheels.Length == 0) return;
            angleDegrees = Mathf.Repeat(angleDegrees + speed / Mathf.Max(.1f, radius) * Mathf.Rad2Deg * deltaTime, 360f);
            Quaternion spin = Quaternion.AngleAxis(angleDegrees, Vector3.right);
            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i] != null) wheels[i].localRotation = spin * restRotations[i];
        }

        public static void SamplePose(Vector3 desiredPosition, Quaternion yaw, float rootHeight, float wheelBase,
            int seed, out Vector3 position, out Quaternion rotation, out float forwardGrade, out float slopeDegrees)
        {
            float x = desiredPosition.x;
            float z = desiredPosition.z;
            float terrainHeight = WorldLayout.HeightAt(x, z, seed);
            float centerHeight = SampleSupportHeight(x, z, desiredPosition.y, rootHeight, terrainHeight);
            Vector3 forward = yaw * Vector3.forward;
            float halfBase = Mathf.Max(.25f, wheelBase * .5f);
            float aheadX = x + forward.x * halfBase, aheadZ = z + forward.z * halfBase;
            float behindX = x - forward.x * halfBase, behindZ = z - forward.z * halfBase;
            float ahead = SampleSupportHeight(aheadX, aheadZ, desiredPosition.y, rootHeight, WorldLayout.HeightAt(aheadX, aheadZ, seed));
            float behind = SampleSupportHeight(behindX, behindZ, desiredPosition.y, rootHeight, WorldLayout.HeightAt(behindX, behindZ, seed));
            forwardGrade = (ahead - behind) / (2f * halfBase);

            float dx = (SupportAt(x + NormalSampleRadius, z, desiredPosition.y, rootHeight, seed) -
                SupportAt(x - NormalSampleRadius, z, desiredPosition.y, rootHeight, seed))
                / (2f * NormalSampleRadius);
            float dz = (SupportAt(x, z + NormalSampleRadius, desiredPosition.y, rootHeight, seed) -
                SupportAt(x, z - NormalSampleRadius, desiredPosition.y, rootHeight, seed))
                / (2f * NormalSampleRadius);
            Vector3 normal = new Vector3(-dx, 1f, -dz).normalized;
            slopeDegrees = Vector3.Angle(Vector3.up, normal);
            if (slopeDegrees > MaximumBodySlope)
                normal = Vector3.Slerp(Vector3.up, normal, MaximumBodySlope / slopeDegrees).normalized;

            Vector3 alignedForward = Vector3.ProjectOnPlane(forward, normal).normalized;
            if (alignedForward.sqrMagnitude < .001f) alignedForward = forward;
            rotation = Quaternion.LookRotation(alignedForward, normal);
            position = new Vector3(x, centerHeight + rootHeight, z);
        }

        private static float SupportAt(float x, float z, float expectedY, float rootHeight, int seed)
        {
            return SampleSupportHeight(x, z, expectedY, rootHeight, WorldLayout.HeightAt(x, z, seed));
        }

        /// <summary>Nearest stable walkable support under a mounted rider, falling back to the exact procedural heightfield.</summary>
        public static float RiderSupportHeight(float x, float z, float expectedY, int seed) =>
            SampleSupportHeight(x, z, expectedY, 0f, WorldLayout.HeightAt(x, z, seed));

        /// <summary>
        /// Prefer a nearby walkable collider under the rider (floors, foundations, ramps), and
        /// retain the procedural heightmap when no such surface is close. The expected rider
        /// feet height keeps a roof or another storey from being mistaken for the driving surface.
        /// </summary>
        private static float SampleSupportHeight(float x, float z, float expectedY, float rootHeight, float fallback)
        {
            float originY = Mathf.Max(expectedY, fallback) + Mathf.Max(2f, rootHeight + 1f);
            float maxDistance = originY - (expectedY - MaximumSupportDrop);
            int count = Physics.RaycastNonAlloc(new Vector3(x, originY, z), Vector3.down, supportHits,
                maxDistance, ~0, QueryTriggerInteraction.Ignore);
            float bestHeight = fallback;
            float bestDifference = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = supportHits[i];
                Collider collider = hit.collider;
                if (collider == null || hit.normal.y < .45f) continue;
                // Ignore characters, vehicles, and loose props: only stable walkable world/building geometry supports a vehicle.
                if (collider.GetComponentInParent<CharacterController>() != null ||
                    collider.GetComponentInParent<RideableScoutBuggy>() != null ||
                    collider.GetComponentInParent<RideableArmoredVan>() != null ||
                    collider.GetComponentInParent<RideableBicycle>() != null) continue;
                Rigidbody body = collider.attachedRigidbody;
                if (body != null && !body.isKinematic) continue;

                float difference = expectedY - hit.point.y;
                if (difference < -MaximumSupportRise || difference > MaximumSupportDrop || difference >= bestDifference) continue;
                bestDifference = difference;
                bestHeight = hit.point.y;
            }
            Array.Clear(supportHits, 0, count);
            return bestHeight;
        }

        /// <summary>Uphill grades reduce available speed; downhill grades give a modest speed gain.</summary>
        public static float GradeSpeedMultiplier(float forwardGrade, float forwardInput)
        {
            if (Mathf.Abs(forwardInput) < .05f) return 1f;
            float gradeInTravelDirection = forwardGrade * Mathf.Sign(forwardInput);
            if (gradeInTravelDirection > 0f)
                return Mathf.Clamp(1f - gradeInTravelDirection * 1.7f, .5f, 1f);
            return Mathf.Clamp(1f + -gradeInTravelDirection * .75f, 1f, 1.25f);
        }

        /// <summary>Terrain is support, not a forward obstacle; other upward-facing slopes are driveable through the grade limit.</summary>
        public static bool IsDriveSurface(RaycastHit hit)
        {
            if (hit.collider == null) return false;
            // Procedural terrain is a continuous heightfield. Its triangle faces must never
            // stop the vehicle hull sweep; SamplePose supplies the vertical support instead.
            if (hit.collider.GetComponentInParent<ProceduralTerrain>() != null) return true;
            return hit.normal.y >= MinimumDriveSurfaceNormalY;
        }

        public static int CurrentSeed
        {
            get
            {
                ProceduralWorld world = ProceduralWorld.Active;
                return world != null ? world.Seed : 20261108;
            }
        }
    }
}
