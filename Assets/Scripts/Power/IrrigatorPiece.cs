using System.Collections.Generic;
using SheltreSim.Building;
using SheltreSim.Storage;
using UnityEngine;

namespace SheltreSim.Power
{
    /// <summary>
    /// While powered, farm plots in reach grow twice as fast and ready ones are harvested into a crate in reach.
    /// Draws nothing when there's nothing for it to do. Having nothing to save, it stays stateless.
    /// </summary>
    public sealed class IrrigatorPiece : PowerConsumerDevice
    {
        private static readonly List<IrrigatorPiece> all = new List<IrrigatorPiece>();

        public float powerKw = 0.5f;
        public float range = 8f;
        public float crateRange = 8f;

        public override string PowerName => "Irrigator";
        public override float PowerDemand => HasWork() ? powerKw : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        /// <summary>Whether a powered irrigator reaches this position. Several in reach still count once.</summary>
        public static bool AnyIrrigating(Vector3 position)
        {
            foreach (IrrigatorPiece irrigator in all)
                if (irrigator.Granted && (irrigator.PowerPosition - position).sqrMagnitude <= irrigator.range * irrigator.range) return true;
            return false;
        }

        protected override void JoinGrid()
        {
            base.JoinGrid();
            if (!all.Contains(this)) all.Add(this);
        }

        protected override void LeaveGrid()
        {
            base.LeaveGrid();
            all.Remove(this);
        }

        private bool InRange(FarmPlot plot) => (plot.transform.position - PowerPosition).sqrMagnitude <= range * range;

        private bool HasWork()
        {
            bool crate = StorageRegistry.Nearest(PowerPosition, crateRange) != null;
            foreach (FarmPlot plot in FarmPlot.All)
                if (InRange(plot) && (plot.State == FarmPlot.Growing || (plot.State == FarmPlot.Ready && crate))) return true;
            return false;
        }

        public override void Advance(float seconds)
        {
            if (!Granted) return;
            foreach (FarmPlot plot in FarmPlot.All)
                if (InRange(plot)) plot.TryAutoHarvest(PowerPosition, crateRange);
        }

        public override string PowerStatus
        {
            get
            {
                int plots = 0, ready = 0;
                foreach (FarmPlot plot in FarmPlot.All)
                {
                    if (!InRange(plot)) continue;
                    plots++;
                    if (plot.State == FarmPlot.Ready) ready++;
                }
                if (plots == 0) return $"idle, no Farm Plot within {range:0} m";
                bool crate = StorageRegistry.Nearest(PowerPosition, crateRange) != null;
                if (PowerDemand <= 0f) return ready > 0 ? $"idle, {ready} plots ready but no crate within {crateRange:0} m" : "idle, nothing is growing";
                if (!Granted) return NoPowerReason();
                return ready > 0 && !crate ? $"watering, {ready} plots ready but no crate within {crateRange:0} m" : $"watering {plots} plots";
            }
        }
    }
}
