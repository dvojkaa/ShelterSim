using System.Collections.Generic;
using SheltreSim.Crafting;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SheltreSim.Core
{
    /// <summary>The bunker project on the left, a shop on the right, both paid from the same savings.</summary>
    public sealed class BunkerTerminalUI : MonoBehaviour
    {
        private static BunkerTerminalUI instance;
        public static bool IsOpen => instance != null && instance.open;

        private Canvas canvas;
        private Text balance, status;
        private readonly List<Text> stageLabels = new List<Text>();
        private readonly List<Button> stageButtons = new List<Button>();
        private Text annexLabel;
        private Button annexButton;
        private readonly List<Button> shopButtons = new List<Button>();
        private readonly List<ShopEntry> shopEntries = new List<ShopEntry>();
        private InputAction cancel;
        private bool open, closeRequested;

        public static void Open(GameObject interactor)
        {
            if (instance == null)
            {
                var host = new GameObject("BunkerTerminalUI");
                instance = host.AddComponent<BunkerTerminalUI>();
            }
            instance.Show();
        }

        private void Awake()
        {
            cancel = new InputAction("CloseTerminal", InputActionType.Button, "<Keyboard>/escape");
            cancel.performed += _ => { if (open) closeRequested = true; };
            cancel.Enable();
        }

        private void OnDestroy()
        {
            cancel?.Dispose();
            if (instance == this) instance = null;
        }

        private void Show()
        {
            if (!ModalUI.TryOpen(this, Close)) return;
            if (canvas == null) BuildUI();
            open = true;
            canvas.gameObject.SetActive(true);
            status.text = "Buy supplies before the last stage is finished. There is no shop afterwards.";
            Refresh();
        }

        private void BuildUI()
        {
            canvas = GameUI.CreateCanvas(transform, "TerminalCanvas", 130, true);
            GameUI.Backdrop(canvas.transform, "Backdrop");
            var center = Vector2.one * .5f;
            var topLeft = new Vector2(0f, 1f);
            Transform card = GameUI.Panel(canvas.transform, "Panel", center, center, Vector2.zero, new Vector2(1320f, 880f), GameUI.PanelColor, true).transform;
            GameUI.Label(card, "Title", "BUNKER PROJECT", topLeft, topLeft, new Vector2(28f, -22f), new Vector2(620f, 44f), 30, GameUI.Accent, bold: true);
            balance = GameUI.Label(card, "Balance", string.Empty, topLeft, topLeft, new Vector2(28f, -68f), new Vector2(1000f, 30f), 21);
            GameUI.Button(card, "Close", "Close [Esc]", Vector2.one, Vector2.one, new Vector2(-24f, -22f), new Vector2(170f, 44f), Close);

            GameUI.Label(card, "ProjectHeader", "THE BUNKER", topLeft, topLeft, new Vector2(28f, -112f), new Vector2(590f, 28f), 18, GameUI.Accent, bold: true);
            for (int i = 0; i < HomeBase.Stages.Length; i++)
            {
                int index = i;
                Transform row = GameUI.Panel(card, "Stage" + i, topLeft, topLeft, new Vector2(28f, -146f - i * 136f), new Vector2(600f, 128f), GameUI.RowColor).transform;
                stageLabels.Add(GameUI.Label(row, "Text", string.Empty, topLeft, topLeft, new Vector2(12f, -8f), new Vector2(430f, 112f), 17));
                stageButtons.Add(GameUI.Button(row, "Buy", "Start", new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(-12f, 0f), new Vector2(136f, 50f), BuyStage, true));
            }
            Transform annex = GameUI.Panel(card, "Annex", topLeft, topLeft, new Vector2(28f, -690f), new Vector2(600f, 100f), GameUI.RowColor).transform;
            annexLabel = GameUI.Label(annex, "Text", string.Empty, topLeft, topLeft, new Vector2(12f, -8f), new Vector2(430f, 84f), 17);
            annexButton = GameUI.Button(annex, "Buy", "Buy", new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(-12f, 0f), new Vector2(136f, 50f), BuyAnnex);

            GameUI.Label(card, "ShopHeader", "SUPPLIES  /  delivered to your backpack", topLeft, topLeft, new Vector2(660f, -112f), new Vector2(630f, 28f), 18, GameUI.Accent, bold: true);
            RectTransform content = GameUI.ScrollContent(card, "Shop", new Vector2(660f, -146f), new Vector2(636f, 644f));
            float y = 0f;
            foreach (ShopEntry entry in ItemCatalog.Shop)
            {
                var definition = ItemCatalog.Def(entry.id);
                if (definition == null) continue;
                ShopEntry captured = entry;
                Transform row = GameUI.Panel(content, "Item_" + entry.id, topLeft, topLeft, new Vector2(0f, -y), new Vector2(620f, 60f), GameUI.RowColor).transform;
                GameUI.Label(row, "Name", $"{entry.quantity} x {definition.displayName}", topLeft, topLeft, new Vector2(12f, -5f), new Vector2(460f, 26f), 19);
                GameUI.Label(row, "About", definition.description, topLeft, topLeft, new Vector2(12f, -31f), new Vector2(460f, 24f), 14, GameUI.Muted);
                shopEntries.Add(captured);
                shopButtons.Add(GameUI.Button(row, "Buy", "$" + entry.price, new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(-10f, 0f), new Vector2(118f, 42f), () => Buy(captured)));
                y += 66f;
            }
            content.sizeDelta = new Vector2(0f, y);
            status = GameUI.Label(card, "Status", string.Empty, topLeft, topLeft, new Vector2(28f, -810f), new Vector2(1264f, 52f), 18, GameUI.Warning);
        }

        private void BuyStage() => Report(HomeBase.Active.TryStartNextStage(out string result), result);
        private void BuyAnnex() => Report(HomeBase.Active.TryBuyAnnex(out string result), result);
        private void Buy(ShopEntry entry) => Report(HomeBase.Active.TryBuy(entry, out string result), result);

        private void Report(bool success, string result)
        {
            if (!success) SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.UiDeny, 0.4f);
            status.text = result;
            Refresh();
        }

        private void Refresh()
        {
            HomeBase home = HomeBase.Active;
            if (home == null) return;
            balance.text = $"Savings ${home.Money:N0}     Free to spend ${home.SpendableMoney:N0}     Kept for the bunker ${home.ReservedForBunker:N0}";
            for (int i = 0; i < HomeBase.Stages.Length; i++)
            {
                HomeBase.Stage stage = HomeBase.Stages[i];
                bool done = home.CompletedStages > i;
                bool working = home.IsBuilding && home.CompletedStages == i;
                bool next = !home.IsBuilding && home.CompletedStages == i;
                string state = done ? "DONE" : working ? $"IN PROGRESS {home.BuildFraction * 100f:0}%" : $"${stage.price:N0}, about {stage.seconds:0} s";
                stageLabels[i].text = $"{i + 1}. {stage.name}   /   {state}\n{stage.description}";
                stageLabels[i].color = done ? GameUI.Accent : next || working ? Color.white : GameUI.Muted;
                stageButtons[i].interactable = next && home.Money >= stage.price;
                stageButtons[i].GetComponentInChildren<Text>().text = done ? "Done" : working ? "Working" : "Start";
            }
            annexLabel.text = $"Storage annex   /   {(home.HasAnnex ? "BUILT" : "$" + HomeBase.AnnexPrice.ToString("N0"))}\nA second room for workshops and shelves. Optional. Available after the shell.";
            annexButton.interactable = !home.HasAnnex && home.CompletedStages >= 2 && home.SpendableMoney >= HomeBase.AnnexPrice;
            for (int i = 0; i < shopButtons.Count; i++)
                shopButtons[i].interactable = home.SpendableMoney >= shopEntries[i].price;
        }

        private void Update()
        {
            if (open && Time.frameCount % 20 == 0) Refresh();
        }

        private void LateUpdate()
        {
            if (closeRequested) Close();
        }

        public void Close()
        {
            closeRequested = false;
            if (!open) return;
            open = false;
            if (canvas != null) canvas.gameObject.SetActive(false);
            ModalUI.Close(this);
        }
    }
}
