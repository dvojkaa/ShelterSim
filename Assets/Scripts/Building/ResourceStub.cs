namespace SheltreSim.Building
{
    /// <summary>
    /// M2 placeholder — always returns true, resources are free for now. This is the
    /// single integration seam for affordability checks: when the real inventory/
    /// resource system lands later, only this method's body should need to change.
    /// Do not let other building-system code grow its own separate "do we have enough
    /// stuff" logic — route every affordability check through here.
    /// </summary>
    public static class ResourceStub
    {
        public static bool TryAfford(int cost) => true;
    }
}
