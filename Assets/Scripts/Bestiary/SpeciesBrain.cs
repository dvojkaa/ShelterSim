using System.Collections.Generic;
using SheltreSim.Audio;
using SheltreSim.Enemies;
using SheltreSim.Player;
using SheltreSim.Raids;
using SheltreSim.Survival;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    public enum BrainState { Idle, Dormant, Returning, Chasing, Windup, Dash, Recover, BackOff, Emerge, Stunned, Breaking }

    /// <summary>
    /// One readable attack: a telegraph (pose + sound) of Windup seconds, then a strike that only lands if the player is still
    /// within Reach, inside the front arc and in line of sight, then a recovery window. A hit during the wind-up interrupts it
    /// unless the enemy has super armour (bosses).
    /// </summary>
    public sealed class Attack
    {
        public string Name = "swing";
        public float Windup = 0.6f, StartRange = 1.3f, Reach = 1.5f, HalfArc = 45f, DamageScale = 1f;
        public float RecoverHit = 1f, RecoverMiss = 1.4f;
        /// <summary>Degrees per second it may turn after the player during the wind-up. 0 locks the direction at the start.</summary>
        public float TurnRate = 180f;
        public PoseKind WindPose = PoseKind.ArmsUp, StrikePose = PoseKind.Slam, RecoverPose = PoseKind.Rest;
        public string WindSound = Sfx.RaiderGrowl, StrikeSound = Sfx.RaiderSwing;
        public float WindVolume = 0.8f;
        /// <summary>Positive speed turns this attack into a visible, dodgeable projectile released at the strike frame.</summary>
        public float ProjectileSpeed, ProjectileScale = 1f;
        public Color ProjectileColor = new Color(.55f, .82f, .28f);
    }

    /// <summary>
    /// The shared shape of every bestiary brain (GPT's design, DEV_COORDINATION.md "bestiář"): idle near its anchor, detect by
    /// sight within SpeciesSpec.Detection, chase with RaidAgent-style capsule steering, attack with a telegraph, strike and
    /// recovery, return to the anchor when the player leaves the leash. Never heals. Time.time only, so a paused game freezes it.
    /// State and the attack timing are public for BestiaryProbe.
    /// </summary>
    [RequireComponent(typeof(EnemyAI))]
    public abstract class SpeciesBrain : MonoBehaviour, IEnemyBrain, IEngagedBrain, IHitAwareBrain
    {
        private static readonly List<SpeciesBrain> all = new List<SpeciesBrain>();
        public static IReadOnlyList<SpeciesBrain> All => all;

        private const float SteerInterval = 0.12f;
        private const float ProbeDistance = 1.1f;
        private const float WalkableNormalY = 0.6f;
        private const float PlayerRadius = 0.35f;
        private static readonly float[] SteeringAngles = { 0f, 35f, -35f, 70f, -70f, 100f, -100f };

        public SpeciesActor Actor { get; private set; }
        public EnemyAI Enemy { get; private set; }
        public SpeciesSpec Spec { get; private set; }
        public BrainState State { get; private set; } = BrainState.Idle;
        public string Detail { get; protected set; } = "";
        public bool Engaged => engaged && Enemy != null && !Enemy.IsDead;
        public bool IsDormant => dormant;
        public bool PlayerVisible => playerVisible;
        public Attack CurrentAttack => plan;

        // Timing, for the probe and for logs.
        public float WindupStartedAt { get; private set; } = -1f;
        public float LastDamageAt { get; private set; } = -1f;
        /// <summary>Seconds from the start of the wind-up to the moment the last damage landed.</summary>
        public float LastDamageAfterWindup { get; private set; } = -1f;
        public int AttacksResolved { get; private set; }
        public bool LastAttackHit { get; private set; }
        public int Hits { get; private set; }
        public int Interrupts { get; private set; }
        public float RecoverUntil { get; private set; }
        public float MaxDistanceFromAnchor { get; private set; }
        public Vector3 SpawnPoint => spawnPoint;

        protected Transform player;
        protected CharacterController playerBody;
        protected SurvivalStats playerStats;
        protected EnemyPose pose;
        protected Attack plan;
        protected float stateStarted;
        protected bool engaged;
        protected bool dormant;
        protected bool playerVisible;
        protected float lastSeen = float.NegativeInfinity;
        protected float alertedUntil = float.NegativeInfinity;
        protected float nextAttack;
        protected float chaseTurnRate = 540f;
        protected float wanderRadius = 5f;

        private bool initialized;
        private Vector3 spawnPoint;
        private float nextSight;
        private Vector3 steering;
        private float nextSteer;
        private Vector3 wanderTarget;
        private float nextWander;
        private System.Random wanderRandom;
        private bool wakeAnnounced;
        // Dash (lunge, rush, charge): a straight run in a direction fixed at its start.
        private Vector3 dashDirection;
        private float dashSpeed, dashMax, dashTravelled, dashContact, dashScale, dashRecoverHit, dashRecoverMiss, dashStuckSince;
        private bool dashStunOnWall;
        public int WallStuns { get; private set; }

        // ---------- Setup ----------

        protected virtual void Awake()
        {
            Actor = GetComponent<SpeciesActor>();
            Enemy = GetComponent<EnemyAI>();
            pose = GetComponent<EnemyPose>();
            Spec = Actor != null ? Actor.Spec : SpeciesSpec.Of(SpeciesId.Wanderer);
            dormant = Spec.IsBoss;
            if (Spec.IsBoss) wanderRadius = 0f;
            wanderRandom = new System.Random(Actor != null ? Actor.LootSeed : 1);
        }

        private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        private void OnDisable() => all.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            spawnPoint = transform.position;
            if (pose == null) pose = GetComponent<EnemyPose>();
            nextAttack = Time.time + 0.5f;
            SetState(dormant ? BrainState.Dormant : BrainState.Idle);
            OnStart();
        }

        protected virtual void OnStart() { }

        private void FindPlayer()
        {
            FirstPersonController controller = Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (controller == null) return;
            player = controller.transform;
            playerBody = controller.GetComponent<CharacterController>();
            playerStats = controller.GetComponent<SurvivalStats>();
        }

        protected Vector3 PlayerCenter => playerBody != null ? player.TransformPoint(playerBody.center) : player.position + Vector3.up * 0.9f;

        // ---------- Anchor and leash ----------

        public bool Anchored => (Actor != null && Actor.GuardRadius > 0f) || Spec.IsBoss || LeashOverride > 0f;
        public Vector3 Anchor => Actor != null && Actor.GuardRadius > 0f ? Actor.GuardPoint : spawnPoint;
        /// <summary>Where the boss notices the player on its own. A boss spawned without one guards 12 m.</summary>
        public float GuardRadius => Actor != null && Actor.GuardRadius > 0f ? Actor.GuardRadius : 12f;
        /// <summary>How far from the anchor it may ever go, and where the player stops being its business.</summary>
        public float Leash => LeashOverride > 0f ? LeashOverride : Anchored ? GuardRadius + 15f : float.PositiveInfinity;
        protected virtual float LeashOverride => 0f;
        protected float LoseRadius => Mathf.Max(Spec.Detection * 2.5f, 30f);

        // ---------- Public verbs ----------

        /// <summary>The group alarm (a Screamer, or a camp member attacked). Wakes a dormant boss too.</summary>
        public virtual void Alert(Vector3 from)
        {
            Initialize();
            if (Enemy == null || Enemy.IsDead) return;
            if (dormant) Wake();
            alertedUntil = Time.time + 10f;
            lastSeen = Time.time;
            Engage();
        }

        public void Wake()
        {
            Initialize();
            if (!dormant) return;
            dormant = false;
            if (Spec.IsBoss && !wakeAnnounced)
            {
                wakeAnnounced = true;
                GameMessageBus.Post($"The {Spec.Name} wakes up.");
                GameAudio.PlayAt(WakeSound, transform.position, 1f, 90f);
            }
            if (State == BrainState.Dormant) SetState(BrainState.Idle);
            lastSeen = Time.time;
            Engage();
        }

        protected virtual string WakeSound => Sfx.Quake;

        // ---------- Tick ----------

        public void TickBrain()
        {
            Initialize();
            if (player == null) FindPlayer();
            if (player == null || Enemy == null) return;

            if (Enemy.IsStunned)
            {
                if (State != BrainState.Stunned) { plan = null; SetState(BrainState.Stunned); }
                pose?.Set(PoseKind.Stagger, 400f);
                Enemy.BrainMove(Vector3.zero);
                return;
            }
            if (State == BrainState.Stunned)
            {
                pose?.Set(PoseKind.Rest);
                SetState(engaged ? BrainState.Chasing : BrainState.Returning);
                nextAttack = Mathf.Max(nextAttack, Time.time + 0.3f);
            }

            Vector3 toPlayer = PlayerCenter - Enemy.CenterPosition;
            float distance = Flat(toPlayer).magnitude;
            UpdateSight(toPlayer.magnitude);
            UpdateEngagement(distance);
            if (engaged) TickEngaged(toPlayer, distance);
            else TickIdle();
            if (Anchored) MaxDistanceFromAnchor = Mathf.Max(MaxDistanceFromAnchor, Flat(transform.position - Anchor).magnitude);
        }

        private void UpdateSight(float distance)
        {
            if (Time.time < nextSight) return;
            nextSight = Time.time + 0.2f;
            playerVisible = distance <= Mathf.Max(Spec.Detection, 40f) &&
                RaidSight.Clear(Enemy.CenterPosition + Vector3.up * 0.25f, PlayerCenter + Vector3.up * 0.25f, transform, player);
            if (playerVisible) lastSeen = Time.time;
        }

        private bool PlayerInLeash => !Anchored ||
            (Flat(player.position - Anchor).magnitude <= Leash && Mathf.Abs(player.position.y - Anchor.y) < 8f);

        private void UpdateEngagement(float distance)
        {
            if (dormant)
            {
                bool inArena = Flat(player.position - Anchor).magnitude <= GuardRadius && Mathf.Abs(player.position.y - Anchor.y) < 6f;
                if (inArena && (playerVisible || distance < 4f)) Wake();
                return;
            }
            if (!engaged)
            {
                // EnemyAI.detectionRadius is the live value (Bestiary.Spawn sets it from the spec), so other code can blind or sharpen it.
                float effectiveDetection = Enemy.detectionRadius * PlayerStealth.DetectionFactor;
                bool sees = playerVisible && distance <= effectiveDetection;
                bool touches = Enemy.detectionRadius > 0f && distance <= 2.5f * PlayerStealth.DetectionFactor && Mathf.Abs(player.position.y - transform.position.y) < 2.5f;
                // A woken boss guards its lair: the player inside the arena is enough.
                bool inArena = Spec.IsBoss && Flat(player.position - Anchor).magnitude <= GuardRadius && playerVisible;
                if ((sees || touches || inArena || Time.time < alertedUntil) && PlayerInLeash) Engage();
                return;
            }
            if (IsCommitted) return;
            bool lost = !PlayerInLeash || distance > LoseRadius || (Time.time - lastSeen > 8f && Time.time >= alertedUntil);
            if (lost) Disengage();
        }

        /// <summary>In the middle of a wind-up or a dash: the attack finishes before anything else is decided.</summary>
        protected bool IsCommitted => State == BrainState.Windup || State == BrainState.Dash;

        protected void Engage()
        {
            if (engaged || Enemy.IsDead) return;
            engaged = true;
            nextAttack = Mathf.Max(nextAttack, Time.time + 0.4f);
            if (State == BrainState.Idle || State == BrainState.Returning || State == BrainState.Dormant) SetState(BrainState.Chasing);
            OnEngaged();
        }

        protected virtual void OnEngaged() { }

        protected void Disengage()
        {
            engaged = false;
            alertedUntil = float.NegativeInfinity;
            plan = null;
            Enemy.BrainCancelAttack();
            pose?.Set(PoseKind.Rest);
            SetState(BrainState.Returning);
            OnDisengaged();
        }

        protected virtual void OnDisengaged() { }

        protected void SetState(BrainState state)
        {
            State = state;
            stateStarted = Time.time;
        }

        protected float InState => Time.time - stateStarted;

        // ---------- Idle ----------

        protected virtual void TickIdle()
        {
            if (dormant)
            {
                Detail = "dormant";
                Move(Vector3.zero);
                pose?.Set(IdlePose);
                return;
            }
            Vector3 home = Anchor;
            if (State == BrainState.Returning)
            {
                Detail = "returning";
                if (Flat(home - transform.position).magnitude <= 1.5f) { SetState(BrainState.Idle); OnReturned(); }
                else { WalkTo(home, 0f, Spec.Speed * 0.85f); return; }
            }
            if (State != BrainState.Idle) SetState(BrainState.Idle);
            pose?.Set(IdlePose);
            if (wanderRadius <= 0f) { Detail = "guarding"; Move(Vector3.zero); return; }
            if (Time.time >= nextWander)
            {
                nextWander = Time.time + 4f + (float)wanderRandom.NextDouble() * 4f;
                double angle = wanderRandom.NextDouble() * System.Math.PI * 2.0;
                float radius = wanderRadius * (float)wanderRandom.NextDouble();
                wanderTarget = home + new Vector3(Mathf.Cos((float)angle), 0f, Mathf.Sin((float)angle)) * radius;
            }
            Detail = "wandering";
            WalkTo(wanderTarget, 0.6f, Spec.Speed * 0.4f);
        }

        protected virtual PoseKind IdlePose => PoseKind.Rest;
        protected virtual void OnReturned() { }

        // ---------- Engaged ----------

        protected virtual void TickEngaged(Vector3 toPlayer, float distance)
        {
            switch (State)
            {
                case BrainState.Windup: TickWindup(toPlayer); return;
                case BrainState.Dash: TickDash(); return;
                case BrainState.Recover:
                    Detail = plan != null ? "recovering from " + plan.Name : "recovering";
                    Move(Vector3.zero);
                    Face(toPlayer, 60f);
                    if (InState > 0.3f && plan != null) pose?.Set(plan.RecoverPose, 200f);
                    if (Time.time >= RecoverUntil) { pose?.Set(PoseKind.Rest, 200f); OnRecovered(); if (State == BrainState.Recover) SetState(BrainState.Chasing); }
                    return;
                default:
                    if (TickSpecial(toPlayer, distance)) return;
                    if (State != BrainState.Chasing) SetState(BrainState.Chasing);
                    TickChase(toPlayer, distance);
                    return;
            }
        }

        /// <summary>Species-specific states (backing off, emerging, breaking a wall). True when it used the frame.</summary>
        protected virtual bool TickSpecial(Vector3 toPlayer, float distance) => false;
        protected virtual void OnRecovered() { }

        protected virtual void TickChase(Vector3 toPlayer, float distance)
        {
            if (Time.time >= nextAttack && playerVisible)
            {
                Attack attack = ChooseAttack(distance);
                if (attack != null && distance <= attack.StartRange && Mathf.Abs(toPlayer.y) < 2.5f + Spec.Height * 0.5f)
                {
                    // It turns to the player first: a wind-up always starts facing them, so the telegraph reads.
                    if (Vector3.Angle(Flat(transform.forward), Flat(toPlayer)) <= 45f)
                    {
                        BeginAttack(attack);
                        return;
                    }
                    if (distance <= attack.Reach)
                    {
                        Detail = "turning to attack";
                        Move(Vector3.zero);
                        Face(toPlayer, chaseTurnRate);
                        return;
                    }
                }
            }
            Detail = "chasing";
            float stop = Mathf.Max(Spec.Radius + PlayerRadius + 0.2f, Mathf.Min(Spec.AttackRange * 0.7f, 1.6f));
            if (distance <= stop)
            {
                Move(Vector3.zero);
                Face(toPlayer, chaseTurnRate);
                return;
            }
            WalkTo(player.position, stop, Spec.Speed);
        }

        /// <summary>The attack it would start now at this distance, or null.</summary>
        protected abstract Attack ChooseAttack(float distance);

        // ---------- Attacks ----------

        protected void BeginAttack(Attack attack)
        {
            plan = attack;
            WindupStartedAt = Time.time;
            SetState(BrainState.Windup);
            Detail = "winding up " + attack.Name;
            pose?.Set(attack.WindPose, 300f);
            if (!string.IsNullOrEmpty(attack.WindSound)) GameAudio.PlayAt(attack.WindSound, transform.position + Vector3.up, attack.WindVolume, 35f);
            Move(Vector3.zero);
            OnWindupStart(attack);
        }

        protected virtual void OnWindupStart(Attack attack) { }

        /// <summary>Movement during a wind-up (the Shift Commander's steps). Default: stands.</summary>
        protected virtual Vector3 WindupMotion(float t) => Vector3.zero;

        private void TickWindup(Vector3 toPlayer)
        {
            float t = Time.time - WindupStartedAt;
            if (plan.TurnRate > 0f) Face(toPlayer, plan.TurnRate);
            Move(WindupMotion(t));
            Enemy.BrainPoseWeapon(t / Mathf.Max(0.05f, plan.Windup));
            if (t < plan.Windup) return;
            Resolve(plan);
        }

        /// <summary>The end of a wind-up. Default: a melee strike in the front arc.</summary>
        protected virtual void Resolve(Attack attack)
        {
            if (attack.ProjectileSpeed > 0f)
            {
                pose?.Set(attack.StrikePose, 900f);
                Enemy.BrainPoseWeapon(-1f);
                Vector3 origin = Enemy.CenterPosition + transform.forward * (Spec.Radius + 0.15f);
                bool launched = playerVisible && RaidSight.Clear(origin, PlayerCenter, transform, player);
                if (launched) SpeciesProjectile.Launch(this, origin, PlayerCenter, attack.ProjectileSpeed, Spec.Damage * attack.ProjectileScale);
                Resolved(false); // The shot is only a hit if its travelling bolt reaches the player.
                Recover(launched ? attack.RecoverHit : attack.RecoverMiss);
                OnStruck(attack, false);
                return;
            }
            bool hit = Strike(attack);
            Recover(hit ? attack.RecoverHit : attack.RecoverMiss);
            OnStruck(attack, hit);
        }

        /// <summary>Called by a projectile after its collision; damage uses the same survival and direction path as melee.</summary>
        public bool ProjectileHit(float damageScale)
        {
            bool hit = DealDamage(damageScale);
            if (hit) LastAttackHit = true;
            return hit;
        }

        protected virtual void OnStruck(Attack attack, bool hit) { }

        /// <summary>Damage only if the player is still in reach, in front and in sight. Counts as a resolved attack.</summary>
        protected bool Strike(Attack attack)
        {
            pose?.Set(attack.StrikePose, 900f);
            Enemy.BrainPoseWeapon(-1f);
            if (!string.IsNullOrEmpty(attack.StrikeSound)) GameAudio.PlayAt(attack.StrikeSound, transform.position + Vector3.up, 0.9f, 30f);
            bool hit = PlayerInArc(attack.Reach, attack.HalfArc) && DealDamage(attack.DamageScale);
            Resolved(hit);
            return hit;
        }

        protected void Resolved(bool hit)
        {
            AttacksResolved++;
            LastAttackHit = hit;
        }

        protected void Recover(float seconds)
        {
            RecoverUntil = Time.time + seconds;
            nextAttack = RecoverUntil;
            SetState(BrainState.Recover);
        }

        /// <summary>Is the player within reach of the centre, inside +-halfArc of facing, vertically close and not behind a wall?</summary>
        public bool PlayerInArc(float reach, float halfArc)
        {
            if (player == null) return false;
            Vector3 offset = PlayerCenter - Enemy.CenterPosition;
            Vector3 flat = Flat(offset);
            if (flat.magnitude > reach + PlayerRadius) return false;
            if (Mathf.Abs(player.position.y - Enemy.FeetPosition.y) > Mathf.Max(2f, Spec.Height * 0.75f)) return false;
            if (flat.magnitude > Spec.Radius && Vector3.Angle(Flat(transform.forward), flat) > halfArc) return false;
            return RaidSight.Clear(Enemy.CenterPosition, PlayerCenter, transform, player);
        }

        protected bool DealDamage(float scale)
        {
            if (playerStats == null || playerStats.IsDead) return false;
            float before = playerStats.health;
            playerStats.TakeDamage(Spec.Damage * scale);
            float dealt = before - playerStats.health;
            if (dealt <= 0f) return false;
            DamageDirection.Report(transform.position);
            Hits++;
            LastDamageAt = Time.time;
            LastDamageAfterWindup = Time.time - WindupStartedAt;
            GameMessageBus.Post($"The {Spec.Name} hit you for {dealt:0} damage.");
            return true;
        }

        // ---------- Dash ----------

        /// <summary>A straight run in the current facing. No homing once started. Hits once on contact.</summary>
        protected void BeginDash(float speed, float maxDistance, float contact, float damageScale, float recoverHit, float recoverMiss, bool stunOnWall, PoseKind dashPose)
        {
            dashDirection = Flat(transform.forward).normalized;
            dashSpeed = speed;
            dashMax = maxDistance;
            dashTravelled = 0f;
            dashContact = contact;
            dashScale = damageScale;
            dashRecoverHit = recoverHit;
            dashRecoverMiss = recoverMiss;
            dashStunOnWall = stunOnWall;
            dashStuckSince = -1f;
            pose?.Set(dashPose, 900f);
            SetState(BrainState.Dash);
            Detail = "dashing";
        }

        private void TickDash()
        {
            Vector3 offset = Flat(PlayerCenter - Enemy.CenterPosition);
            if (offset.magnitude <= dashContact + Spec.Radius && Vector3.Angle(dashDirection, offset) < 75f &&
                Mathf.Abs(player.position.y - Enemy.FeetPosition.y) < 2f && DealDamage(dashScale))
            {
                EndDash(true);
                return;
            }
            float step = dashSpeed * Time.deltaTime;
            Probe(dashDirection, step + 0.15f, out float clearance, out Collider blocker);
            if (blocker != null && blocker.GetComponentInParent<EnemyAI>() == null && clearance <= step + 0.05f)
            {
                if (dashStunOnWall)
                {
                    WallStuns++;
                    Resolved(false);
                    Detail = "stunned by " + blocker.name;
                    GameAudio.PlayAt(Sfx.HitBlock, transform.position + Vector3.up, 1f, 35f);
                    Enemy.Stun(2f);
                    return;
                }
                EndDash(false);
                return;
            }
            Vector3 before = transform.position;
            Move(dashDirection * dashSpeed / Mathf.Max(0.05f, Enemy.SpeedFactor));
            float moved = Flat(transform.position - before).magnitude;
            dashTravelled += moved;
            if (moved < step * 0.2f) { if (dashStuckSince < 0f) dashStuckSince = Time.time; }
            else dashStuckSince = -1f;
            if (dashTravelled >= dashMax || (dashStuckSince >= 0f && Time.time - dashStuckSince > 0.3f) || InState > dashMax / Mathf.Max(0.5f, dashSpeed) + 1f)
                EndDash(false);
        }

        private void EndDash(bool hit)
        {
            Resolved(hit);
            pose?.Set(PoseKind.Rest, 300f);
            Recover(hit ? dashRecoverHit : dashRecoverMiss);
        }

        // ---------- Hits ----------

        public virtual float FilterDamage(float amount, Vector3? from) => amount;

        public virtual void OnHurt(float dealt, Vector3? from)
        {
            Initialize();
            lastSeen = Time.time;
            if (dormant) Wake();
            else Engage();
            if (!Enemy.superArmor && State == BrainState.Windup) Interrupt();
        }

        protected virtual void Interrupt()
        {
            Interrupts++;
            OnInterrupted(plan);
            Enemy.BrainCancelAttack();
            pose?.Set(PoseKind.Stagger, 600f);
            Detail = "interrupted";
            Recover(0.6f);
        }

        protected virtual void OnInterrupted(Attack attack) { }

        /// <summary>Is a hit from this position on the front (within +-halfAngle of where it faces)?</summary>
        protected bool FromFront(Vector3? from, float halfAngle)
        {
            if (!from.HasValue) return false;
            Vector3 toward = Flat(from.Value - transform.position);
            return toward.sqrMagnitude > 0.0001f && Vector3.Angle(Flat(transform.forward), toward) <= halfAngle;
        }

        // ---------- Moving ----------

        /// <summary>Moves, clamped so an anchored actor never leaves its leash. Returns the controller's collision flags.</summary>
        protected CollisionFlags Move(Vector3 velocity)
        {
            if (Anchored && velocity.sqrMagnitude > 0f && !float.IsInfinity(Leash))
            {
                Vector3 radial = Flat(transform.position - Anchor);
                Vector3 next = radial + Flat(velocity) * Time.deltaTime;
                if (next.magnitude > Leash - 0.5f && radial.sqrMagnitude > 0.01f)
                {
                    Vector3 outward = radial.normalized;
                    float along = Vector3.Dot(velocity, outward);
                    if (along > 0f) velocity -= outward * along;
                }
            }
            return Enemy.BrainMove(velocity);
        }

        protected void Face(Vector3 direction, float degreesPerSecond)
        {
            Vector3 flat = Flat(direction);
            if (flat.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(flat);
            Quaternion turned = Quaternion.RotateTowards(transform.rotation, target, degreesPerSecond * Time.deltaTime);
            Enemy.BrainFace(turned * Vector3.forward);
        }

        protected void WalkTo(Vector3 destination, float stopDistance, float speed)
        {
            Vector3 flat = Flat(destination - transform.position);
            if (flat.magnitude <= stopDistance)
            {
                Move(Vector3.zero);
                return;
            }
            if (Time.time >= nextSteer)
            {
                nextSteer = Time.time + SteerInterval;
                steering = Enemy.TryGetPathDirection(destination, out Vector3 pathDirection)
                    ? pathDirection : Choose(flat.normalized);
            }
            Vector3 motion = steering * speed;
            motion = Vector3.ClampMagnitude(motion, flat.magnitude / Mathf.Max(Time.deltaTime, 0.001f));
            if (motion.sqrMagnitude > 0.0001f) Face(motion, chaseTurnRate);
            Move(motion);
        }

        /// <summary>A blocker straight ahead: return true to stop and deal with it (the Breacher breaks it).</summary>
        protected virtual bool OnBlocked(Collider blocker) => false;

        private Vector3 Choose(Vector3 desired)
        {
            Probe(desired, ProbeDistance, out _, out Collider blocker);
            if (blocker == null) return desired;
            if (OnBlocked(blocker)) return Vector3.zero;
            if (blocker.GetComponentInParent<EnemyAI>() != null) return desired;
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

        /// <summary>RaidAgent's capsule probe: the lower end is above the step height and walkable surfaces don't count.</summary>
        protected void Probe(Vector3 direction, float distance, out float clearance, out Collider nearest)
        {
            CharacterController body = Enemy.Body;
            float radius = body.radius * Mathf.Abs(transform.lossyScale.x);
            float half = Mathf.Max(radius, body.height * Mathf.Abs(transform.lossyScale.y) * 0.5f);
            Vector3 center = Enemy.CenterPosition;
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

        protected static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        protected Attack Melee(string name, float windup, float reach, float halfArc, float recoverHit, float recoverMiss,
            PoseKind wind = PoseKind.ArmsUp, PoseKind strike = PoseKind.Slam, string sound = Sfx.RaiderGrowl) => new Attack
        {
            Name = name,
            Windup = Mathf.Max(windup, Spec.AttackWindup),
            Reach = reach,
            StartRange = Mathf.Max(Spec.Radius + PlayerRadius + 0.1f, reach * 0.85f),
            HalfArc = halfArc,
            RecoverHit = recoverHit,
            RecoverMiss = recoverMiss,
            WindPose = wind,
            StrikePose = strike,
            WindSound = sound,
        };
    }
}
