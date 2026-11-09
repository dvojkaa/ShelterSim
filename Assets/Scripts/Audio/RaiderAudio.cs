using System.Collections.Generic;
using SheltreSim.Enemies;
using UnityEngine;

namespace SheltreSim.Audio
{
    /// <summary>
    /// A raider's footsteps and voice, and how many are hunting the player (which drives the tension
    /// music). Added at runtime by GameAudio. Purely observes the public state of EnemyAI, so it
    /// cannot change how a raider behaves and EnemyAI needs no audio code of its own.
    ///
    /// "Hunting" is EnemyAI.IsChasing, not inferred from movement: chunk patrols walk without having
    /// noticed anyone, and a raider can stand next to the player behind a wall.
    /// </summary>
    public sealed class RaiderAudio : MonoBehaviour
    {
        private const float StepHearingRange = 26f;
        private const float SwingAngle = 8f;
        private const float SwingCooldown = 0.9f;

        private static readonly HashSet<RaiderAudio> chasers = new HashSet<RaiderAudio>();
        private static float lastVoiceTime = float.NegativeInfinity;
        private static float quietUntil;

        public static int ChaserCount => chasers.Count;

        private EnemyAI enemy;
        private CharacterController controller;
        private Transform weapon;
        private Quaternion weaponRest;
        private float previousHealth;
        private float stride;
        private bool wasChasing;
        private float nextGrowl;
        private float nextSwing;

        public static void ResetStatics()
        {
            chasers.Clear();
            lastVoiceTime = float.NegativeInfinity;
            quietUntil = 0f;
        }

        /// <summary>A load rewrites raider health and chase state at once, and neither is an event in the world.</summary>
        public static void NoteGameLoaded() => quietUntil = Time.unscaledTime + 0.6f;

        private void Awake()
        {
            enemy = GetComponent<EnemyAI>();
            controller = GetComponent<CharacterController>();
            weapon = transform.Find("WeaponProp");
            if (weapon != null) weaponRest = weapon.localRotation;
            previousHealth = enemy != null ? enemy.health : 0f;
        }

        private void OnEnable()
        {
            // A load can rewrite health while this is disabled, and that isn't a hit.
            if (enemy != null) previousHealth = enemy.health;
        }

        private void OnDisable()
        {
            chasers.Remove(this);
            wasChasing = false;
        }

        private void OnDestroy()
        {
            chasers.Remove(this);
            // EnemyAI sets IsDead before Destroy. A load or a scene unload removes raiders without it.
            if (enemy != null && enemy.IsDead)
            {
                Voice(Sfx.RaiderDeath, transform.position + Vector3.up * 1.4f, 0.9f);
                GameAudio.PlayAt(Sfx.Land, transform.position, 0.6f, 25f, 0.1f, 0.45f);
            }
        }

        private static void Voice(string key, Vector3 position, float volume)
        {
            lastVoiceTime = Time.unscaledTime;
            GameAudio.PlayAt(key, position, volume, 45f, 0.07f);
        }

        private void Update()
        {
            if (enemy == null || enemy.IsDead || Time.timeScale <= 0f) return;

            UpdateHurt();
            UpdateWeapon();

            Vector3 velocity = controller != null && controller.enabled ? controller.velocity : Vector3.zero;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;

            bool chasing = enemy.IsChasing;
            if (chasing != wasChasing)
            {
                wasChasing = chasing;
                if (chasing)
                {
                    chasers.Add(this);
                    Bark(Sfx.RaiderAlert, 0.85f);
                    nextGrowl = Time.time + 5f + (Time.frameCount & 7);
                }
                else
                {
                    chasers.Remove(this);
                }
            }
            else if (chasing && Time.time >= nextGrowl)
            {
                nextGrowl = Time.time + 7f + (Time.frameCount & 7);
                Bark(Sfx.RaiderGrowl, 0.55f);
            }

            UpdateSteps(speed);
        }

        private float lastHurtTime = -10f;

        private void UpdateHurt()
        {
            // Rate-limited against its own last cry only, so a laser beam can't spam it but a first hit always cries out.
            if (enemy.health < previousHealth - 0.01f && Time.unscaledTime >= quietUntil && Time.unscaledTime - lastHurtTime >= 0.3f)
            {
                lastHurtTime = Time.unscaledTime;
                Voice(Sfx.RaiderHurt, transform.position + Vector3.up * 1.4f, 0.85f);
            }
            previousHealth = enemy.health;
        }

        /// <summary>The wind-up is the player's warning, so it sounds when the arm starts to move, not when the hit lands.</summary>
        private void UpdateWeapon()
        {
            if (weapon == null || Time.time < nextSwing) return;
            if (Quaternion.Angle(weaponRest, weapon.localRotation) > SwingAngle)
            {
                nextSwing = Time.time + SwingCooldown;
                GameAudio.PlayAt(Sfx.RaiderSwing, transform.position + Vector3.up * 1.2f, 0.8f, 18f);
            }
        }

        private void Bark(string key, float volume)
        {
            // A pack spotting the player at once is one shout and then another, never a chorus,
            // and a raider that was just hit has already said something.
            if (Time.unscaledTime < quietUntil || Time.unscaledTime - lastVoiceTime < 0.7f) return;
            Voice(key, transform.position + Vector3.up * 1.5f, volume);
        }

        private void UpdateSteps(float speed)
        {
            if (speed < 0.5f || controller == null || !controller.isGrounded) return;
            stride += speed * Time.deltaTime;
            if (stride < 1.9f) return;
            stride = 0f;
            GameAudio.PlayAt(SurfaceAudio.FootstepAt(transform.position), transform.position, 0.75f, StepHearingRange, 0.1f, 0f, 0.82f);
        }
    }
}
