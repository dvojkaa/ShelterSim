using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
    /// Playtest feedback: "I want an inventory screen (I key)." Passive readout, like
    /// SurvivalHUD — no buttons, no EventSystem needed.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        public Inventory inventory;
        public List<ResourceDefinition> knownResources = new List<ResourceDefinition>();

        private GameObject panel;
        private readonly Dictionary<string, Text> rows = new Dictionary<string, Text>();

        private InventoryInputActions input;

        private void Awake()
        {
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
            panelRect.sizeDelta = new Vector2(500f, 400f);

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

                Text row = CreateLabel(panel.transform, "Row_" + resource.id, $"{resource.displayName}: 0", font, 18, rowY, FontStyle.Normal);
                rows[resource.id] = row;
                rowY -= 34f;
            }

            panel.SetActive(false);
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
            panel.SetActive(!panel.activeSelf);
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
        }
    }
}
