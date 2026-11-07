using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SheltreSim.Combat
{
    /// <summary>
    /// Playtest feedback: "I want to attack, at least bash the enemies, with an animation."
    /// Purely procedural swing (rotate a weapon prop forward and back) - no Animator/clips,
    /// since this project has no character rig to animate.
    /// </summary>
    public class MeleeAttacker : MonoBehaviour
    {
        public SheltreSim.Building.BuildModeController buildModeController;
        public Transform weaponVisual;

        public float attackRange = 2.5f;
        public float attackRadius = 0.5f;
        public float attackDamage = 20f;
        public float attackCooldown = 0.6f;
        public float swingDuration = 0.1f;
        public float returnDuration = 0.15f;

        private MeleeInputActions input;
        private float lastAttackTime = float.NegativeInfinity;
        private Quaternion restRotation;

        private void Awake()
        {
            input = new MeleeInputActions();

            if (weaponVisual != null)
            {
                restRotation = weaponVisual.localRotation;
            }
        }

        private void OnEnable()
        {
            input.Enable();
            input.Attack.performed += OnAttackPerformed;
        }

        private void OnDisable()
        {
            input.Attack.performed -= OnAttackPerformed;
            input.Disable();
            input.Dispose();
        }

        private void OnAttackPerformed(InputAction.CallbackContext context)
        {
            if (buildModeController != null && buildModeController.IsBuildModeActive)
            {
                // Left-click already places a build piece in this mode - don't also attack.
                return;
            }

            if (Time.time - lastAttackTime < attackCooldown)
            {
                return;
            }

            lastAttackTime = Time.time;

            Collider target = FindSwingTarget();
            if (target != null)
            {
                SheltreSim.Enemies.EnemyAI enemy = target.GetComponent<SheltreSim.Enemies.EnemyAI>();
                if (enemy == null)
                {
                    enemy = target.GetComponentInParent<SheltreSim.Enemies.EnemyAI>();
                }

                if (enemy != null)
                {
                    enemy.TakeDamage(attackDamage);
                    Debug.Log($"Hit {enemy.name} for {attackDamage} damage.");
                }
                else
                {
                    Debug.Log("Swing missed - nothing to hit.");
                }
            }
            else
            {
                Debug.Log("Swing missed - nothing in range.");
            }

            StartCoroutine(SwingWeapon());
        }

        /// <summary>
        /// Sweeps forward from the camera and returns the nearest collider the swing would
        /// connect with, or null if the swing hits nothing at all.
        ///
        /// Deliberately NOT the single-hit Physics.SphereCast. Unity documents that overload as
        /// "will not detect colliders for which the sphere overlaps the collider" at the start
        /// of the sweep. That rule is why this cast originating inside the player's own body is
        /// harmless - the CameraPivot sits at Y=1.6, well inside the CharacterController capsule
        /// (radius 0.35, spanning Y 0-1.8), and the sphere therefore starts overlapping it, so
        /// the player's own collider is silently skipped rather than self-hit at distance 0.
        ///
        /// The same rule bites the other way, though, and that part IS a real bug: an enemy
        /// capsule is radius 0.5 and the player's controller is radius 0.35, so walking into an
        /// enemy closes the gap to ~0.85m - inside this cast's 0.5 + 0.5 = 1.0m overlap
        /// threshold. With the single-hit overload that enemy overlaps the sphere at t=0 and is
        /// skipped too, meaning you cannot hit an enemy you are pressed right up against, which
        /// is exactly when a melee swing should land. SphereCastAll DOES return those
        /// zero-distance overlap hits, so sweep with it and filter explicitly: drop anything
        /// belonging to the player's own hierarchy, then take the nearest of what's left. Taking
        /// the nearest hit of ANY kind (rather than the nearest enemy) preserves the single-hit
        /// behaviour where a wall between you and a raider blocks the swing.
        /// </summary>
        private Collider FindSwingTarget()
        {
            RaycastHit[] hits = Physics.SphereCastAll(transform.position, attackRadius, transform.forward, attackRange);

            Collider nearest = null;
            float nearestDistance = float.PositiveInfinity;
            Transform playerRoot = transform.root;

            foreach (RaycastHit candidate in hits)
            {
                if (candidate.collider.transform.root == playerRoot)
                {
                    // The player's own CharacterController (and anything else parented under
                    // the player rig) - never a valid melee target.
                    continue;
                }

                if (candidate.distance < nearestDistance)
                {
                    nearestDistance = candidate.distance;
                    nearest = candidate.collider;
                }
            }

            return nearest;
        }

        private IEnumerator SwingWeapon()
        {
            if (weaponVisual == null)
            {
                yield break;
            }

            Quaternion swungRotation = restRotation * Quaternion.Euler(-60f, 0f, 0f);

            float t = 0f;
            while (t < swingDuration)
            {
                t += Time.deltaTime;
                weaponVisual.localRotation = Quaternion.Slerp(restRotation, swungRotation, t / swingDuration);
                yield return null;
            }
            weaponVisual.localRotation = swungRotation;

            t = 0f;
            while (t < returnDuration)
            {
                t += Time.deltaTime;
                weaponVisual.localRotation = Quaternion.Slerp(swungRotation, restRotation, t / returnDuration);
                yield return null;
            }
            weaponVisual.localRotation = restRotation;
        }
    }
}
