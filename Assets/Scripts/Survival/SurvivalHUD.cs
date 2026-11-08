using System.Collections.Generic;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SheltreSim.Survival
{
    public class SurvivalHUD : MonoBehaviour
    {
        public SurvivalStats stats;

        private Image healthFill;
        private Image hungerFill;
        private Image thirstFill;

        private Text healthLabel;
        private Text hungerLabel;
        private Text thirstLabel;

        private Text interactPromptLabel;

        private const int MessageFeedLines = 3;
        private const float MessageLifetimeSeconds = 4f;

        private class FeedEntry
        {
            public string text;
            public float expireTime;
        }

        private readonly List<FeedEntry> feedEntries = new List<FeedEntry>();
        private Text[] feedLabels;

        private bool loggedMissingStats;

        private void Awake()
        {
            // Awake() runs twice across this component's real lifetime: once when an editor
            // tool adds it (AddComponent triggers Awake immediately in-editor, and that canvas
            // gets saved into the scene), and again when Play Mode actually starts (a fresh
            // scene load re-runs every component's Awake). Without this guard, that leaves two
            // "HUD_Canvas" objects stacked on screen — the edit-time one frozen forever at its
            // initial fillAmount, on top of (or behind) the live one this Update() actually
            // drives, reading as "the HUD bars never move."
            Transform stale = transform.Find("HUD_Canvas");
            if (stale != null)
            {
                DestroyImmediate(stale.gameObject);
            }

            GameObject canvasObject = new GameObject("HUD_Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Deliberately NO GraphicRaycaster and no EventSystem: this HUD is a passive
            // readout, nothing on it is clickable. It used to have one harmlessly, because
            // every graphic on it lived in the top-left corner where nothing else does. The
            // crosshair / interact prompt / message feed added in M12 are the first HUD
            // graphics in the MIDDLE of the screen, and Unity's Images default to
            // raycastTarget = true — so with a raycaster still attached, the 600x32 invisible
            // prompt label sat right on top of InventoryUI's Eat/Equip buttons (that panel is
            // centred and both canvases are sortingOrder 0, where the winner between two root
            // overlay canvases is incidental). That is this project's recurring "a UI control
            // that compiles fine but can't actually be clicked" bug, one canvas removed.
            // raycastTarget is also cleared on every graphic below, so re-adding a raycaster
            // here later still can't swallow a click.
            healthFill = CreateBar(canvasObject.transform, "HealthBar", -20f, new Color(0.8f, 0.1f, 0.1f), out healthLabel);
            hungerFill = CreateBar(canvasObject.transform, "HungerBar", -54f, new Color(0.6f, 0.4f, 0.1f), out hungerLabel);
            thirstFill = CreateBar(canvasObject.transform, "ThirstBar", -88f, new Color(0.2f, 0.5f, 0.9f), out thirstLabel);

            CreateCrosshair(canvasObject.transform);
            interactPromptLabel = CreatePromptLabel(canvasObject.transform);
            feedLabels = CreateMessageFeed(canvasObject.transform);

            // GameMessageBus is the single call site gameplay scripts use instead of a bare
            // Debug.Log for one-off player feedback ("Found 3 Wood", "Hit Raider_1 for 20
            // damage") - it still logs to the Console internally, this just also surfaces it
            // here. Subscribed in Awake / unsubscribed in OnDestroy (NOT OnDisable) - this
            // project has a documented trap where unsubscribing in OnDisable breaks components
            // that get disabled and later re-enabled, since there is nothing left to re-sub.
            GameMessageBus.OnMessage += ShowMessage;
        }

        private void OnDestroy()
        {
            GameMessageBus.OnMessage -= ShowMessage;
        }

        private static void CreateCrosshair(Transform parent)
        {
            // Small centered cross made of two thin bars rather than a single dot - more
            // legible against varied backgrounds, still a trivial primitive (no sprite assets
            // exist in this project; a null-sprite Image renders as a plain solid rectangle).
            GameObject crosshair = new GameObject("Crosshair", typeof(RectTransform));
            crosshair.transform.SetParent(parent, false);

            RectTransform crosshairRect = crosshair.GetComponent<RectTransform>();
            crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
            crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
            crosshairRect.pivot = new Vector2(0.5f, 0.5f);
            crosshairRect.anchoredPosition = Vector2.zero;
            crosshairRect.sizeDelta = Vector2.zero;

            CreateCrosshairBar(crosshair.transform, new Vector2(12f, 2f));
            CreateCrosshairBar(crosshair.transform, new Vector2(2f, 12f));
        }

        private static void CreateCrosshairBar(Transform parent, Vector2 size)
        {
            GameObject bar = new GameObject("CrosshairBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bar.transform.SetParent(parent, false);

            RectTransform rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            Image image = bar.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.85f);
            image.raycastTarget = false;
        }

        private static Text CreatePromptLabel(Transform parent)
        {
            // Sits just below the crosshair - this is the primary readout for interaction
            // prompts ("Press E to open", "Press E to search"), replacing the Console log as
            // the thing a player actually looks at while playing.
            GameObject promptObject = new GameObject("InteractPrompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            promptObject.transform.SetParent(parent, false);

            RectTransform rect = promptObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -36f);
            rect.sizeDelta = new Vector2(600f, 32f);

            Text label = promptObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = string.Empty;
            label.raycastTarget = false;

            return label;
        }

        private static Text[] CreateMessageFeed(Transform parent)
        {
            // Last few one-off events ("Found 3 Wood, 2 Water", "Hit Raider_1 for 20 damage"),
            // bottom-left, newest on the bottom line, each line independently expiring after a
            // few seconds.
            Text[] labels = new Text[MessageFeedLines];
            for (int i = 0; i < MessageFeedLines; i++)
            {
                GameObject lineObject = new GameObject("MessageFeedLine" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                lineObject.transform.SetParent(parent, false);

                RectTransform rect = lineObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0f, 0f);
                rect.anchoredPosition = new Vector2(20f, 20f + i * 26f);
                rect.sizeDelta = new Vector2(500f, 24f);

                Text label = lineObject.GetComponent<Text>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 16;
                label.alignment = TextAnchor.LowerLeft;
                label.color = Color.white;
                label.text = string.Empty;
                label.raycastTarget = false;

                labels[i] = label;
            }

            return labels;
        }

        private static Image CreateBar(Transform parent, string name, float yOffset, Color fillColor, out Text label)
        {
            // No sprite assets exist in this project (low-poly/primitive style per design notes) —
            // Image with sprite == null renders as a plain solid color via Unity's default texture,
            // which is fine and intentional here.
            GameObject background = new GameObject(name + "_BG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background.transform.SetParent(parent, false);

            RectTransform bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 1f);
            bgRect.anchorMax = new Vector2(0f, 1f);
            bgRect.pivot = new Vector2(0f, 1f);
            bgRect.anchoredPosition = new Vector2(20f, yOffset);
            bgRect.sizeDelta = new Vector2(200f, 24f);

            Image bgImage = background.GetComponent<Image>();
            bgImage.type = Image.Type.Simple;
            bgImage.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
            bgImage.raycastTarget = false;

            GameObject fill = new GameObject(name + "_Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(background.transform, false);

            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0f, 1f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero;

            Image fillImage = fill.GetComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.color = fillColor;
            fillImage.fillAmount = 1f;
            fillImage.raycastTarget = false;

            // Label sits on top of the bar (same rect as the background), showing the name
            // and live numeric value in one Text — e.g. "Health 72/100". Plain legacy Text,
            // not TextMeshPro — this project doesn't have the TMP package installed.
            GameObject labelObject = new GameObject(name + "_Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(background.transform, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;

            label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 14;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = name;
            label.raycastTarget = false;

            return fillImage;
        }

        private void Update()
        {
            // Runs regardless of whether stats is assigned - the prompt/message feed are
            // independent of survival stats and shouldn't freeze just because that reference
            // is missing.
            UpdateMessageFeed();

            if (stats == null)
            {
                if (!loggedMissingStats)
                {
                    Debug.LogError($"{nameof(SurvivalHUD)} on '{name}' has no stats assigned.");
                    loggedMissingStats = true;
                }
                return;
            }

            healthFill.fillAmount = Mathf.Clamp01(stats.health / stats.maxHealth);
            hungerFill.fillAmount = Mathf.Clamp01(stats.hunger / stats.maxHunger);
            thirstFill.fillAmount = Mathf.Clamp01(stats.thirst / stats.maxThirst);

            healthLabel.text = $"Health {stats.health:F0}/{stats.maxHealth:F0}";
            hungerLabel.text = $"Hunger {stats.hunger:F0}/{stats.maxHunger:F0}";
            thirstLabel.text = $"Thirst {stats.thirst:F0}/{stats.maxThirst:F0}";
        }

        /// <summary>
        /// Updates the on-screen interaction prompt - the primary readout for "Press E to
        /// open" / "Press E to search" style text, called every frame by PlayerInteractor
        /// (including with an empty/null string to clear it when nothing is focused).
        /// </summary>
        public void SetInteractPrompt(string text)
        {
            if (interactPromptLabel == null)
            {
                return;
            }

            interactPromptLabel.text = text ?? string.Empty;
        }

        /// <summary>
        /// Pushes a one-off message ("Found 3 Wood, 2 Water", "Hit Raider_1 for 20 damage")
        /// onto the small on-screen feed. Each line expires independently a few seconds after
        /// it was shown, consumed in Update() rather than via a per-message Coroutine so the
        /// feed survives this component being disabled/re-enabled without dangling routines.
        /// </summary>
        public void ShowMessage(string text)
        {
            if (string.IsNullOrEmpty(text) || feedEntries == null)
            {
                return;
            }

            feedEntries.Add(new FeedEntry { text = text, expireTime = Time.time + MessageLifetimeSeconds });
            while (feedEntries.Count > MessageFeedLines)
            {
                feedEntries.RemoveAt(0);
            }
        }

        private void UpdateMessageFeed()
        {
            if (feedLabels == null)
            {
                return;
            }

            for (int i = feedEntries.Count - 1; i >= 0; i--)
            {
                if (Time.time >= feedEntries[i].expireTime)
                {
                    feedEntries.RemoveAt(i);
                }
            }

            // Newest message on the bottom line (index 0, lowest on screen), older ones stacked
            // above it, blank once the list runs out.
            for (int i = 0; i < feedLabels.Length; i++)
            {
                int entryIndex = feedEntries.Count - 1 - i;
                feedLabels[i].text = entryIndex >= 0 ? feedEntries[entryIndex].text : string.Empty;
            }
        }
    }
}
