using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class SkaterRatingTests
    {
        private static RatingFacts Maxed(double parkRatio)
        {
            var f = new RatingFacts { StoryChaptersDone = 10, StoryChapters = 10, TricksInBook = 40, TricksLanded = 40, TrickGoalsDone = 160, SkateWins = 500, BestStreak = 365 };
            foreach (var id in SkaterRating.Parks) f.ParkBests[id] = (long)(SkaterRating.ParFor(id) * parkRatio);
            f.NightLines = SkaterRating.Parks.ToList();
            foreach (var s in RetroCityLayout.Spots) f.SpotBests[s.Id] = s.Gold * 4;
            foreach (var r in RetroCityLayout.Races) f.RaceBests[r.Id] = r.Gold * 0.6f;
            return f;
        }

        [Test]
        public void TheParts_AddUpToTenThousand()
        {
            int max = SkaterRating.Breakdown(new RatingFacts()).Sum(l => l.Max);
            Assert.AreEqual(SkaterRating.Max, max);
            Assert.AreEqual(0, SkaterRating.Total(new RatingFacts()), "a new save is a ROOKIE with 0");
            Assert.AreEqual(0, SkaterRating.Total(null));
        }

        [Test]
        public void ParIsWorthThreeQuarters_AndMoreAlwaysHelps_ButLessAndLess()
        {
            Assert.AreEqual(0.75f, (float)SkaterRating.Curve(1.0), 1e-6f);
            Assert.AreEqual(0.0, SkaterRating.Curve(0));
            Assert.AreEqual(0.0, SkaterRating.Curve(-3));
            double prev = 0, prevGain = double.MaxValue;
            for (double r = 0.25; r <= 4.0; r += 0.25)
            {
                double c = SkaterRating.Curve(r);
                Assert.IsTrue(c > prev, "never goes down with a better result");
                Assert.IsTrue(c < 1.0, "never reaches 100%");
                Assert.IsTrue(c - prev < prevGain + 1e-12, "each step is worth a bit less");
                prevGain = c - prev;
                prev = c;
            }
        }

        [Test]
        public void FarmingOnePark_CantBeatPlayingThemAll()
        {
            var farmer = new RatingFacts();
            farmer.ParkBests["harbor_plaza"] = SkaterRating.ParFor("harbor_plaza") * 40; // absurd single-park score
            var allRounder = new RatingFacts();
            foreach (var id in SkaterRating.Parks) allRounder.ParkBests[id] = SkaterRating.ParFor(id) / 2;
            Assert.LessOrEqual(SkaterRating.Total(farmer), SkaterRating.ParkPoints);
            Assert.Greater(SkaterRating.Total(allRounder), SkaterRating.Total(farmer) * 3);
        }

        [Test]
        public void EveryParkHasAPar_AndTheGradesClimb()
        {
            foreach (var id in SkaterRating.Parks)
            {
                long par = SkaterRating.ParFor(id);
                Assert.Greater(par, 0, id);
                Assert.AreEqual(RunGrade.None, SkaterRating.GradeFor(id, 0));
                Assert.AreEqual(RunGrade.D, SkaterRating.GradeFor(id, par / 10));
                Assert.AreEqual(RunGrade.A, SkaterRating.GradeFor(id, par));
                Assert.AreEqual(RunGrade.S, SkaterRating.GradeFor(id, par * 2));
                Assert.AreEqual(0, SkaterRating.NextGradeScore(id, par * 2), "nothing above S");
                long next = SkaterRating.NextGradeScore(id, par / 2);
                Assert.IsTrue((int)SkaterRating.GradeFor(id, next) > (int)SkaterRating.GradeFor(id, par / 2), id);
            }
            Assert.AreEqual(0, SkaterRating.ParFor("custom_1"), "custom parks have no par");
            Assert.AreEqual(RunGrade.None, SkaterRating.GradeFor("retro_city", 99999));
        }

        [Test]
        public void ARealMaster_ReachesAllCity_ButNeverPastTheMax()
        {
            int r = SkaterRating.Total(Maxed(3.0));
            Assert.GreaterOrEqual(r, SkaterRating.RankFloors[SkaterRating.RankFloors.Length - 1]);
            Assert.LessOrEqual(r, SkaterRating.Max);
            Assert.AreEqual("ALL-CITY", SkaterRating.RankName(r));
            Assert.AreEqual(0, SkaterRating.ToNextRank(r));
            Assert.AreEqual("ROOKIE", SkaterRating.RankName(0));
            Assert.AreEqual(1000, SkaterRating.ToNextRank(0));
        }

        [Test]
        public void Races_RewardSpeed_AndAnUnfinishedRaceScoresNothing()
        {
            var race = RetroCityLayout.Races[0];
            Assert.AreEqual(0, SkaterRating.RaceScore(race, 0f));
            int gold = SkaterRating.RaceScore(race, race.Gold);
            int slow = SkaterRating.RaceScore(race, race.Bronze);
            int fast = SkaterRating.RaceScore(race, race.Gold * 0.8f);
            Assert.Greater(gold, slow);
            Assert.Greater(fast, gold);
            Assert.LessOrEqual(fast, SkaterRating.RacePoints);
        }

        [Test]
        public void Tips_AreSortedByWhatTheyreWorth_AndDisappearWhenDone()
        {
            var tips = SkaterRating.Tips(new RatingFacts { TricksInBook = 30, StoryChapters = 10 }, id => id.ToUpperInvariant(), 5);
            Assert.AreEqual(5, tips.Count);
            for (int i = 1; i < tips.Count; i++) Assert.GreaterOrEqual(tips[i - 1].Gain, tips[i].Gain);
            foreach (var t in tips) Assert.Greater(t.Gain, 0);

            var done = Maxed(3.0);
            foreach (var t in SkaterRating.Tips(done, null, 50))
                Assert.IsFalse(t.Text.Contains("NIGHT LINE"), "cleared night lines aren't suggested");
        }

        [Test]
        public void TheRatingBoard_HasARange_AndIsInTheHub()
        {
            Assert.IsTrue(ScoreLimits.IsPlausible(SkaterRating.LeaderboardId, 4200));
            Assert.IsFalse(ScoreLimits.IsPlausible(SkaterRating.LeaderboardId, SkaterRating.Max + 1));
            Assert.IsTrue(ScoreLimits.IsPlausible(HallOfMeat.LeaderboardId, 25000));
            Assert.IsFalse(ScoreLimits.IsPlausible(HallOfMeat.LeaderboardId, ScoreLimits.MaxSlam + 1));
            var boards = LeaderboardHub.Boards(new[] { ("harbor_plaza", "Harbor Plaza") });
            Assert.IsTrue(boards.Exists(b => b.Id == SkaterRating.LeaderboardId && b.Group == BoardGroup.Events));
            Assert.IsTrue(boards.Exists(b => b.Id == HallOfMeat.LeaderboardId && b.Group == BoardGroup.Events));
            StringAssert.Contains("`" + SkaterRating.LeaderboardId + "`", GameCenterSetup.Markdown(boards, Achievements.All));
        }
    }

    public class HallOfMeatTests
    {
        private static SlamInput Typical => new SlamInput { Speed = 12f, Drop = 3f, AirTime = 1f, Spin = 200f, Slide = 4f };

        [Test]
        public void HarderSlams_ScoreMore()
        {
            long baseline = HallOfMeat.Score(Typical).Points;
            Assert.Greater(baseline, HallOfMeat.MinSlam);
            var faster = Typical; faster.Speed = 20f;
            var higher = Typical; higher.Drop = 8f;
            var headOn = Typical; headOn.HeadOn = true;
            Assert.Greater(HallOfMeat.Score(faster).Points, baseline);
            Assert.Greater(HallOfMeat.Score(higher).Points, baseline);
            Assert.IsTrue(System.Math.Abs((long)System.Math.Floor(baseline * 1.5) - HallOfMeat.Score(headOn).Points) <= 2, "head-on is worth half again");
        }

        [Test]
        public void OutOfBounds_AndNonsense_ScoreNothing()
        {
            var oob = Typical; oob.OutOfBounds = true;
            Assert.AreEqual(0, HallOfMeat.Score(oob).Points);
            var nan = new SlamInput { Speed = float.NaN, Drop = -5f, Slide = float.NaN };
            Assert.AreEqual(0, HallOfMeat.Score(nan).Points);
            var huge = new SlamInput { Speed = 1e9f, Drop = 1e9f, AirTime = 1e9f, Spin = 1e9f, Slide = 1e9f, HeadOn = true };
            Assert.LessOrEqual(HallOfMeat.Score(huge, 99).Points, ScoreLimits.MaxSlam, "capped for the board");
        }

        [Test]
        public void SlamsCloseTogether_Chain_AndStumblesDont()
        {
            var session = new SlamSession();
            var a = session.Add(Typical, 0f);
            var b = session.Add(Typical, 5f);
            Assert.AreEqual(1f, a.Chain);
            Assert.AreEqual(1f + HallOfMeat.ChainStep, b.Chain, 1e-6f);
            var c = session.Add(Typical, 30f);
            Assert.AreEqual(1f, c.Chain, "the chain resets after the window");
            Assert.AreEqual(3, session.Count);
            Assert.AreEqual(a.Points + b.Points + c.Points, session.Total);
            Assert.AreSame(b, session.Best);

            var stumble = session.Add(new SlamInput { Speed = 0.1f }, 31f);
            Assert.Less(stumble.Points, HallOfMeat.MinSlam);
            Assert.AreEqual(3, session.Count, "a stumble isn't a slam");
            Assert.AreEqual(HallOfMeat.MaxChain, HallOfMeat.ChainFor(100));
        }

        [Test]
        public void Names_ClimbWithThePoints()
        {
            Assert.AreEqual("STUMBLE", HallOfMeat.NameFor(0));
            Assert.AreEqual("HALL OF FAMER", HallOfMeat.NameFor(1000000));
            Assert.AreEqual(HallOfMeat.Names.Length, HallOfMeat.NameFloors.Length);
        }
    }

    public class BoardSetupTests
    {
        [Test]
        public void MediumMedium_ChangesNothing()
        {
            var f = BoardSetup.Factors(1, 1);
            Assert.AreEqual(SetupFactors.Neutral, f);
            Assert.AreEqual(SetupFactors.Neutral, BoardSetup.Factors(-4, 99), "bad saved values fall back to medium");
        }

        [Test]
        public void EveryChoice_IsATradeOff()
        {
            var loose = BoardSetup.Factors(0, 1);
            Assert.Greater(loose.TurnFast, 1f);
            Assert.Less(loose.LandingAlign, 1f);
            Assert.Greater(loose.BalanceNoise, 1f);
            var tight = BoardSetup.Factors(2, 1);
            Assert.Less(tight.TurnFast, 1f);
            Assert.Less(tight.BalanceNoise, 1f);
            var soft = BoardSetup.Factors(1, 0);
            Assert.Less(soft.MaxSpeed, 1f);
            Assert.Greater(soft.TurnFast, 1f);
            var hard = BoardSetup.Factors(1, 2);
            Assert.Greater(hard.MaxSpeed, 1f);
            Assert.Less(hard.GrindDecel, 1f);
            Assert.Less(hard.TurnFast, 1f);
            foreach (int t in new[] { 0, 1, 2 })
            foreach (int w in new[] { 0, 1, 2 })
            {
                var x = BoardSetup.Factors(t, w);
                Assert.IsTrue(x.MaxSpeed >= 0.9f && x.MaxSpeed <= 1.1f, "max speed stays close to the standard");
                Assert.IsTrue(x.TurnFast >= 0.75f && x.TurnFast <= 1.3f, "turning stays close to the standard");
                StringAssert.Contains("TRUCKS", BoardSetup.Summary(t, w));
            }
        }

        [Test]
        public void TheLook_KeepsItsSetup_InRange()
        {
            var look = new SkaterLook { trucks = 7, wheels = -1 };
            look.Sanitize();
            Assert.AreEqual(1, look.trucks);
            Assert.AreEqual(1, look.wheels);
            Assert.AreEqual(1, new SkaterLook().trucks, "new skaters start on medium");
        }
    }

    public class BoardWearTests
    {
        [Test]
        public void Wear_GrowsWithUse_AndANewDeckStartsFresh()
        {
            var w = new BoardWearState();
            Assert.IsTrue(w.UseDeck("art:a"));
            Assert.AreEqual(0f, w.Level);
            w.Add(300f, 200);
            float worn = w.Level;
            Assert.Greater(worn, 0.3f);
            Assert.Less(worn, 1f);
            Assert.IsFalse(w.UseDeck("art:a"), "same deck keeps its wear");
            Assert.AreEqual(worn, w.Level);
            w.off = true;
            Assert.AreEqual(0f, w.Level, "switched off shows a fresh deck");
            w.off = false;
            Assert.IsTrue(w.UseDeck("shop:deck_neon"));
            Assert.AreEqual(0f, w.Level);
            w.Add(float.NaN, -3);
            Assert.AreEqual(0f, w.Level, "nonsense doesn't wear the board");
        }

        [Test]
        public void Chips_OnlyGrow_AndStartAtTheEnds()
        {
            int prev = -1;
            for (int step = 0; step <= BoardWear.Steps; step++)
            {
                int count = 0, ends = 0, middle = 0;
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 16; x++)
                {
                    bool chip = BoardWear.Chipped(x, y, step);
                    if (step > 0 && BoardWear.Chipped(x, y, step - 1)) Assert.IsTrue(chip, "more wear never un-chips a pixel");
                    if (!chip) continue;
                    count++;
                    if (y < 8 || y >= 56) ends++;
                    if (y >= 28 && y < 36) middle++;
                }
                Assert.GreaterOrEqual(count, prev);
                prev = count;
                if (step == 3) Assert.Greater(ends, middle, "nose and tail chip first");
            }
            Assert.AreEqual(0, CountChips(0), "a new deck has no chips");
            Assert.Less(CountChips(BoardWear.Steps), 16 * 64, "even a thrashed deck keeps some graphic");
            Assert.AreEqual("FRESH", BoardWear.Label(0f));
            Assert.AreEqual(0, BoardWear.Step(0f));
            Assert.AreEqual(BoardWear.Steps, BoardWear.Step(1f));
        }

        private static int CountChips(int step)
        {
            int n = 0;
            for (int y = 0; y < 64; y++) for (int x = 0; x < 16; x++) if (BoardWear.Chipped(x, y, step)) n++;
            return n;
        }
    }

    public class SpotContestTests
    {
        private const string Park = "harbor_plaza";

        [Test]
        public void TheField_IsTheSameAllWeek_AndChangesNextWeek()
        {
            var a = SpotContest.Rivals(Park, 202642);
            var b = SpotContest.Rivals(Park, 202642);
            var c = SpotContest.Rivals(Park, 202643);
            Assert.AreEqual(5, a.Count);
            CollectionAssert.AreEqual(a.Select(x => x.Name).ToList(), b.Select(x => x.Name).ToList());
            CollectionAssert.AreEqual(a.SelectMany(x => x.Heats).ToList(), b.SelectMany(x => x.Heats).ToList());
            Assert.IsFalse(a.SelectMany(x => x.Heats).SequenceEqual(c.SelectMany(x => x.Heats)), "a new week, a new field");
            Assert.AreEqual(5, a.Select(x => x.Name).Distinct().Count(), "five different rivals");
            foreach (var r in a)
            {
                Assert.AreEqual(SpotContest.Heats, r.Heats.Count);
                foreach (var h in r.Heats) Assert.IsTrue(h >= 0 && h <= SpotContest.HeatPar(Park) * 2, "rival heats stay near par");
            }
        }

        [Test]
        public void BestTwoHeats_Count()
        {
            Assert.AreEqual(0, SpotContest.CountedTotal(new List<long>()));
            Assert.AreEqual(500, SpotContest.CountedTotal(new List<long> { 500 }));
            Assert.AreEqual(900, SpotContest.CountedTotal(new List<long> { 500, 100, 400 }));
        }

        [Test]
        public void Standings_RevealRivalsHeatByHeat_AndPlaceYou()
        {
            var after1 = SpotContest.Standings(Park, 7, new List<long> { 1 }, "OMARI");
            Assert.AreEqual(6, after1.Count);
            foreach (var e in after1) Assert.AreEqual(1, e.Heats.Count, "only heat 1 is shown after heat 1");
            Assert.AreEqual(6, SpotContest.PlaceOf(after1), "1 point is last");

            long huge = SpotContest.HeatPar(Park) * 3;
            var win = SpotContest.Standings(Park, 7, new List<long> { huge, huge, 0 }, "OMARI");
            Assert.AreEqual(1, SpotContest.PlaceOf(win));
            Assert.IsTrue(win[0].IsYou);
            Assert.AreEqual(150, SpotContest.TokensFor(1));
            Assert.AreEqual(0, SpotContest.TokensFor(4));
            Assert.AreEqual("2ND", SpotContest.PlaceLabel(2));
        }

        [Test]
        public void ToMoveUp_IsEnoughToPassTheNextRival()
        {
            var mine = new List<long> { 1 };
            long need = SpotContest.ToMoveUp(Park, 11, mine, "ME");
            Assert.Greater(need, 0);
            int before = SpotContest.PlaceOf(SpotContest.Standings(Park, 11, new List<long> { 1, 0 }, "ME"));
            int after = SpotContest.PlaceOf(SpotContest.Standings(Park, 11, new List<long> { 1, need }, "ME"));
            Assert.Less(after, before, "that score moves you up");
            Assert.AreEqual(0, SpotContest.ToMoveUp(Park, 11, new List<long> { 1, 1, 1 }, "ME"), "no heats left");
        }

        [Test]
        public void Records_KeepTheBestFinish_AndThePodiumPaysOncePerWeek()
        {
            var list = new List<ContestRecord>();
            Assert.AreEqual(40, SpotContest.Record(list, Park, 3, 1000, 100), "bronze pays");
            Assert.AreEqual(0, SpotContest.Record(list, Park, 3, 900, 100), "the same finish again that week pays nothing");
            Assert.AreEqual(150 - 40, SpotContest.Record(list, Park, 1, 900, 100), "a better finish pays the difference");
            Assert.AreEqual(0, SpotContest.Record(list, Park, 1, 950, 100), "winning again that week pays nothing");
            Assert.AreEqual(1, list[0].wins, "a park's win counts once a week");
            Assert.AreEqual(0, SpotContest.Record(list, Park, 5, 100, 101), "off the podium pays nothing");
            Assert.AreEqual(75, SpotContest.Record(list, Park, 2, 800, 101), "next week pays again");
            Assert.AreEqual(1, list[0].bestPlace);
            Assert.AreEqual(1000, list[0].bestTotal);
            Assert.IsTrue(SpotContest.Available(Park));
            Assert.IsFalse(SpotContest.Available("retro_city"));
        }
    }
}
