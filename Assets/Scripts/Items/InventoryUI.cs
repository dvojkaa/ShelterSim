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

        /// <summary>One grid tile: icon, count badge, name and a per-item action button.</summary>
        private sealed class ItemRow
        {
            public GameObject root;
            public Image background, icon;
            public Text placeholder, count, name, buttonLabel;
            public Button select, action;
        }

        private enum Tab { All, Weapons, Armor, Tools, Medicine, Food, Materials, Build }
        private static readonly string[] TabNames = { "All", "Weapons", "Armor", "Tools", "Medicine", "Food", "Materials", "Build" };

        private GameObject panel;
        private Text vitalsLabel, gearLabel, feedbackLabel;
        private InventoryInputActions input;
        private RectTransform itemContent, recipeContent;
        private GameObject gridScroll, recipeScroll, detailPanel;
        private Image detailIcon;
        private Text detailPlaceholder, detailName, detailMeta, detailText, detailEffect, detailAction, detailQuickLabel;
        private Button detailActionButton, detailQuickButton;
        private readonly Button[] tabButtons = new Button[TabNames.Length];
        private Tab tab = Tab.All;
        private string selectedId;
        private readonly Dictionary<string, ItemRow> itemRows = new Dictionary<string, ItemRow>();
        private readonly Dictionary<string, Text> recipeRows = new Dictionary<string, Text>();
        private readonly Dictionary<string, string> effectText = new Dictionary<string, string>();
        private readonly List<string> shown = new List<string>();
        private readonly StringBuilder sb = new StringBuilder(128);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Color SelectedColor = new Color(0.20f, 0.33f, 0.28f, 0.98f);
        private const float TileW = 160f, TileH = 156f, TileGap = 10f;
        private const int Columns = 4;
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

            GameUI.Label(card, "Title", "INVENTORY", TopLeft, TopLeft,
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

            for (int i = 0; i < TabNames.Length; i++)
            {
                Tab captured = (Tab)i;
                tabButtons[i] = GameUI.Button(card, "Tab_" + TabNames[i], TabNames[i], TopLeft, TopLeft,
                    new Vector2(28f + i * 114f, -166f), new Vector2(108f, 34f), () => SetTab(captured));
            }

            // Item grid (left) and the selected item's details (right).
            GameObject gridRoot = new GameObject("GridHolder", typeof(RectTransform));
            gridRoot.transform.SetParent(card, false);
            itemContent = GameUI.ScrollContent(card, "InventoryScroll", new Vector2(28f, -214f), new Vector2(700f, 508f));
            gridScroll = itemContent.parent.gameObject;
            Destroy(gridRoot);

            detailPanel = GameUI.Panel(card, "Detail", TopLeft, TopLeft, new Vector2(748f, -214f), new Vector2(364f, 508f), GameUI.RowColor).gameObject;
            Transform d = detailPanel.transform;
            Image iconBack = GameUI.Panel(d, "IconBack", TopLeft, TopLeft, new Vector2(100f, -14f), new Vector2(164f, 164f), new Color(0f, 0f, 0f, 0.25f));
            detailIcon = GameUI.Panel(iconBack.transform, "Icon", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(148f, 148f), Color.white);
            detailIcon.preserveAspect = true;
            detailPlaceholder = GameUI.Label(iconBack.transform, "Placeholder", string.Empty, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(150f, 60f), 40, Color.white, TextAnchor.MiddleCenter, true);
            detailName = GameUI.Label(d, "Name", string.Empty, TopLeft, TopLeft, new Vector2(14f, -186f), new Vector2(336f, 32f), 24, GameUI.Accent, TextAnchor.MiddleCenter, true);
            detailMeta = GameUI.Label(d, "Meta", string.Empty, TopLeft, TopLeft, new Vector2(14f, -218f), new Vector2(336f, 24f), 16, GameUI.Muted, TextAnchor.MiddleCenter);
            detailText = GameUI.Label(d, "Description", string.Empty, TopLeft, TopLeft, new Vector2(18f, -250f), new Vector2(328f, 104f), 17);
            detailEffect = GameUI.Label(d, "Effect", string.Empty, TopLeft, TopLeft, new Vector2(18f, -358f), new Vector2(328f, 58f), 17, GameUI.Warning);
            detailActionButton = GameUI.Button(d, "Use", "Use", TopLeft, TopLeft, new Vector2(22f, -428f), new Vector2(320f, 34f), () => UseItem(selectedId), true);
            detailAction = detailActionButton.GetComponentInChildren<Text>();
            detailQuickButton = GameUI.Button(d, "QuickSlot", "Set key", TopLeft, TopLeft, new Vector2(22f, -468f), new Vector2(320f, 30f),
                () => { if (selectedId != null) { QuickSlots.CycleSlot(selectedId); Refresh(); } });
            detailQuickLabel = detailQuickButton.GetComponentInChildren<Text>();

            // Build recipes tab (owned : required per piece).
            recipeContent = GameUI.ScrollContent(card, "RecipeScroll", new Vector2(28f, -214f), new Vector2(1084f, 508f));
            recipeScroll = recipeContent.parent.gameObject;
            float y = 0f;
            foreach (BuildPieceDefinition recipe in knownRecipes)
            {
                if (recipe == null || string.IsNullOrEmpty(recipe.id) || recipeRows.ContainsKey(recipe.id)) continue;
                Transform row = GameUI.Panel(recipeContent, "Recipe_" + recipe.id, TopLeft, TopLeft,
                    new Vector2(0f, -y), new Vector2(1060f, 96f), GameUI.RowColor).transform;
                GameUI.Label(row, "Name", recipe.displayName, TopLeft, TopLeft,
                    new Vector2(12f, -5f), new Vector2(1030f, 26f), 19);
                recipeRows[recipe.id] = GameUI.Label(row, "Cost", string.Empty, TopLeft, TopLeft,
                    new Vector2(12f, -32f), new Vector2(1030f, 58f), 16);
                y += 102f;
            }
            recipeContent.sizeDelta = new Vector2(0f, y);

            feedbackLabel = GameUI.Label(card, "Feedback", string.Empty, TopLeft, TopLeft,
                new Vector2(28f, -738f), new Vector2(1084f, 28f), 18, GameUI.Accent);
            GameUI.Label(card, "BuildHelp", "BUILD: B  /  Wheel: choose  /  R: rotate  /  Click: place  /  CRAFT: C  /  Click an item to inspect, scroll for more", TopLeft, TopLeft,
                new Vector2(28f, -782f), new Vector2(1084f, 24f), 16, GameUI.Muted);
            panel.SetActive(false);
        }

        private void SetTab(Tab value)
        {
            tab = value;
            Refresh();
        }

        // ---------- Test hooks ----------

        public int VisibleItemRowCount { get { int n = 0; foreach (ItemRow r in itemRows.Values) if (r.root.activeSelf) n++; return n; } }
        public bool HasVisibleRow(string id) => itemRows.TryGetValue(id, out ItemRow r) && r.root.activeSelf;
        public Button ActionButton(string id) => itemRows.TryGetValue(id, out ItemRow r) ? r.action : null;
        /// <summary>Selects the item (so the shared quick-slot button targets it) and returns that button.</summary>
        public Button QuickSlotButton(string id) { if (!itemRows.ContainsKey(id)) return null; Select(id); return detailQuickButton; }
        public Button CraftButton => panel != null ? panel.transform.Find("InventoryPanel/CraftButton")?.GetComponent<Button>() : null;
        public string SelectedItemId => selectedId;
        public void SelectForProbe(string id) { selectedId = null; if (itemRows.ContainsKey(id) || ItemCatalog.Exists(id)) { selectedId = id; Refresh(); } }
        public void SetTabForProbe(int index) => SetTab((Tab)index);

        // ---------- Item tiles ----------

        private ItemRow RowFor(string id)
        {
            if (itemRows.TryGetValue(id, out ItemRow existing)) return existing;
            ItemSpec spec = ItemCatalog.Spec(id);
            Image bg = GameUI.Panel(itemContent, "Item_" + id, TopLeft, TopLeft, Vector2.zero, new Vector2(TileW, TileH), GameUI.RowColor, true);
            Transform t = bg.transform;
            var made = new ItemRow { root = bg.gameObject, background = bg };
            string captured = id;
            made.select = bg.gameObject.AddComponent<Button>();
            made.select.targetGraphic = bg;
            Navigation none = made.select.navigation; none.mode = Navigation.Mode.None; made.select.navigation = none;
            made.select.onClick.AddListener(() => { SheltreSim.Audio.GameAudio.UiClick(); Select(captured); });
            made.icon = GameUI.Panel(t, "Icon", new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -8f), new Vector2(84f, 84f), Color.white);
            made.icon.preserveAspect = true;
            made.placeholder = GameUI.Label(t, "Placeholder", string.Empty, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -8f), new Vector2(84f, 84f), 30,
                Color.white, TextAnchor.MiddleCenter, true);
            made.count = GameUI.Label(t, "Count", string.Empty, Vector2.one, Vector2.one, new Vector2(-8f, -6f), new Vector2(70f, 24f), 18, GameUI.Accent, TextAnchor.UpperRight, true);
            made.name = GameUI.Label(t, "Name", spec != null ? spec.name : id, TopLeft, TopLeft, new Vector2(6f, -96f), new Vector2(TileW - 12f, 22f), 15, Color.white, TextAnchor.UpperCenter);
            made.action = GameUI.Button(t, "Use", "Use", new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 8f), new Vector2(TileW - 20f, 28f), () => { Select(captured); UseItem(captured); });
            made.buttonLabel = made.action.GetComponentInChildren<Text>();
            made.buttonLabel.fontSize = 16;
            itemRows[id] = made;
            return made;
        }

        private void Select(string id)
        {
            if (selectedId == id) return;
            selectedId = id;
            Refresh();
        }

        private static bool InTab(Tab t, ItemCategory c)
        {
            switch (t)
            {
                case Tab.All: return true;
                case Tab.Weapons: return c == ItemCategory.Weapon;
                case Tab.Armor: return c == ItemCategory.Armor;
                case Tab.Tools: return c == ItemCategory.Tool;
                case Tab.Medicine: return c == ItemCategory.Medicine;
                case Tab.Food: return c == ItemCategory.Food;
                case Tab.Materials: return c == ItemCategory.Raw || c == ItemCategory.Refined || c == ItemCategory.Endgame;
                default: return false;
            }
        }

        private static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            string[] words = name.Split(' ');
            return words.Length > 1 ? (words[0].Substring(0, 1) + words[1].Substring(0, 1)).ToUpperInvariant() : name.Substring(0, Mathf.Min(2, name.Length)).ToUpperInvariant();
        }

        private static void ShowIcon(Image image, Text placeholder, ItemSpec spec)
        {
            Sprite sprite = ItemIcons.Get(spec.id);
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : ItemIcons.CategoryColor(spec.category);
            placeholder.text = sprite != null ? string.Empty : Initials(spec.name);
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

        private void RefreshDetail(PlayerGear gear)
        {
            ItemSpec spec = selectedId != null ? ItemCatalog.Spec(selectedId) : null;
            if (spec == null)
            {
                detailIcon.sprite = null; detailIcon.color = new Color(1f, 1f, 1f, 0f); detailPlaceholder.text = string.Empty;
                detailName.text = "Nothing selected";
                detailMeta.text = string.Empty;
                detailText.text = tab == Tab.All ? "Your inventory is empty. Gather, loot and craft." : "No items of this kind. Pick another tab.";
                detailEffect.text = string.Empty;
                detailActionButton.gameObject.SetActive(false);
                detailQuickButton.gameObject.SetActive(false);
                return;
            }
            ShowIcon(detailIcon, detailPlaceholder, spec);
            bool equipped = gear != null && gear.IsEquipped(spec.id);
            detailName.text = spec.name;
            detailMeta.text = ItemCatalog.CategoryName(spec.category) + "   |   owned x" + (inventory != null ? inventory.GetCount(spec.id) : 0) + (equipped ? "   |   EQUIPPED" : string.Empty);
            detailText.text = spec.description;
            detailEffect.text = Describe(spec) == spec.description ? string.Empty : Describe(spec);
            string action = ActionName(spec, equipped);
            detailActionButton.gameObject.SetActive(action != null);
            if (action != null)
            {
                detailAction.text = action;
                detailActionButton.interactable = CanUse(spec);
            }
            bool canQuick = QuickSlots.IsQuickUsable(spec.id);
            detailQuickButton.gameObject.SetActive(canQuick);
            if (canQuick)
            {
                int key = QuickSlots.AssignedSlotOf(spec.id);
                detailQuickLabel.text = key > 0 ? $"Quick slot: key {key}  (click to change)" : "Assign quick-slot key";
            }
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

            for (int i = 0; i < tabButtons.Length; i++)
            {
                Image tabImage = (Image)tabButtons[i].targetGraphic;
                tabImage.color = (int)tab == i ? new Color(0.22f, 0.38f, 0.29f) : new Color(0.16f, 0.23f, 0.25f);
            }
            bool buildTab = tab == Tab.Build;
            gridScroll.SetActive(!buildTab);
            detailPanel.SetActive(!buildTab);
            recipeScroll.SetActive(buildTab);

            foreach (ItemRow r in itemRows.Values) r.root.SetActive(false);
            shown.Clear();
            if (inventory != null && !buildTab)
            {
                foreach (ItemCategory category in CategoryOrder)
                {
                    int start = shown.Count;
                    foreach (var kv in inventory.GetAllCounts())
                    {
                        ItemSpec spec = ItemCatalog.Spec(kv.Key);
                        if (kv.Value > 0 && spec != null && spec.category == category && InTab(tab, category)) shown.Add(kv.Key);
                    }
                    shown.Sort(start, shown.Count - start, StringComparer.Ordinal);
                }
            }
            if (selectedId == null || !shown.Contains(selectedId)) selectedId = shown.Count > 0 ? shown[0] : null;

            for (int index = 0; index < shown.Count; index++)
            {
                string id = shown[index];
                ItemSpec spec = ItemCatalog.Spec(id);
                ItemRow row = RowFor(id);
                row.root.SetActive(true);
                ((RectTransform)row.root.transform).anchoredPosition = new Vector2((index % Columns) * (TileW + TileGap), -(index / Columns) * (TileH + TileGap));
                bool equipped = gear != null && gear.IsEquipped(id);
                row.background.color = id == selectedId ? SelectedColor : GameUI.RowColor;
                ShowIcon(row.icon, row.placeholder, spec);
                row.count.text = equipped ? "EQUIPPED" : "x" + inventory.GetCount(id);
                row.count.color = equipped ? GameUI.Warning : GameUI.Accent;
                string action = ActionName(spec, equipped);
                row.action.gameObject.SetActive(action != null);
                if (action != null)
                {
                    row.buttonLabel.text = action;
                    row.action.interactable = CanUse(spec);
                }
            }
            int rows = (shown.Count + Columns - 1) / Columns;
            itemContent.sizeDelta = new Vector2(0f, Mathf.Max(0, rows * (TileH + TileGap)));
            RefreshDetail(gear);

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
