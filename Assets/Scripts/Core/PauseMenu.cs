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
        private Button quitButton, mainMenuButton;
        private float confirmDestructiveUntil;
        private SheltreSim.UI.DisplaySettingsPanel displaySettings;
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

        private void Update()
        {
            if (confirmDestructiveUntil > 0f && Time.unscaledTime > confirmDestructiveUntil)
                ClearDestructiveConfirmation();
        }

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
            ClearDestructiveConfirmation();
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
            displaySettings?.CancelAndClose();
            ClearDestructiveConfirmation();
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
            displaySettings = canvas.gameObject.AddComponent<SheltreSim.UI.DisplaySettingsPanel>();
            displaySettings.Initialize(canvas.transform);
            Transform card = GameUI.Panel(panel.transform, "Card", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 780f), GameUI.PanelColor, true).transform;
            GameUI.Label(card, "Title", "PAUSED", TopLeft, TopLeft, new Vector2(32f, -28f),
                new Vector2(896f, 50f), 38, GameUI.Accent, bold: true);
            GameUI.Label(card, "Subtitle", "Take a breath. The world is paused.", TopLeft, TopLeft,
                new Vector2(32f, -88f), new Vector2(896f, 30f), 20, GameUI.Muted);

            GameUI.Button(card, "ResumeButton", "Resume  /  Esc", TopLeft, TopLeft,
                new Vector2(32f, -150f), new Vector2(270f, 54f), CloseIfOpen, true);
            saveButton = GameUI.Button(card, "SaveButton", "Save game  /  F5", TopLeft, TopLeft,
                new Vector2(32f, -218f), new Vector2(270f, 54f), () => { saveManager?.TrySave(allowWhenModal: true); });
            loadButton = GameUI.Button(card, "LoadButton", "Load saved game  /  F9", TopLeft, TopLeft,
                new Vector2(32f, -286f), new Vector2(270f, 54f), () => { saveManager?.TryLoad(); });
            quitButton = GameUI.Button(card, "QuitButton", "Quit", TopLeft, TopLeft,
                new Vector2(32f, -354f), new Vector2(270f, 54f), RequestQuit);
            mainMenuButton = GameUI.Button(card, "MainMenuButton", "Main menu", TopLeft, TopLeft,
                new Vector2(32f, -420f), new Vector2(270f, 54f), RequestMainMenu);
            GameUI.Button(card, "DisplaySettingsButton", "Display settings", TopLeft, TopLeft,
                new Vector2(32f, -480f), new Vector2(270f, 40f), () => displaySettings?.Open());

            GameUI.Label(card, "ControlsHeading", "FIELD CONTROLS", TopLeft, TopLeft,
                new Vector2(350f, -150f), new Vector2(560f, 30f), 21, GameUI.Accent, bold: true);
            GameUI.Label(card, "Controls",
                "WASD   Move     /     Mouse   Look\n" +
                "Space   Jump     /     Shift   Sprint\n" +
                "Left Ctrl   Crouch (hold, harder to spot)\n" +
                "E   Interact     /     Left click   Attack\n" +
                "1 / 2   Melee / gun     /     R   Reload\n" +
                "3 / 4 / 5   Quick use: medicine, food, drink\n" +
                "F   Flashlight (loads an Energy Cell when empty)\n" +
                "I or Tab   Inventory     /     C   Craft\n" +
                "B   Build mode     /     Wheel   Choose piece\n" +
                "R   Rotate     /     Left click   Place\n" +
                "V   Repair     /     X   Dismantle (in build mode)\n" +
                "G   Blueprints     /     M   Map\n" +
                "Escape   Leave build mode / close menu\n" +
                "F5   Quick save     /     F9   Quick load",
                TopLeft, TopLeft, new Vector2(350f, -186f), new Vector2(590f, 310f), 17);
            statusLabel = GameUI.Label(card, "SaveStatus", string.Empty, TopLeft, TopLeft,
                new Vector2(32f, -528f), new Vector2(896f, 40f), 19, GameUI.Accent);
            GameUI.Label(card, "Advice",
                "Prepare supplies and a Water Collector before using the red bunker console.\nFood and water stay full during preparation; raiders can still hurt you.\nAfter the alarm, scavenge, return home, eat, drink and rest. Save regularly.",
                TopLeft, TopLeft, new Vector2(32f, -574f), new Vector2(896f, 80f), 18, GameUI.Muted);
            SheltreSim.Audio.AudioSettingsPanel.Build(card, new Vector2(32f, -662f));
            GameplaySettingsPanel.Build(card, new Vector2(32f, -716f));
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

        private void RequestQuit()
        {
            if (confirmDestructiveUntil > Time.unscaledTime)
            {
                ClearDestructiveConfirmation();
                QuitGame();
                return;
            }
            ArmDestructiveConfirmation("Quit without saving? Press Quit again within 5 seconds.", "Confirm Quit");
        }

        private void RequestMainMenu()
        {
            if (confirmDestructiveUntil > Time.unscaledTime)
            {
                ClearDestructiveConfirmation();
                CloseIfOpen();
                MainMenuController.ReturnToMenu();
                return;
            }
            ArmDestructiveConfirmation("Return to the menu and discard this run? Press Main menu again within 5 seconds.", "Confirm Main Menu");
        }

        private void ArmDestructiveConfirmation(string message, string buttonText)
        {
            confirmDestructiveUntil = Time.unscaledTime + 5f;
            statusLabel.text = message;
            quitButton.GetComponentInChildren<Text>().text = "Confirm Quit";
            mainMenuButton.GetComponentInChildren<Text>().text = "Confirm Main Menu";
        }

        private void ClearDestructiveConfirmation()
        {
            confirmDestructiveUntil = 0f;
            if (quitButton != null) quitButton.GetComponentInChildren<Text>().text = "Quit";
            if (mainMenuButton != null) mainMenuButton.GetComponentInChildren<Text>().text = "Main menu";
        }

        private void ReturnToMainMenu()
        {
            CloseIfOpen();
            MainMenuController.ReturnToMenu();
        }
    }
}
