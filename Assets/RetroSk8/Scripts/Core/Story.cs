using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>What a story step asks you to do.</summary>
    public enum StoryObjective
    {
        /// <summary>Beat a score in a Two-Minute Run.</summary>
        ScoreRun = 0,
        /// <summary>Out-score a rival whose score climbs as they "skate" alongside you (shown live in the HUD).</summary>
        LineBattle = 1,
        /// <summary>Win a game of S.K.A.T.E. against the rival.</summary>
        Skate = 2,
    }

    /// <summary>Who's talking in a comic panel (sets the panel's colours).</summary>
    public enum StoryMood { Narration = 0, Crew = 1, Rival = 2, You = 3 }

    public sealed class StoryPanel
    {
        public string Speaker;
        public string Line;
        public StoryMood Mood;
    }

    public sealed class StoryStep
    {
        public string Id;
        public string Title;
        public StoryObjective Objective;
        public string LocationId;
        public long Target;
        public string Rival;
        /// <summary>S.K.A.T.E. skill (0 easy, 1 medium, 2 hard).</summary>
        public int RivalLevel;
        public int Tokens = 40;
        /// <summary>Phase 20: the run also needs this many banked bonks or pole jams (0 = none needed).</summary>
        public int Bonks;
        public StoryPanel[] Intro;
        public StoryPanel[] Outro;

        public string ObjectiveText(string parkName) =>
            Objective == StoryObjective.Skate ? $"Beat {Rival} at S.K.A.T.E."
            : (Objective == StoryObjective.LineBattle ? $"Out-skate {Rival}: more than {Target:N0} in a Two-Minute Run at {parkName}"
            : $"Score more than {Target:N0} in a Two-Minute Run at {parkName}")
              + (Bonks > 0 ? $", with {Bonks} bonks or pole jams" : "");
    }

    public sealed class StoryChapter
    {
        public int Number;
        public string Id;
        public string Title;
        public StoryStep[] Steps;
    }

    /// <summary>Saved story progress (JsonUtility-friendly).</summary>
    [Serializable]
    public sealed class StoryState
    {
        public List<string> cleared = new List<string>();

        public bool IsCleared(string stepId) => cleared.Contains(stepId);

        /// <summary>Marks a step cleared. True the first time (pay the reward once).</summary>
        public bool Clear(string stepId)
        {
            if (string.IsNullOrEmpty(stepId) || cleared.Contains(stepId) || Story.FindStep(stepId) == null) return false;
            cleared.Add(stepId);
            return true;
        }

        public void Sanitize()
        {
            if (cleared == null) cleared = new List<string>();
            cleared.RemoveAll(id => Story.FindStep(id) == null);
        }

        public int ChaptersDone()
        {
            int n = 0;
            foreach (var c in Story.Chapters)
            {
                bool all = true;
                foreach (var s in c.Steps) if (!IsCleared(s.Id)) { all = false; break; }
                if (all) n++;
            }
            return n;
        }

        public bool Finished => ChaptersDone() == Story.Chapters.Length;
    }

    /// <summary>
    /// "The Last Spot": Retro Sk8's original story.
    /// Phase 20 adds chapter 7, "Double Feature": an encore at the Twin Screen Drive-In against a new crew, with bonks. You arrive in Retro City, find a crew, and stop The Gloss (a
    /// slick, sponsored rival crew) from booking the city's spots as private film sets, ending with a showdown at
    /// Floodgate Ditch. Six chapters, two steps each; every step is played in order. All characters are fictional.
    /// Engine-free so it is unit-tested.
    /// </summary>
    public static class Story
    {
        public const string Title = "THE LAST SPOT";
        public const string RivalCrew = "THE GLOSS";

        private static StoryPanel N(string line) => new StoryPanel { Speaker = "", Line = line, Mood = StoryMood.Narration };
        private static StoryPanel C(string who, string line) => new StoryPanel { Speaker = who, Line = line, Mood = StoryMood.Crew };
        private static StoryPanel R(string who, string line) => new StoryPanel { Speaker = who, Line = line, Mood = StoryMood.Rival };
        private static StoryPanel Y(string line) => new StoryPanel { Speaker = "YOU", Line = line, Mood = StoryMood.You };

        public static readonly StoryChapter[] Chapters =
        {
            new StoryChapter
            {
                Number = 1, Id = "new_in_town", Title = "NEW IN TOWN",
                Steps = new[]
                {
                    new StoryStep
                    {
                        Id = "s1_pilar", Title = "Prove It", Objective = StoryObjective.ScoreRun, LocationId = "harbor_plaza", Target = 8000, Rival = "PILAR",
                        Intro = new[]
                        {
                            N("Retro City. First week in town. One board, no crew, no clue where to skate."),
                            C("PILAR", "New face at the harbor? Everybody says they can skate."),
                            C("PILAR", "Two minutes. Eight thousand. Then we'll talk."),
                        },
                        Outro = new[]
                        {
                            C("PILAR", "Okay. Okay! You can actually skate."),
                            C("PILAR", "Come back tomorrow. I want you to meet Dex."),
                        },
                    },
                    new StoryStep
                    {
                        Id = "s1_dex", Title = "Flatground Test", Objective = StoryObjective.Skate, LocationId = "harbor_plaza", Rival = "DEX", RivalLevel = 0,
                        Intro = new[]
                        {
                            C("DEX", "Pilar says you're good. Flatground says otherwise."),
                            C("DEX", "Game of S.K.A.T.E. Loser carries the speaker."),
                        },
                        Outro = new[]
                        {
                            C("DEX", "Fine. FINE. You're in."),
                            N("You've got a crew."),
                        },
                    },
                },
            },
            new StoryChapter
            {
                Number = 2, Id = "the_gloss", Title = "THE GLOSS",
                Steps = new[]
                {
                    new StoryStep
                    {
                        Id = "s2_sheen", Title = "Plaza Booked", Objective = StoryObjective.LineBattle, LocationId = "harbor_plaza", Target = 14000, Rival = "SHEEN",
                        Intro = new[]
                        {
                            N("Next morning the plaza is taped off. Lights. Cameras. Matching jackets."),
                            R("SHEEN", "The Gloss has this plaza booked for a shoot. Read the sign."),
                            Y("Spots aren't for booking."),
                            R("SHEEN", "Then out-skate me. Two minutes. My line against yours."),
                        },
                        Outro = new[]
                        {
                            R("SHEEN", "...Lucky lines. Val won't like this."),
                            C("PILAR", "Who's Val?"),
                            N("Nobody answers. The Gloss packs up their lights and leaves."),
                        },
                    },
                    new StoryStep
                    {
                        Id = "s2_home", Title = "A Home Spot", Objective = StoryObjective.ScoreRun, LocationId = "neon_warehouse", Target = 12000, Rival = "DEX",
                        Intro = new[]
                        {
                            C("PILAR", "They'll be back. We need a spot they can't book."),
                            C("DEX", "I know a warehouse. The owner lets us skate after hours if we put on a show."),
                        },
                        Outro = new[]
                        {
                            C("DEX", "The owner's hooked. The warehouse is ours after dark."),
                        },
                    },
                },
            },
            new StoryChapter
            {
                Number = 3, Id = "under_the_neon", Title = "UNDER THE NEON",
                Steps = new[]
                {
                    new StoryStep
                    {
                        Id = "s3_glint", Title = "Friday Night", Objective = StoryObjective.LineBattle, LocationId = "neon_warehouse", Target = 18000, Rival = "GLINT",
                        Intro = new[]
                        {
                            R("GLINT", "Cute hideout. The Gloss films here Friday."),
                            R("GLINT", "Beat my line or clear out."),
                        },
                        Outro = new[]
                        {
                            R("GLINT", "Keep your warehouse. For now."),
                        },
                    },
                    new StoryStep
                    {
                        Id = "s3_glint_skate", Title = "Letters", Objective = StoryObjective.Skate, LocationId = "neon_warehouse", Rival = "GLINT", RivalLevel = 1,
                        Intro = new[]
                        {
                            R("GLINT", "Lines are easy with music on. Flat. Letters. Right now."),
                        },
                        Outro = new[]
                        {
                            R("GLINT", "Whatever. Val's got bigger plans than one warehouse."),
                            C("DEX", "Bigger plans?"),
                        },
                    },
                },
            },
            new StoryChapter
            {
                Number = 4, Id = "rooftop_rumor", Title = "ROOFTOP RUMOR",
                Steps = new[]
                {
                    new StoryStep
                    {
                        Id = "s4_juno", Title = "On Tape", Objective = StoryObjective.ScoreRun, LocationId = "rooftop_run", Target = 16000, Rival = "JUNO",
                        Intro = new[]
                        {
                            C("JUNO", "I film from up here. Last night I caught The Gloss measuring the ditch by the dam."),
                            C("JUNO", "Give me a run worth filming and I'll show you the tape."),
                        },
                        Outro = new[]
                        {
                            C("JUNO", "That's a clip. And here's mine: they're fencing off Floodgate Ditch."),
                            N("The city's best ditch. Turned into a private set."),
                        },
                    },
                    new StoryStep
                    {
                        Id = "s4_lustre", Title = "Delete It", Objective = StoryObjective.LineBattle, LocationId = "rooftop_run", Target = 22000, Rival = "LUSTRE",
                        Intro = new[]
                        {
                            R("LUSTRE", "That footage doesn't leave this roof."),
                            R("LUSTRE", "Beat me and keep it. Lose and it's deleted."),
                        },
                        Outro = new[]
                        {
                            R("LUSTRE", "Keep the tape. You'll need more than footage to stop Val."),
                        },
                    },
                },
            },
            new StoryChapter
            {
                Number = 5, Id = "sunset_showdown", Title = "SUNSET SHOWDOWN",
                Steps = new[]
                {
                    new StoryStep
                    {
                        Id = "s5_rook", Title = "The Old Guard", Objective = StoryObjective.ScoreRun, LocationId = "sunset_bowls", Target = 20000, Rival = "ROOK",
                        Intro = new[]
                        {
                            C("ROOK", "Fences on a ditch? We fought that fight in my day."),
                            C("ROOK", "Carve my bowls proper and the whole bowl crew stands with you."),
                        },
                        Outro = new[]
                        {
                            C("ROOK", "That's how it's done. We'll be at the ditch."),
                        },
                    },
                    new StoryStep
                    {
                        Id = "s5_lustre_skate", Title = "Winner Calls It", Objective = StoryObjective.Skate, LocationId = "sunset_bowls", Rival = "LUSTRE", RivalLevel = 1,
                        Intro = new[]
                        {
                            R("LUSTRE", "One more game. If I win, you stay away from the ditch."),
                        },
                        Outro = new[]
                        {
                            R("LUSTRE", "...Fine. Val will be waiting at the dam. Good luck. You'll need it."),
                        },
                    },
                },
            },
            new StoryChapter
            {
                Number = 6, Id = "the_last_spot", Title = "THE LAST SPOT",
                Steps = new[]
                {
                    new StoryStep
                    {
                        Id = "s6_val", Title = "The Dam", Objective = StoryObjective.LineBattle, LocationId = "floodgate_ditch", Target = 30000, Rival = "VAL STERLING", Tokens = 60,
                        Intro = new[]
                        {
                            N("Floodgate Ditch. The fences are half up. Your whole crew is here, and so is The Gloss."),
                            R("VAL STERLING", "This ditch is about to be a private set. Nobody skates it without a permit."),
                            Y("Then let's skate for it."),
                            R("VAL STERLING", "Two minutes. Beat my line and I'll leave it open."),
                        },
                        Outro = new[]
                        {
                            R("VAL STERLING", "...Again. Flatground. No cameras."),
                        },
                    },
                    new StoryStep
                    {
                        Id = "s6_val_skate", Title = "No Cameras", Objective = StoryObjective.Skate, LocationId = "floodgate_ditch", Rival = "VAL STERLING", RivalLevel = 2, Tokens = 100,
                        Intro = new[]
                        {
                            R("VAL STERLING", "Letters. Winner keeps the ditch."),
                        },
                        Outro = new[]
                        {
                            N("The fences come down that night."),
                            C("PILAR", "Spots belong to whoever skates them."),
                            R("VAL STERLING", "Next time we skate it's just for fun. Deal?"),
                            Y("Deal."),
                            N("THE END. The city's still yours to skate."),
                        },
                    },
                },
            },
            new StoryChapter
            {
                Number = 7, Id = "double_feature", Title = "DOUBLE FEATURE",
                Steps = new[]
                {
                    new StoryStep
                    {
                        Id = "s7_marquee", Title = "Late Show", Objective = StoryObjective.LineBattle, LocationId = "drive_in", Target = 22000, Bonks = 3,
                        Rival = "MARQUEE", Tokens = 70,
                        Intro = new[]
                        {
                            N("A month later. The old drive-in on the edge of town reopens for one last summer of late shows."),
                            C("PILAR", "Somebody's been skating the lot after the movies. Speaker posts, car bumpers, the lot."),
                            R("MARQUEE", "That somebody is us. The Projectionists. This lot's our stage."),
                            R("MARQUEE", "Here, you don't just land tricks. You hit everything. Three bonks in a line or it doesn't count."),
                            Y("Roll the film."),
                        },
                        Outro = new[]
                        {
                            R("MARQUEE", "Okay. You can bonk. But can you do it when it matters?"),
                            R("REEL", "Double feature. Tomorrow night, before the second movie."),
                        },
                    },
                    new StoryStep
                    {
                        Id = "s7_reel", Title = "Second Show", Objective = StoryObjective.LineBattle, LocationId = "drive_in", Target = 32000, Bonks = 6,
                        Rival = "REEL", Tokens = 110,
                        Intro = new[]
                        {
                            N("Intermission. Every car on the lot has its headlights pointed at the screens."),
                            R("REEL", "Six bonks. More points than my run. Clear the Car Hop if you want the crowd."),
                            C("VAL STERLING", "...I came to watch. Don't make me regret it."),
                        },
                        Outro = new[]
                        {
                            N("The second movie starts late. Nobody minds."),
                            R("MARQUEE", "Projectionists don't usually share the stage. For you, we'll make an exception."),
                            C("PILAR", "Every spot in this town, skated. What's next?"),
                            Y("Whatever's next."),
                            N("DOUBLE FEATURE: THE END."),
                        },
                    },
                },
            },
        };

        public static IEnumerable<StoryStep> AllSteps()
        {
            foreach (var c in Chapters)
                foreach (var s in c.Steps)
                    yield return s;
        }

        public static StoryStep FindStep(string id)
        {
            foreach (var s in AllSteps()) if (s.Id == id) return s;
            return null;
        }

        public static StoryChapter ChapterOf(string stepId)
        {
            foreach (var c in Chapters)
                foreach (var s in c.Steps)
                    if (s.Id == stepId) return c;
            return null;
        }

        /// <summary>The first step not yet cleared (steps play in order), or null when the story is finished.</summary>
        public static StoryStep NextStep(StoryState state)
        {
            foreach (var s in AllSteps()) if (state == null || !state.IsCleared(s.Id)) return s;
            return null;
        }

        /// <summary>A step is playable once every step before it is cleared (cleared steps can be replayed).</summary>
        public static bool IsUnlocked(StoryState state, string stepId)
        {
            foreach (var s in AllSteps())
            {
                if (s.Id == stepId) return true;
                if (state == null || !state.IsCleared(s.Id)) return false;
            }
            return false;
        }

        /// <summary>Whether a finished Two-Minute Run clears a score or line-battle step.</summary>
        public static bool RunClears(StoryStep step, string locationId, long score) => RunClears(step, locationId, score, int.MaxValue);

        /// <summary>Phase 20: the score and, for bonk steps, enough banked bonks or pole jams in the run.</summary>
        public static bool RunClears(StoryStep step, string locationId, long score, int bonks) =>
            step != null && step.Objective != StoryObjective.Skate && step.LocationId == locationId && score > step.Target
            && bonks >= step.Bonks;
    }

    /// <summary>
    /// A line-battle rival's run, as banked lines: a few warm-up lines, bigger ones mid-run and a final push,
    /// adding up to exactly <c>target</c> by the end of the run. Deterministic per seed.
    /// </summary>
    public static class RivalCurve
    {
        public static List<GhostBank> Banks(long target, float seconds, int seed)
        {
            var list = new List<GhostBank>();
            if (target <= 0 || seconds <= 0f) return list;
            int n = (int)Math.Max(4, Math.Min(14, target / 2500));
            var rng = new Random(seed);
            var weights = new double[n];
            double sum = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (i + 1.0) / n;
                weights[i] = 0.6 + t * 1.2 + rng.NextDouble() * 0.6; // lines grow as the rival warms up
                sum += weights[i];
            }
            long given = 0;
            float start = Math.Min(8f, seconds * 0.1f), end = Math.Max(start + 1f, seconds - 4f);
            for (int i = 0; i < n; i++)
            {
                long points = i == n - 1 ? target - given : (long)Math.Round(target * weights[i] / sum);
                points = Math.Max(1, Math.Min(points, target - given - (n - 1 - i)));
                given += points;
                float time = start + (end - start) * (i + (float)rng.NextDouble() * 0.6f) / n;
                list.Add(new GhostBank(time, points));
            }
            list.Sort((a, b) => a.Time.CompareTo(b.Time));
            return list;
        }
    }
}
