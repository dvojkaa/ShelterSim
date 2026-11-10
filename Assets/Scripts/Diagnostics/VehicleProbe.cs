using System;
using System.Collections;
using System.Reflection;
using SheltreSim.Audio;
using SheltreSim.Building;
using SheltreSim.Crafting;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Player;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// Run the built player with <c>--vehicle-probe</c>. Places each vehicle through the real build path, mounts it, moves the
    /// rider several metres, dismounts, and checks after each step that the vehicle is still drawn, rides above the ground
    /// (not sunk into it) and that the seat camera is above the terrain. Playtest bug: "the vehicle is placed well, but once
    /// I get in I fall under the map".
    /// </summary>
    public sealed class VehicleProbe : MonoBehaviour
    {
        private int failures;
        private int stepsAtMount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--vehicle-probe") >= 0)
                new GameObject(nameof(VehicleProbe)).AddComponent<VehicleProbe>();
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

            string[] ids = { "piece_scout_buggy", "piece_armored_van", "piece_bicycle" };
            for (int i = 0; i < ids.Length; i++)
            {
                BuildPieceDefinition def = build.availablePieces.Find(p => p != null && p.id == ids[i]);
                if (def == null) { Fail(ids[i], "no build definition"); continue; }
                foreach (CraftingCost cost in def.craftingCost) if (cost.resource != null) inventory.Add(cost.resource.id, cost.amount * 2);
                // The first free spot of a few candidates in and around the yard (the vans need 5 x 6.5 m).
                GameObject vehicle = null;
                string reason = "no candidate spot";
                Vector3[] spots = { new Vector3(58f, 0f, -236f), new Vector3(70f, 0f, -250f), new Vector3(86f, 0f, -250f), new Vector3(86f, 0f, -236f), new Vector3(58f, 0f, -262f), new Vector3(75f, 0f, -262f) };
                foreach (Vector3 spot in spots)
                {
                    Vector3 origin = new Vector3(spot.x, player.transform.position.y, spot.z);
                    player.GetComponent<CharacterController>().enabled = false;
                    player.transform.position = new Vector3(origin.x, player.transform.position.y, origin.z - 5f);
                    player.GetComponent<CharacterController>().enabled = true;
                    yield return new WaitForSeconds(0.3f);
                    vehicle = build.DebugPlaceAt(ids[i], new Ray(origin + Vector3.up * 3f, Vector3.down), 0f, out reason);
                    if (vehicle != null) break;
                }
                if (vehicle == null) { Fail(ids[i], "could not place: " + reason); continue; }
                yield return null;
                float ground = GroundAt(vehicle.transform.position);
                Look(ids[i], "placed", vehicle, player, ground);

                IInteractable interactable = vehicle.GetComponent<IInteractable>();
                if (interactable == null) { Fail(ids[i], "no IInteractable on the placed vehicle"); continue; }
                stepsAtMount = PlayerAudio.StepsPlayed;
                interactable.Interact(player.gameObject);
                yield return new WaitForSeconds(1.2f);
                Look(ids[i], "mounted", vehicle, player, GroundAt(vehicle.transform.position));
                CheckAudio(ids[i], "mounted", player, ids[i] == "piece_bicycle" ? null : (ids[i] == "piece_armored_van" ? Sfx.MotorVan : Sfx.MotorBuggy));

                // Drive: move the rider forward a few metres and let the vehicle follow.
                CharacterController cc = player.GetComponent<CharacterController>();
                for (int step = 0; step < 6; step++)
                {
                    cc.enabled = false;
                    player.transform.position += player.transform.forward * 1.0f;
                    cc.enabled = true;
                    yield return null;
                }
                yield return new WaitForSeconds(0.3f);
                Look(ids[i], "driven 6 m", vehicle, player, GroundAt(vehicle.transform.position));
                CheckAudio(ids[i], "driven 6 m", player, ids[i] == "piece_bicycle" ? Sfx.BikeChain : (ids[i] == "piece_armored_van" ? Sfx.MotorVan : Sfx.MotorBuggy));

                MethodInfo dismount = null;
                foreach (MonoBehaviour behaviour in vehicle.GetComponents<MonoBehaviour>())
                {
                    foreach (MethodInfo m in behaviour.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                        if (m.Name == "Dismount") { dismount = m; break; }
                    if (dismount != null)
                    {
                        // Optional parameters (the buggy has Dismount(bool force = false)) need Type.Missing through reflection.
                        object[] args = new object[dismount.GetParameters().Length];
                        for (int a = 0; a < args.Length; a++) args[a] = Type.Missing;
                        dismount.Invoke(behaviour, BindingFlags.Default | BindingFlags.OptionalParamBinding, null, args, null);
                        break;
                    }
                }
                if (dismount == null) Fail(ids[i], "no Dismount method");
                yield return new WaitForSeconds(1.2f);
                Look(ids[i], "dismounted", vehicle, player, GroundAt(vehicle.transform.position));
            }

            Debug.Log(failures == 0 ? "[VehicleProbe] ALL CHECKS PASSED" : $"[VehicleProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        /// <summary>The right motor loop plays while riding (null = must be silent) and no footstep was played since mounting.</summary>
        private void CheckAudio(string id, string phase, FirstPersonController player, string expectedKey)
        {
            VehicleAudio audio = player.GetComponent<VehicleAudio>();
            if (audio == null) { Fail(id, phase + ": the player has no VehicleAudio"); return; }
            string playing = audio.PlayingKey;
            Debug.Log($"[VehicleProbe] {id} {phase} audio: playing={playing ?? "nothing"} expected={expectedKey ?? "nothing"}");
            if (playing != expectedKey) Fail(id, $"{phase}: vehicle audio plays '{playing ?? "nothing"}', expected '{expectedKey ?? "nothing"}'");
            if (PlayerAudio.StepsPlayed != stepsAtMount) Fail(id, $"{phase}: the player's body played {PlayerAudio.StepsPlayed - stepsAtMount} footstep(s)/landing(s) while riding");
        }

        private static float GroundAt(Vector3 position)
        {
            // Highest solid surface at or below a point above the vehicle, ignoring the vehicle itself.
            RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * 30f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<IInteractable>() != null) continue;
                if (hit.collider.GetComponentInParent<FirstPersonController>() != null) continue;
                if (hit.point.y > best && hit.point.y < position.y + 2f) best = hit.point.y;
            }
            return best;
        }

        private void Look(string id, string phase, GameObject vehicle, FirstPersonController player, float ground)
        {
            Renderer[] renderers = vehicle.GetComponentsInChildren<Renderer>();
            int visible = 0;
            Bounds bounds = new Bounds(vehicle.transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer r in renderers)
            {
                if (r.enabled && r.gameObject.activeInHierarchy) visible++;
                if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
            }
            Camera cam = player.GetComponentInChildren<Camera>();
            float camY = cam != null ? cam.transform.position.y : float.NaN;
            Debug.Log($"[VehicleProbe] {id} {phase}: ground={ground:F2} vehicleBottom={bounds.min.y:F2} top={bounds.max.y:F2} cameraY={camY:F2} visibleRenderers={visible}/{renderers.Length} root={vehicle.transform.position:F2}");
            if (visible == 0) Fail(id, phase + ": no visible renderer");
            if (bounds.min.y < ground - 0.25f) Fail(id, $"{phase}: vehicle is sunk into the ground (bottom {bounds.min.y:F2}, ground {ground:F2})");
            if (!(camY > ground + 0.4f)) Fail(id, $"{phase}: camera is not above the ground (camera {camY:F2}, ground {ground:F2})");
        }

        private void Fail(string id, string message)
        {
            failures++;
            Debug.LogError($"[VehicleProbe] FAIL {id}: {message}");
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(150f);
            Debug.LogError("[VehicleProbe] FAIL: timed out");
            Application.Quit(2);
        }
    }
}
