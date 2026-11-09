using System.Collections.Generic;
using SheltreSim.Combat;
using SheltreSim.Items;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Crafting
{
    /// <summary>
    /// What the player has equipped (one melee weapon, one ranged weapon, one armor, one coat) and what the owned tools are worth.
    /// Equipping never consumes the item. Equipping a melee weapon SETS melee damage to the weapon's value (no stacking).
    /// The ranged slot never touches melee damage: RangedWeaponController reads RangedId.
    /// </summary>
    public sealed class PlayerGear : MonoBehaviour
    {
        public static PlayerGear Active { get; private set; }

        private string weaponId = "", armorId = "", coatId = "", rangedId = "";
        private Inventory inventory;
        private MeleeAttacker melee;

        public string WeaponId => weaponId;
        public string ArmorId => armorId;
        public string CoatId => coatId;
        public string RangedId => rangedId;

        /// <summary>The player chose to equip this item (not fired by a load). Which hand to raise is RangedWeaponController's call.</summary>
        public event System.Action<GearSlot, string> ItemEquipped;

        /// <summary>Adds a PlayerGear to the object if none exists.</summary>
        public static PlayerGear Ensure(GameObject player)
        {
            if (player == null) return null;
            var gear = player.GetComponent<PlayerGear>();
            return gear != null ? gear : player.AddComponent<PlayerGear>();
        }

        private void Awake()
        {
            Active = this;
            Bind();
        }

        private void OnDestroy() { if (Active == this) Active = null; }

        private void Bind()
        {
            if (inventory == null) inventory = GetComponent<Inventory>();
            if (melee == null) melee = GetComponentInChildren<MeleeAttacker>(true);
        }

        private void Update()
        {
            // Crafting can consume an equipped item (a Spiked Club eats its Wooden Club). Drop gear we no longer own.
            Bind();
            if (inventory == null) return;
            if (weaponId.Length > 0 && inventory.GetCount(weaponId) < 1) Unequip(GearSlot.Weapon, true);
            if (armorId.Length > 0 && inventory.GetCount(armorId) < 1) Unequip(GearSlot.Armor, true);
            if (coatId.Length > 0 && inventory.GetCount(coatId) < 1) Unequip(GearSlot.Coat, true);
            if (rangedId.Length > 0 && inventory.GetCount(rangedId) < 1) Unequip(GearSlot.Ranged, true);
        }

        /// <summary>Melee damage of the equipped weapon, or the unarmed base damage.</summary>
        public float AttackDamage
        {
            get
            {
                var def = weaponId.Length > 0 ? ItemCatalog.Def(weaponId) : null;
                return def != null && def.weaponDamage > 0f ? def.weaponDamage : MeleeAttacker.DefaultDamage;
            }
        }

        /// <summary>Fraction of incoming damage removed, capped at ItemCatalog.MaxDamageReduction.</summary>
        public float DamageReduction
        {
            get
            {
                float r = 0f;
                var a = armorId.Length > 0 ? ItemCatalog.Def(armorId) : null;
                var c = coatId.Length > 0 ? ItemCatalog.Def(coatId) : null;
                if (a != null) r += a.damageReduction;
                if (c != null) r += c.damageReduction;
                return Mathf.Clamp(r, 0f, ItemCatalog.MaxDamageReduction);
            }
        }

        /// <summary>Multiplier (1 = no protection) the weather system can apply to cold drain.</summary>
        public float ExposureMultiplier
        {
            get
            {
                var c = coatId.Length > 0 ? ItemCatalog.Def(coatId) : null;
                return c != null ? Mathf.Clamp01(1f - c.warmth) : 1f;
            }
        }

        public static ToolKind ToolFor(string resourceId)
        {
            switch (resourceId)
            {
                case "wood": return ToolKind.Axe;
                case "plant_fiber": return ToolKind.Axe;
                case "stone": case "iron_ore": case "coal": case "sulfur": return ToolKind.Pickaxe;
                default: return ToolKind.None;
            }
        }

        /// <summary>Extra units per harvest from the best owned matching tool (0 with bare hands).</summary>
        public int ToolBonus(string resourceId)
        {
            Bind();
            ToolKind kind = ToolFor(resourceId);
            if (kind == ToolKind.None || inventory == null) return 0;
            int bestTier = 0, bonus = 0;
            foreach (var spec in ItemCatalog.All)
            {
                if (spec.tool != kind || inventory.GetCount(spec.id) < 1 || spec.tier <= bestTier) continue;
                bestTier = spec.tier; bonus = spec.bonus;
            }
            return resourceId == "plant_fiber" ? Mathf.Min(2, bonus) : bonus;
        }

        public string BestToolName(string resourceId)
        {
            Bind();
            ToolKind kind = ToolFor(resourceId);
            if (kind == ToolKind.None || inventory == null) return null;
            int bestTier = 0; string name = null;
            foreach (var spec in ItemCatalog.All)
                if (spec.tool == kind && inventory.GetCount(spec.id) > 0 && spec.tier > bestTier) { bestTier = spec.tier; name = spec.name; }
            return name;
        }

        public string Equipped(GearSlot slot) => slot == GearSlot.Weapon ? weaponId : slot == GearSlot.Armor ? armorId : slot == GearSlot.Coat ? coatId : slot == GearSlot.Ranged ? rangedId : "";

        public bool IsEquipped(string id) => !string.IsNullOrEmpty(id) && (id == weaponId || id == armorId || id == coatId || id == rangedId);

        public bool Equip(string id, out string message)
        {
            Bind();
            var spec = ItemCatalog.Spec(id);
            if (spec == null || !spec.IsEquippable) { message = "That can't be equipped."; return false; }
            if (inventory == null || inventory.GetCount(id) < 1) { message = $"You don't have a {spec.name}."; return false; }
            SetSlot(spec.slot, id);
            SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.Equip, 0.8f);
            message = spec.slot == GearSlot.Weapon ? $"Equipped {spec.name}. Melee damage: {AttackDamage:0}."
                : spec.slot == GearSlot.Ranged ? $"Equipped {spec.name}. 2: raise it, 1: melee, R: reload."
                : $"Equipped {spec.name}. Damage reduction: {DamageReduction * 100f:0}%.";
            ItemEquipped?.Invoke(spec.slot, id);
            return true;
        }

        public void Unequip(GearSlot slot, bool lost = false)
        {
            string was = Equipped(slot);
            if (was.Length == 0) return;
            SetSlot(slot, "");
            string name = ItemCatalog.NameOf(was);
            GameMessageBus.Post(lost ? $"{name} is gone, unequipped." : $"Unequipped {name}.");
        }

        private void SetSlot(GearSlot slot, string id)
        {
            // Melee damage can carry a saved upgrade, so a gun must not re-derive it.
            if (slot == GearSlot.Ranged) { rangedId = id; return; }
            if (slot == GearSlot.Weapon) weaponId = id;
            else if (slot == GearSlot.Armor) armorId = id;
            else if (slot == GearSlot.Coat) coatId = id;
            ApplyWeapon();
        }

        private void ApplyWeapon()
        {
            Bind();
            if (melee != null) melee.attackDamage = AttackDamage;
        }

        // ---------- Save API (called by the lead's SaveManager integration) ----------

        public List<string> CaptureEquipped()
        {
            var list = new List<string>();
            if (weaponId.Length > 0) list.Add(weaponId);
            if (armorId.Length > 0) list.Add(armorId);
            if (coatId.Length > 0) list.Add(coatId);
            if (rangedId.Length > 0) list.Add(rangedId);
            return list;
        }

        /// <summary>Re-equips the given ids (slot is derived from each item). Unknown ids are ignored. Applies weapon damage to MeleeAttacker.</summary>
        public void RestoreEquipped(List<string> ids)
        {
            weaponId = armorId = coatId = rangedId = "";
            if (ids != null)
                foreach (var id in ids)
                {
                    var spec = ItemCatalog.Spec(id);
                    if (spec == null || !spec.IsEquippable) continue;
                    if (spec.slot == GearSlot.Weapon) weaponId = id;
                    else if (spec.slot == GearSlot.Armor) armorId = id;
                    else if (spec.slot == GearSlot.Coat) coatId = id;
                    else if (spec.slot == GearSlot.Ranged) rangedId = id;
                }
            ApplyWeapon();
        }

        public void ResetAll()
        {
            weaponId = armorId = coatId = rangedId = "";
            ApplyWeapon();
        }
    }
}
