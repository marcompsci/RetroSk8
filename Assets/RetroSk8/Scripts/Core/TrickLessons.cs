using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>One short lesson for an advanced trick group (Phase 17). Played at Moonlight Pier, started from the Trick Book.</summary>
    public sealed class TrickLesson
    {
        public string Id;
        public string Title;
        /// <summary>Landing any of these in a banked line counts.</summary>
        public string[] TrickIds = new string[0];
        /// <summary>Any special (the meter is filled at the start and after every landed special).</summary>
        public bool AnySpecial;
        public int Count = 3;
        public string ParkId = "moonlight_pier";
        public float X, Y, Z;
        /// <summary>Heading as a yaw in degrees (0 = +z).</summary>
        public float Yaw;
        /// <summary>Coaching lines, shown one after another as you make progress.</summary>
        public string[] Steps = new string[0];
        public int Tokens = 15;
    }

    /// <summary>Saved progress through the lessons.</summary>
    [Serializable]
    public sealed class LessonState
    {
        public List<string> done = new List<string>();
        public void Sanitize() { if (done == null) done = new List<string>(); done.RemoveAll(string.IsNullOrEmpty); }
        public bool IsDone(string id) => done.Contains(id);
    }

    /// <summary>
    /// Tutorial 2.0 (Phase 17): a lesson for each group of advanced tricks, set up at the right spot of Moonlight Pier
    /// (bonks, Phase 18: the Twin Screen Drive-In)
    /// (the pier-end quarter for lips and reverts, the bait shack for walls, the fishing ledge for bluntslides, the
    /// funbox for spins and specials, the long pier deck for manuals and pops). Land the trick in banked lines a few
    /// times to pass; the first pass pays Tape Tokens. Engine-free and unit-tested.
    /// </summary>
    public static class TrickLessons
    {
        public static readonly TrickLesson[] All =
        {
            new TrickLesson
            {
                Id = "lips", Title = "LIP TRICKS", TrickIds = new[] { "lip_0", "lip_1", "lip_2", "lip_3", "lip_4" },
                X = 0f, Y = 0.05f, Z = 45.5f, Yaw = 0f,
                Steps = new[]
                {
                    "RIDE STRAIGHT AT THE QUARTER PIPE AT THE END OF THE PIER.",
                    "NEAR THE TOP, PRESS ACTION TO STALL ON THE COPING. TILT THE STICK FOR A DIFFERENT STALL.",
                    "ACTION OR JUMP DROPS BACK IN. LAND IT TO BANK THE LINE.",
                },
            },
            new TrickLesson
            {
                Id = "reverts", Title = "REVERTS", TrickIds = new[] { RevertRules.Id },
                X = 0f, Y = 0.05f, Z = 45.5f, Yaw = 0f,
                Steps = new[]
                {
                    "GET AIR OFF THE QUARTER PIPE.",
                    "THE MOMENT YOU LAND, SWIPE LEFT OR RIGHT TO SPIN BACK AROUND.",
                    "A REVERT KEEPS THE LINE GOING: ADD A MANUAL, THEN BANK IT.",
                },
            },
            new TrickLesson
            {
                Id = "walls", Title = "WALLRIDES + WALLPLANTS", TrickIds = new[] { "wallride", "wallplant", "wallie", StyleTricks.FootplantId },
                X = -0.6f, Y = 0.05f, Z = 27f, Yaw = 0f,
                Steps = new[]
                {
                    "THE BAIT SHACK IS ON YOUR LEFT. OLLIE TOWARD ITS WALL.",
                    "PRESS ACTION AT THE WALL: SKIM IT TO WALLRIDE, HIT IT HEAD-ON TO PLANT.",
                    "JUMP DURING A WALLRIDE FOR A WALLIE. LAND CLEAN TO BANK.",
                },
            },
            new TrickLesson
            {
                Id = "blunts", Title = "BLUNTSLIDES", TrickIds = new[] { StyleTricks.Bluntslide.Id },
                X = 2.4f, Y = 0.05f, Z = 27f, Yaw = 0f,
                Steps = new[]
                {
                    "THE FISHING LEDGE IS JUST AHEAD ON YOUR RIGHT.",
                    "HOLD THE STICK DOWN AND PRESS ACTION AS YOU REACH IT.",
                    "BALANCE WITH LEFT AND RIGHT, THEN POP OFF AND LAND.",
                },
            },
            new TrickLesson
            {
                Id = "manuals", Title = "MANUAL STYLES", TrickIds = new[] { StyleTricks.OneFootManual.Id, StyleTricks.Casper.Id },
                X = -3.5f, Y = 0.05f, Z = -8f, Yaw = 0f,
                Steps = new[]
                {
                    "OLLIE, AND AS YOU LAND PRESS ACTION WITH THE STICK LEFT: ONE-FOOT MANUAL.",
                    "STICK RIGHT INSTEAD FOR A CASPER.",
                    "KEEP YOUR BALANCE, THEN BANK THE LINE.",
                },
            },
            new TrickLesson
            {
                Id = "pops", Title = "NO-COMPLY + BONELESS", TrickIds = new[] { StyleTricks.NoComply.Id, StyleTricks.Boneless.Id },
                X = -3.5f, Y = 0.05f, Z = -8f, Yaw = 0f,
                Steps = new[]
                {
                    "HOLD THE STICK DOWN AND TAP JUMP: NO-COMPLY.",
                    "HOLD THE STICK DOWN AND CHARGE THE JUMP LONGER: BONELESS.",
                    "LAND IT AND BANK THE LINE.",
                },
            },
            new TrickLesson
            {
                Id = "spins", Title = "BIG SPINS", TrickIds = new[] { "spin_360", "spin_540", "spin_720", "spin_900", "spin_1080" },
                ParkId = "moonlight_pier", X = 0f, Y = 0.05f, Z = -36f, Yaw = 0f,
                Steps = new[]
                {
                    "RIDE UP THE FUNBOX AND JUMP OFF THE TOP.",
                    "HOLD LEFT OR RIGHT IN THE AIR. LET GO WHEN YOU'RE STRAIGHT AGAIN.",
                    "A 360 OR MORE COUNTS. LAND IT CLEAN.",
                },
            },
            new TrickLesson
            {
                Id = "specials", Title = "SPECIALS", AnySpecial = true,
                X = 0f, Y = 0.05f, Z = -36f, Yaw = 0f,
                Steps = new[]
                {
                    "YOUR SPECIAL METER IS FULL FOR THIS LESSON.",
                    "JUMP OFF THE FUNBOX AND SWIPE IN THE AIR: UP FOR YOUR SIGNATURE, DOWN OR SIDEWAYS FOR THE OTHERS.",
                    "LAND IT TO BANK. THE METER REFILLS AFTER EACH ONE.",
                },
            },
            new TrickLesson
            {
                Id = "bonks", Title = "BONKS + POLE JAMS", TrickIds = new[] { BonkRules.BonkId, BonkRules.PoleJamId },
                ParkId = "drive_in", X = -21f, Y = 0.05f, Z = -6f, Yaw = 0f,
                Steps = new[]
                {
                    "THE SPEAKER POST AHEAD LEANS AWAY FROM YOU. ROLL INTO IT FAST: POLE JAM.",
                    "OR OLLIE INTO A CONE, HYDRANT OR CAR BUMPER: BONK. YOU TAP OFF IT INSTEAD OF BAILING.",
                    "LAND IT AND BANK THE LINE.",
                },
            },
            new TrickLesson
            {
                // Phase 22: at the Off-Season Rink, rolling alongside the west boards.
                Id = "boards", Title = "BOARD GRINDS", TrickIds = new[] { "grind_center_glide", "grind_plank_slide", "grind_crossbar", "grind_nose_needle" },
                ParkId = "offseason_rink", X = -13.8f, Y = 0.05f, Z = -27f, Yaw = 0f,
                Steps = new[]
                {
                    "THE RINK BOARDS ARE ON YOUR LEFT. ROLL ALONGSIDE THEM AND OLLIE TOWARD THE TOP EDGE.",
                    "PRESS ACTION AS YOU REACH IT TO LOCK INTO A GRIND. TILT THE STICK FOR A DIFFERENT ONE.",
                    "KEEP YOUR BALANCE TO THE END OF THE BOARDS, OR JUMP OFF, THEN LAND TO BANK IT.",
                },
            },
        };

        public static TrickLesson Find(string id)
        {
            foreach (var l in All) if (l.Id == id) return l;
            return null;
        }

        /// <summary>The lesson that teaches a trick (by id; specials by flag), or null.</summary>
        public static TrickLesson For(string trickId, bool isSpecial)
        {
            if (string.IsNullOrEmpty(trickId)) return null;
            foreach (var l in All)
            {
                if (l.AnySpecial && isSpecial) return l;
                if (Array.IndexOf(l.TrickIds, trickId) >= 0) return l;
            }
            return null;
        }

        /// <summary>How many times a banked line counts toward a lesson (each matching trick once per line).</summary>
        public static int Matches(TrickLesson lesson, IList<string> lineIds, Func<string, bool> isSpecial)
        {
            if (lesson == null || lineIds == null) return 0;
            int n = 0;
            foreach (var id in lineIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (Array.IndexOf(lesson.TrickIds, id) >= 0 || (lesson.AnySpecial && isSpecial != null && isSpecial(id))) n++;
            }
            return Math.Min(n, 1); // one line = one rep, so a lesson takes a few separate lines
        }

        /// <summary>Which coaching line to show for progress <paramref name="reps"/> of <paramref name="lesson"/>.</summary>
        public static int StepFor(TrickLesson lesson, int reps)
        {
            if (lesson == null || lesson.Steps.Length == 0) return 0;
            return Math.Min(lesson.Steps.Length - 1, reps);
        }

        /// <summary>Pays the first pass only. Returns tokens to add.</summary>
        public static int Complete(LessonState state, TrickLesson lesson)
        {
            if (state == null || lesson == null || state.IsDone(lesson.Id)) return 0;
            state.done.Add(lesson.Id);
            return lesson.Tokens;
        }
    }
}
