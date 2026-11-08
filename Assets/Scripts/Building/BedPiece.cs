using SheltreSim.Interaction;
using SheltreSim.Survival;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Building
{
    /// <summary>
    /// M15 — playtester feedback (via an Opus-model review): the Bed build piece has been
    /// pure decoration since M2, with no sleep/rest function at all. Attached directly to
    /// the prefab asset (not to scene instances), same precedent as WorkbenchStation/
    /// WaterCollectorPiece (M11), so every placed copy — including the one M2 already
    /// furnishes the starting bunker with — gets the behavior via the normal
    /// prefab-instantiate path.
    ///
    /// Sleeping is blocked while a raider is nearby (no free safety nap mid-chase), heals
    /// the player, and charges a simulated-rest hunger/thirst cost so sleeping isn't a pure
    /// upside compared to just standing around. A blocked attempt is free — no cost is
    /// applied unless the sleep actually happens.
    /// </summary>
    public class BedPiece : MonoBehaviour, IInteractable
    {
        public float dangerCheckRadius = 15f;
        public float healAmount = 40f;
        public float simulatedRestSeconds = 60f;

        public string InteractPrompt => IsRaiderNearby()
            ? "Can't sleep — a raider is nearby"
            : "Press E to sleep";

        public void Interact(GameObject interactor)
        {
            if (IsRaiderNearby())
            {
                GameMessageBus.Post("Can't sleep — a raider is nearby.");
                return;
            }

            SurvivalStats stats = interactor.GetComponent<SurvivalStats>();
            if (stats == null)
            {
                stats = interactor.GetComponentInParent<SurvivalStats>();
            }

            if (stats == null)
            {
                Debug.LogError($"'{interactor.name}' has no SurvivalStats component — cannot sleep.");
                return;
            }

            if (stats.IsDead)
            {
                return;
            }

            stats.Heal(healAmount);

            float hungerCost = stats.hungerDecayPerSecond * simulatedRestSeconds;
            float thirstCost = stats.thirstDecayPerSecond * simulatedRestSeconds;
            stats.hunger = Mathf.Clamp(stats.hunger - hungerCost, 0f, stats.maxHunger);
            stats.thirst = Mathf.Clamp(stats.thirst - thirstCost, 0f, stats.maxThirst);

            GameMessageBus.Post($"Slept and recovered {healAmount} health (-{hungerCost:F0} hunger, -{thirstCost:F0} thirst).");
        }

        private bool IsRaiderNearby()
        {
            SheltreSim.Enemies.EnemyAI[] enemies = Object.FindObjectsByType<SheltreSim.Enemies.EnemyAI>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] == null)
                {
                    continue;
                }

                if (Vector3.Distance(enemies[i].transform.position, transform.position) <= dangerCheckRadius)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
