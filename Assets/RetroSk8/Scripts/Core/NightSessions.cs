using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>Time of day for a park session (Phase 28). Saved by number: append only.</summary>
    public enum ParkTime { Day = 0, Dusk = 1, Night = 2 }

    /// <summary>
    /// Night sessions (Phase 28): every built-in park can be skated at dusk or at night (lights, neon and a night mix of
    /// its song), and each park has a NIGHT LINE: bank one combo worth its target while the lights are out.
    /// Engine-free so the rules and numbers are unit-tested.
    /// </summary>
    public static class NightSessions
    {
        public const int TimeCount = 3;
        /// <summary>Tokens for each park's first night line.</summary>
        public const int LineTokens = 250;

        public static readonly string[] TimeNames = { "DAY", "DUSK", "NIGHT" };

        public static string Label(ParkTime t) => TimeNames[Clamp((int)t)];

        public static ParkTime FromSaved(int v) => (ParkTime)Clamp(v);

        private static int Clamp(int v) => v < 0 || v >= TimeCount ? 0 : v;

        /// <summary>How golden (0-1) and how dark (0-1) a time of day looks.</summary>
        public static (float golden, float night) Blend(ParkTime t)
        {
            switch (t)
            {
                case ParkTime.Dusk: return (1f, 0.35f);
                case ParkTime.Night: return (0f, 1f);
                default: return (0f, 0f);
            }
        }

        /// <summary>
        /// Each built-in park's night line, in points banked in one combo: about 1.2x its spot contract line, rounded
        /// up to the thousand. Retro City keeps its own day and night clock, so it has none.
        /// </summary>
        private static readonly Dictionary<string, long> s_targets = new Dictionary<string, long>
        {
            { "harbor_plaza", 12000 },
            { "neon_warehouse", 18000 },
            { "rooftop_run", 20000 },
            { "sunset_bowls", 15000 },
            { "floodgate_ditch", 17000 },
            { "moonlight_pier", 18000 },
            { "drive_in", 20000 },
            { "offseason_rink", 24000 },
            { "shipyard", 30000 },
            { "subway_depot", 34000 },
        };

        public static IEnumerable<string> Parks => s_targets.Keys;
        public static int ParkCount => s_targets.Count;

        public static bool HasLine(string parkId) => parkId != null && s_targets.ContainsKey(parkId);

        public static long TargetFor(string parkId) => parkId != null && s_targets.TryGetValue(parkId, out var t) ? t : 0;

        /// <summary>
        /// The time of day a session really uses. Lessons, the tutorial, S.K.A.T.E. and replays always play in daylight
        /// (they're about seeing the skater clearly), Retro City runs its own clock, and anything else uses the choice.
        /// </summary>
        public static ParkTime Effective(ParkTime chosen, bool lessonOrTutorial, bool duelOrReplay, bool retroCity) =>
            lessonOrTutorial || duelOrReplay || retroCity ? ParkTime.Day : FromSaved((int)chosen);

        /// <summary>True when a banked combo clears this park's night line for the first time.</summary>
        public static bool Clears(string parkId, ParkTime time, long points, ICollection<string> done) =>
            time == ParkTime.Night && HasLine(parkId) && points >= TargetFor(parkId) && (done == null || !done.Contains(parkId));

        /// <summary>
        /// A darker, slower, softer take on a park's song for night sessions: same seed and chords, 90% tempo, less
        /// drive and lead, longer pads, a little more swing, and bright modes turned dorian.
        /// </summary>
        public static SongSpec NightMix(SongSpec day)
        {
            if (day == null) throw new ArgumentNullException(nameof(day));
            return new SongSpec
            {
                Name = day.Name + "_night",
                Bpm = (float)Math.Round(day.Bpm * 0.9f, 1),
                RootMidi = day.RootMidi,
                Scale = day.Scale == MusicScale.Major || day.Scale == MusicScale.Lydian ? MusicScale.Dorian : day.Scale,
                Progression = (int[])day.Progression.Clone(),
                Swing = Math.Min(0.3f, day.Swing + 0.06f),
                Drive = day.Drive * 0.75f,
                Lead = day.Lead * 0.7f,
                Pad = Math.Min(1f, day.Pad + 0.3f),
                Seed = day.Seed + 7,
            };
        }

        /// <summary>Stickers earned with night lines: (sticker id, night lines needed).</summary>
        public static int LinesFor(int stickerId) =>
            stickerId == 13 ? 1 : stickerId == 14 ? 5 : stickerId == 15 ? ParkCount : 0;
    }
}
