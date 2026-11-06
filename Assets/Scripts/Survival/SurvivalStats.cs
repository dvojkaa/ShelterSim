using UnityEngine;

namespace SheltreSim.Survival
{
    public class SurvivalStats : MonoBehaviour
    {
        public float maxHunger = 100f;
        public float maxThirst = 100f;
        public float maxHealth = 100f;

        public float hunger;
        public float thirst;
        public float health;

        // Tuned after first playtest: the original rates (0.5/0.8, 2 for starvation) took
        // minutes before health visibly moved at all, reading as "health never drops" in a
        // short test session. Faster now: hunger/thirst empty in roughly 60-90s after the
        // apocalypse trigger, then starvation drains health in ~20s.
        public float hungerDecayPerSecond = 1.1f;
        public float thirstDecayPerSecond = 1.7f;
        public float healthDecayWhenStarving = 5f;

        /// <summary>
        /// M7 — while true, Update does nothing (no decay at all). Lets the Building phase
        /// (before the apocalypse trigger) be stress-free; GameStateManager flips this off
        /// once the apocalypse sequence completes.
        /// </summary>
        public bool decayPaused;

        /// <summary>
        /// Exposed so other systems (e.g. EnemyAI) can stop acting on a dead player instead
        /// of continuing to hit/attack a body that can't respond.
        /// </summary>
        public bool IsDead { get; private set; }

        private void Awake()
        {
            hunger = maxHunger;
            thirst = maxThirst;
            health = maxHealth;
        }

        private void Update()
        {
            if (IsDead || decayPaused)
            {
                return;
            }

            hunger = Mathf.Clamp(hunger - hungerDecayPerSecond * Time.deltaTime, 0f, maxHunger);
            thirst = Mathf.Clamp(thirst - thirstDecayPerSecond * Time.deltaTime, 0f, maxThirst);

            if (hunger <= 0f || thirst <= 0f)
            {
                health = Mathf.Clamp(health - healthDecayWhenStarving * Time.deltaTime, 0f, maxHealth);
            }

            if (health <= 0f && !IsDead)
            {
                Die();
            }
        }

        /// <summary>
        /// Death previously only set a flag and logged to Console — invisible to a player who
        /// isn't watching it. Now it actually freezes the player: movement and interaction are
        /// disabled (FirstPersonController's OnDisable also unlocks the cursor, which doubles
        /// as a plain "something happened" signal). No respawn/game-over screen yet — that's
        /// further than this bug-fix pass needs to go.
        /// </summary>
        private void Die()
        {
            IsDead = true;
            Debug.Log("Player has died.");

            var controller = GetComponent<SheltreSim.Player.FirstPersonController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            var interactor = GetComponentInChildren<SheltreSim.Interaction.PlayerInteractor>();
            if (interactor != null)
            {
                interactor.enabled = false;
            }
        }
    }
}
