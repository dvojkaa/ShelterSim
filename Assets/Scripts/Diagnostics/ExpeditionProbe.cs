using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Survival;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>Opt-in integration checks; never touches player save files or presentation assets.</summary>
    public sealed class ExpeditionProbe : MonoBehaviour
    {
        ProceduralWorld world;
        FirstPersonController player;
        bool finished;
        float started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--expedition-probe") >= 0)
                new GameObject("ExpeditionProbe").AddComponent<ExpeditionProbe>();
        }

        IEnumerator Start()
        {
            started = Time.realtimeSinceStartup;
            var stack = new Stack<IEnumerator>();
            stack.Push(Run());
            while (stack.Count > 0 && !finished)
            {
                object next = null;
                bool moved;
                try
                {
                    moved = stack.Peek().MoveNext();
                    if (moved) next = stack.Peek().Current;
                    else stack.Pop();
                }
                catch (Exception error) { Fail(error.ToString()); yield break; }
                if (!moved) continue;
                if (next is IEnumerator nested) stack.Push(nested);
                else yield return next;
            }
            if (finished) yield break;
            finished = true;
            Debug.Log("[ExpeditionProbe] ALL CHECKS PASSED");
            Application.Quit(0);
        }

        void Update()
        {
            if (!finished && Time.realtimeSinceStartup - started > 120f) Fail("Timed out after 120 seconds.");
        }

        IEnumerator Run()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            yield return null;
            yield return null;
            world = ProceduralWorld.Active;
            player = FindFirstObjectByType<FirstPersonController>();
            Require(world != null && player != null, "World and player initialized");
            SheltreSim.Core.HomeBase.Active.DebugStartSurvival();
            player.GetComponent<SurvivalStats>().decayPaused = true;
            // Keep streaming and combat stationary while probing chunks explicitly.
            world.enabled = false;
            player.enabled = false;
            player.GetComponent<CharacterController>().enabled = false;
            Time.timeScale = 0f;
            yield return CheckAtmosphere();
            CheckWater();
            yield return CheckLandmarks();
            CheckProgression();
            yield return CheckEncounterPersistence();
        }

        IEnumerator CheckAtmosphere()
        {
            var atmosphere = FindFirstObjectByType<WorldAtmosphere>();
            Require(atmosphere != null, "Weather clock exists");
            foreach (float seconds in new[] { 0f, 89f, 371f, 1200f, 100000f })
            {
                atmosphere.RestoreTime(seconds);
                string weather = atmosphere.WeatherName;
                float intensity = atmosphere.WeatherIntensity;
                Require(atmosphere.Hour >= 0 && atmosphere.Hour < 24, "Clock hour wraps into [0,24)");
                atmosphere.RestoreTime(seconds + 190f);
                atmosphere.RestoreTime(seconds);
                Require(weather == atmosphere.WeatherName && Mathf.Approximately(intensity, atmosphere.WeatherIntensity),
                    "Weather restores deterministically at identical location/time");
            }
            float pausedAt = atmosphere.ElapsedSeconds;
            yield return null;
            yield return null;
            Require(atmosphere.ElapsedSeconds == pausedAt, "Modal pause freezes weather clock");
            Debug.Log("[ExpeditionProbe] Weather restore and modal freeze passed");
        }

        void CheckWater()
        {
            int wet = 0, dry = 0;
            bool broadWater = false, narrowWater = false;
            for (int z = -4096; z <= 4096; z += 32)
            {
                int run = 0;
                for (int x = -4096; x <= 4096; x += 16)
                {
                    float depth = WorldLayout.WaterDepthAt(x, z, world.Seed);
                    Require(!float.IsNaN(depth) && depth >= 0f && depth <= .42001f, "Shallow water depth bounded");
                    if (depth > .015f)
                    {
                        wet++; run++;
                        Require(Mathf.Abs(WorldLayout.WaterSurfaceAt(x, z, world.Seed) -
                            WorldLayout.HeightAt(x, z, world.Seed) - depth) < .0001f, "Water surface matches terrain plus depth");
                    }
                    else
                    {
                        dry++;
                        broadWater |= run >= 7;
                        narrowWater |= run >= 1 && run <= 5;
                        run = 0;
                    }
                }
            }
            Require(wet > 0 && dry > 0 && broadWater && narrowWater,
                "Sample contains dry ground, narrow river reaches and broad lake basins");
            Require(WorldLayout.WaterDepthAt(72f, -217f, world.Seed) == 0f, "Starter bunker remains dry");
            Debug.Log($"[ExpeditionProbe] Water samples: {wet} wet, {dry} dry; narrow and broad reaches present");
        }

        IEnumerator CheckLandmarks()
        {
            var types = new HashSet<string>();
            int wildlife = 0;
            for (int z = -12; z <= 12 && types.Count < 6; z++)
                for (int x = -12; x <= 12 && types.Count < 6; x++)
                {
                    var coordinate = new Vector2Int(x, z);
                    if (!ProceduralLandmarks.HasLandmark(coordinate, world.Seed)) continue;
                    GameObject chunk = world.EnsureChunk(coordinate);
                    var destination = chunk.GetComponentsInChildren<LandmarkDiscovery>().FirstOrDefault(
                        item => item.name.StartsWith("Landmark_", StringComparison.Ordinal));
                    Require(destination != null, "EnsureChunk builds eligible landmark");
                    if (!types.Add(destination.name)) continue;
                    Physics.SyncTransforms();
                    CheckDestination(destination.transform);
                    Debug.Log($"[ExpeditionProbe] {destination.name} at {coordinate} physics/loot passed");
                    yield return null;
                }
            Require(types.Count == 6, "All six landmark types available within +/-12 chunks");
            for (int i = 0; i < 12 && wildlife == 0; i++)
            {
                GameObject chunk = world.EnsureChunk(new Vector2Int(15 + i, 15));
                wildlife += chunk.GetComponentsInChildren<WorldWildlife>().Length;
            }
            Require(wildlife > 0, "Natural chunks generate living wildlife");
        }

        void CheckDestination(Transform destination)
        {
            var crates = destination.GetComponentsInChildren<LootContainer>();
            Require(crates.Length >= 4, "Destination contains specialized interior caches");
            foreach (var crate in crates)
            {
                Require(crate.hasLoot && crate.entries.Any(entry => entry.resource != null && entry.amount > 0), "Cache has usable loot");
                Vector3 feet = crate.transform.position + new Vector3(0, .05f, -1.6f);
                Require(Physics.OverlapCapsule(feet + Vector3.up * .35f, feet + Vector3.up * 1.45f, .35f,
                    ~0, QueryTriggerInteraction.Ignore).Length == 0, "Cache aisle admits standing player");
                Vector3 eye = feet + Vector3.up * 1.5f;
                Vector3 target = crate.GetComponent<Collider>().bounds.center;
                Require(Physics.Raycast(eye, (target - eye).normalized, out RaycastHit hit, 2.5f,
                    ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<LootContainer>() == crate,
                    "Interior loot is visible within interaction range");
            }
            // Sweep the standing capsule down the shared clear entrance axis.
            for (int z = -16; z <= 2; z += 2)
            {
                Vector3 feet = destination.position + new Vector3(0, .05f, z);
                Require(Physics.OverlapCapsule(feet + Vector3.up * .35f, feet + Vector3.up * 1.45f, .35f,
                    ~0, QueryTriggerInteraction.Ignore).Length == 0, "Entrance admits standing player");
            }
            Transform ramp = destination.Find("EntranceRamp");
            Require(ramp != null, "Raised deck has access ramp");
            Require(Vector3.Angle(ramp.up, Vector3.up) <= 35.01f, "Ramp slope is walkable");
            for (int i = 1; i < 10; i++)
            {
                Vector3 surface = ramp.TransformPoint(new Vector3(0, .5f, i / 10f - .5f));
                Require(Physics.Raycast(surface + Vector3.up * 2, Vector3.down, out RaycastHit hit, 2.2f,
                    ~0, QueryTriggerInteraction.Ignore) && hit.transform == ramp,
                    "Ramp top clears terrain and joins the destination approach");
            }
        }

        void CheckProgression()
        {
            var progression = ExpeditionProgression.Active;
            var inventory = player.GetComponent<Inventory>();
            Require(progression != null && inventory != null, "Progression and inventory exist");
            Require(progression.expeditionParts != null && progression.wood != null && progression.scrap != null && progression.iron != null,
                "Upgrade resources wired");
            progression.Restore(0, new List<string>());
            float baseSprint = player.sprintSpeed;
            foreach (var resource in new[] { progression.expeditionParts, progression.wood, progression.scrap, progression.iron })
                inventory.SetCount(resource.id, 100);
            Require(!progression.TryUpgrade(0, out _), "Upgrades require discovery");
            for (int i = 0; i < 3; i++) progression.DiscoverLandmark("probe_site_" + i, "Probe site " + i);
            progression.DiscoverLandmark("probe_site_0", "Duplicate fixture");
            Require(progression.DiscoveryCount == 3, "Discoveries deduplicate");
            for (int i = 0; i < 3; i++)
            {
                var costs = progression.UpgradeCost(i);
                var before = costs.Select(cost => inventory.GetCount(cost.resource.id)).ToArray();
                Require(progression.TryUpgrade(i, out string result), "Upgrade installs: " + result);
                for (int j = 0; j < costs.Count; j++)
                    Require(inventory.GetCount(costs[j].resource.id) == before[j] - costs[j].amount, "Upgrade spends exact cost");
                var installedCounts = costs.Select(cost => inventory.GetCount(cost.resource.id)).ToArray();
                Require(!progression.TryUpgrade(i, out _), "Repeated upgrade is rejected");
                for (int j = 0; j < costs.Count; j++)
                    Require(inventory.GetCount(costs[j].resource.id) == installedCounts[j],
                        "Repeated upgrade cannot spend components or materials");
            }
            Require(Mathf.Approximately(progression.ExposureProtection, .35f) && Mathf.Approximately(progression.NeedsMultiplier, .8f)
                && Mathf.Approximately(player.sprintSpeed, baseSprint * 1.2f), "All three benefits apply");
            var discoveries = progression.CaptureDiscoveries();
            progression.Restore(7, discoveries);
            Require(progression.DiscoveryCount == 3 && Mathf.Approximately(player.sprintSpeed, baseSprint * 1.2f),
                "Progression restore preserves benefits without compounding sprint");
            Debug.Log("[ExpeditionProbe] Discovery gates, upgrade costs, benefits and restore passed");
        }

        IEnumerator CheckEncounterPersistence()
        {
            var slots = world.GetComponentsInChildren<ChunkEncounterSlot>().Where(slot => slot.Actor != null).Take(2).ToArray();
            for (int i = 0; slots.Length < 2 && i < 30; i++)
            {
                world.EnsureChunk(new Vector2Int(20 + i, 20));
                slots = world.GetComponentsInChildren<ChunkEncounterSlot>().Where(slot => slot.Actor != null).Take(2).ToArray();
            }
            Require(slots.Length == 2, "Two generated encounter slots available");
            Vector2Int aliveCoordinate = WorldLayout.Coordinate(slots[0].transform.position);
            Vector2Int defeatedCoordinate = WorldLayout.Coordinate(slots[1].transform.position);
            string aliveName = slots[0].name, defeatedName = slots[1].name;
            slots[0].Actor.TakeDamage(5f);
            float damagedHealth = slots[0].Health;
            slots[1].Actor.TakeDamage(slots[1].Health + 1);
            Require(slots[1].Defeated, "Real actor death marks encounter defeated");
            int seed = world.Seed;
            var snapshot = world.CaptureWorld();
            world.RestoreWorld(seed, snapshot, true);
            yield return null;
            var alive = world.EnsureChunk(aliveCoordinate).GetComponentsInChildren<ChunkEncounterSlot>().FirstOrDefault(slot => slot.name == aliveName);
            var defeated = world.EnsureChunk(defeatedCoordinate).GetComponentsInChildren<ChunkEncounterSlot>().FirstOrDefault(slot => slot.name == defeatedName);
            Require(alive != null && !alive.Defeated && Mathf.Approximately(alive.Health, damagedHealth),
                "Capture/restore/regeneration preserves wounded encounter health");
            Require(defeated != null && defeated.Defeated && defeated.Actor == null,
                "Capture/restore/regeneration keeps defeated patrol absent");
            Debug.Log("[ExpeditionProbe] Encounter damage and defeat persistence passed");
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        void Fail(string reason)
        {
            if (finished) return;
            finished = true;
            Debug.LogError("[ExpeditionProbe] FAILED: " + reason);
            Application.Quit(1);
        }
    }
}
