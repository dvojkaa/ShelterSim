using SheltreSim.Saving;
using SheltreSim.Survival;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SheltreSim.Core
{
    public class PauseMenu : MonoBehaviour
    {
        public SheltreSim.Building.BuildModeController buildModeController;
        public SheltreSim.Items.InventoryUI inventoryUi;
        public bool IsOpen { get; private set; }

        private InputActionMap map;
        private InputAction togglePause;
        private GameObject panel;
        private SaveManager saveManager;
        private SurvivalStats stats;
        private Text statusLabel;
        private Button saveButton;
        private Button loadButton;
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

        private void Awake()
        {
            BuildUI();
            map = new InputActionMap("Pause");
            togglePause = map.AddAction("TogglePause", InputActionType.Button);
            togglePause.AddBinding("<Keyboard>/escape");
        }

        private void OnEnable()
        {
            togglePause.performed += OnTogglePause;
            GameMessageBus.OnMessage += OnMessage;
            map.Enable();
        }

        private void OnDisable()
        {
            togglePause.performed -= OnTogglePause;
            GameMessageBus.OnMessage -= OnMessage;
            map.Disable();
            CloseIfOpen();
        }

        private void OnDestroy() => map?.Dispose();

        private void OnTogglePause(InputAction.CallbackContext context)
        {
            if (IsOpen)
            {
                CloseIfOpen();
                return;
            }
            // A build cancellation and this callback may arrive in either order.
            if (buildModeController != null &&
                (buildModeController.IsBuildModeActive || buildModeController.ConsumedCancelThisFrame)) return;
            if (inventoryUi != null && inventoryUi.IsOpen)
            {
                inventoryUi.CloseIfOpen();
                return;
            }
            if (ModalUI.IsOpen) return;

            if (stats == null) stats = Object.FindFirstObjectByType<SurvivalStats>();
            if (stats != null && stats.IsDead) return;
            if (!ModalUI.TryOpen(this, CloseIfOpen)) return;

            IsOpen = true;
            panel.SetActive(true);
            if (saveManager == null) saveManager = Object.FindFirstObjectByType<SaveManager>();
            RefreshSaveButtons();
            statusLabel.text = saveManager != null && saveManager.HasSave
                ? "A saved game is available. Loading replaces your current progress."
                : "No saved game yet. Save here or press F5 during play.";
        }

        public void CloseIfOpen()
        {
            if (!IsOpen) return;
            IsOpen = false;
            panel.SetActive(false);
            ModalUI.Close(this);
        }

        private void OnMessage(string message)
        {
            if (!IsOpen) return;
            statusLabel.text = message;
            RefreshSaveButtons();
        }

        private void RefreshSaveButtons()
        {
            saveButton.interactable = saveManager != null && (stats == null || !stats.IsDead);
            loadButton.interactable = saveManager != null && saveManager.HasSave;
        }

        private void BuildUI()
        {
            Canvas canvas = GameUI.CreateCanvas(transform, "PauseCanvas", 100, true);
            panel = GameUI.Backdrop(canvas.transform, "PausePanel");
            Transform card = GameUI.Panel(panel.transform, "Card", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 735f), GameUI.PanelColor, true).transform;
            GameUI.Label(card, "Title", "PAUSED", TopLeft, TopLeft, new Vector2(32f, -28f),
                new Vector2(896f, 50f), 38, GameUI.Accent, bold: true);
            GameUI.Label(card, "Subtitle", "Take a breath. The world is paused.", TopLeft, TopLeft,
                new Vector2(32f, -88f), new Vector2(896f, 30f), 20, GameUI.Muted);

            GameUI.Button(card, "ResumeButton", "Resume  /  Esc", TopLeft, TopLeft,
                new Vector2(32f, -150f), new Vector2(270f, 54f), CloseIfOpen, true);
            saveButton = GameUI.Button(card, "SaveButton", "Save game  /  F5", TopLeft, TopLeft,
                new Vector2(32f, -218f), new Vector2(270f, 54f), () => { saveManager?.TrySave(); });
            loadButton = GameUI.Button(card, "LoadButton", "Load saved game  /  F9", TopLeft, TopLeft,
                new Vector2(32f, -286f), new Vector2(270f, 54f), () => { saveManager?.TryLoad(); });
            GameUI.Button(card, "QuitButton", "Quit", TopLeft, TopLeft,
                new Vector2(32f, -354f), new Vector2(270f, 54f), QuitGame);

            GameUI.Label(card, "ControlsHeading", "FIELD CONTROLS", TopLeft, TopLeft,
                new Vector2(350f, -150f), new Vector2(560f, 30f), 21, GameUI.Accent, bold: true);
            GameUI.Label(card, "Controls",
                "WASD   Move     /     Mouse   Look\nSpace   Jump     /     Shift   Sprint\nE   Interact     /     Left click   Melee\nI or Tab   Inventory and supplies\nB   Build mode     /     Wheel   Choose piece\nR   Rotate     /     Left click   Place\nEscape   Leave build mode / close menu\nF5   Quick save     /     F9   Quick load",
                TopLeft, TopLeft, new Vector2(350f, -196f), new Vector2(578f, 240f), 20);
            statusLabel = GameUI.Label(card, "SaveStatus", string.Empty, TopLeft, TopLeft,
                new Vector2(32f, -462f), new Vector2(896f, 60f), 19, GameUI.Accent);
            GameUI.Label(card, "Advice",
                "Prepare supplies and a Water Collector before using the red bunker console.\nFood and water stay full during preparation; raiders can still hurt you.\nAfter the alarm, scavenge, return home, eat, drink and rest. Save regularly.",
                TopLeft, TopLeft, new Vector2(32f, -554f), new Vector2(896f, 96f), 19, GameUI.Muted);
            SheltreSim.Audio.AudioSettingsPanel.Build(card, new Vector2(32f, -664f));
            panel.SetActive(false);
        }

        private static void QuitGame()
        {
            ModalUI.DismissCurrent();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
