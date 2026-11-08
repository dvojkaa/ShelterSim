using System.Collections;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Core
{
    public enum GamePhase
    {
        Building,
        Apocalypse,
        Survival
    }

    public class GameStateManager : MonoBehaviour
    {
        public GamePhase CurrentPhase { get; private set; } = GamePhase.Building;

        public SheltreSim.Survival.SurvivalStats stats;

        /// <summary>Warning between the sirens and the bombs: long enough to get down the stairs from anywhere nearby.</summary>
        public float warningSeconds = 45f;
        public float surfaceBlastDamage = 40f;

        private Coroutine apocalypseSequence;
        private float apocalypseStartedAt;

        public float ApocalypseSecondsRemaining => CurrentPhase == GamePhase.Apocalypse
            ? Mathf.Max(0f, warningSeconds - (Time.time - apocalypseStartedAt))
            : 0f;

        private void Awake()
        {
            if (stats != null)
            {
                stats.decayPaused = true;
            }
        }

        /// <summary>
        /// Restores a loaded phase directly, bypassing TriggerApocalypse/ApocalypseSequence
        /// entirely — a loaded save that was already in Survival should not re-play "Sirens
        /// wail..." or the shake-delay coroutine, it should just BE in that phase immediately.
        /// Called by SaveManager on load, when wired.
        /// </summary>
        public void RestorePhase(GamePhase phase)
        {
            if (apocalypseSequence != null)
            {
                StopCoroutine(apocalypseSequence);
                apocalypseSequence = null;
            }
            // The transition is transient. Loading it finishes the alarm rather than leaving
            // a permanent Apocalypse state with no coroutine to advance it.
            if (phase == GamePhase.Apocalypse) phase = GamePhase.Survival;
            if (phase != GamePhase.Building && phase != GamePhase.Survival) phase = GamePhase.Building;
            CurrentPhase = phase;
            if (stats != null)
            {
                stats.decayPaused = (phase == GamePhase.Building);
            }
            SheltreSim.World.WeatherPresentation.ApocalypseAmount = phase == GamePhase.Building ? 0f : 1f;
            if (HomeBase.Active != null) HomeBase.Active.RefreshVisuals();
        }

        public void TriggerApocalypse()
        {
            if (Time.timeScale <= 0f || (stats != null && stats.IsDead)) return;
            if (CurrentPhase != GamePhase.Building)
            {
                return;
            }

            CurrentPhase = GamePhase.Apocalypse;
            SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.Siren, 0.6f, 0f, 0.3f);
            apocalypseStartedAt = Time.time;
            GameMessageBus.Post("EMERGENCY ALERT. Missiles inbound. Get underground, now.");
            apocalypseSequence = StartCoroutine(ApocalypseSequence());
        }

        private IEnumerator ApocalypseSequence()
        {
            // Sirens repeat through the warning. The world doesn't change until the bombs land.
            const float sirenEvery = 15f;
            float elapsed = 0f;
            while (elapsed + sirenEvery < warningSeconds)
            {
                yield return new WaitForSeconds(sirenEvery);
                elapsed += sirenEvery;
                SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.Siren, 0.6f, 0f, 0.3f);
            }
            yield return new WaitForSeconds(Mathf.Max(0f, warningSeconds - elapsed));

            SheltreSim.World.WeatherPresentation.Flash(3.5f);
            SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.Quake, 0.95f, 0f);
            if (stats != null)
            {
                stats.decayPaused = false;
            }
            CurrentPhase = GamePhase.Survival;
            apocalypseSequence = null;
            // Swapped under the flash: the town burns, and loot, raiders and animals appear.
            if (SheltreSim.World.ProceduralWorld.Active != null) SheltreSim.World.ProceduralWorld.Active.SetRuined(true);
            SheltreSim.World.WeatherPresentation.ApocalypseAmount = 1f;
            if (HomeBase.Active != null) HomeBase.Active.RefreshVisuals();

            bool sheltered = stats == null || SheltreSim.World.WorldLayout.UndergroundAmount(stats.transform.position + Vector3.up) > 0.5f;
            if (!sheltered)
            {
                stats.TakeDamage(surfaceBlastDamage);
                GameMessageBus.Post("Caught in the open. The blast burns you.");
            }
            GameMessageBus.Post("The ground shakes. The town is burning. Food and water now drain.");
            GameMessageBus.Post("Survive: scavenge, craft, build. C: craft by hand. I or Tab: supplies.");
        }
    }
}
