using System;
using System.Collections.Generic;
using SheltreSim.Audio;
using SheltreSim.Bestiary;
using SheltreSim.Building;
using SheltreSim.Enemies;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Raids;
using SheltreSim.Saving;
using SheltreSim.Storage;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SheltreSim.World
{
    /// <summary>Heavy fuel-driven vehicle with a large cargo hold and a slow, deliberate ram.</summary>
    public sealed class RideableArmoredVan : MonoBehaviour, IInteractable, IItemStore, IPieceState
    {
        public const float CargoCapacityKg = 300f;
        public const float FuelRangePerCan = 4000f;
        public const float BatteryRangePerCell = 1500f;
        public const float TankRange = 12000f;
        public const float TurretRange = 70f;
        public const float TurretDamage = 45f;
        public const float TurretShotInterval = 0.75f;
        // Heavy, but still much quicker than walking; sprint gives a clear highway-speed burst.
        public float walkSpeed = 20f;
        public float sprintSpeed = 27f;
        public float ramRadius = 1f;
        public float minimumRamSpeed = 6f;
        public string InteractPrompt => "Press E to drive armored van";

        [Serializable] private sealed class SavedStack { public string id; public int count; }
        [Serializable] private sealed class SavedVan
        {
            public float fuelMeters;
            public bool headlights;
            public List<SavedStack> cargo = new List<SavedStack>();
        }

        private readonly Dictionary<string, int> contents = new Dictionary<string, int>();
        private readonly Dictionary<UnityEngine.Object, float> targetRamCooldown = new Dictionary<UnityEngine.Object, float>();
        private readonly Dictionary<Rigidbody, float> impactReadyAt = new Dictionary<Rigidbody, float>();
        private readonly List<Light> headlights = new List<Light>();
        private readonly Collider[] ramHits = new Collider[32];
        private readonly RaycastHit[] vehicleBlockHits = new RaycastHit[32];
        private readonly Collider[] vehicleOverlapHits = new Collider[32];
        private FirstPersonController rider;
        private VehicleMountView mountView;
        private float oldWalk, oldSprint, fuelMeters, nextNoise, nextCooldownCleanup, nextTurretShot, nextDryTurretMessage, driveScale, currentSpeed;
        private Vector3 lastRiderPosition, lastBumperPosition, vehicleLocalCenter, vehicleLocalHalfExtents;
        private InputAction dismount, cargoAction, refuelAction, batteryAction, lightAction, turretAction, hornAction;
        private Collider[] vehicleColliders;
        private bool[] colliderStates;
        private Transform[] wheels;
        private Quaternion[] wheelRestRotations;
        private float wheelRotation;
        private bool mounted, lightsOn;

        // The prefab root is the footprint centre: the floor is half the footprint height below it, so the root rides that far ABOVE the rider's feet.
        private const float RootHeight = 1.25f; // footprint 2.5 m high
        public Vector3 StorePosition => transform.position;
        public IReadOnlyDictionary<string, int> Contents => contents;
        public float FuelRemainingMeters => fuelMeters;
        public bool IsMounted => mounted;
        public float Speed => currentSpeed;
        public float Throttle01 => rider != null && mounted ? Mathf.Clamp01(rider.MoveInput.magnitude) : 0f;
        public float FuelFraction01 => TankRange > 0f ? Mathf.Clamp01(fuelMeters / TankRange) : 0f;
        public int GetCount(string id) => id != null && contents.TryGetValue(id, out int count) ? count : 0;
        public int GetAddRoom(string id) => Mathf.Max(0, Mathf.FloorToInt((CargoCapacityKg - CargoWeight()) / Mathf.Max(.01f, Encumbrance.WeightOf(id))));

        private void Awake()
        {
            vehicleColliders = GetComponentsInChildren<Collider>(true);
            colliderStates = new bool[vehicleColliders.Length];
            for (int i = 0; i < vehicleColliders.Length; i++) colliderStates[i] = vehicleColliders[i].enabled;
            VehicleTerrain.CacheWheels(transform, out wheels, out wheelRestRotations);
            MeasureVehicleBounds();
            dismount = new InputAction("DismountArmoredVan", InputActionType.Button, "<Keyboard>/space");
            cargoAction = new InputAction("OpenVanCargo", InputActionType.Button, "<Keyboard>/p");
            refuelAction = new InputAction("RefuelVan", InputActionType.Button, "<Keyboard>/h");
            batteryAction = new InputAction("ChargeVan", InputActionType.Button, "<Keyboard>/j");
            lightAction = new InputAction("ToggleVanLights", InputActionType.Button, "<Keyboard>/l");
            turretAction = new InputAction("FireVanTurret", InputActionType.Button, "<Keyboard>/t");
            hornAction = new InputAction("ArmoredVanHorn", InputActionType.Button, "<Keyboard>/k");
            CreateHeadlights();
            ApplyHeadlights();
        }

        private void OnEnable()
        {
            dismount?.Enable(); cargoAction?.Enable(); refuelAction?.Enable(); batteryAction?.Enable(); lightAction?.Enable(); turretAction?.Enable(); hornAction?.Enable();
            StorageRegistry.Register(this);
        }

        private void OnDisable()
        {
            StorageRegistry.Unregister(this);
            dismount?.Disable(); cargoAction?.Disable(); refuelAction?.Disable(); batteryAction?.Disable(); lightAction?.Disable(); turretAction?.Disable(); hornAction?.Disable();
            if (mounted) ForceDismount();
            else if (mountView != null && mountView.IsAttachedTo(transform) && mountView.IsTransitioning) mountView.ForceDismount(transform);
        }

        private void OnDestroy()
        {
            dismount?.Dispose(); cargoAction?.Dispose(); refuelAction?.Dispose(); batteryAction?.Dispose(); lightAction?.Dispose(); turretAction?.Dispose(); hornAction?.Dispose();
        }

        private void Update()
        {
            if (!mounted) return;
            CharacterController riderBody = rider != null ? rider.GetComponent<CharacterController>() : null;
            if (rider == null || !rider.isActiveAndEnabled || riderBody == null || !riderBody.enabled) { ForceDismount(); return; }
            if (mountView != null && mountView.IsTransitioning) return;
            if (Time.timeScale == 0f) return;
            if (dismount.WasPressedThisFrame()) { Dismount(); return; }
            if (cargoAction.WasPressedThisFrame())
            {
                Inventory pack = rider.GetComponent<Inventory>();
                if (pack != null) StorageUI.Open(this, this, pack);
                return;
            }
            if (refuelAction.WasPressedThisFrame()) Refuel();
            if (batteryAction.WasPressedThisFrame()) ChargeBattery();
            if (lightAction.WasPressedThisFrame()) { lightsOn = !lightsOn; ApplyHeadlights(); GameMessageBus.Post(lightsOn ? "Van headlights on." : "Van headlights off."); }
            if (turretAction.WasPressedThisFrame()) FireTurret();
            if (hornAction.WasPressedThisFrame())
            {
                GameAudio.PlayAt(Sfx.VehicleHorn, transform.position + Vector3.up, 1f, 65f, 0.02f);
                AlertNearby();
            }

            Vector3 previousRiderPosition = lastRiderPosition;
            Vector3 position = rider.transform.position;
            Vector3 movement = position - previousRiderPosition;
            float travelled = Vector3.Distance(position, previousRiderPosition);
            Quaternion yaw = Quaternion.Euler(0f, rider.transform.eulerAngles.y, 0f);
            Vector3 follow = FollowPosition(position, yaw, out Quaternion desiredRotation, out float grade);
            desiredRotation = VehicleTerrain.SteerPose(transform.rotation, desiredRotation, 105f, Time.deltaTime);
            Vector3 previousVehiclePosition = transform.position;
            Vector3 resolved = ResolveVehicleMotion(follow, Time.deltaTime > 0f ? travelled / Time.deltaTime : 0f, desiredRotation);
            Vector3 correction = resolved - follow;
            if (correction.sqrMagnitude > 0.0001f)
            {
                // The CharacterController has already moved this frame. Move it back by the same amount
                // that the van body was blocked, keeping the rider seated behind the vehicle.
                riderBody.Move(correction);
                position = rider.transform.position;
                movement = position - previousRiderPosition;
                travelled = Vector3.Distance(position, previousRiderPosition);
                follow = FollowPosition(position, yaw, out desiredRotation, out grade);
                desiredRotation = VehicleTerrain.SteerPose(transform.rotation, desiredRotation, 105f, Time.deltaTime);
                resolved = ResolveVehicleMotion(follow, Time.deltaTime > 0f ? travelled / Time.deltaTime : 0f, desiredRotation);
            }

            transform.position = resolved;
            transform.rotation = desiredRotation;
            lastRiderPosition = position;
            fuelMeters = Mathf.Max(0f, fuelMeters - travelled);
            currentSpeed = Time.deltaTime > 0f ? Vector3.Dot(movement / Time.deltaTime, desiredRotation * Vector3.forward) : 0f;
            VehicleTerrain.SpinWheels(wheels, wheelRestRotations, ref wheelRotation, currentSpeed, .72f, Time.deltaTime);
            float gradeScale = VehicleTerrain.GradeSpeedMultiplier(grade, rider.MoveInput.y);
            float targetScale = fuelMeters > 0f && rider.MoveInput.sqrMagnitude > .001f ? gradeScale : 0f;
            driveScale = VehicleTerrain.ApproachDriveScale(driveScale, targetScale, Time.deltaTime, 2.2f, 3f);
            rider.walkSpeed = walkSpeed * driveScale;
            rider.sprintSpeed = sprintSpeed * driveScale;

            Vector3 previousBumper = lastBumperPosition;
            Vector3 bumper = transform.position + transform.forward * 3.2f + Vector3.up * 0.55f;
            Vector3 vehicleMovement = transform.position - previousVehiclePosition;
            vehicleMovement.y = 0f;
            float vehicleSpeed = Time.deltaTime > 0f ? vehicleMovement.magnitude / Time.deltaTime : 0f;
            if (fuelMeters > 0f && Mathf.Abs(currentSpeed) >= minimumRamSpeed && currentSpeed > 0f)
                TryRam(previousBumper, bumper, vehicleSpeed);
            lastBumperPosition = bumper;

            if (travelled > 0.05f && Time.time >= nextNoise)
            {
                nextNoise = Time.time + 2.5f;
                AlertNearby();
            }
            if (Time.time >= nextCooldownCleanup) PruneCooldowns();
        }

        public void Interact(GameObject interactor)
        {
            if (mounted || interactor == null) return;
            rider = interactor.GetComponentInParent<FirstPersonController>();
            if (rider == null) return;
            mountView = VehicleMountView.For(rider);
            if (mountView == null || mountView.IsBusy) { rider = null; mountView = null; return; }
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;
            Vector3 riderSeatPosition = transform.position - Vector3.up * RootHeight + flatForward * 3f;
            Quaternion riderSeatRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            if (!mountView.BeginMount(transform, riderSeatPosition, riderSeatRotation))
            {
                rider = null;
                mountView = null;
                GameMessageBus.Post("No clear driver space in the van.");
                return;
            }
            mounted = true;
            oldWalk = rider.walkSpeed; oldSprint = rider.sprintSpeed;
            lastRiderPosition = rider.transform.position;
            driveScale = currentSpeed = 0f;
            rider.walkSpeed = 0f;
            rider.sprintSpeed = 0f;
            rider.SetBicycleMounted(true);
            rider.SetCarSteering(true, 70f);
            foreach (Collider collider in vehicleColliders) if (collider != null) collider.enabled = false;
            lastBumperPosition = transform.position + transform.forward * 3.2f + Vector3.up * 0.55f;
            GameMessageBus.Post($"Van controls: P cargo, H fuel (+{FuelRangePerCan / 1000f:0.#} km), J energy cell (+{BatteryRangePerCell / 1000f:0.#} km), L lights, K horn, T turret (1 rifle round), Space dismount. Range: {fuelMeters / 1000f:0.#} km. Ram at speed to damage enemies and structures.");
        }

        private Vector3 FollowPosition(Vector3 riderPosition, Quaternion yaw, out Quaternion groundRotation, out float grade)
        {
            Vector3 follow = riderPosition - yaw * Vector3.forward * 3f;
            VehicleTerrain.SamplePose(follow, yaw, RootHeight, 4.05f, VehicleTerrain.CurrentSeed,
                out Vector3 position, out groundRotation, out grade, out _);
            return position;
        }

        private void MeasureVehicleBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            Vector3 min = Vector3.one * float.PositiveInfinity;
            Vector3 max = Vector3.one * float.NegativeInfinity;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                Bounds bounds = renderer.bounds;
                Vector3 center = bounds.center;
                Vector3 extents = bounds.extents;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 corner = transform.InverseTransformPoint(center + Vector3.Scale(extents, new Vector3(x, y, z)));
                            min = Vector3.Min(min, corner);
                            max = Vector3.Max(max, corner);
                        }
            }
            if (renderers.Length == 0 || float.IsInfinity(min.x) || float.IsInfinity(max.x))
            {
                vehicleLocalCenter = new Vector3(0f, 1f, 0f);
                vehicleLocalHalfExtents = new Vector3(1.25f, 0.9f, 2.8f);
                return;
            }
            Vector3 size = max - min;
            vehicleLocalCenter = (min + max) * 0.5f;
            vehicleLocalHalfExtents = new Vector3(Mathf.Max(.1f, size.x * .47f), Mathf.Max(.1f, size.y * .44f), Mathf.Max(.1f, size.z * .47f));
        }

        private Vector3 ResolveVehicleMotion(Vector3 desiredPosition, float speed, Quaternion desiredRotation)
        {
            Vector3 movement = desiredPosition - transform.position;
            movement.y = 0f;
            float distance = movement.magnitude;
            if (distance <= .001f) return desiredPosition;

            Vector3 direction = movement / distance;
            Vector3 scaledCenter = Vector3.Scale(vehicleLocalCenter, transform.lossyScale);
            Vector3 center = transform.position + desiredRotation * scaledCenter;
            int count = Physics.BoxCastNonAlloc(center, VehicleHalfExtents(), direction, vehicleBlockHits,
                desiredRotation, distance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = distance;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = vehicleBlockHits[i];
                if (IsUpwardSupportContact(hit)) continue;
                if (!IsVehicleBlocker(hit.collider)) continue;
                if (hit.distance < nearest) nearest = hit.distance;
            }
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = vehicleBlockHits[i];
                if (IsUpwardSupportContact(hit)) continue;
                if (hit.distance <= nearest + .02f && IsVehicleBlocker(hit.collider)) ApplyImpact(hit, direction, speed);
            }
            Array.Clear(vehicleBlockHits, 0, count);
            return nearest < distance ? transform.position + direction * Mathf.Max(0f, nearest - .06f) : desiredPosition;
        }

        private bool RotationHasClearance(Vector3 rootPosition, Quaternion rotation)
        {
            Vector3 scaledCenter = Vector3.Scale(vehicleLocalCenter, transform.lossyScale);
            Vector3 center = rootPosition + rotation * scaledCenter;
            int count = Physics.OverlapBoxNonAlloc(center, VehicleHalfExtents(), vehicleOverlapHits, rotation,
                ~0, QueryTriggerInteraction.Ignore);
            bool clear = true;
            for (int i = 0; i < count; i++)
                if (IsVehicleBlocker(vehicleOverlapHits[i])) { clear = false; break; }
            Array.Clear(vehicleOverlapHits, 0, count);
            return clear;
        }

        private Vector3 VehicleHalfExtents()
        {
            Vector3 scale = transform.lossyScale;
            return Vector3.Scale(vehicleLocalHalfExtents, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        }

        private bool IsVehicleBlocker(Collider collider)
        {
            if (collider == null || collider.transform == transform || collider.transform.IsChildOf(transform)) return false;
            if (rider != null && (collider.transform == rider.transform || collider.transform.IsChildOf(rider.transform))) return false;
            return true;
        }

        // The terrain-follow solver places the chassis over the sampled slope. Do not treat
        // an upward-facing, driveable hill as a vertical obstacle during the horizontal sweep.
        private static bool IsUpwardSupportContact(RaycastHit hit) => VehicleTerrain.IsDriveSurface(hit);

        private void ApplyImpact(RaycastHit hit, Vector3 direction, float speed)
        {
            if (speed < minimumRamSpeed || hit.distance > speed * Time.deltaTime + .1f) return;
            Rigidbody body = hit.collider != null ? hit.collider.attachedRigidbody : null;
            if (body == null || body.isKinematic || body.transform == transform || body.transform.IsChildOf(transform)) return;
            if (impactReadyAt.TryGetValue(body, out float readyAt) && Time.time < readyAt) return;
            impactReadyAt[body] = Time.time + .35f;
            float impulse = body.mass * Mathf.Clamp(speed * .25f, 1.5f, 4f);
            body.AddForceAtPosition(direction * impulse, hit.point, ForceMode.Impulse);
        }

        private void FireTurret()
        {
            if (Time.time < nextTurretShot) return;
            Transform aim = rider != null && rider.cameraPivot != null
                ? rider.cameraPivot.GetComponentInChildren<Camera>(true)?.transform
                : null;
            if (aim == null) return;
            nextTurretShot = Time.time + TurretShotInterval;

            if (!TryTake("ammo_rifle", 1))
            {
                if (Time.time >= nextDryTurretMessage)
                {
                    nextDryTurretMessage = Time.time + 1.5f;
                    GameMessageBus.Post("Van turret empty — store rifle ammo in the cargo hold.");
                }
                return;
            }

            Vector3 origin = aim.position;
            Vector3 direction = aim.forward;
            Vector3 endpoint = origin + direction * TurretRange;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, TurretRange, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                Transform struck = hit.collider.transform;
                if (rider != null && (struck == rider.transform || struck.IsChildOf(rider.transform))) continue;
                if (struck == transform || struck.IsChildOf(transform)) continue;

                endpoint = hit.point;
                EnemyAI enemy = hit.collider.GetComponentInParent<EnemyAI>();
                if (enemy != null)
                {
                    if (!enemy.IsDead) enemy.TakeDamage(TurretDamage, rider.transform.position);
                    else continue;
                    break;
                }

                WorldWildlife animal = hit.collider.GetComponentInParent<WorldWildlife>();
                if (animal != null)
                {
                    if (!animal.IsDead && animal.IsHuntable) GameMessageBus.Post(animal.TakeHit(TurretDamage, rider.transform.position));
                    break;
                }

                PlacedPiece piece = hit.collider.GetComponentInParent<PlacedPiece>();
                StructureHealth health = piece != null ? piece.GetComponent<StructureHealth>() : null;
                if (health != null)
                {
                    float factor = HomesteadCatalog.MaterialFor(piece.pieceId) switch
                    {
                        HomesteadMaterial.Wood => .18f,
                        HomesteadMaterial.Stone => .08f,
                        _ => .04f,
                    };
                    health.TakeDamage(TurretDamage * factor);
                }
                break; // Any other solid world collider blocks the shot.
            }
            RaidTracer.Show(transform.position + transform.forward * .55f + Vector3.up * 2.1f,
                endpoint, new Color(1f, .82f, .45f), .05f);
        }

        private void TryRam(Vector3 from, Vector3 to, float speed)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            int count = distance > .001f
                ? Physics.OverlapCapsuleNonAlloc(from, to, ramRadius, ramHits, ~0, QueryTriggerInteraction.Ignore)
                : Physics.OverlapSphereNonAlloc(to, ramRadius, ramHits, ~0, QueryTriggerInteraction.Ignore);
            float damage = Mathf.Clamp(35f + speed * 7f, 35f, 160f);
            bool hitSomething = false;
            for (int i = 0; i < count; i++)
            {
                Collider collider = ramHits[i];
                if (collider == null || collider.transform == transform || collider.transform.IsChildOf(transform)) continue;
                EnemyAI enemy = collider.GetComponentInParent<EnemyAI>();
                if (enemy != null && !enemy.IsDead)
                {
                    if (!ReadyToRam(enemy)) continue;
                    enemy.TakeDamage(damage, transform.position);
                    hitSomething = true;
                    continue;
                }
                WorldWildlife animal = collider.GetComponentInParent<WorldWildlife>();
                if (animal != null && !animal.IsDead)
                {
                    if (!ReadyToRam(animal)) continue;
                    GameMessageBus.Post(animal.TakeHit(damage, transform.position));
                    hitSomething = true;
                    continue;
                }
                PlacedPiece piece = collider.GetComponentInParent<PlacedPiece>();
                StructureHealth health = piece != null ? piece.GetComponent<StructureHealth>() : null;
                if (health == null) continue;
                if (!ReadyToRam(health)) continue;
                health.TakeDamage(damage * .8f);
                hitSomething = true;
            }
            Array.Clear(ramHits, 0, count);
            if (hitSomething) AlertNearby();
        }

        private bool ReadyToRam(UnityEngine.Object target)
        {
            if (target == null) return false;
            if (targetRamCooldown.TryGetValue(target, out float next) && Time.time < next) return false;
            targetRamCooldown[target] = Time.time + 1.1f;
            return true;
        }

        private void PruneCooldowns()
        {
            nextCooldownCleanup = Time.time + 5f;
            var expired = new List<UnityEngine.Object>();
            foreach (KeyValuePair<UnityEngine.Object, float> entry in targetRamCooldown)
                if (entry.Value <= Time.time || entry.Key == null) expired.Add(entry.Key);
            foreach (UnityEngine.Object target in expired) targetRamCooldown.Remove(target);

            var expiredBodies = new List<Rigidbody>();
            foreach (KeyValuePair<Rigidbody, float> entry in impactReadyAt)
                if (entry.Value <= Time.time || entry.Key == null) expiredBodies.Add(entry.Key);
            foreach (Rigidbody body in expiredBodies) impactReadyAt.Remove(body);
        }

        private void Refuel()
        {
            Inventory pack = rider != null ? rider.GetComponent<Inventory>() : null;
            if (pack == null)
            {
                GameMessageBus.Post("Cannot access the player's inventory.");
                return;
            }
            if (TankRange - fuelMeters + 0.001f < FuelRangePerCan)
            {
                GameMessageBus.Post($"Need at least {FuelRangePerCan / 1000f:0.#} km tank space before using another fuel can.");
                return;
            }
            if (!pack.TryConsume("fuel", 1)) { GameMessageBus.Post("Need a fuel can to refuel the van."); return; }
            fuelMeters += FuelRangePerCan;
            GameMessageBus.Post($"Van refuelled. Range: {fuelMeters / 1000f:0.#} km.");
        }

        private void ChargeBattery()
        {
            Inventory pack = rider != null ? rider.GetComponent<Inventory>() : null;
            if (pack == null) { GameMessageBus.Post("Cannot access the player's inventory."); return; }
            if (TankRange - fuelMeters + 0.001f < BatteryRangePerCell)
            {
                GameMessageBus.Post($"Need at least {BatteryRangePerCell / 1000f:0.#} km tank space before using an energy cell.");
                return;
            }
            if (!pack.TryConsume("energy_cell", 1)) { GameMessageBus.Post("Need an energy cell to recharge the van."); return; }
            fuelMeters += BatteryRangePerCell;
            GameMessageBus.Post($"Van battery charged. Range: {fuelMeters / 1000f:0.#} km.");
        }

        private void AlertNearby()
        {
            const float radius = 45f;
            foreach (SpeciesBrain brain in SpeciesBrain.All)
                if (brain != null && brain.Enemy != null && !brain.Enemy.IsDead && (brain.transform.position - transform.position).sqrMagnitude <= radius * radius)
                    brain.Alert(transform.position);
            RaidDirector director = RaidDirector.Active;
            if (director != null)
                foreach (RaidAgent agent in director.Actors)
                    if (agent != null) agent.HearNoise(transform.position, radius);
        }

        private void CreateHeadlights()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject go = new GameObject(side < 0 ? "Headlight_L" : "Headlight_R");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(side * .9f, .2f, 3.1f);
                go.transform.localRotation = Quaternion.Euler(7f, 0f, 0f);
                Light lamp = go.AddComponent<Light>();
                lamp.type = LightType.Spot; lamp.range = 45f; lamp.spotAngle = 62f; lamp.intensity = 3.5f;
                lamp.color = new Color(1f, .9f, .7f); lamp.shadows = LightShadows.None;
                headlights.Add(lamp);
            }
        }

        private void ApplyHeadlights() { foreach (Light lamp in headlights) if (lamp != null) lamp.enabled = lightsOn; }

        private float CargoWeight()
        {
            float total = 0f;
            foreach (KeyValuePair<string, int> stack in contents) total += Encumbrance.WeightOf(stack.Key) * stack.Value;
            return total;
        }

        public void Add(string id, int amount)
        {
            if (string.IsNullOrWhiteSpace(id) || amount <= 0) return;
            int accepted = Mathf.Min(amount, Mathf.Min(GetAddRoom(id), int.MaxValue - GetCount(id)));
            if (accepted > 0) contents[id] = GetCount(id) + accepted;
        }

        public bool TryTake(string id, int amount)
        {
            if (amount <= 0 || GetCount(id) < amount) return false;
            int remaining = GetCount(id) - amount;
            if (remaining == 0) contents.Remove(id); else contents[id] = remaining;
            return true;
        }

        public string CapturePieceState()
        {
            var saved = new SavedVan { fuelMeters = fuelMeters, headlights = lightsOn };
            foreach (KeyValuePair<string, int> stack in contents) saved.cargo.Add(new SavedStack { id = stack.Key, count = stack.Value });
            saved.cargo.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return JsonUtility.ToJson(saved);
        }

        public void RestorePieceState(string state)
        {
            contents.Clear(); fuelMeters = 0f; lightsOn = false;
            if (!string.IsNullOrEmpty(state))
            {
                try
                {
                    SavedVan saved = JsonUtility.FromJson<SavedVan>(state);
                    if (saved != null)
                    {
                        fuelMeters = Mathf.Clamp(saved.fuelMeters, 0f, TankRange);
                        lightsOn = saved.headlights;
                        if (saved.cargo != null)
                            foreach (SavedStack stack in saved.cargo)
                                if (stack != null && stack.count > 0) Add(stack.id, stack.count);
                    }
                }
                catch (Exception error) { Debug.LogWarning($"[RideableArmoredVan] Could not restore cargo: {error.Message}"); }
            }
            ApplyHeadlights();
        }

        private void Dismount()
        {
            if (!mounted || mountView == null || mountView.IsTransitioning) return;
            if (!mountView.BeginDismount(transform))
            {
                GameMessageBus.Post("Can't exit yet — nearby space is blocked. Move or turn the van and try again.");
                return;
            }
            FinishDismount();
        }

        private void ForceDismount()
        {
            if (mountView != null) mountView.ForceDismount(transform);
            FinishDismount();
        }

        private void FinishDismount()
        {
            mounted = false;
            driveScale = currentSpeed = 0f;
            if (rider != null)
            {
                rider.walkSpeed = oldWalk; rider.sprintSpeed = oldSprint;
                rider.SetCarSteering(false);
                rider.SetBicycleMounted(false);
                rider = null;
            }
            for (int i = 0; i < vehicleColliders.Length; i++)
                if (vehicleColliders[i] != null) vehicleColliders[i].enabled = colliderStates[i];
        }
    }
}
