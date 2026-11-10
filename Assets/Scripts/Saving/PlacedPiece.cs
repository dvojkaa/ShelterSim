using UnityEngine;

namespace SheltreSim.Saving
{
    /// <summary>
    /// Tags a placed build piece for save/load tracking. Attached directly to every
    /// build-piece prefab, so every instantiated copy self-registers/unregisters with
    /// PlacedPieceRegistry automatically — nothing else needs to call Register/Unregister.
    /// </summary>
    public class PlacedPiece : MonoBehaviour
    {
        public string pieceId;
        public bool wasPlacedFromKit;

        private void OnEnable() => PlacedPieceRegistry.Register(this);

        private void OnDisable() => PlacedPieceRegistry.Unregister(this);
    }
}
