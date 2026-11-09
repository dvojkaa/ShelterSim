using System;
using System.Collections.Generic;
using SheltreSim.Building;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Saving;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Storage
{
    /// <summary>
    /// A working storage crate. Each instance owns its contents, the starting bunker crate and built crates are the
    /// same prefab, and there is no capacity limit. A crate from an old save has no state, so it comes back empty.
    /// RestorePieceState runs before Awake on an inactive instance, so contents are a plain field and Awake is unused.
    /// </summary>
    public sealed class StorageCratePiece : MonoBehaviour, IItemStore, IInteractable, IPieceState
    {
        /// <summary>A crate that breaks hands its contents to another crate within this range.</summary>
        public const float SalvageRadius = 30f;

        [Serializable] public sealed class SavedStack { public string id; public int count; }
        [Serializable] public sealed class SavedCrate { public List<SavedStack> items = new List<SavedStack>(); }

        // Not serialized by Unity, so every Instantiate of the prefab starts with its own empty dictionary.
        private readonly Dictionary<string, int> contents = new Dictionary<string, int>();

        public Vector3 StorePosition => transform.position;
        public IReadOnlyDictionary<string, int> Contents => contents;
        public bool IsEmpty => contents.Count == 0;

        public long TotalItems
        {
            get
            {
                long total = 0;
                foreach (var stack in contents) total += stack.Value;
                return total;
            }
        }

        public string InteractPrompt
        {
            get
            {
                long total = TotalItems;
                return total == 1 ? "Press E to open the crate (1 item)" : $"Press E to open the crate ({total} items)";
            }
        }

        private void OnEnable()
        {
            StorageRegistry.Register(this);
            StructureHealth.Destroyed += OnStructureDestroyed;
        }

        private void OnDisable()
        {
            StorageRegistry.Unregister(this);
            StructureHealth.Destroyed -= OnStructureDestroyed;
        }

        public void Interact(GameObject interactor)
        {
            if (!isActiveAndEnabled || interactor == null) return;
            Inventory pack = interactor.GetComponentInParent<Inventory>();
            if (pack != null) StorageUI.Open(this, pack);
        }

        public int GetCount(string id) => id != null && contents.TryGetValue(id, out int count) ? count : 0;
        public int GetAddRoom(string id) => int.MaxValue - GetCount(id);

        /// <summary>Adds up to int.MaxValue in a stack. Callers moving items use StorageTransfer, which checks the room first.</summary>
        public void Add(string id, int amount)
        {
            if (string.IsNullOrWhiteSpace(id) || amount <= 0) return;
            contents[id] = (int)Math.Min(int.MaxValue, (long)GetCount(id) + amount);
        }

        /// <summary>All or nothing. A stack that reaches zero disappears.</summary>
        public bool TryTake(string id, int amount)
        {
            if (amount <= 0 || GetCount(id) < amount) return false;
            int left = contents[id] - amount;
            if (left > 0) contents[id] = left; else contents.Remove(id);
            return true;
        }

        /// <summary>Empties the crate into a pack, for anything that removes a crate on purpose (dismantling). Returns the amount moved.</summary>
        public long TakeAllInto(Inventory pack) => StorageTransfer.TakeAll(pack, this);

        // ---------- Saving ----------

        public string CapturePieceState()
        {
            if (contents.Count == 0) return null;
            var saved = new SavedCrate();
            foreach (var stack in contents) saved.items.Add(new SavedStack { id = stack.Key, count = stack.Value });
            saved.items.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return JsonUtility.ToJson(saved);
        }

        /// <summary>Replaces the contents, so restoring twice can't multiply them.</summary>
        public void RestorePieceState(string state)
        {
            contents.Clear();
            if (string.IsNullOrEmpty(state)) return;
            SavedCrate saved;
            try { saved = JsonUtility.FromJson<SavedCrate>(state); }
            catch (ArgumentException error)
            {
                Debug.LogWarning($"[StorageCratePiece] Unreadable crate state, the crate stays empty: {error.Message}");
                return;
            }
            if (saved == null || saved.items == null) return;
            foreach (SavedStack stack in saved.items)
                if (stack != null) Add(stack.id, stack.count);
        }

        // ---------- Breaking ----------

        // Only a piece killed by damage comes through here. A load also disables and destroys crates, and must not move anything.
        private void OnStructureDestroyed(StructureHealth health)
        {
            if (health == null || health.gameObject != gameObject || contents.Count == 0) return;
            IItemStore nearest = null;
            float best = SalvageRadius * SalvageRadius;
            foreach (IItemStore store in StorageRegistry.All)
            {
                if (ReferenceEquals(store, this)) continue;
                float distance = (store.StorePosition - transform.position).sqrMagnitude;
                if (distance <= best) { best = distance; nearest = store; }
            }
            if (nearest == null)
            {
                GameMessageBus.Post($"The crate's contents were lost: {TotalItems} items.");
                contents.Clear();
                return;
            }
            foreach (var stack in new List<KeyValuePair<string, int>>(contents))
            {
                int room = int.MaxValue - nearest.GetCount(stack.Key);
                nearest.Add(stack.Key, Mathf.Min(stack.Value, room));
            }
            contents.Clear();
            GameMessageBus.Post("The crate's contents were saved in a nearby crate.");
        }
    }
}
