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
            };

            foreach (PlacedPiece p in PlacedPieceRegistry.All)
            {
                data.placedPieces.Add(new SaveData.PlacedPieceData
                {
                    pieceId = p.pieceId,
                    position = p.transform.position,
                    rotation = p.transform.rotation,
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

                Instantiate(definition.prefab, entry.position, entry.rotation);
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
    }
}
