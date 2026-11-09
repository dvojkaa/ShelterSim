using SheltreSim.Crafting;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Survival;
using UnityEngine;
using UnityEngine.UI;

namespace SheltreSim.UI
{
    /// <summary>
    /// M24 passive HUD on its own canvas: hit-direction arcs, the low-health vignette, the flashlight battery,
    /// quick keys 3/4/5 and the optional FPS counter. No GraphicRaycaster, nothing is a raycast target.
    /// Hidden with SurvivalHUD's rules (any modal open, or dead). Added to the player by FirstPersonController.
    /// </summary>
    public class PlayerStatusHUD : MonoBehaviour
    {
        private const float LowHealthFraction = 0.25f;
        private static readonly Vector2 BottomRight = new Vector2(1f, 0f);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Color HitColor = new Color(0.95f, 0.12f, 0.08f);

        private Canvas canvas;
        private Canvas backCanvas;
        private SurvivalStats stats;
        private Inventory inventory;
        private Texture2D arcTexture;
        private Texture2D vignetteTexture;
        private RawImage vignette;
        private RawImage[] arcs;
        private Text lightLabel;
        private RectTransform lightFill;
        private Image lightFillImage;
        private Text[] slotLabels;
        private Text fpsLabel;
        private float nextSlowRefresh;
        private float smoothedDelta = 1f / 60f;
        private float nextFpsText;

        private void Awake()
        {
            stats = GetComponent<SurvivalStats>();
            inventory = GetComponent<Inventory>();
            // Order 1: just above SurvivalHUD (0), far below every modal (100).
            canvas = GameUI.CreateCanvas(transform, "PlayerStatus_Canvas", 1, false);
            arcTexture = BuildArcTexture(128);
            vignetteTexture = BuildVignetteTexture(64);

            // The vignette sits under SurvivalHUD (order -1) so the vitals in the corner stay readable.
            backCanvas = GameUI.CreateCanvas(transform, "PlayerStatus_BackCanvas", -1, false);
            vignette = Raw(backCanvas.transform, "LowHealthVignette", vignetteTexture);
            RectTransform v = vignette.rectTransform;
            v.anchorMin = Vector2.zero;
            v.anchorMax = Vector2.one;
            v.offsetMin = v.offsetMax = Vector2.zero;
            vignette.color = new Color(0.75f, 0.02f, 0.02f, 0f);

            arcs = new RawImage[DamageDirection.Capacity];
            for (int i = 0; i < arcs.Length; i++)
            {
                arcs[i] = Raw(canvas.transform, "HitArc" + i, arcTexture);
                RectTransform r = arcs[i].rectTransform;
                r.anchorMin = r.anchorMax = r.pivot = Center;
                r.anchoredPosition = Vector2.zero;
                r.sizeDelta = new Vector2(440f, 440f);
                arcs[i].color = Color.clear;
            }

            // Bottom-right column: flashlight, then the three quick keys above it.
            Transform lightCard = GameUI.Panel(canvas.transform, "Flashlight", BottomRight, BottomRight,
                new Vector2(-24f, 116f), new Vector2(320f, 50f), GameUI.PanelColor).transform;
            lightLabel = GameUI.Label(lightCard, "Label", string.Empty, TopLeft, TopLeft, new Vector2(12f, -4f), new Vector2(300f, 26f), 17);
            Image back = GameUI.Panel(lightCard, "Background", TopLeft, TopLeft, new Vector2(12f, -34f), new Vector2(296f, 7f), new Color(0.16f, 0.21f, 0.22f));
            lightFillImage = GameUI.Panel(back.transform, "Fill", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.95f, 0.85f, 0.45f));
            lightFill = lightFillImage.rectTransform;
            lightFill.anchorMax = Vector2.one;
            lightFill.offsetMin = lightFill.offsetMax = Vector2.zero;

            slotLabels = new Text[QuickSlots.Count];
            for (int i = 0; i < slotLabels.Length; i++)
            {
                Transform card = GameUI.Panel(canvas.transform, "QuickSlot" + (QuickSlots.FirstKey + i), BottomRight, BottomRight,
                    new Vector2(-24f, 174f + (slotLabels.Length - 1 - i) * 44f), new Vector2(320f, 40f), GameUI.PanelColor).transform;
                slotLabels[i] = GameUI.Label(card, "Label", string.Empty, TopLeft, TopLeft, new Vector2(12f, -8f), new Vector2(300f, 26f), 17);
            }

            fpsLabel = GameUI.Label(canvas.transform, "Fps", string.Empty, TopLeft, TopLeft, new Vector2(6f, -2f), new Vector2(160f, 20f), 15, GameUI.Accent);
        }

        private void OnDestroy()
        {
            if (arcTexture != null) Destroy(arcTexture);
            if (vignetteTexture != null) Destroy(vignetteTexture);
        }

        private void LateUpdate()
        {
            bool dead = stats != null && stats.IsDead;
            canvas.enabled = !ModalUI.IsOpen && !dead;
            backCanvas.enabled = canvas.enabled;
            UpdateFps();
            if (!canvas.enabled) return;

            UpdateArcs();
            UpdateVignette();
            UpdateFlashlight();
            if (Time.unscaledTime >= nextSlowRefresh)
            {
                nextSlowRefresh = Time.unscaledTime + 0.2f;
                UpdateSlots();
            }
        }

