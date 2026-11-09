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
        private float oldWalk, oldSprint;
        [Serializable] private sealed class SavedStack { public string id; public int count; }
        [Serializable] private sealed class SavedPanniers { public List<SavedStack> items = new List<SavedStack>(); }
        private readonly Dictionary<string, int> contents = new Dictionary<string, int>();
        private InputAction dismount, openPanniers;
        private Collider[] bikeColliders;
        private bool[] colliderStates;
        private bool mounted;

        public Vector3 StorePosition => transform.position;
        public IReadOnlyDictionary<string, int> Contents => contents;
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
            dismount = new InputAction("DismountBicycle", InputActionType.Button, "<Keyboard>/space");
            openPanniers = new InputAction("OpenBicyclePanniers", InputActionType.Button, "<Keyboard>/p");
        }

        private void OnEnable() { dismount?.Enable(); openPanniers?.Enable(); StorageRegistry.Register(this); }
        private void OnDisable() { StorageRegistry.Unregister(this); dismount?.Disable(); openPanniers?.Disable(); if (mounted) Dismount(); }
        private void OnDestroy() { dismount?.Dispose(); openPanniers?.Dispose(); }

        private void Update()
        {
            if (!mounted) return;
            if (rider == null || !rider.isActiveAndEnabled || !rider.GetComponent<CharacterController>().enabled) { Dismount(); return; }
            if (Time.timeScale == 0f) return;
            if (dismount.WasPressedThisFrame()) { Dismount(); return; }
            if (openPanniers.WasPressedThisFrame())
            {
                Inventory inventory = rider.GetComponent<Inventory>();
                if (inventory != null) StorageUI.Open(this, this, inventory);
                return;
            }
            Vector3 follow = rider.transform.position - rider.transform.forward * .8f;
            follow.y = rider.transform.position.y - .65f;
            transform.position = follow;
            transform.rotation = Quaternion.Euler(0f, rider.transform.eulerAngles.y, 0f);
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
            mounted = true;
            oldWalk = rider.walkSpeed; oldSprint = rider.sprintSpeed;
            rider.walkSpeed = walkSpeed; rider.sprintSpeed = sprintSpeed;
            rider.SetBicycleMounted(true);
            foreach (Collider c in bikeColliders) if (c != null) c.enabled = false;
            GameMessageBus.Post("Bicycle: WASD to ride, P to open panniers, Space to dismount.");
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
            for (int i = 0; i < bikeColliders.Length; i++) if (bikeColliders[i] != null) bikeColliders[i].enabled = colliderStates[i];
        }
    }
}
