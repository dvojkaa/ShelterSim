using System.Collections;
using SheltreSim.Enemies;
using SheltreSim.Player;
using SheltreSim.Survival;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SheltreSim.Combat
{
    /// <summary>One melee weapon with a procedural swing and a forgiving forward sweep.</summary>
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
        private SurvivalStats stats;
        private FirstPersonController playerController;
        private Transform playerRoot;
        private float lastAttackTime = float.NegativeInfinity;
        private Quaternion restRotation;
        private Coroutine swingCoroutine;

        public void ApplyWeaponUpgrade(float bonusDamage)
        {
            if (bonusDamage > 0f && !float.IsNaN(bonusDamage) && !float.IsInfinity(bonusDamage))
            {
                attackDamage += bonusDamage;
            }
        }

        private void Awake()
        {
            input = new MeleeInputActions();
            stats = GetComponentInParent<SurvivalStats>();
            playerController = GetComponentInParent<FirstPersonController>();
            playerRoot = playerController != null ? playerController.transform : transform.root;
            if (weaponVisual != null)
            {
                restRotation = weaponVisual.localRotation;
            }
        }

        private void OnEnable()
        {
            input.Attack.performed += OnAttackPerformed;
            input.Enable();
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.Attack.performed -= OnAttackPerformed;
                input.Disable();
            }
            ResetSwing();
        }

        private void OnDestroy() => input?.Dispose();

        private void Update()
        {
            if (swingCoroutine != null && ((stats != null && (stats.IsDead || stats.health <= 0f)) ||
                (buildModeController != null && buildModeController.IsBuildModeActive)))
            {
                ResetSwing();
            }
        }

        private void OnAttackPerformed(InputAction.CallbackContext context)
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0f || Cursor.lockState != CursorLockMode.Locked ||
                (stats != null && (stats.IsDead || stats.health <= 0f)) ||
                (playerController != null && !playerController.isActiveAndEnabled) ||
                (buildModeController != null && buildModeController.IsBuildModeActive) ||
                Time.time - lastAttackTime < Mathf.Max(0.05f, attackCooldown))
            {
                return;
            }

            lastAttackTime = Time.time;
            Collider target = FindSwingTarget();
            EnemyAI enemy = target != null ? target.GetComponentInParent<EnemyAI>() : null;
            if (enemy != null)
            {
                enemy.TakeDamage(attackDamage);
                GameMessageBus.Post(enemy.IsDead
                    ? "Raider defeated."
                    : $"Hit raider for {attackDamage:0} damage ({enemy.health:0} HP left).");
            }
            else
            {
                GameMessageBus.Post(target != null ? "Swing blocked." : "Swing missed — move closer and aim at the raider.");
            }

            ResetSwing();
            swingCoroutine = StartCoroutine(SwingWeapon());
        }

        /// <summary>
        /// SphereCastAll preserves initial overlaps, which are essential at point-blank range.
        /// Their distance/point carry no direction, so validate enemies against the view and a
        /// separate sight ray. Nearby scenery beside the player cannot swallow the swing, and
        /// an overlapping enemy behind a thin wall or behind the camera cannot be hit.
        /// </summary>
        private Collider FindSwingTarget()
        {
            RaycastHit[] hits = Physics.SphereCastAll(transform.position, Mathf.Max(0.01f, attackRadius),
                transform.forward, Mathf.Max(0f, attackRange), ~0, QueryTriggerInteraction.Ignore);
            Collider nearestEnemy = null;
            Collider nearestObstacle = null;
            float enemyDistance = float.PositiveInfinity;
            float obstacleDistance = float.PositiveInfinity;

            foreach (RaycastHit hit in hits)
            {
                Collider candidate = hit.collider;
                if (BelongsToPlayer(candidate))
                {
                    continue;
                }

                EnemyAI enemy = candidate.GetComponentInParent<EnemyAI>();
                if (enemy == null)
                {
                    if (hit.distance > 0f && hit.distance < obstacleDistance)
                    {
                        obstacleDistance = hit.distance;
                        nearestObstacle = candidate;
                    }
                    continue;
                }
                if (!enemy.isActiveAndEnabled || enemy.IsDead)
                {
                    continue;
                }

                Vector3 offset = candidate.bounds.center - transform.position;
                if (Vector3.Dot(offset.normalized, transform.forward) < 0.25f ||
                    !HasClearStrike(candidate, enemy))
                {
                    continue;
                }
                if (offset.sqrMagnitude < enemyDistance)
                {
                    enemyDistance = offset.sqrMagnitude;
                    nearestEnemy = candidate;
                }
            }

            return nearestEnemy != null ? nearestEnemy : nearestObstacle;
        }

        private bool HasClearStrike(Collider target, EnemyAI enemy)
        {
            Bounds bounds = target.bounds;
            Vector3 point = bounds.center;
            point.y = Mathf.Clamp(transform.position.y, bounds.min.y + 0.1f, bounds.max.y - 0.1f);
            Vector3 direction = point - transform.position;
            RaycastHit[] hits = Physics.RaycastAll(transform.position, direction.normalized,
                direction.magnitude, ~0, QueryTriggerInteraction.Ignore);
            Collider nearest = null;
            float distance = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (!BelongsToPlayer(hit.collider) && hit.distance < distance)
                {
                    nearest = hit.collider;
                    distance = hit.distance;
                }
            }
            return nearest == null || nearest.GetComponentInParent<EnemyAI>() == enemy;
        }

        private bool BelongsToPlayer(Collider candidate)
        {
            Transform root = playerRoot != null ? playerRoot : transform.root;
            return candidate.transform == root || candidate.transform.IsChildOf(root);
        }

        private void ResetSwing()
        {
            if (swingCoroutine != null)
            {
                StopCoroutine(swingCoroutine);
                swingCoroutine = null;
            }
            if (weaponVisual != null)
            {
                weaponVisual.localRotation = restRotation;
            }
        }

        private IEnumerator SwingWeapon()
        {
            if (weaponVisual == null)
            {
                yield break;
            }

            Quaternion swungRotation = restRotation * Quaternion.Euler(-60f, 0f, 0f);
            float outward = Mathf.Max(0.01f, swingDuration);
            float inward = Mathf.Max(0.01f, returnDuration);
            for (float elapsed = 0f; elapsed < outward; elapsed += Time.deltaTime)
            {
                weaponVisual.localRotation = Quaternion.Slerp(restRotation, swungRotation, elapsed / outward);
                yield return null;
            }
            for (float elapsed = 0f; elapsed < inward; elapsed += Time.deltaTime)
            {
                weaponVisual.localRotation = Quaternion.Slerp(swungRotation, restRotation, elapsed / inward);
                yield return null;
            }
            weaponVisual.localRotation = restRotation;
            swingCoroutine = null;
        }
    }
}
