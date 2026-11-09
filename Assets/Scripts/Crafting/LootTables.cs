using System.Collections.Generic;
using SheltreSim.Items;

namespace SheltreSim.Crafting
{
    /// <summary>
    /// Raw sources for scavenged containers. Fill uses ONLY the Random it is given (two draws per row, always),
    /// so a seeded caller gets the same crate every time.
    /// </summary>
    public static class LootTables
    {
        private struct Row
        {
            public string id; public double chance; public int min, max;
            public Row(string id, double chance, int min, int max) { this.id = id; this.chance = chance; this.min = min; this.max = max; }
        }

        public const int MaxStacksPerContainer = 6;

        public static readonly string[] TableNames =
            { "house", "car", "town", "hospital", "factory", "fuel", "farm", "military", "bunker", "relief" };

        private static Row Q(string id, double chance, int min = 1, int max = 1) => new Row(id, chance, min, max);

        private static readonly Dictionary<string, Row[]> tables = new Dictionary<string, Row[]>
        {
            { "house", new[] { Q("cloth", .5, 1, 3), Q("canned_food", .35, 1, 2), Q("nails", .3, 2, 6), Q("scrap_metal", .3, 1, 2),
                Q("bottled_water", .3, 1, 2), Q("duct_tape", .25), Q("plant_fiber", .25, 1, 3), Q("planks", .2, 1, 3), Q("bandage", .2),
                Q("sugar", .2, 1, 2), Q("glass", .15), Q("grain", .15), Q("seeds", .12), Q("herbs", .1), Q("first_aid", .08), Q("copper_wire", .08), Q("ammo_9mm", .05, 3, 8), Q("empty_bottle", .3, 1, 2) } },
            { "car", new[] { Q("scrap_metal", .6, 1, 3), Q("fuel", .25), Q("duct_tape", .25), Q("copper_wire", .2, 1, 2), Q("nails", .2, 2, 5),
                Q("cloth", .2, 1, 2), Q("bottled_water", .15), Q("glass", .1), Q("leather", .08, 1, 2), Q("electronics", .08), Q("empty_bottle", .15) } },
            { "town", new[] { Q("canned_food", .4, 1, 3), Q("bottled_water", .4, 1, 3), Q("cloth", .35, 1, 3), Q("nails", .3, 3, 8), Q("scrap_metal", .3, 1, 3),
                Q("duct_tape", .3, 1, 2), Q("bandage", .25), Q("glass", .25, 1, 2), Q("planks", .2, 1, 3), Q("copper_wire", .2, 1, 3), Q("sugar", .2),
                Q("rope", .15), Q("grain", .15), Q("electronics", .12), Q("first_aid", .12), Q("leather", .1), Q("chemicals", .06), Q("ammo_9mm", .08, 4, 10), Q("ammo_shell", .05, 2, 5), Q("empty_bottle", .25, 1, 2) } },
            { "hospital", new[] { Q("bandage", .6, 1, 3), Q("antiseptic", .45, 1, 2), Q("chemicals", .45, 1, 3), Q("cloth", .3, 1, 3), Q("first_aid", .3),
                Q("glass", .3, 1, 2), Q("bottled_water", .2), Q("antibiotics", .15), Q("sulfur", .12), Q("herbs", .1), Q("stimulant", .1), Q("anti_rad", .08) } },
            { "factory", new[] { Q("scrap_metal", .6, 2, 5), Q("copper_wire", .45, 2, 4), Q("electronics", .35, 1, 3), Q("nails", .35, 4, 10), Q("duct_tape", .3),
                Q("chemicals", .25, 1, 2), Q("coal", .2, 1, 3), Q("fuel", .15), Q("glass", .15), Q("iron_ore", .15, 1, 3), Q("rope", .1), Q("expedition_parts", .03), Q("sulfur", .2, 1, 2) } },
            { "fuel", new[] { Q("fuel", .85, 1, 3), Q("scrap_metal", .3, 1, 2), Q("bottled_water", .25), Q("duct_tape", .2), Q("canned_food", .2),
                Q("coal", .15, 1, 2), Q("chemicals", .12), Q("glass", .1), Q("cloth", .1) } },
            { "farm", new[] { Q("grain", .6, 2, 5), Q("seeds", .55, 2, 4), Q("vegetables", .35, 1, 3), Q("plant_fiber", .35, 2, 4), Q("dirty_water", .3, 1, 2),
                Q("herbs", .2, 1, 2), Q("rope", .15), Q("planks", .15, 1, 3), Q("canned_food", .15), Q("leather", .1), Q("sugar", .1) } },
            { "military", new[] { Q("electronics", .3, 1, 3), Q("copper_wire", .3, 2, 3), Q("canned_food", .3, 1, 3), Q("bandage", .3),
                Q("duct_tape", .3), Q("first_aid", .2), Q("fuel", .2), Q("leather", .15, 1, 2), Q("chemicals", .15), Q("sulfur", .15),
                Q("expedition_parts", .06), Q("ammo_9mm", .35, 6, 14), Q("ammo_rifle", .3, 4, 10), Q("ammo_shell", .2, 3, 6), Q("gunpowder", .15, 1, 3), Q("energy_cell", .08), Q("anti_rad", .06) } },
            { "bunker", new[] { Q("canned_food", .45, 1, 3), Q("bottled_water", .4, 1, 3), Q("bandage", .3), Q("first_aid", .25), Q("duct_tape", .25),
                Q("electronics", .2), Q("copper_wire", .2), Q("fuel", .2), Q("cloth", .2), Q("sugar", .2), Q("coal", .1), Q("expedition_parts", .05), Q("ammo_9mm", .2, 4, 10), Q("ammo_shell", .12, 2, 5) } },
            { "relief", new[] { Q("bottled_water", .6, 2, 4), Q("canned_food", .6, 2, 4), Q("bandage", .4, 1, 3), Q("cloth", .3, 1, 3), Q("first_aid", .2),
                Q("duct_tape", .2), Q("antiseptic", .15), Q("dirty_water", .1), Q("rope", .1), Q("expedition_parts", .03) } },
        };

