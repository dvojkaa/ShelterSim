namespace SheltreSim.Power
{
    /// <summary>
    /// Optional: lets a device tell the Power Console in its own words whether it runs and why not.
    /// Devices without it still appear there, described from the grid's own numbers.
    /// </summary>
    public interface IPowerStatus
    {
        string PowerStatus { get; }
        bool PowerRunning { get; }
    }
}
