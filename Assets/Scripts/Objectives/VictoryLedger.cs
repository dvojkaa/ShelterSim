using SheltreSim.Raids;
using SheltreSim.Saving;
using SheltreSim.UI;
using SheltreSim.World;
using UnityEngine;
using UnityEngine.UI;

namespace SheltreSim.Objectives
{
    /// <summary>Endgame recap shown once when a saved or live rescue broadcast completes.</summary>
    public sealed class VictoryLedger : MonoBehaviour
    {
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private GameObject panel;
        private Text statsLabel;
        private bool open;
        private bool shownThisRun;

        private void Awake()
        {
            Canvas canvas = GameUI.CreateCanvas(transform, "VictoryLedgerCanvas", 210, true);
            panel = GameUI.Backdrop(canvas.transform, "VictoryLedgerBackdrop");
            Transform card = GameUI.Panel(panel.transform, "Card", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 520f), GameUI.PanelColor, true).transform;
            GameUI.Label(card, "Eyebrow", "SHELTER SIM  /  RUN COMPLETE", TopLeft, TopLeft,
                new Vector2(36f, -28f), new Vector2(690f, 30f), 18, GameUI.Accent, bold: true);
            GameUI.Label(card, "Title", "Rescue is on its way", TopLeft, TopLeft,
                new Vector2(36f, -76f), new Vector2(690f, 60f), 40, Color.white, bold: true);
            GameUI.Label(card, "Summary", "The beacon held. The settlement is safe.", TopLeft, TopLeft,
                new Vector2(36f, -148f), new Vector2(690f, 34f), 20, GameUI.Muted);
            statsLabel = GameUI.Label(card, "Stats", string.Empty, TopLeft, TopLeft,
                new Vector2(36f, -208f), new Vector2(690f, 166f), 23, Color.white);
            GameUI.Button(card, "Continue", "Continue in endless mode", TopLeft, TopLeft,
                new Vector2(36f, -420f), new Vector2(420f, 58f), Continue, true);
            GameUI.Label(card, "Hint", "Raids are over. Explore, build and keep surviving.", TopLeft, TopLeft,
                new Vector2(36f, -476f), new Vector2(690f, 28f), 16, GameUI.Muted);
            panel.SetActive(false);
        }

        private void Update()
        {
            if (!shownThisRun && ObjectiveTracker.Victory) Show();
        }

        public void Show()
        {
            if (shownThisRun || !ObjectiveTracker.Victory) return;
            shownThisRun = true;
            WorldAtmosphere atmosphere = Object.FindFirstObjectByType<WorldAtmosphere>();
            float elapsed = atmosphere != null ? Mathf.Max(0f, atmosphere.ElapsedSeconds) : 0f;
            int days = Mathf.FloorToInt(elapsed / 1200f);
            int raids = RaidDirector.Active != null ? RaidDirector.Active.RaidsSurvived : 0;
            int enemies = Mathf.Max(0, LootBagSystem.NextId - 1);
            int standing = PlacedPieceRegistry.All.Count;
            statsLabel.text = $"World days elapsed     {days}\nRaids survived                 {raids}\nEnemies defeated             {enemies}\nStructures still standing   {standing}";
            ModalUI.Replace(this, Close);
            open = true;
            panel.SetActive(true);
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            panel.SetActive(false);
            ModalUI.Close(this);
        }

        private void Continue() => Close();
    }
}
