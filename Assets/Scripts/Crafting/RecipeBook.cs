using System;
using System.Collections.Generic;
using System.Text;
using SheltreSim.Items;

namespace SheltreSim.Crafting
{
    public enum StationKind { Hand = 0, Workbench = 1, Stove = 2, Forge = 3, ChemLab = 4, Electronics = 5, Armory = 6 }

    public struct Ingredient
    {
        public string id; public int amount;
        public Ingredient(string id, int amount) { this.id = id; this.amount = amount; }
    }

    public sealed class Recipe
    {
        public string id;
        public StationKind station;
        public string output;
        public int outputAmount;
        public Ingredient[] inputs;

        /// <summary>Inputs scaled by times as a CraftingCost list (null entries skipped by CraftingSystem if an asset is missing, so callers verify with Resolve).</summary>
        public List<CraftingCost> ToCost(int times = 1)
        {
            var list = new List<CraftingCost>();
            foreach (var i in inputs) list.Add(new CraftingCost { resource = ItemCatalog.Def(i.id), amount = i.amount * times });
            return list;
        }

        public bool Resolves()
        {
            if (ItemCatalog.Def(output) == null) return false;
            foreach (var i in inputs) if (ItemCatalog.Def(i.id) == null) return false;
            return true;
        }
    }

    /// <summary>The crafting ladder: Hand -> Workbench -> Stove / Forge -> ChemLab -> Electronics -> Radio Beacon.</summary>
    public static class RecipeBook
    {
        private static readonly List<Recipe> all = new List<Recipe>();
        private static readonly Dictionary<string, Recipe> byId = new Dictionary<string, Recipe>();
        public static IReadOnlyList<Recipe> All => all;
        public static Recipe Get(string id) => id != null && byId.TryGetValue(id, out var r) ? r : null;

        public static List<Recipe> ForStation(StationKind kind)
        {
            var list = new List<Recipe>();
            foreach (var r in all) if (r.station == kind) list.Add(r);
            return list;
        }

        private static Ingredient[] In(params object[] pairs)
        {
            var list = new Ingredient[pairs.Length / 2];
            for (int i = 0; i < list.Length; i++) list[i] = new Ingredient((string)pairs[i * 2], (int)pairs[i * 2 + 1]);
            return list;
        }

        private static void R(StationKind st, string output, int amount, params object[] inputs) => RV(output, st, output, amount, inputs);

        private static void RV(string id, StationKind st, string output, int amount, params object[] inputs)
        {
            var r = new Recipe { id = id, station = st, output = output, outputAmount = amount, inputs = In(inputs) };
            all.Add(r); byId[id] = r;
        }

        // ---------- Build piece costs (single source for M21CraftingSetup) ----------

