using System;
using System.Collections.Generic;
using System.Text;
using SheltreSim.Audio;
using SheltreSim.Core;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Saving;
using SheltreSim.Survival;
using SheltreSim.UI;
using SheltreSim.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SheltreSim.Objectives
{
    public enum RadioNeed { Fuel, Materials, Parts, Medicine, Food, MilitaryRelay }
    public enum RelayStep { FindOutpost, Repair, Contact, Done }

    /// <summary>
    /// Everything the player is currently trying to do: one waypoint, the military relay's saved steps and the
    /// victory flag. Not a quest framework: the relay is the one completable goal. Creates itself after every scene load.
    /// </summary>
    public sealed class ObjectiveTracker : MonoBehaviour, ISaveableSystem
    {
        public const int SearchRadiusChunks = 40;
        public const float ArrivalRadius = 15f;
        public const int RelayRevealCount = 3;
        public static readonly (string id, int amount)[] RelayCost = { ("circuit_board", 2), ("copper_wire", 4), ("electric_motor", 1) };
        public static readonly (string id, int amount)[] RelayReward = { ("expedition_parts", 3), ("ammo_rifle", 40), ("energy_cell", 4) };

        [Serializable]
        private class State
        {
            public bool hasWaypoint;
            public float waypointX, waypointZ;
            public string waypointTitle, waypointLandmark;
            public string lastPinned;       // the same landmark can't come back twice in a row
            public string relayLocated, relayRepaired;
            public bool relayRewarded;
            public bool victory;
            public List<string> revealed = new List<string>();
        }

        private static ObjectiveTracker instance;
        private static bool hooked;
        private State state = new State();
        private Canvas canvas;
        private GameObject panel;
        private Text label;
        private Transform player;
        private SurvivalStats stats;
        private Inventory inventory;
        private GameStateManager gameState;
        private float nextRefresh;
        private int checkedDiscoveries = -1;

        public static ObjectiveTracker Instance => instance;
        public static bool Victory => instance != null && instance.state.victory;

        public string SaveKey => "objectives";
        public bool HasWaypoint => state.hasWaypoint;
        public Vector3 WaypointPosition => new Vector3(state.waypointX, 0f, state.waypointZ);
        public string WaypointTitle => state.waypointTitle ?? "";
        public string WaypointLandmark => state.waypointLandmark ?? "";
        public string RelayLocatedId => state.relayLocated ?? "";
        public string RelayRepairedId => state.relayRepaired ?? "";
        public bool RelayRepaired => !string.IsNullOrEmpty(state.relayRepaired);
        public bool RelayRewarded => state.relayRewarded;
        public IReadOnlyList<string> RevealedLandmarks => state.revealed;
        public Transform PlayerTransform { get { FindPlayer(); return player; } }
        public string HudText => label != null && panel != null && panel.activeSelf ? label.text : "";
        public bool HudVisible => canvas != null && canvas.enabled && panel != null && panel.activeSelf;

        public RelayStep Step => state.relayRewarded ? RelayStep.Done
            : RelayRepaired ? RelayStep.Contact
            : !string.IsNullOrEmpty(state.relayLocated) ? RelayStep.Repair : RelayStep.FindOutpost;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; hooked = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneLoaded += (scene, mode) => Ensure();
            }
            Ensure();
        }

        public static ObjectiveTracker Ensure()
        {
            if (instance == null) new GameObject("ObjectiveTracker").AddComponent<ObjectiveTracker>();
            return instance;
        }

        /// <summary>Set by RaidDirector when the beacon's broadcast completes: saved with the game, so it comes back on load and resets on a new one. Ends the raids for good.</summary>
        public static void MarkVictory() => Ensure().state.victory = true;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            if (GetComponent<VictoryLedger>() == null) gameObject.AddComponent<VictoryLedger>();
            SaveableSystems.Register(this);
        }

        private void OnDestroy()
        {
            SaveableSystems.Unregister(this);
            if (instance == this) instance = null;
        }

        public string CaptureState() => JsonUtility.ToJson(state);

        public void RestoreState(string json)
        {
            State restored = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { restored = JsonUtility.FromJson<State>(json); }
                catch (ArgumentException) { restored = null; }
            }
            state = restored ?? new State();
            if (state.revealed == null) state.revealed = new List<string>();
            if (float.IsNaN(state.waypointX) || float.IsNaN(state.waypointZ) ||
                float.IsInfinity(state.waypointX) || float.IsInfinity(state.waypointZ)) state.hasWaypoint = false;
            checkedDiscoveries = -1;
            nextRefresh = 0f;
        }

        public void SetWaypoint(Vector3 position, string title, string landmarkId = null)
        {
            state.hasWaypoint = true;
            state.waypointX = position.x;
            state.waypointZ = position.z;
            state.waypointTitle = title;
            state.waypointLandmark = landmarkId;
            nextRefresh = 0f;
        }

        public void ClearWaypoint()
        {
            state.hasWaypoint = false;
            state.waypointTitle = state.waypointLandmark = null;
            nextRefresh = 0f;
        }

        public static string NeedName(RadioNeed need)
        {
            switch (need)
            {
                case RadioNeed.Fuel: return "Fuel";
                case RadioNeed.Materials: return "Building materials";
                case RadioNeed.Parts: return "Expedition parts";
                case RadioNeed.Medicine: return "Medicine";
                case RadioNeed.Food: return "Food";
                default: return "Military relay";
            }
        }

        public static int[] KindsFor(RadioNeed need)
        {
            switch (need)
            {
                case RadioNeed.Fuel: return new[] { ProceduralLandmarks.KindFuel };
                case RadioNeed.Materials: return new[] { ProceduralLandmarks.KindFactory };
                case RadioNeed.Parts: return new[] { ProceduralLandmarks.KindFactory, ProceduralLandmarks.KindBunker, ProceduralLandmarks.KindMilitary };
                case RadioNeed.Medicine: return new[] { ProceduralLandmarks.KindHospital };
                case RadioNeed.Food: return new[] { ProceduralLandmarks.KindFarm };
                default: return new[] { ProceduralLandmarks.KindMilitary };
            }
        }

        /// <summary>
        /// A finished scan: pins the nearest undiscovered match, never marking it discovered and never the one it
        /// returned last time. False when nothing matches in range, which costs nothing but the time spent.
        /// </summary>
        public bool CompleteScan(RadioNeed need, Vector3 from, int radiusChunks = SearchRadiusChunks)
        {
            ProceduralWorld world = ProceduralWorld.Active;
            if (world == null) return false;
            ExpeditionProgression progression = ExpeditionProgression.Active;
            string last = state.lastPinned;
            Vector2Int? found = ProceduralLandmarks.FindNearest(KindsFor(need), from, world.Seed, radiusChunks,
                id => id == last || (progression != null && progression.IsDiscovered(id)));
            if (!found.HasValue || !ProceduralLandmarks.TryGetKind(found.Value, world.Seed, out int kind))
            {
                GameAudio.PlayAt(Sfx.RadioStatic, from, 0.5f, 14f);
                GameMessageBus.Post($"Scan for {NeedName(need)}: only static. Nothing new within {radiusChunks * WorldLayout.ChunkSize / 1000f:0.#} km.");
                return false;
            }
            string landmark = ProceduralLandmarks.StableId(found.Value, world.Seed);
            Vector3 position = ProceduralLandmarks.WorldCenter(found.Value);
            string title = ProceduralLandmarks.Title(kind);
            SetWaypoint(position, title, landmark);
            state.lastPinned = landmark;
            if (kind == ProceduralLandmarks.KindMilitary && Step == RelayStep.FindOutpost) state.relayLocated = landmark;
            GameAudio.PlayAt(Sfx.RadioPing, from, 0.9f, 25f);
            GameAudio.Play2D(Sfx.RadioPing, 0.6f);
            Vector3 offset = position - from;
            offset.y = 0f;
            GameMessageBus.Post($"Radio fix: {title}, {offset.magnitude:0} m {Compass(from, position)} of the console. Waypoint set.");
            return true;
        }

        public bool IsRelayRepairedAt(string landmarkId) => RelayRepaired && state.relayRepaired == landmarkId;

        public static string RelayCostText()
        {
            var text = new StringBuilder();
            foreach (var line in RelayCost)
            {
                if (text.Length > 0) text.Append(", ");
                text.Append(line.amount).Append(' ').Append(SheltreSim.Crafting.ItemCatalog.NameOf(line.id));
            }
            return text.ToString();
        }

        /// <summary>What's still short for the repair, or an empty string once the player can pay for it.</summary>
        public string RelayMissingText(Inventory items)
        {
            var text = new StringBuilder();
            foreach (var line in RelayCost)
            {
                int have = items != null ? items.GetCount(line.id) : 0;
                if (have >= line.amount) continue;
                if (text.Length > 0) text.Append(", ");
                text.Append(line.amount - have).Append(' ').Append(SheltreSim.Crafting.ItemCatalog.NameOf(line.id));
            }
            return text.ToString();
        }

        /// <summary>Atomic: one missing part spends nothing. Works on any military landmark, but only once in a game.</summary>
        public bool TryRepairRelay(string landmarkId, Inventory items, out string result)
        {
            if (string.IsNullOrEmpty(landmarkId) || items == null) { result = "This relay can't be repaired now."; return false; }
            if (RelayRepaired)
            {
                result = state.relayRepaired == landmarkId ? "The relay is already repaired." : "The network already runs through another outpost.";
                return false;
            }
            string missing = RelayMissingText(items);
            if (!string.IsNullOrEmpty(missing)) { result = "Relay repair needs " + RelayCostText() + ". Missing " + missing + "."; return false; }
            foreach (var line in RelayCost) items.SetCount(line.id, items.GetCount(line.id) - line.amount);
            state.relayLocated = state.relayRepaired = landmarkId;
            GameAudio.Play2D(Sfx.ObjectiveDone, 0.8f);
            result = "Relay repaired. Go home and contact it from a powered Radio Console.";
            return true;
        }

        /// <summary>The one-time reward. A repeat, or a second console finishing later, gets false and nothing else.</summary>
        public bool CompleteRelayContact(Inventory items)
        {
            if (Step != RelayStep.Contact || items == null) return false;
            state.relayRewarded = true;
            foreach (var line in RelayReward) items.Add(line.id, line.amount);
            int revealed = RevealNearest();
            GameAudio.Play2D(Sfx.ObjectiveDone, 0.9f);
            GameMessageBus.Post($"Relay online. Received 3 Expedition Parts, 40 Rifle Rounds and 4 Energy Cells. {revealed} sites added to your atlas.");
            return true;
        }

        private int RevealNearest()
        {
            ProceduralWorld world = ProceduralWorld.Active;
            if (world == null) return 0;
            ExpeditionProgression progression = ExpeditionProgression.Active;
            int count = 0;
            for (int i = 0; i < RelayRevealCount; i++)
            {
                Vector2Int? found = ProceduralLandmarks.FindNearest((int[])null, WorldLayout.BunkerCenter, world.Seed, SearchRadiusChunks,
                    id => state.revealed.Contains(id) || (progression != null && progression.IsDiscovered(id)));
                if (!found.HasValue) break;
                state.revealed.Add(ProceduralLandmarks.StableId(found.Value, world.Seed));
                count++;
            }
            return count;
        }

        /// <summary>Same words and thresholds as SurvivalHUD's home bearing.</summary>
        public static string Bearing(Vector3 forward, Vector3 direction)
        {
            float angle = Vector3.SignedAngle(forward, direction, Vector3.up);
            return Mathf.Abs(angle) < 25f ? "AHEAD" : Mathf.Abs(angle) > 150f ? "BEHIND" : angle > 0f ? "RIGHT" : "LEFT";
        }

        private static readonly string[] CompassNames = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
        public static string Compass(Vector3 from, Vector3 to)
        {
            float degrees = Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
            int index = Mathf.RoundToInt(degrees / 45f) % 8;
            return CompassNames[index < 0 ? index + 8 : index];
        }

        /// <summary>The current suggestion, always from real state. At most two lines. Empty when nothing is left to do.</summary>
        public string GoalText()
        {
            FindPlayer();
            var lines = new List<string>();
            SheltreSim.Raids.RaidDirector raids = SheltreSim.Raids.RaidDirector.Active;
            if (raids != null && raids.BroadcastActive)
                return "Defend the Radio Beacon and keep it powered until the broadcast ends.";
            RelayStep step = Step;
            if (step == RelayStep.Contact) lines.Add("Relay repaired: contact it from a powered Radio Console.");
            ExpeditionProgression progression = ExpeditionProgression.Active;
            if (progression != null)
                for (int i = 0; i < 3; i++)
                {
                    if (progression.HasUpgrade(i)) continue;
                    List<string> shortage = progression.DescribeShortage(i, true);
                    lines.Add(shortage.Count > 0 ? shortage[0] : "Install " + ExpeditionProgression.UpgradeNames[i] + " at the Expedition Workshop.");
                    break;
                }
            if (step == RelayStep.Repair)
            {
                string missing = RelayMissingText(inventory);
                lines.Add(string.IsNullOrEmpty(missing) ? "You have the parts: repair the relay at a Military relay outpost."
                    : "Repair the relay at a Military relay outpost: missing " + missing + ".");
            }
            else if (step == RelayStep.FindOutpost)
                lines.Add(RadioConsolePiece.AnyPlaced ? "Find a Military relay outpost: scan from a powered Radio Console."
                    : "Build a Radio Console and power it to find a Military relay outpost.");
            if (lines.Count == 0 && !state.victory) lines.Add("Build the Radio Beacon at your bunker, then defend it powered through a 5 minute broadcast.");
            if (lines.Count > 2) lines.RemoveRange(2, lines.Count - 2);
            return string.Join("\n", lines);
        }

        private void FindPlayer()
        {
            if (player != null) return;
            var controller = FindFirstObjectByType<FirstPersonController>();
            if (controller == null) return;
            player = controller.transform;
            stats = controller.GetComponent<SurvivalStats>();
            inventory = controller.GetComponent<Inventory>();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;
            FindPlayer();
            if (player == null) return;
            bool alive = stats == null || !stats.IsDead;
            if (gameState == null) gameState = FindFirstObjectByType<GameStateManager>();
            bool survival = gameState == null || gameState.CurrentPhase == GamePhase.Survival;
            if (alive && survival && Time.timeScale > 0f)
            {
                NoticeDiscoveredOutpost();
                CheckArrival();
            }
            RefreshHud(survival);
        }

        // Walking into an outpost finds it just as well as the radio does.
        private void NoticeDiscoveredOutpost()
        {
            ExpeditionProgression progression = ExpeditionProgression.Active;
            if (progression == null || progression.DiscoveryCount == checkedDiscoveries) return;
            checkedDiscoveries = progression.DiscoveryCount;
            if (Step != RelayStep.FindOutpost) return;
            foreach (string id in progression.Discoveries)
            {
                if (!ProceduralLandmarks.TryParseStableId(id, out Vector2Int coordinate, out int seed)) continue;
                if (!ProceduralLandmarks.TryGetKind(coordinate, seed, out int kind) || kind != ProceduralLandmarks.KindMilitary) continue;
                state.relayLocated = id;
                GameMessageBus.Post("This outpost has a broken relay. It needs " + RelayCostText() + ".");
                return;
            }
        }

        private void CheckArrival()
        {
            if (!state.hasWaypoint) return;
            Vector3 offset = WaypointPosition - player.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > ArrivalRadius * ArrivalRadius) return;
            string title = WaypointTitle;
            ClearWaypoint();
            GameAudio.Play2D(Sfx.RadioPing, 0.5f);
            GameMessageBus.Post($"Reached {title}. Waypoint cleared.");
        }

        private void RefreshHud(bool survival)
        {
            if (canvas == null) BuildHud();
            var text = new StringBuilder();
            if (state.hasWaypoint)
            {
                Vector3 direction = WaypointPosition - player.position;
                direction.y = 0f;
                text.Append("<color=#A1DEA8><b>WAYPOINT</b>  ").Append(WaypointTitle).Append("\n")
                    .Append(direction.magnitude.ToString("0")).Append(" m ").Append(Bearing(player.forward, direction)).Append("</color>");
            }
            string goal = survival ? GoalText() : "";
            if (goal.Length > 0)
            {
                if (text.Length > 0) text.Append('\n');
                text.Append(goal);
            }
            label.text = text.ToString();
            panel.SetActive(survival && text.Length > 0);
        }

        private void BuildHud()
        {
            canvas = GameUI.CreateCanvas(transform, "ObjectivesHUD", 1, false);
            // Under SurvivalHUD's objective card, which ends 266 down the right edge.
            panel = GameUI.Panel(canvas.transform, "Objectives", Vector2.one, Vector2.one,
                new Vector2(-24f, -274f), new Vector2(380f, 158f), GameUI.PanelColor).gameObject;
            label = GameUI.Label(panel.transform, "Text", "", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -10f), new Vector2(344f, 138f), 16);
            panel.SetActive(false);
        }

        private void LateUpdate()
        {
            if (canvas != null) canvas.enabled = !ModalUI.IsOpen && (stats == null || !stats.IsDead);
        }
    }
}
