using System;
using UnityEngine;

namespace SheltreSim.Power
{
    /// <summary>Stores surplus and carries a load when the sources can't. Starts empty.</summary>
    public sealed class BatteryPiece : PowerDevice, IPowerStorage
    {
        public float capacityKilojoules = 600f;
        public float maxFlowKw = 4f;

        private float stored;

        [Serializable]
        private class State { public float stored; }

        public float StoredEnergy => stored;
        public float EnergyCapacity => capacityKilojoules;
        public float MaxFlow => maxFlowKw;
        public override string PowerName => "Battery";
        public override bool PowerRunning => stored > 0f;

        public override string PowerStatus =>
            $"{(capacityKilojoules > 0f ? Mathf.RoundToInt(100f * stored / capacityKilojoules) : 0)}% charged, {stored:F0} of {capacityKilojoules:F0} kJ";

        public float Charge(float kilojoules)
        {
            if (!(kilojoules > 0f)) return 0f;
            float taken = Mathf.Min(kilojoules, capacityKilojoules - stored);
            if (taken <= 0f) return 0f;
            stored += taken;
            return taken;
        }

        public float Discharge(float kilojoules)
        {
            if (!(kilojoules > 0f)) return 0f;
            float given = Mathf.Min(kilojoules, stored);
            stored -= given;
            return given;
        }

        protected override void JoinGrid() => PowerGrid.Register(this);
        protected override void LeaveGrid() => PowerGrid.Unregister(this);

        public override string CapturePieceState() => JsonUtility.ToJson(new State { stored = stored });

        public override void RestorePieceState(string state)
        {
            State saved = PowerNetworks.ParseState<State>(state);
            if (saved != null) stored = PowerNetworks.Sane(saved.stored, 0f, capacityKilojoules);
        }
    }
}
