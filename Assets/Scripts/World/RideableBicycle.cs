using System;
using System.Collections.Generic;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Saving;
using SheltreSim.Storage;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SheltreSim.World
{
    /// <summary>Early-game pedal bicycle. Mount with E, open panniers with P and dismount with Space.</summary>
    public sealed class RideableBicycle : MonoBehaviour, IInteractable, IItemStore, IPieceState
    {
        public const float PannierCapacityKg = 35f;
        public float walkSpeed = 10.5f;
        public float sprintSpeed = 13f;
        public string InteractPrompt => "Press E to ride bicycle";

        private FirstPersonController rider;
        private VehicleMountView mountView;
        private float oldWalk, oldSprint;
        private float driveScale, currentSpeed;
        private Vector3 lastRiderPosition;
        [Serializable] private sealed class SavedStack { public string id; public int count; }
        [Serializable] private sealed class SavedPanniers { public List<SavedStack> items = new List<SavedStack>(); }
        private readonly Dictionary<string, int> contents = new Dictionary<string, int>();
        private InputAction dismount, openPanniers;
        private Collider[] bikeColliders;
        private bool[] colliderStates;
        private Transform[] wheels;
        private Quaternion[] wheelRestRotations;
        private float wheelRotation;
        private bool mounted;

        // The prefab root is the footprint centre: the floor is half the footprint height below it, so the root rides that far ABOVE the rider's feet.
        private const float RootHeight = 0.65f; // footprint 1.3 m high
        public Vector3 StorePosition => transform.position;
        public IReadOnlyDictionary<string, int> Contents => contents;
        public bool IsMounted => mounted;
        public float Speed => currentSpeed;
        public float Throttle01 => rider != null && mounted ? Mathf.Clamp01(rider.MoveInput.magnitude) : 0f;
        public float FuelFraction01 => 1f;
        public int GetCount(string id) => id != null && contents.TryGetValue(id, out int count) ? count : 0;
        public int GetAddRoom(string id) => Mathf.Max(0, Mathf.FloorToInt((PannierCapacityKg - ContentsWeight()) / Mathf.Max(.01f, Encumbrance.WeightOf(id))));

        private float ContentsWeight()
        {
            float total = 0f;
            foreach (var stack in contents) total += Encumbrance.WeightOf(stack.Key) * stack.Value;
            return total;
        }

        private void Awake()
        {
            bikeColliders = GetComponentsInChildren<Collider>(true);
            colliderStates = new bool[bikeColliders.Length];
            for (int i = 0; i < bikeColliders.Length; i++) colliderStates[i] = bikeColliders[i].enabled;
            VehicleTerrain.CacheWheels(transform, out wheels, out wheelRestRotations);
            dismount = new InputAction("DismountBicycle", InputActionType.Button, "<Keyboard>/space");
            openPanniers = new InputAction("OpenBicyclePanniers", InputActionType.Button, "<Keyboard>/p");
        }

        private void OnEnable() { dismount?.Enable(); openPanniers?.Enable(); StorageRegistry.Register(this); }
        private void OnDisable()
        {
            StorageRegistry.Unregister(this);
            dismount?.Disable();
            openPanniers?.Disable();
            if (mounted)
            {
                mountView?.ForceDismount(transform);
                FinishDismount();
            }
        }
        private void OnDestroy() { dismount?.Dispose(); openPanniers?.Dispose(); }

        private void Update()
        {
            if (!mounted) return;
            if (rider == null || !rider.isActiveAndEnabled || rider.GetComponent<CharacterController>() == null || !rider.GetComponent<CharacterController>().enabled)
            {
                mountView?.ForceDismount(transform);
                FinishDismount();
                return;
            }
            if (Time.timeScale == 0f) return;
            if (mountView != null && mountView.IsTransitioning) return;
            if (dismount.WasPressedThisFrame()) { Dismount(); return; }
            if (openPanniers.WasPressedThisFrame())
            {
                Inventory inventory = rider.GetComponent<Inventory>();
                if (inventory != null) StorageUI.Open(this, this, inventory);
                return;
            }
            Vector3 position = rider.transform.position;
            Vector3 movement = position - lastRiderPosition;
            Quaternion yaw = Quaternion.Euler(0f, rider.transform.eulerAngles.y, 0f);
            VehicleTerrain.SamplePose(position - yaw * Vector3.forward * .8f, yaw, RootHeight, 1.4f,
                VehicleTerrain.CurrentSeed, out Vector3 follow, out Quaternion groundRotation, out float grade, out _);
            groundRotation = VehicleTerrain.SteerPose(transform.rotation, groundRotation, 210f, Time.deltaTime);
            currentSpeed = Time.deltaTime > 0f ? Vector3.Dot(movement / Time.deltaTime, yaw * Vector3.forward) : 0f;
            VehicleTerrain.SpinWheels(wheels, wheelRestRotations, ref wheelRotation, currentSpeed, .33f, Time.deltaTime);
            lastRiderPosition = position;
            float gradeScale = VehicleTerrain.GradeSpeedMultiplier(grade, rider.MoveInput.y);
            float targetScale = rider.MoveInput.sqrMagnitude > .001f ? gradeScale : 0f;
            driveScale = VehicleTerrain.ApproachDriveScale(driveScale, targetScale, Time.deltaTime, 2.2f, 2.8f);
            rider.walkSpeed = walkSpeed * driveScale;
            rider.sprintSpeed = sprintSpeed * driveScale;
            transform.SetPositionAndRotation(follow, groundRotation);
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
            var saved = new SavedPanniers();
            foreach (var stack in contents) saved.items.Add(new SavedStack { id = stack.Key, count = stack.Value });
            saved.items.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return saved.items.Count == 0 ? null : JsonUtility.ToJson(saved);
        }

        public void RestorePieceState(string state)
        {
            contents.Clear();
            if (string.IsNullOrEmpty(state)) return;
            try
            {
                SavedPanniers saved = JsonUtility.FromJson<SavedPanniers>(state);
                if (saved?.items == null) return;
                foreach (SavedStack stack in saved.items) if (stack != null) Add(stack.id, stack.count);
            }
            catch (Exception error) { Debug.LogWarning($"[RideableBicycle] Could not read panniers: {error.Message}"); }
        }

        public void Interact(GameObject interactor)
        {
            if (mounted) return;
            rider = interactor.GetComponentInParent<FirstPersonController>();
            if (rider == null) return;
            mountView = VehicleMountView.For(rider);
            if (mountView == null || mountView.IsBusy) { rider = null; mountView = null; return; }
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;
            Vector3 riderSeatPosition = transform.position - Vector3.up * RootHeight + flatForward * .8f;
            Quaternion riderSeatRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            if (mountView == null || !mountView.BeginMount(transform, riderSeatPosition, riderSeatRotation))
            {
                rider = null;
                mountView = null;
                GameMessageBus.Post("No clear space to mount the bicycle.");
                return;
            }
            mounted = true;
            oldWalk = rider.walkSpeed; oldSprint = rider.sprintSpeed;
            lastRiderPosition = rider.transform.position;
            driveScale = 0f;
            currentSpeed = 0f;
            rider.walkSpeed = 0f; rider.sprintSpeed = 0f;
            rider.SetBicycleMounted(true);
            foreach (Collider c in bikeColliders) if (c != null) c.enabled = false;
            GameMessageBus.Post("Bicycle: WASD to ride, P to open panniers, Space to dismount.");
        }

        private void Dismount()
        {
            if (!mounted) return;
            if (mountView != null && !mountView.BeginDismount(transform))
            {
                GameMessageBus.Post("No capsule-clear space to leave the bicycle.");
                return;
            }
            FinishDismount();
        }

        private void FinishDismount()
        {
            mounted = false;
            currentSpeed = driveScale = 0f;
            if (rider != null)
            {
                rider.walkSpeed = oldWalk; rider.sprintSpeed = oldSprint;
                rider.SetBicycleMounted(false);
                rider = null;
            }
            mountView = null;
            for (int i = 0; i < bikeColliders.Length; i++) if (bikeColliders[i] != null) bikeColliders[i].enabled = colliderStates[i];
        }
    }
}
