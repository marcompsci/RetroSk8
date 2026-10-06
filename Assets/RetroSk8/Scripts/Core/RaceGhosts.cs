using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>
    /// A recorded city checkpoint race (Phase 26): who skated it, their time, the time at each gate, and the run
    /// itself so it can be raced as a ghost.
    /// </summary>
    public sealed class RaceGhost
    {
        public string RaceId;
        public string From;
        public float Time;
        /// <summary>Race time when each gate after the start was passed; the last entry is the finish (= Time).</summary>
        public List<float> Splits = new List<float>();
        public ReplayTrack Track;
    }

    /// <summary>
    /// Ghost races (Phase 26): split times against your best (or a friend's) run at every gate of a Retro City race.
    /// Engine-free and unit-tested.
    /// </summary>
    public static class RaceSplits
    {
        /// <summary>Where the ghost track for a race is saved (GhostStore key; never collides with a park id).</summary>
        public static string GhostKey(string raceId) => "race_" + (raceId ?? "");

        /// <summary>
        /// The reference split for the <paramref name="passed"/>-th gate passed after the start (1 = first gate after
        /// the line), or a negative number when there's nothing to compare against.
        /// </summary>
        public static float Reference(IList<float> splits, int passed)
        {
            if (splits == null || passed < 1 || passed > splits.Count) return -1f;
            float v = splits[passed - 1];
            return v > 0f ? v : -1f;
        }

        /// <summary>"-0.42" when ahead, "+1.10" when behind, "0.00" level. Hundredths, sign always shown when not level.</summary>
        public static string Delta(float mine, float reference)
        {
            int cs = (int)Math.Round((mine - reference) * 100f);
            if (cs == 0) return "0.00";
            string body = (Math.Abs(cs) / 100).ToString() + "." + (Math.Abs(cs) % 100).ToString("00");
            return (cs < 0 ? "-" : "+") + body;
        }

        /// <summary>
        /// Whether a saved ghost recording belongs to a best time: recorded at 20 Hz up to the finish, so it ends within a
        /// couple of samples of it.
        /// </summary>
        public static bool TrackMatches(float trackDuration, float bestTime) =>
            bestTime > 0f && trackDuration > 0f && Math.Abs(trackDuration - bestTime) <= 0.2f;

        /// <summary>True when the splits look like a real run of this race: one per gate after the start, rising, ending at the time.</summary>
        public static bool AreValid(IList<float> splits, int gateCount, float time)
        {
            if (splits == null || gateCount < 2 || splits.Count != gateCount - 1 || time <= 0f) return false;
            float last = 0f;
            foreach (float s in splits)
            {
                if (!(s > last) || s > time + 0.011f) return false; // !(>) also refuses NaN
                last = s;
            }
            return Math.Abs(last - time) <= 0.011f;
        }
    }

    /// <summary>
    /// Race ghost codes (Phase 26): one Retro City checkpoint race packed into text, so a friend can race your actual
    /// line through the streets. "RR:" + the same checksummed base-64 bit stream as ghost codes, with the race, the
    /// time, every split and the run resampled at 10 Hz. Races are short (well under the 3-minute ghost cap), so a code
    /// is a few thousand to ~20,000 characters.
    /// </summary>
    public static class RaceGhostCodes
    {
        public const string Prefix = "RR:";
        private const int Version = 1;
        /// <summary>Times are stored in hundredths of a second in 16 bits (up to 655 s, far past any race's time limit).</summary>
        private const int TimeBits = 16;

        public static bool IsRaceCode(string text) => text != null && text.TrimStart().StartsWith(Prefix, StringComparison.Ordinal);

        public static string Encode(RaceGhost g)
        {
            if (g == null) throw new ArgumentNullException(nameof(g));
            if (g.Track == null || g.Track.Count < 2) throw new ArgumentException("No ghost to send");
            var race = RetroCityLayout.FindRace(g.RaceId) ?? throw new ArgumentException("Unknown race " + g.RaceId);
            if (!RaceSplits.AreValid(g.Splits, race.GateCount, g.Time)) throw new ArgumentException("Splits don't match the race");
            int index = IndexOf(g.RaceId);

            var w = new ShareCodes.BitWriter();
            w.Write(Version, 4);
            w.Write(index, 6);
            w.Write(Cs(g.Time), TimeBits);
            w.Write(g.Splits.Count, 6);
            foreach (float s in g.Splits) w.Write(Cs(s), TimeBits);
            ShareCodes.WriteName(w, g.From ?? "", ShareCodes.MaxFromLength);
            GhostCodes.WriteFrames(w, g.Track);
            return GhostCodes.Seal(Prefix, w.ToBytes());
        }

        public static bool TryDecode(string code, out RaceGhost ghost, out string error)
        {
            ghost = null;
            if (string.IsNullOrWhiteSpace(code)) { error = "NO CODE FOUND"; return false; }
            if (code.Length > CodeLimits.MaxGhostCodeChars) { error = "THAT RACE CODE IS TOO LONG"; return false; }
            if (!IsRaceCode(code)) { error = "THAT ISN'T A RACE GHOST CODE"; return false; }
            var bytes = GhostCodes.Unseal(Prefix, code);
            if (bytes == null) { error = "THAT RACE CODE IS INCOMPLETE (COPY ALL OF IT)"; return false; }
            try
            {
                var r = new ShareCodes.BitReader(bytes);
                int version = r.Read(4);
                if (version != Version) { error = "THAT RACE NEEDS A NEWER VERSION"; return false; }
                int index = r.Read(6);
                if (index >= RetroCityLayout.Races.Count) { error = "THAT RACE NEEDS A NEWER VERSION"; return false; }
                var race = RetroCityLayout.Races[index];
                var g = new RaceGhost { RaceId = race.Id, Time = r.Read(TimeBits) / 100f };
                int n = r.Read(6);
                for (int i = 0; i < n; i++) g.Splits.Add(r.Read(TimeBits) / 100f);
                if (!RaceSplits.AreValid(g.Splits, race.GateCount, g.Time)) { error = "THAT RACE CODE IS DAMAGED"; return false; }
                g.From = ShareCodes.ReadName(r, ShareCodes.MaxFromLength);
                g.Track = GhostCodes.ReadFrames(r, RaceSplits.GhostKey(race.Id), out error);
                if (g.Track == null) return false;
                ghost = g;
                error = null;
                return true;
            }
            catch (Exception)
            {
                error = "THAT RACE CODE IS DAMAGED";
                return false;
            }
        }

        private static int IndexOf(string raceId)
        {
            for (int i = 0; i < RetroCityLayout.Races.Count; i++) if (RetroCityLayout.Races[i].Id == raceId) return i;
            return -1;
        }

        private static int Cs(float seconds) => Math.Max(0, Math.Min((1 << TimeBits) - 1, (int)Math.Round(seconds * 100f)));
    }
}
