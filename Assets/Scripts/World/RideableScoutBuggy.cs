using System;
using System.Collections.Generic;
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
    /// <summary>Fuel-powered scout buggy with headlights and a 120 kg trunk.</summary>
    public sealed class RideableScoutBuggy : MonoBehaviour, IInteractable, IItemStore, IPieceState
    {
        public const float TrunkCapacityKg = 120f;
        public const float FuelRangePerCan = 700f;
        public const float BatteryRangePerCell = 500f;
        public const float TankRange = FuelRangePerCan * 3f;
        public const float RamMinSpeed = 5.5f;
        public const float RamCooldown = 1.2f;
        public float walkSpeed = 18f;
        public float sprintSpeed = 22f;
        public string InteractPrompt => "Press E to drive scout buggy";

        [Serializable] private sealed class SavedStack { public string id; public int count; }
        [Serializable] private sealed class SavedBuggy
        {
            public float fuelMeters;
            public bool headlights;
            public List<SavedStack> trunk = new List<SavedStack>();
        }

        private readonly Dictionary<string, int> contents = new Dictionary<string, int>();
        private readonly Dictionary<UnityEngine.Object, float> ramReadyAt = new Dictionary<UnityEngine.Object, float>();
        private readonly Dictionary<Rigidbody, float> impactReadyAt = new Dictionary<Rigidbody, float>();
        private readonly List<Light> headlights = new List<Light>();
        private readonly Collider[] ramHits = new Collider[24];
        private readonly RaycastHit[] vehicleBlockHits = new RaycastHit[32];
        private readonly Collider[] vehicleOverlapHits = new Collider[32];
        private FirstPersonController rider;
        private float oldWalk, oldSprint, fuelMeters, nextNoise, nextRamCleanup;
        private Vector3 lastRiderPosition, lastBumperPosition, vehicleLocalCenter, vehicleLocalHalfExtents;
        private InputAction dismount, trunkAction, refuelAction, batteryAction, lightAction;
        private Collider[] vehicleColliders;
        private bool[] colliderStates;
        private bool mounted, lightsOn, lowFuelWarningSent, emptyFuelWarningSent, ramSweepReady;

        public Vector3 StorePosition => transform.position;
        public IReadOnlyDictionary<string, int> Contents => contents;
        public int GetCount(string id) => id != null && contents.TryGetValue(id, out int count) ? count : 0;
        public int GetAddRoom(string id) => Mathf.Max(0, Mathf.FloorToInt((TrunkCapacityKg - TrunkWeight()) / Mathf.Max(.01f, Encumbrance.WeightOf(id))));
        public float FuelRemainingMeters => fuelMeters;

        private void Awake()
        {
            vehicleColliders = GetComponentsInChildren<Collider>(true);
            colliderStates = new bool[vehicleColliders.Length];
            for (int i = 0; i < vehicleColliders.Length; i++) colliderStates[i] = vehicleColliders[i].enabled;
            MeasureVehicleBounds();
            dismount = new InputAction("DismountBuggy", InputActionType.Button, "<Keyboard>/space");
            trunkAction = new InputAction("OpenBuggyTrunk", InputActionType.Button, "<Keyboard>/p");
            refuelAction = new InputAction("RefuelBuggy", InputActionType.Button, "<Keyboard>/h");
            batteryAction = new InputAction("ChargeBuggy", InputActionType.Button, "<Keyboard>/j");
            lightAction = new InputAction("ToggleBuggyLights", InputActionType.Button, "<Keyboard>/l");
            CreateHeadlights();
            ApplyHeadlights();
        }

        private void OnEnable()
        {
            dismount?.Enable(); trunkAction?.Enable(); refuelAction?.Enable(); batteryAction?.Enable(); lightAction?.Enable();
            StorageRegistry.Register(this);
        }

        private void OnDisable()
        {
            StorageRegistry.Unregister(this);
            dismount?.Disable(); trunkAction?.Disable(); refuelAction?.Disable(); batteryAction?.Disable(); lightAction?.Disable();
            if (mounted) Dismount();
        }

        private void OnDestroy()
        {
            dismount?.Dispose(); trunkAction?.Dispose(); refuelAction?.Dispose(); batteryAction?.Dispose(); lightAction?.Dispose();
        }

        private void Update()
        {
            if (!mounted) return;
            if (rider == null || !rider.isActiveAndEnabled || !rider.GetComponent<CharacterController>().enabled) { Dismount(); return; }
            if (Time.timeScale == 0f) return;
            if (dismount.WasPressedThisFrame()) { Dismount(); return; }
            if (trunkAction.WasPressedThisFrame())
            {
                Inventory inventory = rider.GetComponent<Inventory>();
                if (inventory != null) StorageUI.Open(this, this, inventory);
                return;
            }
            if (refuelAction.WasPressedThisFrame()) Refuel();
            if (batteryAction.WasPressedThisFrame()) ChargeBattery();
            if (lightAction.WasPressedThisFrame()) { lightsOn = !lightsOn; ApplyHeadlights(); GameMessageBus.Post(lightsOn ? "Buggy headlights on." : "Buggy headlights off."); }

            Vector3 position = rider.transform.position;
            Vector3 movement = position - lastRiderPosition;
            movement.y = 0f;
            float travelled = movement.magnitude;
            float movementSpeed = Time.deltaTime > 0f ? travelled / Time.deltaTime : 0f;
            lastRiderPosition = position;
            fuelMeters = Mathf.Max(0f, fuelMeters - travelled);
            if (fuelMeters <= 0f && !emptyFuelWarningSent)
            {
                emptyFuelWarningSent = true;
                GameMessageBus.Post("Buggy out of fuel. Dismount or press H with a fuel can to refuel.");
            }
            else if (fuelMeters <= 140f && fuelMeters > 0f && !lowFuelWarningSent)
            {
                lowFuelWarningSent = true;
                GameMessageBus.Post("Buggy fuel low — less than 140 m of range remains.");
            }
            float driveScale = fuelMeters > 0f ? 1f : 0f;
            rider.walkSpeed = walkSpeed * driveScale;
            rider.sprintSpeed = sprintSpeed * driveScale;

            Vector3 follow = position - rider.transform.forward * 1.55f;
            follow.y = position.y - .85f;
            Vector3 previousVehiclePosition = transform.position;
            Quaternion previousVehicleRotation = transform.rotation;
            Quaternion desiredRotation = Quaternion.Euler(0f, rider.transform.eulerAngles.y, 0f);
            follow = ResolveVehicleMotion(follow, desiredRotation);
            transform.position = follow;
            transform.rotation = RotationHasClearance(follow, desiredRotation) ? desiredRotation : previousVehicleRotation;
            Vector3 bumper = BumperPosition();
            Vector3 vehicleMovement = transform.position - previousVehiclePosition;
            vehicleMovement.y = 0f;
            float vehicleSpeed = Time.deltaTime > 0f ? vehicleMovement.magnitude / Time.deltaTime : 0f;
            bool movingForward = vehicleMovement.sqrMagnitude > 0.0001f && Vector3.Dot(vehicleMovement.normalized, transform.forward) > 0.5f;
            if (ramSweepReady && fuelMeters > 0f && vehicleSpeed >= RamMinSpeed && movingForward)
                TryRam(lastBumperPosition, bumper, vehicleSpeed);
            lastBumperPosition = bumper;
            ramSweepReady = true;
            if (Time.time >= nextRamCleanup) PruneRamCooldowns();
            if (travelled > 0.05f && Time.time >= nextNoise)
            {
                nextNoise = Time.time + 2.5f;
                AlertNearby();
            }
        }

        public void Interact(GameObject interactor)
        {
            if (mounted) return;
            rider = interactor.GetComponentInParent<FirstPersonController>();
            if (rider == null) return;
            mounted = true;
            oldWalk = rider.walkSpeed; oldSprint = rider.sprintSpeed;
            lastRiderPosition = rider.transform.position;
            rider.walkSpeed = walkSpeed; rider.sprintSpeed = sprintSpeed;
            rider.SetBicycleMounted(true);
            foreach (Collider c in vehicleColliders) if (c != null) c.enabled = false;
            ramSweepReady = false;
            ramReadyAt.Clear();
            GameMessageBus.Post($"Buggy controls: P trunk, H fuel (+{FuelRangePerCan:0} m), J energy cell (+{BatteryRangePerCell:0} m), L lights, Space dismount. Range: {fuelMeters:0} m.");
        }

        private Vector3 BumperPosition() => transform.position + transform.forward * 2.15f + Vector3.up * 0.55f;

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
                vehicleLocalCenter = new Vector3(0f, 0.8f, 0f);
                vehicleLocalHalfExtents = new Vector3(1.7f, 0.7f, 2.1f);
                return;
            }
            Vector3 size = max - min;
            vehicleLocalCenter = (min + max) * 0.5f;
            vehicleLocalHalfExtents = new Vector3(Mathf.Max(0.1f, size.x * 0.47f), Mathf.Max(0.1f, size.y * 0.46f), Mathf.Max(0.1f, size.z * 0.47f));
        }

        private Vector3 ResolveVehicleMotion(Vector3 desiredPosition, Quaternion desiredRotation)
        {
            Vector3 movement = desiredPosition - transform.position;
            movement.y = 0f;
            float distance = movement.magnitude;
            if (distance <= 0.001f) return desiredPosition;

            Vector3 direction = movement / distance;
            Vector3 center = transform.TransformPoint(vehicleLocalCenter);
            int count = Physics.BoxCastNonAlloc(center, VehicleHalfExtents(), direction, vehicleBlockHits,
                transform.rotation, distance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = distance;
            RaycastHit nearestHit = default;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = vehicleBlockHits[i];
                if (!IsVehicleBlocker(hit.collider)) continue;
                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    nearestHit = hit;
                }
            }
            Array.Clear(vehicleBlockHits, 0, count);

            if (nearest < distance)
            {
                float impactSpeed = distance / Mathf.Max(0.0001f, Time.deltaTime);
                if (impactSpeed >= RamMinSpeed)
                    ApplyImpactImpulse(nearestHit.collider, direction, nearestHit.point, impactSpeed);
                desiredPosition = transform.position + direction * Mathf.Max(0f, nearest - 0.04f);
            }
            return desiredPosition;
        }

        /// <summary>Transfers a small, capped portion of a fast bumper impact to dynamic physics props.</summary>
        private void ApplyImpactImpulse(Collider collider, Vector3 direction, Vector3 point, float speed)
        {
            if (collider == null) return;
            Rigidbody body = collider.attachedRigidbody;
            if (body == null || body.isKinematic || !body.gameObject.activeInHierarchy) return;
            if (body.transform == transform || body.transform.IsChildOf(transform)) return;
            if (rider != null && (body.transform == rider.transform || body.transform.IsChildOf(rider.transform))) return;
            if (impactReadyAt.TryGetValue(body, out float readyAt) && Time.time < readyAt) return;

            Vector3 flatDirection = direction;
            flatDirection.y = 0f;
            if (flatDirection.sqrMagnitude < 0.0001f) return;
            impactReadyAt[body] = Time.time + 0.65f;

            // Cap the velocity change so light clutter moves visibly while heavy props remain controllable.
            float impulse = Mathf.Clamp(speed * body.mass * 0.08f, 0.5f, Mathf.Max(0.5f, body.mass * 5f));
            Vector3 impulseDirection = (flatDirection.normalized + Vector3.up * 0.08f).normalized;
            body.AddForceAtPosition(impulseDirection * impulse, point, ForceMode.Impulse);
        }

        private bool RotationHasClearance(Vector3 rootPosition, Quaternion rotation)
        {
            Vector3 center = rootPosition + rotation * Vector3.Scale(vehicleLocalCenter, transform.lossyScale);
            int count = Physics.OverlapBoxNonAlloc(center, VehicleHalfExtents(), vehicleOverlapHits, rotation,
                ~0, QueryTriggerInteraction.Ignore);
            bool clear = true;
            for (int i = 0; i < count; i++)
            {
                if (!IsVehicleBlocker(vehicleOverlapHits[i])) continue;
                clear = false;
                break;
            }
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

        private void TryRam(Vector3 from, Vector3 to, float speed)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            int count = distance > 0.001f
                ? Physics.OverlapCapsuleNonAlloc(from, to, 0.72f, ramHits, ~0, QueryTriggerInteraction.Ignore)
                : Physics.OverlapSphereNonAlloc(to, 0.72f, ramHits, ~0, QueryTriggerInteraction.Ignore);
            float damage = Mathf.Clamp(20f + speed * 3.2f, 35f, 95f);

            for (int i = 0; i < count; i++)
            {
                Collider hit = ramHits[i];
                if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

                EnemyAI enemy = hit.GetComponentInParent<EnemyAI>();
                if (enemy != null && !enemy.IsDead)
                {
                    if (!CanRam(enemy)) continue;
                    enemy.TakeDamage(damage, transform.position);
                    continue;
                }

                WorldWildlife animal = hit.GetComponentInParent<WorldWildlife>();
                if (animal != null && !animal.IsDead)
                {
                    if (!CanRam(animal)) continue;
                    GameMessageBus.Post(animal.TakeHit(damage, transform.position));
                    continue;
                }

                PlacedPiece piece = hit.GetComponentInParent<PlacedPiece>();
                StructureHealth health = piece != null ? piece.GetComponent<StructureHealth>() : null;
                if (health != null && CanRam(health)) health.TakeDamage(damage * 0.6f);
            }
            Array.Clear(ramHits, 0, count);
        }

        private bool CanRam(UnityEngine.Object target)
        {
            if (ramReadyAt.TryGetValue(target, out float readyAt) && Time.time < readyAt) return false;
            ramReadyAt[target] = Time.time + RamCooldown;
            return true;
        }

        private void PruneRamCooldowns()
        {
            nextRamCleanup = Time.time + 5f;
            var expired = new List<UnityEngine.Object>();
            foreach (KeyValuePair<UnityEngine.Object, float> entry in ramReadyAt)
                if (entry.Value <= Time.time) expired.Add(entry.Key);
            foreach (UnityEngine.Object target in expired) ramReadyAt.Remove(target);

            var expiredBodies = new List<Rigidbody>();
            foreach (KeyValuePair<Rigidbody, float> entry in impactReadyAt)
                if (entry.Key == null || entry.Value <= Time.time) expiredBodies.Add(entry.Key);
            foreach (Rigidbody body in expiredBodies) impactReadyAt.Remove(body);
        }

        private void Refuel()
        {
            if (rider == null) return;
            Inventory inventory = rider.GetComponent<Inventory>();
            if (inventory == null)
            {
                GameMessageBus.Post("Cannot access the player's inventory.");
                return;
            }
            if (TankRange - fuelMeters + 0.001f < FuelRangePerCan)
            {
                GameMessageBus.Post($"Need at least {FuelRangePerCan:0} m tank space before using another fuel can.");
                return;
            }
            if (!inventory.TryConsume("fuel", 1)) { GameMessageBus.Post("Need a fuel can to refuel the buggy."); return; }
            fuelMeters += FuelRangePerCan;
            if (fuelMeters > 140f) lowFuelWarningSent = false;
            if (fuelMeters > 0f) emptyFuelWarningSent = false;
            GameMessageBus.Post($"Buggy refuelled. Range: {fuelMeters:0} m.");
        }

        private void ChargeBattery()
        {
            Inventory inventory = rider != null ? rider.GetComponent<Inventory>() : null;
            if (inventory == null) { GameMessageBus.Post("Cannot access the player's inventory."); return; }
            if (TankRange - fuelMeters + 0.001f < BatteryRangePerCell)
            {
                GameMessageBus.Post($"Need at least {BatteryRangePerCell:0} m tank space before using an energy cell.");
                return;
            }
            if (!inventory.TryConsume("energy_cell", 1)) { GameMessageBus.Post("Need an energy cell to recharge the buggy."); return; }
            fuelMeters += BatteryRangePerCell;
            if (fuelMeters > 140f) lowFuelWarningSent = false;
            if (fuelMeters > 0f) emptyFuelWarningSent = false;
            GameMessageBus.Post($"Buggy battery charged. Range: {fuelMeters:0} m.");
        }

        private void AlertNearby()
        {
            const float noiseRadius = 40f;
            foreach (SpeciesBrain brain in SpeciesBrain.All)
                if (brain != null && brain.Enemy != null && !brain.Enemy.IsDead && (brain.transform.position - transform.position).sqrMagnitude <= noiseRadius * noiseRadius)
                    brain.Alert(transform.position);
            RaidDirector director = RaidDirector.Active;
            if (director != null)
                foreach (RaidAgent agent in director.Actors)
                    if (agent != null) agent.HearNoise(transform.position, noiseRadius);
        }

        private void CreateHeadlights()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject go = new GameObject(side < 0 ? "Headlight_L" : "Headlight_R");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(side * .72f, .16f, 2.05f);
                go.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
                Light lamp = go.AddComponent<Light>();
                lamp.type = LightType.Spot; lamp.range = 38f; lamp.spotAngle = 58f; lamp.intensity = 3.2f;
                lamp.color = new Color(1f, .88f, .68f); lamp.shadows = LightShadows.None;
                headlights.Add(lamp);
            }
        }

        private void ApplyHeadlights() { foreach (Light lamp in headlights) if (lamp != null) lamp.enabled = lightsOn; }

        private float TrunkWeight()
        {
            float total = 0f;
            foreach (var stack in contents) total += Encumbrance.WeightOf(stack.Key) * stack.Value;
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
            var saved = new SavedBuggy { fuelMeters = fuelMeters, headlights = lightsOn };
            foreach (var stack in contents) saved.trunk.Add(new SavedStack { id = stack.Key, count = stack.Value });
            saved.trunk.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return JsonUtility.ToJson(saved);
        }

        public void RestorePieceState(string state)
        {
            contents.Clear(); fuelMeters = 0f; lightsOn = false;
            lowFuelWarningSent = false; emptyFuelWarningSent = false;
            if (!string.IsNullOrEmpty(state))
            {
                try
                {
                    SavedBuggy saved = JsonUtility.FromJson<SavedBuggy>(state);
                    if (saved != null)
                    {
                        fuelMeters = Mathf.Clamp(saved.fuelMeters, 0f, TankRange);
                        lightsOn = saved.headlights;
                        if (saved.trunk != null) foreach (SavedStack stack in saved.trunk) if (stack != null) Add(stack.id, stack.count);
                    }
                }
                catch (Exception error) { Debug.LogWarning($"[RideableScoutBuggy] Could not restore state: {error.Message}"); }
            }
            ApplyHeadlights();
        }

        private void Dismount()
        {
            mounted = false;
            ramSweepReady = false;
            ramReadyAt.Clear();
            impactReadyAt.Clear();
            if (rider != null)
            {
                rider.walkSpeed = oldWalk; rider.sprintSpeed = oldSprint;
                rider.SetBicycleMounted(false);
                rider = null;
            }
            for (int i = 0; i < vehicleColliders.Length; i++) if (vehicleColliders[i] != null) vehicleColliders[i].enabled = colliderStates[i];
        }
    }
}
