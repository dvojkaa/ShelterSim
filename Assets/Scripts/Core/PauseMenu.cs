using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SheltreSim.Core
{
    /// <summary>
    /// Escape toggles a Resume/Quit panel when the player isn't in build mode (build mode
    /// already owns Escape for CancelBuild — this defers to it rather than double-firing on
    /// the same keypress). First interactive (clickable) UI in the project, so this is also
    /// responsible for creating the scene's EventSystem if one doesn't exist yet — kept
    /// self-sufficient (doesn't rely on a sibling editor tool to set that prerequisite up),
    /// so this component works correctly however it gets added to a scene.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public SheltreSim.Building.BuildModeController buildModeController;

        /// <summary>
        /// M9 QC — wired by M8MenusSetup. The inventory panel became modal in M9 (it takes
        /// the cursor and freezes time so its Eat button is clickable at all), so Escape has
        /// to back out of it rather than open a second modal on top: otherwise Resume would
        /// unfreeze time and re-lock the cursor while the inventory was still on screen.
        /// </summary>
        public SheltreSim.Items.InventoryUI inventoryUi;

        private InputActionMap map;
        private InputAction togglePause;
        private GameObject panel;
        private bool isOpen;

        private void Awake()
        {
            EnsureEventSystem();
            BuildUI();

            map = new InputActionMap("Pause");
            // Keyboard-only, same deliberate choice SaveInputActions makes for F5/F9 — this is
            // a meta/UI action, not core gameplay, so skipping a gamepad binding is acceptable.
            togglePause = map.AddAction("TogglePause", InputActionType.Button);
            togglePause.AddBinding("<Keyboard>/escape");
        }

        private void OnEnable()
        {
            map.Enable();
            togglePause.performed += OnTogglePause;
        }

        private void OnDisable()
        {
            togglePause.performed -= OnTogglePause;
            map.Disable();
            map.Dispose();
        }

        private void OnTogglePause(InputAction.CallbackContext context)
        {
            if (buildModeController != null &&
                (buildModeController.IsBuildModeActive || buildModeController.ConsumedCancelThisFrame))
            {
                // Build mode's own Escape binding (CancelBuild) owns this keypress. The second
                // test matters because both action maps get the same keypress in the same frame
                // with no guaranteed callback order: if CancelBuild ran first, IsBuildModeActive
                // is already false here and this menu would pop open on the very press that was
                // meant to leave build mode. See BuildModeController.ConsumedCancelThisFrame.
                return;
            }

            if (inventoryUi != null && inventoryUi.IsOpen)
            {
                // Inventory is the thing currently on screen and currently owning timeScale /
                // the cursor. Escape closes it (and only it) — single owner at all times.
                inventoryUi.CloseIfOpen();
                return;
            }

            if (isOpen)
            {
                ClosePause();
            }
            else
            {
                OpenPause();
            }
        }

        private void OpenPause()
        {
            isOpen = true;
            panel.SetActive(true);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ClosePause()
        {
            isOpen = false;
            panel.SetActive(false);
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildUI()
        {
            // Awake runs both when an editor tool adds this component (at edit-time, saved
            // into the scene) and again when Play Mode actually starts (fresh scene load) -
            // without this guard that leaves two stacked canvases, same issue SurvivalHUD and
            // InventoryUI guard against elsewhere in this project.
            Transform stale = transform.Find("PauseCanvas");
            if (stale != null)
            {
                DestroyImmediate(stale.gameObject);
            }

            GameObject canvasObject = new GameObject("PauseCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // SurvivalHUD and InventoryUI each build their own independent overlay canvas and
            // all three default to sortingOrder 0, where draw order between root canvases is
            // incidental. This one is the modal, so pin it on top rather than leaving it to
            // chance — the inventory panel in particular occupies the same centre of screen as
            // the Resume/Quit buttons.
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObject.AddComponent<GraphicRaycaster>();

            panel = new GameObject("PausePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.8f);

            CreateLabel(panel.transform, "Title", "Paused", new Vector2(0f, 150f), 36);
            CreateButton(panel.transform, "ResumeButton", "Resume", new Vector2(0f, 20f), ClosePause);
            CreateButton(panel.transform, "QuitButton", "Quit", new Vector2(0f, -60f), QuitGame);

            panel.SetActive(false);
        }

        private static void CreateLabel(Transform parent, string name, string text, Vector2 anchoredPosition, int fontSize)
        {
            GameObject labelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(parent, false);

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(400f, 60f);

            Text label = labelObject.GetComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
        }

        private static void CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, Action onClick)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(220f, 50f);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.25f, 0.25f, 0.25f);

            Button button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(() => onClick());

            CreateLabel(buttonObject.transform, name + "_Label", label, Vector2.zero, 20);
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
