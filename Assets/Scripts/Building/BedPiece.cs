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

        private float nextDangerCheck;
        private bool dangerNearby;

        public string InteractPrompt => IsRaiderNearby()
            ? "Can't sleep — a raider is nearby"
            : "Press E to sleep";

        public void Interact(GameObject interactor)
        {
            if (interactor == null)
            {
                return;
            }
            if (IsRaiderNearby(true))
            {
                SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.UiDeny, 0.35f);
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

            if (stats.IsDead || stats.health <= 0f)
            {
                return;
            }

            if (stats.health >= stats.maxHealth)
            {
                GameMessageBus.Post("You are already fully rested.");
                return;
            }

            float restSeconds = Mathf.Max(0f, simulatedRestSeconds);
            float hungerCost = Mathf.Max(0f, stats.hungerDecayPerSecond) * restSeconds;
            float thirstCost = Mathf.Max(0f, stats.thirstDecayPerSecond) * restSeconds;
            if (stats.hunger <= hungerCost || stats.thirst <= thirstCost)
            {
                GameMessageBus.Post($"Eat and drink before resting (needs more than {hungerCost:F0} hunger and {thirstCost:F0} thirst).");
                return;
            }

            SheltreSim.Audio.PlayerAudio.NoteRest();
            float previousHealth = stats.health;
            stats.Heal(healAmount);
            if (stats.health <= previousHealth)
            {
                return;
            }
            stats.hunger = Mathf.Clamp(stats.hunger - hungerCost, 0f, stats.maxHunger);
            stats.thirst = Mathf.Clamp(stats.thirst - thirstCost, 0f, stats.maxThirst);

            SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.Sleep, 0.8f, 0f);
            GameMessageBus.Post($"Rested and recovered {stats.health - previousHealth:F0} health (-{hungerCost:F0} hunger, -{thirstCost:F0} thirst).");
        }

        private bool IsRaiderNearby(bool refresh = false)
        {
            if (!refresh && Time.time < nextDangerCheck)
            {
                return dangerNearby;
            }
            nextDangerCheck = Time.time + 0.5f;
            dangerNearby = false;
            float radiusSquared = Mathf.Max(0f, dangerCheckRadius) * Mathf.Max(0f, dangerCheckRadius);
            SheltreSim.Enemies.EnemyAI[] enemies = Object.FindObjectsByType<SheltreSim.Enemies.EnemyAI>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] == null || !enemies[i].isActiveAndEnabled || enemies[i].health <= 0f)
                {
                    continue;
                }

                if ((enemies[i].transform.position - transform.position).sqrMagnitude <= radiusSquared)
                {
                    dangerNearby = true;
                    return true;
                }
            }

            return false;
        }
    }
}
