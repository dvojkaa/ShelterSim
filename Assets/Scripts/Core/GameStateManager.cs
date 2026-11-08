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

        private Coroutine apocalypseSequence;
        private float apocalypseStartedAt;

        public float ApocalypseSecondsRemaining => CurrentPhase == GamePhase.Apocalypse
            ? Mathf.Max(0f, 3f - (Time.time - apocalypseStartedAt))
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
        }

        public void TriggerApocalypse()
        {
            if (Time.timeScale <= 0f || (stats != null && stats.IsDead)) return;
            if (CurrentPhase != GamePhase.Building)
            {
                return;
            }

            CurrentPhase = GamePhase.Apocalypse;
            apocalypseStartedAt = Time.time;
            GameMessageBus.Post("Emergency alert. Stay in your shelter. The apocalypse is beginning.");
            apocalypseSequence = StartCoroutine(ApocalypseSequence());
        }

        private IEnumerator ApocalypseSequence()
        {
            yield return new WaitForSeconds(2f);
            GameMessageBus.Post("The ground shakes. Keep food, water and a way home.");

            yield return new WaitForSeconds(1f);
            if (stats != null)
            {
                stats.decayPaused = false;
            }

            CurrentPhase = GamePhase.Survival;
            apocalypseSequence = null;
            GameMessageBus.Post("Survival has begun. Food and water now drain. Open I or Tab to use supplies.");
        }
    }
}
