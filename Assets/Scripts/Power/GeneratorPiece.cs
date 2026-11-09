using System;
using SheltreSim.Audio;
using SheltreSim.Items;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SheltreSim.Power
{
    /// <summary>
    /// Burns fuel for power, only while running and only in game time. Starts empty and off, which is also what an older
    /// save's generator becomes. E opens a panel to load fuel and start or stop it.
    /// </summary>
    public sealed class GeneratorPiece : PowerDevice, IPowerSource
    {
        public const string FuelId = "fuel";

        public float outputKw = 6f;
        public float heatPerSecond = 2.5f; // one is safe on passive cooling, two need a Cooling Unit
        public float fuelCapacity = 10f;
        public float secondsPerFuelUnit = 120f;

        private float fuel;
        private bool running;
        private Text tankLabel, timeLabel, fuelHeldLabel;
        private Button startButton, addButton;

        [Serializable]
        private class State { public float fuel; public bool running; }

        public float FuelUnits => fuel;
        public bool IsRunning => running;
        public float SecondsRemaining => fuel * secondsPerFuelUnit;
        public float PowerOutput => running && fuel > 0f ? outputKw : 0f;
        float IPowerSource.HeatOutput => heatPerSecond;
        public override string PowerName => "Generator";
        public override bool PowerRunning => PowerOutput > 0f;

        public override string PowerStatus => running
            ? $"running, {fuel:F1} fuel, {PowerNetworks.FormatTime(SecondsRemaining)} left"
            : fuel > 0f ? $"off, {fuel:F1} fuel in the tank" : "out of fuel";

        public override string InteractPrompt => $"Press E: Generator, {PowerStatus}";

        public override void Interact(GameObject interactor) => OpenPanel(interactor);

        protected override void JoinGrid() => PowerGrid.Register(this);
        protected override void LeaveGrid() => PowerGrid.Unregister(this);

        public override void Advance(float seconds)
        {
            SetLoop(Sfx.GeneratorHum, running, 0.5f);
            if (!running) return;
            fuel -= seconds / Mathf.Max(0.1f, secondsPerFuelUnit);
            if (fuel > 0f) return;
            fuel = 0f;
            running = false;
            GameAudio.PlayAt(Sfx.PowerDown, transform.position, 0.8f, 30f);
            GameMessageBus.Post("A generator ran out of fuel.");
        }

        public bool TryStart()
        {
            if (running || fuel <= 0f) return false;
            running = true;
            GameAudio.PlayAt(Sfx.GeneratorStart, transform.position, 0.8f, 30f);
            return true;
        }

        public void Stop()
        {
            if (!running) return;
            running = false;
            GameAudio.PlayAt(Sfx.GeneratorStop, transform.position, 0.8f, 30f);
        }

        /// <summary>Moves one unit from the inventory to the tank, or nothing at all.</summary>
        public bool TryAddFuel(Inventory inventory)
        {
            if (inventory == null || fuel + 1f > fuelCapacity || !inventory.TryConsume(FuelId, 1)) return false;
            fuel += 1f;
            return true;
        }

        public override string CapturePieceState() => JsonUtility.ToJson(new State { fuel = fuel, running = running });

        public override void RestorePieceState(string state)
        {
            State saved = PowerNetworks.ParseState<State>(state);
            if (saved == null) return;
            fuel = PowerNetworks.Sane(saved.fuel, 0f, fuelCapacity);
            running = saved.running && fuel > 0f;
        }

        protected override void BuildPanel(Transform panel)
        {
            Vector2 top = new Vector2(0.5f, 1f);
            tankLabel = GameUI.Label(panel, "Tank", "", top, top, new Vector2(0f, -90f), new Vector2(800f, 40f), 24, Color.white, TextAnchor.MiddleCenter);
            timeLabel = GameUI.Label(panel, "Time", "", top, top, new Vector2(0f, -140f), new Vector2(800f, 36f), 20, GameUI.Muted, TextAnchor.MiddleCenter);
            fuelHeldLabel = GameUI.Label(panel, "Held", "", top, top, new Vector2(0f, -185f), new Vector2(800f, 36f), 20, GameUI.Muted, TextAnchor.MiddleCenter);
            startButton = GameUI.Button(panel, "StartStop", "START", top, top, new Vector2(-150f, -260f), new Vector2(260f, 60f), OnStartStop, true);
            addButton = GameUI.Button(panel, "AddFuel", "ADD 1 FUEL", top, top, new Vector2(150f, -260f), new Vector2(260f, 60f), OnAddFuel);
            GameUI.Label(panel, "Help", $"{outputKw:0.#} kW while running. One fuel lasts {PowerNetworks.FormatTime(secondsPerFuelUnit)}. It burns even with nothing plugged in, and gives off heat.",
                top, top, new Vector2(0f, -350f), new Vector2(800f, 70f), 18, GameUI.Muted, TextAnchor.MiddleCenter);
        }

        protected override void RefreshPanel()
        {
            int held = PanelInventory != null ? PanelInventory.GetCount(FuelId) : 0;
            tankLabel.text = $"Tank: {fuel:F1} / {fuelCapacity:0} fuel   ({(running ? "RUNNING" : "OFF")})";
            timeLabel.text = fuel > 0f ? $"{PowerNetworks.FormatTime(SecondsRemaining)} of running time left" : "The tank is empty";
            fuelHeldLabel.text = $"Fuel you are carrying: {held}";
            startButton.GetComponentInChildren<Text>().text = running ? "STOP" : "START";
            startButton.interactable = running || fuel > 0f;
            addButton.interactable = held > 0 && fuel + 1f <= fuelCapacity;
        }

        private void OnStartStop()
        {
            if (running) Stop();
            else if (!TryStart()) PanelMessage.text = "The tank is empty. Add fuel first.";
            RefreshPanel();
        }

        private void OnAddFuel()
        {
            PanelMessage.text = TryAddFuel(PanelInventory) ? "" : fuel + 1f > fuelCapacity ? "The tank is full." : "You have no Fuel.";
            RefreshPanel();
        }
    }
}
