using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>One skater's line in the contest standings.</summary>
    public sealed class ContestEntry
    {
        public string Name;
        public bool IsYou;
        public readonly List<long> Heats = new List<long>();
        public long Total => SpotContest.CountedTotal(Heats);
    }

    /// <summary>Your best finish at a park's contest. JsonUtility-friendly.</summary>
    [Serializable]
    public sealed class ContestRecord
    {
        public string parkId;
        /// <summary>1 = gold ... 3 = bronze, 0 = no podium yet.</summary>
        public int bestPlace;
        public long bestTotal;
        public int wins;
        /// <summary>The week (WeeklyEvents.WeekKey) podium tokens were last paid, and the place they were paid for.</summary>
        public int paidWeek;
        public int paidPlace;
    }

    /// <summary>
    /// Phase 30: SPOT CONTESTS, like the event days in arcade skate games: three 45-second heats at a park against five
    /// rival skaters, best two heats count, podium pays Tape Tokens. Rivals are seeded by park and week, so everyone who
    /// enters this week's contest at a park faces the same field, and the field changes next week. Their heat scores
    /// are drawn around the park's par (see <see cref="SkaterRating.ParFor"/>). Engine-free and unit-tested.
    /// </summary>
    public static class SpotContest
    {
        public const int Heats = 3;
        public const int CountedHeats = 2;
        public const float HeatSeconds = 45f;
        public static readonly int[] PodiumTokens = { 150, 75, 40 };

        /// <summary>Original rival skaters (no real people).</summary>
        public static readonly string[] RivalNames =
        {
            "DUSTY VEGA", "KAI NOVA", "LITTLE MO", "JUNE BUG", "RIZZO", "TEO SPARKS", "MAMA BEAR", "PIXEL PETE", "SALLY SLAPPY",
        };

        /// <summary>Rival skill as a fraction of a heat's par: one beginner, one ringer, the rest in between.</summary>
        public static readonly float[] RivalSkill = { 0.55f, 0.7f, 0.85f, 1.0f, 1.2f };

        public static bool Available(string parkId) => SkaterRating.ParFor(parkId) > 0;

        /// <summary>Par for one 45-second heat (scaled from the two-minute run's par).</summary>
        public static long HeatPar(string parkId) => (long)Math.Round(SkaterRating.ParFor(parkId) * (HeatSeconds / 120.0));

        /// <summary>This week's seed for a park's contest.</summary>
        public static int Seed(string parkId, int weekKey)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in parkId ?? "") h = h * 31 + c;
                return h * 31 + weekKey;
            }
        }

        /// <summary>Sum of the best <see cref="CountedHeats"/> heats so far.</summary>
        public static long CountedTotal(IList<long> heats)
        {
            if (heats == null || heats.Count == 0) return 0;
            var sorted = new List<long>(heats);
            sorted.Sort((a, b) => b.CompareTo(a));
            long sum = 0;
            for (int i = 0; i < Math.Min(CountedHeats, sorted.Count); i++) sum += Math.Max(0, sorted[i]);
            return sum;
        }

        /// <summary>The five rivals for this park this week, with all three heats already decided.</summary>
        public static List<ContestEntry> Rivals(string parkId, int weekKey)
        {
            var rng = new Random(Seed(parkId, weekKey));
            var names = new List<string>(RivalNames);
            var list = new List<ContestEntry>();
            long par = HeatPar(parkId);
            for (int i = 0; i < RivalSkill.Length; i++)
            {
                int pick = rng.Next(names.Count);
                var e = new ContestEntry { Name = names[pick] };
                names.RemoveAt(pick);
                for (int h = 0; h < Heats; h++)
                {
                    double roll = 0.75 + rng.NextDouble() * 0.5;           // +/-25%
                    if (rng.NextDouble() < 0.15) roll *= 0.4;               // a heat that went wrong
                    long score = (long)Math.Round(par * RivalSkill[i] * roll / 10.0) * 10;
                    e.Heats.Add(Math.Max(0, score));
                }
                list.Add(e);
            }
            return list;
        }

        /// <summary>
        /// Standings after <paramref name="heatsDone"/> heats: rivals' heats revealed up to that point, you with your
        /// heats so far, best total first (ties go to you).
        /// </summary>
        public static List<ContestEntry> Standings(string parkId, int weekKey, IList<long> yourHeats, string yourName)
        {
            int done = yourHeats?.Count ?? 0;
            var table = new List<ContestEntry>();
            foreach (var r in Rivals(parkId, weekKey))
            {
                var shown = new ContestEntry { Name = r.Name };
                for (int h = 0; h < Math.Min(done, r.Heats.Count); h++) shown.Heats.Add(r.Heats[h]);
                table.Add(shown);
            }
            var you = new ContestEntry { Name = string.IsNullOrEmpty(yourName) ? "YOU" : yourName, IsYou = true };
            if (yourHeats != null) foreach (var s in yourHeats) you.Heats.Add(Math.Max(0, s));
            table.Add(you);
            table.Sort((a, b) =>
            {
                int c = b.Total.CompareTo(a.Total);
                return c != 0 ? c : a.IsYou ? -1 : b.IsYou ? 1 : 0;
            });
            return table;
        }

        /// <summary>Your place (1-based) in the standings.</summary>
        public static int PlaceOf(List<ContestEntry> standings)
        {
            if (standings == null) return 0;
            for (int i = 0; i < standings.Count; i++) if (standings[i].IsYou) return i + 1;
            return 0;
        }

        public static int TokensFor(int place) => place >= 1 && place <= PodiumTokens.Length ? PodiumTokens[place - 1] : 0;

        public static string PlaceLabel(int place) =>
            place == 1 ? "1ST" : place == 2 ? "2ND" : place == 3 ? "3RD" : place > 0 ? place + "TH" : "-";

        /// <summary>What you need in your next heat to move up one place (0 if you lead or the contest is over).</summary>
        public static long ToMoveUp(string parkId, int weekKey, IList<long> yourHeats, string yourName)
        {
            int done = yourHeats?.Count ?? 0;
            if (done >= Heats) return 0;
            var now = Standings(parkId, weekKey, yourHeats, yourName);
            int place = PlaceOf(now);
            if (place <= 1) return 0;
            // Rivals' totals once they've skated the next heat too.
            var after = Rivals(parkId, weekKey);
            long target = 0;
            var totals = new List<long>();
            foreach (var r in after)
            {
                var heats = new List<long>();
                for (int h = 0; h <= Math.Min(done, r.Heats.Count - 1); h++) heats.Add(r.Heats[h]);
                totals.Add(CountedTotal(heats));
            }
            totals.Sort((a, b) => b.CompareTo(a));
            // Beat the rival who'll be one place above you.
            int idx = Math.Max(0, Math.Min(totals.Count - 1, place - 2));
            target = totals[idx];
            // Smallest next heat that lifts your counted total above the target.
            var mine = new List<long>(yourHeats ?? new List<long>());
            long lo = 0, hi = Math.Max(target + 1, 1);
            mine.Add(hi);
            if (CountedTotal(mine) <= target) return 0; // can't catch them this heat
            while (lo < hi)
            {
                long mid = lo + (hi - lo) / 2;
                mine[mine.Count - 1] = mid;
                if (CountedTotal(mine) > target) hi = mid; else lo = mid + 1;
            }
            return lo;
        }

        /// <summary>
        /// Records a finished contest and returns the Tape Tokens it pays. The field is the same all week, so a park's
        /// podium pays once per week: a better finish later that week pays only the difference, and replays pay nothing.
        /// Wins count once per park per week too.
        /// </summary>
        public static int Record(List<ContestRecord> records, string parkId, int place, long total, int weekKey)
        {
            if (records == null || string.IsNullOrEmpty(parkId)) return 0;
            var r = records.Find(x => x.parkId == parkId);
            if (r == null) { r = new ContestRecord { parkId = parkId }; records.Add(r); }
            if (place >= 1 && place <= 3 && (r.bestPlace == 0 || place < r.bestPlace)) r.bestPlace = place;
            if (total > r.bestTotal) r.bestTotal = total;
            int earned = TokensFor(place);
            if (earned <= 0) return 0;
            bool thisWeek = r.paidWeek == weekKey;
            int already = thisWeek ? TokensFor(r.paidPlace) : 0;
            if (thisWeek && r.paidPlace >= 1 && place >= r.paidPlace) return 0;
            if (place == 1 && !(thisWeek && r.paidPlace == 1)) r.wins++;
            r.paidWeek = weekKey;
            r.paidPlace = place;
            return Math.Max(0, earned - already);
        }
    }
}
