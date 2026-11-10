using System;
using System.Collections;
using System.Text;
using SheltreSim.Building;
using SheltreSim.Items;
using SheltreSim.Player;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// Run the built player with <c>--place-probe</c>: for turrets, the ceiling light and a control piece, runs the real
    /// build preview and placement from a fixed aim and reports whether a ghost exists and has visible renderers, whether
    /// it is placed, where its renderers end up relative to the ground and what the player is told. Playtest bug:
    /// "the turret does not appear when I try to build it, and the ceiling light lies on the floor".
    /// </summary>
    public sealed class PlaceProbe : MonoBehaviour
    {
        private int failures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--place-probe") >= 0)
                new GameObject(nameof(PlaceProbe)).AddComponent<PlaceProbe>();
        }

        private IEnumerator Start()
        {
            StartCoroutine(Watchdog());
            yield return null;
            yield return null;
            FirstPersonController player = FindAnyObjectByType<FirstPersonController>();
            Inventory inventory = player.GetComponent<Inventory>();
            BuildModeController build = player.GetComponentInChildren<BuildModeController>();
            SheltreSim.Core.HomeBase.Active.DebugStartSurvival();
            yield return new WaitForSeconds(1f);
            foreach (string id in new[] { "steel_bar", "metal_plate", "electric_motor", "circuit_board", "laser_diode", "capacitor", "coolant", "planks", "glass", "copper_wire", "wood", "nails" })
                inventory.Add(id, 200);
            // Open yard inside the gate, where the player spawns in survival.
            Vector3 origin = player.transform.position;
            Debug.Log("[PlaceProbe] player at " + origin);

            string[] ids = { "piece_turret_mg", "piece_turret_laser", "piece_light_ceiling" };
            for (int i = 0; i < ids.Length; i++)
            {
                // The ceiling light is aimed up at the bunker ceiling (a 3 m high room at y 0..3), everything else down at the yard.
                Ray ray = ids[i] == "piece_light_ceiling"
                    ? new Ray(new Vector3(72f, 1.0f, -217f), Vector3.up)
                    : new Ray(origin + new Vector3(2f + i * 3f, 3f, 4f), Vector3.down);
                yield return Check(build, ids[i], ray);
            }

            build.ExitBuildMode();
            Debug.Log(failures == 0 ? "[PlaceProbe] ALL CHECKS PASSED" : $"[PlaceProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private IEnumerator Check(BuildModeController build, string id, Ray ray)
        {
            bool valid = build.DebugPreview(id, ray, 0f, out string reason);
            yield return null;
            GameObject ghost = null;
            foreach (GameObject go in FindObjectsByType<GameObject>(FindObjectsInactive.Include))
                if (go.name.StartsWith("Ghost_")) { ghost = go; break; }
            Report(id, "preview", valid, reason, ghost);
            if (ghost == null) failures++;

            GameObject placed = build.DebugPlaceAt(id, ray, 0f, out string placeReason);
            yield return null;
            Report(id, "placed", placed != null, placeReason, placed);
            if (placed == null) failures++;
            if (placed != null)
            {
                Renderer[] renderers = placed.GetComponentsInChildren<Renderer>();
                bool anyVisible = false;
                foreach (Renderer r in renderers) anyVisible |= r.enabled && r.gameObject.activeInHierarchy;
                if (!anyVisible) { failures++; Debug.LogError($"[PlaceProbe] FAIL {id}: placed piece has no visible renderer"); }
            }
        }

        private void Report(string id, string phase, bool ok, string reason, GameObject go)
        {
            StringBuilder text = new StringBuilder($"[PlaceProbe] {id} {phase}: ok={ok} msg='{reason}'");
            if (go != null)
            {
                Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
                text.Append($" obj='{go.name}' pos={go.transform.position:F2} renderers={renderers.Length}");
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    int enabledCount = 0;
                    foreach (Renderer r in renderers) { b.Encapsulate(r.bounds); if (r.enabled && r.gameObject.activeInHierarchy) enabledCount++; }
                    text.Append($" enabled={enabledCount} boundsMin={b.min:F2} boundsMax={b.max:F2}");
                }
            }
            else text.Append(" obj=null");
            Debug.Log(text.ToString());
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(120f);
            Debug.LogError("[PlaceProbe] FAIL: timed out");
            Application.Quit(2);
        }
    }
}
