using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SheltreSim.Items
{
    /// <summary>
    /// Code-constructed input action map for the inventory toggle. Keyboard-only — this is
    /// a UI toggle, not core gameplay, same deliberate deviation SaveInputActions already
    /// makes for F5/F9.
    /// </summary>
    public class InventoryInputActions : IDisposable
    {
        private readonly InputActionMap map;

        public InputAction Toggle { get; }

        public InventoryInputActions()
        {
            map = new InputActionMap("Inventory");

            Toggle = map.AddAction("Toggle", InputActionType.Button);
            Toggle.AddBinding("<Keyboard>/i");
        }

        public void Enable() => map.Enable();

        public void Disable() => map.Disable();

        public void Dispose()
        {
            map?.Dispose();
        }
    }

    /// <summary>
    /// Playtest feedback: "I want an inventory screen (I key) ... and some crafting menu in
    /// the inventory." M9 upgrades this from a passive readout (SurvivalHUD-style) into the
    /// project's second interactive panel (after PauseMenu): food resources get an Eat
    /// button, and a read-only recipes/crafting-cost list gives the "crafting menu" request
    /// real content without duplicating the actual build-mode placement flow.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        public Inventory inventory;
        public List<ResourceDefinition> knownResources = new List<ResourceDefinition>();

        /// <summary>
        /// M9 — wired externally (by a human, into M8MenusSetup) after this component is
        /// added. Every use below null-guards it: the Eat button must not silently consume a
        /// food item for zero effect just because this hasn't been assigned yet.
        /// </summary>
        public SheltreSim.Survival.SurvivalStats stats;

        /// <summary>
        /// M9 — read-only recipe reference list for the inventory's new "crafting menu"
        /// section. Also wired externally later, same as <see cref="knownResources"/>.
        /// </summary>
        public List<SheltreSim.Building.BuildPieceDefinition> knownRecipes = new List<SheltreSim.Building.BuildPieceDefinition>();

        /// <summary>
        /// M9 QC — wired by M8MenusSetup. Needed because this panel is now interactive: it
        /// takes the cursor and freezes time while open, which must not happen on top of
        /// build mode (a ghost would hover over the panel and the click meant for Eat would
        /// place a piece). Mirrors PauseMenu's existing deference to build mode.
        /// </summary>
        public SheltreSim.Building.BuildModeController buildModeController;

        /// <summary>
        /// True while the panel is showing. PauseMenu reads this so Escape backs out of the
        /// inventory instead of stacking a second modal on top of it — only one thing at a
        /// time may own Time.timeScale and the cursor.
        /// </summary>
        public bool IsOpen => isOpen;

        private GameObject panel;
        private bool isOpen;
        private readonly Dictionary<string, Text> rows = new Dictionary<string, Text>();
        private readonly Dictionary<string, Text> recipeRows = new Dictionary<string, Text>();

        private InventoryInputActions input;

        private void Awake()
        {
            // First interactive (clickable) UI this panel has ever needed — previously it was
            // a pure readout and never required an EventSystem. Same safe find-or-create idiom
            // as PauseMenu.EnsureEventSystem: no-ops if one already exists in the scene.
            EnsureEventSystem();

            // Same reasoning as SurvivalHUD's guard: Awake runs both when an editor tool adds
            // this component at edit-time and again when Play Mode actually starts.
            Transform stale = transform.Find("Inventory_Canvas");
            if (stale != null)
            {
                DestroyImmediate(stale.gameObject);
            }

            input = new InventoryInputActions();

            GameObject canvasObject = new GameObject("Inventory_Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObject.AddComponent<GraphicRaycaster>();

            panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            // Widened/heightened from the original 500x400 (tuned for a resource-only readout)
            // to fit per-food Eat buttons plus a whole second Recipes section without overlap.
            const float panelWidth = 620f;
            panelRect.sizeDelta = new Vector2(panelWidth, 700f);

            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            CreateLabel(panel.transform, "Title", "Inventory", font, 22, -20f, FontStyle.Bold);

            float rowY = -70f;
            foreach (ResourceDefinition resource in knownResources)
            {
                if (resource == null)
                {
                    continue;
                }

                if (resource.isFood)
                {
                    // Food row: label takes the left portion, an Eat button sits anchored to
                    // the panel's right edge so the two never overlap regardless of exact
                    // label text length.
                    Text row = CreateLabelAt(
                        panel.transform, "Row_" + resource.id, $"{resource.displayName}: 0", font, 18,
                        anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(0f, 1f), pivot: new Vector2(0f, 1f),
                        anchoredPosition: new Vector2(20f, rowY), sizeDelta: new Vector2(420f, 28f),
                        alignment: TextAnchor.MiddleLeft, style: FontStyle.Normal);
                    rows[resource.id] = row;

                    // Capture a per-iteration local for the closure. C# foreach loop variables
                    // have been per-iteration since C# 5, so capturing `resource` directly
                    // would already be safe — this local is just belt-and-suspenders clarity.
                    ResourceDefinition capturedResource = resource;
                    CreateButtonAt(
                        panel.transform, "EatButton_" + resource.id, "Eat", font,
                        anchorMin: new Vector2(1f, 1f), anchorMax: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f),
                        anchoredPosition: new Vector2(-20f, rowY), sizeDelta: new Vector2(90f, 28f),
                        onClick: () =>
                        {
                            if (stats == null || inventory == null)
                            {
                                return;
                            }

                            // SurvivalStats.Eat() no-ops while dead, and this panel is still
                            // reachable after death (nothing disables InventoryUI, and Die()
                            // leaves the cursor free), so without this test a click would
                            // destroy a berry for literally nothing. Consume only once the
                            // effect is known to be able to land.
                            if (stats.IsDead)
                            {
                                return;
                            }

                            if (inventory.TryConsume(capturedResource.id, 1))
                            {
                                stats.Eat(capturedResource.hungerRestored);
                            }
                        });
                }
                else
                {
                    Text row = CreateLabel(panel.transform, "Row_" + resource.id, $"{resource.displayName}: 0", font, 18, rowY, FontStyle.Normal);
                    rows[resource.id] = row;
                }

                rowY -= 34f;
            }

            rowY -= 16f;
            CreateLabel(panel.transform, "RecipesHeader", "Recipes", font, 20, rowY, FontStyle.Bold);
            rowY -= 34f;

            foreach (SheltreSim.Building.BuildPieceDefinition recipe in knownRecipes)
            {
                if (recipe == null)
                {
                    continue;
                }

                Text recipeRow = CreateLabel(panel.transform, "Recipe_" + recipe.id, FormatRecipe(recipe), font, 16, rowY, FontStyle.Normal);
                recipeRows[recipe.id] = recipeRow;
                rowY -= 30f;
            }

            panel.SetActive(false);
        }

        /// <summary>
        /// Builds the "Wall Segment — 2 Wood" style line used for each recipe row. Skips null
        /// cost entries/resources the same defensive way CraftingSystem.TryAfford does.
        /// </summary>
        private static string FormatRecipe(SheltreSim.Building.BuildPieceDefinition recipe)
        {
            List<string> parts = new List<string>();
            if (recipe.craftingCost != null)
            {
                foreach (SheltreSim.Crafting.CraftingCost line in recipe.craftingCost)
                {
                    if (line == null || line.resource == null)
                    {
                        continue;
                    }

                    parts.Add($"{line.amount} {line.resource.displayName}");
                }
            }

            string costText = parts.Count > 0 ? string.Join(", ", parts) : "Free";
            return $"{recipe.displayName} — {costText}";
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static Text CreateLabel(Transform parent, string name, string text, Font font, int fontSize, float yOffset, FontStyle style)
        {
            GameObject labelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(parent, false);

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, yOffset);
            rect.sizeDelta = new Vector2(-40f, 28f);

            Text label = labelObject.GetComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = text;

            return label;
        }

        /// <summary>
        /// Like <see cref="CreateLabel"/> but with fully explicit anchors/pivot/size rather
        /// than the fixed full-width-stretch layout — used for food rows, which need to leave
        /// room on the right for an Eat button instead of spanning the whole panel width.
        /// </summary>
        private static Text CreateLabelAt(Transform parent, string name, string text, Font font, int fontSize,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta,
            TextAnchor alignment, FontStyle style)
        {
            GameObject labelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(parent, false);

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Text label = labelObject.GetComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = Color.white;
            label.text = text;

            return label;
        }

        /// <summary>
        /// PauseMenu-style clickable button (GameObject + RectTransform/CanvasRenderer/Image/
        /// Button + child Text label), but with configurable anchors so it can be pinned to a
        /// specific edge of the panel (here: the right edge of a food row) instead of
        /// PauseMenu's fixed centered-in-parent layout.
        /// </summary>
        private static void CreateButtonAt(Transform parent, string name, string label, Font font,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta,
            Action onClick)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.25f, 0.25f, 0.25f);

            Button button = buttonObject.GetComponent<Button>();
            // Selectable.targetGraphic is only auto-assigned by Reset(), which runs when a
            // component is added through the Inspector - not when it arrives via the
            // GameObject(params Type[]) constructor as it does here. Without this the button
            // still works but gives no hover/press feedback at all.
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(() => onClick());

            CreateLabelAt(
                buttonObject.transform, name + "_Label", label, font, 14,
                anchorMin: Vector2.zero, anchorMax: Vector2.one, pivot: new Vector2(0.5f, 0.5f),
                anchoredPosition: Vector2.zero, sizeDelta: Vector2.zero,
                alignment: TextAnchor.MiddleCenter, style: FontStyle.Normal);
        }

        private void OnEnable()
        {
            input.Enable();
            input.Toggle.performed += OnTogglePerformed;
        }

        private void OnDisable()
        {
            input.Toggle.performed -= OnTogglePerformed;
            input.Disable();
            input.Dispose();
        }

        private void OnTogglePerformed(InputAction.CallbackContext context)
        {
            if (isOpen)
            {
                SetPanelOpen(false);
                return;
            }

            if (buildModeController != null && buildModeController.IsBuildModeActive)
            {
                // Build mode owns left-click (placement) and already has a ghost following the
                // crosshair. Opening a cursor-grabbing, time-freezing panel over it would turn
                // the click meant for Eat into a piece placement - the same reason PauseMenu
                // refuses to open while build mode is active.
                return;
            }

            if (Time.timeScale == 0f)
            {
                // Something else (the pause menu - it's the only other thing that touches
                // timeScale) already froze the game and owns the screen. Don't open behind it,
                // and in particular don't become the second owner of timeScale/the cursor:
                // whichever modal closed last would then restore state the other still needs.
                return;
            }

            SetPanelOpen(true);
        }

        /// <summary>
        /// Called by PauseMenu so one Escape press backs out of the inventory rather than
        /// opening a second modal on top of it.
        /// </summary>
        public void CloseIfOpen()
        {
            if (isOpen)
            {
                SetPanelOpen(false);
            }
        }

        /// <summary>
        /// M9 QC — the panel gained clickable buttons but nothing freed the mouse, and
        /// FirstPersonController locks and hides the cursor for the whole session. A locked
        /// cursor sits at the centre of the screen and cannot be moved, so the Eat button was
        /// physically unclickable: M9's headline feature could not be used at all.
        ///
        /// Freeing the cursor alone isn't enough either, because nothing in this project
        /// consumes UI input away from gameplay input (every action map is hand-built and
        /// always listening): mouse movement toward the button would spin the view, and the
        /// click on it would also swing the melee weapon. Rather than add a third set of
        /// per-system guards, this panel is modal exactly like PauseMenu - it freezes time
        /// while open, which the existing Time.timeScale == 0 guards in FirstPersonController
        /// (look), MeleeAttacker (swing) and BuildModeController (entering build mode) already
        /// handle correctly. Only one modal may be open at a time (see OnTogglePerformed and
        /// PauseMenu.OnTogglePause), so timeScale and the cursor always have a single owner.
        /// </summary>
        private void SetPanelOpen(bool open)
        {
            isOpen = open;
            panel.SetActive(open);
            Time.timeScale = open ? 0f : 1f;

            if (open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (stats != null && stats.IsDead)
            {
                // Die() disables FirstPersonController, which deliberately leaves the cursor
                // free as the player-visible "you are dead" signal. Closing the inventory must
                // not quietly re-capture it.
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (!panel.activeSelf || inventory == null)
            {
                return;
            }

            IReadOnlyDictionary<string, int> counts = inventory.GetAllCounts();
            foreach (ResourceDefinition resource in knownResources)
            {
                if (resource == null || !rows.TryGetValue(resource.id, out Text row))
                {
                    continue;
                }

                int count = counts.TryGetValue(resource.id, out int value) ? value : 0;
                row.text = $"{resource.displayName}: {count}";
            }

            // Recipe rows: grey out anything the player can't currently afford, using the same
            // TryAfford check the build system itself uses, so this reference list never
            // disagrees with what build mode will actually let you place.
            foreach (SheltreSim.Building.BuildPieceDefinition recipe in knownRecipes)
            {
                if (recipe == null || !recipeRows.TryGetValue(recipe.id, out Text recipeRow))
                {
                    continue;
                }

                bool canAfford = SheltreSim.Crafting.CraftingSystem.TryAfford(inventory, recipe.craftingCost);
                recipeRow.color = canAfford ? Color.white : new Color(0.55f, 0.55f, 0.55f);
            }
        }
    }
}
