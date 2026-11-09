using System.Collections.Generic;
using System.Text;
using SheltreSim.Crafting;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SheltreSim.Building
{
    /// <summary>
    /// G opens every build piece by category, with what you have against what it needs. A click closes the palette and enters
    /// build mode on that piece. BeginBuild must come after closing: build mode refuses to run while the game is paused.
    /// </summary>
    public sealed class HomesteadPalette : MonoBehaviour
    {
        private const int RowHeight = 70;
        private static HomesteadPalette instance;
        private static bool hooked;

        public static HomesteadPalette Active => instance;
        public bool IsOpen => open;

        private readonly Dictionary<string, Button> rowButtons = new Dictionary<string, Button>();
        private Canvas canvas;
        private RectTransform rows;
        private Text title;
        private readonly List<Button> categoryButtons = new List<Button>();
        private BuildModeController controller;
        private int category;
        private bool open, closeRequested;
        private InputAction toggle, cancel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; hooked = false; }

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
            if (instance == null) new GameObject("HomesteadPalette").AddComponent<HomesteadPalette>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            toggle = new InputAction("BuildPalette", InputActionType.Button, "<Keyboard>/g");
            cancel = new InputAction("CloseBuildPalette", InputActionType.Button, "<Keyboard>/escape");
        }

        private void OnEnable()
        {
            if (toggle == null) return;
            toggle.Enable(); toggle.performed += OnToggle;
            cancel.Enable(); cancel.performed += OnCancel;
        }

        private void OnDisable()
        {
            Close();
            if (toggle == null) return;
            toggle.performed -= OnToggle; toggle.Disable();
            cancel.performed -= OnCancel; cancel.Disable();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            toggle?.Dispose();
            cancel?.Dispose();
        }

        private void OnToggle(InputAction.CallbackContext _)
        {
            if (open) closeRequested = true;
            else Open();
        }

        private void OnCancel(InputAction.CallbackContext _) { if (open) closeRequested = true; }
        private void LateUpdate() { if (closeRequested) Close(); }

        public bool Open()
        {
            if (open) return true;
            controller = controller != null ? controller : FindFirstObjectByType<BuildModeController>();
            if (controller == null || controller.availablePieces.Count == 0) return false;
            if (controller.GetComponentInParent<SheltreSim.Survival.SurvivalStats>()?.IsDead ?? false) return false;
            if (!ModalUI.TryOpen(this, Close)) return false;
            // Build mode can't continue behind a modal. Leave it now and not a frame later.
            controller.ExitBuildMode();
            if (canvas == null) BuildUI();
            open = true;
            closeRequested = false;
            canvas.gameObject.SetActive(true);
            ShowCategory(category);
            return true;
        }

        public void Close()
        {
            closeRequested = false;
            if (!open) return;
            open = false;
            if (canvas != null) canvas.gameObject.SetActive(false);
            ModalUI.Close(this);
        }

        private void BuildUI()
        {
            canvas = GameUI.CreateCanvas(transform, "BuildPalette", 130, true);
            GameUI.Backdrop(canvas.transform, "Backdrop");
            Transform panel = GameUI.Panel(canvas.transform, "Panel", Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(1280f, 880f), GameUI.PanelColor).transform;
            title = GameUI.Label(panel, "Title", "BUILD", new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -22f), new Vector2(1200f, 44f), 28, GameUI.Accent, TextAnchor.MiddleCenter);
            GameUI.Label(panel, "Hint", "Click a piece to build it. Green means enough in your pack and nearby crates. In build mode: R turns, wheel cycles, V repairs, X dismantles.",
                new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -68f), new Vector2(1200f, 30f), 18, GameUI.Muted, TextAnchor.MiddleCenter);
            for (int i = 0; i < HomesteadCatalog.Categories.Length; i++)
            {
                int index = i;
                categoryButtons.Add(GameUI.Button(panel, "Category_" + HomesteadCatalog.Categories[i], HomesteadCatalog.Categories[i], new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(24f, -112f - i * 66f), new Vector2(230f, 58f), () => ShowCategory(index)));
            }
            rows = GameUI.ScrollContent(panel, "Pieces", new Vector2(274f, -112f), new Vector2(982f, 690f));
            GameUI.Button(panel, "Close", "CLOSE [ESC]", new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 16f), new Vector2(260f, 44f), Close);
        }

        private void ShowCategory(int index)
        {
            category = Mathf.Clamp(index, 0, HomesteadCatalog.Categories.Length - 1);
            string name = HomesteadCatalog.Categories[category];
            title.text = "BUILD: " + name.ToUpperInvariant();
            for (int i = 0; i < categoryButtons.Count; i++) categoryButtons[i].interactable = i != category;

            for (int i = rows.childCount - 1; i >= 0; i--)
            {
                GameObject old = rows.GetChild(i).gameObject;
                old.SetActive(false);
                Destroy(old);
            }
            rowButtons.Clear();

            int row = 0;
            for (int i = 0; i < controller.availablePieces.Count; i++)
            {
                BuildPieceDefinition piece = controller.availablePieces[i];
                if (piece == null || HomesteadCatalog.CategoryFor(piece.id) != name) continue;
                AddRow(piece, i, row++);
            }
            if (row == 0)
                GameUI.Label(rows, "Empty", "Nothing to build in this category yet.", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(900f, 40f), 20, GameUI.Muted);
            rows.sizeDelta = new Vector2(0f, Mathf.Max(row, 1) * RowHeight + 8f);
            rows.anchoredPosition = Vector2.zero;
        }

        private void AddRow(BuildPieceDefinition piece, int pieceIndex, int row)
        {
            Button button = GameUI.Button(rows, "Piece_" + piece.id, string.Empty, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(4f, -4f - row * RowHeight), new Vector2(960f, RowHeight - 6f), () => Choose(pieceIndex));
            bool affordable = CraftingSystem.TryAfford(controller.inventory, piece.craftingCost);
            Transform t = button.transform;
            GameUI.Label(t, "Name", piece.displayName, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(14f, 0f), new Vector2(270f, 56f), 21,
                affordable ? Color.white : GameUI.Muted, TextAnchor.MiddleLeft, true);
            GameUI.Label(t, "Cost", CostText(piece), new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(292f, 0f), new Vector2(656f, 58f), 18, Color.white, TextAnchor.MiddleLeft);
            rowButtons[piece.id] = button;
        }

        private string CostText(BuildPieceDefinition piece)
        {
            if (piece.craftingCost == null || piece.craftingCost.Count == 0) return "Free";
            StringBuilder text = new StringBuilder();
            foreach (CraftingCost line in piece.craftingCost)
            {
                if (line == null || line.resource == null || line.amount <= 0) continue;
                int owned = CraftingSystem.CountAvailable(controller.inventory, line.resource.id);
                if (text.Length > 0) text.Append("    ");
                string colour = ColorUtility.ToHtmlStringRGB(owned >= line.amount ? GameUI.Accent : GameUI.Danger);
                text.Append("<color=#").Append(colour).Append('>').Append(line.resource.displayName).Append(' ')
                    .Append(owned).Append(" : ").Append(line.amount).Append("</color>");
            }
            return text.Length > 0 ? text.ToString() : "Free";
        }

        private void Choose(int pieceIndex)
        {
            Close();
            controller.BeginBuild(pieceIndex);
        }

        /// <summary>Probe only: the row of a piece in its own category, as shown.</summary>
        public Button DebugButtonFor(string id)
        {
            string name = HomesteadCatalog.CategoryFor(id);
            ShowCategory(System.Array.IndexOf(HomesteadCatalog.Categories, name));
            return rowButtons.TryGetValue(id, out Button button) ? button : null;
        }
    }
}