        private void UpdateArcs()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            for (int i = 0; i < arcs.Length; i++)
            {
                float strength = DamageDirection.Get(i, out Vector3 from);
                if (strength <= 0f)
                {
                    if (arcs[i].color.a > 0f) arcs[i].color = Color.clear;
                    continue;
                }
                Vector3 direction = from - transform.position;
                direction.y = 0f;
                float angle = direction.sqrMagnitude > 0.0001f && forward.sqrMagnitude > 0.0001f
                    ? Vector3.SignedAngle(forward, direction, Vector3.up) : 0f;
                // Positive angle = attacker to the right: rotate the arc clockwise from "ahead" (top of the screen).
                arcs[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, -angle);
                arcs[i].color = new Color(HitColor.r, HitColor.g, HitColor.b, 0.9f * strength);
            }
        }

        private void UpdateVignette()
        {
            float fraction = stats != null && stats.maxHealth > 0f ? stats.health / stats.maxHealth : 1f;
            if (stats == null || fraction >= LowHealthFraction || stats.health <= 0f)
            {
                if (vignette.color.a > 0f) vignette.color = new Color(vignette.color.r, vignette.color.g, vignette.color.b, 0f);
                return;
            }
            float severity = 1f - Mathf.Clamp01(fraction / LowHealthFraction);   // 0 at 25%, 1 near death
            float speed = Mathf.Lerp(3f, 6f, severity);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
            float alpha = Mathf.Lerp(0.3f, 0.55f, severity) + 0.25f * pulse;
            vignette.color = new Color(vignette.color.r, vignette.color.g, vignette.color.b, Mathf.Clamp01(alpha));
        }

        private void UpdateFlashlight()
        {
            Flashlight light = Flashlight.Active;
            if (light == null)
            {
                lightLabel.text = string.Empty;
                lightFill.anchorMax = new Vector2(0f, 1f);
                return;
            }
            float charge = Mathf.Clamp01(light.Charge);
            lightFill.anchorMax = new Vector2(charge, 1f);
            lightFillImage.color = charge <= 0.1f ? GameUI.Warning : new Color(0.95f, 0.85f, 0.45f);
            int percent = Mathf.CeilToInt(charge * 100f);
            if (charge <= 0f)
            {
                int cells = inventory != null ? inventory.GetCount(Flashlight.CellId) : 0;
                lightLabel.text = cells > 0 ? $"F  LIGHT  NO CHARGE  (F loads a cell, {cells} left)" : "F  LIGHT  NO CHARGE  (needs an Energy Cell)";
                lightLabel.color = GameUI.Warning;
            }
            else
            {
                lightLabel.text = $"F  LIGHT  {(light.IsOn ? "ON" : "off")}   {percent}%";
                lightLabel.color = light.IsOn ? Color.white : GameUI.Muted;
            }
        }

        private void UpdateSlots()
        {
            for (int i = 0; i < slotLabels.Length; i++)
            {
                int key = QuickSlots.FirstKey + i;
                string id = QuickSlots.ItemOn(key);
                if (string.IsNullOrEmpty(id))
                {
                    slotLabels[i].text = $"{key}   " + (i == 0 ? "no medicine" : i == 1 ? "no food" : "nothing to drink");
                    slotLabels[i].color = GameUI.Muted;
                    continue;
                }
                int count = QuickSlots.CountOf(id);
                slotLabels[i].text = $"{key}   {ItemCatalog.NameOf(id)}   x{count}";
                slotLabels[i].color = count <= 0 ? GameUI.Warning : QuickSlots.IsAssigned(key) ? Color.white : GameUI.Muted;
            }
        }

        private void UpdateFps()
        {
            bool show = GameplaySettings.ShowFps;
            if (!show)
            {
                if (fpsLabel.text.Length > 0) fpsLabel.text = string.Empty;
                return;
            }
            float delta = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            smoothedDelta = Mathf.Lerp(smoothedDelta, delta, 0.1f);
            if (Time.unscaledTime < nextFpsText) return;
            nextFpsText = Time.unscaledTime + 0.25f;
            fpsLabel.text = $"{1f / smoothedDelta:0} FPS";
        }

        // ---------- Primitive textures (no imported art) ----------

        private static RawImage Raw(Transform parent, string name, Texture texture)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            obj.transform.SetParent(parent, false);
            RawImage image = obj.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A soft ring segment centred on "up", white; tinted by the RawImage colour.</summary>
        private static Texture2D BuildArcTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "HitArc" };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.88f) / 0.07f);
                    float angle = Mathf.Abs(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg);
                    float spread = Mathf.Clamp01(1f - Mathf.InverseLerp(18f, 34f, angle));
                    byte a = (byte)Mathf.RoundToInt(255f * ring * spread);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>Transparent middle, opaque edges. Stretched over the screen, so it follows its shape.</summary>
        private static Texture2D BuildVignetteTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "LowHealthVignette" };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float t = Mathf.Clamp01(Mathf.InverseLerp(0.55f, 1.25f, r));
                    byte a = (byte)Mathf.RoundToInt(255f * t * t * (3f - 2f * t));
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
