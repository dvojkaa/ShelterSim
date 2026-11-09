using System;
using System.Collections.Generic;
using UnityEngine;

namespace SheltreSim.Building
{
    /// <summary>A piece that may refuse being taken apart, e.g. a generator with fuel in it. Implement on the piece's root.</summary>
    public interface IDismantleGuard
    {
        bool CanDismantle(out string reason);
    }

    public enum HomesteadMaterial { Wood, Metal, Stone }

    public sealed class HomesteadEntry
    {
        public string id, displayName;
        public Vector3 size;
        public HomesteadMaterial material;
        public float hitPoints;
        public BuildPlacementRule rule;
        public HomesteadEntry(string id, string displayName, float w, float h, float d, HomesteadMaterial material, float hitPoints, BuildPlacementRule rule)
        {
            this.id = id; this.displayName = displayName; size = new Vector3(w, h, d);
            this.material = material; this.hitPoints = hitPoints; this.rule = rule;
        }
    }

    /// <summary>
    /// The structural kit's table: sizes, materials, hit points, placement rules. Costs stay in RecipeBook.PieceCosts.
    /// Also the build palette's category table and the material of each older piece, for repair kits.
    /// </summary>
    public static class HomesteadCatalog
    {
        public const string Hammer = "hammer", RepairKitWood = "repair_kit_wood", RepairKitMetal = "repair_kit_metal";
        public const float RepairFraction = 0.4f;
        public const float DismantleRefund = 0.5f;
        public const float ForgivenessSeconds = 30f;

        public static readonly IReadOnlyList<HomesteadEntry> Pieces = new[]
        {
            new HomesteadEntry("piece_foundation", "Foundation", 2f, .4f, 2f, HomesteadMaterial.Wood, 400f, BuildPlacementRule.Foundation),
            new HomesteadEntry("floor_tile", "Floor Tile", 2f, .2f, 2f, HomesteadMaterial.Wood, 150f, BuildPlacementRule.CellFloor),
            new HomesteadEntry("wall_segment", "Wall Segment", 2f, 3f, .2f, HomesteadMaterial.Wood, 180f, BuildPlacementRule.Wall),
            new HomesteadEntry("piece_wall_steel", "Steel Wall", 2f, 3f, .2f, HomesteadMaterial.Metal, 500f, BuildPlacementRule.Wall),
            new HomesteadEntry("piece_wall_half", "Half Wall", 2f, 1.5f, .2f, HomesteadMaterial.Wood, 90f, BuildPlacementRule.Wall),
            new HomesteadEntry("piece_wall_window", "Window Wall", 2f, 3f, .2f, HomesteadMaterial.Wood, 150f, BuildPlacementRule.Wall),
            new HomesteadEntry("piece_door_frame", "Door Frame", 2f, 3f, .2f, HomesteadMaterial.Wood, 150f, BuildPlacementRule.Wall),
            new HomesteadEntry("piece_door_wood", "Wooden Door", 1.6f, 2.2f, .08f, HomesteadMaterial.Wood, 120f, BuildPlacementRule.DoorInFrame),
            new HomesteadEntry("piece_door_steel", "Steel Door", 1.6f, 2.2f, .08f, HomesteadMaterial.Metal, 450f, BuildPlacementRule.DoorInFrame),
            new HomesteadEntry("piece_roof_flat", "Flat Roof", 2f, .2f, 2f, HomesteadMaterial.Wood, 120f, BuildPlacementRule.Roof),
            new HomesteadEntry("piece_roof_slope", "Sloped Roof", 2f, 1.2f, 2f, HomesteadMaterial.Wood, 140f, BuildPlacementRule.Roof),
            new HomesteadEntry("piece_ramp", "Entry Ramp", 2f, .4f, 2f, HomesteadMaterial.Wood, 120f, BuildPlacementRule.Access),
            new HomesteadEntry("piece_stairs", "Stairs", 2f, 3f, 4f, HomesteadMaterial.Wood, 180f, BuildPlacementRule.Access),
            new HomesteadEntry("piece_fence", "Fence", 2f, 1.2f, .1f, HomesteadMaterial.Wood, 70f, BuildPlacementRule.Perimeter),
            new HomesteadEntry("piece_gate", "Gate", 2f, 1.2f, .1f, HomesteadMaterial.Wood, 80f, BuildPlacementRule.Perimeter),
            new HomesteadEntry("piece_barricade", "Barricade", 2f, 1.2f, .4f, HomesteadMaterial.Wood, 300f, BuildPlacementRule.Perimeter),
            new HomesteadEntry("piece_palisade", "Palisade", 2f, 3f, .3f, HomesteadMaterial.Wood, 350f, BuildPlacementRule.Perimeter),
        };

        public static readonly string[] Categories = { "Structure", "Doors", "Furniture", "Stations", "Power", "Automation", "Defence", "Farming", "Special" };
        public const string DefaultCategory = "Special";

