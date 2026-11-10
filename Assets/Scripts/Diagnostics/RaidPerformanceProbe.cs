using System;
using System.Collections;
using System.Collections.Generic;
using SheltreSim.Core;
using SheltreSim.Player;
using SheltreSim.Raids;
using SheltreSim.Survival;
using SheltreSim.UI;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// Built-player performance sample under the full raid-12 attacker roster.
    /// Run with --raid12-perf-probe, without -nographics.
    /// </summary>
    public sealed class RaidPerformanceProbe : MonoBehaviour
    {
        private const float MeasurementSeconds = 20f;
        private const float WatchdogSeconds = 60f;
        private readonly List<float> frameTimesMs = new List<float>(2400);
        private SurvivalStats stats;
        private int failures;
        private bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--raid12-perf-probe") >= 0)
                new GameObject(nameof(RaidPerformanceProbe)).AddComponent<RaidPerformanceProbe>();
        }

        private IEnumerator Start()
        {
            StartCoroutine(Watchdog());
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            yield return null;
            yield return null;
            if (ModalUI.IsOpen) ModalUI.DismissCurrent();
            Time.timeScale = 1f;

            HomeBase home = HomeBase.Active;
            RaidDirector director = RaidDirector.Active;
            FirstPersonController player = FindFirstObjectByType<FirstPersonController>();
            stats = player != null ? player.GetComponent<SurvivalStats>() : null;
            if (home == null || director == null || player == null || stats == null || ProceduralWorld.Active == null)
            {
                Fail("scene is missing the home, player, raid director, or procedural world");
                Finish();
                yield break;
            }

            stats.maxHealth = 100000f;
            stats.health = stats.maxHealth;
            stats.decayPaused = true;
            home.DebugStartSurvival();
            yield return new WaitForSeconds(2f);

            int expected = RaidDirector.RosterFor(12, ProceduralWorld.Active.Seed).Count;
            director.DebugStartRaid(12);
            yield return null;
            int spawned = director.RaidersLeft;
            Check(director.CurrentPhase == RaidDirector.Phase.Active && spawned == expected,
                $"full raid-12 roster spawned ({spawned}/{expected})");
            if (finished || spawned == 0)
            {
                Finish();
                yield break;
            }

            float started = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - started < MeasurementSeconds)
            {
                frameTimesMs.Add(Time.unscaledDeltaTime * 1000f);
                yield return null;
            }

            frameTimesMs.Sort();
            float median = Percentile(0.50f);
            float p95 = Percentile(0.95f);
            float maximum = frameTimesMs.Count > 0 ? frameTimesMs[frameTimesMs.Count - 1] : 0f;
            Debug.Log($"[RaidPerformanceProbe] raid=12 active-at-start={spawned}/{expected} samples={frameTimesMs.Count} median={median:F2} ms p95={p95:F2} ms max={maximum:F2} ms duration={MeasurementSeconds:0}s");
            Check(frameTimesMs.Count >= 100, $"collected enough frame samples ({frameTimesMs.Count})");
            Finish();
        }

        private void Update()
        {
            if (stats != null && !stats.IsDead && stats.health < stats.maxHealth)
                stats.Heal(stats.maxHealth);
        }

        private float Percentile(float fraction)
        {
            if (frameTimesMs.Count == 0) return 0f;
            int index = Mathf.Clamp(Mathf.CeilToInt((frameTimesMs.Count - 1) * fraction), 0, frameTimesMs.Count - 1);
            return frameTimesMs[index];
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(WatchdogSeconds);
            if (finished) yield break;
            Fail($"timed out after {WatchdogSeconds:0} seconds");
            Finish();
        }

        private void Check(bool condition, string message)
        {
            if (condition) Debug.Log("[RaidPerformanceProbe] PASS: " + message);
            else Fail(message);
        }

        private void Fail(string message)
        {
            failures++;
            Debug.LogError("[RaidPerformanceProbe] FAIL: " + message);
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            Time.timeScale = 1f;
            Debug.Log(failures == 0 ? "[RaidPerformanceProbe] ALL CHECKS PASSED" : $"[RaidPerformanceProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }
    }
}
