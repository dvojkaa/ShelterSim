using System;
using System.Collections.Generic;
using SheltreSim.Items;
using UnityEngine;

namespace SheltreSim.Crafting
{
    /// <summary>One purchasable good in the pre-apocalypse store. price is dollars per purchase; quantity is units delivered per purchase.</summary>
    [Serializable]
    public sealed class ShopEntry
    {
        public string id;
        public int price;
        public int quantity;
        public ShopEntry(string id, int price, int quantity) { this.id = id; this.price = price; this.quantity = quantity; }
    }

    /// <summary>Code description of one item. M21CraftingSetup turns each of these into a ResourceDefinition asset.</summary>
    public sealed class ItemSpec
    {
        public string id, name, description;
        public ItemCategory category;
        public bool food, drink, medicine, weapon;
        public float hunger, thirst, heal, damage, reduction, warmth;
        public ToolKind tool; public int tier, bonus;
        public GearSlot slot;
        public string assetName;
        // Ranged weapons. Damage is per pellet. Energy weapons reload a whole magazine from one ammo item and heat up.
        public bool ranged, energy;
        public float roundsPerMinute, spreadDegrees, rangeMeters, heatPerShot, splashRadius;
        public int magazine, pellets = 1;
        public string ammoId;

        public ItemSpec Food(float hungerRestored, float thirstRestored = 0f, float healAmount = 0f)
        {
            food = true; hunger = hungerRestored; thirst = thirstRestored; heal = healAmount; return this;
        }
        public ItemSpec Drink(float thirstRestored, float hungerRestored = 0f, float healAmount = 0f)
        {
            drink = true; thirst = thirstRestored; hunger = hungerRestored; food = false; heal = healAmount; return this;
        }
        public ItemSpec Medicine(float healAmount, float hungerRestored = 0f, float thirstRestored = 0f)
        {
            medicine = true; heal = healAmount; hunger = hungerRestored; thirst = thirstRestored; return this;
        }
        public ItemSpec Weapon(float dmg) { weapon = true; damage = dmg; slot = GearSlot.Weapon; return this; }
        public ItemSpec Ranged(float dmg, float rpm, int mag, string ammo, float spread, float range, int pelletCount = 1)
        {
            ranged = true; damage = dmg; roundsPerMinute = rpm; magazine = mag; ammoId = ammo; spreadDegrees = spread; rangeMeters = range; pellets = pelletCount; slot = GearSlot.Ranged; return this;
        }
        public ItemSpec Energy(float heat, float splash = 0f) { energy = true; heatPerShot = heat; splashRadius = splash; return this; }
        public ItemSpec Tool(ToolKind kind, int toolTier, int toolBonus) { tool = kind; tier = toolTier; bonus = toolBonus; return this; }
        public ItemSpec Armor(float damageReduction) { slot = GearSlot.Armor; reduction = damageReduction; return this; }
        public ItemSpec Coat(float damageReduction, float warmthFraction) { slot = GearSlot.Coat; reduction = damageReduction; warmth = warmthFraction; return this; }
        public ItemSpec Asset(string name) { assetName = name; return this; }

        public bool IsConsumable => food || drink || medicine;
        public bool IsEquippable => slot != GearSlot.None;
    }

    /// <summary>
    /// Single source of truth for every item (id, name, description, category, effects).
    /// M21CraftingSetup writes one ResourceDefinition asset per entry into Assets/Data/Resources;
    /// at runtime Def(id) resolves that asset through Resources.
    /// </summary>
    public static class ItemCatalog
    {
        public const float MaxDamageReduction = 0.6f;

        private static readonly List<ItemSpec> all = new List<ItemSpec>();
        private static readonly Dictionary<string, ItemSpec> specs = new Dictionary<string, ItemSpec>();
        private static Dictionary<string, ResourceDefinition> defs;

