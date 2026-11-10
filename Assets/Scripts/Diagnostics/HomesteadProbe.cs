using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using SheltreSim.Building;
using SheltreSim.Crafting;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Saving;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>`--homestead-probe`: builds a 4 x 4 m house through the real build path at the home lot, with real physics. Run without -nographics.</summary>
    public sealed class HomesteadProbe : MonoBehaviour
    {
        private int failures;
        private BuildModeController build;
        private Inventory inventory;
        private FirstPersonController player;
        private float top;
        private int cx, cz;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--homestead-probe") >= 0)
                new GameObject("HomesteadProbe").AddComponent<HomesteadProbe>();
        }

        private IEnumerator Start()
        {
            StartCoroutine(Watchdog());
            yield return null;
            yield return null;
            player = FindFirstObjectByType<FirstPersonController>();
            inventory = player.GetComponent<Inventory>();
            build = player.GetComponentInChildren<BuildModeController>();
            SheltreSim.Core.HomeBase.Active.DebugStartSurvival();
            yield return new WaitForSeconds(1f);
            foreach (string id in new[] { "planks", "nails", "metal_plate", "steel_bar", "hinges", "rope", "wood" }) inventory.Add(id, 500);
            inventory.Add(HomesteadCatalog.RepairKitWood, 3);
            Check(FindSite(), $"Found a clear flat site at {cx},{cz}");
            Teleport(new Vector3(cx + 1, 6.1f, cz - 6));
            yield return null;

            IEnumerator[] steps = { Refusals(), House(), Door(), Shelter(), Tools(), KitPlacement(), SaveRoundTrip(), Palette() };
            foreach (IEnumerator step in steps) yield return Run(step);

            Time.timeScale = 1f;
            Debug.Log(failures == 0 ? "[HomesteadProbe] ALL CHECKS PASSED" : $"[HomesteadProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private IEnumerator Run(IEnumerator routine)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext()) yield break;
                    current = routine.Current;
                }
                catch (Exception error) { Check(false, "Exception: " + error); yield break; }
                yield return current;
            }
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(230f);
            Debug.LogError("[HomesteadProbe] FAIL: timed out");
            Application.Quit(1);
        }

        private bool FindSite()
        {
            for (int x = 50; x <= 88; x += 2)
                for (int z = -240; z <= -210; z += 2)
                {
                    Vector3 centre = new Vector3(x + 1, 8.5f, z + 1);
                    bool blocked = Physics.OverlapBox(centre, new Vector3(5f, 2.4f, 7f)).Any(c => c.GetComponent<ProceduralTerrain>() == null);
                    if (blocked || !Physics.Raycast(centre, Vector3.down, out RaycastHit hit, 5f) || Mathf.Abs(hit.point.y - 6f) > .05f) continue;
                    cx = x; cz = z;
                    return true;
                }
            return false;
        }

        private void Teleport(Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static Ray Down(float x, float y, float z) => new Ray(new Vector3(x, y + 3f, z), Vector3.down);

        private GameObject Place(string id, Ray ray, float yaw = 0f)
        {
            GameObject placed = build.DebugPlaceAt(id, ray, yaw, out string reason);
            Check(placed != null, placed != null ? $"Placed {id} at {placed.transform.position}" : $"Placed {id}: refused, {reason}");
            Physics.SyncTransforms();
            return placed;
        }

        private IEnumerator Refusals()
        {
            int planks = inventory.GetCount("planks");
            Check(build.DebugPlaceAt("wall_segment", Down(cx, 6f, cz), 0f, out string wallReason) == null && !string.IsNullOrEmpty(wallReason), "Wall on bare ground refused: " + wallReason);
            Check(build.DebugPlaceAt("piece_roof_flat", Down(cx, 6f, cz), 0f, out string roofReason) == null && !string.IsNullOrEmpty(roofReason), "Roof on nothing refused: " + roofReason);
            Check(build.DebugPlaceAt("piece_door_wood", Down(cx, 6f, cz), 0f, out string doorReason) == null && !string.IsNullOrEmpty(doorReason), "Door with no frame refused: " + doorReason);
            Check(inventory.GetCount("planks") == planks, "Refusals spent nothing");
            yield break;
        }

        private IEnumerator House()
        {
            foreach (int x in new[] { cx, cx + 2 })
                foreach (int z in new[] { cz, cz + 2 })
                {
                    GameObject foundation = Place("piece_foundation", Down(x, 6f, z));
                    if (foundation != null) top = foundation.transform.position.y + .2f;
                }
            Check(Mathf.Abs(top - 6.4f) < .03f, $"Foundation top on flat ground is 0.4 m up ({top:F2})");

            Place("piece_door_frame", Down(cx, top, cz - 1 + .05f));
            Place("wall_segment", Down(cx + 2, top, cz - 1 + .05f));
            Place("wall_segment", Down(cx, top, cz + 3 - .05f));
            Place("piece_wall_window", Down(cx + 2, top, cz + 3 - .05f));
            foreach (int z in new[] { cz, cz + 2 })
            {
                Place("wall_segment", Down(cx - 1 + .05f, top, z));
                Place("wall_segment", Down(cx + 3 - .05f, top, z));
            }
            float wallTop = top + 3f;
            Place("piece_roof_flat", Down(cx, wallTop, cz));
            Place("piece_roof_flat", Down(cx + 2, wallTop, cz));
            Place("piece_roof_slope", Down(cx, wallTop, cz + 2));
            Place("piece_roof_slope", Down(cx + 2, wallTop, cz + 2));

            foreach (var (x, z) in new[] { (cx, cz), (cx + 2, cz), (cx, cz + 2), (cx + 2, cz + 2) })
            {
                bool hit = Physics.Raycast(new Vector3(x, top + 1.5f, z), Vector3.up, out RaycastHit info, 5f);
                Check(hit && info.normal.y < -.25f, $"Roof over cell {x},{z} counts as shelter (normal {info.normal.y:F2})");
            }

            Vector3 windowLine = new Vector3(cx + 2, 0f, cz + 3);
            Check(Blocked(windowLine, .35f, 1.8f, top) && Blocked(windowLine, .35f, 1.8f, top + 1f), "Window can't be walked or jumped through");
            yield break;
        }

        private bool Blocked(Vector3 at, float radius, float height, float floor) =>
            Physics.CheckCapsule(new Vector3(at.x, floor + radius + .05f, at.z), new Vector3(at.x, floor + height - radius, at.z), radius, ~0, QueryTriggerInteraction.Ignore);

        private IEnumerator Door()
        {
            GameObject placed = Place("piece_door_wood", new Ray(new Vector3(cx, top + 1.1f, cz - 5), Vector3.forward));
            if (placed == null) yield break;
            var door = placed.GetComponent<HomesteadDoorPiece>();
            Vector3 line = new Vector3(cx, 0f, cz - 1);
            Check(Blocked(line, .35f, 1.8f, top), "Closed door blocks the player");
            door.Interact(player.gameObject);
            Physics.SyncTransforms();
            Check(door.IsOpen, "Door opens");
            Check(!Blocked(line, .35f, 1.8f, top), "Open door passes the player's capsule");
            Check(!Blocked(line, .58f, 2f, top), "Open door passes the raider's 1.16 m capsule");
            Teleport(new Vector3(cx, top + .1f, cz - 1));
            yield return null;
            door.Interact(player.gameObject);
            Check(door.IsOpen, "Door refuses to close on someone in the doorway");
            Teleport(new Vector3(cx + 1, 6.1f, cz - 6));
            yield return null;
            door.Interact(player.gameObject);
            Physics.SyncTransforms();
            Check(!door.IsOpen, "Door closes once clear");
        }

        private IEnumerator Shelter()
        {
            var atmosphere = FindFirstObjectByType<WorldAtmosphere>();
            Teleport(new Vector3(cx + 1, top + .1f, cz + 1));
            yield return new WaitForSeconds(2f);
            Check(atmosphere != null && atmosphere.IsSheltered, "Sheltered inside the house");
            Teleport(new Vector3(cx + 1, 6.1f, cz - 6));
            yield return new WaitForSeconds(2f);
            Check(atmosphere != null && !atmosphere.IsSheltered, "Not sheltered outside");
        }

        private IEnumerator Tools()
        {
            PlacedPiece wall = Physics.OverlapBox(new Vector3(cx + 2, top + 1.5f, cz - 1), Vector3.one * .05f)
                .Select(c => c.GetComponentInParent<PlacedPiece>()).FirstOrDefault(p => p != null);
            StructureHealth health = wall.GetComponent<StructureHealth>();
            Check(!build.DebugRepair(wall), "Repair refused with no hammer");
            Check(!HomesteadTools.CanDismantle(inventory, wall, out _), "Dismantle refused with no hammer");
            inventory.Add(HomesteadCatalog.Hammer, 1);

            int kits = inventory.GetCount(HomesteadCatalog.RepairKitWood);
            Check(!build.DebugRepair(wall) && inventory.GetCount(HomesteadCatalog.RepairKitWood) == kits, "Repair at full health spends no kit");
            health.TakeDamage(50f);
            Check(build.DebugRepair(wall) && inventory.GetCount(HomesteadCatalog.RepairKitWood) == kits - 1 && !health.IsDamaged, "Repair spends one kit and heals");

            Check(!build.DebugDismantle(wall) && wall != null, "Wall under a roof can't be dismantled");

            // A fence well clear of the house.
            Ray spot = Down(cx + 1 - 1, 6f, cz - 5 + .05f);
            int planks = inventory.GetCount("planks"), nails = inventory.GetCount("nails");
            for (int i = 0; i < 3; i++)
            {
                GameObject fence = Place("piece_fence", spot);
                if (fence == null) break;
                PlacedPiece piece = fence.GetComponent<PlacedPiece>();
                Check(!build.DebugDismantle(piece), "First X only asks");
                Check(build.DebugDismantle(piece), "Second X dismantles");
                yield return null;
            }
            Check(inventory.GetCount("planks") == planks && inventory.GetCount("nails") == nails, "Place and dismantle at once returns exactly the cost");

            GameObject old = Place("piece_fence", spot);
            planks = inventory.GetCount("planks");
            build.DebugForgetPlacementTimes();
            PlacedPiece oldPiece = old.GetComponent<PlacedPiece>();
            old.GetComponent<StructureHealth>().TakeDamage(35f);
            build.DebugDismantle(oldPiece);
            build.DebugDismantle(oldPiece);
            Check(inventory.GetCount("planks") == planks, $"Half-health old fence refunds 25% of 3 planks, rounded down: {inventory.GetCount("planks") - planks}");
            yield return null;
        }

        private IEnumerator KitPlacement()
        {
            BuildPieceDefinition definition = build.availablePieces.FirstOrDefault(p => p != null && p.id == "workbench");
            Check(definition != null && ItemCatalog.Def("kit_workbench") != null, "Workbench construction kit and build piece exist");
            if (definition == null || ItemCatalog.Def("kit_workbench") == null) yield break;

            Ray spot = default;
            bool found = false;
            for (int dx = 5; dx <= 21 && !found; dx += 2)
                for (int dz = 5; dz <= 21; dz += 2)
                    if (build.DebugPreview("workbench", Down(cx + dx, 6f, cz + dz), 0f, out _))
                    {
                        spot = Down(cx + dx, 6f, cz + dz);
                        found = true;
                        break;
                    }
            Check(found, "Found a clear workbench placement spot for the kit check");
            if (!found) yield break;

            var materialCounts = new System.Collections.Generic.Dictionary<string, int>();
            foreach (CraftingCost line in definition.craftingCost)
                if (line != null && line.resource != null)
                    materialCounts[line.resource.id] = CraftingSystem.CountAvailable(inventory, line.resource.id);
            inventory.SetCount("kit_workbench", 1);
            GameObject first = build.DebugPlaceAt("workbench", spot, 0f, out string firstReason);
            Check(first != null && inventory.GetCount("kit_workbench") == 0,
                first != null ? "Placement consumes one kit" : "Kit placement succeeds: " + firstReason);
            if (first == null) yield break;
            PlacedPiece firstTag = first.GetComponent<PlacedPiece>();
            Check(firstTag != null && firstTag.wasPlacedFromKit, "Placed piece records kit origin");
            bool materialsUntouched = true;
            foreach (var line in materialCounts)
                materialsUntouched &= CraftingSystem.CountAvailable(inventory, line.Key) == line.Value;
            Check(materialsUntouched, "Kit placement leaves recipe materials untouched");
            Check(firstTag != null && HomesteadTools.DescribeRefund(definition, firstTag, true).Contains("Construction Kit"),
                "A kit-built piece advertises a kit refund inside the 30-second window");

            bool firstConfirm = build.DebugDismantle(firstTag);
            bool secondConfirm = build.DebugDismantle(firstTag);
            Check(!firstConfirm && secondConfirm, "Immediate dismantle confirms and removes the kit-built piece");
            Check(inventory.GetCount("kit_workbench") == 1, "Immediate kit refund restores exactly one kit");
            yield return null;

            GameObject second = build.DebugPlaceAt("workbench", spot, 0f, out string secondReason);
            Check(second != null && inventory.GetCount("kit_workbench") == 0 && second.GetComponent<PlacedPiece>().wasPlacedFromKit,
                second != null ? "Second placement consumes the refunded kit and is saveable" : "Second kit placement: " + secondReason);
            build.ExitBuildMode();
        }

        private static string Signature() => string.Join("|", PlacedPieceRegistry.All
            .Select(p => $"{p.pieceId}@{p.transform.position.x:F2},{p.transform.position.y:F2},{p.transform.position.z:F2}:{(p.GetComponent<HomesteadDoorPiece>()?.IsOpen ?? false)}:kit={p.wasPlacedFromKit}")
            .OrderBy(s => s, StringComparer.Ordinal));

        private IEnumerator SaveRoundTrip()
        {
            var save = FindFirstObjectByType<SaveManager>();
            build.ExitBuildMode();
            string path = Path.Combine(Application.temporaryCachePath, "homestead-probe-" + Guid.NewGuid().ToString("N") + ".json");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo saveTo = typeof(SaveManager).GetMethod("TrySaveToPath", flags), loadFrom = typeof(SaveManager).GetMethod("TryLoadFromPath", flags);
            string before = Signature();
            Check((bool)saveTo.Invoke(save, new object[] { path }), "Saved to a temp path");
            for (int i = 0; i < 2; i++)
            {
                Check((bool)loadFrom.Invoke(save, new object[] { path }), "Loaded " + (i + 1));
                yield return null;
                yield return null;
                Check(Signature() == before, "Pieces and door states unchanged after load " + (i + 1));
            }
            PlacedPiece savedKit = PlacedPieceRegistry.All.FirstOrDefault(p => p.pieceId == "workbench" && p.wasPlacedFromKit);
            BuildPieceDefinition workbench = build.availablePieces.FirstOrDefault(p => p != null && p.id == "workbench");
            Check(savedKit != null && workbench != null && !HomesteadTools.DescribeRefund(workbench, savedKit, false).Contains("Construction Kit"),
                "Kit origin survives save/load, but the immediate refund window does not");
            if (savedKit != null)
            {
                int kits = inventory.GetCount("kit_workbench");
                bool firstConfirm = build.DebugDismantle(savedKit);
                bool secondConfirm = build.DebugDismantle(savedKit);
                Check(!firstConfirm && secondConfirm && inventory.GetCount("kit_workbench") == kits,
                    "An older kit-built piece dismantles and refunds recipe materials, not the kit");
            }
            foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }

        private IEnumerator Palette()
        {
            yield return null;
            var palette = HomesteadPalette.Active;
            Check(palette != null && palette.Open() && Time.timeScale == 0f, "G palette opens and pauses");
            var button = palette.DebugButtonFor("piece_foundation");
            Check(button != null, "Palette lists the foundation");
            button.onClick.Invoke();
            Check(!palette.IsOpen && Time.timeScale == 1f && build.IsBuildModeActive && build.CurrentPieceId == "piece_foundation", "A click closes it and enters build mode on that piece");
            build.ExitBuildMode();
        }

        private void Check(bool condition, string message)
        {
            if (!condition) failures++;
            Debug.Log((condition ? "[HomesteadProbe] PASS: " : "[HomesteadProbe] FAIL: ") + message);
        }
    }
}
