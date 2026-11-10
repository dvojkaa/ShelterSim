using System;
using System.Globalization;
using System.Linq;
using System.Text;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>Standalone structural checks for the new seeded house and town interiors.</summary>
    public sealed class BuildingProbe : MonoBehaviour
    {
        private int failures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--building-probe") >= 0)
                new GameObject("BuildingProbe").AddComponent<BuildingProbe>();
        }

        private void Start() => Run();

        private void Check(bool condition, string message)
        {
            if (condition) Debug.Log("[BuildingProbe] PASS: " + message);
            else { failures++; Debug.LogError("[BuildingProbe] FAIL: " + message); }
        }

        private void Run()
        {
            ProceduralWorld world = ProceduralWorld.Active;
            if (world == null || world.assets == null)
            {
                Check(false, "procedural world and assets are initialized");
                Finish();
                return;
            }

            world.SetRuined(false);
            Vector2Int suburbCoordinate = FindChunk(world.Seed, false);
            Vector2Int townCoordinate = FindChunk(world.Seed, true);
            Check(WorldLayout.IsSuburbChunk(suburbCoordinate, world.Seed), "found a suburb chunk");
            Check(WorldLayout.IsTownChunk(townCoordinate, world.Seed), "found a non-landmark town chunk");
            if (!WorldLayout.IsSuburbChunk(suburbCoordinate, world.Seed) || !WorldLayout.IsTownChunk(townCoordinate, world.Seed))
            {
                Finish();
                return;
            }

            GameObject suburb = world.EnsureChunk(suburbCoordinate);
            GameObject town = world.EnsureChunk(townCoordinate);
            Transform[] suburbHomes = Roots(suburb, "House_");
            Transform[] townBuildings = Roots(town, "Building_");
            string suburbLayout = LayoutHash(suburb);
            string townLayout = LayoutHash(town);
            Check(suburbHomes.Length > 0, "normal suburb has family houses");
            Check(townBuildings.Length > 0, "normal town has buildings");
            Check(suburb.GetComponentsInChildren<SheltreSim.Items.LootContainer>(true).Length == 0 &&
                  town.GetComponentsInChildren<SheltreSim.Items.LootContainer>(true).Length == 0,
                "normal pass has no searchable containers");
            foreach (Transform building in townBuildings)
            {
                int storeys = FloorCount(building);
                Check(storeys >= 2, building.name + " has distinct walkable floor groups");
                Check(FrontDoorClear(building), building.name + " has a clear standing-player front entrance");
            }

            world.SetRuined(true);
            suburb = world.EnsureChunk(suburbCoordinate);
            town = world.EnsureChunk(townCoordinate);
            Transform[] ruinedHomes = Roots(suburb, "House_");
            Transform[] ruinedBuildings = Roots(town, "Building_");
            Check(LayoutHash(suburb) == suburbLayout, "suburb placement and furniture slots are unchanged by the bombs");
            string ruinedTown = LayoutHash(town);
            if (ruinedTown != townLayout) LogLayoutDifference("town", townLayout, ruinedTown);
            // After the bombs some apartment flats are sealed by rubble, so their furniture slots (C##) disappear on purpose.
            // Nothing may be added or moved, and only slots may be missing.
            Check(TownUnchangedExceptSealedSlots(townLayout, ruinedTown), "town lot assignment is unchanged by the bombs; only sealed-flat slots disappear");
            Check(ruinedHomes.Length == suburbHomes.Length && ruinedBuildings.Length == townBuildings.Length,
                "the same occupied lots remain after the bombs");

            Transform lootHouse = ruinedHomes.FirstOrDefault(h => h.GetComponentsInChildren<SheltreSim.Items.LootContainer>(true).Length >= 9);
            Check(lootHouse != null, "a ruined family house exposes at least nine furniture loot slots");
            Check(ruinedBuildings.SelectMany(b => b.GetComponentsInChildren<SheltreSim.Items.LootContainer>(true)).Any(),
                "ruined town buildings expose searchable furniture");
            Check(SlotTablesAreDeterministic(), "all furniture loot tables are deterministic and produce valid bounded stacks");

            Finish();
        }

        private Vector2Int FindChunk(int seed, bool town)
        {
            for (int radius = 1; radius < 64; radius++)
                for (int z = -radius; z <= radius; z++)
                    for (int x = -radius; x <= radius; x++)
                    {
                        var coordinate = new Vector2Int(x, z);
                        if (town ? !WorldLayout.IsTownChunk(coordinate, seed) : !WorldLayout.IsSuburbChunk(coordinate, seed)) continue;
                        if (ProceduralLandmarks.HasLandmark(coordinate, seed)) continue;
                        return coordinate;
                    }
            return Vector2Int.zero;
        }

        private static Transform[] Roots(GameObject chunk, string prefix) => chunk.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith(prefix, StringComparison.Ordinal) &&
                        (t.parent == null || !t.parent.name.StartsWith(prefix, StringComparison.Ordinal))).ToArray();

        private static int FloorCount(Transform building) => building.Cast<Transform>().Count(t => t.name.StartsWith("Floor_", StringComparison.Ordinal));

        private static bool TownUnchangedExceptSealedSlots(string normal, string ruined)
        {
            var a = new System.Collections.Generic.HashSet<string>(normal.Split('\n'));
            var b = new System.Collections.Generic.HashSet<string>(ruined.Split('\n'));
            foreach (string row in b) if (row.Length > 0 && !a.Contains(row)) return false;
            foreach (string row in a)
            {
                if (row.Length == 0 || b.Contains(row)) continue;
                string path = row.Substring(0, row.IndexOf('|'));
                string leaf = path.Substring(path.LastIndexOf('/') + 1);
                if (!IsSlotName(leaf)) return false;
            }
            return true;
        }

        private static void LogLayoutDifference(string label, string normal, string ruined)
        {
            var a = new System.Collections.Generic.HashSet<string>(normal.Split('\n'));
            var b = new System.Collections.Generic.HashSet<string>(ruined.Split('\n'));
            int shown = 0;
            foreach (string row in a) if (!b.Contains(row) && shown++ < 6) Debug.Log($"[BuildingProbe] {label} only in normal pass: {row}");
            shown = 0;
            foreach (string row in b) if (!a.Contains(row) && shown++ < 6) Debug.Log($"[BuildingProbe] {label} only in ruined pass: {row}");
            Debug.Log($"[BuildingProbe] {label} rows normal={a.Count} ruined={b.Count}");
        }

        private static string LayoutHash(GameObject chunk)
        {
            var rows = chunk.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("House_", StringComparison.Ordinal) || t.name.StartsWith("Building_", StringComparison.Ordinal) ||
                            t.name.StartsWith("Floor_", StringComparison.Ordinal) || IsSlotName(t.name))
                .Select(t => HierarchyPath(t, chunk.transform) + "|" + t.localPosition.x.ToString("R", CultureInfo.InvariantCulture) + "," +
                             t.localPosition.y.ToString("R", CultureInfo.InvariantCulture) + "," + t.localPosition.z.ToString("R", CultureInfo.InvariantCulture))
                .OrderBy(s => s, StringComparer.Ordinal);
            var hash = new StringBuilder();
            foreach (string row in rows) hash.Append(row).Append('\n');
            return hash.ToString();
        }

        private static bool IsSlotName(string name) => name.Length == 3 && name[0] == 'C' && char.IsDigit(name[1]) && char.IsDigit(name[2]);

        private static string HierarchyPath(Transform t, Transform root)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null && p != root; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        private static bool FrontDoorClear(Transform building)
        {
            Transform structure = building.Find("Static");
            if (structure == null) return false;
            BoxCollider[] pieces = structure.GetComponentsInChildren<BoxCollider>(true);
            Bounds bounds = new Bounds(building.position, Vector3.zero);
            foreach (BoxCollider box in pieces)
                if (box.enabled) bounds.Encapsulate(box.bounds);
            bool house = building.name.StartsWith("House_", StringComparison.Ordinal);
            bool block = building.Find("Floor_4") != null;
            float depth = house || !block ? 12f : 14f;
            Vector3 feet = building.TransformPoint(new Vector3(0f, .02f, -depth * .5f + .15f));
            Vector3 bottom = feet + Vector3.up * .37f;
            Vector3 top = feet + Vector3.up * 1.43f;
            foreach (Collider hit in Physics.OverlapCapsule(bottom, top, .35f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit == null || hit.transform == building || hit.transform.IsChildOf(building)) continue;
                if (hit.bounds.max.y <= feet.y + .12f) continue;
                return false;
            }
            return true;
        }

        private static bool SlotTablesAreDeterministic()
        {
            var host = new GameObject("BuildingProbeSlot");
            var loot = host.AddComponent<SheltreSim.Items.LootContainer>();
            bool valid = true;
            foreach (string name in SheltreSim.Crafting.LootTables.SlotTableNames)
            {
                SheltreSim.Crafting.LootTables.FillSlot(loot, name, new System.Random(1227), 3);
                string first = string.Join(",", loot.entries.Select(e => e.resource.id + "x" + e.amount));
                SheltreSim.Crafting.LootTables.FillSlot(loot, name, new System.Random(1227), 3);
                string second = string.Join(",", loot.entries.Select(e => e.resource.id + "x" + e.amount));
                valid &= first == second && loot.entries.Count <= SheltreSim.Crafting.LootTables.MaxStacksPerContainer &&
                         loot.entries.All(e => e.resource != null && e.amount > 0);
            }
            Destroy(host);
            return valid;
        }

        private void Finish()
        {
            Debug.Log(failures == 0 ? "[BuildingProbe] ALL CHECKS PASSED" : $"[BuildingProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }
    }
}
