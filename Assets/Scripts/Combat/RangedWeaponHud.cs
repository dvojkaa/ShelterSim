using SheltreSim.Crafting;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SheltreSim.Combat
{
    /// <summary>Passive canvas: ammo, heat, reload state and the centre hit marker. No raycaster, nothing clickable.</summary>
    public sealed class RangedWeaponHud
    {
        private const float MarkerSeconds = 0.12f;
        private const float BarWidth = 372f;

        private readonly GameObject root;
        private readonly GameObject card, heatBack, marker;
        private readonly Text ammoLabel, stateLabel;
        private readonly Image heatFill;
        private float markerUntil;
        private readonly Image[] strokes = new Image[2];

        public string AmmoText => ammoLabel.text;
        public string StateText => stateLabel.text;
        public bool CardVisible => card.activeSelf;
        public bool MarkerVisible => marker.activeSelf;
        public GameObject Root => root;

        public RangedWeaponHud()
        {
            root = new GameObject("RangedWeaponHud");
            Canvas canvas = GameUI.CreateCanvas(root.transform, "RangedHud_Canvas", 2, false);
            Vector2 lowerRight = new Vector2(1f, 0f), topLeft = new Vector2(0f, 1f), lowerLeft = Vector2.zero, center = new Vector2(0.5f, 0.5f);

            card = GameUI.Panel(canvas.transform, "AmmoCard", lowerRight, lowerRight, new Vector2(-24f, 96f), new Vector2(400f, 96f), GameUI.PanelColor).gameObject;
            ammoLabel = GameUI.Label(card.transform, "Ammo", string.Empty, topLeft, topLeft, new Vector2(14f, -8f), new Vector2(372f, 32f), 22, Color.white, bold: true);
            stateLabel = GameUI.Label(card.transform, "State", string.Empty, topLeft, topLeft, new Vector2(14f, -40f), new Vector2(372f, 26f), 18, GameUI.Warning);
            heatBack = GameUI.Panel(card.transform, "HeatBack", topLeft, topLeft, new Vector2(14f, -72f), new Vector2(BarWidth, 12f), GameUI.RowColor).gameObject;
            heatFill = GameUI.Panel(heatBack.transform, "HeatFill", lowerLeft, lowerLeft, Vector2.zero, new Vector2(0f, 12f), GameUI.Warning);

            marker = new GameObject("HitMarker", typeof(RectTransform));
            marker.transform.SetParent(canvas.transform, false);
            var markerRect = (RectTransform)marker.transform;
            markerRect.anchorMin = markerRect.anchorMax = markerRect.pivot = center;
            markerRect.anchoredPosition = Vector2.zero;
            markerRect.sizeDelta = new Vector2(24f, 24f);
            for (int i = 0; i < strokes.Length; i++)
            {
                strokes[i] = GameUI.Panel(marker.transform, "Stroke", center, center, Vector2.zero, new Vector2(22f, 2f), Color.white);
                strokes[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
            }
            marker.SetActive(false);
            card.SetActive(false);
        }

        public void ShowHit(bool kill)
        {
            markerUntil = Time.unscaledTime + MarkerSeconds;
            foreach (Image stroke in strokes) stroke.color = kill ? GameUI.Danger : Color.white;
        }

        public void Refresh(bool visible, ItemSpec spec, int rounds, int reserve, float heat, bool overheated, bool reloading, float reloadLeft)
        {
            bool showMarker = Time.unscaledTime < markerUntil;
            if (marker.activeSelf != showMarker) marker.SetActive(showMarker);
            visible = visible && spec != null;
            if (card.activeSelf != visible) card.SetActive(visible);
            if (!visible) return;

            string ammo = $"{spec.name} {rounds}/{spec.magazine} ({reserve})";
            if (ammoLabel.text != ammo) ammoLabel.text = ammo;
            string state = reloading ? $"Reloading {reloadLeft:0.0}"
                : overheated ? "OVERHEATED"
                : rounds <= 0 ? (reserve > 0 ? "Empty: press R" : "Out of ammo")
                : string.Empty;
            if (stateLabel.text != state) stateLabel.text = state;
            stateLabel.color = overheated ? GameUI.Danger : GameUI.Warning;
            if (heatBack.activeSelf != spec.energy) heatBack.SetActive(spec.energy);
            if (spec.energy)
            {
                heatFill.rectTransform.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(heat / 100f), 12f);
                heatFill.color = overheated ? GameUI.Danger : GameUI.Warning;
            }
        }

        public void Destroy()
        {
            if (root != null) Object.Destroy(root);
        }
    }
}
