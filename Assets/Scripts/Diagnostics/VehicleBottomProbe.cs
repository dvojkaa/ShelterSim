using System;
using System.Collections;
using System.Linq;
using SheltreSim.Building;
using SheltreSim.Crafting;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Player;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// <c>--vehicle-bottom</c>: places the Armored Van, mounts it, drives 6 m and lists the lowest renderers (name, lowest world y,
    /// parent chain, local rotation) before and after, to find which part of the van sinks below the ground. Diagnostics only.
    /// </summary>
    public sealed class VehicleBottomProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--vehicle-bottom") >= 0)
                new GameObject(nameof(VehicleBottomProbe)).AddComponent<VehicleBottomProbe>();
        }

        private IEnumerator Start()
        {
            yield return null; yield return null;
            FirstPersonController player = FindAnyObjectByType<FirstPersonController>();
            Inventory inventory = player.GetComponent<Inventory>();
            BuildModeController build = player.GetComponentInChildren<BuildModeController>();
            SheltreSim.Core.HomeBase.Active.DebugStartSurvival();
            yield return new WaitForSeconds(1f);
            BuildPieceDefinition def = build.availablePieces.Find(p => p != null && p.id == "piece_armored_van");
            foreach (CraftingCost cost in def.craftingCost) if (cost.resource != null) inventory.Add(cost.resource.id, cost.amount * 2);
            GameObject van = null;
            foreach (Vector3 spot in new[] { new Vector3(58f, 0f, -236f), new Vector3(70f, 0f, -250f), new Vector3(86f, 0f, -250f) })
            {
                Vector3 origin = new Vector3(spot.x, player.transform.position.y, spot.z);
                var cc = player.GetComponent<CharacterController>(); cc.enabled = false;
                player.transform.position = new Vector3(origin.x, player.transform.position.y, origin.z - 5f); cc.enabled = true;
                yield return new WaitForSeconds(0.3f);
                van = build.DebugPlaceAt("piece_armored_van", new Ray(origin + Vector3.up * 3f, Vector3.down), 0f, out _);
                if (van != null) break;
            }
            if (van == null) { Debug.LogError("[VehicleBottom] could not place the van"); Application.Quit(1); yield break; }
            yield return null;
            List("placed", van);
            ((IInteractable)van.GetComponent<IInteractable>()).Interact(player.gameObject);
            yield return new WaitForSeconds(2.2f);
            List("mounted", van);
            var controller = player.GetComponent<CharacterController>();
            for (int i = 0; i < 6; i++)
            {
                controller.enabled = false; player.transform.position += player.transform.forward; controller.enabled = true;
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            List("driven 6 m", van);
            Application.Quit(0);
        }

        private static void List(string phase, GameObject van)
        {
            Debug.Log($"[VehicleBottom] {phase}: root {van.transform.position:F2} rotation {van.transform.eulerAngles:F1}");
            foreach (Renderer r in van.GetComponentsInChildren<Renderer>().OrderBy(r => r.bounds.min.y).Take(6))
            {
                string path = r.name;
                for (Transform t = r.transform.parent; t != null && t != van.transform.parent; t = t.parent) path = t.name + "/" + path;
                Debug.Log($"[VehicleBottom]   {path}: minY {r.bounds.min.y:F2} center {r.bounds.center:F2} localRot {r.transform.localEulerAngles:F1} localPos {r.transform.localPosition:F2}");
            }
        }
    }
}
