using SheltreSim.Building;
using SheltreSim.Combat;
using SheltreSim.Core;
using SheltreSim.Items;
using SheltreSim.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// Wires M8 (pause menu, inventory screen, melee combat) into an already-built scene.
    /// Run after M1 (Player/CameraPivot), M2 (BuildSystem/BuildModeController), and M4
    /// (Inventory + resource definitions) have already run. EventSystem creation is owned by
    /// PauseMenu itself (it's the first interactive/clickable UI and stays self-sufficient
    /// rather than depending on this tool to set that prerequisite up) — not duplicated here.
    /// </summary>
    public static class M8MenusSetup
    {
        [MenuItem("Tools/SheltreSim/Setup M8 Menus And Combat")]
        public static void SetupMenusAndCombat()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("No 'Player' GameObject found — run Tools > SheltreSim > Build M1 Bunker Scene first.");
                return;
            }

            Transform buildSystemTransform = player.transform.Find("BuildSystem");
            BuildModeController buildController = buildSystemTransform != null
                ? buildSystemTransform.GetComponent<BuildModeController>()
                : null;
            if (buildController == null)
            {
                Debug.LogError("No 'BuildSystem' child with a BuildModeController found — run Tools > SheltreSim > Setup M2 Build System first.");
                return;
            }

            Transform cameraPivot = player.transform.Find("CameraPivot");
            if (cameraPivot == null)
            {
                Debug.LogError("No 'CameraPivot' child found on Player.");
                return;
            }

            Inventory inventory = player.GetComponent<Inventory>();
            if (inventory == null)
            {
                Debug.LogError("'Player' has no Inventory — run Tools > SheltreSim > Setup M4 Outdoor Area first.");
                return;
            }

            SetupPauseMenu(buildController);
            SetupInventoryUI(player, inventory);
            SetupMeleeAttacker(cameraPivot, buildController);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("M8 ready. Escape (outside build mode) pauses/resumes. I toggles the inventory. Left-click (outside build mode) swings your melee weapon at anything in range.");
        }

        private static void SetupPauseMenu(BuildModeController buildController)
        {
            GameObject pauseMenuObject = GameObject.Find("PauseMenu");
            if (pauseMenuObject == null)
            {
                pauseMenuObject = new GameObject("PauseMenu");
            }

            PauseMenu pauseMenu = pauseMenuObject.GetComponent<PauseMenu>();
            if (pauseMenu == null)
            {
                pauseMenu = pauseMenuObject.AddComponent<PauseMenu>();
            }
            pauseMenu.buildModeController = buildController;
        }

        private static void SetupInventoryUI(GameObject player, Inventory inventory)
        {
            GameObject inventoryUiObject = GameObject.Find("InventoryUI");
            if (inventoryUiObject == null)
            {
                inventoryUiObject = new GameObject("InventoryUI");
            }

            InventoryUI inventoryUi = inventoryUiObject.GetComponent<InventoryUI>();
            if (inventoryUi == null)
            {
                inventoryUi = inventoryUiObject.AddComponent<InventoryUI>();
            }
            inventoryUi.inventory = inventory;

            ResourceDefinition wood = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Data/Resources/Wood.asset");
            ResourceDefinition scrapMetal = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Data/Resources/ScrapMetal.asset");

            inventoryUi.knownResources.Clear();
            if (wood != null)
            {
                inventoryUi.knownResources.Add(wood);
            }
            if (scrapMetal != null)
            {
                inventoryUi.knownResources.Add(scrapMetal);
            }
        }

        private static void SetupMeleeAttacker(Transform cameraPivot, BuildModeController buildController)
        {
            MeleeAttacker attacker = cameraPivot.GetComponent<MeleeAttacker>();
            if (attacker == null)
            {
                attacker = cameraPivot.gameObject.AddComponent<MeleeAttacker>();
            }
            attacker.buildModeController = buildController;

            // Simple first-person "weapon" viewmodel — a thin primitive, not a real asset.
            // Positioned lower-right of the camera, like a held pipe/bat.
            Transform weapon = cameraPivot.Find("WeaponVisual");
            GameObject weaponObject;
            if (weapon == null)
            {
                weaponObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                weaponObject.name = "WeaponVisual";
                weaponObject.transform.SetParent(cameraPivot, false);
                weaponObject.transform.localPosition = new Vector3(0.35f, -0.25f, 0.6f);
                weaponObject.transform.localScale = new Vector3(0.06f, 0.06f, 0.6f);

                // Purely visual — no collider needed on the player's own weapon prop, and an
                // active one would just add noise to the melee SphereCast's hit results.
                Collider weaponCollider = weaponObject.GetComponent<Collider>();
                if (weaponCollider != null)
                {
                    Object.DestroyImmediate(weaponCollider);
                }

                EditorMaterialUtility.Apply(weaponObject, new Color(0.35f, 0.3f, 0.25f));
            }
            else
            {
                weaponObject = weapon.gameObject;
            }

            attacker.weaponVisual = weaponObject.transform;
        }
    }
}