        public static IReadOnlyList<ItemSpec> All => all;
        public static ItemSpec Spec(string id) => id != null && specs.TryGetValue(id, out var s) ? s : null;
        public static bool Exists(string id) => id != null && specs.ContainsKey(id);

        private static ItemSpec I(string id, string name, ItemCategory cat, string desc)
        {
            var s = new ItemSpec { id = id, name = name, category = cat, description = desc };
            all.Add(s); specs[id] = s; return s;
        }

        static ItemCatalog()
        {
            const ItemCategory Raw = ItemCategory.Raw, Ref = ItemCategory.Refined, Food = ItemCategory.Food,
                Med = ItemCategory.Medicine, Tool = ItemCategory.Tool, Wpn = ItemCategory.Weapon, Arm = ItemCategory.Armor, End = ItemCategory.Endgame;

            // ---- Raw: found, scavenged or gathered ----
            I("wood", "Wood", Raw, "Logs and branches. Planks, fuel, tool handles.").Asset("Wood");
            I("stone", "Stone", Raw, "Rough stone for tools, stoves and forges.").Asset("Stone");
            I("iron_ore", "Iron Ore", Raw, "Smelt it in a forge for iron.").Asset("IronOre");
            I("scrap_metal", "Scrap Metal", Raw, "Salvaged metal. Nails, plates, weapons.").Asset("ScrapMetal");
            I("expedition_parts", "Expedition Parts", Raw, "Rare parts from distant landmarks. Upgrades and electronics.").Asset("ExpeditionParts");
            I("plant_fiber", "Plant Fiber", Raw, "Stringy plant stems. Twist into rope.");
            I("cloth", "Cloth", Raw, "Rags and fabric from houses. Bandages, jackets, beds.");
            I("leather", "Leather", Raw, "Tanned hide from animals. Armor and straps.");
            I("raw_meat", "Raw Meat", Raw, "Hunted meat. Cook it before eating.");
            I("coal", "Coal", Raw, "Burns hot. Forge fuel.");
            I("sulfur", "Sulfur", Raw, "Yellow mineral used in medicine.");
            I("chemicals", "Chemicals", Raw, "Lab and cleaning chemicals.");
            I("electronics", "Electronics", Raw, "Salvaged circuit parts.");
            I("copper_wire", "Copper Wire", Raw, "Spools of wire.");
            I("fuel", "Fuel", Raw, "A can of petrol or diesel.");
            I("glass", "Glass", Raw, "Jars and panes.");
            I("grain", "Grain", Raw, "Wheat and oats. Bread and gruel.");
            I("seeds", "Seeds", Raw, "Plant them in a Farm Plot.");
            I("duct_tape", "Duct Tape", Raw, "Fixes nearly everything.");
            I("dirty_water", "Dirty Water", Raw, "Undrinkable. Boil it or run it through a Water Filter.");
            I("herbs", "Herbs", Raw, "Wild medicinal and cooking herbs.");
            I("sugar", "Sugar", Raw, "Sweetener for jam and stimulants.");
            I("vegetables", "Vegetables", Food, "Fresh vegetables from a Farm Plot.").Food(18f);

            // ---- Refined: crafted materials ----
            I("planks", "Planks", Ref, "Cut boards. The base of every build.");
            I("rope", "Rope", Ref, "Twisted fiber rope.");
            I("nails", "Nails", Ref, "Fasteners hammered from scrap.");
            I("charcoal", "Charcoal", Ref, "Burned wood. Clean forge fuel and water filtering.");
            I("iron_ingot", "Iron Ingot", Ref, "Smelted iron.");
            I("steel_bar", "Steel Bar", Ref, "Hard steel stock.");
            I("metal_plate", "Metal Plate", Ref, "Flat plate for armor and stations.");
            I("reinforced_frame", "Reinforced Frame", Ref, "A braced frame. Every real station is built on one.");
            I("circuit_board", "Circuit Board", Ref, "Hand-assembled board.");
            I("hinges", "Hinges", Ref, "For gates and heavy doors.");
            I("repair_kit_wood", "Wood Repair Kit", Ref, "With a hammer, repairs wooden builds. Press V in build mode.");
            I("repair_kit_metal", "Metal Repair Kit", Ref, "With a hammer, repairs metal builds and machines. Press V in build mode.");
            I("fertilizer", "Fertilizer", Ref, "Use on a Farm Plot: the crop grows twice as fast.");
            I("gunpowder", "Gunpowder", Ref, "Mixed at a Chem Lab. Every round needs it.");
            I("capacitor", "Capacitor", Ref, "Stores a sharp burst of power.");
            I("laser_diode", "Laser Diode", Ref, "The heart of an energy weapon.");
            I("coolant", "Coolant", Ref, "Keeps machines and energy weapons from cooking themselves.");
            I("electric_motor", "Electric Motor", Ref, "Fans, pumps and turret mounts.");
            I("solar_cell", "Solar Cell", Ref, "Turns daylight into power.");

            // ---- Ammunition: stacks in your pack, loaded when you reload ----
            I("ammo_9mm", "9mm Rounds", Ref, "For the pistol and the SMG.");
            I("ammo_rifle", "Rifle Rounds", Ref, "For rifles and machine gun turrets.");
            I("ammo_shell", "Shotgun Shells", Ref, "Short range, hits hard.");
            I("energy_cell", "Energy Cell", Ref, "One cell refills an energy weapon. Make them at a bench or charge them at a Cell Charger.");

            // ---- Food and drink ----
            I("food_berries", "Blueberries", Food, "Sweet wild berries.").Food(30f).Asset("Berries");
            I("water", "Water", Food, "Clean drinking water.").Drink(35f).Asset("Water");
            I("bottled_water", "Bottled Water", Food, "Sealed bottle of water.").Drink(45f);
            I("boiled_water", "Boiled Water", Food, "Safe to drink after boiling.").Drink(35f);
            I("herbal_tea", "Herbal Tea", Food, "Warm and calming.").Drink(30f, 0f, 8f);
            I("cooked_meat", "Cooked Meat", Food, "Hot grilled meat.").Food(45f, 0f, 5f);
            I("meat_stew", "Meat Stew", Food, "Thick stew. Filling and soothing.").Food(65f, 12f, 10f);
            I("bread", "Bread", Food, "A fresh loaf.").Food(38f);
            I("berry_jam", "Berry Jam", Food, "Sweet preserve in a jar.").Food(28f, 0f, 5f);
            I("canned_food", "Canned Food", Food, "Long-life canned rations.").Food(50f);

            // ---- Medicine ----
            I("bandage", "Bandage", Med, "Clean cloth wrap.").Medicine(18f);
            I("antiseptic", "Antiseptic", Med, "Disinfectant solution.").Medicine(15f);
            I("first_aid", "First Aid Kit", Med, "A full kit.").Medicine(50f).Asset("FirstAidKit");
            I("antibiotics", "Antibiotics", Med, "Strong medicine.").Medicine(75f);
            I("stimulant", "Stimulant", Med, "Quick heal and a burst of energy.").Medicine(30f, 20f);

            I("dried_meat", "Dried Meat", Food, "Keeps forever. Light and salty.").Food(32f);
            I("trail_mix", "Trail Mix", Food, "Berries and grain for the road.").Food(26f, 0f, 3f);
            I("vegetable_broth", "Vegetable Broth", Food, "Food and water in one bowl.").Food(20f, 22f, 4f);

            // ---- Tools: higher tier wins, never consumed ----
            I("hammer", "Hammer", Tool, "Needed to repair and dismantle builds. Never consumed.");
            I("stone_axe", "Stone Axe", Tool, "Wood gathering +1 per harvest.").Tool(ToolKind.Axe, 1, 1);
            I("iron_axe", "Iron Axe", Tool, "Wood gathering +2 per harvest.").Tool(ToolKind.Axe, 2, 2);
            I("steel_axe", "Steel Axe", Tool, "Wood gathering +3 per harvest.").Tool(ToolKind.Axe, 3, 3);
            I("stone_pickaxe", "Stone Pickaxe", Tool, "Stone, ore and coal +1 per harvest.").Tool(ToolKind.Pickaxe, 1, 1);
            I("iron_pickaxe", "Iron Pickaxe", Tool, "Stone, ore and coal +2 per harvest.").Tool(ToolKind.Pickaxe, 2, 2);
            I("steel_pickaxe", "Steel Pickaxe", Tool, "Stone, ore and coal +3 per harvest.").Tool(ToolKind.Pickaxe, 3, 3);

            // ---- Melee weapons: equipping sets your damage ----
            I("wooden_club", "Wooden Club", Wpn, "Crude but better than fists. Damage 26.").Weapon(26f);
            I("weapon_upgrade", "Makeshift Blade", Wpn, "Sharpened scrap. Damage 32.").Weapon(32f).Asset("WeaponUpgrade");
            I("spiked_club", "Spiked Club", Wpn, "Nails driven through a club. Damage 38.").Weapon(38f);
            I("machete", "Machete", Wpn, "Long cleaver. Damage 46.").Weapon(46f);
            I("fire_axe", "Fire Axe", Wpn, "Heavy and brutal. Damage 58.").Weapon(58f);
            I("steel_sword", "Steel Sword", Wpn, "The best blade you can make. Damage 72.").Weapon(72f);

            // ---- Ranged weapons: damage is per pellet ----
            I("pistol", "Pistol", Wpn, "Reliable sidearm.").Ranged(22f, 240f, 12, "ammo_9mm", 1.2f, 60f);
            I("smg", "Submachine Gun", Wpn, "Fast and inaccurate.").Ranged(14f, 620f, 30, "ammo_9mm", 3f, 45f);
            I("shotgun", "Shotgun", Wpn, "Devastating up close.").Ranged(13f, 70f, 6, "ammo_shell", 5.5f, 25f, 8);
            I("hunting_rifle", "Hunting Rifle", Wpn, "Slow, accurate, long range.").Ranged(68f, 50f, 5, "ammo_rifle", 0.2f, 130f);
            I("assault_rifle", "Assault Rifle", Wpn, "The best a forge can make.").Ranged(30f, 420f, 25, "ammo_rifle", 1.3f, 90f);
            I("laser_pistol", "Laser Pistol", Wpn, "Instant beam, no drop. Heats up.").Ranged(27f, 200f, 20, "energy_cell", 0.3f, 80f).Energy(7f);
            I("laser_rifle", "Laser Rifle", Wpn, "Sustained beam fire. Heats up fast.").Ranged(36f, 300f, 30, "energy_cell", 0.3f, 110f).Energy(5f);
            I("plasma_rifle", "Plasma Rifle", Wpn, "Slow bolts that burst on impact.").Ranged(85f, 85f, 12, "energy_cell", 0.6f, 70f).Energy(16f, 2.5f);

            // ---- Armor ----
            I("padded_jacket", "Padded Jacket", Arm, "Quilted cloth. Takes 8% less damage.").Armor(0.08f);
            I("leather_armor", "Leather Armor", Arm, "Takes 18% less damage.").Armor(0.18f);
            I("scrap_armor", "Scrap Armor", Arm, "Plates riveted onto leather. Takes 30% less damage.").Armor(0.30f);
            I("steel_plate_armor", "Steel Plate Armor", Arm, "Takes 45% less damage.").Armor(0.45f);
            I("warm_coat", "Warm Coat", Arm, "Worn over armor. Cuts cold exposure by 40% and damage by 5%.").Coat(0.05f, 0.4f);

            // ---- Endgame ----
            I("radio_part", "Radio Part", End, "A tuned transmitter module.");
            I("radio_beacon", "Radio Beacon Core", End, "Everything needed to call for rescue. Build the Radio Beacon.");
        }

