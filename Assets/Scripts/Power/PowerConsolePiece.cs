using System.Collections.Generic;
using System.Text;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SheltreSim.Power
{
    /// <summary>
    /// Shows the network here: what comes in, what's asked for, the battery, the heat, and for each device whether
    /// it runs and why not. Needs no power itself. The grid doesn't tick while paused, so an open panel is a snapshot.
    /// </summary>
    public sealed class PowerConsolePiece : PowerDevice
    {
        private const float RowHeight = 28f, BarWidth = 900f;

        private Text summary, warning, heatLabel;
        private Image heatFill;
        private RectTransform rowContent;
        private readonly List<Text> rows = new List<Text>();

        public override string PowerName => "Power Console";
        public override string PowerStatus => "needs no power";
        public override bool PowerRunning => true;
        public override string InteractPrompt => "Press E to read the Power Console";

        public override void Interact(GameObject interactor) => OpenPanel(interactor);

        protected override void JoinGrid() { }
        protected override void LeaveGrid() { }

        protected override Vector2 PanelSize => new Vector2(980f, 860f);

        protected override void BuildPanel(Transform panel)
        {
            Vector2 top = new Vector2(0.5f, 1f), left = new Vector2(0f, 0.5f);
            summary = GameUI.Label(panel, "Summary", "", top, top, new Vector2(0f, -70f), new Vector2(900f, 96f), 21, Color.white);
            Transform bar = GameUI.Panel(panel, "HeatBar", top, top, new Vector2(0f, -174f), new Vector2(BarWidth, 28f), GameUI.RowColor).transform;
            heatFill = GameUI.Panel(bar, "Fill", left, left, Vector2.zero, new Vector2(0f, 28f), GameUI.Accent);
            heatLabel = GameUI.Label(bar, "Label", "", Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(BarWidth, 28f), 18, Color.white, TextAnchor.MiddleCenter);
            warning = GameUI.Label(panel, "Warning", "", top, top, new Vector2(0f, -208f), new Vector2(900f, 34f), 20, GameUI.Danger, TextAnchor.MiddleCenter);
            rowContent = GameUI.ScrollContent(panel, "Devices", new Vector2(40f, -248f), new Vector2(900f, 500f));
        }

        protected override void RefreshPanel()
        {
            PowerNetwork network = PowerNetworks.NetworkNear(transform.position);
            if (network == null)
            {
                summary.text = $"No powered devices within {PowerGrid.LinkRange:0} m.\nBuild a Generator or a Solar Panel nearby.";
                warning.text = "";
                SetHeat(0f, false);
                ShowRows(new List<string>());
                return;
            }

            summary.text = Summarize(network);
            SetHeat(network.Heat, network.Overheated);
            warning.text = network.Overheated
                ? $"OVERHEATED: output is halved until heat falls to {PowerGrid.RecoverAt:0}. Build or power a Cooling Unit."
                : network.Served + 0.001f < network.Demand ? "NOT ENOUGH POWER: less important devices are stopped." : "";
            ShowRows(DescribeDevices(network));
        }

        private static string Summarize(PowerNetwork network)
        {
            var text = new StringBuilder();
            text.Append($"Supply {network.Supply:F1} kW     Demand {network.Demand:F1} kW     Served {network.Served:F1} kW\n");
            text.Append(network.Capacity > 0f
                ? $"Battery {network.Stored:F0} of {network.Capacity:F0} kJ ({Mathf.RoundToInt(100f * network.Stored / network.Capacity)}%)\n"
                : "No battery\n");

            float fuel = 0f, longest = 0f;
            int generators = 0;
            foreach (IPowerSource source in network.Sources)
            {
                if (!PowerNetworks.IsAlive(source) || !(source is GeneratorPiece generator)) continue;
                generators++;
                fuel += generator.FuelUnits;
                if (generator.IsRunning) longest = Mathf.Max(longest, generator.SecondsRemaining);
            }
            text.Append(generators == 0 ? "No generators" : $"Fuel in {generators} generators: {fuel:F1}, running time left {PowerNetworks.FormatTime(longest)}");
            return text.ToString();
        }

        private void SetHeat(float heat, bool overheated)
        {
            float fraction = Mathf.Clamp01(heat / PowerGrid.HeatCapacity);
            heatFill.rectTransform.sizeDelta = new Vector2(BarWidth * fraction, 28f);
            heatFill.color = overheated ? GameUI.Danger : fraction > 0.7f ? GameUI.Warning : GameUI.Accent;
            heatLabel.text = $"Heat {heat:F0} of {PowerGrid.HeatCapacity:0}";
        }

        private static List<string> DescribeDevices(PowerNetwork network)
        {
            var lines = new List<string>();
            foreach (IPowerSource source in network.Sources)
            {
                if (!PowerNetworks.IsAlive(source)) continue;
                lines.Add(source is IPowerStatus status ? Line(source.PowerName, status.PowerStatus, status.PowerRunning)
                    : source.PowerOutput > 0f ? Line(source.PowerName, $"producing {source.PowerOutput:F1} kW", true) : Line(source.PowerName, "producing nothing", false));
            }
            foreach (IPowerStorage store in network.Storage)
            {
                if (!PowerNetworks.IsAlive(store)) continue;
                lines.Add(store is IPowerStatus status ? Line("Battery", status.PowerStatus, status.PowerRunning)
                    : Line("Storage", $"{store.StoredEnergy:F0} of {store.EnergyCapacity:F0} kJ", store.StoredEnergy > 0f));
            }
            foreach (IPowerConsumer consumer in network.Consumers)
            {
                if (!PowerNetworks.IsAlive(consumer)) continue;
                lines.Add(consumer is IPowerStatus status ? Line(consumer.PowerName, status.PowerStatus, status.PowerRunning) : DescribeForeign(network, consumer));
            }
            return lines;
        }

        private static string DescribeForeign(PowerNetwork network, IPowerConsumer consumer)
        {
            if (consumer.PowerDemand <= 0f) return Line(consumer.PowerName, "idle, nothing to do", false);
            return network.IsPowered(consumer) ? Line(consumer.PowerName, $"running, {consumer.PowerDemand:F1} kW", true) : Line(consumer.PowerName, "no power", false);
        }

        private static string Line(string name, string status, bool running)
        {
            string color = ColorUtility.ToHtmlStringRGB(running ? GameUI.Accent : GameUI.Warning);
            return $"{name}: <color=#{color}>{status}</color>";
        }

        private void ShowRows(List<string> lines)
        {
            Vector2 topLeft = new Vector2(0f, 1f);
            while (rows.Count < lines.Count)
                rows.Add(GameUI.Label(rowContent, "Row" + rows.Count, "", topLeft, topLeft, new Vector2(8f, -rows.Count * RowHeight), new Vector2(880f, RowHeight), 18));
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].gameObject.SetActive(i < lines.Count);
                if (i < lines.Count) rows[i].text = lines[i];
            }
            rowContent.sizeDelta = new Vector2(0f, lines.Count * RowHeight);
        }
    }
}
