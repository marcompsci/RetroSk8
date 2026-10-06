using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>
    /// A cosmetic pack sold through the App Store (non-consumable: buy once, restore on any device).
    /// Packs only ever hold looks: decks, wheels, grip, clothes. No stats, no tokens, no random rewards.
    /// </summary>
    public sealed class CosmeticPack
    {
        public string Id;
        /// <summary>The App Store Connect product id (must match exactly).</summary>
        public string ProductId;
        public string Name;
        public string Blurb;
        /// <summary>Shown until the App Store returns the local price.</summary>
        public string FallbackPrice;
        public string[] ItemIds;
    }

    /// <summary>One cosmetic as the shop sees it (keeps the rules engine-free).</summary>
    public struct ShopItem
    {
        public string Id;
        public int Price;
        public bool PackOnly;
        public ShopItem(string id, int price, bool packOnly = false) { Id = id; Price = price; PackOnly = packOnly; }
    }

    /// <summary>
    /// The shop's rules: the three original cosmetic packs, and today's FEATURED shelf (four token items, one of them a
    /// deal at 25% off), rotated daily from the date so every player sees the same shelf.
    /// Engine-free so it is unit-tested.
    /// </summary>
    public static class Shop
    {
        public const int FeaturedCount = 4;
        public const float DealDiscount = 0.25f;

        public static readonly CosmeticPack[] Packs =
        {
            new CosmeticPack
            {
                Id = "night_shift", ProductId = "com.omariibell.retrosk8.pack.nightshift", Name = "NIGHT SHIFT PACK",
                Blurb = "UV violet and lamp lime for skating after dark.", FallbackPrice = "$1.99",
                ItemIds = new[] { "deck_graveyard_shift", "wheels_uv_glow", "grip_static", "shirt_night_shift", "hat_lamp", "palette_midnight_cargo" },
            },
            new CosmeticPack
            {
                Id = "desert_heat", ProductId = "com.omariibell.retrosk8.pack.desertheat", Name = "DESERT HEAT PACK",
                Blurb = "Rust, clay and sun-bleached sand.", FallbackPrice = "$1.99",
                ItemIds = new[] { "deck_mesa_bands", "wheels_sun_baked", "grip_dune", "shirt_dust", "hat_canyon", "palette_clay_chino" },
            },
            new CosmeticPack
            {
                Id = "arcade", ProductId = "com.omariibell.retrosk8.pack.arcade", Name = "ARCADE CABINET PACK",
                Blurb = "Coin-op pink, screen cyan and a pixel checker deck.", FallbackPrice = "$1.99",
                ItemIds = new[] { "deck_pixel_checker", "wheels_coin_op", "grip_scanline", "shirt_high_score", "hat_joystick", "palette_neon_denim" },
            },
        };

        public static CosmeticPack FindPack(string id)
        {
            foreach (var p in Packs) if (p.Id == id) return p;
            return null;
        }

        public static CosmeticPack FindByProduct(string productId)
        {
            foreach (var p in Packs) if (p.ProductId == productId) return p;
            return null;
        }

        /// <summary>The pack an item comes in, or null for token items.</summary>
        public static CosmeticPack PackFor(string itemId)
        {
            foreach (var p in Packs)
                foreach (var i in p.ItemIds)
                    if (i == itemId) return p;
            return null;
        }

        /// <summary>
        /// Phase 19 purchase check: makes the saved pack list match what the App Store has signed for this Apple ID
        /// (StoreKit 2 verified, unrevoked transactions). Packs in the save without a verified transaction are taken
        /// away (a refund, or a hand-edited save); verified packs missing from the save are added (restore on a new
        /// phone). Product ids that aren't ours are ignored. Returns what changed.
        /// </summary>
        public static (List<string> added, List<string> removed) Reconcile(List<string> ownedPackIds, IEnumerable<string> verifiedProductIds)
        {
            var added = new List<string>();
            var removed = new List<string>();
            if (ownedPackIds == null) return (added, removed);
            var verified = new HashSet<string>();
            if (verifiedProductIds != null)
                foreach (var pid in verifiedProductIds)
                {
                    var pack = FindByProduct((pid ?? "").Trim());
                    if (pack != null) verified.Add(pack.Id);
                }
            for (int i = ownedPackIds.Count - 1; i >= 0; i--)
                if (!verified.Contains(ownedPackIds[i])) { removed.Add(ownedPackIds[i]); ownedPackIds.RemoveAt(i); }
            foreach (var id in verified)
                if (!ownedPackIds.Contains(id)) { ownedPackIds.Add(id); added.Add(id); }
            return (added, removed);
        }

        /// <summary>The bridge's comma-separated product id list.</summary>
        public static List<string> ParseProductList(string csv)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(csv) || csv.Length > 4096) return list;
            foreach (var part in csv.Split(','))
            {
                string id = part.Trim();
                if (id.Length > 0 && id.Length <= 120) list.Add(id);
            }
            return list;
        }

        public static string[] ProductIds()
        {
            var ids = new string[Packs.Length];
            for (int i = 0; i < Packs.Length; i++) ids[i] = Packs[i].ProductId;
            return ids;
        }

        public static int DealPrice(int price) => price <= 0 ? price : Math.Max(1, (int)Math.Round(price * (1f - DealDiscount)));

        /// <summary>
        /// Today's featured token items: <see cref="FeaturedCount"/> items that cost tokens, chosen by the date (the same
        /// for everyone, and it doesn't reshuffle when you buy one). The first is the deal of the day.
        /// </summary>
        public static List<string> Featured(int dateKey, IList<ShopItem> items)
        {
            var pool = new List<string>();
            foreach (var it in items)
                if (!it.PackOnly && it.Price > 0 && !pool.Contains(it.Id)) pool.Add(it.Id);
            pool.Sort(StringComparer.Ordinal); // stable order before the date shuffle, whatever order content loads in
            var order = Radio.ShuffleOrder(pool.Count, dateKey);
            var result = new List<string>();
            for (int i = 0; i < order.Length && result.Count < FeaturedCount; i++) result.Add(pool[order[i]]);
            return result;
        }

        /// <summary>Today's price for an item (the deal is 25% off).</summary>
        public static int PriceToday(string id, int basePrice, IList<string> featured) =>
            featured != null && featured.Count > 0 && featured[0] == id ? DealPrice(basePrice) : basePrice;
    }

    public enum StoreEventKind
    {
        Purchased = 0,
        Restored = 1,
        Failed = 2,
        Cancelled = 3,
        RestoreFinished = 4,
        RestoreFailed = 5,
        Deferred = 6,
        ProductsLoaded = 7,
    }

    /// <summary>One message from the App Store bridge: "kind|productId|text".</summary>
    public struct StoreEvent
    {
        public StoreEventKind Kind;
        public string ProductId;
        public string Message;

        public static bool TryParse(string line, out StoreEvent e)
        {
            e = default;
            if (string.IsNullOrEmpty(line)) return false;
            var parts = line.Split(new[] { '|' }, 3);
            switch (parts[0])
            {
                case "purchased": e.Kind = StoreEventKind.Purchased; break;
                case "restored": e.Kind = StoreEventKind.Restored; break;
                case "failed": e.Kind = StoreEventKind.Failed; break;
                case "cancelled": e.Kind = StoreEventKind.Cancelled; break;
                case "restoreDone": e.Kind = StoreEventKind.RestoreFinished; break;
                case "restoreFailed": e.Kind = StoreEventKind.RestoreFailed; break;
                case "deferred": e.Kind = StoreEventKind.Deferred; break;
                case "products": e.Kind = StoreEventKind.ProductsLoaded; break;
                default: return false;
            }
            e.ProductId = parts.Length > 1 ? parts[1] : "";
            e.Message = parts.Length > 2 ? parts[2] : "";
            return true;
        }

        /// <summary>Whether this event unlocks the product.</summary>
        public bool Grants => Kind == StoreEventKind.Purchased || Kind == StoreEventKind.Restored;
    }
}
