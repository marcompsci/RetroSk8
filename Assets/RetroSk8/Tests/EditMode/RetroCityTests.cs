using System.Collections.Generic;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class RetroCityLayoutTests
    {
        private static bool Inside(float x, float z, float margin = 0f) => RetroCityLayout.Contains(x, z, margin);

        [Test]
        public void Ids_AreUnique()
        {
            var ids = new HashSet<string>();
            foreach (var s in RetroCityLayout.Spots) Assert.IsTrue(ids.Add(s.Id), s.Id);
            foreach (var t in RetroCityLayout.Tapes) Assert.IsTrue(ids.Add(t.Id), t.Id);
            foreach (var r in RetroCityLayout.Races) Assert.IsTrue(ids.Add(r.Id), r.Id);
        }

        [Test]
        public void EverythingIsInsideTheCity()
        {
            foreach (var s in RetroCityLayout.Spots)
            {
                Assert.IsTrue(Inside(s.X, s.Z, s.Radius), s.Id);
                float dx = s.MarkerX - s.X, dz = s.MarkerZ - s.Z;
                Assert.IsTrue(dx * dx + dz * dz <= s.Radius * s.Radius, s.Id + " marker must be inside its spot");
            }
            foreach (var t in RetroCityLayout.Tapes) Assert.IsTrue(Inside(t.X, t.Z, 2f), t.Id);
            foreach (var r in RetroCityLayout.Races)
                for (int i = 0; i < r.GateCount; i++) Assert.IsTrue(Inside(r.Gates[i * 2], r.Gates[i * 2 + 1], RetroCityLayout.GateRadius), r.Id);
        }

        [Test]
        public void Spots_DontOverlap()
        {
            var spots = RetroCityLayout.Spots;
            for (int i = 0; i < spots.Count; i++)
            for (int j = i + 1; j < spots.Count; j++)
            {
                float dx = spots[i].X - spots[j].X, dz = spots[i].Z - spots[j].Z;
                Assert.Greater((float)System.Math.Sqrt(dx * dx + dz * dz), spots[i].Radius + spots[j].Radius, $"{spots[i].Id} vs {spots[j].Id}");
            }
        }

        [Test]
        public void Thresholds_Increase()
        {
            foreach (var s in RetroCityLayout.Spots) Assert.IsTrue(s.Bronze < s.Silver && s.Silver < s.Gold, s.Id);
            foreach (var r in RetroCityLayout.Races)
            {
                Assert.IsTrue(r.Gold < r.Silver && r.Silver < r.Bronze, r.Id);
                Assert.GreaterOrEqual(r.GateCount, 3);
            }
        }

        [Test]
        public void TwentyFourTapes_AndSpotLookup()
        {
            Assert.AreEqual(24, RetroCityLayout.Tapes.Count, "20 in the grid + 4 in Riverside Yards (Phase 13)");
            Assert.AreEqual("riverside_yards", RetroCityLayout.SpotAt(0f, 165f).Id);
            Assert.IsFalse(RetroCityLayout.Contains(80f, 160f), "the yards are only 90 m wide");
            Assert.AreEqual("civic_steps", RetroCityLayout.SpotAt(3f, -4f).Id);
            Assert.IsNull(RetroCityLayout.SpotAt(35f, 35f), "intersections are streets, not spots");
        }
    }

    public class CityProgressTests
    {
        [Test]
        public void Medals_ByScoreAndTime()
        {
            Assert.AreEqual(Medal.None, MedalRules.ForScore(999, 1000, 2000, 3000));
            Assert.AreEqual(Medal.Silver, MedalRules.ForScore(2500, 1000, 2000, 3000));
            Assert.AreEqual(Medal.Gold, MedalRules.ForTime(20f, 25f, 30f, 40f));
            Assert.AreEqual(Medal.Bronze, MedalRules.ForTime(39f, 25f, 30f, 40f));
            Assert.AreEqual(Medal.None, MedalRules.ForTime(41f, 25f, 30f, 40f));
            Assert.AreEqual(Medal.None, MedalRules.ForTime(0f, 25f, 30f, 40f));
        }

        [Test]
        public void Spots_AndTapes_PayOnce()
        {
            var p = new CityProgress();
            Assert.AreEqual(CityProgress.SpotTokens, p.FindSpot("civic_steps"));
            Assert.AreEqual(0, p.FindSpot("civic_steps"));
            Assert.AreEqual(CityProgress.TapeTokens, p.CollectTape("tape_01"));
            Assert.AreEqual(0, p.CollectTape("tape_01"));
            Assert.IsTrue(p.HasTape("tape_01"));
        }

        [Test]
        public void MedalUpgrades_PayTheDifference()
        {
            var p = new CityProgress();
            Assert.AreEqual(MedalRules.Tokens(Medal.Bronze), p.RecordChallenge("mall_ledges", 3600, Medal.Bronze));
            Assert.AreEqual(0, p.RecordChallenge("mall_ledges", 2000, Medal.None), "a worse run pays nothing");
            Assert.AreEqual(MedalRules.Tokens(Medal.Gold) - MedalRules.Tokens(Medal.Bronze), p.RecordChallenge("mall_ledges", 13000, Medal.Gold));
            Assert.AreEqual(Medal.Gold, p.ChallengeMedal("mall_ledges"));
        }

        [Test]
        public void RaceBest_KeepsFastest()
        {
            var p = new CityProgress();
            p.RecordRace("canal_cut", 20f, Medal.Bronze);
            p.RecordRace("canal_cut", 25f, Medal.None);
            Assert.AreEqual(20f, p.RaceBest("canal_cut"), 1e-4f);
            p.RecordRace("canal_cut", 13f, Medal.Gold);
            Assert.AreEqual(13f, p.RaceBest("canal_cut"), 1e-4f);
            Assert.AreEqual(Medal.Gold, p.RaceMedal("canal_cut"));
        }

        [Test]
        public void RaceRun_GatesInOrder()
        {
            var race = RetroCityLayout.FindRace("canal_cut");
            var run = new RaceRun(race);
            Assert.IsFalse(run.TryPass(70f, 35f), "can't skip ahead");
            run.Tick(5f);
            Assert.AreEqual(0f, run.Elapsed, 1e-4f, "the clock starts at the start gate");
            Assert.IsTrue(run.TryPass(71f, -34f));
            for (int i = 1; i < race.GateCount; i++)
            {
                run.Tick(3f);
                Assert.IsTrue(run.TryPass(race.Gates[i * 2], race.Gates[i * 2 + 1]));
            }
            Assert.IsTrue(run.Finished);
            Assert.AreEqual(12f, run.Elapsed, 1e-4f);
            Assert.AreEqual(Medal.Gold, run.Result);
            run.Tick(10f);
            Assert.AreEqual(12f, run.Elapsed, 1e-4f, "the clock stops at the finish");
        }
    }
}