        public static bool Has(string tableName) => tableName != null && tables.ContainsKey(tableName);

        public static IEnumerable<string> AllItemIds()
        {
            foreach (var t in tables) foreach (var r in t.Value) yield return r.id;
        }

        public static bool TableContains(string tableName, string id)
        {
            if (!Has(tableName)) return false;
            foreach (var r in tables[tableName]) if (r.id == id) return true;
            return false;
        }

        /// <summary>Rolls the named table into loot.entries using only the supplied random. Unknown tables leave the container empty.</summary>
        public static void Fill(LootContainer loot, string tableName, System.Random random)
        {
            if (loot == null) return;
            loot.entries = new List<LootEntry>();
            if (random == null || !tables.TryGetValue(tableName ?? "", out var rows)) { loot.hasLoot = false; return; }
            foreach (var row in rows)
            {
                double roll = random.NextDouble();
                int amount = row.min + (row.max > row.min ? random.Next(row.max - row.min + 1) : random.Next(1));
                if (roll >= row.chance || loot.entries.Count >= MaxStacksPerContainer) continue;
                var def = ItemCatalog.Def(row.id);
                if (def == null) continue;
                loot.entries.Add(new LootEntry { resource = def, amount = amount });
            }
            // A container worth drawing is worth opening: never empty, however the rolls fell.
            if (loot.entries.Count == 0 && rows.Length > 0 && ItemCatalog.Def(rows[0].id) != null)
                loot.entries.Add(new LootEntry { resource = ItemCatalog.Def(rows[0].id), amount = rows[0].min });
            loot.hasLoot = loot.entries.Count > 0;
        }
    }
}
