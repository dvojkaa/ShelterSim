using System;
using System.Collections;
using SheltreSim.Core;
using SheltreSim.UI;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>Tests contract deadline, pause behavior, countdown save state, and the one-time siren trigger.</summary>
    public sealed class RadioNewsProbe : MonoBehaviour
    {
        private int failures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--radio-news-probe") >= 0)
                new GameObject(nameof(RadioNewsProbe)).AddComponent<RadioNewsProbe>();
        }

        private IEnumerator Start()
        {
            if (ModalUI.IsOpen) ModalUI.DismissCurrent();
            Time.timeScale = 1f;
            yield return null;
            yield return null;

            HomeBase home = HomeBase.Active;
            GameStateManager phase = FindFirstObjectByType<GameStateManager>();
            WorldAtmosphere atmosphere = FindFirstObjectByType<WorldAtmosphere>();
            RadioNewsCountdown radio = home != null ? home.GetComponent<RadioNewsCountdown>() : null;
            Check(home != null && phase != null && atmosphere != null && radio != null,
                "home, phase, world clock and radio timer exist");
            if (home == null || phase == null || atmosphere == null || radio == null) { Finish(); yield break; }

            Check(phase.CurrentPhase == GamePhase.Building && !home.HasContract, "probe begins before a new contract");
            string contractResult;
            Check(home.TryBuyContract(out contractResult), "the bunker contract can be signed");
            Check(radio.IsArmed && Mathf.Abs(radio.SecondsRemaining - RadioNewsCountdown.DeadlineSeconds) < 1f,
                "signing starts a 600 second game-time deadline");

            float beforePause = radio.SecondsRemaining;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(.05f);
            Check(Mathf.Abs(radio.SecondsRemaining - beforePause) < .05f, "pausing the game freezes the radio deadline");
            Time.timeScale = 1f;

            atmosphere.RestoreTime(atmosphere.ElapsedSeconds + 200f);
            yield return null;
            float remaining = radio.SecondsRemaining;
            Check(remaining > 390f && remaining < 400f, $"world time advances the deadline ({remaining:F1}s remain)");
            string saved = radio.CaptureState();
            atmosphere.RestoreTime(atmosphere.ElapsedSeconds + 50f);
            radio.RestoreState(saved);
            Check(Mathf.Abs(radio.SecondsRemaining - remaining) < .1f,
                "save restore preserves remaining countdown instead of restarting it");

            int bulletins = 0;
            Action<string> listen = message => { if (message.StartsWith("RADIO NEWS:", StringComparison.Ordinal)) bulletins++; };
            GameMessageBus.OnMessage += listen;
            try
            {
                phase.warningSeconds = 1f;
                atmosphere.RestoreTime(atmosphere.ElapsedSeconds + radio.SecondsRemaining + 1f);
                yield return null;
                Check(radio.BulletinDelivered && !radio.IsArmed, "deadline delivers the one-time radio bulletin");
                Check(phase.CurrentPhase == GamePhase.Apocalypse, "missed deadline starts the normal siren/bomb phase");
                Check(bulletins == 1, $"radio bulletin fires once ({bulletins})");
            }
            finally { GameMessageBus.OnMessage -= listen; }

            Finish();
        }

        private void Check(bool condition, string message)
        {
            if (condition) Debug.Log("[RadioNewsProbe] PASS: " + message);
            else { failures++; Debug.LogError("[RadioNewsProbe] FAIL: " + message); }
        }

        private void Finish()
        {
            Time.timeScale = 1f;
            Debug.Log(failures == 0 ? "[RadioNewsProbe] ALL CHECKS PASSED" : $"[RadioNewsProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }
    }
}
