using System;
using System.Collections.Generic;
using SheltreSim.Crafting;
using SheltreSim.Items;

namespace SheltreSim.Storage
{
    /// <summary>
    /// The only code that moves items between a pack and a crate. Each move works out one amount, takes it from
    /// the source in a single checked call, and only then gives exactly that amount to the destination, so nothing
    /// is duplicated or lost whatever order the buttons are pressed in. int.MaxValue means "as many as possible".
    /// </summary>
    public static class StorageTransfer
    {
        public const int All = int.MaxValue;

        private static HashSet<string> ammoIds;

        /// <summary>True for anything a ranged weapon loads, including energy cells.</summary>
        public static bool IsAmmo(string id)
        {
            if (ammoIds == null)
            {
                ammoIds = new HashSet<string>();
                foreach (ItemSpec spec in ItemCatalog.All)
                    if (spec.ranged && !string.IsNullOrEmpty(spec.ammoId)) ammoIds.Add(spec.ammoId);
            }
            return id != null && ammoIds.Contains(id);
        }

        /// <summary>Raw and refined materials except ammo. Consumables, tools and gear stay in the pack.</summary>
        public static bool IsBuildingMaterial(string id)
        {
            ItemSpec spec = ItemCatalog.Spec(id);
            if (spec == null || spec.IsConsumable || IsAmmo(id)) return false;
            return spec.category == ItemCategory.Raw || spec.category == ItemCategory.Refined;
        }

        public static bool IsEquipped(Inventory pack, string id)
        {
            PlayerGear gear = pack != null ? pack.GetComponent<PlayerGear>() : null;
            if (gear == null || string.IsNullOrEmpty(id)) return false;
            ItemSpec spec = ItemCatalog.Spec(id);
            return gear.IsEquipped(id) || (spec != null && gear.Equipped(spec.slot) == id);
        }

        /// <summary>What can leave the pack. The worn or wielded copy of equipped gear stays: unequip it to store it.</summary>
        public static int MovableFromPack(Inventory pack, string id)
        {
            if (pack == null) return 0;
            int count = pack.GetCount(id);
            return IsEquipped(pack, id) ? Math.Max(0, count - 1) : count;
        }

        /// <summary>Pack to crate. Returns the amount that moved.</summary>
        public static int Store(Inventory pack, IItemStore crate, string id, int amount)
        {
            if (pack == null || crate == null || string.IsNullOrWhiteSpace(id) || amount <= 0) return 0;
            int moved = Math.Min(Math.Min(Math.Min(amount, MovableFromPack(pack, id)), int.MaxValue - crate.GetCount(id)), crate.GetAddRoom(id));
            if (moved <= 0 || !pack.TryConsume(id, moved)) return 0;
            crate.Add(id, moved);
            return moved;
        }

        /// <summary>Crate to pack. Returns the amount that moved.</summary>
        public static int Take(Inventory pack, IItemStore crate, string id, int amount)
        {
            if (pack == null || crate == null || string.IsNullOrWhiteSpace(id) || amount <= 0) return 0;
            int moved = Math.Min(Math.Min(amount, crate.GetCount(id)), int.MaxValue - pack.GetCount(id));
            if (moved <= 0 || !crate.TryTake(id, moved)) return 0;
            pack.Add(id, moved);
            return moved;
        }

        public static long StoreBuildingMaterials(Inventory pack, IItemStore crate)
        {
            if (pack == null || crate == null) return 0;
            long total = 0;
            foreach (string id in new List<string>(pack.GetAllCounts().Keys))
                if (IsBuildingMaterial(id)) total += Store(pack, crate, id, All);
            return total;
        }

        /// <summary>What quick stack may move: anything but food, drink, medicine and ammo, which you want on you. Equipped copies stay (Store).</summary>
        public static bool IsQuickStackable(string id)
        {
            ItemSpec spec = ItemCatalog.Spec(id);
            return spec != null && !spec.IsConsumable && !IsAmmo(id);
        }

        /// <summary>
        /// Moves each quick-stackable pack item into the first of the crates that already holds some of it (so put the open
        /// crate first). Every move is one Store call, so nothing is lost or duplicated. Returns items moved and kinds moved.
        /// </summary>
        public static (long items, int kinds) QuickStack(Inventory pack, IReadOnlyList<IItemStore> crates)
        {
            if (pack == null || crates == null || crates.Count == 0) return (0, 0);
            long total = 0;
            int kinds = 0;
            foreach (string id in new List<string>(pack.GetAllCounts().Keys))
            {
                if (!IsQuickStackable(id) || MovableFromPack(pack, id) <= 0) continue;
                foreach (IItemStore crate in crates)
                {
                    if (crate == null || crate.GetCount(id) <= 0) continue;
                    int moved = Store(pack, crate, id, All);
                    if (moved > 0) { total += moved; kinds++; }
                    break;
                }
            }
            return (total, kinds);
        }

        public static long TakeAll(Inventory pack, IItemStore crate)
        {
            if (pack == null || crate == null) return 0;
            long total = 0;
            foreach (string id in new List<string>(crate.Contents.Keys)) total += Take(pack, crate, id, All);
            return total;
        }
    }
}
