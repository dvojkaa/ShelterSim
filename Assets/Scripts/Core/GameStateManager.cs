using System.Collections;
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

        private void Awake()
        {
            if (stats != null)
            {
                stats.decayPaused = true;
            }
        }

        public void TriggerApocalypse()
        {
            if (CurrentPhase != GamePhase.Building)
            {
                Debug.LogWarning("Apocalypse already triggered — ignoring.");
                return;
            }

            CurrentPhase = GamePhase.Apocalypse;
            Debug.Log("Sirens wail in the distance...");
            StartCoroutine(ApocalypseSequence());
        }

        private IEnumerator ApocalypseSequence()
        {
            yield return new WaitForSeconds(2f);
            Debug.Log("The ground shakes. It's begun.");

            yield return new WaitForSeconds(1f);
            if (stats != null)
            {
                stats.decayPaused = false;
            }

            CurrentPhase = GamePhase.Survival;
            Debug.Log("Survival has begun — hunger, thirst, and health are now ticking. Scavenge outside, build, and endure.");
        }
    }
}
