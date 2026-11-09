using System;
using System.Collections.Generic;
using SheltreSim.Audio;
using SheltreSim.Building;
using SheltreSim.Core;
using SheltreSim.Crafting;
using SheltreSim.Enemies;
using SheltreSim.Interaction;
using SheltreSim.Objectives;
using SheltreSim.Player;
using SheltreSim.Saving;
using SheltreSim.Survival;
using SheltreSim.UI;
using SheltreSim.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SheltreSim.Raids
{
    /// <summary>
    /// Announced attacks on the active base. Only in Survival with a finished bunker. Runs on game time and stops while
    /// the player is dead. If the player is far from home when one falls due, it waits for them, since a base nobody
    /// can see isn't destroyed in the abstract. Also owns death loot and door damage in its save section ("raids").
    /// </summary>
    public sealed class RaidDirector : MonoBehaviour, ISaveableSystem
    {
        public const float FirstRaidSeconds = 900f;
        public const float WarningSeconds = 60f;
        public const float AlarmWarningSeconds = 120f;
        public const float HomeRange = 200f;
        public const float ReturnFuseSeconds = 25f;
        public const float SpawnRingMin = 35f, SpawnRingMax = 50f;
        public const int MaxRaiders = 14;
        /// <summary>A raider this long without progress, out of the player's sight, gives up, so nothing can hold the next raid back forever.</summary>
        public const float RetreatAfterStuckSeconds = 90f;

        public enum Phase { Countdown, Pending, Active }

        public static RaidDirector Active { get; private set; }
        private static bool hooked;

        public Phase CurrentPhase { get; private set; } = Phase.Countdown;
        public float Countdown { get; private set; } = FirstRaidSeconds;
        public int RaidNumber { get; private set; } = 1;
        public int RaidersLeft => actors.Count;
        public IReadOnlyList<RaidAgent> Actors => actors;
        public bool WarningActive { get; private set; }
        public int RaidsSurvived { get; private set; }

        public string SaveKey => "raids";

        private readonly List<RaidAgent> actors = new List<RaidAgent>();
        private Transform actorRoot;
        private FirstPersonController player;
        private SurvivalStats stats;
        private GameStateManager state;
        private Text banner;
        private GameObject bannerPanel;
        private float nextHorn;
        private float pendingFuse = -1f;
        private bool announcedPending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
            hooked = false;
        }

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

        private static void Ensure()
        {
            if (Active == null) new GameObject("RaidDirector").AddComponent<RaidDirector>();
        }

        private void Awake()
        {
            if (Active != null && Active != this) { Destroy(gameObject); return; }
            Active = this;
            actorRoot = new GameObject("RaidActors").transform;
            actorRoot.SetParent(transform, false);
            var bags = new GameObject("LootBags").transform;
            bags.SetParent(transform, false);
            LootBagSystem.SetRoot(bags);
            LootBagSystem.Clear();
            LootBagSystem.NextId = 1;
            SaveableSystems.Register(this);
            EnemyAI.AnyDeath += OnAnyDeath;
            StructureHealth.Destroyed += OnStructureDestroyed;
            BuildBanner();
        }

        private void OnDestroy()
        {
            if (Active != this) return;
            Active = null;
            SaveableSystems.Unregister(this);
            EnemyAI.AnyDeath -= OnAnyDeath;
            StructureHealth.Destroyed -= OnStructureDestroyed;
        }

        private void BuildBanner()
        {
            Canvas canvas = GameUI.CreateCanvas(transform, "RaidBannerCanvas", 40, false);
            Image panel = GameUI.Panel(canvas.transform, "Banner", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(640f, 54f), new Color(0.2f, 0.03f, 0.02f, 0.82f));
            bannerPanel = panel.gameObject;
            banner = GameUI.Label(panel.transform, "Text", "", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 46f), 28, GameUI.Danger, TextAnchor.MiddleCenter, true);
            bannerPanel.SetActive(false);
        }

        // ---------- The clock ----------

        private bool FindScene()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
                if (player != null) stats = player.GetComponent<SurvivalStats>();
            }
            if (state == null) state = FindFirstObjectByType<GameStateManager>();
            return player != null && state != null;
        }

        private bool SystemLive =>
            state.CurrentPhase == GamePhase.Survival && HomeBase.Active != null && HomeBase.Active.BunkerComplete && ProceduralWorld.Active != null;

        private float PlayerDistanceHome => Vector3.Distance(player.transform.position, RaidRoute.StairHeadCenter);

        private void Update()
        {
            UpdateBanner();
            if (Time.timeScale <= 0f || !FindScene() || !SystemLive) return;
            if (stats != null && (stats.IsDead || stats.health <= 0f)) return;
            float dt = Time.deltaTime;

            if (BroadcastActive)
            {
                TickBroadcast(dt);
                return;
            }
            // Attackers left over from a broadcast hold the ordinary clock until they're dead or gone.
            if (holdForLeftovers)
            {
                actors.RemoveAll(a => a == null);
                RetreatStuck();
                if (actors.Count > 0) return;
                holdForLeftovers = false;
            }
            // After a successful broadcast raids are over for good. A raid already under way (an old save) still plays out.
            if (ObjectiveTracker.Victory && CurrentPhase != Phase.Active)
            {
                WarningActive = false;
                return;
            }

            switch (CurrentPhase)
            {
                case Phase.Countdown:
                    Countdown -= dt;
                    float warning = HasPoweredAlarm() ? AlarmWarningSeconds : WarningSeconds;
                    if (!WarningActive && Countdown <= warning && PlayerDistanceHome <= HomeRange) BeginWarning();
                    if (Countdown <= 0f)
                    {
                        if (PlayerDistanceHome <= HomeRange) StartRaid(RaidNumber);
                        else
                        {
                            CurrentPhase = Phase.Pending;
                            WarningActive = false;
                            pendingFuse = -1f;
                            announcedPending = false;
                        }
                    }
                    break;
                case Phase.Pending:
                    if (PlayerDistanceHome > HomeRange) break;
                    if (!announcedPending)
                    {
                        announcedPending = true;
                        pendingFuse = ReturnFuseSeconds;
                        GameMessageBus.Post("Raiders are massing near your bunker.");
                        GameAudio.Play2D(Sfx.RaidHorn, 0.9f);
                    }
                    pendingFuse -= dt;
                    if (pendingFuse <= 0f) StartRaid(RaidNumber);
                    break;
                case Phase.Active:
                    TickActive();
                    break;
            }
        }

        private void BeginWarning()
        {
            WarningActive = true;
            GameAudio.Play2D(Sfx.RaidHorn, 1f);
            GameMessageBus.Post("Raiders have been spotted heading for your bunker. Get ready.");
        }

        private void TickActive()
        {
            actors.RemoveAll(a => a == null);
            RetreatStuck();
            if (actors.Count == 0)
            {
                FinishRaid();
                return;
            }
            if (Time.time >= nextHorn)
            {
                nextHorn = Time.time + 40f;
                GameAudio.Play2D(Sfx.RaidHorn, 0.45f);
            }
        }

        private void RetreatStuck()
        {
            for (int i = actors.Count - 1; i >= 0; i--)
            {
                RaidAgent agent = actors[i];
                if (agent.StuckSeconds < RetreatAfterStuckSeconds) continue;
                Vector3 eye = player.transform.position + Vector3.up * 1.6f;
                if (RaidSight.Clear(eye, agent.transform.position, player.transform, agent.transform)
                    && Vector3.Distance(eye, agent.transform.position) < 60f) continue;
                Debug.LogWarning($"[RaidDirector] {agent.Variant} gave up at {agent.transform.position}: {agent.LastStuckReason}");
                actors.RemoveAt(i);
                DespawnActor(agent);
            }
        }

        public static float IntervalAfter(int raidNumber) => Mathf.Max(360f, 600f - 30f * (raidNumber - 1));

        public static bool IsBossRaid(int number) => number % 5 == 0;

        private void FinishRaid()
        {
            int finished = RaidNumber;
            CurrentPhase = Phase.Countdown;
            WarningActive = false;
            RaidsSurvived++;
            Countdown = IntervalAfter(finished);
            RaidNumber = finished + 1;
            foreach (InteractableDoor door in FindObjectsByType<InteractableDoor>(FindObjectsSortMode.None)) door.RecoverAfterRaid();
            GameAudio.Play2D(Sfx.ObjectiveDone, 0.8f);
            GameMessageBus.Post(IsBossRaid(finished)
                ? $"Raid {finished} beaten and the Warlord is dead. Search the gold cache for your reward."
                : $"Raid {finished} repelled. Search the bags they dropped.");
        }

        // ---------- Starting a raid ----------

        /// <summary>Probes: starts a raid right now, whatever the clock says. Does nothing outside Survival or with a raid on.</summary>
        public void DebugStartRaid(int number)
        {
            if (!FindScene() || !SystemLive || CurrentPhase == Phase.Active || BroadcastActive) return;
            RaidNumber = Mathf.Max(1, number);
            StartRaid(RaidNumber);
        }

        /// <summary>Probes: one actor at a known spot, feet on the ground there. It isn't part of a raid's count until a raid is active.</summary>
        public RaidAgent DebugSpawn(RaidVariant variant, Vector3 feet)
        {
            RaidVariantSpec spec = RaidVariantSpec.Of(variant);
            return SpawnActor(variant, RaidNumber, feet + Vector3.up * (spec.Scale + 0.1f), Quaternion.identity, -1f, null);
        }

        public string BannerText => bannerPanel != null && bannerPanel.activeSelf ? banner.text : "";

        /// <summary>Probes: moves the clock, for example to just before the warning.</summary>
        public void DebugSetCountdown(float seconds)
        {
            Countdown = seconds;
            WarningActive = false;
        }

        private void StartRaid(int number)
        {
            int seed = ProceduralWorld.Active != null ? ProceduralWorld.Active.Seed : 0;
            List<RaidVariant> roster = RosterFor(number, seed);
            var random = new System.Random(unchecked(seed * 31 + number * 7919));
            var spawned = 0;
            foreach (RaidVariant variant in roster)
            {
                RaidVariantSpec spec = RaidVariantSpec.Of(variant);
                if (!FindSpawnPoint(random, spec, out Vector3 position))
                {
                    Debug.LogWarning($"[RaidDirector] No spawn point for {variant} in raid {number}.");
                    continue;
                }
                SpawnActor(variant, number, position, Quaternion.LookRotation(Flat(RaidRoute.GateOutside - position)), -1f, null);
                spawned++;
            }
            if (spawned == 0)
            {
                // No ground to stand on yet: try again shortly rather than announce nothing.
                Debug.LogWarning($"[RaidDirector] Raid {number} couldn't place anyone, retrying.");
                CurrentPhase = Phase.Countdown;
                Countdown = 10f;
                WarningActive = true;
                return;
            }
            CurrentPhase = Phase.Active;
            WarningActive = false;
            nextHorn = Time.time + 40f;
            GameAudio.Play2D(Sfx.RaidHorn, 1f);
            GameMessageBus.Post(IsBossRaid(number) ? $"Raid {number}: a Warlord leads this attack!" : $"Raid {number} has begun.");
            AlarmPiece.NotifyRaidBegan();
            Debug.Log($"[RaidDirector] Raid {number} started: {spawned} of {roster.Count} ({string.Join(", ", roster)}).");
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>Strength rises with the number. Seeded: the same world and raid number always send the same party.</summary>
        public static List<RaidVariant> RosterFor(int number, int seed)
        {
            var random = new System.Random(unchecked(seed * 17 + number * 104729));
            var roster = new List<RaidVariant>();
            int size = Mathf.Min(MaxRaiders, 2 + number);
            if (IsBossRaid(number)) roster.Add(RaidVariant.Warlord);
            int gunners = number >= 3 ? 1 + (number - 3) / 3 : 0;
            int brutes = number >= 4 ? 1 + (number - 4) / 4 : 0;
            int scavengers = number >= 2 ? 1 + (number - 2) / 5 : 0;
            for (int i = 0; i < gunners && roster.Count < size; i++) roster.Add(RaidVariant.Gunner);
            for (int i = 0; i < brutes && roster.Count < size; i++) roster.Add(RaidVariant.Brute);
            for (int i = 0; i < scavengers && roster.Count < size; i++) roster.Add(RaidVariant.Scavenger);
            while (roster.Count < size) roster.Add(RaidVariant.Raider);
            for (int i = roster.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (roster[i], roster[j]) = (roster[j], roster[i]);
            }
            return roster;
        }

        /// <summary>
        /// The south half of the ring. The stair head and the fence gate both face south, so from there it's a clear walk in, and there's
        /// no navmesh to get a raider round the lot from the north. Real ground only, not in a collider, not in the player's face.
        /// </summary>
        private bool FindSpawnPoint(System.Random random, RaidVariantSpec spec, out Vector3 position)
        {
            int seed = ProceduralWorld.Active.Seed;
            Vector3 fallback = Vector3.zero;
            bool hasFallback = false;
            for (int attempt = 0; attempt < 60; attempt++)
            {
                double angle = (random.NextDouble() * 140.0 - 70.0) * Math.PI / 180.0;
                float radius = SpawnRingMin + (float)random.NextDouble() * (SpawnRingMax - SpawnRingMin);
                float x = RaidRoute.StairHeadCenter.x + Mathf.Sin((float)angle) * radius;
                float z = RaidRoute.StairHeadCenter.z - Mathf.Cos((float)angle) * radius;
                if (WorldLayout.IsWater(x, z, seed, 2f) || !ProceduralWorld.Active.HasGroundAt(new Vector3(x, 0f, z))) continue;
                float terrain = WorldLayout.HeightAt(x, z, seed);
                if (!Physics.Raycast(new Vector3(x, terrain + 30f, z), Vector3.down, out RaycastHit ground, 60f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(ground.point.y - terrain) > 1f || ground.normal.y < 0.7f) continue; // a roof or a wreck, not the ground
                Vector3 feet = ground.point;
                float half = spec.BodyHeight * 0.5f - spec.BodyRadius;
                float centerAboveFeet = spec.BodyHeight * 0.5f + 0.1f;
                Vector3 center = feet + Vector3.up * centerAboveFeet;
                if (Physics.CheckCapsule(center - Vector3.up * half, center + Vector3.up * half, spec.BodyRadius + 0.05f, ~0, QueryTriggerInteraction.Ignore)) continue;
                Vector3 origin = feet + Vector3.up * (spec.Scale + 0.1f);
                if (TooCloseToPlayer(center)) continue;
                if (!hasFallback) { fallback = origin; hasFallback = true; }
                // Prefer a point that can see the gate along its whole width.
                Vector3 gate = RaidRoute.GateOutside + Vector3.up * centerAboveFeet;
                Vector3 toGate = gate - center;
                if (Physics.SphereCast(center, spec.BodyRadius, toGate.normalized, out RaycastHit block, toGate.magnitude, ~0, QueryTriggerInteraction.Ignore) && block.normal.y < 0.6f) continue;
                position = origin;
                return true;
            }
            position = fallback;
            return hasFallback;
        }

        private bool TooCloseToPlayer(Vector3 point)
        {
            Vector3 eye = player.transform.position + Vector3.up * 1.6f;
            return Vector3.Distance(eye, point) < 15f && RaidSight.Clear(eye, point, player.transform, null);
        }

        private RaidAgent SpawnActor(RaidVariant variant, int number, Vector3 position, Quaternion rotation, float health, List<LootItem> stolen)
        {
            GameObject prefab = ProceduralWorld.Active.assets.raiderPrefab;
            if (prefab == null) return null;
            GameObject obj = Instantiate(prefab, position, rotation, actorRoot);
            obj.name = "Raid_" + variant;
            obj.SetActive(true);
            var agent = obj.AddComponent<RaidAgent>();
            agent.Setup(variant, number, unchecked(ProceduralWorld.Active.Seed + number * 131 + actors.Count * 17));
            EnemyAI enemy = obj.GetComponent<EnemyAI>();
            GiveModel(agent, enemy);
            if (health > 0f) enemy.health = Mathf.Min(health, enemy.maxHealth);
            if (stolen != null)
                foreach (LootItem item in stolen) agent.AddStolen(item.id, item.count);
            actors.Add(agent);
            Physics.SyncTransforms();
            return agent;
        }

        /// <summary>
        /// The variant's GPT model in place of the raider's (RaidVariantSpec.ModelId), after Setup sized the capsule so the feet land
        /// on its floor. Only the looks change: the walk animator and the damage flash are pointed at the new parts. No model, no change.
        /// </summary>
        private static void GiveModel(RaidAgent agent, EnemyAI enemy)
        {
            if (!SheltreSim.Bestiary.EnemyModels.Apply(agent.gameObject, agent.Spec.ModelId, true)) return;
            enemy.RefreshVisuals();
            enemy.ApplyTint(agent.Spec.Tint);
            RaiderWalkAnimator walker = agent.GetComponent<RaiderWalkAnimator>();
            if (walker != null) walker.Rebind();
        }

        private static void DespawnActor(RaidAgent agent)
        {
            if (agent == null) return;
            agent.gameObject.SetActive(false);
            Destroy(agent.gameObject);
        }

        private bool HasPoweredAlarm() => AlarmPiece.AnyPoweredNear(RaidRoute.StairHeadCenter, 80f);

        // ---------- The rescue broadcast (the endgame) ----------
        // Five minutes of game time on a powered, standing Radio Beacon while three waves attack. The clock only runs while
        // the grid feeds the beacon, waves are due by time left, so they pause with it. Saved in this section with the
        // actors, and broadcastWave counts waves already sent, so a load can't send one twice.

        public const float BroadcastSeconds = 300f;
        public const int BroadcastWaves = 3;
        public const int MaxBroadcastActors = 18;
        /// <summary>A beacon at the bunker: the waves come by the raid route, so a beacon elsewhere isn't defended by anything.</summary>
        public const float BeaconHomeRange = 60f;

        public bool BroadcastActive { get; private set; }
        public float BroadcastRemaining { get; private set; }
        /// <summary>Waves already sent, 0 to 3.</summary>
        public int BroadcastWave { get; private set; }
        public bool BroadcastPowered { get; private set; }
        /// <summary>The broadcasting beacon, or null (also while it's being found again after a load).</summary>
        public RadioBeaconPiece BroadcastBeacon => BroadcastActive ? beacon : null;

        private RadioBeaconPiece beacon;
        private Vector3 beaconPosition;
        private float beaconMissingSeconds;
        private float nextWaveTry;
        private bool holdForLeftovers;
        private bool wasPowered;

        /// <summary>Starts the broadcast on this beacon, or says why not. Nothing changes on a refusal.</summary>
        public bool TryBeginBroadcast(RadioBeaconPiece target, out string reason)
        {
            reason = null;
            if (target == null || !target.isActiveAndEnabled) { reason = "The beacon can't broadcast right now."; return false; }
            if (ObjectiveTracker.Victory) { reason = "The rescue signal is out. Help is on its way."; return false; }
            if (BroadcastActive) { reason = "The broadcast is already running."; return false; }
            if (!FindScene() || !SystemLive) { reason = "Nobody would hear it yet. Finish your bunker and survive the bombs first."; return false; }
            if (stats != null && stats.IsDead) return false;
            if (Vector3.Distance(target.transform.position, WorldLayout.BunkerCenter) > BeaconHomeRange)
            {
                reason = "The beacon must stand at your bunker, where you can defend it.";
                return false;
            }
            actors.RemoveAll(a => a == null);
            if (CurrentPhase != Phase.Countdown || actors.Count > 0)
            {
                reason = "Deal with the raiders at your base before you start the broadcast.";
                return false;
            }
            BroadcastActive = true;
            BroadcastRemaining = BroadcastSeconds;
            BroadcastWave = 0;
            BroadcastPowered = false;
            beacon = target;
            beaconPosition = target.transform.position;
            beaconMissingSeconds = 0f;
            nextWaveTry = 0f;
            wasPowered = false;
            holdForLeftovers = false;
            WarningActive = false;
            nextHorn = Time.time + 40f;
            GameAudio.Play2D(Sfx.RadioPing, 0.9f);
            GameMessageBus.Post($"Broadcast started. Keep the beacon powered ({target.powerKw:0.#} kW) and standing for 5 minutes. Everyone out there heard it.");
            return true;
        }

        private void TickBroadcast(float dt)
        {
            actors.RemoveAll(a => a == null);
            RetreatStuck();
            if (!ResolveBeacon(dt)) return;

            BroadcastPowered = beacon.Transmitting;
            if (BroadcastPowered != wasPowered)
            {
                if (wasPowered) GameMessageBus.Post("The beacon lost power. The broadcast is paused.");
                else if (BroadcastRemaining < BroadcastSeconds) GameMessageBus.Post("Power is back. The broadcast continues.");
                wasPowered = BroadcastPowered;
            }
            if (BroadcastPowered) BroadcastRemaining = Mathf.Max(0f, BroadcastRemaining - dt);

            if (BroadcastWave < WavesDue(BroadcastRemaining) && Time.time >= nextWaveTry) SendWave(BroadcastWave + 1);
            if (BroadcastRemaining <= 0f)
            {
                CompleteBroadcast();
                return;
            }
            if (actors.Count > 0 && Time.time >= nextHorn)
            {
                nextHorn = Time.time + 40f;
                GameAudio.Play2D(Sfx.RaidHorn, 0.45f);
            }
        }

        /// <summary>Wave 1 at once, wave 2 with 3:20 left, wave 3 with 1:40 left.</summary>
        public static int WavesDue(float remaining)
        {
            float elapsed = BroadcastSeconds - remaining;
            return Mathf.Clamp(1 + Mathf.FloorToInt(elapsed / (BroadcastSeconds / BroadcastWaves)), 1, BroadcastWaves);
        }

        /// <summary>
        /// The beacon reference, found again by position after a load (pieces come back as new objects). Gone for a second,
        /// say through dismantling, means the broadcast failed. Destruction by damage fails it at once (OnStructureDestroyed).
        /// </summary>
        private bool ResolveBeacon(float dt)
        {
            if (beacon != null && beacon.isActiveAndEnabled) { beaconMissingSeconds = 0f; return true; }
            beacon = null;
            float best = 1.5f * 1.5f;
            foreach (RadioBeaconPiece candidate in RadioBeaconPiece.All)
            {
                if (candidate == null || !candidate.isActiveAndEnabled) continue;
                float d = (candidate.transform.position - beaconPosition).sqrMagnitude;
                if (d <= best) { best = d; beacon = candidate; }
            }
            if (beacon != null) { beaconMissingSeconds = 0f; return true; }
            BroadcastPowered = false;
            beaconMissingSeconds += dt;
            if (beaconMissingSeconds > 1f) FailBroadcast("The radio beacon is gone. The broadcast failed. Build a new one to try again.");
            return false;
        }

        private void OnStructureDestroyed(StructureHealth structure)
        {
            if (structure == null) return;
            DropStructureSalvage(structure);
            if (BroadcastActive && beacon != null && structure.gameObject == beacon.gameObject)
                FailBroadcast("The radio beacon was destroyed. The broadcast failed. Rebuild it to try again.");
        }

        private static void DropStructureSalvage(StructureHealth structure)
        {
            PlacedPiece piece = structure.GetComponent<PlacedPiece>();
            if (piece == null || string.IsNullOrEmpty(piece.pieceId) || !RecipeBook.PieceCosts.TryGetValue(piece.pieceId, out Ingredient[] cost)) return;
            var salvage = new List<KeyValuePair<string, int>>();
            foreach (Ingredient ingredient in cost)
            {
                if (ingredient.amount <= 0 || !ItemCatalog.Exists(ingredient.id)) continue;
                // Half-cost salvage is always below the original cost, so building and breaking cannot create materials.
                int amount = Mathf.FloorToInt(ingredient.amount * 0.5f);
                if (amount > 0) salvage.Add(new KeyValuePair<string, int>(ingredient.id, amount));
            }
            if (salvage.Count == 0) return;
            Vector3 drop = StructureSalvagePosition(structure);
            LootBagSystem.Spawn(drop, LootBagSystem.NextId++, LootTier.Ordinary, salvage);
            GameMessageBus.Post("The destroyed structure left salvage behind.");
        }

        private static Vector3 StructureSalvagePosition(StructureHealth structure)
        {
            Vector3 origin = structure.transform.position + Vector3.up * 2f;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<StructureHealth>() == structure ||
                    hit.collider.GetComponentInParent<CharacterController>() != null ||
                    hit.collider.GetComponentInParent<LootBag>() != null) continue;
                return hit.point + Vector3.up * 0.02f;
            }
            return structure.transform.position + Vector3.up * 0.02f;
        }

        private void FailBroadcast(string message)
        {
            EndBroadcast();
            // A breather before the ordinary raids pick up again.
            Countdown = Mathf.Max(Countdown, 300f);
            GameAudio.Play2D(Sfx.RadioStatic, 0.9f);
            GameMessageBus.Post(message);
        }

        private void CompleteBroadcast()
        {
            EndBroadcast();
            ObjectiveTracker.MarkVictory();
            GameAudio.Play2D(Sfx.Upgrade, 0.9f);
            GameMessageBus.Post("VICTORY: The broadcast got through. A rescue convoy is on its way and the raids are over. You survived the apocalypse. You can keep playing.");
            if (actors.Count > 0) GameMessageBus.Post("Some attackers are still at your base. Finish them off.");
        }

        private void EndBroadcast()
        {
            BroadcastActive = false;
            BroadcastPowered = false;
            beacon = null;
            actors.RemoveAll(a => a == null);
            holdForLeftovers = actors.Count > 0;
            WarningActive = false;
        }

        private void SendWave(int wave)
        {
            int seed = ProceduralWorld.Active != null ? ProceduralWorld.Active.Seed : 0;
            List<RaidVariant> roster = BroadcastRoster(wave, seed);
            var random = new System.Random(unchecked(seed * 31 + 1000003 + wave * 7919));
            int spawned = 0;
            foreach (RaidVariant variant in roster)
            {
                if (actors.Count >= MaxBroadcastActors) break;
                RaidVariantSpec spec = RaidVariantSpec.Of(variant);
                if (!FindSpawnPoint(random, spec, out Vector3 position)) continue;
                if (SpawnActor(variant, RaidNumber, position, Quaternion.LookRotation(Flat(RaidRoute.GateOutside - position)), -1f, null) != null) spawned++;
            }
            if (spawned == 0 && actors.Count < MaxBroadcastActors)
            {
                Debug.LogWarning($"[RaidDirector] Broadcast wave {wave} couldn't place anyone, retrying.");
                nextWaveTry = Time.time + 5f;
                return;
            }
            BroadcastWave = wave;
            nextHorn = Time.time + 40f;
            GameAudio.Play2D(Sfx.RaidHorn, 1f);
            GameMessageBus.Post(wave == BroadcastWaves
                ? "Final wave: a Warlord leads the assault on your beacon!"
                : $"Wave {wave} of {BroadcastWaves} is coming for your beacon.");
            AlarmPiece.NotifyRaidBegan();
            Debug.Log($"[RaidDirector] Broadcast wave {wave}: {spawned} of {roster.Count} ({string.Join(", ", roster)}).");
        }

        /// <summary>Fixed parties, shuffled by the world seed. The last one brings a Warlord.</summary>
        public static List<RaidVariant> BroadcastRoster(int wave, int seed)
        {
            var roster = new List<RaidVariant>();
            void Add(RaidVariant variant, int count) { for (int i = 0; i < count; i++) roster.Add(variant); }
            switch (wave)
            {
                case 1: Add(RaidVariant.Raider, 3); Add(RaidVariant.Scavenger, 2); Add(RaidVariant.Gunner, 1); break;
                case 2: Add(RaidVariant.Raider, 2); Add(RaidVariant.Gunner, 2); Add(RaidVariant.Brute, 2); Add(RaidVariant.Scavenger, 1); break;
                default: Add(RaidVariant.Warlord, 1); Add(RaidVariant.Brute, 2); Add(RaidVariant.Gunner, 2); Add(RaidVariant.Raider, 2); break;
            }
            var random = new System.Random(unchecked(seed * 17 + 777767 + wave * 104729));
            // The Warlord stays first so a crowded field never leaves it out.
            int start = roster[0] == RaidVariant.Warlord ? 1 : 0;
            for (int i = roster.Count - 1; i > start; i--)
            {
                int j = start + random.Next(i - start + 1);
                (roster[i], roster[j]) = (roster[j], roster[i]);
            }
            return roster;
        }

        /// <summary>Probes: runs the broadcast clock down to this many seconds (waves fall due as usual on the next frames).</summary>
        public void DebugSetBroadcastRemaining(float seconds)
        {
            if (BroadcastActive) BroadcastRemaining = Mathf.Clamp(seconds, 0f, BroadcastSeconds);
        }

        // ---------- Death ----------

        private void OnAnyDeath(EnemyAI enemy)
        {
            NotifyDeath(enemy);
            RaidAgent agent = enemy.GetComponent<RaidAgent>();
            if (agent != null) actors.Remove(agent);
        }

        /// <summary>Public so a probe can repeat the callback: the second call for one enemy returns null.</summary>
        public LootBag NotifyDeath(EnemyAI enemy)
        {
            if (enemy == null) return null;
            int seed = ProceduralWorld.Active != null ? ProceduralWorld.Active.Seed : 0;
            return LootBagSystem.DropFor(new EnemyAIHandle(enemy), seed);
        }

        // ---------- Banner ----------

        private void UpdateBanner()
        {
            if (banner == null) return;
            string text = null;
            if (BroadcastActive)
                text = $"BROADCAST {Clock(BroadcastRemaining)} / wave {Mathf.Max(1, BroadcastWave)}" + (BroadcastPowered ? "" : " / NO POWER");
            else if (holdForLeftovers && actors.Count > 0) text = $"ATTACKERS LEFT: {actors.Count}";
            else if (CurrentPhase == Phase.Active) text = $"RAID {RaidNumber}: {actors.Count} left";
            else if (ObjectiveTracker.Victory) text = null;
            else if (CurrentPhase == Phase.Pending && pendingFuse >= 0f) text = "RAID INBOUND " + Clock(pendingFuse);
            else if (CurrentPhase == Phase.Countdown && WarningActive) text = "RAID INBOUND " + Clock(Countdown);
            bool show = text != null;
            if (bannerPanel.activeSelf != show) bannerPanel.SetActive(show);
            if (show && banner.text != text) banner.text = text;
        }

        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        // ---------- Saving ----------

        [Serializable]
        public class LootItem { public string id; public int count; }

        [Serializable]
        private class SavedActor
        {
            public int variant;
            public Vector3 position;
            public float yaw;
            public float health;
            public List<LootItem> stolen = new List<LootItem>();
        }

        [Serializable]
        private class SavedBag
        {
            public int id;
            public int tier;
            public Vector3 position;
            public List<LootItem> items = new List<LootItem>();
        }

        [Serializable]
        private class SavedDoor
        {
            public string id;
            public float health;
            public bool broken;
        }

        [Serializable]
        private class Saved
        {
            public int phase;
            public float countdown;
            public int raidNumber;
            public bool warning;
            public bool announcedPending;
            public float pendingFuse;
            public int raidsSurvived;
            public int nextBagId;
            public bool broadcastActive;
            public float broadcastRemaining;
            public int broadcastWave;
            public Vector3 beaconPosition;
            public bool holdForLeftovers;
            public List<SavedActor> actors = new List<SavedActor>();
            public List<SavedBag> bags = new List<SavedBag>();
            public List<SavedDoor> doors = new List<SavedDoor>();
        }

        public string CaptureState()
        {
            var saved = new Saved
            {
                phase = (int)CurrentPhase,
                countdown = Countdown,
                raidNumber = RaidNumber,
                warning = WarningActive,
                announcedPending = announcedPending,
                pendingFuse = pendingFuse,
                raidsSurvived = RaidsSurvived,
                nextBagId = LootBagSystem.NextId,
                broadcastActive = BroadcastActive,
                broadcastRemaining = BroadcastRemaining,
                broadcastWave = BroadcastWave,
                beaconPosition = beaconPosition,
                holdForLeftovers = holdForLeftovers,
            };
            actors.RemoveAll(a => a == null);
            foreach (RaidAgent agent in actors)
            {
                EnemyAI enemy = agent.GetComponent<EnemyAI>();
                if (enemy == null || enemy.IsDead) continue;
                var actor = new SavedActor { variant = (int)agent.Variant, position = agent.transform.position, yaw = agent.transform.eulerAngles.y, health = enemy.health };
                foreach (KeyValuePair<string, int> pair in agent.Stolen) actor.stolen.Add(new LootItem { id = pair.Key, count = pair.Value });
                saved.actors.Add(actor);
            }
            foreach (LootBag bag in LootBagSystem.Bags)
            {
                if (bag == null) continue;
                var entry = new SavedBag { id = bag.Id, tier = (int)bag.Tier, position = bag.transform.position };
                foreach (KeyValuePair<string, int> pair in bag.Contents) entry.items.Add(new LootItem { id = pair.Key, count = pair.Value });
                saved.bags.Add(entry);
            }
            foreach (InteractableDoor door in FindObjectsByType<InteractableDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (door.IsBroken || door.RaidHealth < door.maxRaidHealth)
                    saved.doors.Add(new SavedDoor { id = SaveManager.ComputeStableId(door.transform), health = door.RaidHealth, broken = door.IsBroken });
            }
            return JsonUtility.ToJson(saved);
        }

        public void RestoreState(string json)
        {
            foreach (RaidAgent agent in actors) DespawnActor(agent);
            actors.Clear();
            LootBagSystem.Clear();

            Saved saved = string.IsNullOrEmpty(json) ? new Saved { countdown = FirstRaidSeconds, raidNumber = 1, pendingFuse = -1f, nextBagId = 1 } : JsonUtility.FromJson<Saved>(json);
            CurrentPhase = (Phase)Mathf.Clamp(saved.phase, 0, 2);
            Countdown = saved.countdown > 0f || CurrentPhase != Phase.Countdown ? saved.countdown : 0.1f;
            RaidNumber = Mathf.Max(1, saved.raidNumber);
            WarningActive = saved.warning;
            announcedPending = saved.announcedPending;
            pendingFuse = saved.pendingFuse;
            RaidsSurvived = Mathf.Max(0, saved.raidsSurvived);
            LootBagSystem.NextId = saved.nextBagId;
            nextHorn = Time.time + 40f;
            // The beacon is found again by position on the next frames: pieces come back as new objects.
            BroadcastActive = saved.broadcastActive && !float.IsNaN(saved.broadcastRemaining);
            BroadcastRemaining = BroadcastActive ? Mathf.Clamp(saved.broadcastRemaining, 0f, BroadcastSeconds) : 0f;
            BroadcastWave = BroadcastActive ? Mathf.Clamp(saved.broadcastWave, 0, BroadcastWaves) : 0;
            BroadcastPowered = false;
            beacon = null;
            beaconPosition = saved.beaconPosition;
            beaconMissingSeconds = 0f;
            nextWaveTry = 0f;
            wasPowered = false;
            holdForLeftovers = saved.holdForLeftovers && saved.actors.Count > 0;

            foreach (SavedBag bag in saved.bags)
            {
                var items = new List<KeyValuePair<string, int>>();
                foreach (LootItem item in bag.items) items.Add(new KeyValuePair<string, int>(item.id, item.count));
                LootBagSystem.Spawn(bag.position, bag.id, (LootTier)Mathf.Clamp(bag.tier, 0, 2), items);
            }

            var doors = new Dictionary<string, SavedDoor>();
            foreach (SavedDoor door in saved.doors) doors[door.id] = door;
            foreach (InteractableDoor door in FindObjectsByType<InteractableDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (doors.TryGetValue(SaveManager.ComputeStableId(door.transform), out SavedDoor entry)) door.RestoreRaidState(entry.health, entry.broken);
                else door.RestoreRaidState(door.maxRaidHealth, false);
            }

            foreach (SavedActor actor in saved.actors)
                SpawnActor((RaidVariant)Mathf.Clamp(actor.variant, 0, 4), RaidNumber, actor.position, Quaternion.Euler(0f, actor.yaw, 0f), actor.health, actor.stolen);
            Physics.SyncTransforms();
        }
    }
}
