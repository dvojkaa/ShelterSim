using System;
using System.Collections;
using System.Linq;
using SheltreSim.Core;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>Opt-in display-settings transaction check. Run in an isolated player with --display-settings-probe.</summary>
    public sealed class DisplaySettingsProbe : MonoBehaviour
    {
        private int failures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--display-settings-probe") < 0) return;
            new GameObject("DisplaySettingsProbe").AddComponent<DisplaySettingsProbe>();
        }

        private IEnumerator Start()
        {
            Time.timeScale = 1f;
            yield return null;
            yield return null;

            DisplaySettingsPanel panel = FindFirstObjectByType<DisplaySettingsPanel>();
            Check(panel != null, "the pause menu creates the display-settings controller");
            if (panel == null) { Finish(); yield break; }

            int originalWidth = Screen.width;
            int originalHeight = Screen.height;
            bool originalFullscreen = Screen.fullScreen;
            int originalQuality = QualitySettings.GetQualityLevel();
            int originalVSync = QualitySettings.vSyncCount;

            PrefValue widthPref = ReadPref(DisplaySettingsPanel.WidthKey);
            PrefValue heightPref = ReadPref(DisplaySettingsPanel.HeightKey);
            PrefValue fullscreenPref = ReadPref(DisplaySettingsPanel.FullscreenKey);
            PrefValue qualityPref = ReadPref(DisplaySettingsPanel.QualityKey);
            PrefValue vSyncPref = ReadPref(DisplaySettingsPanel.VSyncKey);

            try
            {
                DisplaySettingsPanel.DisplayMode[] currentModes =
                    DisplaySettingsPanel.AvailableModes(Screen.resolutions, originalWidth, originalHeight);
                Check(currentModes.Any(m => m.width == originalWidth && m.height == originalHeight),
                    "the current display mode is available even when the monitor list omits it");
                Check(currentModes.Select(m => (m.width, m.height)).Distinct().Count() == currentModes.Length,
                    "resolution choices contain no duplicate width/height pairs");
                DisplaySettingsPanel.DisplayMode closest = DisplaySettingsPanel.ClosestMode(currentModes, 999999, 999999);
                Check(currentModes.Any(mode => mode.Equals(closest)),
                    "a saved resolution outside the monitor range resolves to a supported mode");

                panel.Open();
                DisplaySettingsPanel.DisplayMode target = currentModes.FirstOrDefault(m =>
                    m.width != originalWidth || m.height != originalHeight);
                if (target.width <= 0 || target.height <= 0)
                    target = new DisplaySettingsPanel.DisplayMode(originalWidth, originalHeight);

                // The opposite fullscreen value guarantees a pending preview even on single-mode displays.
                panel.BeginPreviewForProbe(target, !originalFullscreen);
                Check(panel.IsPreviewPending, "applying a changed resolution/fullscreen mode starts a confirmation preview");
                Check(panel.PendingMode.width == target.width && panel.PendingMode.height == target.height
                    && panel.PendingFullscreen == !originalFullscreen, "the pending display mode matches the player's selection");
                Check(ReadPref(DisplaySettingsPanel.WidthKey).Equals(widthPref)
                    && ReadPref(DisplaySettingsPanel.HeightKey).Equals(heightPref)
                    && ReadPref(DisplaySettingsPanel.FullscreenKey).Equals(fullscreenPref),
                    "unconfirmed preview does not overwrite saved display preferences");

                panel.RevertPreviewForProbe();
                Check(!panel.IsPreviewPending && panel.OriginalMode.width == originalWidth && panel.OriginalMode.height == originalHeight
                    && panel.OriginalFullscreen == originalFullscreen, "cancel restores the exact original display snapshot");
                Check(ReadPref(DisplaySettingsPanel.WidthKey).Equals(widthPref)
                    && ReadPref(DisplaySettingsPanel.HeightKey).Equals(heightPref)
                    && ReadPref(DisplaySettingsPanel.FullscreenKey).Equals(fullscreenPref),
                    "cancel leaves saved display preferences unchanged");

                int nextVSync = (Mathf.Clamp(originalVSync, 0, 2) + 1) % 3;
                panel.ApplyVSync(nextVSync);
                Check(QualitySettings.vSyncCount == nextVSync && PlayerPrefs.GetInt(DisplaySettingsPanel.VSyncKey, -1) == nextVSync,
                    "VSync changes apply and persist immediately");
                string[] qualityNames = QualitySettings.names;
                if (qualityNames != null && qualityNames.Length > 0)
                {
                    int nextQuality = (originalQuality + 1) % qualityNames.Length;
                    panel.ApplyQuality(nextQuality);
                    Check(QualitySettings.GetQualityLevel() == nextQuality
                        && PlayerPrefs.GetInt(DisplaySettingsPanel.QualityKey, -1) == nextQuality,
                        "quality changes apply and persist immediately");
                }

                // Exercise the confirmation path, then restore player settings before quitting.
                panel.BeginPreviewForProbe(target, !originalFullscreen);
                panel.KeepPreviewForProbe();
                Check(!panel.IsPreviewPending
                    && PlayerPrefs.GetInt(DisplaySettingsPanel.WidthKey, -1) == target.width
                    && PlayerPrefs.GetInt(DisplaySettingsPanel.HeightKey, -1) == target.height
                    && PlayerPrefs.GetInt(DisplaySettingsPanel.FullscreenKey, -1) == (!originalFullscreen ? 1 : 0),
                    "Keep confirms the preview and persists the selected mode");
            }
            finally
            {
                panel.RevertPreviewForProbe();
                Screen.SetResolution(originalWidth, originalHeight, originalFullscreen);
                QualitySettings.vSyncCount = originalVSync;
                QualitySettings.SetQualityLevel(originalQuality, true);
                RestorePref(DisplaySettingsPanel.WidthKey, widthPref);
                RestorePref(DisplaySettingsPanel.HeightKey, heightPref);
                RestorePref(DisplaySettingsPanel.FullscreenKey, fullscreenPref);
                RestorePref(DisplaySettingsPanel.QualityKey, qualityPref);
                RestorePref(DisplaySettingsPanel.VSyncKey, vSyncPref);
                PlayerPrefs.Save();
                panel.CancelAndClose();
            }

            yield return null;
            Finish();
        }

        private readonly struct PrefValue : IEquatable<PrefValue>
        {
            public readonly bool exists;
            public readonly int value;
            public PrefValue(bool exists, int value) { this.exists = exists; this.value = value; }
            public bool Equals(PrefValue other) => exists == other.exists && (!exists || value == other.value);
            public override bool Equals(object obj) => obj is PrefValue other && Equals(other);
            public override int GetHashCode() => unchecked((exists ? 397 : 0) ^ value);
        }

        private static PrefValue ReadPref(string key) =>
            new PrefValue(PlayerPrefs.HasKey(key), PlayerPrefs.GetInt(key, 0));

        private static void RestorePref(string key, PrefValue value)
        {
            if (value.exists) PlayerPrefs.SetInt(key, value.value);
            else PlayerPrefs.DeleteKey(key);
        }

        private void Check(bool passed, string message)
        {
            if (passed) Debug.Log("[DisplaySettingsProbe] PASS: " + message);
            else { failures++; Debug.LogError("[DisplaySettingsProbe] FAIL: " + message); }
        }

        private void Finish()
        {
            Debug.Log(failures == 0 ? "[DisplaySettingsProbe] ALL CHECKS PASSED" : $"[DisplaySettingsProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }
    }
}
