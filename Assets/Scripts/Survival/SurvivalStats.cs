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
            if (IsDead)
            {
                return;
            }

            // The death check must sit ABOVE the decayPaused gate, not below it. decayPaused
            // only means "survival stats don't tick down on their own" (the pre-apocalypse
            // Building phase); it does NOT mean the player is invulnerable — EnemyAI writes
            // health directly and has no notion of game phase, so raiders outside the bunker
            // can empty your health bar before the apocalypse button is ever pressed. With the
            // check below the gate, that left the player sitting at 0 HP, alive and unfrozen,
            // and then dying instantly the moment they triggered the apocalypse.
            if (health <= 0f)
            {
                Die();
                return;
            }

            if (decayPaused)
            {
                return;
            }

            hunger = Mathf.Clamp(hunger - hungerDecayPerSecond * Time.deltaTime, 0f, maxHunger);
            thirst = Mathf.Clamp(thirst - thirstDecayPerSecond * Time.deltaTime, 0f, maxThirst);

            if (hunger <= 0f || thirst <= 0f)
            {
                health = Mathf.Clamp(health - healthDecayWhenStarving * Time.deltaTime, 0f, maxHealth);
            }

            if (health <= 0f)
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
            SetPlayerControlEnabled(false);
        }

        /// <summary>
        /// Save/load entry point for the three vitals. Goes through here rather than letting
        /// callers poke the public fields directly, because restoring health is not just a
        /// number change: loading a save made while alive, from a session where the player had
        /// already died, has to undo <see cref="Die"/> as well. Previously F9-after-death
        /// restored the saved health to the HUD but left IsDead true and FirstPersonController
        /// /PlayerInteractor disabled, so the loaded game was permanently frozen and
        /// unrecoverable while showing a healthy player.
        /// </summary>
        public void RestoreVitals(float restoredHunger, float restoredThirst, float restoredHealth)
        {
            hunger = Mathf.Clamp(restoredHunger, 0f, maxHunger);
            thirst = Mathf.Clamp(restoredThirst, 0f, maxThirst);
            health = Mathf.Clamp(restoredHealth, 0f, maxHealth);

            if (IsDead && health > 0f)
            {
                IsDead = false;
                SetPlayerControlEnabled(true);
                Debug.Log("Player revived by load.");
            }
        }

        private void SetPlayerControlEnabled(bool value)
        {
            var controller = GetComponent<SheltreSim.Player.FirstPersonController>();
            if (controller != null)
            {
                controller.enabled = value;
            }

            var interactor = GetComponentInChildren<SheltreSim.Interaction.PlayerInteractor>(true);
            if (interactor != null)
            {
                interactor.enabled = value;
            }
        }
    }
}
