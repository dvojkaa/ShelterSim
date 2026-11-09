using System;
using UnityEngine;

namespace SheltreSim.Player
{
    /// <summary>
    /// Look sensitivity, field of view and the FPS counter. Machine settings in PlayerPrefs, like the volume
    /// settings (not save data). FirstPersonController listens to Changed and applies them.
    /// </summary>
    public static class GameplaySettings
    {
        public const float MinSensitivity = 0.03f, MaxSensitivity = 0.4f, DefaultSensitivity = 0.12f;
        public const float MinFov = 60f, MaxFov = 100f, DefaultFov = 60f;

        private const string SensitivityKey = "player.lookSensitivity";
        private const string FovKey = "player.fov";
        private const string FpsKey = "player.showFps";

        private static bool loaded;
        private static float sensitivity = DefaultSensitivity;
        private static float fov = DefaultFov;
        private static bool showFps;

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            loaded = false;
            Changed = null;
        }

        public static float LookSensitivity { get { Load(); return sensitivity; } }
        public static float FieldOfView { get { Load(); return fov; } }
        public static bool ShowFps { get { Load(); return showFps; } }

        public static void SetLookSensitivity(float value)
        {
            Load();
            sensitivity = Mathf.Clamp(Sanitize(value, DefaultSensitivity), MinSensitivity, MaxSensitivity);
            PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
            Changed?.Invoke();
        }

        public static void SetFieldOfView(float value)
        {
            Load();
            fov = Mathf.Clamp(Sanitize(value, DefaultFov), MinFov, MaxFov);
            PlayerPrefs.SetFloat(FovKey, fov);
            Changed?.Invoke();
        }

        public static void SetShowFps(bool value)
        {
            Load();
            showFps = value;
            PlayerPrefs.SetInt(FpsKey, value ? 1 : 0);
            Changed?.Invoke();
        }

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            sensitivity = Mathf.Clamp(Sanitize(PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity), DefaultSensitivity), MinSensitivity, MaxSensitivity);
            fov = Mathf.Clamp(Sanitize(PlayerPrefs.GetFloat(FovKey, DefaultFov), DefaultFov), MinFov, MaxFov);
            showFps = PlayerPrefs.GetInt(FpsKey, 0) == 1;
        }

        private static float Sanitize(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }
}
