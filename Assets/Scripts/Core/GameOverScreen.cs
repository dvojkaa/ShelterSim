using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SheltreSim.Core
{
    /// <summary>
    /// M13 — an Opus-model review flagged that SurvivalStats.Die() freezes the player (disables
    /// FirstPersonController/PlayerInteractor, unlocks the cursor) with no on-screen feedback at
    /// all: the only way out was F9 to load an old save, which doesn't exist if the player never
    /// saved. This gives death an actual screen with a Restart option. Built the same way
    /// PauseMenu builds its own UI (self-sufficient: creates its own EventSystem if one doesn't
    /// exist yet, same reasoning as PauseMenu's own comment on that).
    ///
    /// Deliberately does NOT touch Time.timeScale: by the time Die() calls Show(), the player is
    /// already fully frozen (controller/interactor disabled), and SceneManager.LoadScene works
    /// regardless of timeScale. Setting it to 0 here would just be one more flag this screen has
    /// to remember to reset, for a screen that never needs to coexist with gameplay continuing.
    /// </summary>
    public class GameOverScreen : MonoBehaviour
    {
        private GameObject panel;

        private void Awake()
        {
            EnsureEventSystem();
            BuildUI();
        }

        /// <summary>
        /// Called by SurvivalStats on death. Unlocks/shows the cursor defensively — Die()
        /// already does this via FirstPersonController.OnDisable, but this screen must remain
        /// usable even if that wiring ever changes.
        /// </summary>
        public void Show()
        {
            panel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
            // Awake runs both when an editor tool adds this component (at edit-time, saved into
            // the scene) and again when Play Mode actually starts (fresh scene load) — without
            // this guard that leaves two stacked canvases, same guard PauseMenu/SurvivalHUD/
            // InventoryUI all have for the same reason.
            Transform stale = transform.Find("GameOverCanvas");
            if (stale != null)
            {
                DestroyImmediate(stale.gameObject);
            }

            GameObject canvasObject = new GameObject("GameOverCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Must always win over every other panel in the scene, including PauseMenu's own
            // sortingOrder 100 — death is the one state that should never be obscured.
            canvas.sortingOrder = 200;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObject.AddComponent<GraphicRaycaster>();

            panel = new GameObject("GameOverPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.85f);

            CreateLabel(panel.transform, "Title", "You Died", new Vector2(0f, 60f), 48);
            CreateButton(panel.transform, "RestartButton", "Restart", new Vector2(0f, -40f), RestartGame);

            panel.SetActive(false);
        }

        private static void RestartGame()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
            rect.sizeDelta = new Vector2(500f, 80f);

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
            // Selectable.targetGraphic must be assigned explicitly — AddComponent<Button>() via
            // `new GameObject(...)` does not auto-wire it to the sibling Image the way the
            // inspector's "UI > Button" menu item does (see CLAUDE.md).
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(() => onClick());

            CreateLabel(buttonObject.transform, name + "_Label", label, Vector2.zero, 20);
        }
    }
}