        // ---------- Raw sources that are not loot containers ----------

        /// <summary>
        /// Items obtainable without crafting AND without a loot table: harvested from world nodes
        /// (ResourceNode primary or bonus drops), hunting carcasses, farming, the Water Collector.
        /// RecipeBook.Validate unions this with every LootTables entry to get the raw set.
        /// </summary>
        public static readonly string[] GatheredSources =
        {
            "wood", "stone", "iron_ore", "scrap_metal", "food_berries", "water",
            "plant_fiber", "herbs", "coal",       // node bonus drops (bushes, rocks)
            "raw_meat", "leather",                // hunting carcasses
            "vegetables", "seeds", "grain",       // farm plots / farmland
            "dirty_water",                        // streams and puddles
        };

        // ---------- Shop ----------

        private static readonly List<ShopEntry> shop = new List<ShopEntry>
        {
            new ShopEntry("canned_food", 24, 6), new ShopEntry("bottled_water", 18, 6), new ShopEntry("grain", 30, 5),
            new ShopEntry("seeds", 20, 5), new ShopEntry("first_aid", 45, 1), new ShopEntry("bandage", 12, 4),
            new ShopEntry("antiseptic", 20, 2), new ShopEntry("antibiotics", 120, 2), new ShopEntry("planks", 30, 10),
            new ShopEntry("nails", 15, 20), new ShopEntry("duct_tape", 10, 2), new ShopEntry("cloth", 25, 6),
            new ShopEntry("rope", 22, 3), new ShopEntry("iron_axe", 90, 1), new ShopEntry("iron_pickaxe", 110, 1),
            new ShopEntry("machete", 160, 1), new ShopEntry("padded_jacket", 75, 1), new ShopEntry("fuel", 40, 4),
            new ShopEntry("copper_wire", 35, 6), new ShopEntry("glass", 20, 4), new ShopEntry("chemicals", 60, 3),
            new ShopEntry("steel_plate_armor", 400, 1), new ShopEntry("pistol", 350, 1), new ShopEntry("ammo_9mm", 40, 24),
            new ShopEntry("shotgun", 500, 1), new ShopEntry("ammo_shell", 45, 12),
        };

