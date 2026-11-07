using UnityEditor;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// Headless-automation helper: runs every milestone's scene-setup tool in order via
    /// `-batchmode -executeMethod SheltreSim.EditorTools.BatchSetupAll.RunAll -quit`.
    /// Exists purely for verifying the whole tool chain compiles and executes without
    /// a human clicking through the Tools menu — not part of the shipped game.
    /// </summary>
    public static class BatchSetupAll
    {
        public static void RunAll()
        {
            Debug.Log("[BatchSetupAll] Building M1 scene...");
            M1SceneBuilder.BuildScene();

            Debug.Log("[BatchSetupAll] Setting up M2 build system...");
            M2BuildSystemSetup.SetupBuildSystem();

            Debug.Log("[BatchSetupAll] Setting up M3 survival & save...");
            M3SurvivalSaveSetup.SetupSurvivalAndSave();

            Debug.Log("[BatchSetupAll] Setting up M4 outdoor area...");
            M4OutdoorAreaSetup.SetupOutdoorArea();

            // Must run before M8: M8MenusSetup wires the new Stone/IronOre/Berries resource
            // definitions and the Recipes list into InventoryUI, and those assets don't exist
            // until this has created them.
            Debug.Log("[BatchSetupAll] Setting up M9 world resources...");
            M9WorldResourcesSetup.SetupWorldResources();

            Debug.Log("[BatchSetupAll] Setting up M5 crafting...");
            M5CraftingSetup.SetupCrafting();

            Debug.Log("[BatchSetupAll] Setting up M6 enemies...");
            M6EnemySetup.SetupEnemies();

            Debug.Log("[BatchSetupAll] Setting up M7 apocalypse trigger...");
            M7ApocalypseSetup.SetupApocalypseTrigger();

            Debug.Log("[BatchSetupAll] Setting up M8 menus and combat...");
            M8MenusSetup.SetupMenusAndCombat();

            Debug.Log("[BatchSetupAll] All milestones wired up successfully.");
        }
    }
}
