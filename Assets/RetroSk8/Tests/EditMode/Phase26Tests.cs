using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class RaceGhostTests
    {
        /// <summary>A plausible run of a race: the skater rolls along x at 6 m/s, 20 samples a second.</summary>
        private static ReplayTrack Track(string raceId, float seconds)
        {
            var t = new ReplayTrack { LocationId = RaceSplits.GhostKey(raceId) };
            for (int i = 0; i <= (int)(seconds * 20f); i++)
            {
                float time = i / 20f;
                t.Add(new ReplayFrame
                {
                    Time = time,
                    Position = new RVec3(70f - time * 6f, 0.05f, 35f + (float)Math.Sin(time) * 2f),
                    Rotation = RQuat.Identity, Pose = RQuat.Identity, Body = RQuat.Identity, Board = RQuat.Identity,
                    BoardPosition = new RVec3(0f, 0.1f, 0f),
                });
            }
            return t;
        }

        private static RaceGhost Ghost()
        {
            var race = RetroCityLayout.FindRace("canal_cut");
            var g = new RaceGhost { RaceId = race.Id, From = "OMARI", Time = 15.37f, Track = Track(race.Id, 15.37f) };
            for (int i = 1; i < race.GateCount; i++) g.Splits.Add(i == race.GateCount - 1 ? 15.37f : 15.37f * i / (race.GateCount - 1));
            return g;
        }

        [Test]
        public void Delta_ShowsAheadAndBehind_InHundredths()
        {
            Assert.AreEqual("-0.42", RaceSplits.Delta(10f, 10.42f));
            Assert.AreEqual("+1.10", RaceSplits.Delta(12.1f, 11f));
            Assert.AreEqual("0.00", RaceSplits.Delta(5f, 5.001f));
            Assert.AreEqual("+12.05", RaceSplits.Delta(30f, 17.95f));
        }

        [Test]
        public void Reference_IsTheSplitForThatGate_OrNothing()
        {
            var splits = new List<float> { 5f, 10f, 15f };
            Assert.AreEqual(5f, RaceSplits.Reference(splits, 1), 0.0001f);
            Assert.AreEqual(15f, RaceSplits.Reference(splits, 3), 0.0001f);
            Assert.Less(RaceSplits.Reference(splits, 0), 0f);
            Assert.Less(RaceSplits.Reference(splits, 4), 0f);
            Assert.Less(RaceSplits.Reference(null, 1), 0f);
            Assert.Less(RaceSplits.Reference(new List<float> { 0f }, 1), 0f, "a zero split means none was kept");
        }

        [Test]
        public void Splits_MustMatchTheRace()
        {
            Assert.IsTrue(RaceSplits.AreValid(new List<float> { 4f, 9f, 12.5f }, 4, 12.5f));
            Assert.IsFalse(RaceSplits.AreValid(new List<float> { 4f, 9f }, 4, 9f), "one per gate after the start");
            Assert.IsFalse(RaceSplits.AreValid(new List<float> { 4f, 3f, 12.5f }, 4, 12.5f), "times only go up");
            Assert.IsFalse(RaceSplits.AreValid(new List<float> { 4f, 9f, 12f }, 4, 12.5f), "the last split is the finish");
            Assert.IsFalse(RaceSplits.AreValid(new List<float> { 4f, float.NaN, 12.5f }, 4, 12.5f));
            Assert.IsFalse(RaceSplits.AreValid(new List<float>(), 4, 0f), "no best yet");
            Assert.IsFalse(RaceSplits.AreValid(null, 4, 12.5f));
        }

        [Test]
        public void Code_RoundTrips()
        {
            var g = Ghost();
            string code = RaceGhostCodes.Encode(g);
            Assert.IsTrue(code.StartsWith(RaceGhostCodes.Prefix));
            Assert.IsTrue(RaceGhostCodes.IsRaceCode(code));
            Assert.IsFalse(GhostCodes.IsGhostCode(code), "not mistaken for a park ghost");
            Assert.Less(code.Length, 20000f, "a short race makes a short code");

            Assert.IsTrue(RaceGhostCodes.TryDecode(code, out var back, out var error), error);
            Assert.AreEqual("canal_cut", back.RaceId);
            Assert.AreEqual("OMARI", back.From);
            Assert.AreEqual(g.Time, back.Time, 0.006f);
            Assert.AreEqual(g.Splits.Count, back.Splits.Count);
            for (int i = 0; i < g.Splits.Count; i++) Assert.AreEqual(g.Splits[i], back.Splits[i], 0.006f, "split " + i);
            Assert.AreEqual(RaceSplits.GhostKey("canal_cut"), back.Track.LocationId);
            Assert.AreEqual(g.Track.Duration, back.Track.Duration, 0.11f);
            g.Track.Sample(7.3f, out var a);
            back.Track.Sample(7.3f, out var b);
            Assert.AreEqual(a.Position.X, b.Position.X, 0.02f);
            Assert.AreEqual(a.Position.Z, b.Position.Z, 0.02f);
        }

        [Test]
        public void Code_WrappedByAChatApp_StillDecodes()
        {
            string code = RaceGhostCodes.Encode(Ghost());
            string wrapped = string.Join("\n", Enumerable.Range(0, (code.Length + 79) / 80).Select(i => code.Substring(i * 80, Math.Min(80, code.Length - i * 80))));
            Assert.IsTrue(RaceGhostCodes.TryDecode("  " + wrapped + "\n", out _, out var error), error);
        }

        [Test]
        public void BadCodes_AreRefused_WithAReason()
        {
            string code = RaceGhostCodes.Encode(Ghost());
            Assert.IsFalse(RaceGhostCodes.TryDecode(code.Substring(0, code.Length / 2), out _, out var e1));
            Assert.IsTrue(e1.Contains("INCOMPLETE"), e1);
            char[] chars = code.ToCharArray();
            int mid = chars.Length / 2;
            chars[mid] = chars[mid] == 'A' ? 'B' : 'A';
            Assert.IsFalse(RaceGhostCodes.TryDecode(new string(chars), out _, out _), "one changed character fails the checksum");
            Assert.IsFalse(RaceGhostCodes.TryDecode("RG:" + code.Substring(3), out _, out _), "a park ghost prefix");
            Assert.IsFalse(RaceGhostCodes.TryDecode("", out _, out _));
            Assert.IsFalse(RaceGhostCodes.TryDecode(RaceGhostCodes.Prefix + new string('A', CodeLimits.MaxGhostCodeChars), out _, out var e2));
            Assert.IsTrue(e2.Contains("TOO LONG"), e2);
        }

        [Test]
        public void Encode_RefusesUnknownRacesAndBadSplits()
        {
            var g = Ghost();
            g.RaceId = "nowhere";
            Assert.Throws<ArgumentException>(() => RaceGhostCodes.Encode(g));
            g = Ghost();
            g.Splits.RemoveAt(0);
            Assert.Throws<ArgumentException>(() => RaceGhostCodes.Encode(g));
            g = Ghost();
            g.Track = null;
            Assert.Throws<ArgumentException>(() => RaceGhostCodes.Encode(g));
        }

        [Test]
        public void GhostFile_MustMatchTheSavedBest()
        {
            Assert.IsTrue(RaceSplits.TrackMatches(15.33f, 15.37f), "recorded at 20 Hz up to the finish");
            Assert.IsFalse(RaceSplits.TrackMatches(17.9f, 15.37f), "a ghost from another run (a restored save)");
            Assert.IsFalse(RaceSplits.TrackMatches(15.37f, 0f), "no best");
            Assert.IsFalse(RaceSplits.TrackMatches(0f, 15.37f), "an empty file");
        }

        [Test]
        public void EveryRace_FitsTheCode()
        {
            Assert.Less(RetroCityLayout.Races.Count, 64f, "6 bits of race index");
            foreach (var r in RetroCityLayout.Races)
            {
                Assert.Less(r.GateCount, 64f, r.Id + ": 6 bits of split count");
                Assert.Less(r.Bronze * 1.5f, 655f, r.Id + ": the time limit fits 16 bits of hundredths");
                Assert.Less(r.Bronze * 1.5f, GhostCodes.MaxSeconds, r.Id + ": the whole run fits a ghost");
                Assert.IsFalse(Array.IndexOf(ShareCodes.BuiltInParks, RaceSplits.GhostKey(r.Id)) >= 0, "ghost keys never collide with a park");
            }
        }

        [Test]
        public void BestRace_KeepsItsSplits_UntilBeaten()
        {
            var city = new CityProgress();
            Assert.AreEqual(0, city.RaceBestSplits("canal_cut").Count);
            city.RecordRace("canal_cut", 16f, Medal.Bronze, new List<float> { 4f, 8f, 12f, 16f });
            city.RecordRace("canal_cut", 18f, Medal.None, new List<float> { 5f, 9f, 13f, 18f });
            Assert.AreEqual(16f, city.RaceBest("canal_cut"), 0.0001f);
            Assert.AreEqual(4f, city.RaceBestSplits("canal_cut")[0], 0.0001f, "a slower run leaves the best splits alone");
            city.RecordRace("canal_cut", 15f, Medal.Silver, new List<float> { 3.5f, 7.5f, 11f, 15f });
            Assert.AreEqual(3.5f, city.RaceBestSplits("canal_cut")[0], 0.0001f);
            Assert.AreEqual(4, city.RaceBestSplits("canal_cut").Count);
            city.RecordRace("ring_road", 60f, Medal.Gold); // the old overload still works (no splits)
            Assert.AreEqual(0, city.RaceBestSplits("ring_road").Count);
        }

        [Test]
        public void ParkGhostCodes_StillRoundTrip_AfterSharingTheFramePacking()
        {
            var track = new ReplayTrack { LocationId = "harbor_plaza", Score = 12345 };
            for (int i = 0; i < 200; i++)
                track.Add(new ReplayFrame { Time = i / 20f, Position = new RVec3(i * 0.3f, 0f, -i * 0.1f), Rotation = RQuat.Identity, Pose = RQuat.Identity, Body = RQuat.Identity, Board = RQuat.Identity });
            var c = new ScoreChallenge { LocationId = "harbor_plaza", Target = 12345, From = "DEX", Ghost = track, GhostBanks = new List<GhostBank> { new GhostBank(3f, 500) } };
            string code = GhostCodes.Encode(c);
            Assert.IsTrue(GhostCodes.TryDecode(code, out var back, out var error), error);
            Assert.AreEqual(12345L, back.Target);
            Assert.AreEqual("DEX", back.From);
            Assert.AreEqual(1, back.GhostBanks.Count);
            Assert.AreEqual(track.Duration, back.Ghost.Duration, 0.11f);
        }
    }

    public class AssistTests
    {
        [Test]
        public void GameSpeed_Levels()
        {
            Assert.AreEqual(1f, Assists.SpeedScale(0), 0.0001f);
            Assert.AreEqual(0.9f, Assists.SpeedScale(1), 0.0001f);
            Assert.AreEqual(0.7f, Assists.SpeedScale(3), 0.0001f);
            Assert.AreEqual(0.7f, Assists.SpeedScale(99), 0.0001f, "clamped");
            Assert.AreEqual(1f, Assists.SpeedScale(-4), 0.0001f, "clamped");
            Assert.AreEqual("GAME SPEED: 80%", Assists.SpeedName(2));
        }

        [Test]
        public void Next_CyclesBackToOff()
        {
            Assert.AreEqual(1, Assists.Next(0, Assists.MaxBalance));
            Assert.AreEqual(2, Assists.Next(1, Assists.MaxBalance));
            Assert.AreEqual(0, Assists.Next(2, Assists.MaxBalance));
            Assert.AreEqual(0, Assists.Next(7, Assists.MaxBalance));
        }

        [Test]
        public void Levels_AnyAndClamp()
        {
            Assert.IsFalse(new AssistLevels().Any);
            Assert.IsTrue(new AssistLevels { Landing = 1 }.Any);
            var c = Assists.Clamp(new AssistLevels { Speed = 9, Balance = -1, Landing = 5 });
            Assert.AreEqual(Assists.MaxSpeed, c.Speed);
            Assert.AreEqual(0, c.Balance);
            Assert.AreEqual(Assists.MaxLanding, c.Landing);
        }

        /// <summary>Seconds until the meter fails with no correction and the noise always pushing the same way (or the cap).</summary>
        private static float SecondsToFall(BalanceSettings s, float input, float cap = 30f)
        {
            var m = new BalanceMeter(s);
            m.Begin(1);
            float dt = 1f / 60f, t = 0f;
            while (t < cap)
            {
                if (m.Step(dt, input, 1f)) return t;
                t += dt;
            }
            return cap;
        }

        [Test]
        public void Balance_SteadyLastsLonger_AndAutoHoldsItself()
        {
            var grind = new BalanceSettings();
            float normal = SecondsToFall(Assists.Balance(grind, 0), 0f);
            float steady = SecondsToFall(Assists.Balance(grind, 1), 0f);
            float auto = SecondsToFall(Assists.Balance(grind, 2), 0f);
            Assert.Less(normal, 3f, "untouched, a normal meter falls fast");
            Assert.Greater(steady, normal * 1.5f, "steady gives you more time");
            Assert.AreEqual(30f, auto, 0.001f, "auto never falls on its own, even with the noise always pushing");
            Assert.Less(SecondsToFall(Assists.Balance(grind, 2), 1f), 5f, "you can still steer off on purpose");
            var manual = new BalanceSettings { baseTip = 1.3f, tipGrowthPerSecond = 0.45f, control = 2.8f };
            Assert.AreEqual(30f, SecondsToFall(Assists.Balance(manual, 2), 0f), 0.001f, "manuals too");
        }

        [Test]
        public void Balance_NeverChangesTheProfile()
        {
            var b = new BalanceSettings();
            var copy = Assists.Balance(b, 2);
            Assert.AreNotEqual(b, copy, "a new object");
            Assert.AreEqual(new BalanceSettings().jitter, b.jitter, 0.0001f);
            Assert.AreEqual(0f, b.autoCorrect, 0.0001f);
            Assert.AreEqual(b.jitter, Assists.Balance(b, 0).jitter, 0.0001f, "level 0 is the same rules");
        }

        [Test]
        public void Landing_WiderWindows_StillCountSpins()
        {
            var rules = new LandingRules();
            // A 360 landed 60° off: a bail normally, a sketchy landing with the widest window.
            var input = new LandingInput { AirYawDegrees = 420f, SurfaceAngleDegrees = 5f };
            Assert.AreEqual(LandingQuality.Bail, LandingJudge.Evaluate(input, Assists.Landing(rules, 0)).Quality);
            var widest = LandingJudge.Evaluate(input, Assists.Landing(rules, 2));
            Assert.AreNotEqual(LandingQuality.Bail, widest.Quality, "widest");
            Assert.AreEqual(2, widest.HalfTurns, "the spin still counts as a 360");
            // 26° off is sketchy normally and clean with the wide window.
            var small = new LandingInput { AirYawDegrees = 206f, SurfaceAngleDegrees = 5f };
            Assert.AreEqual(LandingQuality.Sketchy, LandingJudge.Evaluate(small, rules).Quality);
            Assert.AreEqual(LandingQuality.Clean, LandingJudge.Evaluate(small, Assists.Landing(rules, 1)).Quality);
            foreach (int level in new[] { 1, 2 })
            {
                var r = Assists.Landing(rules, level);
                Assert.Less(r.sketchyYawError, 90f, "under 90° so a half turn is never read as the wrong spin");
                Assert.Less(r.cleanYawError, r.sketchyYawError);
                Assert.Less(r.maxUnfinishedFraction, 1f, "landing mid-flip still bails");
            }
            Assert.AreEqual(new LandingRules().cleanYawError, rules.cleanYawError, 0.0001f, "the profile is never changed");
        }
    }

    public class ColorVisionTests
    {
        private const int Protan = 0, Deutan = 1, Tritan = 2, Typical = 3;
        private static readonly Rgb Cream = Rgb.Hex(0xF3E9D2);

        private static double D(Rgb a, Rgb b, int type) => VisionPalette.SeenDistance(a, b, type);

        [Test]
        public void RedGreen_KeepsFailAndSuccessApart_ForProtanAndDeutan()
        {
            var std = VisionPalette.For(ColorVision.Standard);
            var rg = VisionPalette.For(ColorVision.RedGreen);
            foreach (int type in new[] { Protan, Deutan })
            {
                Assert.Greater((float)D(rg.Bad, rg.Good, type), 80f, "type " + type);
                Assert.Greater((float)D(rg.Bad, rg.Good, type), (float)D(std.Bad, std.Good, type) + 30f, "clearly better than standard, type " + type);
            }
        }

        [Test]
        public void BlueYellow_KeepsTheAccentReadable_ForTritans()
        {
            var std = VisionPalette.For(ColorVision.Standard);
            var by = VisionPalette.For(ColorVision.BlueYellow);
            Assert.Less((float)D(std.Accent, Cream, Tritan), 35f, "tape yellow fades into cream text for tritans");
            Assert.Greater((float)D(by.Accent, Cream, Tritan), 55f);
            Assert.Greater((float)D(by.Bad, by.Good, Tritan), 100f);
            Assert.Greater((float)D(by.Bad, by.Accent, Tritan), 40f);
            Assert.Greater((float)D(by.Good, by.Accent, Tritan), 40f);
        }

        [Test]
        public void HighContrast_WorksForEveryone_AndDarkensPanels()
        {
            var hc = VisionPalette.For(ColorVision.HighContrast);
            foreach (int type in new[] { Protan, Deutan, Tritan, Typical })
                Assert.Greater((float)D(hc.Bad, hc.Good, type), 80f, "type " + type);
            Assert.Greater(hc.PanelAlpha, VisionPalette.For(ColorVision.Standard).PanelAlpha);
        }

        [Test]
        public void EveryPalette_IsClearWithTypicalVision()
        {
            for (int i = 0; i < VisionPalette.Count; i++)
            {
                var p = VisionPalette.For((ColorVision)i);
                Assert.Greater((float)D(p.Bad, p.Good, Typical), 100f, "palette " + i);
                Assert.Greater((float)D(p.Bad, p.Accent, Typical), 40f, "palette " + i);
                Assert.IsTrue(VisionPalette.Name((ColorVision)i).StartsWith("COLOUR VISION: "));
            }
        }

        [Test]
        public void Saved_ValuesAreSafe_AndCycle()
        {
            Assert.AreEqual(ColorVision.Standard, VisionPalette.FromSaved(-1));
            Assert.AreEqual(ColorVision.Standard, VisionPalette.FromSaved(99));
            Assert.AreEqual(ColorVision.BlueYellow, VisionPalette.FromSaved(2));
            Assert.AreEqual(ColorVision.Standard, VisionPalette.Next(ColorVision.HighContrast));
            Assert.AreEqual((int)ColorVision.RedGreen, 1, "the old colour-safe switch maps to 1");
        }
    }

    public class StoryFinaleTests
    {
        [Test]
        public void ChapterTen_GoesBackAcrossTheCity()
        {
            var c = Story.Chapters.First(x => x.Id == "all_city");
            Assert.AreEqual(10, c.Number);
            Assert.AreEqual(c, Story.Chapters.Last(), "the finale is last");
            Assert.AreEqual(4, c.Steps.Length);
            var parks = c.Steps.Select(s => s.LocationId).Distinct().ToList();
            Assert.GreaterOrEqual(parks.Count, 3f, "three different earlier parks");
            Assert.AreEqual("harbor_plaza", c.Steps[0].LocationId, "it starts where the story did");
            Assert.AreEqual(Story.FinalStepId, c.Steps[3].Id);
            Assert.AreEqual(StoryObjective.Skate, c.Steps[3].Objective);
            Assert.AreEqual(2, c.Steps[3].RivalLevel);
            Assert.AreEqual("All-City Hoodie", c.Steps[3].RewardItem);
            Assert.Greater(c.Steps[2].Bonks, 0f);
            for (int i = 1; i < c.Steps.Length; i++)
                if (c.Steps[i].Objective != StoryObjective.Skate && c.Steps[i - 1].Objective != StoryObjective.Skate)
                    Assert.Greater(c.Steps[i].Target, c.Steps[i - 1].Target, "targets climb");
        }

        [Test]
        public void TheOriginals_HaveTheirOwnLook()
        {
            foreach (var name in new[] { "REWIND", "STATIC" })
            {
                var look = StoryCast.LookFor(name, null);
                Assert.IsNotNull(look, name);
                Assert.AreEqual((int)ShirtStyle.Tank, look.shirtStyle, name);
            }
            Assert.AreNotEqual(StoryCast.LookFor("REWIND", null).skinTone, StoryCast.LookFor("STATIC", null).skinTone, "two different people");
        }

        [Test]
        public void Finale_PlaysTheEpilogue_OthersDont()
        {
            var finale = Story.FindStep(Story.FinalStepId);
            var panels = Story.ClearPanels(finale);
            Assert.AreEqual(finale.Outro.Length + Story.Epilogue.Length, panels.Length);
            Assert.IsTrue(panels.Last().Line.Contains("THE END"));
            var other = Story.FindStep("s9_anchor");
            Assert.AreEqual(other.Outro.Length, Story.ClearPanels(other).Length);
            Assert.AreEqual(0, Story.ClearPanels(null).Length);
            foreach (var p in Story.Epilogue) Assert.IsNotEmpty(p.Line);
        }

        [Test]
        public void FinishingEverything_FinishesTheStory()
        {
            var state = new StoryState();
            foreach (var s in Story.AllSteps())
            {
                Assert.IsFalse(state.Finished);
                Assert.AreEqual(s.Id, Story.NextStep(state).Id);
                Assert.IsTrue(state.Clear(s.Id));
            }
            Assert.IsTrue(state.Finished);
            Assert.AreEqual(10, state.ChaptersDone());
            Assert.IsNull(Story.NextStep(state));
        }

        [Test]
        public void NewAchievements_AllCityAndPhotoFinish()
        {
            var allCity = Achievements.Find("all_city");
            var photo = Achievements.Find("photo_finish");
            Assert.IsNotNull(allCity);
            Assert.IsNotNull(photo);
            Assert.AreEqual(0f, allCity.Progress(new PlayerProgress()), 0.0001f);
            Assert.AreEqual(1f, allCity.Progress(new PlayerProgress { AllCity = true }), 0.0001f);
            Assert.AreEqual(1f, photo.Progress(new PlayerProgress { RaceGhostWins = 1 }), 0.0001f);
            Assert.AreEqual(20, Achievements.All.Count);
            Assert.AreEqual(995, GameCenterSetup.TotalPoints(Achievements.All));
            Assert.LessOrEqual(GameCenterSetup.TotalPoints(Achievements.All), GameCenterSetup.MaxPoints);
        }
    }
}
