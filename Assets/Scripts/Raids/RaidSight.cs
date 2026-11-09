using SheltreSim.Enemies;
using UnityEngine;

namespace SheltreSim.Raids
{
    /// <summary>Line of sight shared by raid enemies and turrets. Triggers don't block. The nearest collider has to belong to the target.</summary>
    public static class RaidSight
    {
        public static bool Clear(Vector3 from, Vector3 to, Transform ignore, Transform target)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.001f) return true;
            Collider nearest = null;
            float distance = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(from, delta / length, length, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform t = hit.collider.transform;
                if (ignore != null && (t == ignore || t.IsChildOf(ignore))) continue;
                if (hit.distance < distance)
                {
                    nearest = hit.collider;
                    distance = hit.distance;
                }
            }
            return nearest == null || (target != null && (nearest.transform == target || nearest.transform.IsChildOf(target)));
        }

        /// <summary>The nearest live enemy within range that a turret could actually hit from this muzzle, or null.</summary>
        public static EnemyAI NearestTarget(Vector3 muzzle, float range, Transform ignore)
        {
            EnemyAI best = null;
            float bestDistance = range * range;
            foreach (EnemyAI enemy in EnemyAI.Live)
            {
                if (enemy == null || enemy.IsDead) continue;
                Vector3 center = enemy.CenterPosition;
                float d = (center - muzzle).sqrMagnitude;
                if (d > bestDistance) continue;
                if (!Clear(muzzle, center, ignore, enemy.transform)) continue;
                bestDistance = d;
                best = enemy;
            }
            return best;
        }
    }

    /// <summary>A short-lived line, pooled, for tracers. One material for all of them.</summary>
    public sealed class RaidTracer : MonoBehaviour
    {
        private static readonly System.Collections.Generic.Stack<RaidTracer> pool = new System.Collections.Generic.Stack<RaidTracer>();
        private static Material material;
        private LineRenderer line;
        private float until;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            pool.Clear();
            material = null;
        }

        public static Material LineMaterial()
        {
            if (material == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                material = new Material(shader != null ? shader : Shader.Find("Standard"));
            }
            return material;
        }

        public static void Show(Vector3 from, Vector3 to, Color color, float seconds)
        {
            RaidTracer tracer = null;
            while (pool.Count > 0 && tracer == null) tracer = pool.Pop();
            if (tracer == null)
            {
                var obj = new GameObject("RaidTracer");
                tracer = obj.AddComponent<RaidTracer>();
                tracer.line = obj.AddComponent<LineRenderer>();
                tracer.line.material = LineMaterial();
                tracer.line.useWorldSpace = true;
                tracer.line.positionCount = 2;
                tracer.line.startWidth = 0.035f;
                tracer.line.endWidth = 0.02f;
                tracer.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tracer.line.receiveShadows = false;
            }
            tracer.gameObject.SetActive(true);
            tracer.line.SetPosition(0, from);
            tracer.line.SetPosition(1, to);
            tracer.line.startColor = color;
            tracer.line.endColor = new Color(color.r, color.g, color.b, 0.15f);
            tracer.until = Time.time + seconds;
        }

        private void Update()
        {
            if (Time.time < until) return;
            gameObject.SetActive(false);
            pool.Push(this);
        }
    }
}
