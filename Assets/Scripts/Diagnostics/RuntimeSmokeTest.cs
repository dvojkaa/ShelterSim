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
            var ids = new HashSet<string>();
            foreach (var crate in FindObjectsByType<SheltreSim.Items.LootContainer>(FindObjectsSortMode.None))
                Require(ids.Add(SaveManager.ComputeStableId(crate.transform)), "Unique crate identity");
            Require(ids.Count > 20, "Full scavenging scene loaded");

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

            save.stats.RestoreVitals(70f, 80f, 60f);
            save.player.RestoreView(25f);
            snapshotPath = Path.Combine(Application.temporaryCachePath, "sheltersim-smoke-" + Guid.NewGuid().ToString("N") + ".json");
            Require(InvokeSnapshot(save, "TrySaveToPath"), "Real save serialization");
            save.stats.TakeDamage(1000f);
            Require(save.stats.IsDead && Time.timeScale == 0f, "Lethal damage immediately opens modal death screen");
            Require(InvokeSnapshot(save, "TryLoadFromPath"), "Real load after death");
            Require(!save.stats.IsDead && save.player.enabled && Time.timeScale > 0f,
                "Load restores movement and releases death modal");
            Require(Mathf.Abs(save.stats.health - 60f) < 0.01f && Mathf.Abs(save.player.CameraPitch - 25f) < 0.01f,
                "Vitals and camera pitch survive save/load");
            Require(InvokeSnapshot(save, "TryLoadFromPath"), "Two loads in one frame remain safe");
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
            if (snapshotPath != null && File.Exists(snapshotPath)) File.Delete(snapshotPath);
            Application.Quit(exitCode);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Debug.Log("[RuntimeSmokeTest] PASS: " + message);
        }
    }
}
