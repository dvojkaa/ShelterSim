using System.Collections.Generic;

namespace SheltreSim.Saving
{
    /// <summary>Runtime index of every currently-placed PlacedPiece, for save/load.</summary>
    public static class PlacedPieceRegistry
    {
        private static readonly List<PlacedPiece> all = new List<PlacedPiece>();

        public static IReadOnlyList<PlacedPiece> All => all;

        public static void Register(PlacedPiece piece)
        {
            if (!all.Contains(piece))
            {
                all.Add(piece);
            }
        }

        public static void Unregister(PlacedPiece piece)
        {
            all.Remove(piece);
        }
    }
}
