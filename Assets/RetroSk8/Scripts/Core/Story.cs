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
        /// <summary>Phase 23: a cosmetic this step unlocks (its display name; the item's rewardStep is this step's id).</summary>
        public string RewardItem;
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
    /// Phase 26 adds chapter 10, "All City", the finale (four steps back across the city) and an epilogue.
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
                        Rival = "REEL", Tokens = 110, RewardItem = "Projectionist Tee",
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
            new StoryChapter
            {
                Number = 8, Id = "off_season", Title = "OFF-SEASON",
                Steps = new[]
                {
                    new StoryStep
                    {
                        Id = "s8_open", Title = "Open Skate", Objective = StoryObjective.ScoreRun, LocationId = "offseason_rink", Target = 30000,
                        Rival = "FROST", Tokens = 80,
                        Intro = new[]
                        {
                            N("Late summer. The town rink has melted its ice for the season, and somebody left the side door open."),
                            C("PILAR", "Bare concrete, boards all the way round, and bleachers. Somebody's been waxing those boards."),
                            R("FROST", "That'd be us. The Rink Rats. We sweep this place, and after hours we skate it."),
                            R("FROST", "Open skate's tonight. Put up a real score or go back to the parking lot."),
                            Y("Doors are open. I'm in."),
                        },
                        Outro = new[]
                        {
                            R("FROST", "Not bad for a first lap. Slapshot thinks it was luck."),
                            R("SLAPSHOT", "Tomorrow. My line against yours, board to board."),
                        },
                    },
                    new StoryStep
                    {
                        Id = "s8_slapshot", Title = "Board to Board", Objective = StoryObjective.LineBattle, LocationId = "offseason_rink", Target = 40000,
                        Rival = "SLAPSHOT", Tokens = 110,
                        Intro = new[]
                        {
                            N("The arena lights buzz on, one row at a time."),
                            R("SLAPSHOT", "Grind the boards, hit the Boards Hop, drop the bleachers. Whoever has the better line keeps the rink."),
                            C("MARQUEE", "We closed the drive-in early to watch this. Make it worth it."),
                        },
                        Outro = new[]
                        {
                            R("SLAPSHOT", "...Fine. Your line was cleaner."),
                            R("FROST", "Then it's me. Letters, centre ice, no do-overs."),
                        },
                    },
                    new StoryStep
                    {
                        Id = "s8_frost", Title = "Centre Ice", Objective = StoryObjective.Skate, LocationId = "offseason_rink", Rival = "FROST", RivalLevel = 2, Tokens = 130,
                        RewardItem = "Rink Rats Jersey",
                        Intro = new[]
                        {
                            R("FROST", "S.K.A.T.E. I set, you match. When the ice comes back in the fall, this place is ours again."),
                            Y("Then let's make the most of summer."),
                        },
                        Outro = new[]
                        {
                            N("The Rink Rats hand over a key on a frayed lanyard."),
                            R("FROST", "Side door. Any night before the ice goes back in. Bring the whole crew."),
                            C("PILAR", "Every crew in town skates with us now."),
                            N("OFF-SEASON: THE END."),
                        },
                    },
                },
            },
            new StoryChapter
            {
                Number = 9, Id = "dry_dock", Title = "DRY DOCK",
                Steps = new[]
                {
                    new StoryStep
                    {
                        Id = "s9_canyon", Title = "Shift Change", Objective = StoryObjective.ScoreRun, LocationId = "shipyard", Target = 34000,
                        Rival = "RIVET", Tokens = 90,
                        Intro = new[]
                        {
                            N("Dawn at the shipyard. The cranes are still, and the containers are stacked two high with a gap you could drive a forklift through."),
                            C("FROST", "The Deckhands run this yard before the morning shift clocks in. I told them about you."),
                            R("RIVET", "Container Canyon, the quay, the gangway. Show us a real score before the whistle blows."),
                            Y("Clock me in."),
                        },
                        Outro = new[]
                        {
                            R("RIVET", "Huh. You found lines we never saw."),
                            R("RIVET", "Tomorrow, line for line. And you bonk the bollards like everyone else."),
                        },
                    },
                    new StoryStep
                    {
                        Id = "s9_rivet", Title = "Bollard Run", Objective = StoryObjective.LineBattle, LocationId = "shipyard", Target = 45000, Bonks = 4,
                        Rival = "RIVET", Tokens = 120,
                        Intro = new[]
                        {
                            N("Fog off the water. The quay lights are still on."),
                            R("RIVET", "Four bonks or pole jams in your lines, and more points than mine. The crane hook counts."),
                            C("PILAR", "Every crew we've met is watching from the containers."),
                        },
                        Outro = new[]
                        {
                            R("RIVET", "Okay. That was the cleanest Bollard Run I've seen."),
                            R("ANCHOR", "Then it's my turn. Letters, on the quay. The loser sweeps the yard."),
                        },
                    },
                    new StoryStep
                    {
                        Id = "s9_anchor", Title = "On the Quay", Objective = StoryObjective.Skate, LocationId = "shipyard", Rival = "ANCHOR", RivalLevel = 2, Tokens = 140,
                        RewardItem = "Deckhand Jacket",
                        Intro = new[]
                        {
                            R("ANCHOR", "I've skated this quay since before the cranes were painted. S.K.A.T.E., no take-backs."),
                            Y("Then you know where all the good spots are. Let's go."),
                        },
                        Outro = new[]
                        {
                            N("The morning whistle blows. Nobody moves for a second."),
                            R("ANCHOR", "Grab a broom. ...Kidding. Here: every Deckhand gets a jacket."),
                            C("PILAR", "Harbor Plaza to the docks. Every crew in the city."),
                            Y("Same time tomorrow?"),
                            N("DRY DOCK: THE END."),
                        },
                    },
                },
            },
            // Phase 26: the finale. The Originals, the crew who built the city's first spots, take you back across it.
            new StoryChapter
            {
                Number = 10, Id = "all_city", Title = "ALL CITY",
                Steps = new[]
                {
                    new StoryStep
                    {
                        Id = "s10_rewind", Title = "Where It Started", Objective = StoryObjective.ScoreRun, LocationId = "harbor_plaza", Target = 30000,
                        Rival = "REWIND", Tokens = 100,
                        Intro = new[]
                        {
                            N("A taped-up flyer on every crew's spot: ALL CITY. ONE NIGHT. THE ORIGINALS ARE BACK."),
                            C("PILAR", "The Originals built the first ledges in this city. Before the Gloss. Before any of us."),
                            R("REWIND", "So you're the one everybody's talking about. Show me the harbor the way you skate it now."),
                            Y("Same plaza where I started. Different skater."),
                        },
                        Outro = new[]
                        {
                            R("REWIND", "Thirty thousand at the harbor. When I was your age we called that a rumor."),
                            R("STATIC", "Rumors are easy. Meet me on the rooftops. Line for line."),
                        },
                    },
                    new StoryStep
                    {
                        Id = "s10_static", Title = "Across Town", Objective = StoryObjective.LineBattle, LocationId = "rooftop_run", Target = 46000,
                        Rival = "STATIC", Tokens = 130,
                        Intro = new[]
                        {
                            N("Midnight. The city lights stretch all the way to the docks."),
                            R("STATIC", "Juno filmed you up here once. I watched that tape a hundred times."),
                            R("STATIC", "Out-skate me and I'll tell you where the very first spot was."),
                        },
                        Outro = new[]
                        {
                            R("STATIC", "Fine. You earned it. The first spot was the dam."),
                            C("DEX", "Floodgate Ditch? Val Sterling's still out there every night."),
                        },
                    },
                    new StoryStep
                    {
                        Id = "s10_val", Title = "One More Take", Objective = StoryObjective.LineBattle, LocationId = "floodgate_ditch", Target = 52000, Bonks = 5,
                        Rival = "VAL STERLING", Tokens = 150,
                        Intro = new[]
                        {
                            R("VAL STERLING", "No cameras. No sponsors. Just a guy who misses skating with people."),
                            R("VAL STERLING", "One more take. Five bonks, the whole ditch, and you still have to beat me."),
                            Y("Let's make it a good one."),
                        },
                        Outro = new[]
                        {
                            R("VAL STERLING", "...That's the line I always wanted to film."),
                            R("REWIND", "The Gloss, the Originals and you, at the same spot. Never thought I'd see it."),
                            R("REWIND", "One game left. Letters, back where you started. Winner keeps the city."),
                        },
                    },
                    new StoryStep
                    {
                        Id = "s10_rewind_skate", Title = "The Last Line", Objective = StoryObjective.Skate, LocationId = "harbor_plaza",
                        Rival = "REWIND", RivalLevel = 2, Tokens = 220, RewardItem = "All-City Hoodie",
                        Intro = new[]
                        {
                            N("Harbor Plaza. Every crew in Retro City is on the steps."),
                            R("REWIND", "I've been setting tricks since before your board was pressed. Don't hold back."),
                            C("PILAR", "Go get it. We're all right here."),
                        },
                        Outro = new[]
                        {
                            N("The last letter. The plaza goes quiet, then loud."),
                            R("REWIND", "Nobody keeps the city. You skate it, then you hand it on."),
                            R("REWIND", "Here. Every crew signed this hoodie tonight."),
                            C("PILAR", "Harbor, warehouse, rooftops, bowls, the dam, the drive-in, the rink, the docks."),
                            Y("And tomorrow we find a new spot."),
                        },
                    },
                },
            },
        };

        /// <summary>Phase 26: the last step of the story; clearing it plays <see cref="Epilogue"/> after its outro.</summary>
        public const string FinalStepId = "s10_rewind_skate";

        /// <summary>Phase 26: the ending, played once after the finale's closing panels (and on replays of it).</summary>
        public static readonly StoryPanel[] Epilogue =
        {
            N("Weeks later. A new flyer goes up at the harbor: OPEN SESSION. EVERY CREW WELCOME."),
            C("DEX", "Kids from all over town showed up. Some of them can't even ollie yet."),
            R("FROST", "The Rink Rats are teaching them. The Deckhands brought brooms."),
            R("MARQUEE", "We're projecting the session on the dam wall tonight."),
            R("SHEEN", "The Gloss are... helping. Mostly by carrying the boards."),
            C("PILAR", "Remember your first week? One board, no crew, no clue."),
            Y("Now look at it."),
            N("THE LAST SPOT: THE END. THANKS FOR SKATING."),
        };

        /// <summary>The panels to play when a step is cleared: its outro, plus the epilogue for the finale.</summary>
        public static StoryPanel[] ClearPanels(StoryStep step)
        {
            if (step == null) return new StoryPanel[0];
            var outro = step.Outro ?? new StoryPanel[0];
            if (step.Id != FinalStepId) return outro;
            var all = new StoryPanel[outro.Length + Epilogue.Length];
            Array.Copy(outro, all, outro.Length);
            Array.Copy(Epilogue, 0, all, outro.Length, Epilogue.Length);
            return all;
        }

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

        /// <summary>
        /// Phase 21: what to tell the player after a Two-Minute Run that didn't clear the step, or null. Runs at other
        /// parks say nothing (they used to show a RETRY message wherever you skated).
        /// </summary>
        public static string RetryMessage(StoryStep step, string locationId, long score, int bonks)
        {
            if (step == null || step.Objective == StoryObjective.Skate || step.LocationId != locationId) return null;
            if (RunClears(step, locationId, score, bonks)) return null;
            if (step.Bonks > 0 && score > step.Target) return $"STORY: {bonks}/{step.Bonks} BONKS. HIT MORE STUFF AND RETRY!";
            return step.Objective == StoryObjective.LineBattle
                ? $"STORY: {step.Rival} STILL HAS THE BETTER LINE. RETRY!"
                : $"STORY: NEED MORE THAN {step.Target:N0}. RETRY!";
        }
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
