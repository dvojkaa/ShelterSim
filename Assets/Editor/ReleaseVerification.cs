using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SheltreSim.Building;
using SheltreSim.Crafting;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Saving;
using SheltreSim.Survival;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>Repeatable construction checks and a Linux player build, without entering Play Mode.</summary>
    public static class ReleaseVerification
    {
        private const string ScenePath = "Assets/Scenes/M1_BunkerShell.unity";

        [MenuItem("Tools/SheltreSim/Verify Current Game")]
        public static void VerifyCurrentGame()
        {
            var player = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            Require(player != null && player.cameraPivot != null, "Player and camera are wired");
            var save = UnityEngine.Object.FindFirstObjectByType<SaveManager>();
            Require(save != null && save.player == player && save.stats != null &&
                save.buildController != null && save.inventory != null && save.gameStateManager != null,
                "Save system references are complete");
            var hud = UnityEngine.Object.FindFirstObjectByType<SurvivalHUD>();
            Require(hud != null, "Survival HUD exists");
            var build = save.buildController;
            Require(build.survivalHud == hud && build.availablePieces.Count >= 40,
                "Build menu has the full set of pieces and HUD wiring");
            Require(build.availablePieces.All(p => p != null && p.prefab != null && !string.IsNullOrEmpty(p.id)) &&
                build.availablePieces.Select(p => p.id).Distinct().Count() == build.availablePieces.Count,
                "Build definitions have valid, distinct IDs and prefabs");
            Require(UnityEngine.Object.FindFirstObjectByType<SheltreSim.Core.GameOverScreen>() != null,
                "Death screen exists");
            var home = UnityEngine.Object.FindFirstObjectByType<SheltreSim.Core.HomeBase>();
            Require(home != null && home.gameState != null && home.inventory != null && home.stageVisuals.Length == SheltreSim.Core.HomeBase.Stages.Length &&
                home.entranceCover != null && home.annexPlug != null && home.annexRoom != null && home.supplyCage != null &&
                home.northWingShell != null && home.northWingPlug != null && home.workshopWing != null && home.hydroWing != null &&
                home.vaultRoom != null && home.vaultPlug != null && home.mainBlastDoor != null,
                "Home site, Supply Cage, optional rooms and bunker doors are wired");
            Require(home.Money == SheltreSim.Core.HomeBase.StartingMoney && home.Money == 70000 &&
                home.supplyCage.GetComponent<SheltreSim.Core.SupplyCageGuard>() != null &&
                home.supplyCage.GetComponent<StructureHealth>() == null,
                "New run has $70,000 and a fixed, protected Supply Cage");
            Require(UnityEngine.Object.FindFirstObjectByType<SheltreSim.Core.SiteTerminal>() != null, "Site terminal exists");
            Require(GameObject.Find("OutdoorArea") == null, "The old starting region is gone");
            var proceduralWorld = UnityEngine.Object.FindFirstObjectByType<SheltreSim.World.ProceduralWorld>();
            Require(proceduralWorld != null && proceduralWorld.assets != null && proceduralWorld.assets.biomeMaterials.Length == 6,
                "Procedural world and six biome materials are wired");
            VerifyEconomy();
            VerifyWorldGeometry();
            Debug.Log("[ReleaseVerification] All construction and economy checks passed. Interactive gameplay still needs a playtest.");
        }

        private static void VerifyWorldGeometry()
        {
            Physics.SyncTransforms();
            Require(UnityEngine.Object.FindObjectsByType<LootContainer>(FindObjectsSortMode.None).Length == 0,
                "Nothing to loot exists in the scene before the apocalypse");
            var player = GameObject.Find("Player");
            foreach (var hit in Physics.OverlapCapsule(player.transform.position + Vector3.up * 0.5f, player.transform.position + Vector3.up * 1.4f, 0.3f, ~0, QueryTriggerInteraction.Ignore))
                Require(hit.transform.IsChildOf(player.transform), $"Player does not spawn inside {hit.name}");
        }

        private static void VerifyEconomy()
        {
            Require(SheltreSim.Core.BunkerShop.All.Count == 30 &&
                SheltreSim.Core.BunkerShop.Get("bunker_contract")?.price == SheltreSim.Core.HomeBase.ContractPrice,
                "Bunker catalogue contains the mandatory contract and 29 offers");
            foreach (SheltreSim.Core.BunkerShop.Offer offer in SheltreSim.Core.BunkerShop.All)
                foreach (SheltreSim.Core.BunkerShop.Grant grant in offer.grants)
                    Require(SheltreSim.Crafting.ItemCatalog.Def(grant.itemId) != null,
                        $"Shop grant '{grant.itemId}' has an item definition");
            GameObject fixture = new GameObject("VerificationFixture");
            ResourceDefinition wood = ScriptableObject.CreateInstance<ResourceDefinition>();
            try
            {
                wood.id = "verification_wood";
                Inventory inventory = fixture.AddComponent<Inventory>();
                inventory.SetCount(wood.id, 3);
                var duplicateCost = new List<CraftingCost>
                {
                    new CraftingCost { resource = wood, amount = 2 },
                    new CraftingCost { resource = wood, amount = 2 }
                };
                Require(!CraftingSystem.TrySpend(inventory, duplicateCost) && inventory.GetCount(wood.id) == 3,
                    "Combined recipe costs cannot overdraft or partially spend");
                inventory.SetCount(wood.id, 4);
                Require(CraftingSystem.TrySpend(inventory, duplicateCost) && inventory.GetCount(wood.id) == 0,
                    "Combined recipe costs spend the exact total");
                Require(!inventory.TryConsume(wood.id, -1) && inventory.GetCount(wood.id) == 0,
                    "Negative consumption cannot generate resources");
                Require(CraftingSystem.TrySpend(inventory, null), "Empty recipes are supported");
                duplicateCost[0].amount = -1;
                Require(!CraftingSystem.TrySpend(inventory, duplicateCost), "Negative recipe costs are rejected");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture);
                UnityEngine.Object.DestroyImmediate(wood);
            }
        }

        public static void RebuildAndVerify()
        {
            BatchSetupAll.RunAll();
            VerifyCurrentGame();
        }

        public static void RebuildVerifyAndBuildLinux()
        {
            RebuildAndVerify();
            BuildLinuxPlayer();
        }

        [MenuItem("Tools/SheltreSim/Build Linux Player")]
        public static void BuildLinuxPlayer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before building.");
            if (!File.Exists(ScenePath)) throw new InvalidOperationException("Build the game scene first.");
            EditorSceneManager.SaveOpenScenes();
            EditorSceneManager.OpenScene(ScenePath);
            VerifyCurrentGame();
            Directory.CreateDirectory("Builds/Linux");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Linux/SheltreSim.x86_64",
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Linux build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
            Debug.Log($"[ReleaseVerification] Linux player built successfully ({report.summary.totalSize} bytes).");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[ReleaseVerification] " + message);
            Debug.Log("[ReleaseVerification] PASS: " + message);
        }
    }
}
