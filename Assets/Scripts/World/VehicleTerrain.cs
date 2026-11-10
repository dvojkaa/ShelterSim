using System.Collections.Generic;
using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>Heightmap-based support pose and speed scaling shared by all ridden vehicles.</summary>
    public static class VehicleTerrain
    {
        public const float MaximumBodySlope = 24f;
        private const float NormalSampleRadius = 2f;

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
            float centerHeight = WorldLayout.HeightAt(x, z, seed);
            Vector3 forward = yaw * Vector3.forward;
            float halfBase = Mathf.Max(.25f, wheelBase * .5f);
            float ahead = WorldLayout.HeightAt(x + forward.x * halfBase, z + forward.z * halfBase, seed);
            float behind = WorldLayout.HeightAt(x - forward.x * halfBase, z - forward.z * halfBase, seed);
            forwardGrade = (ahead - behind) / (2f * halfBase);

            float dx = (WorldLayout.HeightAt(x + NormalSampleRadius, z, seed) - WorldLayout.HeightAt(x - NormalSampleRadius, z, seed))
                / (2f * NormalSampleRadius);
            float dz = (WorldLayout.HeightAt(x, z + NormalSampleRadius, seed) - WorldLayout.HeightAt(x, z - NormalSampleRadius, seed))
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

        /// <summary>Uphill grades reduce available speed; downhill grades give a modest speed gain.</summary>
        public static float GradeSpeedMultiplier(float forwardGrade, float forwardInput)
        {
            if (Mathf.Abs(forwardInput) < .05f) return 1f;
            float gradeInTravelDirection = forwardGrade * Mathf.Sign(forwardInput);
            if (gradeInTravelDirection > 0f)
                return Mathf.Clamp(1f - gradeInTravelDirection * 1.7f, .5f, 1f);
            return Mathf.Clamp(1f + -gradeInTravelDirection * .75f, 1f, 1.25f);
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
