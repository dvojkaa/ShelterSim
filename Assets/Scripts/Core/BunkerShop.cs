using System;
using System.Collections.Generic;
using SheltreSim.Crafting;
using SheltreSim.Saving;

namespace SheltreSim.Core
{
    /// <summary>Static source data for the proposed pre-apocalypse bunker catalogue.
    /// Grant IDs that start with kit_ are planned build-kit items; ItemCatalog definitions
    /// are intentionally owned by the later ItemCatalog implementation task.</summary>
    public static class BunkerShop
    {
        public enum Category { Bunker, Rooms, PowerAndWater, Stations, Defence, Supplies, Gear, Vehicles }

        [Flags]
        public enum RoomBits
        {
            None = 0, Annex = 1, Workshop = 2, Hydro = 4, Vault = 8, ReinforcedDoor = 16
        }

        [Serializable]
        public sealed class Grant
        {
            public readonly string itemId;
            public readonly int quantity;
            public Grant(string itemId, int quantity) { this.itemId = itemId; this.quantity = quantity; }
        }

        [Serializable]
        public sealed class Offer
        {
            public readonly string id;
            public readonly string displayName;
            public readonly Category category;
            public readonly int price;
            public readonly int limit;
            public readonly int requiredStage;
            public readonly RoomBits requiredRooms;
            public readonly bool requiresContract;
            public readonly string exclusiveGroup;
            public readonly RoomBits roomBit;
            public readonly Grant[] grants;

            internal Offer(string id, string name, Category category, int price, int limit, int stage,
                RoomBits requiredRooms, string exclusiveGroup, RoomBits roomBit, params object[] grantPairs)
            {
                this.id = id; displayName = name; this.category = category; this.price = price;
                this.limit = limit; requiredStage = stage; this.requiredRooms = requiredRooms;
                requiresContract = id != "bunker_contract"; this.exclusiveGroup = exclusiveGroup;
                this.roomBit = roomBit;
                grants = new Grant[grantPairs.Length / 2];
                for (int i = 0; i < grants.Length; i++)
                    grants[i] = new Grant((string)grantPairs[i * 2], (int)grantPairs[i * 2 + 1]);
            }
        }

        // The offer constructor takes grant pairs directly, keeping this table readable and immutable.
        private static Offer O(string id, string name, Category cat, int price, int limit = 1, int stage = 0,
            RoomBits rooms = RoomBits.None, string exclusive = null, RoomBits roomBit = RoomBits.None,
            params object[] grants) => new Offer(id, name, cat, price, limit, stage, rooms, exclusive, roomBit, grants);

