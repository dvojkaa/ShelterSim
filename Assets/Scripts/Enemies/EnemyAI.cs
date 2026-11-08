using System.Collections;
using SheltreSim.Interaction;
using SheltreSim.Player;
using SheltreSim.Survival;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Enemies
{
    /// <summary>
    /// Simple sight-based chase and melee AI. Local obstacle probes and doorway approach
    /// points help with small props and the bunker entrance; this is not a navigation mesh.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyAI : MonoBehaviour
    {
        public float maxHealth = 30f;
        public float health;
        public float detectionRadius = 10f;
        public float loseInterestRadius = 50f;
        public float attackRange = 1.5f;
        public float moveSpeed = 2.5f;
        public float attackDamage = 5f;
        public float attackCooldown = 1.5f;
        public float attackWindup = 0.35f;
        public float doorBreakRadius = 1.8f;
        public float gravity = -18f;

        public bool IsDead { get; private set; }
        public bool IsChasing => chasing;
        public event System.Action<EnemyAI> OnDeath;

        private bool initialized;
        private CharacterController controller;
        private Transform playerTransform;
        private CharacterController playerController;
        private SurvivalStats playerStats;
        private InteractableDoor[] doors;
        private bool chasing;
        private float verticalSpeed;
        private float nextAttackTime;
        private bool attackPending;
        private float attackStartedAt;
        private float nextDoorCheckTime;
        private Vector3 steeringDirection;
        private float nextSteerTime;
        private InteractableDoor navigationDoor;
        private float doorEntrySide;
        private bool doorApproached;
        private Transform weaponVisual;
        private Quaternion weaponRestRotation;
        private Renderer[] visualRenderers;
        private MaterialPropertyBlock[] originalProperties;
        private MaterialPropertyBlock flashProperties;
        private Coroutine flashCoroutine;

        private static readonly float[] SteeringAngles = { 0f, 35f, -35f, 70f, -70f, 100f, -100f };
        private const float DoorApproachDistance = 1.5f;
        private const float SteeringInterval = 0.15f;
        private const float SteeringDistance = 1.25f;

        private Vector3 BodyCenter => transform.TransformPoint(controller.center);
        private Vector3 PlayerCenter => playerController != null
            ? playerController.transform.TransformPoint(playerController.center)
            : playerTransform.position + Vector3.up * 0.9f;

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            controller = GetComponent<CharacterController>();
            // Keep displacement independent of frame rate. At high FPS the default
            // 1 mm cutoff silently discards otherwise valid walking and gravity steps.
            controller.minMoveDistance = 0f;
            health = Mathf.Max(1f, maxHealth);
            nextAttackTime = Time.time + 0.5f;
            flashProperties = new MaterialPropertyBlock();
        }

        private void Start()
        {
            Initialize();
            FindPlayer();
            doors = Object.FindObjectsByType<InteractableDoor>(FindObjectsSortMode.None);
            CacheVisuals();
        }

        private void FindPlayer()
        {
            FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (player == null)
            {
                return;
            }
            playerTransform = player.transform;
            playerController = player.GetComponent<CharacterController>();
            playerStats = player.GetComponent<SurvivalStats>();
        }

        private void CacheVisuals()
        {
            if (visualRenderers != null)
            {
                return;
            }
            visualRenderers = GetComponentsInChildren<Renderer>();
            originalProperties = new MaterialPropertyBlock[visualRenderers.Length];
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                originalProperties[i] = new MaterialPropertyBlock();
                visualRenderers[i].GetPropertyBlock(originalProperties[i]);
            }
            weaponVisual = transform.Find("WeaponProp");
            if (weaponVisual != null)
            {
                weaponRestRotation = weaponVisual.localRotation;
            }
        }

        public void TakeDamage(float amount)
        {
            Initialize();
            if (IsDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
            {
                return;
            }
            health = Mathf.Clamp(health - amount, 0f, Mathf.Max(1f, maxHealth));
            chasing = true;
            CancelAttack();
            if (health <= 0f)
            {
                IsDead = true;
                controller.enabled = false;
                OnDeath?.Invoke(this);
                Destroy(gameObject);
                return;
            }

            CacheVisuals();
            ResetDamageFlash();
            flashCoroutine = StartCoroutine(FlashDamage());
        }

        /// <summary>Loads an existing living raider without firing death or respawn events.</summary>
        public void RestoreState(Vector3 position, Quaternion rotation, float restoredHealth)
        {
            Initialize();
            ResetDamageFlash();
            CancelAttack();
            bool controllerWasEnabled = controller.enabled;
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            health = Mathf.Clamp(restoredHealth, 0.01f, Mathf.Max(1f, maxHealth));
            IsDead = false;
            controller.enabled = controllerWasEnabled;
            verticalSpeed = 0f;
            chasing = false;
            navigationDoor = null;
            steeringDirection = Vector3.zero;
            nextSteerTime = 0f;
            nextAttackTime = Time.time + 0.5f;
        }

        private IEnumerator FlashDamage()
        {
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                Renderer visual = visualRenderers[i];
                if (visual == null) continue;
                visual.GetPropertyBlock(flashProperties);
                flashProperties.SetColor("_Color", Color.white);
                flashProperties.SetColor("_BaseColor", Color.white);
                // Textured models are already white-tinted, so the flash has to come from emission (SS_Raider material).
                flashProperties.SetColor("_EmissionColor", Color.white * 0.75f);
                visual.SetPropertyBlock(flashProperties);
            }
            yield return new WaitForSeconds(0.15f);
            RestoreRendererProperties();
            flashCoroutine = null;
        }

        private void RestoreRendererProperties()
        {
            if (visualRenderers == null) return;
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                if (visualRenderers[i] != null)
                {
                    visualRenderers[i].SetPropertyBlock(originalProperties[i]);
                }
            }
        }

        private void ResetDamageFlash()
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                flashCoroutine = null;
                RestoreRendererProperties();
            }
        }

        private void OnDisable()
        {
            ResetDamageFlash();
            CancelAttack();
        }

        private void Update()
        {
            if (IsDead || Time.timeScale <= 0f || !controller.enabled)
            {
                return;
            }
            if (playerTransform == null || (playerStats != null && (playerStats.IsDead || playerStats.health <= 0f)))
            {
                CancelAttack();
                Move(Vector3.zero);
                return;
            }

            // A distant actor must not keep falling or walking after its streamed terrain
            // has been released. Resume simulation when that region becomes resident again.
            if (SheltreSim.World.ProceduralWorld.Active != null &&
                !SheltreSim.World.ProceduralWorld.Active.HasGroundAt(transform.position))
            {
                CancelAttack();
                verticalSpeed = 0f;
                return;
            }
            Vector3 offset = PlayerCenter - BodyCenter;
            float distance = offset.magnitude;
            bool visible = HasClearLine(BodyCenter + Vector3.up * 0.25f,
                PlayerCenter + Vector3.up * 0.25f, playerTransform);
            if (!chasing && distance <= detectionRadius && visible)
            {
                chasing = true;
            }
            if (chasing && distance > Mathf.Max(detectionRadius, loseInterestRadius))
            {
                chasing = false;
                navigationDoor = null;
            }

            Vector3 motion = Vector3.zero;
            if (chasing)
            {
                TryBreakNearbyDoors();
                if (distance <= attackRange && visible)
                {
                    FaceDirection(offset);
                    TryAttack();
                }
                else
                {
                    CancelAttack();
                    Vector3 destination = GetChaseDestination(visible);
                    Vector3 direction = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up);
                    if (Time.time >= nextSteerTime)
                    {
                        nextSteerTime = Time.time + SteeringInterval;
                        steeringDirection = ChooseDirection(direction.normalized);
                    }
                    motion = steeringDirection * Mathf.Max(0f, moveSpeed);
                    // Avoid overshooting a doorway approach point on a long frame.
                    motion = Vector3.ClampMagnitude(motion, direction.magnitude / Mathf.Max(Time.deltaTime, 0.001f));
                    FaceDirection(motion);
                }
            }
            else
            {
                CancelAttack();
            }

            Move(motion);
            if (!attackPending && weaponVisual != null)
            {
                weaponVisual.localRotation = Quaternion.Slerp(weaponVisual.localRotation,
                    weaponRestRotation, Mathf.Clamp01(Time.deltaTime * 14f));
            }
        }

        private void Move(Vector3 horizontal)
        {
            if (controller.isGrounded && verticalSpeed < 0f)
            {
                verticalSpeed = -2f;
            }
            verticalSpeed += gravity * Time.deltaTime;
            CollisionFlags flags = controller.Move((horizontal + Vector3.up * verticalSpeed) * Time.deltaTime);
            if ((flags & CollisionFlags.Below) != 0 && verticalSpeed < 0f)
            {
                verticalSpeed = -2f;
            }
        }

        private void TryAttack()
        {
            if (playerStats == null) return;
            if (!attackPending)
            {
                if (Time.time < nextAttackTime) return;
                attackPending = true;
                attackStartedAt = Time.time;
            }

            float progress = (Time.time - attackStartedAt) / Mathf.Max(0.05f, attackWindup);
            if (weaponVisual != null)
            {
                weaponVisual.localRotation = weaponRestRotation * Quaternion.Euler(0f, 0f, -65f * Mathf.Clamp01(progress));
            }
            if (progress < 1f) return;

            attackPending = false;
            nextAttackTime = Time.time + Mathf.Max(0.1f, attackCooldown);
            // Update has rechecked range, sight and life on every frame of the wind-up.
            float previousHealth = playerStats.health;
            playerStats.TakeDamage(attackDamage);
            float damageDealt = previousHealth - playerStats.health;
            if (damageDealt > 0f)
            {
                GameMessageBus.Post($"A raider hit you for {damageDealt:0} damage.");
            }
            if (weaponVisual != null)
            {
                weaponVisual.localRotation = weaponRestRotation * Quaternion.Euler(70f, 0f, 0f);
            }
        }

        private void CancelAttack()
        {
            if (attackPending)
            {
                nextAttackTime = Mathf.Max(nextAttackTime, Time.time + 0.2f);
            }
            attackPending = false;
            if (weaponVisual != null)
            {
                weaponVisual.localRotation = weaponRestRotation;
            }
        }

        private Vector3 GetChaseDestination(bool playerVisible)
        {
            if (navigationDoor != null)
            {
                float targetSide = Vector3.Dot(playerTransform.position - navigationDoor.PassageCenter, navigationDoor.PassageNormal);
                float ownSide = Vector3.Dot(transform.position - navigationDoor.PassageCenter, navigationDoor.PassageNormal);
                if (targetSide * doorEntrySide >= 0f || ownSide * doorEntrySide < -DoorApproachDistance * 0.75f)
                {
                    navigationDoor = null;
                }
            }

            if (navigationDoor == null && doors != null && doors.Length > 0)
            {
                // A door's plane extends across the whole map. Opposite sides of that
                // plane alone do not mean this doorway lies on our route: outdoors the
                // player may be reachable directly, well to the side of the bunker.
                Vector3 direct = Vector3.ProjectOnPlane(playerTransform.position - transform.position, Vector3.up);
                if (direct.sqrMagnitude < 0.001f ||
                    (playerVisible && ProbeClearance(direct.normalized, direct.magnitude) >= direct.magnitude))
                {
                    return playerTransform.position;
                }
                float nearest = float.PositiveInfinity;
                foreach (InteractableDoor door in doors)
                {
                    if (door == null || !door.isActiveAndEnabled) continue;
                    Vector3 from = transform.position - door.PassageCenter;
                    Vector3 to = playerTransform.position - door.PassageCenter;
                    float fromSide = Vector3.Dot(from, door.PassageNormal);
                    float toSide = Vector3.Dot(to, door.PassageNormal);
                    if (fromSide * toSide >= 0f || to.sqrMagnitude > 49f || from.sqrMagnitude > 196f || from.sqrMagnitude >= nearest)
                    {
                        continue;
                    }
                    nearest = from.sqrMagnitude;
                    navigationDoor = door;
                    doorEntrySide = Mathf.Sign(fromSide);
                    doorApproached = Mathf.Abs(fromSide) < 0.4f;
                    nextSteerTime = 0f;
                }
            }
            if (navigationDoor == null) return playerTransform.position;

            Vector3 approach = navigationDoor.PassageCenter + navigationDoor.PassageNormal * (doorEntrySide * DoorApproachDistance);
            if (!doorApproached && Vector3.ProjectOnPlane(approach - transform.position, Vector3.up).sqrMagnitude < 0.09f)
            {
                doorApproached = true;
                nextSteerTime = 0f;
            }
            return doorApproached
                ? navigationDoor.PassageCenter - navigationDoor.PassageNormal * (doorEntrySide * DoorApproachDistance)
                : approach;
        }

        private Vector3 ChooseDirection(Vector3 desired)
        {
            if (desired.sqrMagnitude < 0.001f) return Vector3.zero;
            Vector3 best = Vector3.zero;
            float bestScore = float.NegativeInfinity;
            foreach (float angle in SteeringAngles)
            {
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * desired;
                float clearance = ProbeClearance(direction);
                if (angle == 0f && clearance >= SteeringDistance) return desired;
                if (clearance < 0.15f) continue;
                float score = Vector3.Dot(direction, desired) + clearance / SteeringDistance * 2f
                    + Vector3.Dot(direction, steeringDirection) * 0.35f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = direction;
                }
            }
            return best;
        }

        private float ProbeClearance(Vector3 direction, float probeDistance = SteeringDistance)
        {
            Vector3 scale = transform.lossyScale;
            float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfHeight = Mathf.Max(radius, controller.height * Mathf.Abs(scale.y) * 0.5f);
            Vector3 center = BodyCenter;
            // Raise the lower end above the step height so walkable floor seams do not look
            // like walls. CharacterController.Move remains the authority on real collision.
            Vector3 bottom = center - Vector3.up * Mathf.Max(0f, halfHeight - radius - controller.stepOffset - 0.03f);
            Vector3 top = center + Vector3.up * Mathf.Max(0f, halfHeight - radius - 0.03f);
            RaycastHit[] hits = Physics.CapsuleCastAll(bottom, top, radius + 0.02f, direction,
                probeDistance, ~0, QueryTriggerInteraction.Ignore);
            float distance = probeDistance;
            foreach (RaycastHit hit in hits)
            {
                if (IsSelf(hit.collider) || BelongsTo(hit.collider, playerTransform)) continue;
                if (hit.distance <= 0f)
                {
                    Vector3 towardObstacle = hit.collider.bounds.ClosestPoint(center) - center;
                    if (Vector3.Dot(direction, towardObstacle) <= 0.01f) continue;
                }
                distance = Mathf.Min(distance, hit.distance);
            }
            return distance;
        }

        private void TryBreakNearbyDoors()
        {
            if (doors == null || Time.time < nextDoorCheckTime) return;
            nextDoorCheckTime = Time.time + 0.4f;
            foreach (InteractableDoor door in doors)
            {
                if (door == null || !door.isActiveAndEnabled || door.IsOpen) continue;
                if (Vector3.Distance(BodyCenter, door.PassageCenter) <= doorBreakRadius &&
                    HasClearLine(BodyCenter, door.PassageCenter, door.transform))
                {
                    door.ForceOpen();
                    nextSteerTime = 0f;
                }
            }
        }

        private bool HasClearLine(Vector3 origin, Vector3 destination, Transform target)
        {
            Vector3 direction = destination - origin;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, direction.magnitude,
                ~0, QueryTriggerInteraction.Ignore);
            Collider nearest = null;
            float distance = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (!IsSelf(hit.collider) && hit.distance < distance)
                {
                    nearest = hit.collider;
                    distance = hit.distance;
                }
            }
            return nearest == null || BelongsTo(nearest, target);
        }

        private bool IsSelf(Collider candidate) => BelongsTo(candidate, transform);

        private static bool BelongsTo(Collider candidate, Transform owner) => owner != null &&
            (candidate.transform == owner || candidate.transform.IsChildOf(owner));

        private void FaceDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }
}
