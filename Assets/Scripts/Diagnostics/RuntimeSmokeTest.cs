using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SheltreSim.Building;
using SheltreSim.Player;
using SheltreSim.Saving;
using SheltreSim.Survival;
using UnityEngine;
using SheltreSim.World;

namespace SheltreSim.Diagnostics
{
    /// <summary>Opt-in standalone regression run. Normal game launches never create this component.</summary>
    public sealed class RuntimeSmokeTest : MonoBehaviour
    {
        private string snapshotPath;
        private bool completed;
        private float startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (!Environment.GetCommandLineArgs().Contains("--smoke-test")) return;
            new GameObject("RuntimeSmokeTest").AddComponent<RuntimeSmokeTest>();
        }

        private IEnumerator Start()
        {
            startedAt = Time.realtimeSinceStartup;
            // Allow the scene's real Awake/OnEnable/Start and UI construction to finish.
            yield return null;
            yield return null;
            try
            {
                RunChecks();
            }
            catch (Exception exception)
            {
                Debug.LogError("[RuntimeSmokeTest] FAIL: " + exception);
                Finish(1);
                yield break;
            }
            // Destroy() is deferred; repeat after the old pieces really leave the hierarchy.
            yield return null;
            try
            {
                var save = FindFirstObjectByType<SaveManager>();
                Require(InvokeSnapshot(save, "TryLoadFromPath"), "Repeated load after deferred destruction");
                Require(!save.stats.IsDead && Time.timeScale > 0f, "Repeated load remains playable");
                Debug.Log("[RuntimeSmokeTest] ALL CHECKS PASSED");
                Finish(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[RuntimeSmokeTest] FAIL: " + exception);
                Finish(1);
            }
        }

        private void RunChecks()
        {
            var save = FindFirstObjectByType<SaveManager>();
            Require(save != null && save.player != null && save.stats != null, "Runtime scene wiring");
            SheltreSim.Core.HomeBase.Active.DebugStartSurvival();
            var world = ProceduralWorld.Active;
            var town = world.EnsureChunk(new Vector2Int(1, -2));
            Require(save.player.GetComponent<CharacterController>().minMoveDistance == 0f,
                "Player movement has no high-frame-rate displacement cutoff");
            foreach (var enemy in FindObjectsByType<SheltreSim.Enemies.EnemyAI>(FindObjectsSortMode.None))
                Require(enemy.GetComponent<CharacterController>().minMoveDistance == 0f,
                    "Raider movement has no high-frame-rate displacement cutoff");
            var ids = new HashSet<string>();
            foreach (var crate in FindObjectsByType<SheltreSim.Items.LootContainer>(FindObjectsSortMode.None))
                Require(ids.Add(SaveManager.ComputeStableId(crate.transform)), "Unique crate identity");
            Require(ids.Count >= 2, "A town holds crates after the bombs");

            var fixture = new GameObject("SmokeIdentityFixture");
            var first = new GameObject("Crate/With[Symbols]");
            var second = new GameObject(first.name);
            first.transform.SetParent(fixture.transform);
            second.transform.SetParent(fixture.transform);
            Require(SaveManager.ComputeStableId(first.transform) != SaveManager.ComputeStableId(second.transform),
                "Duplicate sibling names get separate IDs");
            Destroy(fixture);

            var controller = save.buildController;
            var ghostFactory = typeof(BuildModeController).GetMethod("EnsureGhostForPiece", BindingFlags.Instance | BindingFlags.NonPublic);
            ghostFactory.Invoke(controller, new object[] { controller.availablePieces.First(p => p.prefab.GetComponent<WaterCollectorPiece>() != null) });
            var ghostField = typeof(BuildModeController).GetField("currentGhost", BindingFlags.Instance | BindingFlags.NonPublic);
            var ghost = (GameObject)ghostField.GetValue(controller);
            Require(ghost.GetComponentsInChildren<MonoBehaviour>().All(component => !component.enabled),
                "Build preview disables all gameplay behavior");
            Require(!PlacedPieceRegistry.All.Any(piece => piece.gameObject == ghost), "Ghost is absent from save registry");
            controller.ExitBuildMode();

            // Sibling crates, saved separately and restored individually after their chunk is rebuilt.
            var crates = town.GetComponentsInChildren<SheltreSim.Items.LootContainer>()
                .GroupBy(crate => crate.transform.parent).First(group => group.Count() >= 2).ToArray();
            Vector3 searchedCrate = crates[0].transform.position, unsearchedCrate = crates[1].transform.position;
            crates[0].RestoreSearchedState(true);
            crates[1].RestoreSearchedState(false);
            var waterDefinition = controller.availablePieces.First(piece => piece.prefab.GetComponent<WaterCollectorPiece>() != null);
            var collector = Instantiate(waterDefinition.prefab, new Vector3(0f, 0.6f, 0f), Quaternion.identity)
                .GetComponent<WaterCollectorPiece>();
            collector.RestoreState(2, 12f);

            Require(world != null && world.HasGroundAt(save.player.transform.position), "Runtime world loads ground under the player immediately");
            Require(world.PendingChunkCount > 0, "Surrounding world streams in after startup instead of generating nine chunks synchronously");
            var biomes = new HashSet<int>();
            for (int z = -16; z <= 16; z++) for (int x = -16; x <= 16; x++)
                biomes.Add(WorldLayout.BiomeIndexAt(x * 512f, z * 512f, world.Seed));
            Require(biomes.Count == 6, "Seeded world contains all six biome types");
            Require(town.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Building_")) >= 4,
                "Generated town contains several enterable buildings");
            var testCoordinate = new Vector2Int(6, -6);
            while (WorldLayout.IsTownChunk(testCoordinate, world.Seed) || WorldLayout.IsSuburbChunk(testCoordinate, world.Seed) || ProceduralLandmarks.HasLandmark(testCoordinate,world.Seed)) testCoordinate.x++;
            var generated = world.EnsureChunk(testCoordinate);
            var generatedNode = generated.GetComponentsInChildren<SheltreSim.Items.ResourceNode>().First();
            string generatedNodeName = generatedNode.name;
            int savedCharges = generatedNode.remainingCharges - 1;
            generatedNode.remainingCharges = savedCharges;
            var terrainCollider = generated.GetComponentInChildren<MeshCollider>();
            float px = testCoordinate.x * 128f + 35f, pz = testCoordinate.y * 128f + 53f;
            Require(terrainCollider.Raycast(new Ray(new Vector3(px,500f,pz), Vector3.down), out RaycastHit groundHit, 1000f) &&
                Mathf.Abs(groundHit.point.y - WorldLayout.HeightAt(px,pz,world.Seed)) < .01f,
                "Content height matches actual triangle terrain collision");
            Require(world.CaptureWorld().Any(state => state.x == testCoordinate.x && state.z == testCoordinate.y && state.nodes.Count > 0),
                "Modified generated chunks are captured sparsely");

            var progression=ExpeditionProgression.Active;
            Require(progression!=null,"Expedition progression wired");
            progression.Restore(1,new List<string>{"smoke_discovery"});
            var atmosphere=world.GetComponent<WorldAtmosphere>();
            atmosphere.RestoreTime(240f);
            save.stats.RestoreVitals(70f, 80f, 60f);
            save.player.RestoreView(25f);
            snapshotPath = Path.Combine(Application.temporaryCachePath, "sheltersim-smoke-" + Guid.NewGuid().ToString("N") + ".json");
            Require(InvokeSnapshot(save, "TrySaveToPath"), "Real save serialization");
            progression.Restore(0,new List<string>());
            atmosphere.RestoreTime(0f);
            crates[0].RestoreSearchedState(false);
            crates[1].RestoreSearchedState(true);
            collector.RestoreState(0, 0f);
            save.stats.TakeDamage(1000f);
            Require(save.stats.IsDead && Time.timeScale == 0f, "Lethal damage immediately opens modal death screen");
            Require(InvokeSnapshot(save, "TryLoadFromPath"), "Real load after death");
            Require(!save.stats.IsDead && save.player.enabled && Time.timeScale > 0f,
                "Load restores movement and releases death modal");
            Require(Mathf.Abs(save.stats.health - 60f) < 0.01f && Mathf.Abs(save.player.CameraPitch - 25f) < 0.01f,
                "Vitals and camera pitch survive save/load");
            Require(progression.HasUpgrade(0)&&progression.DiscoveryCount==1&&Mathf.Abs(atmosphere.ElapsedSeconds-240f)<.01f,
                "Discoveries upgrades and world clock survive save/load");
            var revisited = world.EnsureChunk(testCoordinate);
            Require(revisited.GetComponentsInChildren<SheltreSim.Items.ResourceNode>().First(candidate => candidate.name == generatedNodeName).remainingCharges == savedCharges,
                "Generated resource state survives save, unload and regeneration");
            var atlas = world.GetComponent<WorldMapUI>();
            atlas.Open();
            Require(SheltreSim.UI.ModalUI.IsOpen && Time.timeScale == 0f, "World map takes exclusive modal ownership");
            atlas.Close();
            Require(!SheltreSim.UI.ModalUI.IsOpen && Time.timeScale > 0f, "Closing map restores gameplay");
            town = world.EnsureChunk(new Vector2Int(1, -2));
            var reloaded = town.GetComponentsInChildren<SheltreSim.Items.LootContainer>();
            Require(reloaded.First(c => (c.transform.position - searchedCrate).sqrMagnitude < .01f).Searched &&
                !reloaded.First(c => (c.transform.position - unsearchedCrate).sqrMagnitude < .01f).Searched,
                "Same-name sibling crates restore their individual searched states");
            Require(FindObjectsByType<WaterCollectorPiece>(FindObjectsSortMode.None)
                .Any(piece => piece.Stored == 2 && Mathf.Abs(piece.CollectionElapsed - 12f) < 0.01f),
                "Collector water and partial progress survive replacement");
            Require(InvokeSnapshot(save, "TryLoadFromPath"), "Two loads in one frame remain safe");

            // Replacing a save creates a backup. Corruption must recover that previous
            // snapshot instead of erasing the live world or leaving the player dead.
            save.stats.health = 55f;
            Require(InvokeSnapshot(save, "TrySaveToPath"), "Existing save is replaced with a backup");
            File.WriteAllText(snapshotPath, "{ invalid json");
            Require(InvokeSnapshot(save, "TryLoadFromPath") && Mathf.Abs(save.stats.health - 60f) < 0.01f,
                "Corrupt newest save recovers the previous snapshot");
            save.stats.health = 50f;
            Require(InvokeSnapshot(save, "TrySaveToPath"), "Saving after backup recovery succeeds");
            File.WriteAllText(snapshotPath, "{ invalid json again");
            Require(InvokeSnapshot(save, "TryLoadFromPath") && Mathf.Abs(save.stats.health - 60f) < 0.01f,
                "Saving after recovery keeps the known good backup");
        }

        private bool InvokeSnapshot(SaveManager save, string method)
        {
            var action = typeof(SaveManager).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(action != null, method + " test seam exists");
            return (bool)action.Invoke(save, new object[] { snapshotPath });
        }

        private void Update()
        {
            if (!completed && Time.realtimeSinceStartup - startedAt > 30f)
            {
                Debug.LogError("[RuntimeSmokeTest] FAIL: timed out");
                Finish(1);
            }
        }

        private void Finish(int exitCode)
        {
            completed = true;
            if (snapshotPath != null)
                foreach (string suffix in new[] { "", ".bak", ".tmp" })
                    if (File.Exists(snapshotPath + suffix)) File.Delete(snapshotPath + suffix);
            Application.Quit(exitCode);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Debug.Log("[RuntimeSmokeTest] PASS: " + message);
        }
    }
}
