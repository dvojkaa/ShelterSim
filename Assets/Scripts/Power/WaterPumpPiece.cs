using System;
using SheltreSim.Audio;
using SheltreSim.Items;
using SheltreSim.Storage;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Power
{
    /// <summary>Electric well. Delivers to a crate within reach by itself, otherwise holds water for E. Stops when full.</summary>
    public sealed class WaterPumpPiece : PowerConsumerDevice
    {
        public const string WaterId = "water";

        public float powerKw = 1f;
        public float heatPerSecond = 0.25f;
        public float secondsPerWater = 25f;
        public int maxStored = 10;
        public float crateRange = 8f;

        private int stored;
        private float progress;

        [Serializable]
        private class State { public int stored; public float progress; }

        public int Stored => stored;
        public float Progress => progress;
        public override string PowerName => "Water Pump";
        public override int PowerPriority => 1;
        public override float PowerDemand => stored < maxStored ? powerKw : 0f;
        public override float HeatOutput => Granted ? heatPerSecond : 0f;
        private bool HasCrate => StorageRegistry.Nearest(PowerPosition, crateRange) != null;

        public override string PowerStatus
        {
            get
            {
                string destination = HasCrate ? "delivering to a crate" : $"{stored} water held, no crate in {crateRange:0} m";
                if (stored >= maxStored) return $"full, {destination}";
                return Granted ? $"pumping, {destination}, next in {PowerNetworks.FormatTime(secondsPerWater - progress)}" : $"{NoPowerReason()}, {destination}";
            }
        }

        public override string InteractPrompt => stored > 0 ? $"Press E to collect {stored} Water ({PowerStatus})" : $"{PowerName}: {PowerStatus}";

        public override void Advance(float seconds)
        {
            // Water from before a crate was built moves over as soon as there is one.
            if (stored > 0 && StorageRegistry.TryAddNearby(PowerPosition, crateRange, WaterId, stored)) stored = 0;

            if (!Granted || stored >= maxStored) return;
            progress += seconds;
            if (progress < secondsPerWater) return;
            progress = 0f;
            if (!StorageRegistry.TryAddNearby(PowerPosition, crateRange, WaterId, 1)) stored++;
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
            inventory.Add(WaterId, stored);
            GameAudio.PlayAt(Sfx.Drink, transform.position, 0.6f, 15f, 0.05f, 0f, 1.3f);
            GameMessageBus.Post($"Collected {stored} Water.");
            stored = 0;
        }

        public override string CapturePieceState() => JsonUtility.ToJson(new State { stored = stored, progress = progress });

        public override void RestorePieceState(string state)
        {
            State saved = PowerNetworks.ParseState<State>(state);
            if (saved == null) return;
            stored = Mathf.Clamp(saved.stored, 0, maxStored);
            progress = stored < maxStored ? PowerNetworks.Sane(saved.progress, 0f, secondsPerWater) : 0f;
        }
    }
}
