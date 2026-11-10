using UnityEngine;

namespace SheltreSim.Core
{
    /// <summary>Run-level difficulty. Normal is deliberately the zero/default value for legacy saves.</summary>
    public enum GameDifficulty
    {
        Normal = 0,
        Easy = 1,
        Hard = 2,
    }

    /// <summary>Rules shared by new-game setup, survival needs, raids, and save migration.</summary>
    public static class GameDifficultySettings
    {
        public static GameDifficulty Current { get; private set; } = GameDifficulty.Normal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Current = GameDifficulty.Normal;

        public static void Select(GameDifficulty difficulty)
        {
            Current = System.Enum.IsDefined(typeof(GameDifficulty), difficulty) ? difficulty : GameDifficulty.Normal;
        }

        public static GameDifficulty Next(GameDifficulty difficulty) => difficulty switch
        {
            GameDifficulty.Normal => GameDifficulty.Easy,
            GameDifficulty.Easy => GameDifficulty.Hard,
            _ => GameDifficulty.Normal,
        };

        public static int StartingBudget => Current switch
        {
            GameDifficulty.Easy => 85000,
            GameDifficulty.Hard => 58000,
            _ => HomeBase.EconomyTuning.NormalBudget,
        };

        public static float NeedsMultiplier => Current switch
        {
            GameDifficulty.Easy => .75f,
            GameDifficulty.Hard => 1.35f,
            _ => 1f,
        };

        public static float RaidHealthMultiplier => Current switch
        {
            GameDifficulty.Easy => .8f,
            GameDifficulty.Hard => 1.2f,
            _ => 1f,
        };

        public static float RaidDamageMultiplier => Current switch
        {
            GameDifficulty.Easy => .8f,
            GameDifficulty.Hard => 1.15f,
            _ => 1f,
        };

        public static float FirstRaidSeconds => Current switch
        {
            GameDifficulty.Easy => 720f,
            GameDifficulty.Hard => 420f,
            _ => 540f,
        };

        public static float RaidIntervalAfter(int raidNumber) => Current switch
        {
            GameDifficulty.Easy => Mathf.Max(540f, 720f - 20f * Mathf.Max(0, raidNumber - 1)),
            GameDifficulty.Hard => Mathf.Max(300f, 480f - 20f * Mathf.Max(0, raidNumber - 1)),
            _ => Mathf.Max(360f, 600f - 30f * Mathf.Max(0, raidNumber - 1)),
        };

        public static string Name(GameDifficulty difficulty) => difficulty switch
        {
            GameDifficulty.Easy => "EASY",
            GameDifficulty.Hard => "HARD",
            _ => "NORMAL",
        };

        public static string Description(GameDifficulty difficulty) => difficulty switch
        {
            GameDifficulty.Easy => "$85,000 start • slower hunger and thirst • raids start later and hit softer",
            GameDifficulty.Hard => "$58,000 start • faster hunger and thirst • earlier, tougher raids",
            _ => "$70,000 start • standard survival and raid pacing",
        };
    }
}
