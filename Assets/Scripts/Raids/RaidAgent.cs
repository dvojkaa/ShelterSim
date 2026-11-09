using System.Collections.Generic;
using SheltreSim.Audio;
using SheltreSim.Building;
using SheltreSim.Enemies;
using SheltreSim.Interaction;
using SheltreSim.Player;
using SheltreSim.Power;
using SheltreSim.Storage;
using SheltreSim.Survival;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Raids
{
    /// <summary>
    /// A raid enemy's decisions, driving EnemyAI as its brain. Walks RaidRoute, breaks what's in the way, goes for the player
    /// inside about 12 m with line of sight and returns to the route on losing them. State, route position and
    /// the last stuck reason are public so a probe log can say why an actor isn't moving.
    /// </summary>
    [RequireComponent(typeof(EnemyAI))]
    public sealed class RaidAgent : MonoBehaviour, IEnemyBrain
    {
        public const float EngageRange = 12f;
        public const float StuckStructureSeconds = 6f;
        public const float StuckStepSeconds = 20f;
        private const float ProbeDistance = 1.1f;
        private const float DefenceDiversion = 5f;
        private const float MeleeReach = 0.9f; // from the capsule's surface
        private const float SteerInterval = 0.12f;
        private const float WalkableNormalY = 0.6f; // about 53 degrees: the 30 degree ramp is floor, not an obstacle
        private static readonly float[] SteeringAngles = { 0f, 35f, -35f, 70f, -70f, 100f, -100f };

        public RaidVariant Variant { get; private set; }
        public RaidVariantSpec Spec { get; private set; }
        public int RaidNumber { get; private set; }
        public string State { get; private set; } = "new";
        public string LastStuckReason { get; private set; } = "";
        public int RouteIndex => routeIndex;
        public int RouteCount => route.Count;
        public float StuckSeconds => Time.time - lastProgressTime;
        public IReadOnlyDictionary<string, int> Stolen => stolen;
        /// <summary>Structures this actor struck, for probe logs.</summary>
        public int StructureHits { get; private set; }
        /// <summary>Standing at something and hitting it, as opposed to walking or queueing.</summary>
        public bool IsBreaking { get; private set; }

        private readonly Dictionary<string, int> stolen = new Dictionary<string, int>();
        private List<Vector3> route = new List<Vector3>();
        private bool routeOutward;
        private int routeIndex;
        private EnemyAI enemy;
        private CharacterController body;
        private Transform player;
        private CharacterController playerBody;
        private SurvivalStats playerStats;
        private Vector3 steering;
        private float nextSteer;
        private float engageUntil;
        private float nextSightCheck;
        private bool playerVisible;
        private bool wasEngaging = true;
        private float lastProgressTime;
        private float bestDistance = float.PositiveInfinity;
        private float nextStep;
        private float attackStarted;
        private Vector3 movementSample;
        private float nextMovementSample;
        private float nextDefenceCheck;
        private Collider attackCollider;
        private StructureHealth attackStructure;
        private InteractableDoor attackDoor;
        private bool windingUp;
        private float windupStarted;
        private float nextStrike;
        private float nextShot;
        private float nextSteal;
        private int stealCount;
        private System.Random stealRandom;
        private EnemyAI jamPartner;

        public void Setup(RaidVariant variant, int raidNumber, int seed)
        {
            enemy = GetComponent<EnemyAI>();
            body = enemy.Body;
            Variant = variant;
            RaidNumber = raidNumber;
            Spec = RaidVariantSpec.Of(variant);
            stealRandom = new System.Random(seed);
            enemy.SetBodySize(Spec.Scale, Spec.BodyRadius, Spec.BodyHeight);
            enemy.Configure(Spec.Health, Spec.Speed, Spec.Damage, Spec.AttackCooldown, Spec.AttackWindup, Spec.AttackRange);
            enemy.superArmor = Spec.SuperArmor;
            enemy.ApplyTint(Spec.Tint);
            enemy.SetBrain(this);
            lastProgressTime = Time.time;
            nextStrike = Time.time + 0.4f;
            nextShot = Time.time + 1f;
            FindPlayer();
            RebuildRoute();
        }

        public void AddStolen(string id, int amount)
        {
            if (string.IsNullOrEmpty(id) || amount <= 0) return;
            stolen.TryGetValue(id, out int have);
            stolen[id] = have + amount;
            stealCount = Mathf.Max(stealCount, 1);
        }

        private void FindPlayer()
        {
            FirstPersonController controller = Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (controller == null) return;
            player = controller.transform;
            playerBody = controller.GetComponent<CharacterController>();
            playerStats = controller.GetComponent<SurvivalStats>();
        }

        private Vector3 PlayerCenter => playerBody != null ? player.TransformPoint(playerBody.center) : player.position + Vector3.up * 0.9f;

        // ---------- Top level ----------

        public void TickBrain()
        {
            if (player == null) FindPlayer();
            if (player == null) return;
            IsBreaking = false;
            if (enemy.IsStunned)
            {
                State = "stunned";
                windingUp = false;
                enemy.BrainCancelAttack();
                enemy.BrainMove(Vector3.zero);
                return;
            }

            Vector3 toPlayer = PlayerCenter - enemy.CenterPosition;
            float distance = toPlayer.magnitude;
            if (Time.time >= nextSightCheck)
            {
                nextSightCheck = Time.time + 0.2f;
                playerVisible = distance <= 30f && RaidSight.Clear(enemy.CenterPosition + Vector3.up * 0.25f, PlayerCenter + Vector3.up * 0.25f, transform, player);
            }
            if (Variant == RaidVariant.Scavenger && TickStealing(distance)) return;

            float engageRange = Spec.Ranged ? Mathf.Max(EngageRange, Spec.RangedRange) : EngageRange;
            if (playerVisible && distance <= engageRange * PlayerStealth.DetectionFactor) engageUntil = Time.time + 1.5f;
            bool engaging = Time.time < engageUntil;
            if (engaging)
            {
                wasEngaging = true;
                TickEngage(toPlayer, distance);
                return;
            }
            if (wasEngaging)
            {
                wasEngaging = false;
                enemy.BrainCancelAttack();
                RebuildRoute();
            }

            if (attackCollider != null || attackStructure != null || attackDoor != null)
            {
                if (TickStructureAttack()) return;
            }
            if (Time.time >= nextDefenceCheck)
            {
                nextDefenceCheck = Time.time + 1f;
                if (FindDefenceNearby(out StructureHealth defence, out Collider defenceCollider))
                {
                    SetAttack(defenceCollider, defence, null);
                    if (TickStructureAttack()) return;
                }
            }
            TickRoute(distance);
        }

        /// <summary>Turrets within a few metres, in plain sight, are worth stopping for. Otherwise nothing ever shoots back at them.</summary>
        private bool FindDefenceNearby(out StructureHealth structure, out Collider collider)
        {
            structure = null; collider = null;
            float best = DefenceDiversion * DefenceDiversion;
            Vector3 center = enemy.CenterPosition;
            foreach (StructureHealth candidate in StructureHealth.All)
            {
                if (candidate == null || candidate.GetComponent<IRaidPriorityTarget>() == null) continue;
                float d = (candidate.transform.position - transform.position).sqrMagnitude;
                if (d >= best) continue;
                Collider c = candidate.GetComponentInChildren<Collider>();
                if (c == null || !RaidSight.Clear(center, c.bounds.center, transform, candidate.transform)) continue;
                best = d;
                structure = candidate;
                collider = c;
            }
            return structure != null;
        }

        // ---------- The player ----------

        private void TickEngage(Vector3 toPlayer, float distance)
        {
            ResetProgress();
            if (Spec.Ranged && playerVisible && distance <= Spec.RangedRange && distance > Spec.AttackRange + 0.5f)
            {
                State = "shooting player";
                enemy.BrainFace(toPlayer);
                enemy.BrainMove(Vector3.zero);
                if (Time.time >= nextShot)
                {
                    nextShot = Time.time + Spec.RangedInterval;
                    ShootPlayer();
                }
                return;
            }
            if (distance <= enemy.attackRange && playerVisible)
            {
                State = "hitting player";
                enemy.BrainFace(toPlayer);
                enemy.BrainMeleePlayer();
                enemy.BrainMove(Vector3.zero);
                return;
            }
            enemy.BrainCancelAttack();
            State = "chasing player";
            if (attackCollider != null && TickStructureAttack()) return;
            Walk(player.position, 0f);
        }

        private Vector3 Muzzle => enemy.CenterPosition + Vector3.up * 0.45f + transform.forward * 0.45f;

        private void ShootPlayer()
        {
            GameAudio.PlayAt(Sfx.RaiderGunshot, Muzzle, 1f, 60f, 0.08f);
            RaidTracer.Show(Muzzle, PlayerCenter, new Color(1f, 0.82f, 0.45f), 0.05f);
            if (playerStats == null) return;
            float before = playerStats.health;
            playerStats.TakeDamage(Spec.RangedDamage);
            if (before - playerStats.health > 0f)
            {
                DamageDirection.Report(transform.position);
                GameMessageBus.Post($"A gunner shot you for {before - playerStats.health:0} damage.");
            }
        }

        // ---------- Route ----------

        private void RebuildRoute()
        {
            route = routeOutward ? RaidRoute.Outward(transform.position) : RaidRoute.Inward(transform.position);
            routeIndex = 0;
            ResetProgress();
        }

        private void ResetProgress()
        {
            lastProgressTime = Time.time;
            bestDistance = float.PositiveInfinity;
        }

        private void TickRoute(float playerDistance)
        {
            if (routeIndex >= route.Count)
            {
                TickFinalTarget();
                return;
            }
            Vector3 point = route[routeIndex];
            Vector3 from = routeIndex > 0 ? route[routeIndex - 1] : transform.position;
            Vector3 flat = Flat(point - transform.position);
            Vector3 along = Flat(point - from);
            // Passing the point counts: in a queue in the shaft nobody can stand exactly on it.
            bool passed = routeIndex > 0 && along.sqrMagnitude > 0.01f && Vector3.Dot(flat, along) <= 0f;
            if (flat.magnitude < 0.8f || passed)
            {
                routeIndex++;
                ResetProgress();
                return;
            }
            State = $"route {routeIndex + 1}/{route.Count}{(routeOutward ? " out" : "")}";
            if (Spec.Ranged && TickShootObstacle(flat)) return;
            TrackProgress(flat.magnitude);
            if (CheckStuck(point)) return;
            Walk(point, 0f);
        }

        /// <summary>The end of the route: the nearer of the player and the machines in the room, otherwise back up after the player.</summary>
        private void TickFinalTarget()
        {
            Vector3 position = transform.position;
            bool playerUnderground = RaidRoute.Underground(player.position);
            if (RaidRoute.Underground(position))
            {
                StructureHealth machine = NearestMachine(position);
                float machineDistance = machine != null ? Vector3.Distance(position, machine.transform.position) : float.PositiveInfinity;
                float playerDistance = playerUnderground ? Vector3.Distance(position, player.position) : float.PositiveInfinity;
                if (machine != null && machineDistance < playerDistance)
                {
                    State = "going for machine";
                    SetAttack(machine.GetComponentInChildren<Collider>(), machine, null);
                    if (attackCollider != null && TickStructureAttack()) return;
                }
                if (playerUnderground)
                {
                    State = "going for player";
                    TrackMovement();
                    TrackProgress(playerDistance);
                    if (CheckStuck(player.position)) return;
                    Walk(player.position, 0f);
                    return;
                }
                // The room is empty and the player is outside: back up the stairs.
                routeOutward = true;
                RebuildRoute();
                if (route.Count > 0) return;
            }
            if (playerUnderground)
            {
                routeOutward = false;
                RebuildRoute();
                if (route.Count > 0) return;
            }
            State = "hunting on surface";
            TrackMovement();
            TrackProgress(Flat(player.position - position).magnitude);
            if (CheckStuck(player.position)) return;
            Walk(player.position, 0f);
        }

        private static StructureHealth NearestMachine(Vector3 position)
        {
            StructureHealth best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (StructureHealth structure in StructureHealth.All)
            {
                if (structure == null || !RaidRoute.Underground(structure.transform.position)) continue;
                if (!IsMachine(structure)) continue;
                float d = (structure.transform.position - position).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = structure; }
            }
            return best;
        }

        private static bool IsMachine(StructureHealth structure)
        {
            return structure.GetComponent<IPowerConsumer>() != null || structure.GetComponent<IPowerSource>() != null || structure.GetComponent<IPowerStorage>() != null;
        }

        // ---------- Walking ----------

        private void Walk(Vector3 destination, float stopDistance)
        {
            Vector3 flat = Flat(destination - transform.position);
            if (flat.magnitude <= stopDistance)
            {
                enemy.BrainMove(Vector3.zero);
                return;
            }
            if (Time.time >= nextSteer)
            {
                nextSteer = Time.time + SteerInterval;
                steering = Choose(flat.normalized);
                if (attackCollider != null)
                {
                    // A blocker was found: stop and hit it next frame instead of sliding along it.
                    enemy.BrainMove(Vector3.zero);
                    return;
                }
            }
            Vector3 motion = steering * Spec.Speed;
            motion = Vector3.ClampMagnitude(motion, flat.magnitude / Mathf.Max(Time.deltaTime, 0.001f));
            enemy.BrainFace(motion);
            enemy.BrainMove(motion);
        }

        /// <summary>Picks a direction. If what's straight ahead is something the player built or a closed door, marks it for attack.</summary>
        private Vector3 Choose(Vector3 desired)
        {
            Probe(desired, ProbeDistance, out _, out Collider blocker);
            if (blocker == null) return desired;

            StructureHealth structure = blocker.GetComponentInParent<StructureHealth>();
            InteractableDoor door = blocker.GetComponentInParent<InteractableDoor>();
            if (door != null && !door.IsOpen)
            {
                SetAttack(blocker, null, door);
                return Vector3.zero;
            }
            // A scavenger's crate is what it came for. It steals from it and doesn't break it.
            if (Variant == RaidVariant.Scavenger && blocker.GetComponentInParent<IItemStore>() != null) return desired;
            if (structure != null)
            {
                SetAttack(blocker, structure, null);
                return Vector3.zero;
            }
            EnemyAI other = blocker.GetComponentInParent<EnemyAI>();
            if (other != null)
            {
                // Queueing behind a comrade who is hitting something is progress. Otherwise, if the queue hasn't moved
                // for a while, let the two pass through each other: capsules jammed in a 2.8 m shaft must not stop a raid.
                RaidAgent ahead = other.GetComponent<RaidAgent>();
                if (ahead != null && ahead.IsBreaking) ResetProgress();
                else if (StuckSeconds > 4f && other != jamPartner)
                {
                    jamPartner = other;
                    Physics.IgnoreCollision(body, other.Body, true);
                    LastStuckReason = "jam: passing " + other.name;
                }
                return desired;
            }

            Vector3 best = Vector3.zero;
            float bestScore = float.NegativeInfinity;
            foreach (float angle in SteeringAngles)
            {
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * desired;
                Probe(direction, ProbeDistance, out float clearance, out _);
                if (clearance < 0.15f) continue;
                float score = Vector3.Dot(direction, desired) + clearance / ProbeDistance * 2f + Vector3.Dot(direction, steering) * 0.35f;
                if (score > bestScore) { bestScore = score; best = direction; }
            }
            return best;
        }

        /// <summary>
        /// Nearest thing the capsule would run into. The lower end is raised above the step height and walkable surfaces don't count,
        /// so ramps, floor seams, trap plates and loot bags aren't obstacles. CharacterController.Move stays the authority.
        /// </summary>
        private void Probe(Vector3 direction, float distance, out float clearance, out Collider nearest)
        {
            float radius = body.radius * Mathf.Abs(transform.lossyScale.x);
            float half = Mathf.Max(radius, body.height * Mathf.Abs(transform.lossyScale.y) * 0.5f);
            Vector3 center = enemy.CenterPosition;
            Vector3 bottom = center - Vector3.up * Mathf.Max(0f, half - radius - body.stepOffset - 0.03f);
            Vector3 top = center + Vector3.up * Mathf.Max(0f, half - radius - 0.03f);
            clearance = distance;
            nearest = null;
            foreach (RaycastHit hit in Physics.CapsuleCastAll(bottom, top, radius + 0.02f, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform t = hit.collider.transform;
                if (t == transform || t.IsChildOf(transform)) continue;
                if (player != null && (t == player || t.IsChildOf(player))) continue;
                if (hit.collider.GetComponentInParent<LootBag>() != null) continue;
                if (hit.distance <= 0f)
                {
                    Vector3 towards = hit.collider.bounds.ClosestPoint(center) - center;
                    if (Vector3.Dot(direction, towards) <= 0.01f) continue;
                }
                else if (hit.normal.y > WalkableNormalY) continue;
                if (hit.distance < clearance)
                {
                    clearance = hit.distance;
                    nearest = hit.collider;
                }
            }
        }

        // ---------- Breaking things ----------

        private void SetAttack(Collider collider, StructureHealth structure, InteractableDoor door)
        {
            attackCollider = collider;
            attackStructure = structure;
            attackDoor = door;
            attackStarted = Time.time;
            ResetProgress();
        }

        private void ClearAttack()
        {
            attackCollider = null;
            attackStructure = null;
            attackDoor = null;
            windingUp = false;
            nextSteer = 0f;
            ResetProgress();
        }

        private bool TargetGone()
        {
            if (attackDoor != null) return attackDoor.IsOpen || attackDoor.IsBroken;
            return attackStructure == null || attackCollider == null || !attackCollider.gameObject.activeInHierarchy;
        }

        /// <summary>Returns true while it is occupying this actor's frame.</summary>
        private bool TickStructureAttack()
        {
            if (TargetGone())
            {
                ClearAttack();
                return false;
            }
            Vector3 center = enemy.CenterPosition;
            Vector3 closest = attackCollider.bounds.ClosestPoint(center);
            // A rotated collider's bounds are loose, so the capsule radius is measured on the ground plane only.
            float reach = Flat(closest - center).magnitude - body.radius * Mathf.Abs(transform.lossyScale.x);
            if (reach > MeleeReach)
            {
                State = attackDoor != null ? "walking to door" : "walking to " + attackCollider.name;
                TrackProgress(Flat(closest - center).magnitude);
                if (Time.time - attackStarted > StuckStructureSeconds)
                {
                    // Can't get to it. Forget it, the stuck logic will decide what's next.
                    LastStuckReason = "couldn't reach " + attackCollider.name;
                    ClearAttack();
                    return false;
                }
                Vector3 desired = Flat(closest - center).normalized;
                enemy.BrainFace(desired);
                enemy.BrainMove(desired * Spec.Speed);
                return true;
            }

            State = attackDoor != null ? $"breaking door ({attackDoor.RaidHealth:0})" : $"breaking {attackCollider.name} ({attackStructure.Health:0})";
            IsBreaking = true;
            ResetProgress();
            enemy.BrainFace(Flat(closest - center));
            enemy.BrainMove(Vector3.zero);
            if (Spec.Ranged && Time.time >= nextShot)
            {
                nextShot = Time.time + Spec.RangedInterval;
                ShootStructure(closest);
                return true;
            }
            if (Time.time < nextStrike) return true;
            if (!windingUp)
            {
                windingUp = true;
                windupStarted = Time.time;
            }
            float progress = (Time.time - windupStarted) / Mathf.Max(0.05f, Spec.AttackWindup);
            enemy.BrainPoseWeapon(progress);
            if (progress < 1f) return true;
            windingUp = false;
            nextStrike = Time.time + Spec.AttackCooldown;
            enemy.BrainPoseWeapon(-1f);
            Strike(Spec.StructureDamage);
            return true;
        }

        private void Strike(float damage)
        {
            StructureHits++;
            if (attackDoor != null) attackDoor.TakeRaidDamage(damage);
            else if (attackStructure != null) attackStructure.TakeDamage(damage);
        }

        /// <summary>A gunner shoots out a player-built block on its route from range, if it can see it.</summary>
        private bool TickShootObstacle(Vector3 flat)
        {
            Vector3 origin = enemy.CenterPosition + Vector3.up * 0.45f;
            if (!Physics.Raycast(origin, flat.normalized, out RaycastHit hit, Spec.RangedRange, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<EnemyAI>() != null) return false;
            InteractableDoor door = hit.collider.GetComponentInParent<InteractableDoor>();
            StructureHealth structure = hit.collider.GetComponentInParent<StructureHealth>();
            if (door != null && door.IsOpen) door = null;
            if (door == null && structure == null) return false;
            attackCollider = hit.collider;
            attackDoor = door;
            attackStructure = door == null ? structure : null;
            State = "shooting obstacle";
            ResetProgress();
            enemy.BrainFace(flat);
            enemy.BrainMove(Vector3.zero);
            if (Time.time >= nextShot)
            {
                nextShot = Time.time + Spec.RangedInterval;
                ShootStructure(hit.point);
            }
            // The next frame re-evaluates the ray from here, and stays at range. Melee only happens when it arrives anyway.
            attackCollider = null;
            attackDoor = null;
            attackStructure = null;
            return true;
        }

        private void ShootStructure(Vector3 point)
        {
            GameAudio.PlayAt(Sfx.RaiderGunshot, Muzzle, 1f, 60f, 0.08f);
            RaidTracer.Show(Muzzle, point, new Color(1f, 0.82f, 0.45f), 0.05f);
            StructureHits++;
            InteractableDoor door = attackDoor;
            StructureHealth structure = attackStructure;
            if (door != null) door.TakeRaidDamage(Spec.RangedDamage);
            else if (structure != null) structure.TakeDamage(Spec.RangedDamage);
        }

        // ---------- Being stuck ----------

        private void TrackProgress(float distanceToGoal)
        {
            if (distanceToGoal < bestDistance - 0.25f)
            {
                bestDistance = distanceToGoal;
                lastProgressTime = Time.time;
            }
        }

        /// <summary>A moving goal can walk away faster than the actor closes in. Really covering ground isn't being stuck.</summary>
        private void TrackMovement()
        {
            if (Time.time < nextMovementSample) return;
            nextMovementSample = Time.time + 2f;
            if (Flat(transform.position - movementSample).sqrMagnitude >= 4f) ResetProgress();
            movementSample = transform.position;
        }

        /// <summary>Six seconds without getting closer: hit the nearest structure. Twenty: step past or skip the point.</summary>
        private bool CheckStuck(Vector3 goal)
        {
            float stuck = StuckSeconds;
            if (stuck < StuckStructureSeconds) return false;
            if (attackCollider == null && stuck < StuckStepSeconds)
            {
                // Once per second: a physics overlap is not free with a dozen actors.
                if (Time.time < nextStep) return false;
                nextStep = Time.time + 1f;
                if (FindNearbyTarget(out Collider collider, out StructureHealth structure, out InteractableDoor door))
                {
                    LastStuckReason = $"no progress for {stuck:0} s, attacking {collider.name}";
                    float keepStuck = lastProgressTime;
                    SetAttack(collider, structure, door);
                    lastProgressTime = keepStuck;
                    return TickStructureAttack();
                }
                LastStuckReason = $"no progress for {stuck:0} s, nothing to break near {transform.position}";
                return false;
            }
            if (stuck >= StuckStepSeconds)
            {
                StepForward(goal);
                ResetProgress();
                return true;
            }
            return false;
        }

        private bool FindNearbyTarget(out Collider collider, out StructureHealth structure, out InteractableDoor door)
        {
            collider = null; structure = null; door = null;
            float best = 36f;
            Vector3 position = transform.position;
            foreach (StructureHealth candidate in StructureHealth.All)
            {
                if (candidate == null || candidate.GetComponent<IRaidIgnored>() != null) continue;
                Collider c = candidate.GetComponentInChildren<Collider>();
                if (c == null || c.isTrigger) continue;
                float d = (c.bounds.ClosestPoint(position) - position).sqrMagnitude;
                if (d < best) { best = d; collider = c; structure = candidate; door = null; }
            }
            foreach (InteractableDoor candidate in Object.FindObjectsByType<InteractableDoor>(FindObjectsSortMode.None))
            {
                if (candidate.IsOpen) continue;
                Collider c = candidate.GetComponent<Collider>();
                if (c == null) continue;
                float d = (c.bounds.ClosestPoint(position) - position).sqrMagnitude;
                if (d < best) { best = d; collider = c; door = candidate; structure = null; }
            }
            return collider != null;
        }

        /// <summary>
        /// Nothing to break and no way on. Above ground it steps over the obstacle if the far side is free and nothing the
        /// player built is in between (a fence, a wreck); that can't bypass a defence, or a bunker wall. Otherwise it gives up on this route point.
        /// </summary>
        private void StepForward(Vector3 goal)
        {
            Vector3 position = transform.position;
            Vector3 direction = Flat(goal - position).normalized;
            if (!RaidRoute.Underground(position) && direction.sqrMagnitude > 0.1f && CanStep(position, direction, out Vector3 landing))
            {
                LastStuckReason = $"stepped from {position} to {landing}";
                body.enabled = false;
                transform.position = landing;
                body.enabled = true;
                return;
            }
            LastStuckReason = $"stuck at {position}, skipping route point {routeIndex + 1}/{route.Count}";
            if (routeIndex < route.Count) routeIndex++;
        }

        private bool CanStep(Vector3 from, Vector3 direction, out Vector3 landing)
        {
            const float step = 2.5f;
            landing = from + direction * step;
            float toFeet = from.y - enemy.FeetPosition.y;
            foreach (RaycastHit hit in Physics.RaycastAll(enemy.CenterPosition, direction, step, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<StructureHealth>() != null || hit.collider.GetComponentInParent<InteractableDoor>() != null) return false;
            if (!Physics.Raycast(landing + Vector3.up * 4f, Vector3.down, out RaycastHit ground, 10f, ~0, QueryTriggerInteraction.Ignore)) return false;
            landing.y = ground.point.y + toFeet + 0.05f;
            float radius = body.radius * Mathf.Abs(transform.lossyScale.x);
            Vector3 center = landing + (enemy.CenterPosition - from);
            float half = body.height * Mathf.Abs(transform.lossyScale.y) * 0.5f - radius;
            foreach (Collider overlap in Physics.OverlapCapsule(center - Vector3.up * half, center + Vector3.up * half, radius, ~0, QueryTriggerInteraction.Ignore))
                if (!overlap.transform.IsChildOf(transform)) return false;
            return true;
        }

        // ---------- Scavenger ----------

        /// <summary>Goes for crates first and only fights once it has something, or at arm's length.</summary>
        private bool TickStealing(float playerDistance)
        {
            if (stealCount >= 3 || playerDistance < 2.5f) return false;
            IItemStore adjacent = NearestWithItems(transform.position, 2.4f);
            if (adjacent != null)
            {
                State = "stealing";
                enemy.BrainMove(Vector3.zero);
                enemy.BrainFace(adjacent.StorePosition - transform.position);
                if (Time.time >= nextSteal)
                {
                    nextSteal = Time.time + 3f;
                    Steal(adjacent);
                }
                return true;
            }
            if (!RaidRoute.InRoom(transform.position)) return false; // inside, crates come before the player
            IItemStore target = NearestWithItems(transform.position, 40f);
            if (target == null) return false;
            State = "going for a crate";
            TrackProgress(Flat(target.StorePosition - transform.position).magnitude);
            if (CheckStuck(target.StorePosition)) return true;
            if (attackCollider != null && TickStructureAttack()) return true;
            Walk(target.StorePosition, 1.8f);
            return true;
        }

        private static IItemStore NearestWithItems(Vector3 position, float radius)
        {
            IItemStore best = null;
            float bestDistance = radius * radius;
            foreach (IItemStore store in StorageRegistry.All)
            {
                float d = Flat(store.StorePosition - position).sqrMagnitude;
                if (d > bestDistance || store.Contents == null) continue;
                bool any = false;
                foreach (KeyValuePair<string, int> pair in store.Contents)
                    if (pair.Value > 0) { any = true; break; }
                if (!any) continue;
                bestDistance = d;
                best = store;
            }
            return best;
        }

        /// <summary>Takes a few items into its bag, from which they drop when it dies. Destroys nothing.</summary>
        private void Steal(IItemStore store)
        {
            var ids = new List<string>();
            foreach (KeyValuePair<string, int> pair in store.Contents)
                if (pair.Value > 0) ids.Add(pair.Key);
            ids.Sort(string.CompareOrdinal);
            int stacks = Mathf.Min(2, ids.Count);
            for (int i = 0; i < stacks; i++)
            {
                int pick = stealRandom.Next(ids.Count);
                string id = ids[pick];
                ids.RemoveAt(pick);
                int amount = Mathf.Min(store.GetCount(id), stealRandom.Next(1, 4));
                if (amount > 0 && store.TryTake(id, amount)) AddStolen(id, amount);
            }
            stealCount++;
            GameAudio.PlayAt(Sfx.CrateOpen, store.StorePosition, 0.9f, 25f);
            GameMessageBus.Post("A scavenger is emptying your crate! Kill it to get the items back.");
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
