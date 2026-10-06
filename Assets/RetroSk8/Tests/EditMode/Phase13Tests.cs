using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class OutfitTests
    {
        [Test]
        public void OldSaves_KeepTheirLook()
        {
            var look = new SkaterLook(); // every Phase 13 field is 0
            look.Sanitize();
            Assert.IsFalse(SkaterLook.Pick(look.shirtColor, out _), "0 = shop shirt");
            Assert.IsFalse(SkaterLook.Pick(look.wheelColor, out _), "0 = shop wheels");
            Assert.AreEqual((int)ShirtStyle.Tee, look.shirtStyle);
            Assert.AreEqual((int)DeckShape.Popsicle, look.deckShape);
            Assert.IsFalse(look.LongSleeves);
            Assert.IsFalse(look.BareShins);
        }

        [Test]
        public void ColourChoices_MapToThePalette()
        {
            Assert.IsTrue(SkaterLook.Pick(1, out var first));
            Assert.AreEqual(LookPalette.Colors[0].R, first.R, 1e-6f);
            Assert.IsTrue(SkaterLook.Pick(LookPalette.Colors.Length, out var last));
            Assert.AreEqual(LookPalette.Colors[LookPalette.Colors.Length - 1].G, last.G, 1e-6f);
            Assert.IsFalse(SkaterLook.Pick(LookPalette.Colors.Length + 1, out _));
        }

        [Test]
        public void Sanitize_WrapsEveryNewChoice()
        {
            var look = new SkaterLook { shirtStyle = 99, bottomsStyle = -1, shoeStyle = 7, deckShape = 5, shirtColor = -3, gripColor = 500 };
            look.Sanitize();
            Assert.That(look.shirtStyle >= 0 && look.shirtStyle < LookPalette.ShirtNames.Length);
            Assert.AreEqual(LookPalette.BottomsNames.Length - 1, look.bottomsStyle);
            Assert.That(look.shoeStyle >= 0 && look.shoeStyle < LookPalette.ShoeNames.Length);
            Assert.That(look.deckShape >= 0 && look.deckShape < LookPalette.ShapeNames.Length);
            Assert.That(look.shirtColor >= 0 && look.shirtColor <= LookPalette.Colors.Length);
            Assert.That(look.gripColor >= 0 && look.gripColor <= LookPalette.Colors.Length);
        }

        [Test]
        public void Cuts_DecideSleevesAndShins()
        {
            Assert.IsTrue(new SkaterLook { shirtStyle = (int)ShirtStyle.Hoodie }.LongSleeves);
            Assert.IsTrue(new SkaterLook { shirtStyle = (int)ShirtStyle.Flannel }.LongSleeves);
            Assert.IsTrue(new SkaterLook { shirtStyle = (int)ShirtStyle.Tank }.Sleeveless);
            Assert.IsTrue(new SkaterLook { bottomsStyle = (int)BottomsStyle.Shorts }.BareShins);
            var clone = new SkaterLook { shirtStyle = 2, deckShape = 1 }.Clone();
            Assert.AreEqual(2, clone.shirtStyle);
            Assert.AreEqual(1, clone.deckShape);
        }
    }

    public class StoryTests
    {
        [Test]
        public void Structure_IsSoundAndOriginal()
        {
            var ids = new HashSet<string>();
            int steps = 0;
            for (int i = 0; i < Story.Chapters.Length; i++)
            {
                var c = Story.Chapters[i];
                Assert.AreEqual(i + 1, c.Number);
                foreach (var s in c.Steps)
                {
                    steps++;
                    Assert.IsTrue(ids.Add(s.Id), s.Id);
                    Assert.GreaterOrEqual(Array.IndexOf(ShareCodes.BuiltInParks, s.LocationId), 0, s.Id + " park");
                    Assert.Greater(s.Intro.Length, 0, s.Id);
                    Assert.Greater(s.Outro.Length, 0, s.Id);
                    Assert.Greater(s.Tokens, 0);
                    if (s.Objective != StoryObjective.Skate) Assert.Greater(s.Target, 0, s.Id);
                    else Assert.That(s.RivalLevel >= 0 && s.RivalLevel <= 2, s.Id);
                }
            }
            Assert.AreEqual(20, steps);
            Assert.AreEqual("floodgate_ditch", Story.Chapters[5].Steps[0].LocationId, "the finale is at the new park");
        }

        [Test]
        public void Steps_UnlockInOrder_AndPayOnce()
        {
            var state = new StoryState();
            var first = Story.NextStep(state);
            Assert.AreEqual("s1_pilar", first.Id);
            Assert.IsTrue(Story.IsUnlocked(state, "s1_pilar"));
            Assert.IsFalse(Story.IsUnlocked(state, "s1_dex"));
            Assert.IsTrue(state.Clear("s1_pilar"));
            Assert.IsFalse(state.Clear("s1_pilar"), "rewards pay once");
            Assert.IsFalse(state.Clear("not_a_step"));
            Assert.IsTrue(Story.IsUnlocked(state, "s1_dex"));
            Assert.AreEqual("s1_dex", Story.NextStep(state).Id);
            Assert.AreEqual(0, state.ChaptersDone());
            state.Clear("s1_dex");
            Assert.AreEqual(1, state.ChaptersDone());

            foreach (var s in Story.AllSteps()) state.Clear(s.Id);
            Assert.IsTrue(state.Finished);
            Assert.IsNull(Story.NextStep(state));
        }

        [Test]
        public void Runs_ClearOnlyTheirParkAndTarget()
        {
            var step = Story.FindStep("s2_sheen");
            Assert.IsTrue(Story.RunClears(step, "harbor_plaza", step.Target + 1));
            Assert.IsFalse(Story.RunClears(step, "harbor_plaza", step.Target), "must beat it, not tie it");
            Assert.IsFalse(Story.RunClears(step, "neon_warehouse", step.Target * 2));
            Assert.IsFalse(Story.RunClears(Story.FindStep("s1_dex"), "harbor_plaza", 999999), "S.K.A.T.E. steps clear by winning");
        }

        [Test]
        public void RivalCurve_AddsUpToTheTarget_DuringTheRun()
        {
            foreach (long target in new long[] { 8000, 14000, 30000, 123457 })
            {
                var banks = RivalCurve.Banks(target, 120f, 42);
                long sum = 0;
                float last = -1f;
                foreach (var b in banks)
                {
                    Assert.Greater(b.Points, 0);
                    Assert.GreaterOrEqual(b.Time, last);
                    Assert.That(b.Time > 0f && b.Time < 120f);
                    last = b.Time;
                    sum += b.Points;
                }
                Assert.AreEqual(target, sum);
                Assert.AreEqual(target, RivalTimeline.ScoreAt(banks, 999f));
                Assert.Less(RivalTimeline.ScoreAt(banks, 30f), target, "the rival builds up over the run");
            }
            Assert.AreEqual(RivalCurve.Banks(14000, 120f, 7)[2].Points, RivalCurve.Banks(14000, 120f, 7)[2].Points);
        }

        [Test]
        public void State_Sanitize_DropsUnknownSteps()
        {
            var s = new StoryState { cleared = new List<string> { "s1_pilar", "old_step" } };
            s.Sanitize();
            Assert.AreEqual(1, s.cleared.Count);
        }
    }

    public class JuiceTests
    {
        [Test]
        public void Fireworks_And_SlowMo_Thresholds()
        {
            Assert.AreEqual(0, Juice.Fireworks(Juice.FireworkPoints - 1));
            Assert.Greater(Juice.Fireworks(Juice.FireworkPoints), 0);
            Assert.Greater(Juice.Fireworks(40000), Juice.Fireworks(10000));
            Assert.LessOrEqual(Juice.Fireworks(10000000), 120);
            Assert.IsFalse(Juice.SlowMo(Juice.SlowMoPoints - 1));
            Assert.IsTrue(Juice.SlowMo(Juice.SlowMoPoints));
        }

        [Test]
        public void SlowMo_HoldsThenEasesBack()
        {
            Assert.AreEqual(Juice.SlowScale, Juice.TimeScaleAt(0f), 1e-5f);
            Assert.AreEqual(Juice.SlowScale, Juice.TimeScaleAt(Juice.SlowHold), 1e-5f);
            float mid = Juice.TimeScaleAt(Juice.SlowHold + Juice.SlowEase * 0.5f);
            Assert.That(mid > Juice.SlowScale && mid < 1f);
            Assert.AreEqual(1f, Juice.TimeScaleAt(Juice.SlowMoLength + 0.01f), 1e-5f);
            Assert.AreEqual(1f, Juice.TimeScaleAt(-1f), 1e-5f);
        }

        [Test]
        public void EaseOutBack_StartsAndEndsRight()
        {
            Assert.AreEqual(0f, Juice.EaseOutBack(0f), 1e-5f);
            Assert.AreEqual(1f, Juice.EaseOutBack(1f), 1e-5f);
            Assert.Greater(Juice.EaseOutBack(0.8f), 1f, "it overshoots before settling");
        }

        [Test]
        public void Tips_ShowOnce_AndSpaced()
        {
            var tips = new TipState();
            Assert.IsTrue(tips.TryShow(TipId.Ollie, 999f));
            Assert.IsFalse(tips.TryShow(TipId.Ollie, 999f), "once ever");
            Assert.IsFalse(tips.TryShow(TipId.Grind, TipState.MinGap - 1f), "not right after another tip");
            Assert.IsTrue(tips.TryShow(TipId.Grind, TipState.MinGap));
            foreach (TipId id in Enum.GetValues(typeof(TipId))) Assert.IsNotEmpty(TipState.Text(id, true));
            tips.seen.Add(99);
            tips.Sanitize();
            CollectionAssert.DoesNotContain(tips.seen, 99);
        }
    }

    public class NewParkTests
    {
        [Test]
        public void FloodgateDitch_IsShareable()
        {
            Assert.AreEqual(5, Array.IndexOf(ShareCodes.BuiltInParks, "floodgate_ditch"), "appended, so old codes keep their parks");
            var c = new ScoreChallenge { LocationId = "floodgate_ditch", Target = 12345, From = "OMARI" };
            Assert.IsTrue(ShareCodes.TryDecodeChallenge(ShareCodes.EncodeChallenge(c), out var back, out var error), error);
            Assert.AreEqual("floodgate_ditch", back.LocationId);
        }

        [Test]
        public void DitchSong_Renders()
        {
            var data = MusicComposer.Render(MusicComposer.Ditch, 8000);
            Assert.AreEqual(MusicComposer.LoopSamples(MusicComposer.Ditch, 8000), data.Length);
        }
    }
}
