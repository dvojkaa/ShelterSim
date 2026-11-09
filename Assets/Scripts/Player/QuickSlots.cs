using System.Text;
using SheltreSim.Audio;
using SheltreSim.Crafting;
using SheltreSim.Items;
using SheltreSim.Saving;
using SheltreSim.Survival;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SheltreSim.Player
{
    /// <summary>
    /// Keys 3, 4 and 5 use one consumable each without opening the inventory. A slot the player has not
    /// assigned shows the first owned item of its kind (3 medicine, 4 food, 5 drink, in catalog order).
    /// Uses the same path as InventoryUI's Use button. Added to the player by FirstPersonController. Saved as "quickslots".
    /// </summary>
    public class QuickSlots : MonoBehaviour, ISaveableSystem
    {
        public const int FirstKey = 3;
        public const int Count = 3;

        public static QuickSlots Active { get; private set; }

        public string SaveKey => "quickslots";

        private readonly string[] assigned = new string[Count];
        private readonly InputAction[] keys = new InputAction[Count];
        private SurvivalStats stats;
        private Inventory inventory;
        private float nextUseTime;
        private readonly StringBuilder sb = new StringBuilder();

        [System.Serializable]
        private class State
        {
            public string[] ids = new string[0];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active = null;

        // ---------- Public API (for InventoryUI and the HUD) ----------

        /// <summary>Puts an item on key 3, 4 or 5. Null or empty id clears the slot back to its default. Returns false for a non-consumable.</summary>
        public static bool Assign(int slot, string id)
        {
            if (Active == null) return false;
            return Active.AssignInternal(slot, id);
        }

        /// <summary>
        /// One button per inventory row: moves the item to the next quick key (none, 3, 4, 5, none) and says so.
        /// Returns the key it is on now, or 0.
        /// </summary>
        public static int CycleSlot(string id)
        {
            if (Active == null || !IsQuickUsable(id)) return 0;
            int current = AssignedSlotOf(id);
            int nextKey = current == 0 ? FirstKey : current + 1;
            if (nextKey >= FirstKey + Count)
            {
                Active.AssignInternal(current, null);
                GameMessageBus.Post($"{ItemCatalog.NameOf(id)} removed from the quick keys.");
                return 0;
            }
            Active.AssignInternal(nextKey, id);
            GameMessageBus.Post($"{ItemCatalog.NameOf(id)} is on quick key {nextKey}.");
            return nextKey;
        }

        /// <summary>The key (3..5) this item was explicitly assigned to, or 0.</summary>
        public static int AssignedSlotOf(string id)
        {
            if (Active == null || string.IsNullOrEmpty(id)) return 0;
            for (int i = 0; i < Count; i++) if (Active.assigned[i] == id) return FirstKey + i;
            return 0;
        }

        /// <summary>The item key 3..5 would use right now (assigned or default), or null.</summary>
        public static string ItemOn(int slot)
        {
            if (Active == null) return null;
            int i = slot - FirstKey;
            if (i < 0 || i >= Count) return null;
            return Active.Resolve(i);
        }

        public static bool IsAssigned(int slot)
        {
            int i = slot - FirstKey;
            return Active != null && i >= 0 && i < Count && !string.IsNullOrEmpty(Active.assigned[i]);
        }

        public static int CountOf(string id) => Active != null && Active.inventory != null && !string.IsNullOrEmpty(id) ? Active.inventory.GetCount(id) : 0;

        public static bool IsQuickUsable(string id)
        {
            ItemSpec spec = ItemCatalog.Spec(id);
            return spec != null && spec.IsConsumable;
        }

        // ---------- Lifecycle ----------

        private void Awake()
        {
            Active = this;
            stats = GetComponent<SurvivalStats>();
            inventory = GetComponent<Inventory>();
            for (int i = 0; i < Count; i++)
            {
                keys[i] = new InputAction("QuickSlot" + (FirstKey + i), InputActionType.Button, "<Keyboard>/" + (FirstKey + i));
                keys[i].AddBinding("<Keyboard>/numpad" + (FirstKey + i));
            }
            SaveableSystems.Register(this);
        }

        private void OnEnable() { foreach (InputAction key in keys) key.Enable(); }
        private void OnDisable() { foreach (InputAction key in keys) key.Disable(); }

        private void OnDestroy()
        {
            SaveableSystems.Unregister(this);
            foreach (InputAction key in keys) key?.Dispose();
            if (Active == this) Active = null;
        }

        private void Update()
        {
            for (int i = 0; i < Count; i++)
            {
                if (!keys[i].WasPressedThisFrame()) continue;
                // Paused (any modal, including the inventory itself), dead, or a key held through a frame hitch.
                if (Time.timeScale <= 0f || ModalUI.IsOpen || stats == null || stats.IsDead || Time.unscaledTime < nextUseTime) return;
                nextUseTime = Time.unscaledTime + 0.25f;
                Use(i);
                return;
            }
        }

        // ---------- Internals ----------

        private bool AssignInternal(int slot, string id)
        {
            int index = slot - FirstKey;
            if (index < 0 || index >= Count) return false;
            if (string.IsNullOrEmpty(id))
            {
                assigned[index] = null;
                return true;
            }
            if (!IsQuickUsable(id)) return false;
            for (int i = 0; i < Count; i++) if (assigned[i] == id) assigned[i] = null;
            assigned[index] = id;
            return true;
        }

        private string Resolve(int index)
        {
            if (!string.IsNullOrEmpty(assigned[index])) return assigned[index];
            if (inventory == null) return null;
            foreach (ItemSpec spec in ItemCatalog.All)
            {
                bool kind = index == 0 ? spec.medicine : index == 1 ? spec.food && !spec.medicine : spec.drink && !spec.medicine;
                if (kind && inventory.GetCount(spec.id) > 0) return spec.id;
            }
            return null;
        }

        private void Use(int index)
        {
            int key = FirstKey + index;
            string id = Resolve(index);
            ItemSpec spec = ItemCatalog.Spec(id);
            if (spec == null)
            {
                Deny(index == 0 ? $"Quick key {key}: no medicine in your pack." : index == 1 ? $"Quick key {key}: no food in your pack." : $"Quick key {key}: nothing to drink in your pack.");
                return;
            }
            if (inventory == null || inventory.GetCount(id) < 1)
            {
                Deny($"No {spec.name} left.");
                return;
            }
            // Same rule as InventoryUI: never waste a consumable at a full vital.
            bool useful = (spec.hunger > 0f && stats.hunger < stats.maxHunger) || (spec.thirst > 0f && stats.thirst < stats.maxThirst) ||
                (spec.heal > 0f && stats.health < stats.maxHealth);
            if (!useful || stats.health <= 0f)
            {
                Deny(spec.heal > 0f && spec.hunger <= 0f && spec.thirst <= 0f ? "You are already at full health." : $"You don't need {spec.name} right now.");
                return;
            }
            if (!inventory.TryConsume(id, 1)) return;
            float h = stats.health, f = stats.hunger, w = stats.thirst;
            if (spec.hunger > 0f) stats.Eat(spec.hunger);
            if (spec.thirst > 0f) stats.Drink(spec.thirst);
            if (spec.heal > 0f) stats.Heal(spec.heal);
            // Eat/drink/heal sounds come from PlayerAudio, which watches the vitals.
            sb.Clear();
            sb.Append(spec.food ? "Ate " : spec.drink ? "Drank " : "Used ").Append(spec.name).Append(':');
            if (stats.hunger > f) sb.Append(" +").Append((stats.hunger - f).ToString("0")).Append(" food");
            if (stats.thirst > w) sb.Append(" +").Append((stats.thirst - w).ToString("0")).Append(" water");
            if (stats.health > h) sb.Append(" +").Append((stats.health - h).ToString("0")).Append(" health");
            GameMessageBus.Post(sb.Append('.').ToString());
        }

        private static void Deny(string message)
        {
            GameAudio.Play2D(Sfx.UiDeny, 0.6f);
            GameMessageBus.Post(message);
        }

        public string CaptureState()
        {
            var state = new State { ids = new string[Count] };
            for (int i = 0; i < Count; i++) state.ids[i] = assigned[i] ?? string.Empty;
            return JsonUtility.ToJson(state);
        }

        public void RestoreState(string json)
        {
            for (int i = 0; i < Count; i++) assigned[i] = null;
            if (string.IsNullOrEmpty(json)) return;
            State state;
            try { state = JsonUtility.FromJson<State>(json); }
            catch (System.Exception) { return; }
            if (state?.ids == null) return;
            for (int i = 0; i < Count && i < state.ids.Length; i++)
                if (IsQuickUsable(state.ids[i])) assigned[i] = state.ids[i];
        }
    }
}
