using SheltreSim.Survival;
using UnityEngine;

namespace SheltreSim.Audio
{
    /// <summary>
    /// The player's body: footsteps, jump, landing, being hurt, eating, drinking, healing, heartbeat,
    /// hunger and thirst, and death. Added to the player at runtime and only observes public state,
    /// so it cannot change how the player moves and survival code needs no audio of its own.
    /// </summary>
    public sealed class PlayerAudio : MonoBehaviour
    {
        private const float LowHealth = 0.3f;
        private const float LowVital = 0.2f;
        private const float VitalRearm = 0.3f;
        private const float HurtThreshold = 2f; // Starving drains in tiny steps, and that isn't a blow.

        private static float suppressUntil;
        private static float restUntil;

        private CharacterController controller;
        private SurvivalStats stats;
        private float stride;
        private bool wasGrounded = true;
        private float airborneSeconds;
        private float fastestFall;
        private float nextHeartbeat;
        private bool wasDead;
        private bool hungerWarned;
        private bool thirstWarned;
        private float previousHealth;
        private float previousHunger;
        private float previousThirst;
        private float nextHurtVoice;

        /// <summary>A load rewrites position and every vital at once, and none of that is an event in the world.</summary>
        public static void SuppressLandingBriefly() => suppressUntil = Time.unscaledTime + 0.6f;

        /// <summary>Sleeping heals too, but it has its own sound rather than a bandage.</summary>
        public static void NoteRest() => restUntil = Time.unscaledTime + 0.5f;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            stats = GetComponent<SurvivalStats>();
            suppressUntil = Time.unscaledTime + 0.6f; // The first ground contact isn't a landing.
            wasDead = stats != null && stats.IsDead;
            RememberVitals();
        }

        private void RememberVitals()
        {
            if (stats == null) return;
            previousHealth = stats.health;
            previousHunger = stats.hunger;
            previousThirst = stats.thirst;
        }

        private void Update()
        {
            UpdateDeath();
            // Inventory is a modal, so eating and drinking happen while time is stopped.
            UpdateVitalChanges();
            if (Time.timeScale <= 0f || controller == null || !controller.enabled || (stats != null && stats.IsDead))
            {
                return;
            }
            UpdateMovement();
            UpdateVitals();
        }

        private void UpdateDeath()
        {
            if (stats == null) return;
            if (stats.IsDead && !wasDead && Time.unscaledTime > suppressUntil)
            {
                GameAudio.Play2D(Sfx.PlayerDeath, 0.9f);
                GameAudio.Play2D(Sfx.StingerDeath, 0.85f, 0f, 0.35f);
            }
            wasDead = stats.IsDead;
        }

        private void UpdateVitalChanges()
        {
            if (stats == null) return;
            if (Time.unscaledTime <= suppressUntil || stats.IsDead)
            {
                RememberVitals();
                return;
            }

            if (previousHealth - stats.health >= HurtThreshold)
            {
                GameAudio.Play2D(Sfx.HitFlesh, 0.65f, 0.08f);
                if (Time.unscaledTime >= nextHurtVoice)
                {
                    nextHurtVoice = Time.unscaledTime + 0.5f;
                    GameAudio.Play2D(Sfx.PlayerHurt, 0.75f, 0.05f);
                }
            }
            else if (stats.health - previousHealth >= 1f && Time.unscaledTime > restUntil)
            {
                GameAudio.Play2D(Sfx.Heal, 0.8f, 0f);
            }

            if (stats.hunger - previousHunger >= 0.5f) GameAudio.Play2D(Sfx.Eat, 0.8f);
            if (stats.thirst - previousThirst >= 0.5f) GameAudio.Play2D(Sfx.Drink, 0.8f);
            RememberVitals();
        }

        private void UpdateMovement()
        {
            Vector3 velocity = controller.velocity;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            bool grounded = controller.isGrounded;

            if (grounded)
            {
                if (!wasGrounded && airborneSeconds > 0.15f && Time.unscaledTime > suppressUntil)
                {
                    PlayLanding();
                }
                airborneSeconds = 0f;
                fastestFall = 0f;

                if (speed > 0.6f)
                {
                    // Longer strides at a run, but each stride still comes round faster.
                    stride += speed * Time.deltaTime;
                    float length = Mathf.Lerp(2.05f, 2.9f, Mathf.InverseLerp(4f, 7f, speed));
                    if (stride >= length)
                    {
                        stride = 0f;
                        float volume = Mathf.Lerp(0.3f, 0.48f, Mathf.InverseLerp(4f, 7f, speed));
                        GameAudio.Play2D(SurfaceAudio.FootstepAt(transform.position), volume, 0.07f);
                    }
                }
                else
                {
                    // Starting to walk should sound soon, not after a full silent stride.
                    stride = 1.4f;
                }
            }
            else
            {
                airborneSeconds += Time.deltaTime;
                fastestFall = Mathf.Max(fastestFall, -velocity.y);
                if (wasGrounded && velocity.y > 2f)
                {
                    GameAudio.Play2D(Sfx.Jump, 0.4f, 0.08f);
                    GameAudio.Play2D(Sfx.PlayerBreath, 0.35f, 0.08f);
                }
            }
            wasGrounded = grounded;
        }

        private void PlayLanding()
        {
            string surface = SurfaceAudio.FootstepAt(transform.position);
            if (fastestFall > 5f)
            {
                GameAudio.Play2D(Sfx.Land, Mathf.Lerp(0.4f, 0.8f, Mathf.InverseLerp(5f, 12f, fastestFall)), 0.08f);
            }
            GameAudio.Play2D(surface, 0.55f, 0.05f);
            stride = 0f;
        }

        private void UpdateVitals()
        {
            if (stats == null) return;
            float health01 = stats.health / Mathf.Max(1f, stats.maxHealth);
            if (health01 < LowHealth && Time.unscaledTime >= nextHeartbeat)
            {
                float danger = 1f - health01 / LowHealth;
                nextHeartbeat = Time.unscaledTime + Mathf.Lerp(1.25f, 0.8f, danger);
                GameAudio.Play2D(Sfx.Heartbeat, Mathf.Lerp(0.3f, 0.6f, danger), 0f);
            }

            hungerWarned = WarnOnce(stats.hunger / Mathf.Max(1f, stats.maxHunger), hungerWarned);
            thirstWarned = WarnOnce(stats.thirst / Mathf.Max(1f, stats.maxThirst), thirstWarned);
        }

        private static bool WarnOnce(float amount, bool warned)
        {
            if (!warned && amount < LowVital)
            {
                GameAudio.Play2D(Sfx.WarnVital, 0.5f);
                return true;
            }
            return warned && amount < VitalRearm;
        }
    }
}
