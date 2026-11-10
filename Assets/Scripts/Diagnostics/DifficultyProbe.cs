using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using SheltreSim.Core;
using SheltreSim.Raids;
using SheltreSim.Saving;
using SheltreSim.Survival;
using UnityEngine;
using UnityEngine.UI;

namespace SheltreSim.Diagnostics
{
    /// <summary>Run with --difficulty-probe. Covers menu selection, profile effects and legacy save migration.</summary>
    public sealed class DifficultyProbe : MonoBehaviour
    {
        private int failures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--difficulty-probe") >= 0)
                new GameObject(nameof(DifficultyProbe)).AddComponent<DifficultyProbe>();
        }

        private IEnumerator Start()
        {
            Time.timeScale = 1f;
            yield return null;
            yield return null;

            Check(GameDifficultySettings.Current == GameDifficulty.Normal, "a fresh process starts with Normal selected");
            Check(HomeBase.StartingMoney == 70000 && SurvivalNeeds(GameDifficulty.Normal) == 1f
                && RaidDirector.FirstRaidSeconds == 540f && RaidDirector.StatMultiplierFor(1) == 1f
                && RaidDirector.DamageMultiplierFor(1) == 1f && RaidDirector.IntervalAfter(1) == 600f,
                "Normal exactly preserves the shipped budget, needs and raid schedule");

            MainMenuController menu = FindFirstObjectByType<MainMenuController>();
            Button cycle = menu != null ? menu.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(b => b.name == "DifficultyButton") : null;
            Text label = menu != null ? menu.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(t => t.name == "DifficultyLabel") : null;
            Check(cycle != null && label != null, "the main menu exposes a new-game difficulty selector");
            if (cycle != null && label != null)
            {
                cycle.onClick.Invoke();
                Check(GameDifficultySettings.Current == GameDifficulty.Easy && label.text.Contains("EASY"), "menu cycles to Easy and labels it");
                cycle.onClick.Invoke();
                Check(GameDifficultySettings.Current == GameDifficulty.Hard && label.text.Contains("HARD"), "menu cycles to Hard and labels it");
            }

            GameDifficultySettings.Select(GameDifficulty.Easy);
            Check(HomeBase.StartingMoney == 85000 && SurvivalNeeds(GameDifficulty.Easy) == .75f
                && RaidDirector.FirstRaidSeconds == 720f && Near(RaidDirector.StatMultiplierFor(1), .8f)
                && Near(RaidDirector.DamageMultiplierFor(1), .8f) && RaidDirector.IntervalAfter(1) == 720f,
                "Easy grants its budget and softer, slower survival pressure");
            GameDifficultySettings.Select(GameDifficulty.Hard);
            Check(HomeBase.StartingMoney == 58000 && Near(SurvivalNeeds(GameDifficulty.Hard), 1.35f)
                && RaidDirector.FirstRaidSeconds == 420f && Near(RaidDirector.StatMultiplierFor(1), 1.2f)
                && Near(RaidDirector.DamageMultiplierFor(1), 1.15f) && RaidDirector.IntervalAfter(1) == 480f,
                "Hard lowers the budget and increases survival and raid pressure");

            var hardSave = new SaveData { version = SaveData.CurrentVersion, difficulty = (int)GameDifficulty.Hard };
            SaveData restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(hardSave));
            Check(restored != null && restored.difficulty == (int)GameDifficulty.Hard, "difficulty survives save JSON serialization");
            var oldSave = new SaveData { version = 7, difficulty = (int)GameDifficulty.Hard };
            typeof(SaveManager).GetMethod("MigrateBunkerEconomy", BindingFlags.Static | BindingFlags.NonPublic)
                ?.Invoke(null, new object[] { oldSave });
            Check(oldSave.difficulty == (int)GameDifficulty.Normal, "version 7 saves migrate to Normal");

            GameDifficultySettings.Select(GameDifficulty.Normal);
            Time.timeScale = 1f;
            Debug.Log(failures == 0 ? "[DifficultyProbe] ALL CHECKS PASSED" : $"[DifficultyProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private static float SurvivalNeeds(GameDifficulty difficulty)
        {
            GameDifficultySettings.Select(difficulty);
            return GameDifficultySettings.NeedsMultiplier;
        }

        private void Check(bool condition, string message)
        {
            if (condition) Debug.Log("[DifficultyProbe] PASS: " + message);
            else { failures++; Debug.LogError("[DifficultyProbe] FAIL: " + message); }
        }

        private static bool Near(float a, float b) => Mathf.Abs(a - b) < .001f;
    }
}
