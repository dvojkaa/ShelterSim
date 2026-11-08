using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SheltreSim.Building;
using SheltreSim.Core;
using SheltreSim.Enemies;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.UI;
using SheltreSim.World;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace SheltreSim.Saving
{
    /// <summary>Versioned single-slot saves with a recoverable previous copy.</summary>
    public class SaveManager : MonoBehaviour
    {
        public SheltreSim.Player.FirstPersonController player;
        public SheltreSim.Survival.SurvivalStats stats;
        public BuildModeController buildController;
        public Inventory inventory;
        public SheltreSim.Combat.MeleeAttacker meleeAttacker;
        public GameStateManager gameStateManager;

        private const string SaveFileName = "savegame.json";
        private const int MaximumEntries = 10000;
        private const int MaximumProceduralEntries = 100000;
        private const int MaximumChunkCoordinate = 100000;
        private const long MaximumSaveBytes = 10 * 1024 * 1024;
        private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
        public bool HasSave => File.Exists(SavePath) || File.Exists(SavePath + ".bak");

        private SaveInputActions input;
        private EnemySpawner enemySpawner;
        private float initialAttackDamage = -1f;

        private void Awake()
        {
            input = new SaveInputActions();
            ResolveReferences();
        }

        private void OnEnable()
        {
            input.SaveGame.performed += OnSaveRequested;
            input.LoadGame.performed += OnLoadRequested;
            input.Enable();
        }

        private void OnDisable()
        {
            input.SaveGame.performed -= OnSaveRequested;
            input.LoadGame.performed -= OnLoadRequested;
            input.Disable();
        }

        private void OnDestroy() => input?.Dispose();
        private void OnSaveRequested(InputAction.CallbackContext context) => TrySave();
        private void OnLoadRequested(InputAction.CallbackContext context) => TryLoad();

        public bool TrySave() => TrySaveToPath(SavePath);
        public bool TryLoad() => TryLoadFromPath(SavePath);

        private void ResolveReferences()
        {
            if (player == null) player = Object.FindFirstObjectByType<SheltreSim.Player.FirstPersonController>();
            if (stats == null && player != null) stats = player.GetComponent<SheltreSim.Survival.SurvivalStats>();
            if (inventory == null && player != null) inventory = player.GetComponent<Inventory>();
            if (meleeAttacker == null && player != null) meleeAttacker = player.GetComponent<SheltreSim.Combat.MeleeAttacker>();
            if (buildController == null) buildController = Object.FindFirstObjectByType<BuildModeController>();
            if (gameStateManager == null) gameStateManager = Object.FindFirstObjectByType<GameStateManager>();
            if (enemySpawner == null) enemySpawner = Object.FindFirstObjectByType<EnemySpawner>();
            if (initialAttackDamage <= 0f && meleeAttacker != null) initialAttackDamage = meleeAttacker.attackDamage;
        }

        private bool HasRequiredReferences()
        {
            ResolveReferences();
            if (player != null && stats != null && buildController != null)
            {
                return true;
            }
            GameMessageBus.Post("Save/load is unavailable: the player or building system is missing.");
            return false;
        }

        // Kept separate from the default slot so integration verification can use a temporary
        // file without reading or overwriting the player's actual saved game.
        private bool TrySaveToPath(string path)
        {
            if (!HasRequiredReferences()) return false;
            if (stats.IsDead || stats.health <= 0f)
            {
                GameMessageBus.Post("Cannot save after death. Load your last save or restart.");
                return false;
            }

            string temporaryPath = path + ".tmp";
            try
            {
                SaveData data = CaptureSnapshot();
                Dictionary<string, BuildPieceDefinition> definitions = BuildDefinitions();
                ValidateSnapshot(data, definitions);
                string json = JsonUtility.ToJson(data, true);
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                Require(bytes.Length <= MaximumSaveBytes, "The save file is too large.");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                using (FileStream stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                // Replace only after the complete JSON is on disk. An interrupted write must
                // leave the last successful save recoverable rather than truncating it.
                if (File.Exists(path))
                {
                    // After recovering a backup, the primary can still be corrupt. Never
                    // rotate that rejected file over the last known-good backup on F5.
                    bool primaryIsValid = TryReadSnapshot(path, definitions, out _, out _);
                    File.Replace(temporaryPath, path, primaryIsValid ? path + ".bak" : null);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }

                GameMessageBus.Post("Game saved.");
                Debug.Log($"Saved game to {path}");
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Save failed: {error.Message}");
                GameMessageBus.Post("Could not save the game. Your previous save has been kept.");
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private bool TryLoadFromPath(string path)
        {
            if (!HasRequiredReferences()) return false;
            if (!File.Exists(path) && !File.Exists(path + ".bak"))
            {
                GameMessageBus.Post("No saved game yet. Press F5 to save.");
                return false;
            }

            GameObject stagedPieces = null;
            try
            {
                Dictionary<string, BuildPieceDefinition> definitions = BuildDefinitions();
                bool recoveredBackup = false;
                if (!TryReadSnapshot(path, definitions, out SaveData data, out string mainError))
                {
                    if (!TryReadSnapshot(path + ".bak", definitions, out data, out string backupError))
                    {
                        Debug.LogWarning($"Load rejected: {mainError} Backup: {backupError}");
                        GameMessageBus.Post("Could not load the save. Your current game has been kept.");
                        return false;
                    }
                    recoveredBackup = true;
                    Debug.LogWarning($"Using the previous save because the newest save could not load: {mainError}");
                }

                // Resolve every prefab and prepare its replacement while inactive BEFORE
                // changing the current world. Missing content or bad JSON cannot erase it.
                stagedPieces = PreparePieces(data, definitions);
                ModalUI.DismissCurrent();
                buildController.ExitBuildMode();
                ApplySnapshot(data, stagedPieces);
                GameMessageBus.Post(recoveredBackup ? "Recovered the previous saved game." : "Game loaded.");
                Debug.Log($"Loaded game from {path} ({data.placedPieces.Count} pieces restored).");
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Load failed: {error.Message}");
                GameMessageBus.Post("Could not finish loading the game.");
                return false;
            }
            finally
            {
                if (stagedPieces != null) Destroy(stagedPieces);
            }
        }

        private SaveData CaptureSnapshot()
        {
            SaveData data = new SaveData
            {
                version = SaveData.CurrentVersion,
                sceneName = player.gameObject.scene.name,
                playerPosition = player.transform.position,
                playerYaw = player.transform.eulerAngles.y,
                playerPitch = player.CameraPitch,
                hunger = stats.hunger,
                thirst = stats.thirst,
                health = stats.health,
                attackDamage = meleeAttacker != null ? meleeAttacker.attackDamage : -1f,
                gamePhase = gameStateManager != null ? (int)gameStateManager.CurrentPhase
                    : (int)(stats.decayPaused ? GamePhase.Building : GamePhase.Survival),
                placedPieces = new List<SaveData.PlacedPieceData>(),
                inventory = new List<SaveData.InventoryEntry>(),
                nodeStates = new List<SaveData.NodeState>(),
                crateStates = new List<SaveData.CrateState>(),
                doorStates = new List<SaveData.DoorState>(),
                raiderStates = enemySpawner != null ? enemySpawner.CaptureState() : null,
                hasProceduralWorld = ProceduralWorld.Active != null,
                worldSeed = ProceduralWorld.Active != null ? ProceduralWorld.Active.Seed : 0,
                worldElapsedSeconds = ProceduralWorld.Active != null && ProceduralWorld.Active.GetComponent<WorldAtmosphere>() != null
                    ? ProceduralWorld.Active.GetComponent<WorldAtmosphere>().ElapsedSeconds : 0f,
                expeditionUpgrades = ExpeditionProgression.Active != null ? ExpeditionProgression.Active.UpgradeMask : 0,
                discoveredLandmarks = ExpeditionProgression.Active != null ? ExpeditionProgression.Active.CaptureDiscoveries() : new List<string>(),
                proceduralChunks = ProceduralWorld.Active != null ? ProceduralWorld.Active.CaptureWorld()
                    : new List<ProceduralWorldState.ChunkState>(),
                money = HomeBase.Active != null ? HomeBase.Active.Money : 0,
                bunkerStages = HomeBase.Active != null ? HomeBase.Active.CompletedStages : 0,
                bunkerBuilding = HomeBase.Active != null && HomeBase.Active.IsBuilding,
                bunkerBuildElapsed = HomeBase.Active != null ? HomeBase.Active.BuildElapsed : 0f,
                bunkerAnnex = HomeBase.Active != null && HomeBase.Active.HasAnnex,
                equippedGear = player.GetComponent<SheltreSim.Crafting.PlayerGear>() != null
                    ? player.GetComponent<SheltreSim.Crafting.PlayerGear>().CaptureEquipped() : new List<string>(),
            };

            foreach (PlacedPiece piece in PlacedPieceRegistry.All)
            {
                if (piece.gameObject.scene != player.gameObject.scene) continue;
                WaterCollectorPiece collector = piece.GetComponent<WaterCollectorPiece>();
                FarmPlot farm = piece.GetComponent<FarmPlot>();
                data.placedPieces.Add(new SaveData.PlacedPieceData
                {
                    farmStored = farm != null ? farm.State : -1,
                    farmElapsed = farm != null ? farm.GrowElapsed : 0f,
                    pieceId = piece.pieceId,
                    position = piece.transform.position,
                    rotation = piece.transform.rotation,
                    waterStored = collector != null ? collector.Stored : -1,
                    collectionElapsed = collector != null ? collector.CollectionElapsed : 0f,
                });
            }

            foreach (ResourceNode node in SceneObjects<ResourceNode>())
            {
                if (node.GetComponentInParent<ProceduralWorld>() != null) continue;
                data.nodeStates.Add(new SaveData.NodeState
                {
                    id = ComputeStableId(node.transform),
                    remainingCharges = node.remainingCharges,
                    depleted = node.IsDepleted,
                    regrowthElapsed = node.RegrowthElapsed,
                });
            }
            foreach (LootContainer crate in SceneObjects<LootContainer>())
            {
                if (crate.GetComponentInParent<ProceduralWorld>() != null) continue;
                data.crateStates.Add(new SaveData.CrateState
                {
                    id = ComputeStableId(crate.transform),
                    searched = crate.Searched,
                });
            }
            foreach (InteractableDoor door in SceneObjects<InteractableDoor>())
            {
                data.doorStates.Add(new SaveData.DoorState
                {
                    id = ComputeStableId(door.transform),
                    open = door.IsOpen,
                });
            }
            if (inventory != null)
            {
                foreach (KeyValuePair<string, int> entry in inventory.GetAllCounts())
                {
                    data.inventory.Add(new SaveData.InventoryEntry { resourceId = entry.Key, count = entry.Value });
                }
            }
            return data;
        }

        private Dictionary<string, BuildPieceDefinition> BuildDefinitions()
        {
            Dictionary<string, BuildPieceDefinition> definitions = new Dictionary<string, BuildPieceDefinition>();
            Require(buildController.availablePieces != null, "No build piece definitions are available.");
            foreach (BuildPieceDefinition definition in buildController.availablePieces)
            {
                if (definition == null || string.IsNullOrEmpty(definition.id)) continue;
                Require(!definitions.ContainsKey(definition.id), $"Duplicate build piece id '{definition.id}'.");
                definitions.Add(definition.id, definition);
            }
            return definitions;
        }

        private bool TryReadSnapshot(string path, Dictionary<string, BuildPieceDefinition> definitions,
            out SaveData data, out string error)
        {
            data = null;
            try
            {
                Require(File.Exists(path), "The file is missing.");
                Require(new FileInfo(path).Length <= MaximumSaveBytes, "The save file is too large.");
                string json = File.ReadAllText(path);
                Require(!string.IsNullOrWhiteSpace(json) && json.TrimStart().StartsWith("{"),
                    "The save is not a JSON object.");
                data = new SaveData();
                // FromJsonOverwrite preserves the explicit missing-field sentinels above.
                // FromJson's behavior for absent fields must not decide migration defaults.
                JsonUtility.FromJsonOverwrite(json, data);
                ValidateSnapshot(data, definitions);
                Require(!data.hasProceduralWorld || ProceduralWorld.Active != null,
                    "The procedural world system is unavailable.");
                Require(string.IsNullOrEmpty(data.sceneName) || data.sceneName == player.gameObject.scene.name,
                    "The save belongs to a different scene.");
                error = null;
                return true;
            }
            catch (Exception failure)
            {
                data = null;
                error = failure.Message;
                return false;
            }
        }

        private static void ValidateSnapshot(SaveData data, Dictionary<string, BuildPieceDefinition> definitions)
        {
            // Version 5 moved the start of the game: older saves place everything in a world that's gone.
            Require(data != null && data.version == SaveData.CurrentVersion, "This save format is unsupported.");
            Require(data.money >= 0 && data.bunkerStages >= 0 && data.bunkerStages <= HomeBase.Stages.Length &&
                Finite(data.bunkerBuildElapsed) && data.bunkerBuildElapsed >= 0f, "The saved bunker project is invalid.");
            Require(data.equippedGear != null && data.equippedGear.Count <= 8, "The saved gear is invalid.");
            Require(Finite(data.playerPosition) && Finite(data.playerYaw) && Finite(data.playerPitch),
                "The saved player position is invalid.");
            Require(Finite(data.hunger) && Finite(data.thirst) && Finite(data.health)
                && data.hunger >= 0f && data.thirst >= 0f && data.health >= 0f, "The saved vitals are invalid.");
            Require(Finite(data.attackDamage) && (data.attackDamage == -1f || data.attackDamage > 0f),
                "The saved weapon damage is invalid.");
            Require(data.gamePhase >= (int)GamePhase.Building && data.gamePhase <= (int)GamePhase.Survival,
                "The saved game phase is invalid.");
            Require(data.placedPieces != null && data.placedPieces.Count <= MaximumEntries,
                "The saved build pieces are missing or too numerous.");

            foreach (SaveData.PlacedPieceData piece in data.placedPieces)
            {
                Require(piece != null && !string.IsNullOrEmpty(piece.pieceId)
                    && definitions.TryGetValue(piece.pieceId, out _), "A saved build piece is unknown.");
                BuildPieceDefinition definition = definitions[piece.pieceId];
                Require(definition.prefab != null && definition.prefab.activeSelf,
                    $"The prefab for '{piece.pieceId}' is missing or inactive.");
                PlacedPiece tag = definition.prefab.GetComponent<PlacedPiece>();
                Require(tag != null && tag.enabled && tag.pieceId == piece.pieceId,
                    $"The prefab for '{piece.pieceId}' cannot be saved reliably.");
                Require(Finite(piece.position) && ValidRotation(piece.rotation), "A build piece has an invalid transform.");
                Require(piece.waterStored >= -1 && Finite(piece.collectionElapsed) && piece.collectionElapsed >= 0f,
                    "A water collector has invalid saved progress.");
                Require(piece.farmStored >= -1 && piece.farmStored <= 2 && Finite(piece.farmElapsed) && piece.farmElapsed >= 0f,
                    "A farm plot has invalid saved progress.");
            }

            if (data.version >= 2)
            {
                Require(!string.IsNullOrEmpty(data.sceneName) && data.inventory != null
                    && data.nodeStates != null && data.crateStates != null && data.doorStates != null,
                    "The save is incomplete.");
            }

            Require(Finite(data.worldElapsedSeconds) && data.worldElapsedSeconds >= 0f && data.worldElapsedSeconds <= 100000000f,
                "The world clock is invalid.");
            Require(data.expeditionUpgrades >= 0 && data.expeditionUpgrades <= 7, "Unknown expedition upgrade.");
            Require(data.discoveredLandmarks != null && data.discoveredLandmarks.Count <= MaximumEntries &&
                data.discoveredLandmarks.All(id => !string.IsNullOrEmpty(id) && id.Length <= 256) &&
                data.discoveredLandmarks.Distinct().Count() == data.discoveredLandmarks.Count, "Invalid landmark discoveries.");
            ValidateProceduralChunks(data);
            ValidateEntries(data.inventory, entry => entry.resourceId, entry => entry.count >= 0, true);
            ValidateEntries(data.nodeStates, entry => entry.id,
                entry => entry.remainingCharges >= 0 && Finite(entry.regrowthElapsed) && entry.regrowthElapsed >= 0f,
                data.version > 0);
            ValidateEntries(data.crateStates, entry => entry.id, entry => true, data.version > 0);
            ValidateEntries(data.doorStates, entry => entry.id, entry => true, true);
            ValidateEntries(data.raiderStates, entry => entry.id,
                entry => Finite(entry.position) && ValidRotation(entry.rotation)
                    && Finite(entry.health) && entry.health >= 0f
                    && Finite(entry.respawnRemaining) && entry.respawnRemaining >= 0f, true);
        }

        private static void ValidateProceduralChunks(SaveData data)
        {
            Require(data.version >= 3 || !data.hasProceduralWorld,
                "An older save contains unsupported procedural world state.");
            Require(data.proceduralChunks != null && data.proceduralChunks.Count <= MaximumEntries,
                "The saved procedural chunks are missing or too numerous.");
            Require(data.hasProceduralWorld || data.proceduralChunks.Count == 0,
                "The save contains chunks without a procedural world.");
            HashSet<Vector2Int> coordinates = new HashSet<Vector2Int>();
            int totalEntries = 0;
            foreach (ProceduralWorldState.ChunkState chunk in data.proceduralChunks)
            {
                Require(chunk != null && chunk.x >= -MaximumChunkCoordinate && chunk.x <= MaximumChunkCoordinate
                    && chunk.z >= -MaximumChunkCoordinate && chunk.z <= MaximumChunkCoordinate,
                    "A saved chunk has invalid coordinates.");
                Require(coordinates.Add(new Vector2Int(chunk.x, chunk.z)), "The save contains a duplicate chunk.");
                Require(chunk.nodes != null && chunk.crates != null, "A saved chunk is incomplete.");
                if(chunk.encounters == null) chunk.encounters = new List<ProceduralWorldState.EncounterState>();
                totalEntries += chunk.nodes.Count + chunk.crates.Count + chunk.encounters.Count;
                Require(totalEntries <= MaximumProceduralEntries, "The save contains too much procedural state.");
                ValidateEntries(chunk.nodes, node => node.id,
                    node => ValidProceduralId(node.id) && node.charges >= 0 && node.charges <= MaximumEntries
                        && Finite(node.elapsed) && node.elapsed >= 0f, true);
                ValidateEntries(chunk.crates, crate => crate.id, crate => ValidProceduralId(crate.id), true);
                ValidateEntries(chunk.encounters, actor=>actor.id, actor=>ValidProceduralId(actor.id)&&Finite(actor.health)&&actor.health>=0f&&actor.health<=10000f,true);
            }
        }

        private static bool ValidProceduralId(string id) => !string.IsNullOrEmpty(id) && id.Length <= 1024;

        private static void ValidateEntries<T>(List<T> entries, Func<T, string> id, Func<T, bool> valid,
            bool requireUnique) where T : class
        {
            if (entries == null) return;
            Require(entries.Count <= MaximumEntries, "The save contains too many entries.");
            HashSet<string> ids = new HashSet<string>();
            foreach (T entry in entries)
            {
                Require(entry != null && !string.IsNullOrEmpty(id(entry)) && valid(entry), "A saved entry is invalid.");
                Require(!requireUnique || ids.Add(id(entry)), "The save contains a duplicate identifier.");
            }
        }

        private GameObject PreparePieces(SaveData data, Dictionary<string, BuildPieceDefinition> definitions)
        {
            Require(string.IsNullOrEmpty(data.sceneName) || data.sceneName == player.gameObject.scene.name,
                "The save belongs to a different scene.");
            GameObject staging = new GameObject("SaveLoad_Staging");
            staging.SetActive(false);
            try
            {
                foreach (SaveData.PlacedPieceData piece in data.placedPieces)
                {
                    GameObject instance = Instantiate(definitions[piece.pieceId].prefab,
                        piece.position, piece.rotation, staging.transform);
                    WaterCollectorPiece collector = instance.GetComponent<WaterCollectorPiece>();
                    if (collector != null && piece.waterStored >= 0)
                    {
                        collector.RestoreState(piece.waterStored, piece.collectionElapsed);
                    }
                    FarmPlot farm = instance.GetComponent<FarmPlot>();
                    if (farm != null && piece.farmStored >= 0) farm.RestoreState(piece.farmStored, piece.farmElapsed);
                }
                return staging;
            }
            catch
            {
                Destroy(staging);
                throw;
            }
        }

        private void ApplySnapshot(SaveData data, GameObject staging)
        {
            List<PlacedPiece> previous = new List<PlacedPiece>(PlacedPieceRegistry.All);
            foreach (PlacedPiece piece in previous)
            {
                if (piece.gameObject.scene != player.gameObject.scene) continue;
                // Destroy is deferred until the end of the frame. Disable first so colliders,
                // behavior and registry entries disappear before the replacement becomes live.
                piece.gameObject.SetActive(false);
                Destroy(piece.gameObject);
            }
            while (staging.transform.childCount > 0)
            {
                staging.transform.GetChild(0).SetParent(null, true);
            }

            RestoreWorldObjects(data);
            if (inventory != null && data.inventory != null)
            {
                inventory.Clear();
                foreach (SaveData.InventoryEntry entry in data.inventory) inventory.SetCount(entry.resourceId, entry.count);
            }
            if (meleeAttacker != null)
            {
                // An older save without damage must restore the starting value, not keep an
                // upgrade obtained later in this session while handing its item back.
                meleeAttacker.attackDamage = data.attackDamage > 0f ? data.attackDamage : initialAttackDamage;
            }
            if (enemySpawner != null && data.raiderStates != null) enemySpawner.RestoreState(data.raiderStates);
            if (gameStateManager != null) gameStateManager.RestorePhase((GamePhase)data.gamePhase);
            else stats.decayPaused = data.gamePhase == (int)GamePhase.Building;

            CharacterController controller = player.GetComponent<CharacterController>();
            bool controllerWasEnabled = controller != null && controller.enabled;
            if (controllerWasEnabled) controller.enabled = false;
            try
            {
                player.transform.SetPositionAndRotation(data.playerPosition, Quaternion.Euler(0f, data.playerYaw, 0f));
                player.RestoreView(data.playerPitch);
            }
            finally
            {
                if (controllerWasEnabled) controller.enabled = true;
            }
            // Chunk generation must see the restored location, never the location from the
            // session being replaced. Old saves start fresh generated surroundings while
            // retaining their hand-built-region state above.
            if (ProceduralWorld.Active != null)
                ProceduralWorld.Active.RestoreWorld(data.hasProceduralWorld ? data.worldSeed : ProceduralWorld.Active.Seed,
                    data.hasProceduralWorld ? data.proceduralChunks : new List<ProceduralWorldState.ChunkState>(),
                    data.gamePhase != (int)GamePhase.Building);
            if (HomeBase.Active != null)
                HomeBase.Active.Restore(data.money, data.bunkerStages, data.bunkerBuilding, data.bunkerBuildElapsed, data.bunkerAnnex);
            SheltreSim.Crafting.PlayerGear gear = player.GetComponent<SheltreSim.Crafting.PlayerGear>();
            if (gear != null) gear.RestoreEquipped(data.equippedGear);
            if(ExpeditionProgression.Active!=null)ExpeditionProgression.Active.Restore(data.expeditionUpgrades,data.discoveredLandmarks);
            if(ProceduralWorld.Active!=null)ProceduralWorld.Active.GetComponent<WorldAtmosphere>()?.RestoreTime(data.worldElapsedSeconds);
            stats.RestoreVitals(data.hunger, data.thirst, data.health);
            Physics.SyncTransforms();
        }

        private void RestoreWorldObjects(SaveData data)
        {
            bool legacy = data.version == 0;
            if (data.nodeStates != null)
            {
                Dictionary<string, ResourceNode> nodes = new Dictionary<string, ResourceNode>();
                foreach (ResourceNode node in SceneObjects<ResourceNode>())
                {
                    if (node.GetComponentInParent<ProceduralWorld>() != null) continue;
                    nodes[legacy ? LegacyId(node.transform) : ComputeStableId(node.transform)] = node;
                }
                foreach (SaveData.NodeState state in data.nodeStates)
                    if (nodes.TryGetValue(state.id, out ResourceNode node))
                        node.RestoreState(state.remainingCharges, state.depleted, state.regrowthElapsed);
            }

            if (data.crateStates != null)
            {
                Dictionary<string, bool> searched = new Dictionary<string, bool>();
                bool ambiguousLegacyCrates = false;
                foreach (SaveData.CrateState state in data.crateStates)
                {
                    if (legacy && searched.TryGetValue(state.id, out bool previous))
                    {
                        searched[state.id] = previous || state.searched;
                        ambiguousLegacyCrates = true;
                    }
                    else searched[state.id] = state.searched;
                }
                foreach (LootContainer crate in SceneObjects<LootContainer>())
                {
                    if (crate.GetComponentInParent<ProceduralWorld>() != null) continue;
                    if (searched.TryGetValue(legacy ? LegacyId(crate.transform) : ComputeStableId(crate.transform), out bool value))
                        crate.RestoreSearchedState(value);
                }
                if (ambiguousLegacyCrates)
                    Debug.LogWarning("This older save shared IDs between nearby crates. Ambiguous groups keep any searched state; new saves track every crate separately.");
            }

            if (data.doorStates != null)
            {
                Dictionary<string, InteractableDoor> doors = new Dictionary<string, InteractableDoor>();
                foreach (InteractableDoor door in SceneObjects<InteractableDoor>())
                    doors[ComputeStableId(door.transform)] = door;
                foreach (SaveData.DoorState state in data.doorStates)
                    if (doors.TryGetValue(state.id, out InteractableDoor door)) door.RestoreState(state.open);
            }
        }

        private IEnumerable<T> SceneObjects<T>() where T : Component
        {
            foreach (T component in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (component.gameObject.scene == player.gameObject.scene) yield return component;
        }

        /// <summary>
        /// Names alone collide for sibling LootCrates. Escaped names plus the ordinal among
        /// equally named siblings stay unique without depending on unrelated spawned objects.
        /// Scene construction is deterministic; these objects are never moved in the hierarchy.
        /// </summary>
        public static string ComputeStableId(Transform target)
        {
            if (target == null) return string.Empty;
            List<string> segments = new List<string>();
            for (Transform current = target; current != null; current = current.parent)
            {
                int ordinal = 0;
                if (current.parent != null)
                {
                    for (int i = 0; i < current.GetSiblingIndex(); i++)
                        if (current.parent.GetChild(i).name == current.name) ordinal++;
                }
                else
                {
                    foreach (GameObject root in current.gameObject.scene.GetRootGameObjects())
                        if (root.name == current.name && root.transform.GetSiblingIndex() < current.GetSiblingIndex()) ordinal++;
                }
                segments.Add(Uri.EscapeDataString(current.name) + "[" + ordinal + "]");
            }
            segments.Reverse();
            return string.Join("/", segments);
        }

        private static string LegacyId(Transform target)
        {
            string path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = target.name + "/" + path;
            }
            return path;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool ValidRotation(Quaternion value)
        {
            if (!Finite(value.x) || !Finite(value.y) || !Finite(value.z) || !Finite(value.w)) return false;
            float squaredLength = value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;
            // Transform rotations are unit quaternions. Reject corrupt magnitudes, including
            // finite components whose squared length overflows, before touching Unity physics.
            return Finite(squaredLength) && Mathf.Abs(squaredLength - 1f) <= 0.01f;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }
    }
}
