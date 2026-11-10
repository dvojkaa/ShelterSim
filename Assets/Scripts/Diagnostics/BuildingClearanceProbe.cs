using System;
using System.Collections;
using System.Linq;
using SheltreSim.Items;
using SheltreSim.Saving;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>Run in a built player with --building-clearance-probe; checks all-containers completion and save-state restore.</summary>
    public sealed class BuildingClearanceProbe : MonoBehaviour
    {
        private int failures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--building-clearance-probe") >= 0)
                new GameObject(nameof(BuildingClearanceProbe)).AddComponent<BuildingClearanceProbe>();
        }

        private IEnumerator Start()
        {
            for (int i = 0; i < 120 && ProceduralWorld.Active == null; i++) yield return null;
            Check(ProceduralWorld.Active != null, "procedural world is available");
            if (ProceduralWorld.Active == null) { Finish(); yield break; }

            BuildingClearanceTracker tracker = BuildingClearanceTracker.Ensure();
            tracker.RestoreState(null);
            GameObject building = new GameObject("Building_ClearanceProbe");
            building.transform.position = new Vector3(2100f, 0f, 2100f);
            BuildingInterior interior = building.AddComponent<BuildingInterior>();
            LootContainer first = AddEmptyContainer(building.transform, "Container_A");
            LootContainer second = AddEmptyContainer(building.transform, "Container_B");
            GameObject searcher = new GameObject("ClearanceProbeSearcher");
            first.Interact(searcher);

            string id = $"building:{ProceduralWorld.Active.Seed}:{building.name}:{Mathf.RoundToInt(building.transform.position.x * 2f)}:{Mathf.RoundToInt(building.transform.position.z * 2f)}";
            Check(!tracker.CaptureState().Contains(id), "a building stays uncleared while one container remains");
            second.Interact(searcher);
            Check(BuildingClearanceTracker.Cleared.Any(entry => entry != null && entry.id == id),
                "all searched containers mark their parent building as cleared");

            string savedState = tracker.CaptureState();
            Check(SaveableSystems.CaptureAll().Any(state => state.key == "building_clearance"),
                "building-clearance state is registered with the save system");
            tracker.RestoreState(null);
            Check(!BuildingClearanceTracker.Cleared.Any(entry => entry != null && entry.id == id),
                "restoring an empty state clears atlas marks for a new game");
            tracker.RestoreState(savedState);
            Check(BuildingClearanceTracker.Cleared.Any(entry => entry != null && entry.id == id &&
                Mathf.Abs(entry.x - building.transform.position.x) < .01f && Mathf.Abs(entry.z - building.transform.position.z) < .01f),
                "saved clear marker restores its stable id and map position");

            Destroy(searcher);
            Destroy(building);
            Finish();
        }

        private static LootContainer AddEmptyContainer(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            LootContainer container = child.AddComponent<LootContainer>();
            container.hasLoot = false;
            return container;
        }

        private void Check(bool condition, string description)
        {
            if (condition) Debug.Log("[BuildingClearanceProbe] PASS: " + description);
            else { failures++; Debug.LogError("[BuildingClearanceProbe] FAIL: " + description); }
        }

        private void Finish()
        {
            Debug.Log(failures == 0 ? "[BuildingClearanceProbe] ALL CHECKS PASSED" : $"[BuildingClearanceProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }
    }
}
