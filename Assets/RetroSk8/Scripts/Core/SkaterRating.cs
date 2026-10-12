using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>Everything the SK8 RATING is worked out from: plain numbers the runtime copies out of the save.</summary>
    public sealed class RatingFacts
    {
        /// <summary>Best Two-Minute Run score per built-in park (assisted bests already left out).</summary>
        public Dictionary<string, long> ParkBests = new Dictionary<string, long>();
        public ICollection<string> NightLines = new List<string>();
        /// <summary>Best spot-challenge combo per Retro City spot.</summary>
        public Dictionary<string, long> SpotBests = new Dictionary<string, long>();
        /// <summary>Best time in seconds per Retro City race (0 = not finished).</summary>
        public Dictionary<string, float> RaceBests = new Dictionary<string, float>();
        public int StoryChaptersDone;
        public int StoryChapters = 10;
        public int TricksInBook;
        public int TricksLanded;
        public int TrickGoalsDone;
        public int SkateWins;
        public int BestStreak;
    }

    public enum RatingPart { Parks = 0, NightLines = 1, Spots = 2, Races = 3, Story = 4, TrickBook = 5, Skate = 6, Streak = 7 }

    /// <summary>One line of the rating breakdown.</summary>
    public struct RatingLine
    {
        public RatingPart Part;
        public int Points;
        public int Max;
    }

    /// <summary>A concrete way to raise the rating, and what it's worth.</summary>
    public struct RatingTip
    {
        public string Text;
        public int Gain;
    }

    /// <summary>Phase 30: how a run at a park rates against that park's par.</summary>
    public enum RunGrade { None = 0, D = 1, C = 2, B = 3, A = 4, S = 5 }

    /// <summary>
    /// The SK8 RATING (Phase 30): one career number from 0 to 10,000 that measures the whole skater, posted to its own
    /// Game Center board. Built to fix what single-park high scores get wrong in other skate games:
    /// - every park counts the same, because each best is scored against that park's par on a curve (75% of a park's
    ///   points at par, and less and less for going further), so farming one easy park can't carry a rating;
    /// - breadth beats grinding: night lines, city spots and races, the story, the Trick Book, S.K.A.T.E. and the
    ///   daily streak all add points, each capped;
    /// - it only ever uses your best results, so playing never lowers it, and assisted bests don't count;
    /// - it's transparent: a breakdown and "what's worth the most next" tips are shown with it.
    /// Engine-free, so the numbers are unit-tested.
    /// </summary>
    public static class SkaterRating
    {
        public const int Max = 10000;
        public const string LeaderboardId = "retrosk8.rating";

        public const int ParkPoints = 480;      // x10 parks = 4,800
        public const int NightLinePoints = 50;  // x10 = 500
        public const int SpotPoints = 90;       // x10 = 900
        public const int RacePoints = 150;      // x4 = 600
        public const int StoryPoints = 600;
        public const int TricksLandedPoints = 900;
        public const int TrickGoalPoints = 600;
        public const int SkatePoints = 500;
        public const int StreakPoints = 600;

        /// <summary>S.K.A.T.E. wins and streak days that score 75% of their part.</summary>
        public const int SkateWinsPar = 10;
        public const int StreakPar = 14;

        /// <summary>
        /// A park's par: a strong Two-Minute Run, set at 5x the park's night line (itself ~1.2x its best contract line).
        /// One knob to retune once real scores come in from the beta.
        /// </summary>
        public const int ParFactor = 5;

        /// <summary>The curve's steepness: ln 4, so a result at par scores 75%, double par ~94%, triple par ~98%.</summary>
        private static readonly double K = Math.Log(4.0);

        public static readonly string[] RankNames = { "ROOKIE", "GROM", "LOCAL", "AM", "SPONSORED", "PRO", "LEGEND", "ALL-CITY" };
        public static readonly int[] RankFloors = { 0, 1000, 2500, 4000, 5500, 7000, 8500, 9500 };

        /// <summary>0..1 for a result <paramref name="ratio"/> times par. Never reaches 1, never goes down with a better result.</summary>
        public static double Curve(double ratio) => ratio <= 0 || double.IsNaN(ratio) ? 0 : 1.0 - Math.Exp(-K * Math.Min(ratio, 50.0));

        private static int Scaled(int max, double ratio) => (int)Math.Floor(max * Curve(ratio));

        public static long ParFor(string parkId) => NightSessions.TargetFor(parkId) * ParFactor;

        public static IEnumerable<string> Parks => NightSessions.Parks;

        public static int ParkScore(string parkId, long best)
        {
            long par = ParFor(parkId);
            return par <= 0 || best <= 0 ? 0 : Scaled(ParkPoints, (double)best / par);
        }

        public static int SpotScore(CitySpot spot, long best) =>
            spot == null || best <= 0 || spot.Gold <= 0 ? 0 : Scaled(SpotPoints, (double)best / spot.Gold);

        /// <summary>Races score on speed: the gold time is par (75%), faster keeps adding a little.</summary>
        public static int RaceScore(CityRace race, float seconds) =>
            race == null || seconds <= 0f || race.Gold <= 0f ? 0 : Scaled(RacePoints, race.Gold / seconds);

        public static RunGrade GradeFor(string parkId, long score)
        {
            long par = ParFor(parkId);
            if (par <= 0 || score <= 0) return RunGrade.None;
            double r = (double)score / par;
            return r >= 1.5 ? RunGrade.S : r >= 1.0 ? RunGrade.A : r >= 0.6 ? RunGrade.B : r >= 0.3 ? RunGrade.C : RunGrade.D;
        }

        /// <summary>The score needed at a park for the next grade (0 when it's already S or the park has no par).</summary>
        public static long NextGradeScore(string parkId, long score)
        {
            long par = ParFor(parkId);
            if (par <= 0) return 0;
            double[] steps = { 0.3, 0.6, 1.0, 1.5 };
            foreach (var s in steps)
            {
                long need = (long)Math.Ceiling(par * s);
                if (score < need) return need;
            }
            return 0;
        }

        public static string GradeLabel(RunGrade g) => g == RunGrade.None ? "-" : g.ToString();

        public static List<RatingLine> Breakdown(RatingFacts f)
        {
            f = f ?? new RatingFacts();
            var lines = new List<RatingLine>();
            int parks = 0, parkMax = 0, nights = 0, nightMax = 0;
            foreach (var id in Parks)
            {
                parkMax += ParkPoints;
                nightMax += NightLinePoints;
                f.ParkBests.TryGetValue(id, out long best);
                parks += ParkScore(id, best);
                if (f.NightLines != null && f.NightLines.Contains(id)) nights += NightLinePoints;
            }
            lines.Add(new RatingLine { Part = RatingPart.Parks, Points = parks, Max = parkMax });
            lines.Add(new RatingLine { Part = RatingPart.NightLines, Points = nights, Max = nightMax });

            int spots = 0, spotMax = 0;
            foreach (var s in RetroCityLayout.Spots)
            {
                spotMax += SpotPoints;
                f.SpotBests.TryGetValue(s.Id, out long best);
                spots += SpotScore(s, best);
            }
            lines.Add(new RatingLine { Part = RatingPart.Spots, Points = spots, Max = spotMax });

            int races = 0, raceMax = 0;
            foreach (var r in RetroCityLayout.Races)
            {
                raceMax += RacePoints;
                f.RaceBests.TryGetValue(r.Id, out float t);
                races += RaceScore(r, t);
            }
            lines.Add(new RatingLine { Part = RatingPart.Races, Points = races, Max = raceMax });

            int chapters = Math.Max(1, f.StoryChapters);
            int story = (int)Math.Floor(StoryPoints * Clamp01((double)f.StoryChaptersDone / chapters));
            lines.Add(new RatingLine { Part = RatingPart.Story, Points = story, Max = StoryPoints });

            int book = 0;
            if (f.TricksInBook > 0)
            {
                book += (int)Math.Floor(TricksLandedPoints * Clamp01((double)f.TricksLanded / f.TricksInBook));
                book += (int)Math.Floor(TrickGoalPoints * Clamp01((double)f.TrickGoalsDone / (f.TricksInBook * 4.0)));
            }
            lines.Add(new RatingLine { Part = RatingPart.TrickBook, Points = book, Max = TricksLandedPoints + TrickGoalPoints });

            lines.Add(new RatingLine { Part = RatingPart.Skate, Points = Scaled(SkatePoints, (double)f.SkateWins / SkateWinsPar), Max = SkatePoints });
            lines.Add(new RatingLine { Part = RatingPart.Streak, Points = Scaled(StreakPoints, (double)f.BestStreak / StreakPar), Max = StreakPoints });
            return lines;
        }

        public static int Total(RatingFacts f)
        {
            int sum = 0;
            foreach (var l in Breakdown(f)) sum += l.Points;
            return Math.Max(0, Math.Min(Max, sum));
        }

        public static int RankIndex(int rating)
        {
            int idx = 0;
            for (int i = 0; i < RankFloors.Length; i++) if (rating >= RankFloors[i]) idx = i;
            return idx;
        }

        public static string RankName(int rating) => RankNames[RankIndex(rating)];

        /// <summary>Points to the next rank (0 at the top rank).</summary>
        public static int ToNextRank(int rating)
        {
            int i = RankIndex(rating);
            return i + 1 < RankFloors.Length ? RankFloors[i + 1] - rating : 0;
        }

        public static string PartName(RatingPart p)
        {
            switch (p)
            {
                case RatingPart.Parks: return "PARK BESTS";
                case RatingPart.NightLines: return "NIGHT LINES";
                case RatingPart.Spots: return "CITY SPOTS";
                case RatingPart.Races: return "CITY RACES";
                case RatingPart.Story: return "SKATE STORY";
                case RatingPart.TrickBook: return "TRICK BOOK";
                case RatingPart.Skate: return "S.K.A.T.E. WINS";
                default: return "DAILY STREAK";
            }
        }

        /// <summary>
        /// The most valuable next steps, best first: reaching the next grade at a park, clearing a night line, a gold
        /// at a spot or race, the next story chapter, landing new tricks. Each tip says what it's worth.
        /// </summary>
        public static List<RatingTip> Tips(RatingFacts f, Func<string, string> parkName = null, int count = 3)
        {
            f = f ?? new RatingFacts();
            var tips = new List<RatingTip>();
            foreach (var id in Parks)
            {
                string name = parkName?.Invoke(id) ?? id.ToUpperInvariant();
                f.ParkBests.TryGetValue(id, out long best);
                long need = NextGradeScore(id, best);
                if (need > 0)
                {
                    int gain = ParkScore(id, need) - ParkScore(id, best);
                    var grade = GradeFor(id, need);
                    if (gain > 0) tips.Add(new RatingTip { Gain = gain, Text = $"{need:N0} IN A RUN AT {name} ({GradeLabel(grade)} GRADE)" });
                }
                if (f.NightLines == null || !f.NightLines.Contains(id))
                    tips.Add(new RatingTip { Gain = NightLinePoints, Text = $"CLEAR THE NIGHT LINE AT {name} ({NightSessions.TargetFor(id):N0} IN ONE LINE)" });
            }
            foreach (var s in RetroCityLayout.Spots)
            {
                f.SpotBests.TryGetValue(s.Id, out long best);
                if (best >= s.Gold) continue;
                int gain = SpotScore(s, s.Gold) - SpotScore(s, best);
                if (gain > 0) tips.Add(new RatingTip { Gain = gain, Text = $"GOLD AT {s.Name.ToUpperInvariant()} ({s.Gold:N0} IN ONE LINE)" });
            }
            foreach (var r in RetroCityLayout.Races)
            {
                f.RaceBests.TryGetValue(r.Id, out float t);
                if (t > 0f && t <= r.Gold) continue;
                int gain = RaceScore(r, r.Gold) - RaceScore(r, t);
                if (gain > 0) tips.Add(new RatingTip { Gain = gain, Text = $"GOLD IN {r.Name.ToUpperInvariant()} (UNDER {r.Gold:0} S)" });
            }
            int chapters = Math.Max(1, f.StoryChapters);
            if (f.StoryChaptersDone < chapters)
                tips.Add(new RatingTip { Gain = StoryPoints / chapters, Text = $"FINISH SKATE STORY CHAPTER {f.StoryChaptersDone + 1}" });
            if (f.TricksInBook > 0 && f.TricksLanded < f.TricksInBook)
            {
                int left = Math.Min(5, f.TricksInBook - f.TricksLanded);
                int gain = (int)Math.Floor(TricksLandedPoints * (double)left / f.TricksInBook);
                if (gain > 0) tips.Add(new RatingTip { Gain = gain, Text = $"LAND {left} NEW TRICK{(left == 1 ? "" : "S")} (TRICK BOOK)" });
            }
            tips.Sort((a, b) => b.Gain.CompareTo(a.Gain));
            if (tips.Count > count) tips.RemoveRange(count, tips.Count - count);
            return tips;
        }

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
