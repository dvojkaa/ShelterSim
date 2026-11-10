using System;
using System.Collections.Generic;
using System.Linq;
using SheltreSim.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SheltreSim.UI
{
    /// <summary>Pause-menu display controls with a timed confirmation for resolution/fullscreen previews.</summary>
    public sealed class DisplaySettingsPanel : MonoBehaviour
    {
        public const string WidthKey = "SheltreSim.Display.Width";
        public const string HeightKey = "SheltreSim.Display.Height";
        public const string FullscreenKey = "SheltreSim.Display.Fullscreen";
        public const string VSyncKey = "SheltreSim.Display.VSync";
        public const string QualityKey = "SheltreSim.Display.Quality";
        public const float ConfirmationSeconds = 15f;

        public readonly struct DisplayMode : IEquatable<DisplayMode>
        {
            public readonly int width;
            public readonly int height;
            public readonly int refreshRate;

            public DisplayMode(int width, int height, int refreshRate = 0)
            {
                this.width = width;
                this.height = height;
                this.refreshRate = refreshRate;
            }

            public bool Equals(DisplayMode other) => width == other.width && height == other.height;
            public override bool Equals(object obj) => obj is DisplayMode other && Equals(other);
            public override int GetHashCode() => unchecked(width * 397 ^ height);
            public override string ToString() => $"{width} × {height}";
        }

        private struct DisplaySnapshot
        {
            public int width;
            public int height;
            public bool fullscreen;
        }

        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private GameObject overlay;
        private Text resolutionLabel;
        private Text qualityLabel;
        private Text statusLabel;
        private Button keepButton;
        private Button revertButton;
        private List<DisplayMode> modes;
        private int selectedMode;
        private int selectedQuality;
        private int selectedVSync;
        private bool selectedFullscreen;
        private bool previewPending;
        private float previewRemaining;
        private DisplaySnapshot previewOriginal;
        private DisplayMode previewTarget;
        private bool previewTargetFullscreen;

        public bool IsOpen => overlay != null && overlay.activeSelf;
        public bool IsPreviewPending => previewPending;
        public float PreviewRemaining => previewRemaining;
        public DisplayMode PendingMode => previewTarget;
        public bool PendingFullscreen => previewTargetFullscreen;
        public DisplayMode OriginalMode => new DisplayMode(previewOriginal.width, previewOriginal.height);
        public bool OriginalFullscreen => previewOriginal.fullscreen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySavedSettings()
        {
            if (PlayerPrefs.HasKey(VSyncKey)) QualitySettings.vSyncCount = Mathf.Clamp(PlayerPrefs.GetInt(VSyncKey), 0, 2);
            if (PlayerPrefs.HasKey(QualityKey) && QualitySettings.names != null && QualitySettings.names.Length > 0)
                QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(QualityKey), 0, QualitySettings.names.Length - 1), true);

            if (!PlayerPrefs.HasKey(WidthKey) || !PlayerPrefs.HasKey(HeightKey)) return;
            int width = Mathf.Max(640, PlayerPrefs.GetInt(WidthKey));
            int height = Mathf.Max(360, PlayerPrefs.GetInt(HeightKey));
            bool fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            DisplayMode[] available = AvailableModes(Screen.resolutions, Screen.width, Screen.height);
            DisplayMode selected = ClosestMode(available, width, height);
            Screen.SetResolution(selected.width, selected.height, fullscreen);
        }

        public void Initialize(Transform canvas)
        {
            overlay = GameUI.Backdrop(canvas, "DisplaySettingsOverlay");
            Image cardImage = GameUI.Panel(overlay.transform, "DisplaySettingsCard", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(760f, 610f), GameUI.PanelColor, true);
            Transform card = cardImage.transform;

            GameUI.Label(card, "DisplayTitle", "DISPLAY SETTINGS", TopLeft, TopLeft, new Vector2(32f, -28f),
                new Vector2(696f, 48f), 32, GameUI.Accent, bold: true);
            GameUI.Label(card, "DisplaySubtitle", "Changes apply immediately. Confirm a resolution within 15 seconds to keep it.",
                TopLeft, TopLeft, new Vector2(32f, -82f), new Vector2(696f, 56f), 17, GameUI.Muted);

            resolutionLabel = GameUI.Label(card, "DisplayResolution", string.Empty, TopLeft, TopLeft,
                new Vector2(42f, -164f), new Vector2(360f, 42f), 22, Color.white, alignment: TextAnchor.MiddleLeft);
            GameUI.Button(card, "DisplayResolutionPrevious", "−", TopLeft, TopLeft, new Vector2(416f, -150f),
                new Vector2(52f, 44f), () => ChangeResolution(-1));
            GameUI.Button(card, "DisplayResolutionNext", "+", TopLeft, TopLeft, new Vector2(474f, -150f),
                new Vector2(52f, 44f), () => ChangeResolution(1));
            GameUI.Button(card, "DisplayApplyResolution", "Apply display mode", TopLeft, TopLeft, new Vector2(532f, -150f),
                new Vector2(194f, 44f), ApplyDisplayMode);

            GameUI.Button(card, "DisplayFullscreen", string.Empty, TopLeft, TopLeft, new Vector2(42f, -226f),
                new Vector2(312f, 48f), ToggleFullscreen);
            GameUI.Button(card, "DisplayVSync", string.Empty, TopLeft, TopLeft, new Vector2(386f, -226f),
                new Vector2(340f, 48f), CycleVSync);

            GameUI.Label(card, "QualityCaption", "QUALITY", TopLeft, TopLeft, new Vector2(42f, -310f),
                new Vector2(200f, 36f), 18, GameUI.Muted, bold: true);
            qualityLabel = GameUI.Label(card, "DisplayQuality", string.Empty, TopLeft, TopLeft,
                new Vector2(200f, -306f), new Vector2(270f, 40f), 21, Color.white, alignment: TextAnchor.MiddleCenter);
            GameUI.Button(card, "DisplayQualityPrevious", "−", TopLeft, TopLeft, new Vector2(506f, -300f),
                new Vector2(52f, 44f), () => ChangeQuality(-1));
            GameUI.Button(card, "DisplayQualityNext", "+", TopLeft, TopLeft, new Vector2(566f, -300f),
                new Vector2(52f, 44f), () => ChangeQuality(1));

            statusLabel = GameUI.Label(card, "DisplayStatus", string.Empty, TopLeft, TopLeft,
                new Vector2(42f, -374f), new Vector2(676f, 48f), 19, GameUI.Warning, alignment: TextAnchor.MiddleLeft);
            keepButton = GameUI.Button(card, "DisplayKeep", "Keep changes", TopLeft, TopLeft, new Vector2(42f, -442f),
                new Vector2(312f, 48f), KeepDisplayMode, true);
            revertButton = GameUI.Button(card, "DisplayRevert", "Revert / Back", TopLeft, TopLeft, new Vector2(386f, -442f),
                new Vector2(340f, 48f), CancelAndClose);
            overlay.SetActive(false);
        }

        private void Update()
        {
            if (!previewPending) return;
            previewRemaining -= Time.unscaledDeltaTime;
            if (previewRemaining <= 0f)
            {
                RevertDisplayMode();
                return;
            }
            RefreshLabels();
        }

        public void Open()
        {
            if (overlay == null) return;
            modes = AvailableModes(Screen.resolutions, Screen.width, Screen.height).ToList();
            selectedMode = IndexOfMode(modes, Screen.width, Screen.height);
            selectedFullscreen = Screen.fullScreen;
            selectedQuality = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0,
                Mathf.Max(0, (QualitySettings.names?.Length ?? 1) - 1));
            selectedVSync = Mathf.Clamp(QualitySettings.vSyncCount, 0, 2);
            overlay.SetActive(true);
            RefreshLabels();
        }

        public void CancelAndClose()
        {
            if (previewPending) RevertDisplayMode();
            if (overlay != null) overlay.SetActive(false);
        }

        private void ChangeResolution(int delta)
        {
            if (previewPending) return;
            if (modes == null || modes.Count == 0) modes = AvailableModes(Screen.resolutions, Screen.width, Screen.height).ToList();
            selectedMode = (selectedMode + delta + modes.Count) % modes.Count;
            RefreshLabels();
        }

        private void ToggleFullscreen()
        {
            if (previewPending) return;
            selectedFullscreen = !selectedFullscreen;
            RefreshLabels();
        }

        private void CycleVSync()
        {
            ApplyVSync((selectedVSync + 1) % 3);
            RefreshLabels();
        }

        internal void ApplyVSync(int value)
        {
            selectedVSync = Mathf.Clamp(value, 0, 2);
            QualitySettings.vSyncCount = selectedVSync;
            PlayerPrefs.SetInt(VSyncKey, selectedVSync);
            PlayerPrefs.Save();
        }

        private void ChangeQuality(int delta)
        {
            string[] names = QualitySettings.names;
            if (names == null || names.Length == 0) return;
            ApplyQuality((selectedQuality + delta + names.Length) % names.Length);
            RefreshLabels();
        }

        internal void ApplyQuality(int value)
        {
            string[] names = QualitySettings.names;
            if (names == null || names.Length == 0) return;
            selectedQuality = Mathf.Clamp(value, 0, names.Length - 1);
            QualitySettings.SetQualityLevel(selectedQuality, true);
            PlayerPrefs.SetInt(QualityKey, selectedQuality);
            PlayerPrefs.Save();
        }

        private void ApplyDisplayMode()
        {
            if (previewPending || modes == null || modes.Count == 0) return;
            DisplayMode target = modes[Mathf.Clamp(selectedMode, 0, modes.Count - 1)];
            if (target.width == Screen.width && target.height == Screen.height && selectedFullscreen == Screen.fullScreen)
            {
                PersistDisplayMode(target, selectedFullscreen);
                statusLabel.text = "Display mode is already active and saved.";
                return;
            }

            previewOriginal = new DisplaySnapshot { width = Screen.width, height = Screen.height, fullscreen = Screen.fullScreen };
            previewTarget = target;
            previewTargetFullscreen = selectedFullscreen;
            previewPending = true;
            previewRemaining = ConfirmationSeconds;
            Screen.SetResolution(target.width, target.height, selectedFullscreen);
            statusLabel.text = "Keep this display mode before the timer expires, or it will revert.";
            RefreshLabels();
        }

        private void KeepDisplayMode()
        {
            if (!previewPending) return;
            PersistDisplayMode(previewTarget, previewTargetFullscreen);
            previewPending = false;
            previewRemaining = 0f;
            statusLabel.text = "Display mode saved.";
            RefreshLabels();
        }

        private void RevertDisplayMode()
        {
            if (!previewPending) return;
            Screen.SetResolution(previewOriginal.width, previewOriginal.height, previewOriginal.fullscreen);
            previewPending = false;
            previewRemaining = 0f;
            selectedFullscreen = previewOriginal.fullscreen;
            modes = AvailableModes(Screen.resolutions, previewOriginal.width, previewOriginal.height).ToList();
            selectedMode = IndexOfMode(modes, previewOriginal.width, previewOriginal.height);
            statusLabel.text = "Display mode reverted.";
            RefreshLabels();
        }

        private void PersistDisplayMode(DisplayMode mode, bool fullscreen)
        {
            PlayerPrefs.SetInt(WidthKey, mode.width);
            PlayerPrefs.SetInt(HeightKey, mode.height);
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void RefreshLabels()
        {
            if (resolutionLabel == null) return;
            if (modes == null || modes.Count == 0) modes = AvailableModes(Screen.resolutions, Screen.width, Screen.height).ToList();
            selectedMode = Mathf.Clamp(selectedMode, 0, Mathf.Max(0, modes.Count - 1));
            resolutionLabel.text = "Resolution  " + (modes.Count > 0 ? modes[selectedMode].ToString() : $"{Screen.width} × {Screen.height}");
            Text fullscreenText = overlay.transform.Find("DisplaySettingsCard/DisplayFullscreen/Label")?.GetComponent<Text>();
            if (fullscreenText != null) fullscreenText.text = selectedFullscreen ? "Fullscreen: on" : "Fullscreen: off";
            Text syncText = overlay.transform.Find("DisplaySettingsCard/DisplayVSync/Label")?.GetComponent<Text>();
            if (syncText != null) syncText.text = "VSync: " + (selectedVSync == 0 ? "off" : selectedVSync == 1 ? "on" : "half rate");
            string[] names = QualitySettings.names;
            if (qualityLabel != null) qualityLabel.text = names != null && names.Length > 0 ? names[selectedQuality] : "Default";
            if (statusLabel != null && previewPending) statusLabel.text = $"Keep this mode? Reverting in {Mathf.CeilToInt(previewRemaining)} seconds.";
            if (keepButton != null) keepButton.gameObject.SetActive(previewPending);
            if (revertButton != null) revertButton.GetComponentInChildren<Text>().text = previewPending ? "Revert now" : "Back";
            Button apply = overlay.transform.Find("DisplaySettingsCard/DisplayApplyResolution")?.GetComponent<Button>();
            if (apply != null) apply.interactable = !previewPending;
        }

        // Probe entry points use the same transaction code as the visible buttons.
        public void BeginPreviewForProbe(DisplayMode target, bool fullscreen)
        {
            if (overlay == null) Initialize(transform);
            modes = AvailableModes(Screen.resolutions, Screen.width, Screen.height).ToList();
            selectedMode = IndexOfMode(modes, target.width, target.height);
            selectedFullscreen = fullscreen;
            if (!overlay.activeSelf) overlay.SetActive(true);
            RefreshLabels();
            ApplyDisplayMode();
        }

        public void RevertPreviewForProbe() => RevertDisplayMode();
        public void KeepPreviewForProbe() => KeepDisplayMode();

        public static DisplayMode[] AvailableModes(Resolution[] supported, int currentWidth, int currentHeight)
        {
            var result = new List<DisplayMode>();
            if (supported != null)
                foreach (Resolution resolution in supported)
                {
                    if (resolution.width <= 0 || resolution.height <= 0) continue;
                    int hz = Mathf.RoundToInt((float)resolution.refreshRateRatio.value);
                    var mode = new DisplayMode(resolution.width, resolution.height, hz);
                    int existing = result.FindIndex(candidate => candidate.Equals(mode));
                    if (existing < 0) result.Add(mode);
                    else if (hz > result[existing].refreshRate) result[existing] = mode;
                }
            if (!result.Any(mode => mode.width == currentWidth && mode.height == currentHeight))
                result.Add(new DisplayMode(currentWidth, currentHeight, Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value)));
            result.Sort((a, b) => a.width != b.width ? a.width.CompareTo(b.width) : a.height.CompareTo(b.height));
            return result.ToArray();
        }

        public static DisplayMode ClosestMode(IReadOnlyList<DisplayMode> candidates, int width, int height)
        {
            if (candidates == null || candidates.Count == 0) return new DisplayMode(Mathf.Max(640, width), Mathf.Max(360, height));
            DisplayMode best = candidates[0];
            long bestDistance = long.MaxValue;
            foreach (DisplayMode candidate in candidates)
            {
                long dx = candidate.width - (long)width, dy = candidate.height - (long)height;
                long distance = dx * dx + dy * dy;
                if (distance >= bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        private static int IndexOfMode(IReadOnlyList<DisplayMode> list, int width, int height)
        {
            if (list == null || list.Count == 0) return 0;
            for (int i = 0; i < list.Count; i++) if (list[i].width == width && list[i].height == height) return i;
            return 0;
        }
    }
}
