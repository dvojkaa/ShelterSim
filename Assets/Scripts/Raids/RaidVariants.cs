using UnityEngine;

namespace SheltreSim.Raids
{
    public enum RaidVariant { Raider, Gunner, Brute, Scavenger, Warlord }

    /// <summary>Which death-loot table an enemy rolls on.</summary>
    public enum LootTier { Ordinary, Elite, Warlord }

    /// <summary>
    /// The one table of what each raid enemy is. ModelId names the FBX planned for it in Assets/Art/Models. None exist yet,
    /// so every variant is today's raider prefab with a tint and a scale.
    /// BodyRadius and BodyHeight are the capsule in world metres and don't follow the scale: the bunker door opening is
    /// 2.3 m high and the capsule adds a 0.08 skin at each end, so 2.0 is the limit (measured by RaidProbe). A larger model must not grow it.
    /// </summary>
    public sealed class RaidVariantSpec
    {
        public RaidVariant Variant;
        public string DisplayName;
        public string ModelId;
        public LootTier Loot;
        public float Health, Speed, Damage, StructureDamage, AttackCooldown, AttackWindup, AttackRange;
        public float Scale, BodyRadius, BodyHeight;
        public Color Tint;
        public bool SuperArmor;
        public bool Ranged;
        public float RangedDamage, RangedRange, RangedInterval;

        private static readonly RaidVariantSpec[] Table =
        {
            new RaidVariantSpec { Variant = RaidVariant.Raider, DisplayName = "Raider", ModelId = "enemy_wanderer", Loot = LootTier.Ordinary,
                Health = 30f, Speed = 2.5f, Damage = 5f, StructureDamage = 8f, AttackCooldown = 1.5f, AttackWindup = 0.35f, AttackRange = 1.5f,
                Scale = 1f, BodyRadius = 0.5f, BodyHeight = 2f, Tint = Color.white },
            new RaidVariantSpec { Variant = RaidVariant.Gunner, DisplayName = "Gunner", ModelId = "enemy_plated", Loot = LootTier.Elite,
                Health = 35f, Speed = 2.3f, Damage = 4f, StructureDamage = 6f, AttackCooldown = 1.5f, AttackWindup = 0.35f, AttackRange = 1.5f,
                Scale = 1f, BodyRadius = 0.5f, BodyHeight = 2f, Tint = new Color(0.62f, 0.72f, 0.98f),
                Ranged = true, RangedDamage = 8f, RangedRange = 15f, RangedInterval = 1.2f },
            // The breacher: slow, and hard on anything you built. One of the bestiary's readable roles.
            new RaidVariantSpec { Variant = RaidVariant.Brute, DisplayName = "Brute", ModelId = "enemy_breacher", Loot = LootTier.Elite,
                Health = 90f, Speed = 1.8f, Damage = 10f, StructureDamage = 16f, AttackCooldown = 1.8f, AttackWindup = 0.55f, AttackRange = 1.7f,
                Scale = 1.25f, BodyRadius = 0.55f, BodyHeight = 2f, Tint = new Color(0.95f, 0.6f, 0.45f) },
            new RaidVariantSpec { Variant = RaidVariant.Scavenger, DisplayName = "Scavenger", ModelId = "enemy_scavenger", Loot = LootTier.Ordinary,
                Health = 18f, Speed = 3.6f, Damage = 3f, StructureDamage = 4f, AttackCooldown = 1.2f, AttackWindup = 0.3f, AttackRange = 1.5f,
                Scale = 0.92f, BodyRadius = 0.42f, BodyHeight = 1.84f, Tint = new Color(0.72f, 0.95f, 0.55f) },
            // 8x health needs a long, loud wind-up to stay fair, and hits don't interrupt it or it could be stun-locked.
            new RaidVariantSpec { Variant = RaidVariant.Warlord, DisplayName = "Warlord", ModelId = "boss_shift_commander", Loot = LootTier.Warlord,
                Health = 240f, Speed = 2.2f, Damage = 18f, StructureDamage = 24f, AttackCooldown = 2.4f, AttackWindup = 0.9f, AttackRange = 1.8f,
                Scale = 1.35f, BodyRadius = 0.6f, BodyHeight = 2f, Tint = new Color(1f, 0.82f, 0.3f), SuperArmor = true },
        };

        public static RaidVariantSpec Of(RaidVariant variant)
        {
            foreach (RaidVariantSpec spec in Table)
                if (spec.Variant == variant) return spec;
            return Table[0];
        }
    }
}
