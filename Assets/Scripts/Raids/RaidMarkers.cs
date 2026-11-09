namespace SheltreSim.Raids
{
    /// <summary>A raider that passes within a few metres of this, in sight, stops and destroys it. Turrets.</summary>
    public interface IRaidPriorityTarget { }

    /// <summary>A stuck raider doesn't pick this as "the nearest thing to break". Traps: they lie flat and don't block anything.</summary>
    public interface IRaidIgnored { }
}
