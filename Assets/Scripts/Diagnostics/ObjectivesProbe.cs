using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using SheltreSim.Building;
using SheltreSim.Core;
using SheltreSim.Crafting;
using SheltreSim.Items;
using SheltreSim.Objectives;
using SheltreSim.Player;
using SheltreSim.Power;
using SheltreSim.Saving;
using SheltreSim.Survival;
using SheltreSim.UI;
using SheltreSim.World;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// `--objectives-probe`: landmark metadata against really built chunks, guaranteed rewards against a same-seed
    /// replay, then the radio, the relay and the atlas through their real code paths, including every refusal.
    /// Saves to a temp path only.
    /// </summary>
    public sealed class ObjectivesProbe : MonoBehaviour
    {
        private sealed class ProbeSource : IPowerSource
        {
            public Vector3 PowerPosition { get; set; }
            public float PowerOutput { get; set; }
            public float HeatOutput => 0f;
            public string PowerName => "Probe source";
        }

        // Far from anything a real device could stand on, so no other network can power the test consoles.
        private static readonly Vector3 Isolated = new Vector3(72f, 0f, 1500f);

        private int failures;
        private FirstPersonController player;
        private SurvivalStats stats;
        private Inventory inventory;
        private ProceduralWorld world;
        private ObjectiveTracker tracker;
        private ExpeditionProgression progression;
        private ProbeSource source;
        private RadioConsolePiece console;
        private int seed;
        private string lastMessage = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--objectives-probe") < 0) return;
            new GameObject("ObjectivesProbe").AddComponent<ObjectivesProbe>();
        }

        private IEnumerator Start()
        {
            StartCoroutine(Watchdog());
            GameMessageBus.OnMessage += OnMessage;
            yield return null;
            yield return null;
            player = FindFirstObjectByType<FirstPersonController>();
            stats = player.GetComponent<SurvivalStats>();
            inventory = player.GetComponent<Inventory>();
            world = ProceduralWorld.Active;
            tracker = ObjectiveTracker.Instance;
            progression = ExpeditionProgression.Active;
            Check(world != null && tracker != null && progression != null, "World, tracker (self-created) and progression exist");
            HomeBase.Active.DebugStartSurvival();
            yield return null;
            seed = world.Seed;

            source = new ProbeSource { PowerPosition = Isolated + Vector3.right * 3f };
            PowerGrid.Register(source);
            var consoleObject = new GameObject("ProbeRadio");
            consoleObject.transform.position = Isolated;
            console = consoleObject.AddComponent<RadioConsolePiece>();

            yield return Run(CheckMetadata());
            yield return Run(CheckSearch());
            yield return Run(CheckRewards());
            Economy();
            yield return Run(CheckRadio());
            yield return Run(CheckExclusion());
            yield return Run(CheckRelay());
            yield return Run(CheckHudAndGoals());
            yield return Run(CheckAtlas());
            yield return Run(CheckVictoryAndSave());

            PowerGrid.Unregister(source);
            GameMessageBus.OnMessage -= OnMessage;
            Time.timeScale = 1f;
            Debug.Log(failures == 0 ? "[ObjectivesProbe] ALL CHECKS PASSED" : $"[ObjectivesProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private void OnMessage(string message) => lastMessage = message;

        private IEnumerator Run(IEnumerator routine)
        {
            if (stats != null) stats.health = stats.maxHealth;
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext()) yield break;
                    current = routine.Current;
                }
                catch (Exception error)
                {
                    Check(false, "Exception: " + error);
                    yield break;
                }
                yield return current;
            }
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(170f);
            Debug.LogError("[ObjectivesProbe] FAIL: timed out");
            Application.Quit(1);
        }

        private static Transform FindLandmark(GameObject chunk) =>
            chunk.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Landmark_"));

        private IEnumerator CheckMetadata()
        {
            Check(world.Ruined, "World is ruined for metadata checks");
            Check(ProceduralLandmarks.TryGetKind(new Vector2Int(-2, -3), seed, out int a) && a == ProceduralLandmarks.KindHospital, "Forced (-2,-3) is a hospital");
            // (3,-4) is forced but its deck doesn't fit the suburb terrain since M20, so no landmark is built there. Metadata must agree.
            Check(ProceduralLandmarks.TryGetKind(new Vector2Int(3, -4), seed, out _) == ProceduralLandmarks.HasLandmark(new Vector2Int(3, -4), seed), "Metadata agrees with HasLandmark at (3,-4)");
            Check(ProceduralLandmarks.TryGetKind(new Vector2Int(-4, -5), seed, out int c) && c == ProceduralLandmarks.KindFactory, "Forced (-4,-5) is a factory");

            var coordinates = new List<Vector2Int> { new Vector2Int(-2, -3), new Vector2Int(-4, -5) };
            for (int kind = 0; kind < ProceduralLandmarks.KindCount; kind++)
            {
                Vector2Int? nearest = ProceduralLandmarks.FindNearest(kind, WorldLayout.BunkerCenter, seed, ObjectiveTracker.SearchRadiusChunks);
                Check(nearest.HasValue, $"A {ProceduralLandmarks.KindName(kind)} exists within {ObjectiveTracker.SearchRadiusChunks} chunks");
                if (nearest.HasValue && !coordinates.Contains(nearest.Value)) coordinates.Add(nearest.Value);
            }
            int matched = 0;
            foreach (Vector2Int coordinate in coordinates)
            {
                if (!ProceduralLandmarks.TryGetKind(coordinate, seed, out int kind)) { Check(false, $"{coordinate} has metadata"); continue; }
                GameObject chunk = world.EnsureChunk(coordinate);
                Transform landmark = FindLandmark(chunk);
                var discovery = landmark != null ? landmark.GetComponent<LandmarkDiscovery>() : null;
                bool same = landmark != null && landmark.name == "Landmark_" + ProceduralLandmarks.KindName(kind) && discovery != null &&
                    discovery.stableId == ProceduralLandmarks.StableId(coordinate, seed) && discovery.title == ProceduralLandmarks.Title(kind);
                Check(same, $"Metadata matches the built chunk at {coordinate}: {(landmark != null ? landmark.name : "none")} / {(discovery != null ? discovery.stableId : "none")}");
                if (same) matched++;
                yield return null;
            }
            Check(matched == coordinates.Count, $"All {matched} checked landmarks match");

            int empties = 0;
            for (int x = -6; x <= 6 && empties < 3; x++)
                for (int z = -8; z <= 2 && empties < 3; z++)
                {
                    var coordinate = new Vector2Int(x, z);
                    if (ProceduralLandmarks.TryGetKind(coordinate, seed, out _)) continue;
                    Check(FindLandmark(world.EnsureChunk(coordinate)) == null, $"No metadata and no landmark built at {coordinate}");
                    empties++;
                }
            yield return null;
        }

        private IEnumerator CheckSearch()
        {
            int loaded = world.LoadedChunkCount, remembered = world.RememberedChunkCount;
            var clock = Stopwatch.StartNew();
            foreach (Vector3 from in new[] { WorldLayout.BunkerCenter, Isolated, new Vector3(20000f, 0f, 20000f) })
                for (int kind = 0; kind < ProceduralLandmarks.KindCount; kind++)
                    ProceduralLandmarks.FindNearest(kind, from, seed, ObjectiveTracker.SearchRadiusChunks);
            ProceduralLandmarks.FindNearest(ProceduralLandmarks.KindFuel, Isolated, seed, ObjectiveTracker.SearchRadiusChunks, id => true);
            clock.Stop();
            Check(world.LoadedChunkCount == loaded && world.RememberedChunkCount == remembered, $"Searching generated no chunks ({loaded} loaded, {clock.ElapsedMilliseconds} ms for 18 searches and a full 40-ring sweep)");

            // Against brute force, because a ring search is easy to get subtly wrong.
            for (int kind = 0; kind < ProceduralLandmarks.KindCount; kind++)
            {
                Vector3 from = Isolated;
                Vector2Int center = WorldLayout.Coordinate(from);
                float best = float.PositiveInfinity;
                for (int x = -15; x <= 15; x++)
                    for (int z = -15; z <= 15; z++)
                    {
                        var coordinate = center + new Vector2Int(x, z);
                        if (!ProceduralLandmarks.TryGetKind(coordinate, seed, out int found) || found != kind) continue;
                        Vector3 p = ProceduralLandmarks.WorldCenter(coordinate);
                        best = Mathf.Min(best, new Vector2(p.x - from.x, p.z - from.z).magnitude);
                    }
                Vector2Int? nearest = ProceduralLandmarks.FindNearest(kind, from, seed, 15);
                float distance = float.PositiveInfinity;
                if (nearest.HasValue)
                {
                    Vector3 p = ProceduralLandmarks.WorldCenter(nearest.Value);
                    distance = new Vector2(p.x - from.x, p.z - from.z).magnitude;
                }
                Check(Mathf.Approximately(best, distance) || (float.IsInfinity(best) && float.IsInfinity(distance)),
                    $"FindNearest agrees with brute force for {ProceduralLandmarks.KindName(kind)} ({distance:0} m)");
            }
            Check(ProceduralLandmarks.StableId(new Vector2Int(-2, 7), 5) == "landmark:5:-2:7", "Stable id format is the discovery one");
            Check(ProceduralLandmarks.TryParseStableId("landmark:-5:-2:7", out Vector2Int parsed, out int parsedSeed) && parsed == new Vector2Int(-2, 7) && parsedSeed == -5, "Stable id parses");
            Check(!ProceduralLandmarks.TryParseStableId("probe_site_0", out _, out _), "A foreign id doesn't parse");
            yield return null;
        }

        private IEnumerator CheckRewards()
        {
            var scratchObject = new GameObject("ProbeLootScratch");
            var scratch = scratchObject.AddComponent<LootContainer>();
            for (int kind = 0; kind < ProceduralLandmarks.KindCount; kind++)
            {
                Vector2Int coordinate = ProceduralLandmarks.FindNearest(kind, WorldLayout.BunkerCenter, seed, ObjectiveTracker.SearchRadiusChunks).Value;
                Transform landmark = FindLandmark(world.EnsureChunk(coordinate));
                var random = new System.Random(WorldLayout.Hash(coordinate.x, coordinate.y, seed + 1237));
                bool rowsMatch = true;
                string detail = "";
                for (int slot = 0; slot < 4; slot++)
                {
                    LootTables.Fill(scratch, ProceduralLandmarks.TableName(kind), random);
                    var expected = scratch.entries.Select(e => (e.resource.id, e.amount)).ToList();
                    bool slotPart = slot == 3 && (kind == ProceduralLandmarks.KindFactory || kind == ProceduralLandmarks.KindBunker);
                    if (slotPart || random.NextDouble() < .12) expected.Add(("expedition_parts", 1));
                    if (slot == 0) foreach (var reward in ProceduralLandmarks.GuaranteedRewards(kind)) expected.Add(reward);
                    var loot = landmark.Find("Cache_" + slot.ToString("D2")).GetComponent<LootContainer>();
                    var actual = loot.entries.Select(e => (e.resource.id, e.amount)).ToList();
                    if (!actual.SequenceEqual(expected))
                    {
                        rowsMatch = false;
                        detail = $" slot {slot}: [{string.Join(",", actual)}] vs [{string.Join(",", expected)}]";
                    }
                }
                Check(rowsMatch, $"{ProceduralLandmarks.KindName(kind)} at {coordinate}: rolled rows unchanged, guaranteed rows appended{detail}");
                var first = landmark.Find("Cache_00").GetComponent<LootContainer>();
                foreach (var reward in ProceduralLandmarks.GuaranteedRewards(kind))
                    Check(first.entries.Any(e => e.resource.id == reward.id && e.amount == reward.amount), $"{ProceduralLandmarks.KindName(kind)} first cache holds {reward.amount} {reward.id}");
                if (kind == ProceduralLandmarks.KindFactory || kind == ProceduralLandmarks.KindBunker)
                    Check(landmark.Find("Cache_03").GetComponent<LootContainer>().entries.Any(e => e.resource.id == "expedition_parts"), $"{ProceduralLandmarks.KindName(kind)} slot 3 still holds its part");
                yield return null;
            }
            Destroy(scratchObject);
        }

        private void Economy()
        {
            foreach (int radius in new[] { 5, 10, 20 })
            {
                var counts = new int[ProceduralLandmarks.KindCount];
                Vector2Int center = WorldLayout.Coordinate(WorldLayout.BunkerCenter);
                for (int x = -radius; x <= radius; x++)
                    for (int z = -radius; z <= radius; z++)
                        if (ProceduralLandmarks.TryGetKind(center + new Vector2Int(x, z), seed, out int kind)) counts[kind]++;
                int parts = counts[ProceduralLandmarks.KindFactory] + counts[ProceduralLandmarks.KindBunker] + counts[ProceduralLandmarks.KindMilitary];
                Debug.Log($"[ObjectivesProbe] ECONOMY radius {radius} chunks ({radius * 128} m): {string.Join(", ", Enumerable.Range(0, ProceduralLandmarks.KindCount).Select(k => ProceduralLandmarks.ShortName(k) + "=" + counts[k]))}; guaranteed parts {parts}, plus 3 from the relay; 12 needed for all upgrades");
            }
        }

        private void Power(bool on, float kilowatts = 2f)
        {
            source.PowerOutput = on ? kilowatts : 0f;
            PowerGrid.DebugTick(PowerGrid.TickSeconds);
        }

        private IEnumerator CheckRadio()
        {
            tracker.RestoreState(null);
            progression.Restore(0, new List<string>());
            Power(false);
            Check(!console.IsPowered && console.InteractPrompt.Contains("NO POWER"), "Prompt says there is no power: " + console.InteractPrompt);
            Check(console.PowerDemand == 0f && console.PowerPriority == 2, "Idle console draws nothing, priority 2");

            console.Interact(player.gameObject);
            Check(ModalUI.IsOpen && Time.timeScale == 0f, "Console panel pauses the game");
            Button fuel = console.GetComponentsInChildren<Button>(true).First(b => b.name == "Scan_Fuel");
            Button close = console.GetComponentsInChildren<Button>(true).First(b => b.name == "Close");
            Check(console.GetComponentsInChildren<Button>(true).Count(b => b.name.StartsWith("Scan_")) == 6, "Six scan choices on the panel");
            fuel.onClick.Invoke();
            Check(console.CurrentOperation == RadioConsolePiece.Operation.Scan && console.CurrentNeed == RadioNeed.Fuel, "Fuel button starts a fuel scan");
            Check(!fuel.interactable, "Scan buttons lock during a scan");
            close.onClick.Invoke();
            Check(!ModalUI.IsOpen && Time.timeScale == 1f, "Close returns control");
            Check(console.PowerDemand == 1f, "Scanning draws 1 kW");

            console.DebugAdvance(100f);
            yield return new WaitForSeconds(.6f);
            Check(console.Elapsed == 0f && !tracker.HasWaypoint, "No power: no progress, no result");

            Power(true);
            Check(console.IsPowered, "Powered by the probe source");
            console.DebugAdvance(10f);
            Check(Mathf.Approximately(console.Elapsed, 10f), "Powered time counts");
            yield return new WaitForSeconds(.6f);
            Check(console.Elapsed > 10f, $"Real Update advances it too ({console.Elapsed:F2} s)");
            float before = console.Elapsed;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(.2f);
            Time.timeScale = 1f;
            Check(console.Elapsed - before < .15f, "A paused game doesn't advance it");

            Power(false);
            float kept = console.Elapsed;
            console.DebugAdvance(100f);
            yield return new WaitForSeconds(.4f);
            Check(Mathf.Approximately(console.Elapsed, kept) && console.CurrentOperation == RadioConsolePiece.Operation.Scan, "A power cut keeps progress and stops it");
            Check(console.InteractPrompt.Contains("NO POWER") && console.InteractPrompt.Contains("%"), "Prompt shows the stopped scan: " + console.InteractPrompt);

            // RestorePieceState runs on an inactive instance before Awake.
            var restoredObject = new GameObject("ProbeRadioRestored");
            restoredObject.SetActive(false);
            restoredObject.transform.position = Isolated + Vector3.left * 900f;
            var restored = restoredObject.AddComponent<RadioConsolePiece>();
            restored.RestorePieceState(console.CapturePieceState());
            restoredObject.SetActive(true);
            Check(restored.CurrentOperation == RadioConsolePiece.Operation.Scan && restored.CurrentNeed == RadioNeed.Fuel && Mathf.Approximately(restored.Elapsed, kept), "Piece state survives a capture and restore");
            restored.RestorePieceState("garbage{");
            Check(Mathf.Approximately(restored.Elapsed, kept), "Damaged piece state is ignored");
            Destroy(restoredObject);

            console.Interact(player.gameObject);
            console.Interact(player.gameObject);
            console.Close();
            Check(!console.BeginScan(RadioNeed.Food, out _) && console.CurrentNeed == RadioNeed.Fuel && Mathf.Approximately(console.Elapsed, kept), "Repeating E or starting again changes nothing");
            Check(!tracker.HasWaypoint, "Nothing created by repeated interaction");

            int discovered = progression.DiscoveryCount;
            Power(true);
            console.DebugAdvance(RadioConsolePiece.ScanSeconds - console.Elapsed + .1f);
            Check(console.CurrentOperation == RadioConsolePiece.Operation.None && console.Elapsed == 0f, "Scan finishes after 45 s of powered time");
            Check(tracker.HasWaypoint, "Scan pins a waypoint: " + tracker.WaypointTitle);
            Check(ProceduralLandmarks.TryParseStableId(tracker.WaypointLandmark, out Vector2Int pinned, out _) && ProceduralLandmarks.TryGetKind(pinned, seed, out int pinnedKind) && pinnedKind == ProceduralLandmarks.KindFuel, "Pinned site is a fuel station");
            Check(!progression.IsDiscovered(tracker.WaypointLandmark) && progression.DiscoveryCount == discovered, "Pinning doesn't discover it");
            Check(lastMessage.Contains(" m ") && lastMessage.Contains(tracker.WaypointTitle), "Message gives distance and direction: " + lastMessage);
            string first = tracker.WaypointLandmark;
            console.DebugAdvance(500f);
            Check(tracker.WaypointLandmark == first, "No second result from the same scan");
            yield return null;
        }

        private IEnumerator CheckExclusion()
        {
            string a = tracker.WaypointLandmark;
            Check(tracker.CompleteScan(RadioNeed.Fuel, Isolated), "Second fuel scan succeeds");
            string b = tracker.WaypointLandmark;
            Check(b != a, "The same site never comes twice in a row");
            progression.DiscoverLandmark(b, "Probe discovered");
            Check(tracker.CompleteScan(RadioNeed.Fuel, Isolated), "Third fuel scan succeeds");
            string c = tracker.WaypointLandmark;
            Check(c != b && !progression.IsDiscovered(c), "A discovered site is never returned");
            progression.DiscoverLandmark(a, "Probe discovered");
            progression.DiscoverLandmark(c, "Probe discovered");
            Check(tracker.CompleteScan(RadioNeed.Fuel, Isolated), "Fourth scan succeeds");
            string d = tracker.WaypointLandmark;
            Check(d != a && d != b && d != c, "Excludes every discovered site");

            Vector2Int? here = ProceduralLandmarks.FindNearest(ProceduralLandmarks.KindFuel, Isolated, seed, 0);
            if (!here.HasValue)
            {
                lastMessage = "";
                Check(!tracker.CompleteScan(RadioNeed.Fuel, Isolated, 0) && tracker.WaypointLandmark == d, "Nothing in range: false, waypoint untouched");
                Check(lastMessage.Contains("static"), "And it says so: " + lastMessage);
            }
            progression.Restore(0, new List<string>());
            yield return null;
        }

        private void SetRelayParts(int boards, int wire, int motors)
        {
            inventory.SetCount("circuit_board", boards);
            inventory.SetCount("copper_wire", wire);
            inventory.SetCount("electric_motor", motors);
        }

        private bool RelayPartsAre(int boards, int wire, int motors) => inventory.GetCount("circuit_board") == boards &&
            inventory.GetCount("copper_wire") == wire && inventory.GetCount("electric_motor") == motors;

        private IEnumerator CheckRelay()
        {
            tracker.RestoreState(null);
            Check(tracker.Step == RelayStep.FindOutpost, "Relay starts unfound");
            Check(!console.BeginContact(out _), "Can't contact a relay nobody repaired");
            Check(tracker.CompleteScan(RadioNeed.MilitaryRelay, Isolated), "Military relay scan finds one");
            Check(tracker.Step == RelayStep.Repair && !progression.IsDiscovered(tracker.RelayLocatedId), "Located, not discovered");

            Vector2Int site = ProceduralLandmarks.FindNearest(ProceduralLandmarks.KindMilitary, WorldLayout.BunkerCenter, seed, ObjectiveTracker.SearchRadiusChunks).Value;
            string siteId = ProceduralLandmarks.StableId(site, seed);
            RelayRepairPoint point = world.EnsureChunk(site).GetComponentInChildren<RelayRepairPoint>(true);
            Check(point != null && point.landmarkId == siteId, "A repair point stands on the military landmark");
            Vector2Int otherKind = ProceduralLandmarks.FindNearest(ProceduralLandmarks.KindFarm, WorldLayout.BunkerCenter, seed, ObjectiveTracker.SearchRadiusChunks).Value;
            Check(world.EnsureChunk(otherKind).GetComponentInChildren<RelayRepairPoint>(true) == null, "No repair point on a farm");

            foreach (var parts in new[] { (1, 4, 1), (2, 3, 1), (2, 4, 0), (0, 0, 0) })
            {
                SetRelayParts(parts.Item1, parts.Item2, parts.Item3);
                Check(point.InteractPrompt.Contains("Missing"), "Prompt names what's missing: " + point.InteractPrompt);
                point.Interact(player.gameObject);
                Check(RelayPartsAre(parts.Item1, parts.Item2, parts.Item3) && !tracker.RelayRepaired, $"{parts}: a missing part spends nothing");
            }
            SetRelayParts(5, 9, 2);
            Check(point.InteractPrompt.StartsWith("Press E"), "Prompt offers the repair");
            point.Interact(player.gameObject);
            Check(RelayPartsAre(3, 5, 1) && tracker.IsRelayRepairedAt(siteId) && tracker.Step == RelayStep.Contact, "Repair spends exactly 2 boards, 4 wire, 1 motor");
            point.Interact(player.gameObject);
            Check(RelayPartsAre(3, 5, 1), "Repairing again spends nothing");

            Vector2Int otherSite = ProceduralLandmarks.FindNearest(ProceduralLandmarks.KindMilitary, WorldLayout.BunkerCenter, seed, ObjectiveTracker.SearchRadiusChunks, id => id == siteId).Value;
            RelayRepairPoint other = world.EnsureChunk(otherSite).GetComponentInChildren<RelayRepairPoint>(true);
            Check(other != null && other.InteractPrompt.Contains("another outpost"), "A second outpost is dead: " + (other != null ? other.InteractPrompt : "none"));
            other.Interact(player.gameObject);
            Check(RelayPartsAre(3, 5, 1) && tracker.RelayRepairedId == siteId, "Only once globally");

            world.RestoreWorld(seed, world.CaptureWorld(), true);
            yield return null;
            yield return null;
            RelayRepairPoint rebuilt = world.EnsureChunk(site).GetComponentInChildren<RelayRepairPoint>(true);
            Check(rebuilt != null && rebuilt != point && rebuilt.InteractPrompt.Contains("repaired"), "Repair survives a chunk rebuild: " + (rebuilt != null ? rebuilt.InteractPrompt : "none"));

            string saved = tracker.CaptureState();
            tracker.RestoreState(null);
            Check(tracker.Step == RelayStep.FindOutpost, "A save with no section resets to a new game");
            tracker.RestoreState("not json{");
            Check(tracker.Step == RelayStep.FindOutpost, "A damaged section resets without throwing");
            tracker.RestoreState(saved);
            Check(tracker.Step == RelayStep.Contact && tracker.RelayRepairedId == siteId && tracker.HasWaypoint, "Steps and waypoint survive capture and restore");

            int parts0 = inventory.GetCount("expedition_parts"), ammo0 = inventory.GetCount("ammo_rifle"), cells0 = inventory.GetCount("energy_cell");
            Check(console.BeginContact(out _), "Contact starts once repaired");
            var secondObject = new GameObject("ProbeRadioSecond");
            secondObject.transform.position = Isolated + Vector3.forward * 4f;
            var second = secondObject.AddComponent<RadioConsolePiece>();
            Check(second.BeginContact(out _), "A second console may also be calling");
            Power(true, 5f);
            console.DebugAdvance(30f);
            Power(false);
            console.DebugAdvance(100f);
            Check(Mathf.Approximately(console.Elapsed, 30f) && !tracker.RelayRewarded, "Contact keeps progress through a power cut");
            Power(true, 5f);
            console.DebugAdvance(30.1f);
            Check(tracker.RelayRewarded && tracker.Step == RelayStep.Done, "Contact completes after 60 s of powered time");
            bool rewarded = inventory.GetCount("expedition_parts") == parts0 + 3 && inventory.GetCount("ammo_rifle") == ammo0 + 40 && inventory.GetCount("energy_cell") == cells0 + 4;
            Check(rewarded, "Reward: 3 parts, 40 rifle rounds, 4 cells");
            second.DebugAdvance(70f);
            Check(second.CurrentOperation == RadioConsolePiece.Operation.None && inventory.GetCount("expedition_parts") == parts0 + 3, "A second console finishing later gets nothing");
            Destroy(secondObject);
            Check(!console.BeginContact(out _) && !tracker.CompleteRelayContact(inventory) && inventory.GetCount("expedition_parts") == parts0 + 3, "The reward can't be taken twice");
            Check(tracker.RevealedLandmarks.Count == ObjectiveTracker.RelayRevealCount && tracker.RevealedLandmarks.Distinct().Count() == 3 &&
                tracker.RevealedLandmarks.All(id => ProceduralLandmarks.TryParseStableId(id, out Vector2Int v, out _) && ProceduralLandmarks.TryGetKind(v, seed, out _)), "Three real landmarks are revealed: " + string.Join(" ", tracker.RevealedLandmarks));
            string done = tracker.CaptureState();
            tracker.RestoreState(null);
            tracker.RestoreState(done);
            Check(tracker.Step == RelayStep.Done && tracker.RevealedLandmarks.Count == 3 && !tracker.CompleteRelayContact(inventory), "Restoring after the reward can't grant it again");
            yield return null;
        }

        private IEnumerator CheckHudAndGoals()
        {
            tracker.RestoreState(null);
            progression.Restore(0, new List<string> { "landmark:1:0:0" });
            inventory.SetCount(progression.expeditionParts.id, 1);
            List<string> shortage = progression.DescribeShortage(0, true);
            Check(shortage.Count > 0 && shortage[0].Contains("Missing 2 ") && shortage[0].Contains(ExpeditionProgression.UpgradeNames[0]) && shortage[0].Contains("factory"), "Upgrade shortage is exact: " + shortage.FirstOrDefault());
            Check(tracker.GoalText().Contains("Missing 2 "), "Goal text comes from the real shortage: " + tracker.GoalText());
            Check(progression.DescribeShortage(2).Any(s => s.Contains("Discover 2 more")), "Missing sites are named too");
            inventory.SetCount(progression.expeditionParts.id, 100);
            inventory.SetCount(progression.wood.id, 100);
            Check(progression.DescribeShortage(0).Count == 0 && tracker.GoalText().Contains("Install"), "Goal says to install once affordable");
            progression.Restore(7, new List<string>());
            SetRelayParts(0, 0, 0);
            Check(tracker.GoalText().Contains("Military relay"), "With upgrades done, the goal is the relay: " + tracker.GoalText());

            Vector3 p = player.transform.position;
            tracker.SetWaypoint(p + player.transform.forward * 100f, "Probe far");
            yield return new WaitForSeconds(.6f);
            Check(tracker.HasWaypoint, "Waypoint at 100 m stays");
            Check(tracker.HudVisible && tracker.HudText.Contains("Probe far") && tracker.HudText.Contains("AHEAD"), "HUD shows name, distance and bearing: " + tracker.HudText.Replace('\n', '/'));
            Canvas hud = FindObjectsByType<Canvas>(FindObjectsSortMode.None).FirstOrDefault(canvas => canvas.name == "ObjectivesHUD");
            Check(hud != null && hud.GetComponent<GraphicRaycaster>() == null && hud.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), "HUD canvas is passive");
            tracker.SetWaypoint(p - player.transform.forward * 100f, "Probe behind");
            yield return new WaitForSeconds(.4f);
            Check(tracker.HudText.Contains("BEHIND"), "Bearing follows the player's facing");

            lastMessage = "";
            tracker.SetWaypoint(p + Vector3.right * 10f, "Probe near");
            yield return new WaitForSeconds(.6f);
            Check(!tracker.HasWaypoint && lastMessage.Contains("Probe near"), "Waypoint clears on arrival with a message: " + lastMessage);

            // Walking into an outpost counts as finding it.
            Vector2Int site = ProceduralLandmarks.FindNearest(ProceduralLandmarks.KindMilitary, WorldLayout.BunkerCenter, seed, ObjectiveTracker.SearchRadiusChunks).Value;
            progression.DiscoverLandmark(ProceduralLandmarks.StableId(site, seed), "Probe outpost");
            yield return new WaitForSeconds(.4f);
            Check(tracker.Step == RelayStep.Repair, "Discovering an outpost advances the relay step");
            progression.Restore(0, new List<string>());
        }

        private IEnumerator CheckAtlas()
        {
            var atlas = FindFirstObjectByType<WorldMapUI>();
            Check(atlas != null, "Atlas exists");
            if (atlas == null) yield break;
            Vector3 p = player.transform.position;
            Vector2Int nearby = ProceduralLandmarks.FindNearest((int[])null, p, seed, 5).Value;
            Vector2Int far = new Vector2Int(WorldLayout.Coordinate(p).x + 200, WorldLayout.Coordinate(p).y);
            progression.DiscoverLandmark(ProceduralLandmarks.StableId(nearby, seed), "Probe nearby");
            tracker.SetWaypoint(ProceduralLandmarks.WorldCenter(far), "Probe far away");
            int loaded = world.LoadedChunkCount;
            atlas.Open();
            Check(world.LoadedChunkCount == loaded, "Opening the atlas generates no chunks");
            Check(ModalUI.IsOpen, "Atlas is modal");
            Transform canvas = atlas.transform.Find("WorldMapCanvas");
            RectTransform map = canvas.GetComponentsInChildren<RectTransform>(true).First(r => r.name == "Map");
            RectTransform bunker = canvas.GetComponentsInChildren<RectTransform>(true).First(r => r.name == "Bunker");
            Vector2 expected = new Vector2(WorldLayout.BunkerCenter.x - p.x, WorldLayout.BunkerCenter.z - p.z) / WorldMapUI.ViewSpan * WorldMapUI.MapSize;
            Check(Vector2.Distance(bunker.anchoredPosition, expected) < .01f && bunker.gameObject.activeSelf, $"Bunker marker is at the real bunker, not the world origin ({bunker.anchoredPosition})");
            RectTransform waypoint = canvas.GetComponentsInChildren<RectTransform>(true).First(r => r.name == "Waypoint");
            Check(waypoint.gameObject.activeSelf && Mathf.Abs(waypoint.anchoredPosition.x) <= WorldMapUI.MapSize * .5f && Mathf.Abs(waypoint.anchoredPosition.y) <= WorldMapUI.MapSize * .5f && Mathf.Abs(waypoint.anchoredPosition.x) >= WorldMapUI.MapSize * .5f - 93f, $"Offscreen waypoint is clamped to the edge ({waypoint.anchoredPosition})");
            Text site = canvas.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "Site0");
            Vector2 siteExpected = WorldMapUI.MapOffset(ProceduralLandmarks.WorldCenter(nearby), p);
            Check(site != null && site.gameObject.activeSelf && Vector2.Distance(site.rectTransform.anchoredPosition, siteExpected) < .01f && site.text.StartsWith("["), "Discovered landmark has a labelled marker: " + (site != null ? site.text : "none"));
            RectTransform closeRect = canvas.GetComponentsInChildren<RectTransform>(true).First(r => r.name == "Close");
            RectTransform legend = canvas.GetComponentsInChildren<RectTransform>(true).First(r => r.name == "Position");
            Check(!Overlaps(map, closeRect) && !Overlaps(legend, closeRect) && !Overlaps(map, legend), "Map, legend and Close button don't overlap");
            Check(canvas.GetComponentsInChildren<Graphic>(true).Where(g => g.transform.IsChildOf(map)).All(g => !g.raycastTarget), "Nothing on the map can take a click");
            string text = canvas.GetComponentsInChildren<Text>(true).First(t => t.name == "Position").text;
            Check(text.Contains("Home") && text.Contains("Probe far away"), "Legend gives home and the waypoint");
            atlas.Close();
            Check(!ModalUI.IsOpen && Time.timeScale == 1f, "Atlas closes cleanly");
            progression.Restore(0, new List<string>());
            tracker.ClearWaypoint();
            yield return null;
        }

        private static bool Overlaps(RectTransform a, RectTransform b)
        {
            var ca = new Vector3[4];
            var cb = new Vector3[4];
            a.GetWorldCorners(ca);
            b.GetWorldCorners(cb);
            return ca[0].x < cb[2].x && cb[0].x < ca[2].x && ca[0].y < cb[2].y && cb[0].y < ca[2].y;
        }

        private IEnumerator CheckVictoryAndSave()
        {
            tracker.RestoreState(null);
            Check(!RadioBeaconPiece.VictoryAnnounced, "No victory in a new game");
            var beaconObject = new GameObject("ProbeBeacon");
            beaconObject.AddComponent<RadioBeaconPiece>().Interact(player.gameObject);
            Destroy(beaconObject);
            Check(RadioBeaconPiece.VictoryAnnounced, "Beacon announces victory");
            tracker.SetWaypoint(new Vector3(5000f, 0f, 5000f), "Saved waypoint", "landmark:1:2:3");

            var save = FindFirstObjectByType<SaveManager>();
            string path = Path.Combine(Application.temporaryCachePath, "objectives-probe-" + Guid.NewGuid().ToString("N") + ".json");
            Check(Invoke(save, "TrySaveToPath", path), "Save to a temp path");
            tracker.RestoreState(null);
            Check(!RadioBeaconPiece.VictoryAnnounced, "State cleared before the load");
            Check(Invoke(save, "TryLoadFromPath", path), "Load it back");
            yield return null;
            yield return null;
            Check(RadioBeaconPiece.VictoryAnnounced, "Victory survives a real save and load");
            Check(tracker.HasWaypoint && tracker.WaypointTitle == "Saved waypoint", "Waypoint survives a real save and load");
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }

        private static bool Invoke(SaveManager save, string method, string path)
        {
            MethodInfo info = typeof(SaveManager).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return info != null && (bool)info.Invoke(save, new object[] { path });
        }

        private void Check(bool condition, string message)
        {
            if (!condition) failures++;
            Debug.Log((condition ? "[ObjectivesProbe] PASS: " : "[ObjectivesProbe] FAIL: ") + message);
        }
    }
}
