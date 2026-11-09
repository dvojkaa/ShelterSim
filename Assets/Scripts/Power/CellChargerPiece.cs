using System;
using SheltreSim.Audio;
using SheltreSim.Items;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Power
{
    /// <summary>Makes Energy Cells from electricity. Only advances while powered, stops when full, and E collects.</summary>
    public sealed class CellChargerPiece : PowerConsumerDevice
    {
        public const string CellId = "energy_cell";

        public float powerKw = 1.5f;
        public float heatPerSecond = 0.5f;
        public float secondsPerCell = 60f;
        public int maxStored = 5;

        private int stored;
        private float progress;

        [Serializable]
        private class State { public int stored; public float progress; }

        public int Stored => stored;
        public float Progress => progress;
        public override string PowerName => "Cell Charger";
        public override float PowerDemand => stored < maxStored ? powerKw : 0f;
        public override float HeatOutput => Granted ? heatPerSecond : 0f;

        public override string PowerStatus
        {
            get
            {
                if (stored >= maxStored) return $"full, {stored} cells ready";
                if (!Granted) return $"{NoPowerReason()}, {stored} cells ready";
                return $"charging, {stored} cells ready, next in {PowerNetworks.FormatTime(secondsPerCell - progress)}";
            }
        }

        public override string InteractPrompt => stored > 0 ? $"Press E to collect {stored} Energy Cells ({PowerStatus})" : $"{PowerName}: {PowerStatus}";

        public override void Advance(float seconds)
        {
            if (!Granted || stored >= maxStored) return;
            progress += seconds;
            if (progress < secondsPerCell) return;
            progress = 0f; // One cell per completed cycle: a long frame or a load can't pay out a burst.
            stored++;
        }

        public override void Interact(GameObject interactor)
        {
            Inventory inventory = PowerNetworks.InventoryOf(interactor);
            if (inventory == null) return;
            if (stored <= 0)
            {
                GameMessageBus.Post($"{PowerName}: {PowerStatus}");
                return;
            }
            inventory.Add(CellId, stored);
            GameAudio.Play2D(Sfx.Equip, 0.5f);
            GameMessageBus.Post($"Collected {stored} Energy Cells.");
            stored = 0;
        }

        public override string CapturePieceState() => JsonUtility.ToJson(new State { stored = stored, progress = progress });

        public override void RestorePieceState(string state)
        {
            State saved = PowerNetworks.ParseState<State>(state);
            if (saved == null) return;
            stored = Mathf.Clamp(saved.stored, 0, maxStored);
            progress = stored < maxStored ? PowerNetworks.Sane(saved.progress, 0f, secondsPerCell) : 0f;
        }
    }
}
