using UnityEngine;

namespace SheltreSim.Building
{
    /// <summary>Grid snapping math for build-mode piece placement.</summary>
    public static class BuildGrid
    {
        public const float CellSize = 2f;

        /// <summary>
        /// Snaps X/Z to the nearest grid cell. Y passes through unchanged — M2 placement is
        /// flat-ground-plane only; multi-floor/vertical snapping is a later milestone's concern.
        /// </summary>
        public static Vector3 SnapPosition(Vector3 worldPosition)
        {
            float snappedX = Mathf.Round(worldPosition.x / CellSize) * CellSize;
            float snappedZ = Mathf.Round(worldPosition.z / CellSize) * CellSize;
            return new Vector3(snappedX, worldPosition.y, snappedZ);
        }

        /// <summary>
        /// Snaps yaw to the nearest 90° increment. Pitch/roll are always 0 — building pieces
        /// are axis-aligned to the 4 cardinal directions only for M2.
        /// </summary>
        public static Quaternion SnapRotation(float yawDegrees)
        {
            float snappedYaw = Mathf.Round(yawDegrees / 90f) * 90f;
            return Quaternion.Euler(0f, snappedYaw, 0f);
        }
    }
}
