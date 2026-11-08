using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SheltreSim.UI
{
    /// <summary>Shared primitive UI construction; no imported sprites or font packages required.</summary>
    public static class GameUI
    {
        public static readonly Color PanelColor = new Color(0.055f, 0.085f, 0.09f, 0.97f);
        public static readonly Color RowColor = new Color(0.10f, 0.15f, 0.16f, 0.96f);
        public static readonly Color Accent = new Color(0.63f, 0.87f, 0.66f);
        public static readonly Color Muted = new Color(0.66f, 0.74f, 0.75f);
        public static readonly Color Warning = new Color(1f, 0.72f, 0.36f);
        public static readonly Color Danger = new Color(1f, 0.39f, 0.32f);

        public static Canvas CreateCanvas(Transform parent, string name, int order, bool interactive)
        {
            Transform stale = parent.Find(name);
            if (stale != null)
            {
                stale.gameObject.SetActive(false);
                if (Application.isPlaying) Object.Destroy(stale.gameObject);
                else Object.DestroyImmediate(stale.gameObject);
            }

            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            obj.transform.SetParent(parent, false);
            Canvas canvas = obj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            CanvasScaler scaler = obj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            if (interactive)
            {
                obj.AddComponent<GraphicRaycaster>();
                EnsureEventSystem();
            }
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            EventSystem existing = Object.FindFirstObjectByType<EventSystem>();
            if (existing != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        public static Image Panel(Transform parent, string name, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, Color color, bool interactive = false)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            SetRect(obj.GetComponent<RectTransform>(), anchor, pivot, position, size);
            Image image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = interactive;
            return image;
        }

        public static GameObject Backdrop(Transform parent, string name)
        {
            Image image = Panel(parent, name, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero,
                new Color(0.015f, 0.025f, 0.03f, 0.85f), true);
            RectTransform rect = image.rectTransform;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return image.gameObject;
        }

        public static Text Label(Transform parent, string name, string value, Vector2 anchor,
            Vector2 pivot, Vector2 position, Vector2 size, int fontSize = 20,
            Color? color = null, TextAnchor alignment = TextAnchor.UpperLeft, bool bold = false)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            obj.transform.SetParent(parent, false);
            SetRect(obj.GetComponent<RectTransform>(), anchor, pivot, position, size);
            Text text = obj.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = color ?? Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        public static Button Button(Transform parent, string name, string label, Vector2 anchor,
            Vector2 pivot, Vector2 position, Vector2 size, UnityAction onClick, bool primary = false)
        {
            Image image = Panel(parent, name, anchor, pivot, position, size,
                primary ? new Color(0.22f, 0.38f, 0.29f) : new Color(0.16f, 0.23f, 0.25f), true);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.7f, 0.8f, 0.75f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.55f);
            button.colors = colors;
            // Explicit keyboard shortcuts handle menu navigation. Space must remain jump,
            // and Enter must not activate a stale selected button after a menu is reopened.
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(onClick);
            Label(image.transform, "Label", label, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, size - new Vector2(12f, 4f), 20, alignment: TextAnchor.MiddleCenter);
            return button;
        }

        public static RectTransform ScrollContent(Transform parent, string name, Vector2 position, Vector2 size)
        {
            Image viewportImage = Panel(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f),
                position, size, new Color(0f, 0f, 0f, 0.04f), true);
            RectTransform viewport = viewportImage.rectTransform;
            viewportImage.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = viewportImage.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;
            GameObject contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewport, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            scroll.content = content;
            return content;
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
