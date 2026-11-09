using System.Collections.Generic;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>Every enemy kind in the world. Order is saved nowhere: ids are by name.</summary>
    public enum SpeciesId
    {
        Wanderer, Runner, Scavenger, Screamer, Plated, Breacher,
        Rootwalker, Mirelurker, IronBoar,
        Gatekeeper, RootColossus, ShiftCommander,
    }

    public enum BodyKind { Humanoid, Quadruped }

    /// <summary>
    /// The one table of what each enemy is, after GPT's bestiary (DEV_COORDINATION.md, "bestiář"). ModelId is the FBX in
    /// Assets/Art/Models, turned into Resources/Enemies/&lt;id&gt; by M22SystemsSetup. Health is tuned against melee 20-72 and
    /// guns 13-85 per hit. Capsules are world metres, independent of the model's size: the bunker door is 2.3 m high, so
    /// nothing that may enter it is taller than 2.0 (the bosses never come to the bunker).
    /// Loot: Guaranteed always drops, Bonus rows each roll once from the seed. Chance only decides the bonus.
    /// </summary>
    public sealed class SpeciesSpec
    {
        public SpeciesId Id;
        public string Name, ModelId;
        public BodyKind Body;
        public bool IsBoss;
        public float Health, Speed, Damage, AttackWindup, AttackCooldown, AttackRange, Detection;
        public float Radius, Height;
        public (string id, int min, int max)[] Guaranteed;
        public (string id, double chance, int min, int max)[] Bonus;

        private static readonly List<SpeciesSpec> table = new List<SpeciesSpec>
        {
            S(SpeciesId.Wanderer, "Wanderer", "enemy_wanderer", 40, 2.0f, 7, .55f, 1.6f, 1.5f, 11, .45f, 1.9f,
                G(("cloth", 1, 3), ("scrap_metal", 1, 2)), B(("canned_food", .2, 1, 1), ("bandage", .15, 1, 1))),
            S(SpeciesId.Runner, "Runner", "enemy_runner", 26, 4.6f, 6, .35f, 1.1f, 1.4f, 15, .42f, 1.9f,
                G(("plant_fiber", 2, 4)), B(("duct_tape", .35, 1, 1), ("bottled_water", .2, 1, 1))),
            S(SpeciesId.Scavenger, "Scavenger", "enemy_scavenger", 34, 2.8f, 6, .45f, 1.3f, 1.5f, 12, .45f, 1.9f,
                G(("scrap_metal", 2, 3), ("nails", 3, 6)), B(("bandage", .3, 1, 2), ("ammo_9mm", .3, 4, 10))),
            S(SpeciesId.Screamer, "Screamer", "enemy_screamer", 32, 2.4f, 5, .5f, 1.5f, 1.5f, 16, .45f, 1.9f,
                G(("cloth", 1, 2), ("chemicals", 1, 1)), B(("antiseptic", .3, 1, 1), ("herbs", .3, 1, 2))),
            S(SpeciesId.Plated, "Plated", "enemy_plated", 90, 1.7f, 12, .8f, 2.0f, 1.7f, 10, .5f, 1.95f,
                G(("metal_plate", 1, 2), ("scrap_metal", 1, 3)), B(("iron_ingot", .35, 1, 1), ("steel_bar", .1, 1, 1))),
            S(SpeciesId.Breacher, "Breacher", "enemy_breacher", 80, 2.0f, 11, .7f, 1.8f, 1.7f, 11, .5f, 1.95f,
                G(("nails", 4, 8), ("metal_plate", 1, 1)), B(("repair_kit_metal", .3, 1, 1), ("gunpowder", .25, 1, 2))),
            S(SpeciesId.Rootwalker, "Rootwalker", "enemy_rootwalker", 70, 1.6f, 14, 1.0f, 2.4f, 2.4f, 10, .5f, 2.0f,
                G(("plant_fiber", 2, 4), ("seeds", 1, 3)), B(("herbs", .4, 1, 2), ("wood", .4, 2, 4))),
            S(SpeciesId.Mirelurker, "Mirelurker", "enemy_mirelurker", 55, 2.6f, 10, .7f, 1.7f, 1.6f, 9, .6f, 1.1f,
                G(("plant_fiber", 1, 3), ("raw_meat", 1, 1)), B(("chemicals", .25, 1, 1), ("herbs", .3, 1, 2))),
            S(SpeciesId.IronBoar, "Iron Boar", "enemy_iron_boar", 85, 3.2f, 15, .9f, 2.2f, 1.8f, 13, .55f, 1.3f,
                G(("leather", 1, 2), ("raw_meat", 1, 2)), B(("scrap_metal", .4, 1, 3), ("metal_plate", .15, 1, 1))),
            // Bosses: long readable wind-ups, a recovery window after every strike. They guard a place and never leave it.
            S(SpeciesId.Gatekeeper, "Gatekeeper", "boss_gatekeeper", 380, 1.9f, 26, 1.3f, 2.8f, 2.4f, 18, .7f, 2.3f,
                G(("metal_plate", 6, 6), ("copper_wire", 6, 6), ("nails", 20, 20), ("expedition_parts", 2, 2)), B(("steel_bar", .5, 1, 2))),
            S(SpeciesId.RootColossus, "Root Colossus", "boss_root_colossus", 520, 1.4f, 30, 1.4f, 3.0f, 3.4f, 18, 1.2f, 4.0f,
                G(("seeds", 12, 12), ("chemicals", 4, 4), ("fertilizer", 4, 4), ("expedition_parts", 2, 2)), B(("herbs", .6, 3, 5))),
            S(SpeciesId.ShiftCommander, "Shift Commander", "boss_shift_commander", 450, 2.2f, 24, 1.1f, 2.5f, 2.2f, 18, .65f, 2.15f,
                G(("radio_part", 1, 1), ("electronics", 4, 4), ("ammo_rifle", 40, 40), ("expedition_parts", 3, 3)), B(("energy_cell", .5, 2, 4))),
        };

        public static IReadOnlyList<SpeciesSpec> All => table;
        public static SpeciesSpec Of(SpeciesId id) { foreach (var s in table) if (s.Id == id) return s; return table[0]; }
        public static bool TryParse(string name, out SpeciesId id) => System.Enum.TryParse(name, out id);

        /// <summary>Seeded, once per actor. Never call twice for the same death.</summary>
        public List<KeyValuePair<string, int>> RollLoot(int seed)
        {
            var random = new System.Random(seed);
            var items = new List<KeyValuePair<string, int>>();
            foreach (var g in Guaranteed) items.Add(new KeyValuePair<string, int>(g.id, g.min + random.Next(g.max - g.min + 1)));
            foreach (var b in Bonus)
            {
                double roll = random.NextDouble();
                int amount = b.min + random.Next(b.max - b.min + 1);
                if (roll < b.chance) items.Add(new KeyValuePair<string, int>(b.id, amount));
            }
            items.RemoveAll(p => !SheltreSim.Crafting.ItemCatalog.Exists(p.Key) || p.Value <= 0);
            return items;
        }

        private static SpeciesSpec S(SpeciesId id, string name, string model, float hp, float speed, float dmg, float windup, float cooldown,
            float range, float detection, float radius, float height, (string, int, int)[] g, (string, double, int, int)[] b) => new SpeciesSpec
        {
            Id = id, Name = name, ModelId = model, Health = hp, Speed = speed, Damage = dmg, AttackWindup = windup, AttackCooldown = cooldown,
            AttackRange = range, Detection = detection, Radius = radius, Height = height, Guaranteed = g, Bonus = b,
            Body = id == SpeciesId.Mirelurker || id == SpeciesId.IronBoar ? BodyKind.Quadruped : BodyKind.Humanoid,
            IsBoss = id == SpeciesId.Gatekeeper || id == SpeciesId.RootColossus || id == SpeciesId.ShiftCommander,
        };
        private static (string, int, int)[] G(params (string, int, int)[] rows) => rows;
        private static (string, double, int, int)[] B(params (string, double, int, int)[] rows) => rows;
    }
}
