using System;
using SheltreSim.Player;
using UnityEngine;
using UnityEngine.UI;

namespace SheltreSim.UI
{
    /// <summary>Look sensitivity, field of view and the FPS counter in the pause menu. Same -/+ pattern as AudioSettingsPanel.</summary>
    public static class GameplaySettingsPanel
    {
        private const float SensitivityStep = 0.02f;
        private const float FovStep = 5f;
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

        public static void Build(Transform card, Vector2 position)
        {
            Row(card, "Look", position, 155f,
                () => $"Look {GameplaySettings.LookSensitivity:0.00}",
                delta => GameplaySettings.SetLookSensitivity((float)Math.Round(GameplaySettings.LookSensitivity + delta * SensitivityStep, 2)));
            Row(card, "Fov", position + new Vector2(300f, 0f), 155f,
                () => $"FOV {GameplaySettings.FieldOfView:0}",
                delta => GameplaySettings.SetFieldOfView(Mathf.Round((GameplaySettings.FieldOfView + delta * FovStep) / FovStep) * FovStep));

            Text fpsText = null;
            Button fps = GameUI.Button(card, "FpsToggle", string.Empty, TopLeft, TopLeft, position + new Vector2(600f, 0f), new Vector2(266f, 46f),
                () =>
                {
                    GameplaySettings.SetShowFps(!GameplaySettings.ShowFps);
                    if (fpsText != null) fpsText.text = FpsCaption();
                });
            fpsText = fps.GetComponentInChildren<Text>();
            fpsText.text = FpsCaption();
            fps.gameObject.AddComponent<LabelRefresher>().refresh = () => fpsText.text = FpsCaption();
        }

        private static string FpsCaption() => GameplaySettings.ShowFps ? "FPS counter: on" : "FPS counter: off";

        private static void Row(Transform card, string name, Vector2 position, float labelWidth, Func<string> caption, Action<int> change)
        {
            Text label = GameUI.Label(card, name + "Setting", string.Empty, TopLeft, TopLeft,
                position + new Vector2(0f, -12f), new Vector2(labelWidth, 30f), 20);
            void Refresh() => label.text = caption();
            GameUI.Button(card, name + "Down", "-", TopLeft, TopLeft, position + new Vector2(160f, 0f), new Vector2(50f, 46f), () => { change(-1); Refresh(); });
            GameUI.Button(card, name + "Up", "+", TopLeft, TopLeft, position + new Vector2(216f, 0f), new Vector2(50f, 46f), () => { change(1); Refresh(); });
            label.gameObject.AddComponent<LabelRefresher>().refresh = Refresh;
            Refresh();
        }

        /// <summary>Re-reads the value each time the pause menu opens.</summary>
        private sealed class LabelRefresher : MonoBehaviour
        {
            public Action refresh;
            private void OnEnable() => refresh?.Invoke();
        }
    }
}
