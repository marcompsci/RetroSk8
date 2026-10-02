using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class ShareCodeTests
    {
        private static CustomPark SamplePark()
        {
            var park = CustomPark.Starter(CustomParkIds.ForSlot(3), "Rail Yard #2");
            park.theme = (int)ParkTheme.NeonNight;
            park.Add(PieceKind.Bowl, 6, 30, 1, 2);
            park.Add(PieceKind.Stairs, 32, 24, 3, 0);
            return park;
        }

        [Test]
        public void ParkCode_RoundTrips()
        {
            var park = SamplePark();
            string code = ShareCodes.EncodePark(park);
            StringAssert.StartsWith("RP-", code);
            Assert.IsTrue(ShareCodes.TryDecodePark(code, out var back, out var error), error);
            Assert.AreEqual("RAIL YARD #2", back.name);
            Assert.AreEqual(park.theme, back.theme);
            Assert.AreEqual(park.pieces.Count, back.pieces.Count);
            for (int i = 0; i < park.pieces.Count; i++)
            {
                Assert.AreEqual(park.pieces[i].kind, back.pieces[i].kind);
                Assert.AreEqual(park.pieces[i].x, back.pieces[i].x);
                Assert.AreEqual(park.pieces[i].z, back.pieces[i].z);
                Assert.AreEqual(park.pieces[i].rot, back.pieces[i].rot);
                Assert.AreEqual(park.pieces[i].size, back.pieces[i].size);
            }
        }

        [Test]
        public void Codes_SurviveSloppyTyping()
        {
            string code = ShareCodes.EncodePark(SamplePark());
            string sloppy = code.ToLowerInvariant().Replace("-", " ").Replace('1', 'l').Replace('0', 'o');
            Assert.IsTrue(ShareCodes.TryDecodePark(sloppy, out var back, out var error), error);
            Assert.AreEqual(SamplePark().pieces.Count, back.pieces.Count);
        }

        [Test]
        public void Typos_AreCaught()
        {
            string code = ShareCodes.EncodePark(SamplePark());
            char last = code[code.Length - 1];
            string broken = code.Substring(0, code.Length - 1) + (last == 'A' ? 'B' : 'A');
            Assert.IsFalse(ShareCodes.TryDecodePark(broken, out _, out var error));
            Assert.IsNotNull(error);
            Assert.IsFalse(ShareCodes.TryDecodePark("hello there", out _, out _));
            Assert.IsFalse(ShareCodes.TryDecodePark("", out _, out _));
        }

        [Test]
        public void ChallengeCode_RoundTrips_ForBuiltInAndCustomParks()
        {
            var c = new ScoreChallenge { LocationId = "sunset_bowls", Target = 25430, From = "Omari" };
            string code = ShareCodes.EncodeChallenge(c);
            StringAssert.StartsWith("RC-", code);
            Assert.IsTrue(ShareCodes.TryDecodeChallenge(code, out var back, out var error), error);
            Assert.AreEqual("sunset_bowls", back.LocationId);
            Assert.IsNull(back.Park);
            Assert.AreEqual(25430, back.Target);
            Assert.AreEqual("OMARI", back.From);

            var custom = new ScoreChallenge { Park = SamplePark(), Target = 9001, From = "DOCK RAT" };
            Assert.IsTrue(ShareCodes.TryDecodeChallenge(ShareCodes.EncodeChallenge(custom), out back, out error), error);
            Assert.IsNotNull(back.Park);
            Assert.IsTrue(CustomParkIds.IsCustom(back.Park.id));
            Assert.AreEqual(SamplePark().pieces.Count, back.Park.pieces.Count);
            Assert.AreEqual(9001, back.Target);
        }

        [Test]
        public void WrongKindOfCode_SaysSo()
        {
            string park = ShareCodes.EncodePark(SamplePark());
            Assert.IsFalse(ShareCodes.TryDecodeChallenge(park, out _, out var error));
            StringAssert.Contains("PARK CODE", error);
            Assert.AreEqual(ShareCodes.ParkPrefix, ShareCodes.KindOf(park));
        }

        [Test]
        public void FullPark_CodeStaysPasteable()
        {
            var park = CustomPark.Create(CustomParkIds.ForSlot(1), "FULL");
            for (int i = 0; i < CustomPark.MaxPieces; i++) park.Add(PieceKind.Bench, 20, 20);
            string code = ShareCodes.EncodePark(park);
            Assert.Less(code.Length, 360);
            Assert.IsTrue(ShareCodes.TryDecodePark(code, out var back, out _));
            Assert.AreEqual(CustomPark.MaxPieces, back.pieces.Count);
        }
    }
}