        private static readonly Dictionary<string, string> CategoryById = new Dictionary<string, string>
        {
            { "piece_foundation", "Structure" }, { "floor_tile", "Structure" }, { "wall_segment", "Structure" }, { "piece_wall_steel", "Structure" },
            { "piece_wall_half", "Structure" }, { "piece_wall_window", "Structure" }, { "piece_door_frame", "Structure" }, { "piece_roof_flat", "Structure" },
            { "piece_roof_slope", "Structure" }, { "piece_ramp", "Structure" }, { "piece_stairs", "Structure" },
            { "piece_door_wood", "Doors" }, { "piece_door_steel", "Doors" }, { "piece_gate", "Doors" },
            { "bed", "Furniture" }, { "storage_crate", "Furniture" }, { "piece_shelf", "Furniture" }, { "piece_lamp", "Furniture" },
            { "workbench", "Stations" }, { "piece_stove", "Stations" }, { "piece_forge", "Stations" }, { "piece_chemlab", "Stations" },
            { "piece_electronics", "Stations" }, { "piece_armory", "Stations" },
            { "generator", "Power" }, { "piece_solar", "Power" }, { "piece_battery", "Power" }, { "piece_cooler", "Power" },
            { "piece_console", "Power" }, { "piece_charger", "Power" },
            { "piece_pump", "Automation" }, { "piece_irrigator", "Automation" }, { "piece_fabricator", "Automation" }, { "piece_repair", "Automation" },
            { "piece_fence", "Defence" }, { "piece_barricade", "Defence" }, { "piece_palisade", "Defence" }, { "piece_sandbag", "Defence" },
            { "piece_spike_trap", "Defence" }, { "piece_shock_trap", "Defence" }, { "piece_turret_mg", "Defence" }, { "piece_turret_laser", "Defence" }, { "piece_alarm", "Defence" },
            { "piece_farm", "Farming" }, { "water_collector", "Farming" }, { "piece_filter", "Farming" },
            { "piece_radio", "Special" }, { "piece_radio_console", "Special" },
        };

        // Everything not listed here and not in Pieces is metal, the common case for machines.
        private static readonly HashSet<string> WoodenPieces = new HashSet<string> { "bed", "storage_crate", "piece_shelf", "workbench", "piece_farm" };
        private static readonly HashSet<string> StonePieces = new HashSet<string> { "piece_stove", "piece_forge", "piece_sandbag" };

        private static readonly HashSet<string> FullWalls = new HashSet<string> { "wall_segment", "piece_wall_steel", "piece_wall_window", "piece_door_frame" };

        private static readonly Dictionary<string, HomesteadEntry> byId = BuildIndex();

        private static Dictionary<string, HomesteadEntry> BuildIndex()
        {
            var index = new Dictionary<string, HomesteadEntry>(StringComparer.Ordinal);
            foreach (HomesteadEntry entry in Pieces) index[entry.id] = entry;
            return index;
        }

        public static HomesteadEntry Find(string id) => id != null && byId.TryGetValue(id, out HomesteadEntry entry) ? entry : null;
        public static BuildPlacementRule RuleFor(string id) => Find(id)?.rule ?? BuildPlacementRule.Free;
        public static string CategoryFor(string id) => id != null && CategoryById.TryGetValue(id, out string category) ? category : DefaultCategory;

        public static HomesteadMaterial MaterialFor(string id)
        {
            HomesteadEntry entry = Find(id);
            if (entry != null) return entry.material;
            if (id != null && WoodenPieces.Contains(id)) return HomesteadMaterial.Wood;
            if (id != null && StonePieces.Contains(id)) return HomesteadMaterial.Stone;
            return HomesteadMaterial.Metal;
        }

        /// <summary>Wood needs the wood kit. Metal and stone both take the metal kit.</summary>
        public static string RepairKitFor(string id) => MaterialFor(id) == HomesteadMaterial.Wood ? RepairKitWood : RepairKitMetal;

        /// <summary>A 3 m wall piece: something can stand on it, and a roof can rest on it.</summary>
        public static bool IsFullWall(string id) => id != null && FullWalls.Contains(id);

        /// <summary>A flat surface a wall can stand on.</summary>
        public static bool IsWallBase(string id) => id == "piece_foundation" || id == "floor_tile" || id == "piece_roof_flat";

        /// <summary>
        /// The amount handed back for one cost line. Rounded down, so it can never exceed what went in.
        /// Forgiven (just placed by mistake, undamaged): everything. Otherwise half, scaled by remaining health.
        /// </summary>
        public static int RefundAmount(int paid, float healthFraction, bool forgiven)
        {
            if (paid <= 0) return 0;
            if (forgiven) return paid;
            return Mathf.Clamp(Mathf.FloorToInt(paid * DismantleRefund * Mathf.Clamp01(healthFraction) + 0.0001f), 0, paid);
        }
    }
}