        public static readonly Dictionary<string, Ingredient[]> PieceCosts = new Dictionary<string, Ingredient[]>
        {
            { "wall_segment", In("planks", 2, "nails", 4) },
            { "floor_tile", In("planks", 2, "nails", 2) },
            { "workbench", In("planks", 4, "nails", 8, "scrap_metal", 2) },
            { "bed", In("planks", 3, "cloth", 3) },
            { "storage_crate", In("planks", 3, "nails", 4) },
            { "water_collector", In("scrap_metal", 2, "planks", 1, "nails", 2) },
            { "generator", In("metal_plate", 3, "copper_wire", 2, "nails", 4) },
            { "piece_stove", In("stone", 8, "scrap_metal", 4, "reinforced_frame", 1, "nails", 4) },
            { "piece_forge", In("stone", 14, "scrap_metal", 6, "reinforced_frame", 2, "charcoal", 6) },
            { "piece_chemlab", In("planks", 4, "glass", 6, "metal_plate", 3, "steel_bar", 1, "reinforced_frame", 1) },
            { "piece_electronics", In("metal_plate", 4, "steel_bar", 2, "copper_wire", 6, "electronics", 4, "expedition_parts", 2, "reinforced_frame", 1) },
            { "piece_farm", In("planks", 4, "nails", 4, "rope", 2) },
            { "piece_wall_steel", In("metal_plate", 3, "steel_bar", 1) },
            { "piece_lamp", In("glass", 1, "copper_wire", 2, "scrap_metal", 1) },
            { "piece_shelf", In("planks", 3, "nails", 4) },
            { "piece_filter", In("metal_plate", 2, "glass", 2, "charcoal", 4, "cloth", 2) },
            { "piece_radio", In("radio_beacon", 1, "steel_bar", 2, "metal_plate", 4) },
            { "piece_armory", In("metal_plate", 4, "steel_bar", 2, "planks", 4, "reinforced_frame", 1) },
            // Power and heat.
            { "piece_console", In("circuit_board", 1, "metal_plate", 2, "copper_wire", 2) },
            { "piece_solar", In("solar_cell", 4, "metal_plate", 2, "copper_wire", 4) },
            { "piece_battery", In("capacitor", 2, "metal_plate", 2, "copper_wire", 4, "chemicals", 2) },
            { "piece_cooler", In("electric_motor", 1, "metal_plate", 3, "copper_wire", 2, "coolant", 2) },
            { "piece_charger", In("capacitor", 2, "circuit_board", 1, "metal_plate", 2, "copper_wire", 2) },
            // Automation.
            { "piece_pump", In("electric_motor", 1, "metal_plate", 2, "rope", 1, "glass", 1) },
            { "piece_irrigator", In("metal_plate", 2, "copper_wire", 1, "rope", 2, "glass", 2) },
            { "piece_fabricator", In("electric_motor", 2, "circuit_board", 2, "steel_bar", 2, "reinforced_frame", 1) },
            { "piece_repair", In("electric_motor", 1, "circuit_board", 1, "metal_plate", 3, "scrap_metal", 4) },
            // Homestead: a 2 m kit for building above ground.
            { "piece_foundation", In("planks", 4, "nails", 4) },
            { "piece_wall_half", In("planks", 1, "nails", 2) },
            { "piece_wall_window", In("planks", 2, "nails", 4) },
            { "piece_door_frame", In("planks", 2, "nails", 4) },
            { "piece_door_wood", In("planks", 3, "nails", 4) },
            { "piece_door_steel", In("metal_plate", 3, "steel_bar", 1, "hinges", 2) },
            { "piece_roof_flat", In("planks", 2, "nails", 2) },
            { "piece_roof_slope", In("planks", 3, "nails", 3) },
            { "piece_ramp", In("planks", 3, "nails", 4) },
            { "piece_stairs", In("planks", 6, "nails", 8) },
            { "piece_fence", In("planks", 3, "nails", 4) },
            { "piece_gate", In("planks", 3, "nails", 4, "hinges", 1) },
            { "piece_barricade", In("planks", 4, "nails", 4, "rope", 1) },
            { "piece_palisade", In("wood", 6, "rope", 2) },
            // Radio.
            { "piece_radio_console", In("circuit_board", 2, "copper_wire", 4, "metal_plate", 2, "electronics", 2) },
            // Defence.
            { "piece_sandbag", In("cloth", 3, "stone", 4) },
            { "piece_spike_trap", In("nails", 12, "planks", 2, "scrap_metal", 2) },
            { "piece_shock_trap", In("capacitor", 1, "copper_wire", 3, "scrap_metal", 3) },
            { "piece_turret_mg", In("steel_bar", 2, "metal_plate", 4, "electric_motor", 1, "circuit_board", 1) },
            { "piece_turret_laser", In("laser_diode", 2, "capacitor", 2, "metal_plate", 3, "circuit_board", 2, "coolant", 1) },
            { "piece_alarm", In("electronics", 1, "copper_wire", 2, "scrap_metal", 2) },
        };

