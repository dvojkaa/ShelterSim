using System.Collections.Generic;
using System.Text;
using System;
using SheltreSim.Building;
using SheltreSim.Items;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SheltreSim.Crafting
{
    /// <summary>
    /// One modal for every crafting station. Opened by C (hand crafting), by interacting with a station,
    /// or by InventoryUI's Craft button. ModalUI owns the pause and the cursor; Escape closes it.
    /// </summary>
    public sealed class CraftingUI : MonoBehaviour
    {
        public static CraftingUI Instance { get; private set; }

        public Inventory inventory;
        public SheltreSim.Survival.SurvivalStats stats;
        public BuildModeController buildModeController;

        public bool IsOpen { get; private set; }
        public StationKind CurrentStation { get; private set; } = StationKind.Hand;

        private enum Filter { All, Materials, Food, Medicine, Tools, Gear, Special }
        private static readonly string[] FilterNames = { "All", "Materials", "Food", "Medicine", "Tools", "Gear", "Special" };

        private sealed class Row
        {
            public Recipe recipe;
            public GameObject root;
            public Text inputs;
            public Button craft, craftFive, pin;
        }

        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private const float RowHeight = 124f;

        private GameObject panel;
        private Text title, status, subtitle;
        private InputField search;
        private Button craftableFilter, pinnedFilter;
        private RectTransform content;
        private readonly List<Row> rows = new List<Row>();
        private readonly StringBuilder text = new StringBuilder(256);
        private static readonly HashSet<string> pinnedRecipes = new HashSet<string>(StringComparer.Ordinal);
        private Filter filter = Filter.All;
        private bool craftableOnly, pinnedOnly;
        private InputAction toggle, cancel;
        private bool closeRequested;
        private bool built;
        private StationKind builtFor = (StationKind)(-1);

        private void Awake()
        {
            Instance = this;
            toggle = new InputAction("CraftingToggle", InputActionType.Button, "<Keyboard>/c");
            cancel = new InputAction("CraftingClose", InputActionType.Button, "<Keyboard>/escape");
            BuildShell();
        }

        private void OnEnable()
        {
            toggle.performed += OnToggle; cancel.performed += OnCancel;
            toggle.Enable(); cancel.Enable();
        }

        private void OnDisable()
        {
            toggle.performed -= OnToggle; cancel.performed -= OnCancel;
            toggle.Disable(); cancel.Disable();
            Close();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            toggle?.Dispose(); cancel?.Dispose();
        }

        private void OnToggle(InputAction.CallbackContext _)
        {
            GameObject selected = UnityEngine.EventSystems.EventSystem.current != null
                ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && search != null && selected.GetComponentInParent<InputField>() == search) return;
            if (IsOpen) closeRequested = true; else OpenHand();
        }

        // Escape is also PauseMenu's key. Closing in LateUpdate (after both callbacks ran this frame)
        // stops PauseMenu from seeing a free modal and opening the pause screen on the same press.
        private void OnCancel(InputAction.CallbackContext _) { if (IsOpen) closeRequested = true; }
        private void LateUpdate() { if (closeRequested) { closeRequested = false; Close(); } }

        public bool OpenHand() => Open(StationKind.Hand);

        public static CraftingUI Find()
        {
            if (Instance == null) Instance = FindFirstObjectByType<CraftingUI>();
            return Instance;
        }

        public bool Open(StationKind kind)
        {
            if (IsOpen) return false;
            if (stats == null) stats = FindFirstObjectByType<SheltreSim.Survival.SurvivalStats>();
            if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
            if (buildModeController == null) buildModeController = FindFirstObjectByType<BuildModeController>();
            if (stats != null && (stats.IsDead || stats.health <= 0f)) return false;
            if (buildModeController != null && buildModeController.IsBuildModeActive)
            {
                GameMessageBus.Post("Leave build mode with B or Escape to craft.");
                return false;
            }
            if (!ModalUI.TryOpen(this, Close)) return false;

            CurrentStation = kind;
            IsOpen = true;
            closeRequested = false;
            filter = Filter.All;
            craftableOnly = false;
            pinnedOnly = false;
            search.text = string.Empty;
            BuildRows(kind);
            panel.SetActive(true);
            title.text = RecipeBook.StationName(kind).ToUpperInvariant();
            subtitle.text = kind == StationKind.Hand ? "Hand crafting  /  C or Escape to close" : "Game paused  /  Escape to close";
            int crates = CraftingSystem.NearbyCrateCount(inventory);
            if (crates > 0) subtitle.text += crates == 1 ? "  /  Also using 1 crate nearby" : $"  /  Also using {crates} crates nearby";
            status.text = "Missing ingredients are shown in red.";
            Refresh();
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            closeRequested = false;
            if (panel != null) panel.SetActive(false);
            ModalUI.Close(this);
        }

        // ---------- UI ----------

        private void BuildShell()
        {
            if (built) return;
            built = true;
            Canvas canvas = GameUI.CreateCanvas(transform, "Crafting_Canvas", 90, true);
            panel = GameUI.Backdrop(canvas.transform, "CraftingBackdrop");
            Transform card = GameUI.Panel(panel.transform, "CraftingPanel", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1140f, 820f), GameUI.PanelColor, true).transform;
            title = GameUI.Label(card, "Title", "CRAFTING", TopLeft, TopLeft, new Vector2(28f, -24f), new Vector2(850f, 44f), 30, GameUI.Accent, bold: true);
            GameUI.Button(card, "CloseButton", "Close", Vector2.one, Vector2.one, new Vector2(-28f, -24f), new Vector2(120f, 42f), Close);
            subtitle = GameUI.Label(card, "Subtitle", string.Empty, TopLeft, TopLeft, new Vector2(28f, -70f), new Vector2(900f, 26f), 18, GameUI.Muted);
            for (int i = 0; i < FilterNames.Length; i++)
            {
                Filter f = (Filter)i;
                GameUI.Button(card, "Filter_" + FilterNames[i], FilterNames[i], TopLeft, TopLeft,
                    new Vector2(28f + i * 154f, -104f), new Vector2(146f, 38f), () => { filter = f; Refresh(); });
            }
            BuildSearchControls(card);
            content = GameUI.ScrollContent(card, "RecipeScroll", new Vector2(28f, -194f), new Vector2(1084f, 540f));
            status = GameUI.Label(card, "Status", string.Empty, TopLeft, TopLeft, new Vector2(28f, -748f), new Vector2(1084f, 28f), 18, GameUI.Accent);
            GameUI.Label(card, "Help", "Craft x5 makes five batches at once  /  Scroll the list for more", TopLeft, TopLeft,
                new Vector2(28f, -784f), new Vector2(1084f, 24f), 16, GameUI.Muted);
            panel.SetActive(false);
        }

        private void BuildSearchControls(Transform card)
        {
            Image background = GameUI.Panel(card, "RecipeSearch", TopLeft, TopLeft,
                new Vector2(28f, -151f), new Vector2(340f, 34f), new Color(0.025f, 0.04f, 0.045f, 1f), true);
            search = background.gameObject.AddComponent<InputField>();
            search.lineType = InputField.LineType.SingleLine;
            Text value = GameUI.Label(background.transform, "Value", string.Empty, Vector2.zero, Vector2.zero,
                new Vector2(10f, 0f), new Vector2(316f, 30f), 17, Color.white, TextAnchor.MiddleLeft);
            value.rectTransform.anchorMin = Vector2.zero;
            value.rectTransform.anchorMax = Vector2.one;
            value.rectTransform.offsetMin = new Vector2(10f, 0f);
            value.rectTransform.offsetMax = new Vector2(-10f, 0f);
            Text placeholder = GameUI.Label(background.transform, "Placeholder", "Search recipes...", Vector2.zero, Vector2.zero,
                new Vector2(10f, 0f), new Vector2(316f, 30f), 17, GameUI.Muted, TextAnchor.MiddleLeft);
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.rectTransform.anchorMin = Vector2.zero;
            placeholder.rectTransform.anchorMax = Vector2.one;
            placeholder.rectTransform.offsetMin = new Vector2(10f, 0f);
            placeholder.rectTransform.offsetMax = new Vector2(-10f, 0f);
            search.textComponent = value;
            search.placeholder = placeholder;
            search.targetGraphic = background;
            search.onValueChanged.AddListener(_ => Refresh());

            craftableFilter = GameUI.Button(card, "CraftableOnly", "Can Craft: Off", TopLeft, TopLeft,
                new Vector2(386f, -151f), new Vector2(184f, 34f), () =>
                {
                    craftableOnly = !craftableOnly;
                    Refresh();
                });
            pinnedFilter = GameUI.Button(card, "PinnedOnly", "Pinned: Off", TopLeft, TopLeft,
                new Vector2(578f, -151f), new Vector2(150f, 34f), () =>
                {
                    pinnedOnly = !pinnedOnly;
                    Refresh();
                });
        }

        private void BuildRows(StationKind kind)
        {
            if (builtFor == kind) return;
            foreach (Row old in rows) if (old.root != null) Destroy(old.root);
            rows.Clear();
            builtFor = kind;
            foreach (Recipe recipe in RecipeBook.ForStation(kind))
            {
                Recipe captured = recipe;
                ResourceDefinition outDef = ItemCatalog.Def(recipe.output);
                Transform row = GameUI.Panel(content, "Recipe_" + recipe.id, TopLeft, TopLeft, Vector2.zero, new Vector2(1070f, RowHeight - 6f), GameUI.RowColor).transform;
                string head = recipe.outputAmount > 1 ? $"{ItemCatalog.NameOf(recipe.output)}  x{recipe.outputAmount}" : ItemCatalog.NameOf(recipe.output);
                GameUI.Label(row, "Name", head, TopLeft, TopLeft, new Vector2(14f, -6f), new Vector2(760f, 28f), 21, bold: true);
                GameUI.Label(row, "Desc", outDef != null ? outDef.description : string.Empty, TopLeft, TopLeft, new Vector2(14f, -34f), new Vector2(760f, 24f), 16, GameUI.Muted);
                Row r = new Row { recipe = recipe, root = row.gameObject };
                r.inputs = GameUI.Label(row, "Inputs", string.Empty, TopLeft, TopLeft, new Vector2(14f, -60f), new Vector2(760f, 52f), 17);
                r.pin = GameUI.Button(row, "Pin", "☆", TopLeft, TopLeft, new Vector2(790f, -4f), new Vector2(52f, 32f),
                    () => TogglePin(captured));
                r.craft = GameUI.Button(row, "Craft", "Craft", Vector2.one, Vector2.one, new Vector2(-136f, -34f), new Vector2(120f, 48f), () => DoCraft(captured, 1), true);
                r.craftFive = GameUI.Button(row, "CraftFive", "Craft x5", Vector2.one, Vector2.one, new Vector2(-12f, -34f), new Vector2(116f, 48f), () => DoCraft(captured, 5));
                rows.Add(r);
            }
        }

        private static bool Matches(Filter f, ItemCategory c)
        {
            switch (f)
            {
                case Filter.All: return true;
                case Filter.Materials: return c == ItemCategory.Raw || c == ItemCategory.Refined;
                case Filter.Food: return c == ItemCategory.Food;
                case Filter.Medicine: return c == ItemCategory.Medicine;
                case Filter.Tools: return c == ItemCategory.Tool;
                case Filter.Gear: return c == ItemCategory.Weapon || c == ItemCategory.Armor;
                default: return c == ItemCategory.Endgame;
            }
        }

        private void Refresh()
        {
            float y = 0f;
            string query = search != null ? search.text.Trim() : string.Empty;
            foreach (Row r in rows)
            {
                var spec = ItemCatalog.Spec(r.recipe.output);
                bool canCraft = RecipeBook.CanCraft(inventory, r.recipe, 1);
                bool matchesSearch = string.IsNullOrEmpty(query) ||
                    ItemCatalog.NameOf(r.recipe.output).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (spec != null && !string.IsNullOrEmpty(spec.description) && spec.description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    r.recipe.id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                bool isPinned = pinnedRecipes.Contains(r.recipe.id);
                bool visible = spec != null && Matches(filter, spec.category) && matchesSearch &&
                    (!craftableOnly || canCraft) && (!pinnedOnly || isPinned);
                r.root.SetActive(visible);
                Text pinText = r.pin.GetComponentInChildren<Text>();
                if (pinText != null) pinText.text = isPinned ? "★" : "☆";
                if (!visible) continue;
                RectTransform rect = (RectTransform)r.root.transform;
                rect.anchoredPosition = new Vector2(0f, -y);
                y += RowHeight;
                text.Clear();
                for (int i = 0; i < r.recipe.inputs.Length; i++)
                {
                    Ingredient ing = r.recipe.inputs[i];
                    int owned = inventory != null ? inventory.GetCount(ing.id) : 0;
                    int inCrates = CraftingSystem.CountInCrates(inventory, ing.id);
                    bool ok = (long)owned + inCrates >= ing.amount;
                    if (i > 0) text.Append("   ");
                    if (!ok) text.Append("<color=#ff6459>");
                    text.Append(ItemCatalog.NameOf(ing.id)).Append(' ').Append(owned);
                    if (inCrates > 0) text.Append(" + ").Append(inCrates).Append(" in crates");
                    text.Append(':').Append(ing.amount);
                    if (!ok) text.Append("</color>");
                }
                r.inputs.text = text.ToString();
                r.craft.interactable = canCraft;
                r.craftFive.interactable = RecipeBook.CanCraft(inventory, r.recipe, 5);
            }
            content.sizeDelta = new Vector2(0f, y);
            SetButtonLabel(craftableFilter, craftableOnly ? "Can Craft: On" : "Can Craft: Off");
            SetButtonLabel(pinnedFilter, pinnedOnly ? "Pinned: On" : "Pinned: Off");
        }

        private void TogglePin(Recipe recipe)
        {
            if (!pinnedRecipes.Add(recipe.id)) pinnedRecipes.Remove(recipe.id);
            Refresh();
        }

        private static void SetButtonLabel(Button button, string value)
        {
            if (button == null) return;
            Text label = button.GetComponentInChildren<Text>();
            if (label != null) label.text = value;
        }

        private void DoCraft(Recipe recipe, int times)
        {
            if (!IsOpen) return;
            if (RecipeBook.TryCraft(inventory, recipe, times, out string message))
            {
                SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.Craft, 0.9f);
                status.text = message;
                GameMessageBus.Post(message);
            }
            else
            {
                SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.UiDeny, 0.35f);
                status.text = message;
            }
            Refresh();
        }

        // ---------- Test hooks ----------

        public Button CraftButton(string recipeId, bool five = false)
        {
            foreach (Row r in rows) if (r.recipe.id == recipeId) return five ? r.craftFive : r.craft;
            return null;
        }

        public Button PinButton(string recipeId)
        {
            foreach (Row r in rows) if (r.recipe.id == recipeId) return r.pin;
            return null;
        }

        public int VisibleRowCount { get { int n = 0; foreach (Row r in rows) if (r.root.activeSelf) n++; return n; } }
        public int CraftableVisibleRowCount
        {
            get { int n = 0; foreach (Row r in rows) if (r.root.activeSelf && RecipeBook.CanCraft(inventory, r.recipe, 1)) n++; return n; }
        }
        public bool HasVisibleRecipe(string recipeId)
        {
            foreach (Row r in rows) if (r.recipe.id == recipeId) return r.root.activeSelf;
            return false;
        }
        public void SetSearchForProbe(string value) { search.text = value ?? string.Empty; }
        public void SetCraftableOnlyForProbe(bool enabled) { craftableOnly = enabled; Refresh(); }
        public void SetPinnedOnlyForProbe(bool enabled) { pinnedOnly = enabled; Refresh(); }
        public void SetPinnedForProbe(string recipeId, bool pinned)
        {
            if (pinned) pinnedRecipes.Add(recipeId); else pinnedRecipes.Remove(recipeId);
            Refresh();
        }
    }
}