        private static readonly Offer[] offers =
        {
            O("bunker_contract", "Bunker Contract", Category.Bunker, 20000, 1, 0, RoomBits.None, null, RoomBits.None,
                "hammer",1, "canned_food",4, "bottled_water",4, "bandage",2, "paper_map",1),
            O("door_reinforced", "Reinforced Blast Door", Category.Bunker, 3000, 1, 3, RoomBits.None, null, RoomBits.ReinforcedDoor),
            O("room_annex", "Storage Annex", Category.Rooms, 4000, 1, 2, RoomBits.None, null, RoomBits.Annex,
                "kit_storage_crate",2),
            O("room_wing_workshop", "Workshop Wing", Category.Rooms, 6000, 1, 2, RoomBits.None, "north_wing", RoomBits.Workshop,
                "kit_workbench",1, "kit_piece_stove",1),
            O("room_wing_hydro", "Hydroponics Wing", Category.Rooms, 6000, 1, 2, RoomBits.None, "north_wing", RoomBits.Hydro,
                "kit_piece_farm",4, "kit_piece_irrigator",1, "kit_piece_light_ceiling",2, "seeds",10, "fertilizer",4),
            O("room_vault", "Vault", Category.Rooms, 3500, 1, 3, RoomBits.Annex, null, RoomBits.Vault,
                "kit_bed",1, "kit_storage_crate",1),
            O("kit_generator", "Generator + Fuel", Category.PowerAndWater, 2500, 1, 0, RoomBits.None, null, RoomBits.None,
                "kit_generator",1, "fuel",6),
            O("fuel_drums", "Fuel Drums", Category.PowerAndWater, 1500, 3, 0, RoomBits.None, null, RoomBits.None, "fuel",20),
            O("solar_array", "Solar Array", Category.PowerAndWater, 6500, 1, 0, RoomBits.None, null, RoomBits.None,
                "kit_piece_solar",2, "kit_piece_battery",1),
            O("water_collectors", "Water Collectors", Category.PowerAndWater, 800, 1, 0, RoomBits.None, null, RoomBits.None,
                "kit_water_collector",2),
            O("electric_well", "Electric Well", Category.PowerAndWater, 3500, 1, 0, RoomBits.None, null, RoomBits.None,
                "kit_piece_pump",1),
            O("workbench", "Workbench", Category.Stations, 1200, 1, 0, RoomBits.None, null, RoomBits.None, "kit_workbench",1),
            O("stove", "Stove", Category.Stations, 1500, 1, 0, RoomBits.None, null, RoomBits.None, "kit_piece_stove",1),
            O("forge", "Forge", Category.Stations, 3000, 1, 0, RoomBits.None, null, RoomBits.None, "kit_piece_forge",1),
            O("sandbags", "Sandbags", Category.Defence, 1200, 1, 0, RoomBits.None, null, RoomBits.None, "kit_piece_sandbag",6),
            O("spike_traps", "Spike Traps", Category.Defence, 1600, 1, 0, RoomBits.None, null, RoomBits.None, "kit_piece_spike_trap",4),
            O("alarm", "Raid Alarm", Category.Defence, 800, 1, 0, RoomBits.None, null, RoomBits.None, "kit_piece_alarm",1),
            // The MG is the strongest early defence. Requiring a Workshop makes it a deliberate
            // specialization rather than a first-click purchase before the player commits to a wing.
            O("mg_turret", "Machine Gun Turret", Category.Defence, 7500, 2, 0, RoomBits.Workshop, null, RoomBits.None,
                "kit_piece_turret_mg",1, "ammo_rifle",100),
            O("pantry", "Pantry Bundle", Category.Supplies, 1500, 3, 0, RoomBits.None, null, RoomBits.None,
                "canned_food",20, "bottled_water",20),
            O("medical", "Medical Bundle", Category.Supplies, 2500, 2, 0, RoomBits.None, null, RoomBits.None,
                "first_aid",4, "bandage",8, "antiseptic",4, "antibiotics",2, "anti_rad",3),
            O("seed_sack", "Seed Sack", Category.Supplies, 800, 2, 0, RoomBits.None, null, RoomBits.None,
                "seeds",15, "grain",10, "fertilizer",4),
            O("builder_pallet", "Builder Pallet", Category.Supplies, 2000, 3, 0, RoomBits.None, null, RoomBits.None,
                "planks",40, "nails",80, "hinges",6, "rope",10, "duct_tape",10),
            O("metal_stock", "Metal Stock", Category.Supplies, 4000, 2, 0, RoomBits.None, null, RoomBits.None,
                "steel_bar",20, "metal_plate",20, "reinforced_frame",4),
            O("tool_set", "Tool Set", Category.Gear, 1000, 1, 0, RoomBits.None, null, RoomBits.None,
                "iron_axe",1, "iron_pickaxe",1, "machete",1),
            O("hazard_kit", "Hazard Kit", Category.Gear, 1500, 1, 0, RoomBits.None, null, RoomBits.None,
                "gas_mask",1, "anti_rad",3, "padded_jacket",1),
            O("backpack_frame", "Backpack Frame", Category.Gear, 1200, 2, 0, RoomBits.None, null, RoomBits.None,
                "backpack_frame",1),
            O("pistol_kit", "Pistol Kit", Category.Gear, 1800, 1, 0, RoomBits.None, null, RoomBits.None,
                "pistol",1, "ammo_9mm",60),
            O("shotgun_kit", "Shotgun Kit", Category.Gear, 2500, 1, 0, RoomBits.None, null, RoomBits.None,
                "shotgun",1, "ammo_shell",36),
            O("bicycle", "Bicycle", Category.Vehicles, 1500, 1, 0, RoomBits.None, null, RoomBits.None,
                "kit_piece_bicycle",1),
            // Mobility can multiply the value of every expedition loot table; require the Annex
            // as a meaningful early budget choice while retaining the designed $12k price.
            O("buggy", "Scout Buggy", Category.Vehicles, 12000, 1, 0, RoomBits.Annex, null, RoomBits.None,
                "kit_piece_scout_buggy",1, "fuel",4),
        };

        private static readonly Dictionary<string, Offer> byId = BuildIndex();
        private static Dictionary<string, Offer> BuildIndex()
        {
            var map = new Dictionary<string, Offer>(StringComparer.Ordinal);
            foreach (Offer offer in offers) map.Add(offer.id, offer);
            return map;
        }

        public static IReadOnlyList<Offer> All => offers;
        public static Offer Get(string id) => id != null && byId.TryGetValue(id, out Offer offer) ? offer : null;
        public static IReadOnlyList<Offer> ForCategory(Category category)
        {
            var result = new List<Offer>();
            foreach (Offer offer in offers) if (offer.category == category) result.Add(offer);
            return result;
        }

        /// <summary>Best-effort needs hints from carried, delivered and already placed equipment.</summary>
        public static IReadOnlyList<string> DependencyHints(HomeBase home)
        {
            var hints = new List<string>();
            if (home == null) return hints;
            int Count(string id)
            {
                int count = home.inventory != null ? home.inventory.GetCount(id) : 0;
                if (home.supplyCage != null) count = (int)Math.Min(int.MaxValue, (long)count + home.supplyCage.GetCount(id));
                return count;
            }
            bool Placed(string id)
            {
                foreach (PlacedPiece piece in PlacedPieceRegistry.All)
                    if (piece != null && piece.pieceId == id) return true;
                return false;
            }
            bool hasGenerator = Count("kit_generator") > 0 || Count("generator") > 0 || Placed("generator");
            bool hasSolar = Count("kit_piece_solar") > 0 || Count("piece_solar") > 0 || Placed("piece_solar");
            if (!hasGenerator && !hasSolar) hints.Add("Needs power: you own no generator or solar array.");
            if (hasGenerator && !hasSolar && Count("fuel") == 0) hints.Add("Needs fuel: you have no fuel for a generator.");
            if (Count("seeds") == 0) hints.Add("Needs seeds: you have none to plant.");
            if (Count("kit_piece_stove") == 0 && Count("piece_stove") == 0 && !Placed("piece_stove"))
                hints.Add("Needs a stove to cook food or boil water.");
            return hints;
        }
    }
}