        public static string PieceIdFor(StationKind kind)
        {
            switch (kind)
            {
                case StationKind.Workbench: return "workbench";
                case StationKind.Stove: return "piece_stove";
                case StationKind.Forge: return "piece_forge";
                case StationKind.ChemLab: return "piece_chemlab";
                case StationKind.Electronics: return "piece_electronics";
                case StationKind.Armory: return "piece_armory";
                default: return null;
            }
        }

        public static string StationName(StationKind kind)
        {
            switch (kind)
            {
                case StationKind.Hand: return "Hand Crafting";
                case StationKind.Workbench: return "Workbench";
                case StationKind.Stove: return "Stove";
                case StationKind.Forge: return "Forge";
                case StationKind.ChemLab: return "Chem Lab";
                case StationKind.Armory: return "Armory";
                default: return "Electronics Bench";
            }
        }

        static RecipeBook()
        {
            const StationKind H = StationKind.Hand, W = StationKind.Workbench, S = StationKind.Stove,
                F = StationKind.Forge, C = StationKind.ChemLab, E = StationKind.Electronics, A = StationKind.Armory;

            // Hand: basics from raw materials only.
            R(H, "planks", 3, "wood", 2);
            R(H, "rope", 1, "plant_fiber", 3);
            R(H, "nails", 4, "scrap_metal", 1);
            R(H, "bandage", 1, "cloth", 2);
            R(H, "charcoal", 2, "wood", 4);
            R(H, "stone_axe", 1, "stone", 3, "wood", 2, "plant_fiber", 2);
            R(H, "stone_pickaxe", 1, "stone", 3, "wood", 2, "plant_fiber", 2);
            R(H, "wooden_club", 1, "wood", 4, "plant_fiber", 1);

            // Workbench: needs planks and nails to build; frames unlock every other station.
            R(W, "reinforced_frame", 1, "planks", 4, "nails", 8, "scrap_metal", 2);
            RV("planks_bulk", W, "planks", 10, "wood", 6);
            RV("nails_bulk", W, "nails", 14, "scrap_metal", 3);
            R(W, "weapon_upgrade", 1, "scrap_metal", 2, "wood", 2, "plant_fiber", 1);
            R(W, "spiked_club", 1, "wooden_club", 1, "nails", 6, "scrap_metal", 1);
            R(W, "padded_jacket", 1, "cloth", 8, "plant_fiber", 4, "rope", 1);
            R(W, "leather_armor", 1, "leather", 6, "rope", 3, "nails", 6);
            R(W, "warm_coat", 1, "cloth", 6, "leather", 3, "rope", 2);

            // Stove: cooking. Wood is the fuel.
            R(S, "cooked_meat", 1, "raw_meat", 1, "wood", 1);
            R(S, "meat_stew", 1, "raw_meat", 1, "vegetables", 1, "water", 1, "wood", 1);
            R(S, "bread", 2, "grain", 2, "water", 1, "wood", 1);
            R(S, "berry_jam", 2, "food_berries", 4, "sugar", 1, "glass", 1);
            R(S, "herbal_tea", 2, "herbs", 2, "water", 1, "wood", 1);
            R(S, "boiled_water", 2, "dirty_water", 2, "wood", 1);
            RV("charcoal_stove", S, "charcoal", 6, "wood", 8);

            // Forge: needs coal or charcoal.
            R(F, "iron_ingot", 1, "iron_ore", 2, "coal", 1);
            RV("iron_ingot_charcoal", F, "iron_ingot", 1, "iron_ore", 2, "charcoal", 2);
            R(F, "steel_bar", 1, "iron_ingot", 2, "coal", 2);
            RV("steel_bar_charcoal", F, "steel_bar", 1, "iron_ingot", 2, "charcoal", 3);
            R(F, "metal_plate", 2, "scrap_metal", 3, "coal", 1);
            RV("metal_plate_charcoal", F, "metal_plate", 2, "scrap_metal", 3, "charcoal", 1);
            R(F, "iron_axe", 1, "iron_ingot", 3, "planks", 2, "rope", 1);
            R(F, "iron_pickaxe", 1, "iron_ingot", 3, "planks", 2, "rope", 1);
            R(F, "steel_axe", 1, "steel_bar", 2, "iron_ingot", 1, "planks", 2, "rope", 1);
            R(F, "steel_pickaxe", 1, "steel_bar", 2, "iron_ingot", 1, "planks", 2, "rope", 1);
            R(F, "machete", 1, "iron_ingot", 3, "duct_tape", 1, "leather", 1);
            R(F, "fire_axe", 1, "steel_bar", 2, "planks", 3, "duct_tape", 2);
            R(F, "steel_sword", 1, "steel_bar", 4, "leather", 2, "rope", 1);
            R(F, "scrap_armor", 1, "metal_plate", 6, "leather", 2, "rope", 3, "duct_tape", 2);
            R(F, "steel_plate_armor", 1, "metal_plate", 8, "steel_bar", 4, "leather", 4, "rope", 3);

            // Chem Lab: medicine and preserving.
            R(C, "antiseptic", 2, "chemicals", 1, "water", 1, "herbs", 1);
            R(C, "first_aid", 1, "bandage", 2, "antiseptic", 1, "cloth", 1);
            R(C, "antibiotics", 2, "chemicals", 2, "sulfur", 1, "herbs", 2, "glass", 1);
            R(C, "stimulant", 1, "chemicals", 1, "sugar", 1, "herbs", 1, "water", 1);
            R(C, "canned_food", 3, "cooked_meat", 2, "metal_plate", 1, "chemicals", 1);

            // Electronics Bench: needs expedition parts; ends at the radio beacon.
            RV("copper_wire_salvage", E, "copper_wire", 2, "electronics", 1);
            R(E, "circuit_board", 1, "electronics", 2, "copper_wire", 2, "chemicals", 1);
            R(E, "radio_part", 1, "circuit_board", 2, "copper_wire", 3, "expedition_parts", 1, "metal_plate", 1);
            R(E, "radio_beacon", 1, "radio_part", 3, "steel_bar", 4, "copper_wire", 6, "fuel", 3);

            // Homestead materials and travel food.
            R(H, "hammer", 1, "wood", 2, "stone", 2, "rope", 1);
            R(W, "repair_kit_wood", 2, "planks", 2, "nails", 2);
            R(W, "repair_kit_metal", 2, "metal_plate", 1, "nails", 2);
            R(F, "hinges", 2, "iron_ingot", 1);
            R(C, "fertilizer", 2, "plant_fiber", 3, "vegetables", 1, "chemicals", 1);
            R(S, "dried_meat", 2, "raw_meat", 1, "wood", 1);
            R(S, "trail_mix", 2, "food_berries", 2, "grain", 1, "sugar", 1);
            R(S, "vegetable_broth", 2, "vegetables", 1, "water", 1, "wood", 1);

            // Materials for power, automation and energy weapons.
            R(C, "gunpowder", 4, "chemicals", 1, "sulfur", 1, "charcoal", 2);
            R(C, "coolant", 2, "chemicals", 2, "water", 1);
            R(E, "capacitor", 2, "electronics", 1, "copper_wire", 2, "chemicals", 1);
            R(E, "laser_diode", 1, "electronics", 2, "glass", 2, "copper_wire", 1);
            R(E, "electric_motor", 1, "copper_wire", 4, "iron_ingot", 2, "scrap_metal", 1);
            R(E, "solar_cell", 2, "glass", 2, "electronics", 1, "copper_wire", 1);
            R(E, "energy_cell", 1, "capacitor", 1, "chemicals", 1, "scrap_metal", 1);

            // Armory: rounds and firearms.
            R(A, "ammo_9mm", 12, "scrap_metal", 2, "gunpowder", 1);
            R(A, "ammo_rifle", 8, "scrap_metal", 2, "gunpowder", 2);
            R(A, "ammo_shell", 6, "scrap_metal", 1, "gunpowder", 1, "cloth", 1);
            R(A, "pistol", 1, "steel_bar", 1, "metal_plate", 2, "nails", 4);
            R(A, "smg", 1, "steel_bar", 3, "metal_plate", 3, "duct_tape", 2);
            R(A, "shotgun", 1, "steel_bar", 2, "planks", 3, "metal_plate", 2);
            R(A, "hunting_rifle", 1, "steel_bar", 3, "planks", 3, "rope", 1);
            R(A, "assault_rifle", 1, "steel_bar", 5, "metal_plate", 4, "circuit_board", 1, "duct_tape", 2);

            // Energy weapons: the end of the electronics ladder.
            R(E, "laser_pistol", 1, "laser_diode", 1, "capacitor", 2, "metal_plate", 2, "circuit_board", 1);
            R(E, "laser_rifle", 1, "laser_diode", 2, "capacitor", 3, "steel_bar", 2, "circuit_board", 2, "coolant", 2);
            R(E, "plasma_rifle", 1, "laser_diode", 2, "capacitor", 4, "steel_bar", 3, "circuit_board", 3, "coolant", 4, "expedition_parts", 2);
        }

