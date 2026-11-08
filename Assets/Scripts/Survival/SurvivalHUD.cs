using System.Collections.Generic;
using SheltreSim.Building;
using SheltreSim.Core;
using SheltreSim.Items;
using SheltreSim.Saving;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SheltreSim.Survival
{
    public class SurvivalHUD : MonoBehaviour
    {
        public SurvivalStats stats;

        private Canvas canvas;
        private RectTransform healthFill;
        private RectTransform hungerFill;
        private RectTransform thirstFill;
        private Text healthLabel;
        private Text hungerLabel;
        private Text thirstLabel;
        private Text warningLabel;
        private Text phaseLabel;
        private Text phaseDetailLabel;
        private Text objectiveLabel;
        private Text homeLabel;
        private Text controlsLabel;
        private Text interactPromptLabel;
        private Text buildReadoutLabel;
        private Image damageFlash;
        private GameObject crosshair;
        private Image[] crosshairBars;
        private GameStateManager gameState;
        private Inventory inventory;
        private BuildModeController buildController;
        private Transform homeAnchor;
        private string interactPrompt = string.Empty;
        private string buildReadout = string.Empty;
        private float previousHealth = -1f;
        private float flashAmount;
        private float nextGuidanceUpdate;
        private const int MessageFeedLines = 4;
        private const float MessageLifetimeSeconds = 6f;
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopRight = Vector2.one;
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private class FeedEntry
        {
            public string text;
            public int count = 1;
            public float expireTime;
        }

        private readonly List<FeedEntry> feedEntries = new List<FeedEntry>();
        private Text[] feedLabels;

        private void Awake()
        {
            // A passive HUD has no GraphicRaycaster. All its graphics also ignore raycasts,
            // so prompts can never swallow an inventory or menu button click.
            canvas = GameUI.CreateCanvas(transform, "HUD_Canvas", 0, false);
            damageFlash = GameUI.Panel(canvas.transform, "DamageFlash", Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.zero, Color.clear);
            damageFlash.rectTransform.anchorMax = Vector2.one;
            damageFlash.rectTransform.offsetMin = damageFlash.rectTransform.offsetMax = Vector2.zero;

            Transform vitalCard = GameUI.Panel(canvas.transform, "Vitals", TopLeft, TopLeft,
                new Vector2(24f, -24f), new Vector2(300f, 218f), GameUI.PanelColor).transform;
            GameUI.Label(vitalCard, "Title", "SHELTER STATUS", TopLeft, TopLeft,
                new Vector2(18f, -14f), new Vector2(264f, 28f), 19, GameUI.Accent, bold: true);
            healthFill = CreateBar(vitalCard, "Health", 54f, new Color(0.78f, 0.29f, 0.25f), out healthLabel);
            hungerFill = CreateBar(vitalCard, "Food", 106f, new Color(0.81f, 0.61f, 0.27f), out hungerLabel);
            thirstFill = CreateBar(vitalCard, "Water", 158f, new Color(0.31f, 0.67f, 0.86f), out thirstLabel);
            warningLabel = GameUI.Label(canvas.transform, "VitalWarning", string.Empty, TopLeft, TopLeft,
                new Vector2(24f, -254f), new Vector2(310f, 90f), 20, GameUI.Warning, bold: true);

            Transform objectiveCard = GameUI.Panel(canvas.transform, "Objective", TopRight, TopRight,
                new Vector2(-24f, -24f), new Vector2(380f, 242f), GameUI.PanelColor).transform;
            phaseLabel = GameUI.Label(objectiveCard, "Phase", string.Empty, TopLeft, TopLeft,
                new Vector2(18f, -14f), new Vector2(344f, 32f), 23, GameUI.Accent, bold: true);
            phaseDetailLabel = GameUI.Label(objectiveCard, "PhaseDetails", string.Empty, TopLeft, TopLeft,
                new Vector2(18f, -55f), new Vector2(344f, 42f), 16, GameUI.Muted);
            objectiveLabel = GameUI.Label(objectiveCard, "NextStep", string.Empty, TopLeft, TopLeft,
                new Vector2(18f, -108f), new Vector2(344f, 118f), 19);

            homeLabel = GameUI.Label(canvas.transform, "HomeBearing", string.Empty, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(520f, 32f), 20,
                GameUI.Accent, TextAnchor.MiddleCenter, true);
            AddShadow(homeLabel);
            buildReadoutLabel = GameUI.Label(canvas.transform, "BuildReadout", string.Empty, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(560f, 128f), 21,
                alignment: TextAnchor.UpperCenter, bold: true);
            AddShadow(buildReadoutLabel);

            crosshair = new GameObject("Crosshair", typeof(RectTransform));
            crosshair.transform.SetParent(canvas.transform, false);
            RectTransform crosshairRect = crosshair.GetComponent<RectTransform>();
            crosshairRect.anchorMin = crosshairRect.anchorMax = Center;
            crosshairRect.anchoredPosition = Vector2.zero;
            crosshairRect.sizeDelta = Vector2.zero;
            crosshairBars = new[]
            {
                GameUI.Panel(crosshair.transform, "Horizontal", Center, Center, Vector2.zero, new Vector2(12f, 2f), Color.white),
                GameUI.Panel(crosshair.transform, "Vertical", Center, Center, Vector2.zero, new Vector2(2f, 12f), Color.white)
            };
            interactPromptLabel = GameUI.Label(canvas.transform, "InteractPrompt", string.Empty, Center,
                new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(900f, 74f), 21,
                alignment: TextAnchor.UpperCenter, bold: true);
            AddShadow(interactPromptLabel);

            feedLabels = new Text[MessageFeedLines];
            for (int i = 0; i < feedLabels.Length; i++)
            {
                feedLabels[i] = GameUI.Label(canvas.transform, "MessageFeedLine" + i, string.Empty,
                    Vector2.zero, Vector2.zero, new Vector2(24f, 72f + i * 50f), new Vector2(780f, 46f), 19);
                AddShadow(feedLabels[i]);
            }
            Transform controlsCard = GameUI.Panel(canvas.transform, "Controls", new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(1160f, 38f), GameUI.PanelColor).transform;
            controlsLabel = GameUI.Label(controlsCard, "ControlsLabel", string.Empty, Center, Center,
                Vector2.zero, new Vector2(1124f, 30f), 18, GameUI.Muted, TextAnchor.MiddleCenter);
        }

        private void OnEnable() => GameMessageBus.OnMessage += ShowMessage;
        private void OnDisable() => GameMessageBus.OnMessage -= ShowMessage;

        private void Start()
        {
            // Runtime discovery keeps existing scenes compatible; editor-generated references
            // are optional and no new scene rebuild is required for these readouts.
            if (stats == null) stats = Object.FindFirstObjectByType<SurvivalStats>();
            gameState = Object.FindFirstObjectByType<GameStateManager>();
            if (stats != null)
            {
                inventory = stats.GetComponent<Inventory>();
                buildController = stats.GetComponentInChildren<BuildModeController>();
                previousHealth = stats.health;
            }
            ApocalypseTrigger landmark = Object.FindFirstObjectByType<ApocalypseTrigger>();
            if (landmark != null) homeAnchor = landmark.transform;
        }

        private static RectTransform CreateBar(Transform parent, string name, float y, Color color, out Text label)
        {
            label = GameUI.Label(parent, name + "Label", name, TopLeft, TopLeft,
                new Vector2(18f, -y), new Vector2(264f, 25f), 19);
            Image background = GameUI.Panel(parent, name + "Background", TopLeft, TopLeft,
                new Vector2(18f, -(y + 30f)), new Vector2(264f, 8f), new Color(0.16f, 0.21f, 0.22f));
            Image fill = GameUI.Panel(background.transform, name + "Fill", Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.zero, color);
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            // Width via anchors also works with a null sprite; Image.Filled requires a sprite
            // in Unity's mesh generation path and cannot reliably draw primitive stat bars.
            return fill.rectTransform;
        }

        private static void AddShadow(Text label)
        {
            Shadow shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        private void LateUpdate()
        {
            UpdateMessageFeed();
            bool dead = stats != null && stats.IsDead;
            canvas.enabled = !ModalUI.IsOpen && !dead;
            if (stats == null) return;

            UpdateBar(healthFill, healthLabel, "HEALTH", stats.health, stats.maxHealth);
            UpdateBar(hungerFill, hungerLabel, "FOOD", stats.hunger, stats.maxHunger);
            UpdateBar(thirstFill, thirstLabel, "WATER", stats.thirst, stats.maxThirst);
            if (previousHealth >= 0f && stats.health < previousHealth - 0.1f) flashAmount = 0.18f;
            previousHealth = stats.health;
            flashAmount = Mathf.MoveTowards(flashAmount, 0f, Time.unscaledDeltaTime * 0.35f);
            damageFlash.color = new Color(0.8f, 0.1f, 0.05f, flashAmount);

            bool building = buildController != null && buildController.IsBuildModeActive;
            interactPromptLabel.text = building ? string.Empty : interactPrompt;
            crosshair.SetActive(!building);
            foreach (Image bar in crosshairBars) bar.color = string.IsNullOrEmpty(interactPrompt) ? Color.white : GameUI.Accent;
            buildReadoutLabel.text = building ? buildReadout : string.Empty;
            controlsLabel.text = building
                ? "BUILD   /   Wheel: choose   /   R: rotate   /   Click: place   /   B or Esc: finish   /   Green: ready   /   Yellow: need supplies"
                : "WASD: move   /   Shift: sprint   /   E: interact   /   Click: melee   /   I or Tab: inventory   /   B: build   /   F5: save   /   Esc: menu";

            if (Time.unscaledTime >= nextGuidanceUpdate)
            {
                UpdateGuidance();
                nextGuidanceUpdate = Time.unscaledTime + 0.25f;
            }
            UpdateHomeBearing();
        }

        private static void UpdateBar(RectTransform fill, Text label, string name, float value, float max)
        {
            float fraction = max > 0f ? Mathf.Clamp01(value / max) : 0f;
            fill.anchorMax = new Vector2(fraction, 1f);
            label.text = $"{name}    {Mathf.CeilToInt(value)} / {Mathf.CeilToInt(max)}";
            label.color = fraction <= 0.2f ? GameUI.Warning : Color.white;
        }

        private void UpdateGuidance()
        {
            float food = stats.maxHunger > 0f ? stats.hunger / stats.maxHunger : 0f;
            float water = stats.maxThirst > 0f ? stats.thirst / stats.maxThirst : 0f;
            float health = stats.maxHealth > 0f ? stats.health / stats.maxHealth : 0f;
            warningLabel.text = health <= 0.25f ? "LOW HEALTH\nUse first aid or rest at a bed."
                : water <= 0.2f ? "LOW WATER\nDrink supplies from I or Tab."
                : food <= 0.2f ? "LOW FOOD\nEat supplies from I or Tab." : string.Empty;

            GamePhase phase = gameState != null ? gameState.CurrentPhase : GamePhase.Survival;
            if (phase == GamePhase.Apocalypse)
            {
                phaseLabel.text = "EMERGENCY ALERT";
                phaseLabel.color = GameUI.Danger;
                phaseDetailLabel.text = "Stay in the shelter. The world is changing.";
                objectiveLabel.text = $"Survival begins in {Mathf.CeilToInt(gameState.ApocalypseSecondsRemaining)}...\nFood and water will start draining.\nKeep supplies ready in your inventory.";
                return;
            }

            phaseLabel.color = GameUI.Accent;
            bool collector = HasWaterCollector();
            int foodCount = inventory != null ? inventory.GetCount("food_berries") : 0;
            int waterCount = inventory != null ? inventory.GetCount("water") : 0;
            if (phase == GamePhase.Building)
            {
                phaseLabel.text = "PREPARATION";
                phaseDetailLabel.text = "Food and water are stable. Raiders can still hurt you outside.";
                objectiveLabel.text = (collector ? "[x] Water Collector built\n" : "[ ] Build a Water Collector (B)\n") +
                    (foodCount > 0 ? "[x] Food stocked\n" : "[ ] Gather berries outside (E)\n") +
                    (waterCount > 0 ? "[x] Water stocked\n" : "[ ] Loot or collect water (E)\n") +
                    "Ready? Use the red bunker console.";
            }
            else
            {
                phaseLabel.text = "SURVIVAL";
                phaseDetailLabel.text = "Scavenge, return home, recover. Save your progress with F5.";
                objectiveLabel.text = water <= 0.35f
                    ? (waterCount > 0 ? "Open I or Tab and drink water.\n" : "Collect water at home or search crates.\n") + "An empty water bar costs health."
                    : food <= 0.35f
                        ? (foodCount > 0 ? "Open I or Tab and eat food.\n" : "Find berries outside or search crates.\n") + "An empty food bar costs health."
                        : health <= 0.6f ? "Recover before the next supply run.\nUse first aid in your inventory, or rest at a bed away from raiders."
                        : !collector ? "Build a Water Collector for a supply at home.\nGather Scrap Metal, then press B."
                        : "Keep food and water stocked.\nUse the Workbench to craft a blade, then equip it from your inventory.";
            }
        }

        private static bool HasWaterCollector()
        {
            foreach (PlacedPiece piece in PlacedPieceRegistry.All)
            {
                if (piece != null && piece.isActiveAndEnabled && piece.pieceId == "water_collector") return true;
            }
            return false;
        }

        private void UpdateHomeBearing()
        {
            if (homeAnchor == null || stats == null)
            {
                homeLabel.text = string.Empty;
                return;
            }
            Vector3 direction = homeAnchor.position - stats.transform.position;
            direction.y = 0f;
            float distance = direction.magnitude;
            if (distance < 7f)
            {
                homeLabel.text = "HOME SHELTER";
                return;
            }
            float angle = Vector3.SignedAngle(stats.transform.forward, direction, Vector3.up);
            string bearing = Mathf.Abs(angle) < 25f ? "AHEAD"
                : Mathf.Abs(angle) > 150f ? "BEHIND" : angle > 0f ? "RIGHT" : "LEFT";
            homeLabel.text = $"HOME  /  {distance:0} m  /  {bearing}";
        }

        public void SetInteractPrompt(string text) => interactPrompt = text ?? string.Empty;

        public void SetBuildReadout(string text, bool canAfford)
        {
            buildReadout = text ?? string.Empty;
            if (buildReadoutLabel != null) buildReadoutLabel.color = canAfford ? GameUI.Accent : GameUI.Warning;
        }

        public void ShowMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            FeedEntry last = feedEntries.Count > 0 ? feedEntries[feedEntries.Count - 1] : null;
            if (last != null && last.text == text)
            {
                last.count++;
                last.expireTime = Time.unscaledTime + MessageLifetimeSeconds;
                return;
            }
            feedEntries.Add(new FeedEntry { text = text, expireTime = Time.unscaledTime + MessageLifetimeSeconds });
            while (feedEntries.Count > MessageFeedLines) feedEntries.RemoveAt(0);
        }

        private void UpdateMessageFeed()
        {
            for (int i = feedEntries.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime >= feedEntries[i].expireTime) feedEntries.RemoveAt(i);
            }
            for (int i = 0; i < feedLabels.Length; i++)
            {
                int entryIndex = feedEntries.Count - 1 - i;
                if (entryIndex < 0)
                {
                    feedLabels[i].text = string.Empty;
                    continue;
                }
                FeedEntry entry = feedEntries[entryIndex];
                feedLabels[i].text = entry.text + (entry.count > 1 ? $"  (x{entry.count})" : string.Empty);
                feedLabels[i].color = new Color(1f, 1f, 1f, Mathf.Clamp01(entry.expireTime - Time.unscaledTime));
            }
        }
    }
}
