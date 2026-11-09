namespace SheltreSim.Power
{
    /// <summary>
    /// A device that draws power. The grid also calls SetPowered(true) on an idle device when its network has any
    /// power at all, so that alone doesn't mean the device was given anything. Granted means it asked for power on this
    /// tick and got it, and it's the only thing that may advance gameplay.
    /// </summary>
    public abstract class PowerConsumerDevice : PowerDevice, IPowerConsumer
    {
        protected bool Granted { get; private set; }

        public abstract float PowerDemand { get; }
        public virtual int PowerPriority => 2;
        public virtual float HeatOutput => 0f;
        public virtual float CoolingOutput => 0f;
        public override bool PowerRunning => Granted;

        public void SetPowered(bool powered) => Granted = powered && PowerDemand > 0f;

        protected override void JoinGrid() => PowerGrid.Register(this);

        protected override void LeaveGrid()
        {
            PowerGrid.Unregister(this);
            Granted = false;
        }

        /// <summary>Why a device that wants power isn't getting it.</summary>
        protected string NoPowerReason()
        {
            PowerNetwork network = PowerNetworks.NetworkOf(this);
            return network == null || (network.Supply <= 0f && network.Stored <= 0f)
                ? "no power, nothing in range is producing"
                : "not enough power, more important devices come first";
        }
    }
}
