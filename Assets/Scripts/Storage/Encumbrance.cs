using System.Collections.Generic;
using SheltreSim.Crafting;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Saving;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SheltreSim.Storage
{
    /// <summary>
    /// Soft carrying limit. An overloaded pack only takes away sprint, fading to walking speed at worst: walking never
    /// slows, nothing is dropped and food and medicine stay usable. Only sets FirstPersonController.sprintMultiplier,
    /// so the sprint upgrade in sprintSpeed keeps working. Derived from the pack, so nothing is saved. Creates itself
    /// after every scene load, no scene edit.
    /// </summary>
    public sealed class Encumbrance : MonoBehaviour, ISaveableSystem
    {
        public const float SoftLimit = 60f;
        public const float NoSprintAt = 100f;
        public const float WarnFraction = 0.8f;
        public const float RefreshSeconds = 0.25f;

        private static readonly Dictionary<string, float> Overrides = new Dictionary<string, float>
        {
            { "ammo_9mm", 0.02f }, { "ammo_rifle", 0.02f }, { "ammo_shell", 0.02f },
            { "stone", 2f }, { "iron_ore", 2f }, { "metal_plate", 2f }, { "steel_bar", 2.5f }, { "coal", 1f },
        };

        private static Encumbrance instance;
        private static bool hooked;

        private FirstPersonController player;
        private Inventory pack;
        private GameObject hud;
        private Text label;
        private float timer;
        private int backpackTier;

        [System.Serializable]
        private sealed class SavedBackpack { public int tier; }

        public static Encumbrance Active => instance;
        public const int MaxBackpackTier = 3;
        public int BackpackTier => backpackTier;
        public float CarryCapacity => SoftLimit + backpackTier * 20f;
        public bool CanUpgradeBackpack => backpackTier < MaxBackpackTier;
        public string SaveKey => "player_backpack";
        public float CurrentLoad { get; private set; }
        public string LabelText { get; private set; } = string.Empty;
        public bool LabelVisible => hud != null && hud.activeSelf;
        public Canvas HudCanvas => hud != null ? hud.GetComponent<Canvas>() : null;

        // ---------- Weights ----------

        /// <summary>Kilograms per unit. Unknown ids weigh 1 kg.</summary>
        public static float WeightOf(string id)
        {
            if (id != null && Overrides.TryGetValue(id, out float weight)) return weight;
            ItemSpec spec = ItemCatalog.Spec(id);
            if (spec == null) return 1f;
            switch (spec.category)
            {
                case ItemCategory.Raw: return 1f;
                case ItemCategory.Refined: return 0.5f;
                case ItemCategory.Food: return 0.4f;
                case ItemCategory.Medicine: return 0.2f;
                case ItemCategory.Tool: return 3f;
                case ItemCategory.Weapon: return 3f;
                case ItemCategory.Armor: return 8f;
                default: return 1f;
            }
        }

        public static float PackWeight(Inventory inventory)
        {
            if (inventory == null) return 0f;
            double total = 0d;
            foreach (var stack in inventory.GetAllCounts())
                if (stack.Value > 0) total += (double)WeightOf(stack.Key) * stack.Value;
            return (float)System.Math.Min(total, float.MaxValue);
        }

        /// <summary>Exactly 1 at or under the limit, down to the walk/sprint ratio by NoSprintAt. Never zero.</summary>
        public static float SprintFactor(float load, float walkSpeed, float sprintSpeed)
            => SprintFactor(load, walkSpeed, sprintSpeed, SoftLimit, NoSprintAt);

        public static float SprintFactor(float load, float walkSpeed, float sprintSpeed, float softLimit, float noSprintAt)
        {
            if (load <= softLimit || sprintSpeed <= 0f) return 1f;
            float worst = Mathf.Clamp(walkSpeed / sprintSpeed, 0.05f, 1f);
            return Mathf.Lerp(1f, worst, Mathf.InverseLerp(softLimit, Mathf.Max(softLimit + 0.01f, noSprintAt), load));
        }

        /// <summary>The full readout used by the storage screen.</summary>
        public static string Describe(float load)
            => Describe(load, SoftLimit);

        public static string Describe(float load, float capacity)
        {
            if (load >= capacity + 40f) return $"Overloaded: no sprint ({load:0.#}/{capacity:0} kg)";
            if (load > capacity) return $"Overloaded: slower sprint ({load:0.#}/{capacity:0} kg)";
            return $"Load {load:0.#}/{capacity:0} kg";
        }

        // ---------- Lifecycle ----------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            hooked = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneLoaded += (scene, mode) => Ensure();
            }
            Ensure();
        }

        private static void Ensure()
        {
            if (instance == null) new GameObject("Encumbrance").AddComponent<Encumbrance>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            SaveableSystems.Register(this);
            BuildHud();
        }

        private void OnDestroy()
        {
            SaveableSystems.Unregister(this);
            if (instance == this) instance = null;
        }

        // Real time, so taking things out of a crate while the game is paused shows up at once behind the screen.
        private void Update()
        {
            timer += Time.unscaledDeltaTime;
            if (timer < RefreshSeconds) return;
            timer = 0f;
            RefreshNow();
        }

        public void RefreshNow()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<FirstPersonController>();
                pack = player != null ? player.GetComponent<Inventory>() : null;
            }
            if (player == null) { ShowLabel(string.Empty, Color.white); return; }

            CurrentLoad = PackWeight(pack);
            player.sprintMultiplier = SprintFactor(CurrentLoad, player.walkSpeed, player.sprintSpeed, CarryCapacity, CarryCapacity + 40f);

            if (CurrentLoad >= CarryCapacity + 40f) ShowLabel(Describe(CurrentLoad, CarryCapacity), GameUI.Danger);
            else if (CurrentLoad > CarryCapacity) ShowLabel(Describe(CurrentLoad, CarryCapacity), GameUI.Warning);
            else if (CurrentLoad > CarryCapacity * WarnFraction) ShowLabel(Describe(CurrentLoad, CarryCapacity), GameUI.Muted);
            else ShowLabel(string.Empty, Color.white);
        }

        public bool TryInstallBackpackUpgrade(Inventory inventory, string itemId)
        {
            if (!CanUpgradeBackpack || inventory == null || string.IsNullOrEmpty(itemId) ||
                !(ItemCatalog.Spec(itemId)?.backpackUpgrade ?? false) || !inventory.TryConsume(itemId, 1)) return false;
            backpackTier++;
            RefreshNow();
            return true;
        }

        public string CaptureState() => JsonUtility.ToJson(new SavedBackpack { tier = backpackTier });

        public void RestoreState(string state)
        {
            int tier = 0;
            if (!string.IsNullOrEmpty(state))
            {
                try { tier = JsonUtility.FromJson<SavedBackpack>(state)?.tier ?? 0; }
                catch { tier = 0; }
            }
            backpackTier = Mathf.Clamp(tier, 0, MaxBackpackTier);
            RefreshNow();
        }

        // ---------- HUD ----------

        private void BuildHud()
        {
            Canvas canvas = GameUI.CreateCanvas(transform, "Encumbrance_Canvas", 1, false);
            hud = canvas.gameObject;
            // Directly under SurvivalHUD's objective card in the top right corner.
            label = GameUI.Label(canvas.transform, "LoadLabel", string.Empty, Vector2.one, Vector2.one,
                new Vector2(-24f, -274f), new Vector2(380f, 30f), 19, GameUI.Muted, TextAnchor.MiddleRight, true);
            Shadow shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1f, -1f);
            hud.SetActive(false);
        }

        private void ShowLabel(string text, Color color)
        {
            LabelText = text;
            bool visible = text.Length > 0;
            if (hud == null) return;
            if (hud.activeSelf != visible) hud.SetActive(visible);
            if (!visible) return;
            label.text = text;
            label.color = color;
        }
    }
}
