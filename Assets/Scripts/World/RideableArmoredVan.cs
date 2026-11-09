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
    /// <summary>Heavy fuel-driven vehicle with a large cargo hold and a slow, deliberate ram.</summary>
    public sealed class RideableArmoredVan : MonoBehaviour, IInteractable, IItemStore, IPieceState
    {
        public const float CargoCapacityKg = 300f;
        public const float FuelRangePerCan = 700f;
        public const float BatteryRangePerCell = 500f;
        public const float TankRange = FuelRangePerCan * 8f;
        public const float TurretRange = 70f;
        public const float TurretDamage = 45f;
        public const float TurretShotInterval = 0.75f;
        public float walkSpeed = 14f;
        public float sprintSpeed = 18f;
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
        private readonly List<Light> headlights = new List<Light>();
        private readonly Collider[] ramHits = new Collider[32];
        private FirstPersonController rider;
        private float oldWalk, oldSprint, fuelMeters, nextNoise, nextCooldownCleanup, nextTurretShot, nextDryTurretMessage;
        private Vector3 lastRiderPosition, lastBumperPosition;
        private InputAction dismount, cargoAction, refuelAction, batteryAction, lightAction, turretAction;
        private Collider[] vehicleColliders;
        private bool[] colliderStates;
        private bool mounted, lightsOn;

        public Vector3 StorePosition => transform.position;
        public IReadOnlyDictionary<string, int> Contents => contents;
        public float FuelRemainingMeters => fuelMeters;
        public int GetCount(string id) => id != null && contents.TryGetValue(id, out int count) ? count : 0;
        public int GetAddRoom(string id) => Mathf.Max(0, Mathf.FloorToInt((CargoCapacityKg - CargoWeight()) / Mathf.Max(.01f, Encumbrance.WeightOf(id))));

        private void Awake()
        {
            vehicleColliders = GetComponentsInChildren<Collider>(true);
            colliderStates = new bool[vehicleColliders.Length];
            for (int i = 0; i < vehicleColliders.Length; i++) colliderStates[i] = vehicleColliders[i].enabled;
            dismount = new InputAction("DismountArmoredVan", InputActionType.Button, "<Keyboard>/space");
            cargoAction = new InputAction("OpenVanCargo", InputActionType.Button, "<Keyboard>/p");
            refuelAction = new InputAction("RefuelVan", InputActionType.Button, "<Keyboard>/h");
            batteryAction = new InputAction("ChargeVan", InputActionType.Button, "<Keyboard>/j");
            lightAction = new InputAction("ToggleVanLights", InputActionType.Button, "<Keyboard>/l");
            turretAction = new InputAction("FireVanTurret", InputActionType.Button, "<Keyboard>/t");
            CreateHeadlights();
            ApplyHeadlights();
        }

        private void OnEnable()
        {
            dismount?.Enable(); cargoAction?.Enable(); refuelAction?.Enable(); batteryAction?.Enable(); lightAction?.Enable(); turretAction?.Enable();
            StorageRegistry.Register(this);
        }

        private void OnDisable()
        {
            StorageRegistry.Unregister(this);
            dismount?.Disable(); cargoAction?.Disable(); refuelAction?.Disable(); batteryAction?.Disable(); lightAction?.Disable(); turretAction?.Disable();
            if (mounted) Dismount();
        }

        private void OnDestroy()
        {
            dismount?.Dispose(); cargoAction?.Dispose(); refuelAction?.Dispose(); batteryAction?.Dispose(); lightAction?.Dispose(); turretAction?.Dispose();
        }

        private void Update()
        {
            if (!mounted) return;
            CharacterController riderBody = rider != null ? rider.GetComponent<CharacterController>() : null;
            if (rider == null || !rider.isActiveAndEnabled || riderBody == null || !riderBody.enabled) { Dismount(); return; }
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

            Vector3 position = rider.transform.position;
            Vector3 previousBumper = lastBumperPosition;
            Vector3 movement = position - lastRiderPosition;
            float travelled = Vector3.Distance(position, lastRiderPosition);
            float speed = Time.deltaTime > 0f ? travelled / Time.deltaTime : 0f;
            lastRiderPosition = position;
            fuelMeters = Mathf.Max(0f, fuelMeters - travelled);
            rider.walkSpeed = fuelMeters > 0f ? walkSpeed : 0f;
            rider.sprintSpeed = fuelMeters > 0f ? sprintSpeed : 0f;

            Vector3 follow = position - rider.transform.forward * 3f;
            follow.y = position.y - 1f;
            transform.position = follow;
            transform.rotation = Quaternion.Euler(0f, rider.transform.eulerAngles.y, 0f);
            Vector3 bumper = transform.position + transform.forward * 3.2f + Vector3.up * 0.55f;
            if (fuelMeters > 0f && speed >= minimumRamSpeed && Vector3.Dot(movement, rider.transform.forward) > 0f)
                TryRam(previousBumper, bumper, speed);
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
            mounted = true;
            oldWalk = rider.walkSpeed; oldSprint = rider.sprintSpeed;
            lastRiderPosition = rider.transform.position;
            rider.walkSpeed = fuelMeters > 0f ? walkSpeed : 0f;
            rider.sprintSpeed = fuelMeters > 0f ? sprintSpeed : 0f;
            rider.SetBicycleMounted(true);
            foreach (Collider collider in vehicleColliders) if (collider != null) collider.enabled = false;
            transform.position = rider.transform.position - rider.transform.forward * 3f;
            transform.rotation = Quaternion.Euler(0f, rider.transform.eulerAngles.y, 0f);
            lastBumperPosition = transform.position + transform.forward * 3.2f + Vector3.up * 0.55f;
            GameMessageBus.Post($"Van controls: P cargo, H fuel (+{FuelRangePerCan:0} m), J energy cell (+{BatteryRangePerCell:0} m), L lights, T turret (1 rifle round), Space dismount. Range: {fuelMeters:0} m. Ram at speed to damage enemies and structures.");
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
                GameMessageBus.Post($"Need at least {FuelRangePerCan:0} m tank space before using another fuel can.");
                return;
            }
            if (!pack.TryConsume("fuel", 1)) { GameMessageBus.Post("Need a fuel can to refuel the van."); return; }
            fuelMeters += FuelRangePerCan;
            GameMessageBus.Post($"Van refuelled. Range: {fuelMeters:0} m.");
        }

        private void ChargeBattery()
        {
            Inventory pack = rider != null ? rider.GetComponent<Inventory>() : null;
            if (pack == null) { GameMessageBus.Post("Cannot access the player's inventory."); return; }
            if (TankRange - fuelMeters + 0.001f < BatteryRangePerCell)
            {
                GameMessageBus.Post($"Need at least {BatteryRangePerCell:0} m tank space before using an energy cell.");
                return;
            }
            if (!pack.TryConsume("energy_cell", 1)) { GameMessageBus.Post("Need an energy cell to recharge the van."); return; }
            fuelMeters += BatteryRangePerCell;
            GameMessageBus.Post($"Van battery charged. Range: {fuelMeters:0} m.");
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
            mounted = false;
            if (rider != null)
            {
                rider.walkSpeed = oldWalk; rider.sprintSpeed = oldSprint;
                rider.SetBicycleMounted(false);
                rider = null;
            }
            for (int i = 0; i < vehicleColliders.Length; i++)
                if (vehicleColliders[i] != null) vehicleColliders[i].enabled = colliderStates[i];
        }
    }
}
