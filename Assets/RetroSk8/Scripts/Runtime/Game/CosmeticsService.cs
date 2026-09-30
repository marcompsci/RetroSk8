using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Save;

namespace RetroSk8.Game
{
    /// <summary>The equipped item per slot, resolved to definitions.</summary>
    public sealed class CosmeticLoadout
    {
        private readonly Dictionary<CosmeticSlot, CosmeticDefinition> _items = new Dictionary<CosmeticSlot, CosmeticDefinition>();

        public CosmeticDefinition this[CosmeticSlot slot]
        {
            get => _items.TryGetValue(slot, out var c) ? c : null;
            set => _items[slot] = value;
        }

        public CosmeticLoadout Clone()
        {
            var copy = new CosmeticLoadout();
            foreach (var kv in _items) copy._items[kv.Key] = kv.Value;
            return copy;
        }
    }

    /// <summary>Ownership, purchasing and equipping of cosmetics, persisted through SaveManager. Tokens are earned only by skating.</summary>
    public static class CosmeticsService
    {
        public static bool IsOwned(CosmeticDefinition c) => c != null && (c.IsFree || SaveManager.Data.ownedCosmetics.Contains(c.id));

        public static PurchaseResult Buy(CosmeticDefinition c)
        {
            var result = ShopRules.Check(SaveManager.Data.tapeTokens, c.price, IsOwned(c));
            if (result != PurchaseResult.Ok) return result;
            SaveManager.Data.tapeTokens -= c.price;
            SaveManager.Data.ownedCosmetics.Add(c.id);
            SaveManager.Save();
            return result;
        }

        public static bool Equip(CosmeticDefinition c)
        {
            if (!IsOwned(c)) return false;
            var list = SaveManager.Data.equipped;
            list.RemoveAll(e => e.slot == (int)c.slot);
            list.Add(new EquipEntry { slot = (int)c.slot, id = c.id });
            SaveManager.Save();
            return true;
        }

        public static bool IsEquipped(CosmeticDefinition c)
        {
            foreach (var e in SaveManager.Data.equipped)
                if (e.slot == (int)c.slot) return e.id == c.id;
            return false;
        }

        /// <summary>Equipped items, falling back to the first free item of each slot.</summary>
        public static CosmeticLoadout CurrentLoadout(ContentRegistry content)
        {
            var loadout = new CosmeticLoadout();
            if (content == null) return loadout;
            foreach (var c in content.cosmetics)
            {
                if (c == null) continue;
                if (loadout[c.slot] == null && c.IsFree) loadout[c.slot] = c;
            }
            foreach (var e in SaveManager.Data.equipped)
            {
                var c = content.cosmetics.Find(x => x != null && x.id == e.id);
                if (c != null && IsOwned(c)) loadout[c.slot] = c;
            }
            return loadout;
        }

        public static IEnumerable<CosmeticDefinition> InSlot(ContentRegistry content, CosmeticSlot slot)
        {
            foreach (var c in content.cosmetics)
                if (c != null && c.slot == slot) yield return c;
        }
    }
}
