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
            Require(build.survivalHud == hud && build.availablePieces.Count == 7,
                "Build menu has seven pieces and HUD wiring");
            Require(build.availablePieces.All(p => p != null && p.prefab != null && !string.IsNullOrEmpty(p.id)) &&
                build.availablePieces.Select(p => p.id).Distinct().Count() == build.availablePieces.Count,
                "Build definitions have valid, distinct IDs and prefabs");
            Require(UnityEngine.Object.FindFirstObjectByType<SheltreSim.Core.GameOverScreen>() != null &&
                UnityEngine.Object.FindFirstObjectByType<SheltreSim.Enemies.EnemySpawner>() != null,
                "Death screen and enemy respawner exist");
            VerifyEconomy();
            VerifyWorldGeometry();
            Debug.Log("[ReleaseVerification] All construction and economy checks passed. Interactive gameplay still needs a playtest.");
        }

        private static void VerifyWorldGeometry()
        {
            Physics.SyncTransforms();
            var crates = UnityEngine.Object.FindObjectsByType<LootContainer>(FindObjectsSortMode.None);
            Require(crates.Length > 0, "Scavenging map contains loot crates");
            foreach (var crate in crates)
            {
                foreach (var hit in Physics.OverlapSphere(crate.transform.position, 0.05f,
                             ~0, QueryTriggerInteraction.Ignore))
                    Require(hit.transform.IsChildOf(crate.transform), $"Loot crate {crate.transform.parent.name} is not buried in {hit.name}");
            }
            Debug.Log($"[ReleaseVerification] Verified {crates.Length} loot crates against actual world colliders.");
        }

        private static void VerifyEconomy()
        {
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
