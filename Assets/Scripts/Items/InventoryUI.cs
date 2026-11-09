using System;
using System.Collections.Generic;
using System.Text;
using SheltreSim.Building;
using SheltreSim.Crafting;
using SheltreSim.Player;
using SheltreSim.Storage;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SheltreSim.Items
{
    public class InventoryInputActions : IDisposable
    {
        private readonly InputActionMap map = new InputActionMap("Inventory");
        public InputAction Toggle { get; }

        public InventoryInputActions()
        {
            Toggle = map.AddAction("Toggle", InputActionType.Button);
            Toggle.AddBinding("<Keyboard>/i");
            Toggle.AddBinding("<Keyboard>/tab");
        }

        public void Enable() => map.Enable();
        public void Disable() => map.Disable();
        public void Dispose() => map.Dispose();
    }

    public class InventoryUI : MonoBehaviour
    {
        public Inventory inventory;
        /// <summary>Legacy (pre-M21): the list is now built from the inventory's contents via ItemCatalog. Kept so setup tools compile.</summary>
        public List<ResourceDefinition> knownResources = new List<ResourceDefinition>();
        public SheltreSim.Survival.SurvivalStats stats;
        public List<BuildPieceDefinition> knownRecipes = new List<BuildPieceDefinition>();
        public BuildModeController buildModeController;
        public SheltreSim.Combat.MeleeAttacker meleeAttacker;
        public CraftingUI craftingUi;

        public bool IsOpen { get; private set; }

        private sealed class ItemRow
        {
            public GameObject root;
            public Text name, effect, buttonLabel;
            public Button action, quickSlot;
        }

        private GameObject panel;
        private Text vitalsLabel, gearLabel, feedbackLabel;
        private InventoryInputActions input;
        private RectTransform itemContent;
        private readonly Dictionary<string, ItemRow> itemRows = new Dictionary<string, ItemRow>();
        private readonly Dictionary<ItemCategory, Text> headers = new Dictionary<ItemCategory, Text>();
        private readonly Dictionary<string, Text> recipeRows = new Dictionary<string, Text>();
        private readonly Dictionary<string, string> effectText = new Dictionary<string, string>();
        private readonly List<string> shown = new List<string>();
        private readonly StringBuilder sb = new StringBuilder(128);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly ItemCategory[] CategoryOrder =
        {
            ItemCategory.Weapon, ItemCategory.Armor, ItemCategory.Tool, ItemCategory.Medicine, ItemCategory.Food,
            ItemCategory.Raw, ItemCategory.Refined, ItemCategory.Endgame,
        };

        private void Awake()
        {
            input = new InventoryInputActions();
            BuildUI();
        }

        private void OnEnable()
        {
            input.Toggle.performed += OnTogglePerformed;
            input.Enable();
        }

        private void OnDisable()
        {
            input.Toggle.performed -= OnTogglePerformed;
            input.Disable();
            CloseIfOpen();
        }

        private void OnDestroy() => input?.Dispose();

        private void OnTogglePerformed(InputAction.CallbackContext context)
        {
            if (IsOpen)
            {
                CloseIfOpen();
                return;
            }

            if (stats != null && stats.IsDead) return;
            if (buildModeController != null && buildModeController.IsBuildModeActive)
            {
                GameMessageBus.Post("Leave build mode with B or Escape to open your inventory.");
                return;
            }
            Open();
        }

        public bool Open()
        {
            if (IsOpen) return true;
            if (stats != null && stats.IsDead) return false;
            if (!ModalUI.TryOpen(this, CloseIfOpen)) return false;

            IsOpen = true;
            panel.SetActive(true);
            feedbackLabel.text = "Use supplies and equip gear here. Press Craft (or C) to make things.";
            Refresh();
            return true;
        }

        public void CloseIfOpen()
        {
            if (!IsOpen) return;
            IsOpen = false;
            panel.SetActive(false);
            ModalUI.Close(this);
        }

        private void OpenCrafting()
        {
            CraftingUI ui = craftingUi != null ? craftingUi : CraftingUI.Find();
            if (ui == null)
            {
                feedbackLabel.text = "Crafting is not available.";
                return;
            }
            CloseIfOpen();
            ui.OpenHand();
        }

        private void BuildUI()
        {
            Canvas canvas = GameUI.CreateCanvas(transform, "Inventory_Canvas", 80, true);
            panel = GameUI.Backdrop(canvas.transform, "InventoryBackdrop");
            Transform card = GameUI.Panel(panel.transform, "InventoryPanel", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1140f, 820f), GameUI.PanelColor, true).transform;

            GameUI.Label(card, "Title", "SUPPLIES & BLUEPRINTS", TopLeft, TopLeft,
                new Vector2(28f, -24f), new Vector2(650f, 44f), 30, GameUI.Accent, bold: true);
            GameUI.Button(card, "CloseButton", "Close", Vector2.one, Vector2.one,
                new Vector2(-28f, -24f), new Vector2(120f, 42f), CloseIfOpen);
            GameUI.Button(card, "CraftButton", "Craft  (C)", Vector2.one, Vector2.one,
                new Vector2(-160f, -24f), new Vector2(150f, 42f), OpenCrafting, true);
            GameUI.Label(card, "Subtitle", "Game paused  /  I, Tab or Escape to return", TopLeft, TopLeft,
                new Vector2(28f, -72f), new Vector2(900f, 26f), 18, GameUI.Muted);
            vitalsLabel = GameUI.Label(card, "Vitals", string.Empty, TopLeft, TopLeft,
                new Vector2(28f, -104f), new Vector2(1084f, 28f), 20);
            gearLabel = GameUI.Label(card, "Gear", string.Empty, TopLeft, TopLeft,
                new Vector2(28f, -132f), new Vector2(1084f, 28f), 18, GameUI.Accent);

            GameUI.Label(card, "SuppliesHeader", "INVENTORY", TopLeft, TopLeft,
                new Vector2(28f, -168f), new Vector2(510f, 28f), 18, GameUI.Accent, bold: true);
            GameUI.Label(card, "RecipesHeader", "BUILD RECIPES  /  owned : required", TopLeft, TopLeft,
                new Vector2(590f, -168f), new Vector2(522f, 28f), 18, GameUI.Accent, bold: true);

            itemContent = GameUI.ScrollContent(card, "InventoryScroll",
                new Vector2(28f, -202f), new Vector2(522f, 520f));

            RectTransform recipeContent = GameUI.ScrollContent(card, "RecipeScroll",
                new Vector2(590f, -202f), new Vector2(522f, 520f));
            float y = 0f;
            foreach (BuildPieceDefinition recipe in knownRecipes)
            {
                if (recipe == null || string.IsNullOrEmpty(recipe.id) || recipeRows.ContainsKey(recipe.id)) continue;
                Transform row = GameUI.Panel(recipeContent, "Recipe_" + recipe.id, TopLeft, TopLeft,
                    new Vector2(0f, -y), new Vector2(520f, 112f), GameUI.RowColor).transform;
                GameUI.Label(row, "Name", recipe.displayName, TopLeft, TopLeft,
                    new Vector2(12f, -5f), new Vector2(496f, 26f), 19);
                recipeRows[recipe.id] = GameUI.Label(row, "Cost", string.Empty, TopLeft, TopLeft,
                    new Vector2(12f, -32f), new Vector2(496f, 74f), 16);
                y += 118f;
            }
            recipeContent.sizeDelta = new Vector2(0f, y);

            feedbackLabel = GameUI.Label(card, "Feedback", string.Empty, TopLeft, TopLeft,
                new Vector2(28f, -738f), new Vector2(1084f, 28f), 18, GameUI.Accent);
            GameUI.Label(card, "BuildHelp", "BUILD: B  /  Wheel: choose  /  R: rotate  /  Click: place  /  CRAFT: C  /  Scroll lists for more", TopLeft, TopLeft,
                new Vector2(28f, -782f), new Vector2(1084f, 24f), 16, GameUI.Muted);
            panel.SetActive(false);
        }

        // ---------- Test hooks ----------

        public int VisibleItemRowCount { get { int n = 0; foreach (ItemRow r in itemRows.Values) if (r.root.activeSelf) n++; return n; } }
        public bool HasVisibleRow(string id) => itemRows.TryGetValue(id, out ItemRow r) && r.root.activeSelf;
        public Button ActionButton(string id) => itemRows.TryGetValue(id, out ItemRow r) ? r.action : null;
        public Button QuickSlotButton(string id) => itemRows.TryGetValue(id, out ItemRow r) ? r.quickSlot : null;
        public Button CraftButton => panel != null ? panel.transform.Find("InventoryPanel/CraftButton")?.GetComponent<Button>() : null;

        // ---------- Item rows ----------

        private ItemRow RowFor(string id)
        {
            if (itemRows.TryGetValue(id, out ItemRow existing)) return existing;
            Transform row = GameUI.Panel(itemContent, "Item_" + id, TopLeft, TopLeft, Vector2.zero, new Vector2(520f, 58f), GameUI.RowColor).transform;
            var made = new ItemRow { root = row.gameObject };
            made.name = GameUI.Label(row, "Count", id, TopLeft, TopLeft, new Vector2(12f, -5f), new Vector2(390f, 26f), 19);
            made.effect = GameUI.Label(row, "Effect", string.Empty, TopLeft, TopLeft, new Vector2(12f, -31f), new Vector2(390f, 22f), 15, GameUI.Muted);
            string captured = id;
            made.action = GameUI.Button(row, "Use", "Use", Vector2.one, Vector2.one, new Vector2(-10f, -9f), new Vector2(90f, 40f), () => UseItem(captured));
            made.buttonLabel = made.action.GetComponentInChildren<Text>();
            made.quickSlot = GameUI.Button(row, "QuickSlot", "Set key", Vector2.one, Vector2.one,
                new Vector2(-108f, -9f), new Vector2(92f, 40f), () => QuickSlots.CycleSlot(captured));
            made.quickSlot.gameObject.SetActive(false);
            itemRows[id] = made;
            return made;
        }

        private Text HeaderFor(ItemCategory category)
        {
            if (headers.TryGetValue(category, out Text header)) return header;
            header = GameUI.Label(itemContent, "Header_" + category, ItemCatalog.CategoryName(category).ToUpperInvariant(), TopLeft, TopLeft,
                Vector2.zero, new Vector2(520f, 26f), 16, GameUI.Warning, bold: true);
            headers[category] = header;
            return header;
        }

        private static string ActionName(ItemSpec spec, bool equipped)
        {
            if (spec.food) return "Eat";
            if (spec.drink) return "Drink";
            if (spec.medicine) return "Use";
            if (spec.backpackUpgrade) return "Install";
            if (spec.IsEquippable) return equipped ? "Unequip" : "Equip";
            return null;
        }

        private string Describe(ItemSpec spec)
        {
            if (effectText.TryGetValue(spec.id, out string cached)) return cached;
            sb.Clear();
            if (spec.food || spec.drink || spec.medicine)
            {
                if (spec.hunger > 0f) sb.Append("+").Append(spec.hunger.ToString("0")).Append(" food  ");
                if (spec.thirst > 0f) sb.Append("+").Append(spec.thirst.ToString("0")).Append(" water  ");
                if (spec.heal > 0f) sb.Append("+").Append(spec.heal.ToString("0")).Append(" health");
            }
            else if (spec.ranged)
            {
                sb.Append("Damage ").Append(spec.damage.ToString("0"));
                if (spec.pellets > 1) sb.Append(" x ").Append(spec.pellets);
                sb.Append(", ").Append(spec.roundsPerMinute.ToString("0")).Append(" rpm, ").Append(spec.magazine)
                    .Append(spec.energy ? " shots, " : " rounds, ").Append(ItemCatalog.NameOf(spec.ammoId));
                if (spec.splashRadius > 0f) sb.Append(", burst ").Append(spec.splashRadius.ToString("0.#")).Append(" m");
            }
            else if (spec.weapon) sb.Append("Melee damage ").Append(spec.damage.ToString("0"));
            else if (spec.slot == GearSlot.Armor || spec.slot == GearSlot.Coat) sb.Append("Takes ").Append((spec.reduction * 100f).ToString("0")).Append("% less damage");
            else if (spec.tool != ToolKind.None) sb.Append(spec.tool == ToolKind.Axe ? "Wood" : "Stone, ore, coal").Append(" +").Append(spec.bonus.ToString("0")).Append(" per harvest");
            else sb.Append(spec.description);
            string result = sb.ToString();
            effectText[spec.id] = result;
            return result;
        }

        private PlayerGear Gear() => inventory != null ? PlayerGear.Ensure(inventory.gameObject) : PlayerGear.Active;

        private bool CanUse(ItemSpec spec)
        {
            if (inventory == null || stats == null || stats.IsDead || stats.health <= 0f || inventory.GetCount(spec.id) < 1) return false;
            if (spec.IsEquippable) return Gear() != null;
            if (spec.backpackUpgrade) return Encumbrance.Active != null && Encumbrance.Active.CanUpgradeBackpack;
            if (!spec.IsConsumable) return false;
            return (spec.hunger > 0f && stats.hunger < stats.maxHunger) || (spec.thirst > 0f && stats.thirst < stats.maxThirst) ||
                (spec.heal > 0f && stats.health < stats.maxHealth);
        }

        private void UseItem(string id)
        {
            ItemSpec spec = ItemCatalog.Spec(id);
            // Guard stale UI events and avoid wasting a consumable at a full vital.
            if (!IsOpen || spec == null || !CanUse(spec)) return;
            string result;
            if (spec.backpackUpgrade)
            {
                Encumbrance encumbrance = Encumbrance.Active;
                if (encumbrance == null || !encumbrance.TryInstallBackpackUpgrade(inventory, id)) return;
                result = $"Backpack upgraded: {encumbrance.CarryCapacity:0} kg capacity.";
            }
            else if (spec.IsEquippable)
            {
                PlayerGear gear = Gear();
                if (gear.IsEquipped(id))
                {
                    gear.Unequip(spec.slot);
                    result = $"Unequipped {spec.name}.";
                }
                else if (!gear.Equip(id, out result)) { feedbackLabel.text = result; return; }
            }
            else
            {
                if (!inventory.TryConsume(id, 1)) return;
                float h = stats.health, f = stats.hunger, w = stats.thirst;
                if (spec.hunger > 0f) stats.Eat(spec.hunger);
                if (spec.thirst > 0f) stats.Drink(spec.thirst);
                if (spec.heal > 0f) stats.Heal(spec.heal);
                sb.Clear();
                sb.Append(spec.food ? "Ate " : spec.drink ? "Drank " : "Used ").Append(spec.name).Append(':');
                if (stats.hunger > f) sb.Append(" +").Append((stats.hunger - f).ToString("0")).Append(" food");
                if (stats.thirst > w) sb.Append(" +").Append((stats.thirst - w).ToString("0")).Append(" water");
                if (stats.health > h) sb.Append(" +").Append((stats.health - h).ToString("0")).Append(" health");
                result = sb.Append('.').ToString();
            }
            feedbackLabel.text = result;
            GameMessageBus.Post(result);
            Refresh();
        }

        /// <summary>Rebuilds the visible rows. Called on open and after each action, never per frame (the world is paused while open).</summary>
        public void Refresh()
        {
            if (panel == null) return;
            PlayerGear gear = Gear();
            if (stats != null)
            {
                vitalsLabel.text = $"HEALTH  {stats.health:0}/{stats.maxHealth:0}       FOOD  {stats.hunger:0}/{stats.maxHunger:0}       WATER  {stats.thirst:0}/{stats.maxThirst:0}";
            }
            if (gear != null)
            {
                sb.Clear();
                sb.Append("WEAPON ").Append(gear.WeaponId.Length > 0 ? ItemCatalog.NameOf(gear.WeaponId) : "Fists")
                    .Append("   RANGED ").Append(gear.RangedId.Length > 0 ? ItemCatalog.NameOf(gear.RangedId) : "none")
                    .Append("   ARMOR ").Append(gear.ArmorId.Length > 0 ? ItemCatalog.NameOf(gear.ArmorId) : "none")
                    .Append("   COAT ").Append(gear.CoatId.Length > 0 ? ItemCatalog.NameOf(gear.CoatId) : "none")
                    .Append("      DAMAGE ").Append(gear.AttackDamage.ToString("0"))
                    .Append("   ARMOR ").Append((gear.DamageReduction * 100f).ToString("0")).Append('%');
                gearLabel.text = sb.ToString();
            }

            foreach (ItemRow r in itemRows.Values) r.root.SetActive(false);
            foreach (Text h in headers.Values) h.gameObject.SetActive(false);

            float y = 0f;
            foreach (ItemCategory category in CategoryOrder)
            {
                shown.Clear();
                if (inventory != null)
                    foreach (var kv in inventory.GetAllCounts())
                    {
                        ItemSpec spec = ItemCatalog.Spec(kv.Key);
                        if (kv.Value > 0 && spec != null && spec.category == category) shown.Add(kv.Key);
                    }
                if (shown.Count == 0) continue;
                shown.Sort(System.StringComparer.Ordinal);
                Text header = HeaderFor(category);
                header.gameObject.SetActive(true);
                header.rectTransform.anchoredPosition = new Vector2(4f, -y);
                y += 28f;
                foreach (string id in shown)
                {
                    ItemSpec spec = ItemCatalog.Spec(id);
                    ItemRow row = RowFor(id);
                    row.root.SetActive(true);
                    ((RectTransform)row.root.transform).anchoredPosition = new Vector2(0f, -y);
                    bool equipped = gear != null && gear.IsEquipped(id);
                    row.name.text = equipped ? $"{spec.name}   x{inventory.GetCount(id)}   [EQUIPPED]" : $"{spec.name}   x{inventory.GetCount(id)}";
                    row.effect.text = Describe(spec);
                    bool canQuickSlot = QuickSlots.IsQuickUsable(spec.id);
                    row.quickSlot.gameObject.SetActive(canQuickSlot);
                    float labelWidth = canQuickSlot ? 300f : 390f;
                    row.name.rectTransform.sizeDelta = new Vector2(labelWidth, 26f);
                    row.effect.rectTransform.sizeDelta = new Vector2(labelWidth, 22f);
                    if (canQuickSlot)
                    {
                        int key = QuickSlots.AssignedSlotOf(id);
                        Text quickLabel = row.quickSlot.GetComponentInChildren<Text>();
                        if (quickLabel != null) quickLabel.text = key > 0 ? $"Key {key}  >" : "Set key";
                    }
                    string action = ActionName(spec, equipped);
                    row.action.gameObject.SetActive(action != null);
                    if (action != null)
                    {
                        row.buttonLabel.text = action;
                        row.action.interactable = CanUse(spec);
                    }
                    y += 64f;
                }
            }
            itemContent.sizeDelta = new Vector2(0f, y);

            foreach (BuildPieceDefinition recipe in knownRecipes)
            {
                if (recipe == null || !recipeRows.TryGetValue(recipe.id, out Text row)) continue;
                sb.Clear();
                bool affordable = CraftingSystem.TryAfford(inventory, recipe.craftingCost);
                sb.Append(affordable ? "Ready" : "Need supplies");
                bool any = false;
                if (recipe.craftingCost != null)
                {
                    foreach (CraftingCost cost in recipe.craftingCost)
                    {
                        if (cost == null || cost.resource == null || cost.amount <= 0) continue;
                        int owned = inventory != null ? inventory.GetCount(cost.resource.id) : 0;
                        sb.Append(any ? "   " : "\n").Append(cost.resource.displayName).Append(' ').Append(owned).Append(':').Append(cost.amount);
                        any = true;
                    }
                }
                if (!any) sb.Append("\nFree");
                row.text = sb.ToString();
                row.color = affordable ? GameUI.Accent : GameUI.Muted;
            }
        }
    }
}
