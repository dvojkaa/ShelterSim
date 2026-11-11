using System;
using System.Collections;
using System.IO;
using SheltreSim.Building;
using SheltreSim.Crafting;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Player;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// <c>--seat-shots &lt;folder&gt;</c> (run WITH graphics): places and mounts the Scout Buggy and the Armored Van and saves the
    /// driver's view, to check that the windscreen/cabin does not block it. Checks nothing.
    /// </summary>
    public sealed class SeatShotProbe : MonoBehaviour
    {
        private string folder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "--seat-shots");
            if (i < 0) return;
            new GameObject("SeatShotProbe").AddComponent<SeatShotProbe>().folder = i + 1 < args.Length ? args[i + 1] : "seat_shots";
        }

        private IEnumerator Start()
        {
            yield return null; yield return null;
            Directory.CreateDirectory(folder);
            var player = FindAnyObjectByType<FirstPersonController>();
            var inventory = player.GetComponent<Inventory>();
            var build = player.GetComponentInChildren<BuildModeController>();
            SheltreSim.Core.HomeBase.Active.DebugStartSurvival();
            yield return new WaitForSeconds(1f);
            string[] ids = { "piece_scout_buggy", "piece_armored_van" };
            Vector3[] spots = { new Vector3(58f, 0f, -236f), new Vector3(70f, 0f, -250f), new Vector3(86f, 0f, -250f), new Vector3(86f, 0f, -236f), new Vector3(58f, 0f, -262f) };
            foreach (string id in ids)
            {
                BuildPieceDefinition def = build.availablePieces.Find(p => p != null && p.id == id);
                if (def == null) { Debug.LogError("[SeatShots] no definition " + id); continue; }
                foreach (CraftingCost cost in def.craftingCost) if (cost.resource != null) inventory.Add(cost.resource.id, cost.amount * 2);
                GameObject vehicle = null;
                foreach (Vector3 spot in spots)
                {
                    var controller = player.GetComponent<CharacterController>();
                    controller.enabled = false;
                    player.transform.position = new Vector3(spot.x, player.transform.position.y, spot.z - 5f);
                    controller.enabled = true;
                    yield return new WaitForSeconds(0.3f);
                    vehicle = build.DebugPlaceAt(id, new Ray(new Vector3(spot.x, player.transform.position.y, spot.z) + Vector3.up * 3f, Vector3.down), 0f, out _);
                    if (vehicle != null) break;
                }
                if (vehicle == null) { Debug.LogError("[SeatShots] could not place " + id); continue; }
                yield return null;
                vehicle.GetComponent<IInteractable>()?.Interact(player.gameObject);
                yield return new WaitForSeconds(3f);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, id + "_seat.png"));
                yield return new WaitForSeconds(.5f);
                vehicle.GetComponent<IInteractable>()?.Interact(player.gameObject); // dismount
                yield return new WaitForSeconds(1.5f);
            }
            Debug.Log("[SeatShots] done");
            Application.Quit(0);
        }
    }
}
