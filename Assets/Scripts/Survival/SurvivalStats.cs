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

        public float hungerDecayPerSecond = 0.5f;
        public float thirstDecayPerSecond = 0.8f;
        public float healthDecayWhenStarving = 2f;

        /// <summary>
        /// M7 — while true, Update does nothing (no decay at all). Lets the Building phase
        /// (before the apocalypse trigger) be stress-free; GameStateManager flips this off
        /// once the apocalypse sequence completes.
        /// </summary>
        public bool decayPaused;

        private bool isDead;

        private void Awake()
        {
            hunger = maxHunger;
            thirst = maxThirst;
            health = maxHealth;
        }

        private void Update()
        {
            if (isDead || decayPaused)
            {
                return;
            }

            hunger = Mathf.Clamp(hunger - hungerDecayPerSecond * Time.deltaTime, 0f, maxHunger);
            thirst = Mathf.Clamp(thirst - thirstDecayPerSecond * Time.deltaTime, 0f, maxThirst);

            if (hunger <= 0f || thirst <= 0f)
            {
                health = Mathf.Clamp(health - healthDecayWhenStarving * Time.deltaTime, 0f, maxHealth);
            }

            if (health <= 0f && !isDead)
            {
                isDead = true;
                Debug.Log("Player has died.");
            }
        }
    }
}
