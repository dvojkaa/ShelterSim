using SheltreSim.Interaction;
using SheltreSim.Player;
using SheltreSim.Survival;
using UnityEngine;

namespace SheltreSim.Enemies
{
    /// <summary>
    /// Simple raider/mutant AI: detect -> chase -> attack. No pathfinding/NavMesh — movement
    /// is still a straight-line "steer toward the player" direction, same as before — but it
    /// is now driven through a <see cref="CharacterController"/> instead of writing
    /// transform.position directly, so the straight line actually stops at walls, buildings,
    /// cars and furniture instead of clipping straight through them (playtester feedback:
    /// "give raiders collision so they actually bump into things"). A chasing enemy that
    /// walks into a closed door counts as "bumping into it" too, which is exactly the case
    /// <see cref="TryBreakNearbyDoors"/> exists to resolve — otherwise collision would have
    /// turned "hide behind a closed door" back into a real safe room, just for a different
    /// reason than before (a wall instead of giving up early).
    ///
    /// Playtest feedback reversed the original "no combat" scope decision - the player can
    /// now fight back via SheltreSim.Combat.MeleeAttacker, which calls TakeDamage below.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyAI : MonoBehaviour
    {
        public float maxHealth = 30f;
        public float health;
        private bool isDead;

        public float detectionRadius = 10f;

        /// <summary>
        /// Must be strictly larger than detectionRadius. Originally just a flicker-prevention
        /// hysteresis gap right at the detection boundary, but at the old value of 14m a
        /// raider gave up well before reaching the bunker if the player sprinted home — so
        /// "close the door and you're safe" was true by accident (raiders never got close
        /// enough to test whether anything stopped them at the doorway), not by design.
        /// Raised to comfortably exceed the distance from any raider spawn point to the
        /// bunker doorway (the farthest, Raider_4, is ~31m) so a raider that has noticed the
        /// player reliably commits to the chase all the way home instead of giving up
        /// partway. The player can still always outrun a raider by sprinting (7 m/s vs this
        /// moveSpeed's 2.5 m/s) — this only removes the "it happened to give up" safety net.
        /// </summary>
        public float loseInterestRadius = 50f;

        public float attackRange = 1.5f;
        public float moveSpeed = 2.5f;
        public float attackDamage = 5f;
        public float attackCooldown = 1.5f;

        /// <summary>How close the enemy must be to a closed door to force it open.</summary>
        public float doorBreakRadius = 1.8f;
        private const float DoorCheckInterval = 0.4f;
        private float lastDoorCheckTime = float.NegativeInfinity;

        // Mirrors FirstPersonController's gravity handling (same constant) so the enemy's
        // CharacterController behaves the same way on the same ground - grounded characters
        // need a small constant downward push or CharacterController.isGrounded flickers.
        public float gravity = -18f;
        private CharacterController controller;
        private Vector3 verticalVelocity;

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

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

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

                    TryBreakNearbyDoors();

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

                    TryBreakNearbyDoors();
                    FacePlayer();
                    TryAttack();
                    break;
            }
        }

        private void MoveTowardPlayer()
        {
            Vector3 target = new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z);
            Vector3 toTarget = target - transform.position;
            toTarget.y = 0f;
            Vector3 horizontalMove = toTarget.sqrMagnitude > 0.0001f
                ? Vector3.ClampMagnitude(toTarget.normalized * moveSpeed, moveSpeed)
                : Vector3.zero;

            if (controller.isGrounded)
            {
                // Small downward bias instead of 0 keeps isGrounded reporting true on flat
                // ground - same idiom as FirstPersonController.HandleMove.
                verticalVelocity.y = -2f;
            }
            verticalVelocity.y += gravity * Time.deltaTime;

            controller.Move((horizontalMove + new Vector3(0f, verticalVelocity.y, 0f)) * Time.deltaTime);
            FacePlayer();
        }

        /// <summary>
        /// A chasing/attacking enemy that is now collision-blocked by <see cref="controller"/>
        /// (see the class doc comment) would otherwise stop dead at a closed bunker door
        /// forever, since nothing ever opens it from the raider's side. Periodically checks
        /// for a nearby closed <see cref="InteractableDoor"/> and forces it open — this is
        /// the thing that actually makes "enemies can come in through the door" true once
        /// walls and doors are real obstacles instead of being walked through.
        ///
        /// The radius only has to beat the distance the controller stops at, which is roughly
        /// its own radius plus the door's half-thickness (~0.6m) — 1.8m is comfortably past
        /// that, so there is no "wedged against the door just outside the check radius" case.
        /// Opening the door is necessary but not sufficient: the gap behind it also has to be
        /// wide enough for this controller, which is why M12's QC pass widened both doorway
        /// constants (see M1SceneBuilder.DoorGap).
        /// </summary>
        private void TryBreakNearbyDoors()
        {
            if (Time.time - lastDoorCheckTime < DoorCheckInterval)
            {
                return;
            }
            lastDoorCheckTime = Time.time;

            Collider[] nearby = Physics.OverlapSphere(transform.position, doorBreakRadius);
            foreach (Collider col in nearby)
            {
                InteractableDoor door = col.GetComponentInParent<InteractableDoor>();
                if (door != null && !door.IsOpen)
                {
                    door.ForceOpen();
                }
            }
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
