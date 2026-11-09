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
        /// <summary>
        /// The one-click entry point: builds the whole game (every milestone, in order) and
        /// then presses Play, so a human doesn't have to click through 11 separate Tools menu
        /// items first. Only safe to call from an interactive Editor session with a real
        /// display — forcing EditorApplication.isPlaying from -batchmode -nographics hangs
        /// indefinitely (see CLAUDE.md's hard-won lessons), so this entry point does NOT run
        /// under BatchSetupAll's own headless verification path; use RunAll directly for that.
        /// </summary>
        [MenuItem("Tools/SheltreSim/▶ START GAME (build everything + Play)")]
        public static void StartGame()
        {
            RunAll();
            EditorApplication.isPlaying = true;
        }

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

            Debug.Log("[BatchSetupAll] Setting up M10 map expansion...");
            M10MapExpansionSetup.SetupMapExpansion();

            // Must run after M10MapExpansionSetup (consumes the "LootSpawn" markers it leaves
            // behind) and before M8 (which wires Water/First Aid Kit/Makeshift Blade into
            // InventoryUI, same ordering reason as M9 above).
            Debug.Log("[BatchSetupAll] Setting up M10 consumables and loot...");
            M10ConsumablesAndLootSetup.SetupConsumablesAndLoot();

            // Must run after M9 (needs the Wood/Stone/IronOre/Berries resource assets), M10
            // map expansion (needs ExpandedWasteland + its buildings/cars already placed, so
            // this tool can read their real bounds) and M10 consumables/loot (so LootSpawn
            // markers are already gone by the time this tool scans the zone).
            Debug.Log("[BatchSetupAll] Setting up M12 biome blend...");
            M12BiomeBlendSetup.SetupBiomeBlend();

            // Must run after M10ConsumablesAndLootSetup (needs Water/WeaponUpgrade to exist)
            // and after M2 (needs the Workbench/Water Collector prefabs to exist).
            Debug.Log("[BatchSetupAll] Setting up M11 workbench and water...");
            M11WorkbenchAndWaterSetup.SetupWorkbenchAndWater();

            Debug.Log("[BatchSetupAll] Setting up M5 crafting...");
            M5CraftingSetup.SetupCrafting();

            Debug.Log("[BatchSetupAll] Setting up M6 enemies...");
            M6EnemySetup.SetupEnemies();

            Debug.Log("[BatchSetupAll] Setting up M7 apocalypse trigger...");
            M7ApocalypseSetup.SetupApocalypseTrigger();

            Debug.Log("[BatchSetupAll] Setting up M8 menus and combat...");
            M8MenusSetup.SetupMenusAndCombat();

            // Needs SurvivalStats (M3) on the Player; after M8 purely for chain-order
            // consistency, not a hard dependency.
            Debug.Log("[BatchSetupAll] Setting up M13 death screen...");
            M13DeathScreenSetup.SetupDeathScreen();

            // Needs raiders already spawned (M6 + M12) so it can find them at Play-mode Start.
            Debug.Log("[BatchSetupAll] Setting up M14 raider respawn...");
            M14RaiderRespawnSetup.SetupRaiderRespawn();

            // Needs the BedPiece prefab (M2).
            Debug.Log("[BatchSetupAll] Setting up M15 bed...");
            M15BedSetup.SetupBed();

            // Must stay last: everything above decides positions and colliders, some of it by
            // reading renderer bounds, and this only swaps what they look like.
            Debug.Log("[BatchSetupAll] Applying M16 art pass...");
            M16ArtPass.ApplyArt();

            Debug.Log("[BatchSetupAll] Setting up M18 runtime procedural world...");
            M18ProceduralWorldSetup.Setup();

            M19ExpeditionSetup.Setup();

            // Removes the old starting region, so it has to follow everything that built or read it.
            Debug.Log("[BatchSetupAll] Setting up M20 home site...");
            M20HomeSetup.Setup();

            Debug.Log("[BatchSetupAll] Setting up M21 crafting...");
            M21CraftingSetup.Setup();

            // Needs the items and pieces M21 writes. Homestead first: it adds to the same list and the palette.
            Debug.Log("[BatchSetupAll] Setting up M22 homestead and bunker systems...");
            M22HomesteadSetup.Setup();
            M22SystemsSetup.Setup();

            Debug.Log("[BatchSetupAll] All milestones wired up successfully.");
        }
    }
}
