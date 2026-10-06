using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>One trick as the Trick Book shows it.</summary>
    public sealed class TrickInfo
    {
        public string Id;
        public string Name;
        public TrickCategory Category;
        public TrickFamily Family;
        public int Variation;
        public bool IsSpecial;
        public int Points;
        public TrickBookTab Tab;
        public string HowTo;
    }

    public enum TrickBookTab { Flips = 0, Grabs = 1, Shoves = 2, Grinds = 3, Manuals = 4, LipsAndWalls = 5, SpinsAndMore = 6, /// <summary>Specials: the shove/grab specials and each style's signature.</summary>
        Signatures = 7 }

    /// <summary>Four challenges every trick has (Phase 14). Saved as bits: append only.</summary>
    public enum TrickGoal
    {
        /// <summary>Land it 10 times.</summary>
        LandTen = 0,
        /// <summary>Land it inside a line of 5+ tricks.</summary>
        LongLine = 1,
        /// <summary>Land it at 3 different parks.</summary>
        ThreeParks = 2,
        /// <summary>Land it in a 10,000+ point line.</summary>
        BigLine = 3,
    }

    /// <summary>Your history with one trick (JsonUtility-friendly).</summary>
    [Serializable]
    public sealed class TrickRecord
    {
        public string id;
        public int landed;
        public int longestLine;   // most tricks in a banked line that included it
        public long bestLine;     // biggest banked line that included it
        public List<string> parks = new List<string>();
        public int claimed;       // TrickGoal bits already paid
    }

    /// <summary>
    /// The Trick Book: every trick in the game with how to do it, what you've landed, and four challenges per trick
    /// that pay Tape Tokens. Engine-free so the catalogue, the rules and the payouts are unit-tested.
    /// </summary>
    [Serializable]
    public sealed class TrickBookState
    {
        public const int LandTarget = 10;
        public const int LongLineTricks = 5;
        public const int ParkTarget = 3;
        public const long BigLinePoints = 10000;

        public List<TrickRecord> tricks = new List<TrickRecord>();

        public TrickRecord Find(string id)
        {
            foreach (var t in tricks) if (t.id == id) return t;
            return null;
        }

        private TrickRecord Get(string id)
        {
            var t = Find(id);
            if (t != null) return t;
            t = new TrickRecord { id = id };
            tricks.Add(t);
            return t;
        }

        /// <summary>
        /// Records a banked line (the ids of every trick in it, in order) at a park. Returns the challenges this
        /// completed for the first time, so the caller can pay them. Gaps aren't tricks and are ignored.
        /// </summary>
        public List<(string id, TrickGoal goal)> Record(IList<string> ids, long points, string locationId)
        {
            var done = new List<(string, TrickGoal)>();
            if (ids == null || ids.Count == 0 || points <= 0) return done;
            int lineLength = 0;
            foreach (var id in ids) if (Counts(id)) lineLength++;
            var seen = new HashSet<string>();
            foreach (var id in ids)
            {
                if (!Counts(id)) continue;
                var t = Get(id);
                t.landed++;
                if (!seen.Add(id)) continue; // per-line stats once per trick
                t.longestLine = Math.Max(t.longestLine, lineLength);
                t.bestLine = Math.Max(t.bestLine, points);
                if (!string.IsNullOrEmpty(locationId) && !t.parks.Contains(locationId)) t.parks.Add(locationId);
            }
            foreach (var id in seen)
            {
                var t = Find(id);
                foreach (TrickGoal g in Enum.GetValues(typeof(TrickGoal)))
                {
                    int bit = 1 << (int)g;
                    if ((t.claimed & bit) != 0 || !Met(t, g)) continue;
                    t.claimed |= bit;
                    done.Add((id, g));
                }
            }
            return done;
        }

        public static bool Counts(string id) => !string.IsNullOrEmpty(id) && !id.StartsWith("gap_", StringComparison.Ordinal);

        public static bool Met(TrickRecord t, TrickGoal g)
        {
            if (t == null) return false;
            switch (g)
            {
                case TrickGoal.LandTen: return t.landed >= LandTarget;
                case TrickGoal.LongLine: return t.longestLine >= LongLineTricks;
                case TrickGoal.ThreeParks: return t.parks.Count >= ParkTarget;
                default: return t.bestLine >= BigLinePoints;
            }
        }

        /// <summary>(current, target) for a progress bar.</summary>
        public static (long current, long target) Progress(TrickRecord t, TrickGoal g)
        {
            switch (g)
            {
                case TrickGoal.LandTen: return (Math.Min(LandTarget, t?.landed ?? 0), LandTarget);
                case TrickGoal.LongLine: return (Math.Min(LongLineTricks, t?.longestLine ?? 0), LongLineTricks);
                case TrickGoal.ThreeParks: return (Math.Min(ParkTarget, t?.parks.Count ?? 0), ParkTarget);
                default: return (Math.Min(BigLinePoints, t?.bestLine ?? 0), BigLinePoints);
            }
        }

        public static int Tokens(TrickGoal g) => g == TrickGoal.BigLine ? 40 : 25;

        public static string GoalText(TrickGoal g) =>
            g == TrickGoal.LandTen ? $"LAND IT {LandTarget} TIMES"
            : g == TrickGoal.LongLine ? $"LAND IT IN A {LongLineTricks}-TRICK LINE"
            : g == TrickGoal.ThreeParks ? $"LAND IT AT {ParkTarget} PARKS"
            : $"LAND IT IN A {BigLinePoints:N0}+ LINE";

        /// <summary>How many of a trick's four challenges are done.</summary>
        public int GoalsDone(string id)
        {
            var t = Find(id);
            if (t == null) return 0;
            int n = 0;
            for (int i = 0; i < 4; i++) if ((t.claimed & (1 << i)) != 0) n++;
            return n;
        }

        public void Sanitize()
        {
            if (tricks == null) tricks = new List<TrickRecord>();
            tricks.RemoveAll(t => t == null || string.IsNullOrEmpty(t.id));
            foreach (var t in tricks) if (t.parks == null) t.parks = new List<string>();
        }
    }

    /// <summary>Builds the Trick Book catalogue and writes each trick's "how to" from the real input rules.</summary>
    public static class TrickCatalog
    {
        private static readonly string[] Stick = { "STICK CENTRED", "STICK LEFT", "STICK RIGHT", "STICK UP OR DOWN" };

        /// <summary>
        /// The catalogue: the trick library's air tricks, specials, grinds and manuals (passed in), plus the tricks that
        /// live in code (lips, walls, reverts, pops, the style pack, spins and signatures).
        /// </summary>
        public static List<TrickInfo> Build(IEnumerable<TrickInfo> library, IList<string> spinNames)
        {
            var list = new List<TrickInfo>();
            var ids = new HashSet<string>();
            void Add(TrickInfo t)
            {
                if (t == null || string.IsNullOrEmpty(t.Id) || !ids.Add(t.Id)) return;
                if (string.IsNullOrEmpty(t.HowTo)) t.HowTo = HowTo(t);
                list.Add(t);
            }

            foreach (var t in library)
            {
                if (t == null) continue;
                // A full-meter flip swipe does your style's signature, so the library's flip special never comes up.
                if ((t.IsSpecial || t.Category == TrickCategory.Special) && t.Family == TrickFamily.Flip) continue;
                t.Tab = TabFor(t);
                Add(t);
            }
            // Style pack (Phase 10).
            foreach (var s in new[] { StyleTricks.NoComply, StyleTricks.Boneless })
                Add(new TrickInfo { Id = s.Id, Name = s.Name, Category = s.Category, Points = s.Points, Tab = TrickBookTab.SpinsAndMore });
            Add(new TrickInfo { Id = StyleTricks.Bluntslide.Id, Name = StyleTricks.Bluntslide.Name, Category = TrickCategory.Grind, Points = StyleTricks.Bluntslide.Points, Tab = TrickBookTab.Grinds,
                HowTo = "START A GRIND ON A LEDGE OR COPING WITH THE STICK DOWN (NOT ON A ROUND RAIL)." });
            Add(new TrickInfo { Id = StyleTricks.OneFootManual.Id, Name = StyleTricks.OneFootManual.Name, Category = TrickCategory.Manual, Points = StyleTricks.OneFootManual.Points, Tab = TrickBookTab.Manuals,
                HowTo = "START A MANUAL WITH THE STICK LEFT. BALANCE WITH LEFT AND RIGHT." });
            Add(new TrickInfo { Id = StyleTricks.Casper.Id, Name = StyleTricks.Casper.Name, Category = TrickCategory.Manual, Points = StyleTricks.Casper.Points, Tab = TrickBookTab.Manuals,
                HowTo = "START A MANUAL WITH THE STICK RIGHT. BALANCE WITH LEFT AND RIGHT." });

            // Lips (Phase 7): the stick picks the stall.
            foreach (StickZone z in new[] { StickZone.Neutral, StickZone.Up, StickZone.Down, StickZone.Left, StickZone.Right })
                Add(new TrickInfo
                {
                    Id = LipRules.IdFor(z), Name = LipRules.NameFor(z), Category = TrickCategory.Lip, Points = LipRules.StartPoints, Tab = TrickBookTab.LipsAndWalls,
                    HowTo = $"RIDE UP A QUARTER PIPE SQUARE TO THE COPING AND PRESS ACTION NEAR THE TOP WITH THE {ZoneText(z)}. ACTION OR JUMP DROPS BACK IN.",
                });
            Add(new TrickInfo { Id = "wallride", Name = "Wallride", Category = TrickCategory.Wall, Points = WallRules.WallrideStartPoints, Tab = TrickBookTab.LipsAndWalls,
                HowTo = "IN THE AIR NEXT TO A WALL, PRESS ACTION AND SKIM ALONG IT." });
            Add(new TrickInfo { Id = "wallplant", Name = "Wallplant", Category = TrickCategory.Wall, Points = WallRules.WallplantPoints, Tab = TrickBookTab.LipsAndWalls,
                HowTo = "IN THE AIR, HIT A WALL HEAD-ON AND PRESS ACTION TO PLANT AND SPRING BACK." });
            Add(new TrickInfo { Id = StyleTricks.FootplantId, Name = StyleTricks.FootplantName, Category = TrickCategory.Wall, Points = WallRules.WallplantPoints, Tab = TrickBookTab.LipsAndWalls,
                HowTo = "LIKE A WALLPLANT, WITH THE STICK DOWN. PAYS A QUARTER MORE." });
            Add(new TrickInfo { Id = "wallie", Name = "Wallie", Category = TrickCategory.Wall, Points = WallRules.WalliePoints, Tab = TrickBookTab.LipsAndWalls,
                HowTo = "JUMP DURING A WALLRIDE OR A PLANT TO POP OFF THE WALL." });
            // Bonks (Phase 18): objects marked in the parks (cones, hydrants, car bumpers, speaker posts).
            Add(new TrickInfo { Id = BonkRules.BonkId, Name = BonkRules.BonkName, Category = TrickCategory.Bonk, Points = BonkRules.BonkPoints, Tab = TrickBookTab.SpinsAndMore,
                HowTo = "IN THE AIR, HIT A CONE, HYDRANT, BARREL, CAR BUMPER OR SPEAKER POST. YOU TAP OFF IT INSTEAD OF BAILING." });
            Add(new TrickInfo { Id = BonkRules.PoleJamId, Name = BonkRules.PoleJamName, Category = TrickCategory.Bonk, Points = BonkRules.PoleJamPoints, Tab = TrickBookTab.SpinsAndMore,
                HowTo = "ROLL FAST INTO A SLANTED POST (THE DRIVE-IN SPEAKER POSTS, CITY SIGNPOSTS) TO RIDE UP AND LAUNCH." });
            Add(new TrickInfo { Id = RevertRules.Id, Name = RevertRules.Name, Category = TrickCategory.Revert, Points = RevertRules.Points, Tab = TrickBookTab.SpinsAndMore,
                HowTo = "RIGHT AFTER LANDING FROM A RAMP, SWIPE LEFT OR RIGHT. THE LINE KEEPS GOING." });

            // Spins: hold left or right in the air.
            int spins = Math.Max(4, Math.Min(6, spinNames?.Count ?? 0));
            for (int half = 1; half <= spins; half++)
            {
                string name = spinNames != null && half - 1 < spinNames.Count && !string.IsNullOrEmpty(spinNames[half - 1]) ? spinNames[half - 1] : $"{half * 180}";
                Add(new TrickInfo { Id = $"spin_{half * 180}", Name = name, Category = TrickCategory.Spin, Tab = TrickBookTab.SpinsAndMore,
                    HowTo = $"HOLD LEFT OR RIGHT IN THE AIR AND LAND AFTER {half * 180}°." });
            }

            // Signature specials (one per style).
            for (int i = 0; i < StyleTricks.Signatures.Length; i++)
            {
                var s = StyleTricks.Signatures[i];
                Add(new TrickInfo { Id = s.Id, Name = s.Name, Category = TrickCategory.Special, Points = s.Points, IsSpecial = true, Tab = TrickBookTab.Signatures,
                    HowTo = $"{StyleTricks.StyleNames[i]} STYLE ONLY (CREATE-A-SKATER). FILL THE SPECIAL METER, THEN SWIPE UP IN THE AIR." });
            }
            return list;
        }

        public static TrickBookTab TabFor(TrickInfo t)
        {
            if (t.IsSpecial || t.Category == TrickCategory.Special) return TrickBookTab.Signatures;
            switch (t.Category)
            {
                case TrickCategory.Grind: return TrickBookTab.Grinds;
                case TrickCategory.Manual: return TrickBookTab.Manuals;
                case TrickCategory.Lip:
                case TrickCategory.Wall: return TrickBookTab.LipsAndWalls;
                case TrickCategory.Spin:
                case TrickCategory.Revert:
                case TrickCategory.Pop:
                case TrickCategory.Bonk: return TrickBookTab.SpinsAndMore;
            }
            return t.Family == TrickFamily.Grab ? TrickBookTab.Grabs : t.Family == TrickFamily.Shove ? TrickBookTab.Shoves : TrickBookTab.Flips;
        }

        /// <summary>Plain-English input for a library trick, from the same rules the game uses (GestureRules, TrickController).</summary>
        public static string HowTo(TrickInfo t)
        {
            string swipe = t.Family == TrickFamily.Grab ? "SWIPE DOWN" : t.Family == TrickFamily.Shove ? "SWIPE LEFT OR RIGHT" : "SWIPE UP";
            switch (t.Category)
            {
                case TrickCategory.Grind:
                    return $"PRESS ACTION NEAR A RAIL, LEDGE OR COPING WITH THE {Stick[Clamp(t.Variation)]}. BALANCE WITH LEFT AND RIGHT.";
                case TrickCategory.Manual:
                    return "PRESS ACTION RIGHT AFTER A LANDING. BALANCE WITH LEFT AND RIGHT; ACTION AGAIN TO BANK.";
            }
            if (t.IsSpecial || t.Category == TrickCategory.Special) return $"FILL THE SPECIAL METER, THEN {swipe} IN THE AIR.";
            return $"IN THE AIR, {swipe} WITH THE {Stick[Clamp(t.Variation)]}.";
        }

        public static string TabName(TrickBookTab tab) =>
            tab == TrickBookTab.Flips ? "FLIPS" : tab == TrickBookTab.Grabs ? "GRABS" : tab == TrickBookTab.Shoves ? "SHOVES"
            : tab == TrickBookTab.Grinds ? "GRINDS" : tab == TrickBookTab.Manuals ? "MANUALS" : tab == TrickBookTab.LipsAndWalls ? "LIPS + WALLS"
            : tab == TrickBookTab.SpinsAndMore ? "SPINS + MORE" : "SPECIALS";

        private static int Clamp(int v) => Math.Max(0, Math.Min(3, v));

        private static string ZoneText(StickZone z) =>
            z == StickZone.Up ? "STICK UP" : z == StickZone.Down ? "STICK DOWN" : z == StickZone.Left ? "STICK LEFT" : z == StickZone.Right ? "STICK RIGHT" : "STICK CENTRED";
    }
}
