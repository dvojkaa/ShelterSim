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

        private bool loggedMissingStats;

        private void Awake()
        {
            GameObject canvasObject = new GameObject("HUD_Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObject.AddComponent<GraphicRaycaster>();

            // No EventSystem: this HUD is a passive readout, nothing on it is clickable.
            healthFill = CreateBar(canvasObject.transform, "HealthBar", -20f, new Color(0.8f, 0.1f, 0.1f));
            hungerFill = CreateBar(canvasObject.transform, "HungerBar", -54f, new Color(0.6f, 0.4f, 0.1f));
            thirstFill = CreateBar(canvasObject.transform, "ThirstBar", -88f, new Color(0.2f, 0.5f, 0.9f));
        }

        private static Image CreateBar(Transform parent, string name, float yOffset, Color fillColor)
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

            return fillImage;
        }

        private void Update()
        {
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
        }
    }
}