        // ---------- Crafting ----------

        /// <summary>Atomic: spends times x inputs in one CraftingSystem.TrySpend, then adds times x output.</summary>
        public static bool TryCraft(Inventory inventory, Recipe recipe, int times, out string message)
        {
            message = null;
            if (inventory == null || recipe == null || times < 1) { message = "Cannot craft."; return false; }
            if (!recipe.Resolves()) { message = "Recipe has a missing item definition."; return false; }
            var name = ItemCatalog.NameOf(recipe.output);
            if (!CraftingSystem.TrySpend(inventory, recipe.ToCost(times))) { message = $"Not enough materials for {name}."; return false; }
            inventory.Add(recipe.output, recipe.outputAmount * times);
            message = $"Crafted {recipe.outputAmount * times} {name}.";
            return true;
        }

        public static bool CanCraft(Inventory inventory, Recipe recipe, int times)
        {
            // The same checks TryCraft makes, so a button can't promise what the craft would refuse.
            if (inventory == null || recipe == null || times < 1 || !recipe.Resolves()) return false;
            return CraftingSystem.TryAfford(inventory, recipe.ToCost(times));
        }

        // ---------- Static validation ----------

        /// <summary>
        /// Checks ids, then a fixpoint reachability pass from raw sources (gathered + every loot table):
        /// a recipe only produces once its station and all inputs are reachable, so no item can come from a cycle.
        /// Returns the list of problems (empty = ok); summary describes depth of the ladder.
        /// </summary>
        public static List<string> Validate(out string summary)
        {
            var errors = new List<string>();
            var raw = new HashSet<string>(ItemCatalog.GatheredSources);
            foreach (var id in LootTables.AllItemIds()) raw.Add(id);
            foreach (var id in raw) if (!ItemCatalog.Exists(id)) errors.Add("raw source with unknown item: " + id);
            foreach (var t in LootTables.TableNames) if (!LootTables.Has(t)) errors.Add("missing loot table " + t);
            foreach (var shopEntry in ItemCatalog.Shop)
                if (!ItemCatalog.Exists(shopEntry.id) || shopEntry.price < 1 || shopEntry.quantity < 1) errors.Add("bad shop entry " + shopEntry.id);

            // Loot must not skip the ladder: tools, weapons, armor, endgame items and forge/bench materials are crafted only.
            var gated = new HashSet<string> { "steel_bar", "metal_plate", "iron_ingot", "reinforced_frame", "circuit_board" };
            foreach (var id in LootTables.AllItemIds())
            {
                var sp = ItemCatalog.Spec(id);
                if (sp != null && (gated.Contains(id) || sp.category == ItemCategory.Tool || sp.category == ItemCategory.Weapon ||
                    sp.category == ItemCategory.Armor || sp.category == ItemCategory.Endgame))
                    errors.Add("loot table hands out a crafted-only item: " + id);
            }

            foreach (var r in all)
            {
                if (!ItemCatalog.Exists(r.output)) errors.Add($"recipe {r.id}: unknown output {r.output}");
                if (r.inputs.Length == 0) errors.Add($"recipe {r.id}: no inputs (item from nothing)");
                if (r.outputAmount < 1) errors.Add($"recipe {r.id}: output amount < 1");
                foreach (var i in r.inputs)
                {
                    if (!ItemCatalog.Exists(i.id)) errors.Add($"recipe {r.id}: unknown input {i.id}");
                    if (i.amount < 1) errors.Add($"recipe {r.id}: input amount < 1 for {i.id}");
                    if (i.id == r.output) errors.Add($"recipe {r.id}: consumes its own output");
                }
            }
            foreach (var kv in PieceCosts)
                foreach (var i in kv.Value)
                    if (!ItemCatalog.Exists(i.id)) errors.Add($"piece {kv.Key}: unknown cost item {i.id}");

            var depth = new Dictionary<string, int>();
            foreach (var id in raw) depth[id] = 0;
            var stationOpen = new Dictionary<StationKind, int> { { StationKind.Hand, 0 } };
            // The bunker starts furnished with a Workbench, but its build cost must still be reachable.
            var pieceReach = new Dictionary<string, int>();
            bool changed = true;
            int guard = 0;
            while (changed && guard++ < 200)
            {
                changed = false;
                foreach (var kv in PieceCosts)
                {
                    if (pieceReach.ContainsKey(kv.Key)) continue;
                    int d = 0; bool ok = true;
                    foreach (var i in kv.Value) { if (depth.TryGetValue(i.id, out int di)) d = Math.Max(d, di); else { ok = false; break; } }
                    if (ok) { pieceReach[kv.Key] = d + 1; changed = true; }
                }
                foreach (StationKind kind in Enum.GetValues(typeof(StationKind)))
                {
                    string pid = PieceIdFor(kind);
                    if (pid != null && !stationOpen.ContainsKey(kind) && pieceReach.TryGetValue(pid, out int pd)) { stationOpen[kind] = pd; changed = true; }
                }
                foreach (var r in all)
                {
                    if (!stationOpen.TryGetValue(r.station, out int sd)) continue;
                    int d = sd; bool ok = true;
                    foreach (var i in r.inputs) { if (depth.TryGetValue(i.id, out int di)) d = Math.Max(d, di); else { ok = false; break; } }
                    if (!ok) continue;
                    if (!depth.ContainsKey(r.output)) { depth[r.output] = d + 1; changed = true; }
                    else if (d + 1 < depth[r.output]) { depth[r.output] = d + 1; changed = true; }
                }
            }
            foreach (StationKind kind in Enum.GetValues(typeof(StationKind)))
                if (!stationOpen.ContainsKey(kind)) errors.Add("station never becomes buildable: " + kind);
            foreach (var kv in PieceCosts) if (!pieceReach.ContainsKey(kv.Key)) errors.Add("piece never affordable from raw sources: " + kv.Key);
            foreach (var s in ItemCatalog.All) if (!depth.ContainsKey(s.id)) errors.Add("item unreachable from raw sources: " + s.id);
            foreach (var r in all) if (!stationOpen.ContainsKey(r.station)) errors.Add("recipe on unreachable station: " + r.id);

            var sb = new StringBuilder();
            sb.Append($"{ItemCatalog.All.Count} items, {all.Count} recipes, {PieceCosts.Count} piece costs; ladder depth:");
            foreach (var id in new[] { "planks", "reinforced_frame", "iron_ingot", "steel_bar", "circuit_board", "radio_part", "radio_beacon" })
                if (depth.TryGetValue(id, out int dd)) sb.Append($" {id}={dd}");
            if (pieceReach.TryGetValue("piece_radio", out int rd)) sb.Append($" piece_radio={rd}");
            summary = sb.ToString();
            return errors;
        }
    }
}
