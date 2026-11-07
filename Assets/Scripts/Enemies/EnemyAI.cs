using SheltreSim.Player;
using SheltreSim.Survival;
using UnityEngine;

namespace SheltreSim.Enemies
{
    /// <summary>
    /// Simple raider/mutant AI: detect -> chase -> attack. No pathfinding (flat open
    /// outdoor area, direct MoveTowards is enough) and no NavMesh dependency.
    ///
    /// Playtest feedback reversed the original "no combat" scope decision - the player can
    /// now fight back via SheltreSim.Combat.MeleeAttacker, which calls TakeDamage below.
    /// </summary>
    public class EnemyAI : MonoBehaviour
    {
        public float maxHealth = 30f;
        public float health;
        private bool isDead;

        public float detectionRadius = 10f;

        /// <summary>
        /// Must be strictly larger than detectionRadius. This hysteresis gap stops the
        /// enemy flickering between Chasing and Idle when the player sits right at the
        /// detection boundary.
        /// </summary>
        public float loseInterestRadius = 14f;

        public float attackRange = 1.5f;
        public float moveSpeed = 2.5f;
        public float attackDamage = 5f;
        public float attackCooldown = 1.5f;

        private enum State
        {
            Idle,
            Chasing,
            Attacking
        }

        private State state = State.Idle;
        private Transform playerTransform;
        private SurvivalStats playerStats;
        private float lastAttackTime = float.NegativeInfinity;

        private void Start()
        {
            // Must happen BEFORE the early return below. 'health' is a serialized public field
            // that the M6 editor tool saves into the scene as 0 (Start doesn't run at edit
            // time), so if the player lookup fails and this is skipped, the enemy sits at 0 HP
            // and dies to the first swing that touches it.
            health = maxHealth;

            FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>();
            if (player == null)
            {
                Debug.LogError($"{nameof(EnemyAI)} on '{name}' found no FirstPersonController in the scene - nothing to chase.");
                return;
            }

            playerTransform = player.transform;
            playerStats = player.GetComponent<SurvivalStats>();
        }

        public void TakeDamage(float amount)
        {
            if (isDead)
            {
                return;
            }

            health = Mathf.Clamp(health - amount, 0f, maxHealth);

            if (health <= 0f)
            {
                isDead = true;
                Debug.Log($"{name} was killed.");
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                return;
            }

            float distance = Vector3.Distance(transform.position, playerTransform.position);

            switch (state)
            {
                case State.Idle:
                    if (distance <= detectionRadius)
                    {
                        state = State.Chasing;
                    }
                    break;

                case State.Chasing:
                    if (distance > loseInterestRadius)
                    {
                        state = State.Idle;
                        break;
                    }

                    if (distance <= attackRange)
                    {
                        state = State.Attacking;
                        break;
                    }

                    MoveTowardPlayer();
                    break;

                case State.Attacking:
                    if (distance > attackRange)
                    {
                        state = State.Chasing;
                        break;
                    }

                    FacePlayer();
                    TryAttack();
                    break;
            }
        }

        private void MoveTowardPlayer()
        {
            Vector3 target = new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z);
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            FacePlayer();
        }

        private void FacePlayer()
        {
            Vector3 direction = playerTransform.position - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        private void TryAttack()
        {
            if (playerStats != null && playerStats.IsDead)
            {
                return;
            }

            if (Time.time - lastAttackTime < attackCooldown)
            {
                return;
            }

            lastAttackTime = Time.time;

            if (playerStats != null)
            {
                playerStats.health = Mathf.Clamp(playerStats.health - attackDamage, 0f, playerStats.maxHealth);
                Debug.Log($"{name} hit the player for {attackDamage} damage (health now {playerStats.health}).");
            }
        }
    }
}
