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
        /// <summary>
        /// Pack items come only from a verified pack and story rewards only from the story, never from the
        /// ownedCosmetics list (Phase 24: a hand-made save code could list them there).
        /// </summary>
        public static bool IsOwned(CosmeticDefinition c)
        {
            if (c == null) return false;
            if (c.IsPackItem) return SaveManager.Data.ownedPacks.Contains(c.packId);
            if (c.IsStoryReward) return SaveManager.Data.story != null && SaveManager.Data.story.IsCleared(c.rewardStep);
            return c.IsFree || SaveManager.Data.ownedCosmetics.Contains(c.id);
        }

        /// <summary>Today's FEATURED shelf in the shop (item ids; the first is the deal of the day).</summary>
        public static List<string> Featured(ContentRegistry content)
        {
            var items = new List<ShopItem>();
            if (content != null)
                foreach (var c in content.cosmetics)
                    if (c != null) items.Add(new ShopItem(c.id, c.price, c.IsPackItem || c.IsStoryReward)); // story rewards are never on sale
            return Shop.Featured(GameSession.TodayKey, items);
        }

        /// <summary>What an item costs today in Tape Tokens (the deal of the day is 25% off).</summary>
        public static int PriceToday(CosmeticDefinition c, List<string> featured) => c == null ? 0 : Shop.PriceToday(c.id, c.price, featured);

        public static PurchaseResult Buy(CosmeticDefinition c) => Buy(c, c != null ? c.price : 0);

        public static PurchaseResult Buy(CosmeticDefinition c, int price)
        {
            if (c == null) return PurchaseResult.InvalidPrice;
            if (c.IsPackItem) return IsOwned(c) ? PurchaseResult.AlreadyOwned : PurchaseResult.PackOnly;
            if (c.IsStoryReward) return IsOwned(c) ? PurchaseResult.AlreadyOwned : PurchaseResult.StoryReward;
            var result = ShopRules.Check(SaveManager.Data.tapeTokens, price, IsOwned(c));
            if (result != PurchaseResult.Ok) return result;
            SaveManager.Data.tapeTokens -= price;
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
