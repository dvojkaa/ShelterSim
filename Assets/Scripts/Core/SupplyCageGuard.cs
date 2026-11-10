using SheltreSim.Building;
using UnityEngine;

namespace SheltreSim.Core
{
    /// <summary>Protects the bunker delivery store from being dismantled by the player.</summary>
    public sealed class SupplyCageGuard : MonoBehaviour, IDismantleGuard
    {
        public bool CanDismantle(out string reason)
        {
            reason = "The bunker Supply Cage is fixed in place.";
            return false;
        }
    }
}
