using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>One stop on today's City Jam route.</summary>
    public sealed class JamStop
    {
        public CitySpot Spot;
        /// <summary>Points to bank inside the spot before the session clock runs out.</summary>
        public long Target;
    }

    public enum JamPhase { Travel = 0, Session = 1, Done = 2 }

    /// <summary>Today's City Jam result (JsonUtility-friendly).</summary>
    [Serializable]
    public sealed class JamRecord
    {
        /// <summary>Streaks.DayNumber of the day the medal below was earned.</summary>
        public int day;
        public int medal;
        public long bestTotal;
        public int jamsPlayed;
        public int golds;
    }

    /// <summary>
    /// City Jam (Phase 15): a daily event in Retro City. Today's route is four spots in a set order; ride to each one,
    /// then bank its target inside the spot before a short session clock runs out. The whole jam has an overall
    /// clock too. Clear 2 stops for bronze, 3 for silver, all 4 for gold. Everyone gets the same route on the same
    /// day. Engine-free and unit-tested.
    /// </summary>
    public static class CityJam
    {
        public const int StopCount = 4;
        public const float TotalSeconds = 300f;
        public const float SessionSeconds = 30f;
        /// <summary>A stop's target as a share of its spot challenge's bronze score (a jam is a sprint, not a session).</summary>
        public const float TargetShare = 0.6f;

        /// <summary>Tokens for a medal; replaying only pays the difference when you beat today's medal.</summary>
        public static int Tokens(Medal m) => m == Medal.Gold ? 80 : m == Medal.Silver ? 40 : m == Medal.Bronze ? 20 : 0;

        public static Medal MedalFor(int cleared) =>
            cleared >= 4 ? Medal.Gold : cleared == 3 ? Medal.Silver : cleared == 2 ? Medal.Bronze : Medal.None;

        /// <summary>Today's route: four different spots, picked by the day so every player rides the same jam.</summary>
        public static List<JamStop> Plan(int day, IReadOnlyList<CitySpot> spots)
        {
            var list = new List<JamStop>();
            if (spots == null || spots.Count == 0) return list;
            var pool = new List<CitySpot>(spots);
            var rng = new Random(unchecked(day * 7919 + 17));
            int n = Math.Min(StopCount, pool.Count);
            for (int i = 0; i < n; i++)
            {
                int k = rng.Next(pool.Count);
                var s = pool[k];
                pool.RemoveAt(k);
                long target = (long)Math.Round(s.Bronze * TargetShare / 100.0) * 100;
                list.Add(new JamStop { Spot = s, Target = Math.Max(500, target) });
            }
            return list;
        }

        /// <summary>Pays the improvement over today's best medal and records the run. Returns tokens to pay.</summary>
        public static int Record(JamRecord rec, int today, Medal medal, long total)
        {
            if (rec == null) return 0;
            if (rec.day != today) { rec.day = today; rec.medal = 0; }
            rec.jamsPlayed++;
            rec.bestTotal = Math.Max(rec.bestTotal, total);
            var before = (Medal)rec.medal;
            if (medal <= before) return 0;
            rec.medal = (int)medal;
            if (medal == Medal.Gold) rec.golds++;
            return Tokens(medal) - Tokens(before);
        }
    }

    /// <summary>One City Jam in progress.</summary>
    public sealed class JamRun
    {
        public readonly List<JamStop> Stops;
        public int Index { get; private set; }
        public JamPhase Phase { get; private set; }
        public float TimeLeft { get; private set; }
        public float SessionLeft { get; private set; }
        public long StopScore { get; private set; }
        public long Total { get; private set; }
        public int Cleared { get; private set; }
        /// <summary>Why the jam ended early ("" while running or after clearing every stop).</summary>
        public string EndReason { get; private set; } = "";

        public JamRun(List<JamStop> stops)
        {
            Stops = stops ?? throw new ArgumentNullException(nameof(stops));
            TimeLeft = CityJam.TotalSeconds;
            Phase = stops.Count == 0 ? JamPhase.Done : JamPhase.Travel;
        }

        public JamStop Current => Phase == JamPhase.Done || Index >= Stops.Count ? null : Stops[Index];
        public bool Finished => Phase == JamPhase.Done;
        public Medal Result => CityJam.MedalFor(Cleared);

        public void Tick(float dt)
        {
            if (Finished || dt <= 0f) return;
            TimeLeft -= dt;
            if (Phase == JamPhase.Session) SessionLeft -= dt;
            if (TimeLeft <= 0f) { TimeLeft = 0f; End("OUT OF TIME"); return; }
            if (Phase == JamPhase.Session && SessionLeft <= 0f) { SessionLeft = 0f; End("SESSION OVER"); }
        }

        /// <summary>Call every frame with whether the skater is inside the current stop. Returns true when a session starts.</summary>
        public bool UpdatePosition(bool insideStop)
        {
            if (Phase != JamPhase.Travel || !insideStop) return false;
            Phase = JamPhase.Session;
            SessionLeft = CityJam.SessionSeconds;
            StopScore = 0;
            return true;
        }

        /// <summary>A banked line. Counts only during a session inside the stop. Returns true when it clears the stop.</summary>
        public bool AddBanked(long points, bool insideStop)
        {
            if (Phase != JamPhase.Session || !insideStop || points <= 0) return false;
            StopScore += points;
            Total += points;
            if (StopScore < Stops[Index].Target) return false;
            Cleared++;
            Index++;
            if (Index >= Stops.Count) { Phase = JamPhase.Done; return true; }
            Phase = JamPhase.Travel;
            StopScore = 0;
            return true;
        }

        /// <summary>Leaving a stop mid-session ends the jam (like spot challenges).</summary>
        public void LeftStop() { if (Phase == JamPhase.Session) End("LEFT THE SPOT"); }

        public void Abandon() => End("JAM ABANDONED");

        private void End(string reason)
        {
            if (Finished) return;
            EndReason = reason;
            Phase = JamPhase.Done;
        }
    }
}
