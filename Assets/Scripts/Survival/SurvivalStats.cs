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

        // Rebalanced per an Opus-model review's top balance flag: the previous rates (1.1/1.7,
        // 5 for starvation) killed a do-nothing player in ~79s after the apocalypse trigger —
        // thirst always hit zero before hunger ever mattered, and there was no time to make a
        // real "push deeper or head home" decision before dying. Target is now roughly 5-8
        // minutes of do-nothing survival: thirst empties in ~4 min (still the binding
        // constraint, same ordering as before), hunger in ~5.5 min, then starvation drains
        // health over another ~2 min — about 6 min total. See WaterCollectorPiece.secondsPerUnit
        // for the matching Water Collector retune (its old rate would now cover ~280% of thirst
        // drain instead of the intended ~70-90%).
        public float hungerDecayPerSecond = 0.3f;
        public float thirstDecayPerSecond = 0.42f;
        public float healthDecayWhenStarving = 0.8f;

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

            AdvanceNeeds(Time.deltaTime);
        }

        private void AdvanceNeeds(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            float hungerRate = Mathf.Max(0f, hungerDecayPerSecond);
            float thirstRate = Mathf.Max(0f, thirstDecayPerSecond);
            float untilHungry = hunger <= 0f ? 0f
                : hungerRate > 0f ? hunger / hungerRate : float.PositiveInfinity;
            float untilThirsty = thirst <= 0f ? 0f
                : thirstRate > 0f ? thirst / thirstRate : float.PositiveInfinity;

            hunger = Mathf.Clamp(hunger - hungerRate * seconds, 0f, maxHunger);
            thirst = Mathf.Clamp(thirst - thirstRate * seconds, 0f, maxThirst);
            // Only the part of this frame spent at zero needs damages health. This also
            // keeps a slow frame from charging a whole frame of starvation prematurely.
            float starvingSeconds = Mathf.Max(0f, seconds - Mathf.Min(untilHungry, untilThirsty));
            TakeDamage(Mathf.Max(0f, healthDecayWhenStarving) * starvingSeconds);
        }

        /// <summary>Applies damage and resolves death in the same call as a lethal hit.</summary>
        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
            {
                return;
            }

            health = Mathf.Clamp(health - amount, 0f, maxHealth);
            if (health <= 0f)
            {
                Die();
            }
        }

        /// <summary>
        /// M13 — direct field reference to the death screen, wired by M13DeathScreenSetup, same
        /// direct-reference convention SurvivalHUD/InventoryUI use elsewhere in this project
        /// rather than Unity's UnityEvent/listener indirection. Null-checked: SurvivalStats
        /// works (freeze-only, no screen) even if M13's setup tool hasn't run yet.
        /// </summary>
        public SheltreSim.Core.GameOverScreen gameOverScreen;

        /// <summary>
        /// Death previously only set a flag, logged to Console, and froze the player (movement
        /// and interaction disabled — FirstPersonController's OnDisable also unlocks the
        /// cursor) with no on-screen feedback at all: the only way out was F9 to load an old
        /// save, which doesn't exist if the player never saved. An Opus-model review flagged
        /// this, so death now also shows a "You Died" screen with a Restart option.
        /// </summary>
        private void Die()
        {
            if (IsDead)
            {
                return;
            }
            IsDead = true;
            Debug.Log("Player has died.");
            SetPlayerControlEnabled(false);
            gameOverScreen?.Show();
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

            if (health <= 0f)
            {
                Die();
                // An already-dead snapshot still needs its panel after the load operation
                // dismissed the previous modal.
                gameOverScreen?.Show();
                return;
            }

            bool wasDead = IsDead;
            IsDead = false;
            gameOverScreen?.Hide();
            if (wasDead)
            {
                SetPlayerControlEnabled(true);
                Debug.Log("Player revived by load.");
            }
        }

        /// <summary>
        /// M9 — called by InventoryUI's Eat button after Inventory.TryConsume has already
        /// removed the food item. No-op while dead: a dead player has no Update loop pushing
        /// hunger back down, and letting a corpse "eat" would be a confusing, meaningless
        /// inventory interaction rather than a useful recovery path (there is no recovery
        /// path — see Die()'s summary).
        /// </summary>
        public void Eat(float amount)
        {
            if (IsDead || health <= 0f || !IsPositiveFinite(amount))
            {
                return;
            }

            hunger = Mathf.Clamp(hunger + amount, 0f, maxHunger);
        }

        /// <summary>M10 — same shape as <see cref="Eat"/>, for drinkable resources.</summary>
        public void Drink(float amount)
        {
            if (IsDead || health <= 0f || !IsPositiveFinite(amount))
            {
                return;
            }

            thirst = Mathf.Clamp(thirst + amount, 0f, maxThirst);
        }

        /// <summary>M10 — same shape as <see cref="Eat"/>, for medicine resources.</summary>
        public void Heal(float amount)
        {
            if (IsDead || health <= 0f || !IsPositiveFinite(amount))
            {
                return;
            }

            health = Mathf.Clamp(health + amount, 0f, maxHealth);
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
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
