using SheltreSim.Building;
using SheltreSim.Combat;
using SheltreSim.Core;
using SheltreSim.Interaction;
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

            // Melee attacker first: M10's Equip button needs a reference to it, and the
            // inventory needs to exist before the pause menu (M9 made the inventory panel
            // modal, so Escape has to close it rather than stack a second modal on top).
            MeleeAttacker meleeAttacker = SetupMeleeAttacker(cameraPivot, buildController);
            InventoryUI inventoryUi = SetupInventoryUI(player, inventory, buildController, meleeAttacker);
            SetupPauseMenu(buildController, inventoryUi);
            WireMeleeAttackerIntoSave(meleeAttacker);
            WireInteractorIntoHud(cameraPivot);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("M8 ready. Escape (outside build mode) pauses/resumes. I toggles the inventory. Left-click (outside build mode) swings your melee weapon at anything in range.");
        }

        /// <summary>
        /// M10 QC — MeleeAttacker.attackDamage is permanent progression once a looted weapon
        /// upgrade is equipped, so the save file has to know about it; without this the item
        /// came back on load while the bonus stayed, which stacks damage without limit across
        /// repeated F5/Equip/F9 cycles. Optional in the same degrade-gracefully way M4 wires
        /// the inventory: if M3 hasn't run there's no SaveManager and melee damage simply
        /// isn't persisted.
        /// </summary>
        private static void WireMeleeAttackerIntoSave(MeleeAttacker meleeAttacker)
        {
            GameObject saveManagerObject = GameObject.Find("SaveManager");
            SheltreSim.Saving.SaveManager saveManager = saveManagerObject != null
                ? saveManagerObject.GetComponent<SheltreSim.Saving.SaveManager>()
                : null;

            if (saveManager == null)
            {
                Debug.LogWarning("No SaveManager found — a looted weapon upgrade will not survive save/load. Run Tools > SheltreSim > Setup M3 Survival & Save first if you want that.");
                return;
            }

            saveManager.meleeAttacker = meleeAttacker;
        }

        /// <summary>
        /// Wires PlayerInteractor's on-screen prompt/crosshair/message feed to the HUD — added
        /// alongside M11 since, until now, every interact prompt and gather/loot/craft/hit
        /// result was Console-log only (see SurvivalHUD.SetInteractPrompt / GameMessageBus).
        /// Done here, not in M1SceneBuilder or M3SurvivalSaveSetup, because this is the first
        /// tool in the chain that runs after both PlayerInteractor (M1) and the HUD (M3) exist
        /// — same reasoning as every other cross-system reference this file already wires.
        /// Optional/null-checked: M8 should still work if M3 hasn't run.
        /// </summary>
        private static void WireInteractorIntoHud(Transform cameraPivot)
        {
            PlayerInteractor interactor = cameraPivot.GetComponent<PlayerInteractor>();
            if (interactor == null)
            {
                Debug.LogError("No PlayerInteractor found on CameraPivot — run Tools > SheltreSim > Build M1 Bunker Scene first.");
                return;
            }

            GameObject hud = GameObject.Find("HUD");
            SheltreSim.Survival.SurvivalHUD survivalHud = hud != null ? hud.GetComponent<SheltreSim.Survival.SurvivalHUD>() : null;
            if (survivalHud == null)
            {
                Debug.LogWarning("No SurvivalHUD found — on-screen prompts/messages will fall back to Console-only. Run Tools > SheltreSim > Setup M3 Survival & Save first if you want them.");
                return;
            }

            interactor.survivalHud = survivalHud;
        }

        private static void SetupPauseMenu(BuildModeController buildController, InventoryUI inventoryUi)
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
            pauseMenu.inventoryUi = inventoryUi;
        }

        private static InventoryUI SetupInventoryUI(GameObject player, Inventory inventory, BuildModeController buildController, MeleeAttacker meleeAttacker)
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

            // M9 — Eat button needs SurvivalStats to actually restore hunger. Same component
            // that owns decay/death, found on the Player (see M3SurvivalSaveSetup).
            inventoryUi.stats = player.GetComponent<SheltreSim.Survival.SurvivalStats>();

            // M9 QC — the panel is modal now (cursor + timeScale), so it must defer to build
            // mode exactly like PauseMenu does.
            inventoryUi.buildModeController = buildController;

            // M10 — Equip button needs something to apply the weapon-upgrade bonus to.
            inventoryUi.meleeAttacker = meleeAttacker;

            // M9/M10 — only present once their respective Setup tools have run (BatchSetupAll
            // does, in order, before this); null-checked below so running M8 without them
            // still degrades gracefully, same as wood/scrapMetal always did.
            string[] resourcePaths =
            {
                "Assets/Data/Resources/Wood.asset",
                "Assets/Data/Resources/ScrapMetal.asset",
                "Assets/Data/Resources/Stone.asset",
                "Assets/Data/Resources/IronOre.asset",
                "Assets/Data/Resources/Berries.asset",
                "Assets/Data/Resources/Water.asset",
                "Assets/Data/Resources/FirstAidKit.asset",
                "Assets/Data/Resources/WeaponUpgrade.asset",
            };

            inventoryUi.knownResources.Clear();
            foreach (string path in resourcePaths)
            {
                ResourceDefinition resource = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(path);
                if (resource != null)
                {
                    inventoryUi.knownResources.Add(resource);
                }
            }

            // M9 — "crafting menu" reference list: the same build pieces BuildModeController
            // can place, shown read-only with their real cost (CraftingSystem.TryAfford keeps
            // the affordability color in sync automatically).
            //
            // M9 QC — taken straight from the controller's own availablePieces rather than a
            // hard-coded list of four asset paths. The hard-coded version silently listed 4 of
            // the 7 buildable pieces (no Storage Crate, Water Collector or Generator) and would
            // have gone stale again the next time the build menu grew. Reading the same list
            // the build system cycles through is what actually makes "this panel cannot drift
            // out of sync with what build mode will let you place" true.
            inventoryUi.knownRecipes.Clear();
            foreach (SheltreSim.Building.BuildPieceDefinition piece in buildController.availablePieces)
            {
                if (piece != null)
                {
                    inventoryUi.knownRecipes.Add(piece);
                }
            }

            return inventoryUi;
        }

        private static MeleeAttacker SetupMeleeAttacker(Transform cameraPivot, BuildModeController buildController)
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

            return attacker;
        }
    }
}
