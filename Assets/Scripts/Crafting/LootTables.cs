using System.Collections.Generic;
using SheltreSim.Items;
using SheltreSim.Bestiary;
using UnityEngine;

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

        private sealed class SlotTable
        {
            public Row[] rows;
            public double emptyChance;
            public SlotTable(double emptyChance, params Row[] rows) { this.emptyChance = emptyChance; this.rows = rows; }
        }

        public const int MaxStacksPerContainer = 6;

        public static readonly string[] TableNames =
            { "house", "car", "town", "hospital", "factory", "fuel", "farm", "military", "bunker", "relief" };

        public static readonly string[] SlotTableNames =
            { "kitchen_run", "fridge", "pantry_shelf", "wardrobe", "dresser", "linen_closet", "nightstand",
              "medicine_cabinet", "tv_unit", "desk", "filing_cabinet", "bookcase", "toolbox", "lockers",
              "shop_shelf", "counter", "pharmacy", "hardware", "mail_lockers", "safe", "roof" };

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

        // Small, readable furniture inventories. These deliberately do not force a minimum item:
        // every slot has an independent empty roll and may contain zero, one, or several stacks.
        private static readonly Dictionary<string, SlotTable> slotTables = new Dictionary<string, SlotTable>
        {
            { "kitchen_run", new SlotTable(.30, Q("canned_food",.35,1,2), Q("sugar",.25), Q("grain",.20), Q("empty_bottle",.30), Q("glass",.15), Q("duct_tape",.10), Q("chemicals",.08)) },
            { "fridge", new SlotTable(.50, Q("bottled_water",.30), Q("dirty_water",.30), Q("vegetables",.10), Q("canned_food",.10)) },
            { "pantry_shelf", new SlotTable(.20, Q("canned_food",.45,1,3), Q("bottled_water",.35,1,2), Q("sugar",.20), Q("seeds",.15,1,3), Q("herbs",.10)) },
            { "wardrobe", new SlotTable(.25, Q("cloth",.60,1,4), Q("leather",.12), Q("rope",.10), Q("ammo_9mm",.04,4,8)) },
            { "dresser", new SlotTable(.25, Q("cloth",.60,1,4), Q("leather",.12), Q("rope",.10)) },
            { "linen_closet", new SlotTable(.25, Q("cloth",.60,1,4), Q("leather",.12), Q("rope",.10)) },
            { "nightstand", new SlotTable(.50, Q("bandage",.20), Q("cloth",.20), Q("stimulant",.04), Q("ammo_9mm",.06,3,6), Q("energy_cell",.02)) },
            { "medicine_cabinet", new SlotTable(.35, Q("bandage",.40,1,2), Q("antiseptic",.30), Q("first_aid",.10), Q("antibiotics",.06), Q("anti_rad",.04), Q("empty_bottle",.20)) },
            { "tv_unit", new SlotTable(.40, Q("electronics",.25), Q("copper_wire",.25,1,3), Q("duct_tape",.15), Q("glass",.10), Q("energy_cell",.03)) },
            { "desk", new SlotTable(.40, Q("electronics",.25), Q("copper_wire",.25,1,3), Q("duct_tape",.15), Q("glass",.10), Q("energy_cell",.03)) },
            { "filing_cabinet", new SlotTable(.40, Q("electronics",.25), Q("copper_wire",.25,1,3), Q("duct_tape",.15), Q("glass",.10), Q("energy_cell",.03)) },
            { "bookcase", new SlotTable(.55, Q("cloth",.20), Q("planks",.25,1,2), Q("herbs",.05)) },
            { "toolbox", new SlotTable(.15, Q("nails",.50,3,8), Q("scrap_metal",.40,1,3), Q("duct_tape",.30), Q("hinges",.15), Q("copper_wire",.15), Q("rope",.15), Q("repair_kit_metal",.03)) },
            { "lockers", new SlotTable(.15, Q("nails",.50,3,8), Q("scrap_metal",.40,1,3), Q("duct_tape",.30), Q("hinges",.15), Q("copper_wire",.15), Q("rope",.15), Q("repair_kit_metal",.03)) },
            { "shop_shelf", new SlotTable(.25, Q("canned_food",.40,1,3), Q("bottled_water",.40,1,3), Q("sugar",.25), Q("bandage",.15), Q("duct_tape",.15)) },
            { "counter", new SlotTable(.25, Q("canned_food",.40,1,3), Q("bottled_water",.40,1,3), Q("sugar",.25), Q("bandage",.15), Q("duct_tape",.15), Q("ammo_9mm",.10,4,10)) },
            { "pharmacy", new SlotTable(.20, Q("bandage",.60,1,3), Q("antiseptic",.45,1,2), Q("chemicals",.45,1,3), Q("cloth",.30,1,3), Q("first_aid",.30), Q("glass",.30,1,2), Q("bottled_water",.20), Q("antibiotics",.15), Q("sulfur",.12), Q("herbs",.10), Q("stimulant",.10), Q("anti_rad",.08)) },
            { "hardware", new SlotTable(.20, Q("nails",.50,3,8), Q("scrap_metal",.40,1,3), Q("duct_tape",.30), Q("hinges",.15), Q("copper_wire",.15), Q("rope",.15), Q("repair_kit_metal",.03)) },
            { "mail_lockers", new SlotTable(.60, Q("cloth",.30), Q("electronics",.10), Q("duct_tape",.10)) },
            { "safe", new SlotTable(0, Q("ammo_9mm",.40,6,14), Q("first_aid",.30), Q("energy_cell",.15), Q("expedition_parts",.08), Q("gunpowder",.20,1,3)) },
            { "roof", new SlotTable(.40, Q("fuel",.30), Q("ammo_rifle",.20,4,10), Q("bottled_water",.30,1,2), Q("rope",.20), Q("energy_cell",.08)) },
        };

        public static bool Has(string tableName) => tableName != null && (tables.ContainsKey(tableName) || slotTables.ContainsKey(tableName));

        public static IEnumerable<string> AllItemIds()
        {
            foreach (var t in tables) foreach (var r in t.Value) yield return r.id;
            foreach (var t in slotTables) foreach (var r in t.Value.rows) yield return r.id;
        }

        public static bool TableContains(string tableName, string id)
        {
            if (tableName == null) return false;
            if (tables.TryGetValue(tableName, out Row[] rows))
                foreach (var r in rows) if (r.id == id) return true;
            if (slotTables.TryGetValue(tableName, out SlotTable slot))
                foreach (var r in slot.rows) if (r.id == id) return true;
            return false;
        }

        /// <summary>Rolls a deterministic, possibly empty furniture slot. Two RNG draws per row are always consumed.</summary>
        public static void FillSlot(LootContainer loot, string tableName, System.Random random, int tier)
        {
            if (loot == null) return;
            loot.entries = new List<LootEntry>();
            if (random == null || !slotTables.TryGetValue(tableName ?? string.Empty, out SlotTable slot))
            {
                loot.hasLoot = false;
                return;
            }

            int stacks = 0;
            for (int i = 0; i < slot.rows.Length; i++)
            {
                Row row = slot.rows[i];
                double roll = random.NextDouble();
                int amount = row.min + random.Next(row.max - row.min + 1);
                bool valid = stacks < MaxStacksPerContainer && ItemCatalog.Def(row.id) != null;
                double chance = row.chance * (RareRows.Contains(row.id) ? System.Math.Min(1.6, 1.0 + .12 * System.Math.Max(0, tier)) : 1.0);
                if (valid && roll < chance)
                {
                    loot.entries.Add(new LootEntry { resource = ItemCatalog.Def(row.id), amount = amount });
                    stacks++;
                }
            }

            double emptyRoll = random.NextDouble();
            if (emptyRoll < slot.emptyChance) loot.entries.Clear();
            loot.hasLoot = loot.entries.Count > 0;
        }

        private static readonly HashSet<string> RareRows = new HashSet<string>
        {
            "ammo_9mm", "ammo_rifle", "ammo_shell", "first_aid", "antibiotics", "anti_rad",
            "energy_cell", "expedition_parts", "circuit_board"
        };

        /// <summary>Rolls the named table into loot.entries using only the supplied random. Unknown tables leave the container empty.</summary>
        public static void Fill(LootContainer loot, string tableName, System.Random random)
            => Fill(loot, tableName, random, 0);

        /// <summary>Distance-tiered version of Fill; farther expeditions improve rare finds without changing seeded draw order.</summary>
        public static void FillAtWorldPosition(LootContainer loot, string tableName, System.Random random, Vector3 worldPosition)
            => Fill(loot, tableName, random, EncounterHabitats.DistanceRing(worldPosition) * 2);

        private static void Fill(LootContainer loot, string tableName, System.Random random, int tier)
        {
            if (loot == null) return;
            loot.entries = new List<LootEntry>();
            if (random == null || !tables.TryGetValue(tableName ?? "", out var rows)) { loot.hasLoot = false; return; }
            foreach (var row in rows)
            {
                double roll = random.NextDouble();
                int amount = row.min + (row.max > row.min ? random.Next(row.max - row.min + 1) : random.Next(1));
                double chance = row.chance * (RareRows.Contains(row.id) ? System.Math.Min(1.6, 1.0 + .12 * System.Math.Max(0, tier)) : 1.0);
                if (roll >= chance || loot.entries.Count >= MaxStacksPerContainer) continue;
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
