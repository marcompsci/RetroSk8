using System;

namespace RetroSk8.Core
{
    /// <summary>
    /// Anti-cheat ceilings for Game Center (Phase 19). Retro Sk8 has no server of its own, so the game checks every
    /// score before sending it: anything no real run could reach (a combo worth more than the whole scoring system can
    /// pay, a race faster than the skater's top speed allows) is dropped and logged instead of posted. The same numbers
    /// go into App Store Connect as each leaderboard's score range, so Game Center itself ignores outliers sent by a
    /// modified copy of the game. Generous on purpose: a real record must never be rejected.
    /// </summary>
    public static class ScoreLimits
    {
        /// <summary>Most points a run can earn per second of run time (well above anything the scoring can produce).</summary>
        public const long MaxPointsPerSecond = 25000;
        /// <summary>Longest timed run (Retro City); Two-Minute Runs are 120 s.</summary>
        public const float LongestRunSeconds = 180f;
        /// <summary>The most one banked line can be worth.</summary>
        public const long MaxCombo = 3000000;
        public const long MaxRunScore = (long)(MaxPointsPerSecond * LongestRunSeconds); // 4,500,000
        /// <summary>A believable career total of S.K.A.T.E. wins.</summary>
        public const long MaxSkateWins = 100000;
        /// <summary>Faster than the skater can roll or fall (m/s); race floors use this with a safety margin.</summary>
        public const float MaxSpeed = 30f;
        public const float RaceMargin = 0.8f;
        /// <summary>Slowest race time worth a board entry (10 minutes, in hundredths).</summary>
        public const long MaxRaceHundredths = 60000;

        public enum BoardKind { Unknown = 0, ParkRun = 1, Weekly = 2, Race = 3, Challenge = 4, SkateWins = 5 }

        public static BoardKind KindOf(string boardId)
        {
            if (string.IsNullOrEmpty(boardId)) return BoardKind.Unknown;
            if (boardId == Leaderboards.SkateWins) return BoardKind.SkateWins;
            if (boardId == WeeklyEvents.LeaderboardId) return BoardKind.Weekly;
            if (boardId.StartsWith(Achievements.GameCenterPrefix + "score.", StringComparison.Ordinal)) return BoardKind.ParkRun;
            if (boardId.StartsWith(Achievements.GameCenterPrefix + "race.", StringComparison.Ordinal)) return BoardKind.Race;
            if (boardId.StartsWith(Achievements.GameCenterPrefix + "challenge.", StringComparison.Ordinal)) return BoardKind.Challenge;
            return BoardKind.Unknown;
        }

        /// <summary>Shortest believable race time in hundredths: the straight-line gate path at top speed, minus a margin.</summary>
        public static long MinRaceHundredths(CityRace race)
        {
            if (race == null || race.Gates == null || race.GateCount < 2) return 0;
            double length = 0;
            for (int i = 1; i < race.GateCount; i++)
            {
                double dx = race.Gates[i * 2] - race.Gates[i * 2 - 2], dz = race.Gates[i * 2 + 1] - race.Gates[i * 2 - 1];
                length += Math.Sqrt(dx * dx + dz * dz);
            }
            return (long)Math.Floor(length / MaxSpeed * RaceMargin * 100.0);
        }

        /// <summary>The (min, max) a board accepts. Also the "score range" to set in App Store Connect.</summary>
        public static (long min, long max) RangeFor(string boardId)
        {
            switch (KindOf(boardId))
            {
                case BoardKind.ParkRun:
                case BoardKind.Weekly: return (1, MaxRunScore);
                case BoardKind.Challenge: return (1, MaxCombo);
                case BoardKind.SkateWins: return (1, MaxSkateWins);
                case BoardKind.Race:
                {
                    string raceId = boardId.Substring((Achievements.GameCenterPrefix + "race.").Length);
                    var race = RetroCityLayout.FindRace(raceId);
                    return (Math.Max(1, MinRaceHundredths(race)), MaxRaceHundredths);
                }
                default: return (0, 0);
            }
        }

        /// <summary>True when <paramref name="score"/> is believable for the board (unknown boards are never sent).</summary>
        public static bool IsPlausible(string boardId, long score)
        {
            var (min, max) = RangeFor(boardId);
            return max > 0 && score >= min && score <= max;
        }

        /// <summary>A run's score is believable for its length: within <see cref="MaxPointsPerSecond"/> per second.</summary>
        public static bool IsPlausibleRun(long score, float seconds) =>
            score >= 0 && seconds > 0f && score <= (long)(MaxPointsPerSecond * Math.Min(seconds, LongestRunSeconds));

        /// <summary>A banked line's points, clamped to what the game can pay (used on untrusted duel results).</summary>
        public static long ClampCombo(long points) => points < 0 ? 0 : points > MaxCombo ? MaxCombo : points;
    }
}
