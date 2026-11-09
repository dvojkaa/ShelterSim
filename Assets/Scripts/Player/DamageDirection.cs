using UnityEngine;

namespace SheltreSim.Player
{
    /// <summary>
    /// Where recent hits on the player came from. SurvivalStats.TakeDamage has no source, so the attackers
    /// report their own position right after they damage the player. PlayerStatusHUD draws one arc per entry.
    /// </summary>
    public static class DamageDirection
    {
        public const int Capacity = 4;
        public const float ShowSeconds = 1f;

        private static readonly Vector3[] sources = new Vector3[Capacity];
        private static readonly float[] times = new float[Capacity];
        private static int next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();

        /// <summary>Call after an attack from <paramref name="from"/> actually damaged the player.</summary>
        public static void Report(Vector3 from)
        {
            if (float.IsNaN(from.x) || float.IsNaN(from.y) || float.IsNaN(from.z)) return;
            sources[next] = from;
            times[next] = Time.time;
            next = (next + 1) % Capacity;
        }

        public static void Clear()
        {
            for (int i = 0; i < Capacity; i++) times[i] = float.NegativeInfinity;
            next = 0;
        }

        /// <summary>Entry i's source and how much of its display time is left (0 = expired, 1 = just hit).</summary>
        public static float Get(int index, out Vector3 from)
        {
            from = sources[index];
            float age = Time.time - times[index];
            if (age < 0f) return 0f;   // a scene reload restarted nothing, but be safe
            return Mathf.Clamp01(1f - age / ShowSeconds);
        }
    }
}
