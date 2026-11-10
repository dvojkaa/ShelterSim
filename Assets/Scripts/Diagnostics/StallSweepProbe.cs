using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// <c>--stall-sweep x y z nx ny nz</c> (defaults: the stall point of the town block in --raider-stair-probe). Generates the
    /// chunk, then with a raider-sized CharacterController at that point lists every collider around it, the capsule overlap, a
    /// capsule cast toward the next waypoint and the colliders the controller actually bumps into while it tries to move there.
    /// Read-only diagnostics: nothing is asserted.
    /// </summary>
    public sealed class StallSweepProbe : MonoBehaviour
    {
        private Vector3 feet = new Vector3(231.33f, 7.43f, -355.64f);
        private Vector3 next = new Vector3(232.85f, 7.94f, -355.64f);
        private readonly StringBuilder hits = new StringBuilder();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "--stall-sweep");
            if (i < 0) return;
            var probe = new GameObject("StallSweepProbe").AddComponent<StallSweepProbe>();
            float F(int k) => float.Parse(args[i + k], CultureInfo.InvariantCulture);
            if (args.Length > i + 6 && !args[i + 1].StartsWith("-"))
            {
                probe.feet = new Vector3(F(1), F(2), F(3));
                probe.next = new Vector3(F(4), F(5), F(6));
            }
        }

        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            ProceduralWorld world = ProceduralWorld.Active;
            world.SetRuined(true);
            Vector2Int chunk = WorldLayout.Coordinate(feet);
            world.EnsureChunk(chunk);
            yield return null;
            Physics.SyncTransforms();
            Debug.Log($"[StallSweep] chunk {chunk}, feet {feet}, next {next}");

            // Everything within 2 m of the capsule.
            foreach (Collider c in Physics.OverlapSphere(feet + Vector3.up * 1f, 2.2f, ~0, QueryTriggerInteraction.Ignore).OrderBy(c => Vector3.Distance(c.bounds.center, feet)))
                Debug.Log($"[StallSweep] near: {Path(c.transform)} ({c.GetType().Name}) bounds {c.bounds.min:F2}..{c.bounds.max:F2}");

            const float radius = .5f, height = 1.9f;
            Vector3 bottom = feet + Vector3.up * (radius + .02f);
            Vector3 top = feet + Vector3.up * (height - radius);
            var overlap = Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);
            Debug.Log($"[StallSweep] capsule overlap at the stall point: {overlap.Length} collider(s)" + (overlap.Length > 0 ? ": " + string.Join(", ", overlap.Select(c => Path(c.transform))) : ""));

            Vector3 direction = next - feet;
            float distance = direction.magnitude;
            direction.Normalize();
            foreach (RaycastHit hit in Physics.CapsuleCastAll(bottom, top, radius, direction, distance, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                Debug.Log($"[StallSweep] cast toward next: {Path(hit.collider.transform)} ({hit.collider.GetType().Name}) at {hit.distance:F2} m, normal {hit.normal:F2}");

            // A real CharacterController (the raider's size) trying to walk there.
            var body = new GameObject("StallBody");
            var controller = body.AddComponent<CharacterController>();
            controller.radius = radius; controller.height = height; controller.center = Vector3.up * (height * .5f); controller.skinWidth = .08f; controller.stepOffset = .3f; controller.slopeLimit = 45f;
            controller.enabled = false;
            body.transform.position = feet;
            controller.enabled = true;
            var recorder = body.AddComponent<HitRecorder>();
            recorder.sink = hits;
            Vector3 flat = new Vector3(next.x - feet.x, 0f, next.z - feet.z);
            for (int i = 0; i < 40; i++)
            {
                controller.Move(flat.normalized * 0.06f + Vector3.down * 0.05f);
                yield return null;
            }
            Debug.Log($"[StallSweep] controller walk: started {feet:F2}, ended {body.transform.position:F2}; contacts:\n{hits}");
            Application.Quit(0);
        }

        private static string Path(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        private sealed class HitRecorder : MonoBehaviour
        {
            public StringBuilder sink;
            private readonly System.Collections.Generic.HashSet<string> seen = new System.Collections.Generic.HashSet<string>();

            private void OnControllerColliderHit(ControllerColliderHit hit)
            {
                string key = Path(hit.collider.transform) + "|" + hit.normal.ToString("F1");
                if (seen.Add(key)) sink.AppendLine($"  hit {Path(hit.collider.transform)} ({hit.collider.GetType().Name}) normal {hit.normal:F2} point {hit.point:F2}");
            }
        }
    }
}
