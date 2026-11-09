namespace SheltreSim.Power
{
    /// <summary>
    /// Removes heat from its whole network. Idles when there's no heat and nothing producing any,
    /// so it can't drain a solar and battery base overnight for nothing.
    /// </summary>
    public sealed class CoolingUnitPiece : PowerConsumerDevice
    {
        public float powerKw = 1f;
        public float coolingPerSecond = 8f;

        public override string PowerName => "Cooling Unit";
        public override int PowerPriority => 1;
        public override float CoolingOutput => Granted ? coolingPerSecond : 0f;
        public override float PowerDemand => HasWork() ? powerKw : 0f;

        private bool HasWork()
        {
            PowerNetwork network = PowerNetworks.NetworkOf(this);
            if (network == null) return false;
            if (network.Heat > 0f) return true;
            foreach (IPowerSource source in network.Sources)
                if (PowerNetworks.IsAlive(source) && source.PowerOutput > 0f && source.HeatOutput > 0f) return true;
            return false;
        }

        public override string PowerStatus
        {
            get
            {
                PowerNetwork network = PowerNetworks.NetworkOf(this);
                int heat = network != null ? UnityEngine.Mathf.RoundToInt(network.Heat) : 0;
                if (PowerDemand <= 0f) return "idle, nothing to cool";
                return Granted ? $"cooling, heat {heat} of 100" : $"{NoPowerReason()}, heat {heat} of 100";
            }
        }
    }
}