        /// <summary>Everything the pre-apocalypse store sells. A prepper cannot afford all of it on roughly $5,000.</summary>
        public static IReadOnlyList<ShopEntry> Shop => shop;

        // ---------- Runtime asset lookup ----------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => defs = null;

        public static void InvalidateCache() => defs = null;

        /// <summary>The ResourceDefinition asset for an item id (loaded from Resources, cached; null if the asset is missing).</summary>
        public static ResourceDefinition Def(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (defs == null) defs = new Dictionary<string, ResourceDefinition>();
            if (defs.TryGetValue(id, out var cached) && cached != null) return cached;
            if (!Exists(id)) return null;
            foreach (var d in Resources.LoadAll<ResourceDefinition>(""))
                if (d != null && !string.IsNullOrEmpty(d.id)) defs[d.id] = d;
            return defs.TryGetValue(id, out cached) && cached != null ? cached : null;
        }

        public static string NameOf(string id)
        {
            var s = Spec(id);
            return s != null ? s.name : id;
        }

        public static string CategoryName(ItemCategory c)
        {
            switch (c)
            {
                case ItemCategory.Raw: return "Raw materials";
                case ItemCategory.Refined: return "Refined materials";
                case ItemCategory.Food: return "Food and drink";
                case ItemCategory.Medicine: return "Medicine";
                case ItemCategory.Tool: return "Tools";
                case ItemCategory.Weapon: return "Weapons";
                case ItemCategory.Armor: return "Armor";
                default: return "Special";
            }
        }
    }
}
