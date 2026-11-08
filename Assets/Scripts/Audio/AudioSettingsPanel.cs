using System;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SheltreSim.Audio
{
    /// <summary>Master, music and effects volume in the pause menu. Stored in PlayerPrefs: a machine setting, not save data.</summary>
    public static class AudioSettingsPanel
    {
        private const float Step = 0.1f;
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

        public static void Build(Transform card, Vector2 position)
        {
            Row(card, "Master", position, () => GameAudio.MasterVolume, GameAudio.SetMaster);
            Row(card, "Music", position + new Vector2(300f, 0f), () => GameAudio.MusicVolume, GameAudio.SetMusic);
            Row(card, "Effects", position + new Vector2(600f, 0f), () => GameAudio.EffectsVolume, GameAudio.SetEffects);
        }

        private static void Row(Transform card, string name, Vector2 position, Func<float> read, Action<float> write)
        {
            Text label = GameUI.Label(card, name + "Volume", string.Empty, TopLeft, TopLeft,
                position + new Vector2(0f, -12f), new Vector2(160f, 30f), 20);
            void Refresh() => label.text = $"{name} {Mathf.RoundToInt(read() * 100f)}%";
            void Change(float delta)
            {
                write(Mathf.Round((read() + delta) / Step) * Step);
                Refresh();
            }
            GameUI.Button(card, name + "Down", "-", TopLeft, TopLeft, position + new Vector2(160f, 0f), new Vector2(50f, 46f), () => Change(-Step));
            GameUI.Button(card, name + "Up", "+", TopLeft, TopLeft, position + new Vector2(216f, 0f), new Vector2(50f, 46f), () => Change(Step));

            // Built during Awake order, before GameAudio has necessarily read the saved values.
            label.gameObject.AddComponent<VolumeLabelRefresher>().refresh = Refresh;
            Refresh();
        }

        /// <summary>Re-reads the value each time the pause menu opens, in case it changed since the UI was built.</summary>
        private sealed class VolumeLabelRefresher : MonoBehaviour
        {
            public Action refresh;
            private void OnEnable() => refresh?.Invoke();
        }
    }
}
