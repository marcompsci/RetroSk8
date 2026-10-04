using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Save;

namespace RetroSk8.Game
{
    /// <summary>
    /// Runtime side of the Trick Book (Phase 14): builds the catalogue from the trick library, records every banked line
    /// and pays the per-trick challenges in Tape Tokens (messages go through the shared results queue).
    /// </summary>
    public static class TrickBookService
    {
        private static List<TrickInfo> s_catalog;
        private static Dictionary<string, TrickInfo> s_byId;

        public static TrickBookState State => SaveManager.Data.trickBook;

        public static IReadOnlyList<TrickInfo> Catalog
        {
            get
            {
                if (s_catalog != null) return s_catalog;
                var lib = ContentRegistry.WithDefaults(null).trickLibrary;
                var source = new List<TrickInfo>();
                if (lib != null)
                    foreach (var d in lib.All())
                        if (d != null)
                            source.Add(new TrickInfo
                            {
                                Id = d.id, Name = d.displayName, Category = d.category, Family = d.family,
                                Variation = d.variation, IsSpecial = d.isSpecial, Points = d.baseValue,
                            });
                s_catalog = TrickCatalog.Build(source, lib != null ? lib.spinNames : null);
                s_byId = new Dictionary<string, TrickInfo>();
                foreach (var t in s_catalog) s_byId[t.Id] = t;
                return s_catalog;
            }
        }

        public static TrickInfo Find(string id)
        {
            if (Catalog == null || string.IsNullOrEmpty(id)) return null;
            return s_byId.TryGetValue(id, out var t) ? t : null;
        }

        /// <summary>Called for every banked line. Saves happen at run end (MetaHook) unless a challenge pays.</summary>
        public static void Record(IReadOnlyList<string> ids, long points, string locationId)
        {
            if (ids == null) return;
            var list = new List<string>(ids);
            var done = State.Record(list, points, locationId);
            if (done.Count == 0) return;
            int tokens = 0;
            foreach (var (id, goal) in done)
            {
                var info = Find(id);
                // Only tricks in the book pay (an id from a removed trick still records, but quietly).
                if (info == null) continue;
                int t = TrickBookState.Tokens(goal);
                tokens += t;
                CareerService.Pending.Add($"TRICK BOOK: {info.Name.ToUpperInvariant()} · {TrickBookState.GoalText(goal)}  +{t}");
            }
            if (tokens > 0) SaveManager.AddTokens(tokens); else SaveManager.Save();
        }

        /// <summary>(tricks landed at least once, tricks in the book).</summary>
        public static (int landed, int total) Completion()
        {
            int landed = 0;
            foreach (var t in Catalog)
            {
                var r = State.Find(t.Id);
                if (r != null && r.landed > 0) landed++;
            }
            return (landed, Catalog.Count);
        }
    }
}
