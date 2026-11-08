using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SheltreSim.Saving
{
    public class SaveManager : MonoBehaviour
    {
        public SheltreSim.Player.FirstPersonController player;
        public SheltreSim.Survival.SurvivalStats stats;
        public SheltreSim.Building.BuildModeController buildController;

        /// <summary>
        /// Optional — M4's outdoor-area setup tool wires this in if it has run; save/load
        /// still works for player/stats/pieces without it, inventory is just skipped.
        /// </summary>
        public SheltreSim.Items.Inventory inventory;

        /// <summary>
        /// Optional — M8's setup tool wires this in once melee combat exists. Only needed so
        /// a looted weapon upgrade (M10) survives a save/load round trip; save/load works
        /// without it, the damage bonus is just not persisted.
        /// </summary>
        public SheltreSim.Combat.MeleeAttacker meleeAttacker;

        /// <summary>
        /// Optional — needed so GameStateManager.CurrentPhase (Building/Apocalypse/Survival)
        /// survives a save/load round trip; save/load works without it, the phase is just not
        /// persisted (reload always lands back in Building, the pre-existing behaviour).
        /// </summary>
        public SheltreSim.Core.GameStateManager gameStateManager;

        private const string SaveFileName = "savegame.json";

        private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        private SaveInputActions input;

        private void Awake()
        {
            input = new SaveInputActions();
        }

        private void OnEnable()
        {
            input.Enable();
            input.SaveGame.performed += OnSaveRequested;
            input.LoadGame.performed += OnLoadRequested;
        }

        private void OnDisable()
        {
            input.SaveGame.performed -= OnSaveRequested;
            input.LoadGame.performed -= OnLoadRequested;
            input.Disable();
            input.Dispose();
        }

        private void OnSaveRequested(InputAction.CallbackContext context)
        {
            if (player == null || stats == null)
            {
                Debug.LogError($"{nameof(SaveManager)}: player and stats must both be assigned.");
                return;
            }

            SaveData data = new SaveData
            {
                playerPosition = player.transform.position,
                playerYaw = player.transform.eulerAngles.y,
                hunger = stats.hunger,
                thirst = stats.thirst,
                health = stats.health,
                attackDamage = meleeAttacker != null ? meleeAttacker.attackDamage : -1f,
                gamePhase = gameStateManager != null
                    ? (int)gameStateManager.CurrentPhase
                    : (int)SheltreSim.Core.GamePhase.Building,
            };

            foreach (PlacedPiece p in PlacedPieceRegistry.All)
            {
                SaveData.PlacedPieceData pieceData = new SaveData.PlacedPieceData
                {
                    pieceId = p.pieceId,
                    position = p.transform.position,
                    rotation = p.transform.rotation,
                };

                SheltreSim.Building.WaterCollectorPiece collector = p.GetComponent<SheltreSim.Building.WaterCollectorPiece>();
                if (collector != null)
                {
                    pieceData.waterStored = collector.Stored;
                }

                data.placedPieces.Add(pieceData);
            }

            // Resource nodes/loot crates are static scene objects, not PlacedPieces — matched
            // back up on load by hierarchy-path id rather than destroyed/re-Instantiated.
            // FindObjectsInactive.Include matters here in case any node is still fully
            // inactive from before the regrow-timer change (or a half-applied scene edit).
            foreach (SheltreSim.Items.ResourceNode node in Object.FindObjectsByType<SheltreSim.Items.ResourceNode>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                data.nodeStates.Add(new SaveData.NodeState
                {
                    id = ComputeStableId(node.transform),
                    remainingCharges = node.remainingCharges,
                    depleted = node.IsDepleted,
                });
            }

            foreach (SheltreSim.Items.LootContainer crate in Object.FindObjectsByType<SheltreSim.Items.LootContainer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                data.crateStates.Add(new SaveData.CrateState
                {
                    id = ComputeStableId(crate.transform),
                    searched = crate.Searched,
                });
            }

            if (inventory != null)
            {
                foreach (KeyValuePair<string, int> entry in inventory.GetAllCounts())
                {
                    data.inventory.Add(new SaveData.InventoryEntry
                    {
                        resourceId = entry.Key,
                        count = entry.Value,
                    });
                }
            }

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);

            Debug.Log($"Saved game to {SavePath}");
        }

        private void OnLoadRequested(InputAction.CallbackContext context)
        {
            if (player == null || stats == null || buildController == null)
            {
                Debug.LogError($"{nameof(SaveManager)}: player, stats, and buildController must all be assigned.");
                return;
            }

            if (!File.Exists(SavePath))
            {
                Debug.LogWarning($"No save file at {SavePath}.");
                return;
            }

            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));

            // Teleporting a CharacterController by writing transform.position while the
            // controller is enabled is unreliable — the controller keeps its own cached
            // position and can stomp the write on its next Move(). Switching it off around
            // the assignment is the standard idiom and has no other side effects.
            CharacterController characterController = player.GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            player.transform.SetPositionAndRotation(data.playerPosition, Quaternion.Euler(0f, data.playerYaw, 0f));

            if (characterController != null)
            {
                characterController.enabled = true;
            }

            // Not three field assignments: RestoreVitals also un-does a prior death, so that
            // loading a save taken while alive actually gives control back instead of leaving
            // a frozen player staring at a full health bar.
            stats.RestoreVitals(data.hunger, data.thirst, data.health);

            // Non-positive means the save predates this field (see SaveData.attackDamage) —
            // keep whatever damage the live component already has rather than zeroing it.
            if (meleeAttacker != null && data.attackDamage > 0f)
            {
                meleeAttacker.attackDamage = data.attackDamage;
            }

            // Snapshot first — destroying a piece triggers OnDisable -> Unregister,
            // which would mutate PlacedPieceRegistry.All while we're iterating it.
            List<PlacedPiece> existing = new List<PlacedPiece>(PlacedPieceRegistry.All);
            foreach (PlacedPiece p in existing)
            {
                Destroy(p.gameObject);
            }

            foreach (SaveData.PlacedPieceData entry in data.placedPieces)
            {
                var definition = buildController.availablePieces.Find(d => d.id == entry.pieceId);
                if (definition == null)
                {
                    Debug.LogWarning($"No BuildPieceDefinition with id '{entry.pieceId}' — skipping saved piece.");
                    continue;
                }

                GameObject instance = Instantiate(definition.prefab, entry.position, entry.rotation);

                // Non-negative means this entry actually had a Water Collector (see
                // SaveData.PlacedPieceData.waterStored) — negative means either no collector,
                // or a save predating this field, either way leave the fresh instance alone.
                if (entry.waterStored >= 0)
                {
                    SheltreSim.Building.WaterCollectorPiece collector = instance.GetComponent<SheltreSim.Building.WaterCollectorPiece>();
                    if (collector != null)
                    {
                        collector.SetStored(entry.waterStored);
                    }
                }
            }

            // Resource nodes/loot crates are static scene objects that were never destroyed
            // above — match each saved entry back to its live instance by hierarchy-path id.
            Dictionary<string, SheltreSim.Items.ResourceNode> nodesById = new Dictionary<string, SheltreSim.Items.ResourceNode>();
            foreach (SheltreSim.Items.ResourceNode node in Object.FindObjectsByType<SheltreSim.Items.ResourceNode>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                nodesById[ComputeStableId(node.transform)] = node;
            }

            foreach (SaveData.NodeState state in data.nodeStates)
            {
                if (nodesById.TryGetValue(state.id, out SheltreSim.Items.ResourceNode node))
                {
                    node.RestoreState(state.remainingCharges, state.depleted);
                }
            }

            Dictionary<string, SheltreSim.Items.LootContainer> cratesById = new Dictionary<string, SheltreSim.Items.LootContainer>();
            foreach (SheltreSim.Items.LootContainer crate in Object.FindObjectsByType<SheltreSim.Items.LootContainer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                cratesById[ComputeStableId(crate.transform)] = crate;
            }

            foreach (SaveData.CrateState state in data.crateStates)
            {
                if (cratesById.TryGetValue(state.id, out SheltreSim.Items.LootContainer crate))
                {
                    crate.RestoreSearchedState(state.searched);
                }
            }

            // Bypasses TriggerApocalypse/ApocalypseSequence entirely — a loaded Survival-phase
            // save must not re-play the siren/shake sequence or re-pause decay via coroutine.
            if (gameStateManager != null)
            {
                gameStateManager.RestorePhase((SheltreSim.Core.GamePhase)data.gamePhase);
            }

            if (inventory != null)
            {
                inventory.Clear();
                foreach (SaveData.InventoryEntry entry in data.inventory)
                {
                    inventory.SetCount(entry.resourceId, entry.count);
                }
            }

            Debug.Log($"Loaded game from {SavePath} ({data.placedPieces.Count} pieces restored).");
        }

        /// <summary>
        /// Computes a stable id for a static scene object (ResourceNode/LootContainer) from its
        /// hierarchy path, e.g. "OutdoorArea/ExpandedWasteland/Buildings/Building_3/LootCrate".
        /// These objects are baked into the scene at editor-tool run time with the same seeded,
        /// deterministic construction every run — never instantiated at runtime — so the path
        /// from scene root to the object is stable across saves without needing a GUID/registry.
        /// </summary>
        private static string ComputeStableId(Transform t)
        {
            var sb = new System.Text.StringBuilder(t.name);
            Transform current = t.parent;
            while (current != null)
            {
                sb.Insert(0, "/").Insert(0, current.name);
                current = current.parent;
            }
            return sb.ToString();
        }
    }
}
