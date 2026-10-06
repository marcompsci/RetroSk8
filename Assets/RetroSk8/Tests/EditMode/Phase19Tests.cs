using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    /// <summary>Phase 19 security: input limits, fuzzing every decoder, the gallery, anti-cheat, the save seal, purchases, duels.</summary>
    public class CodeLimitTests
    {
        internal static CustomPark FullPark()
        {
            var park = CustomPark.Create(CustomParkIds.ForSlot(1), "ABCDEFGHIJKLMNOPQR");
            var kinds = (PieceKind[])Enum.GetValues(typeof(PieceKind));
            for (int z = 0; z < CustomPark.GridCells && park.pieces.Count < CustomPark.MaxPieces; z += 3)
                for (int x = 0; x < CustomPark.GridCells && park.pieces.Count < CustomPark.MaxPieces; x += 3)
                    park.Add(kinds[(x + z) % kinds.Length] == PieceKind.Bowl ? PieceKind.Ledge : kinds[(x + z) % kinds.Length], x, z, (x / 3) % 4, 1, 1, 1);
            return park;
        }

        internal static ScoreChallenge LongestGhost(CustomPark park)
        {
            var track = new ReplayTrack(GhostCodes.SampleRate);
            var rng = new Random(7);
            int samples = (int)(GhostCodes.MaxSeconds * GhostCodes.SampleRate);
            for (int i = 0; i < samples; i++)
            {
                float t = i / GhostCodes.SampleRate;
                // Big jumps every few samples force absolute positions, the most expensive encoding.
                float far = i % 4 == 0 ? (float)rng.NextDouble() * 4000f : 0f;
                var q = new RQuat((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, 1f);
                track.Add(new ReplayFrame
                {
                    Time = t,
                    Position = new RVec3(far + (float)rng.NextDouble() * 15f, (float)rng.NextDouble() * 10f, far),
                    Rotation = q, Pose = q, Body = q, Board = q,
                    BoardPosition = new RVec3(1.9f, -1.9f, 1.9f),
                });
            }
            track.Score = 9999999;
            var banks = new List<GhostBank>();
            for (int i = 0; i < GhostCodes.MaxBanks; i++) banks.Add(new GhostBank(i * 0.5f, 99999));
            return new ScoreChallenge { Park = park, Target = 9999999, From = "ABCDEFGHIJKL", Ghost = track, GhostBanks = banks };
        }

        [Test]
        public void TheBiggestCodesTheGameWrites_FitUnderTheLimits()
        {
            var park = FullPark();
            Assert.Greater(park.pieces.Count, 40, "a big park");
            string parkCode = ShareCodes.EncodePark(park);
            Assert.Less(parkCode.Length, CodeLimits.MaxParkCodeChars, "park code " + parkCode.Length);
            string challenge = ShareCodes.EncodeChallenge(new ScoreChallenge { Park = park, Target = 1 << 29, From = "ABCDEFGHIJKL" });
            Assert.Less(challenge.Length, CodeLimits.MaxParkCodeChars, "challenge code " + challenge.Length);
            string ghost = GhostCodes.Encode(LongestGhost(park));
            Assert.Less(ghost.Length, CodeLimits.MaxGhostCodeChars, "ghost code " + ghost.Length);
            Assert.IsTrue(GhostCodes.TryDecode(ghost, out var back, out string err), err);
            Assert.LessOrEqual(back.Ghost.Count, CodeLimits.MaxGhostSamples);
        }

        [Test]
        public void OversizedInput_IsRefusedBeforeDecoding()
        {
            string huge = "RP-" + new string('A', CodeLimits.MaxParkCodeChars + 10);
            Assert.IsFalse(ShareCodes.TryDecodePark(huge, out _, out string e1));
            StringAssert.Contains("TOO LONG", e1);
            Assert.IsFalse(ShareCodes.TryDecodeChallenge("RC" + new string('B', 5000), out _, out _));
            Assert.IsFalse(GhostCodes.TryDecode(GhostCodes.Prefix + new string('A', CodeLimits.MaxGhostCodeChars + 1), out _, out string e2));
            StringAssert.Contains("TOO LONG", e2);
            Assert.IsNull(ShareCodes.KindOf(new string('R', CodeLimits.MaxGhostCodeChars + 1)));
        }

        [Test]
        public void RecordIds_MustBePlain()
        {
            Assert.IsTrue(CodeLimits.IsSafeRecordId("A1b2-C3_d4.e5:f6"));
            Assert.IsFalse(CodeLimits.IsSafeRecordId(""));
            Assert.IsFalse(CodeLimits.IsSafeRecordId("has space"));
            Assert.IsFalse(CodeLimits.IsSafeRecordId("tab\there"));
            Assert.IsFalse(CodeLimits.IsSafeRecordId("../../etc"));
            Assert.IsFalse(CodeLimits.IsSafeRecordId(new string('a', CodeLimits.MaxRecordIdLength + 1)));
            Assert.AreEqual(1577836800L, CodeLimits.ClampCreated(5, 1800000000L));
            Assert.AreEqual(1800000000L, CodeLimits.ClampCreated(9000000000L, 1800000000L));
        }
    }

    public class FuzzTests
    {
        private static string RandomText(Random rng, int max, string alphabet)
        {
            int n = rng.Next(max);
            var sb = new StringBuilder(n);
            for (int i = 0; i < n; i++) sb.Append(alphabet[rng.Next(alphabet.Length)]);
            return sb.ToString();
        }

        private const string Wild = "RPCG:-_\t\n 0123456789ABCDEFGHJKMNPQRSTVWXYZabcdefxyz/+=é漢\u0000￿";

        [Test]
        public void EveryDecoder_SurvivesGarbage()
        {
            var rng = new Random(19);
            for (int i = 0; i < 3000; i++)
            {
                string junk = RandomText(rng, 400, Wild);
                Assert.DoesNotThrow(() => ShareCodes.TryDecodePark(junk, out _, out _), junk);
                Assert.DoesNotThrow(() => ShareCodes.TryDecodeChallenge("RC" + junk, out _, out _));
                Assert.DoesNotThrow(() => ShareCodes.TryDecodePark("RP" + junk, out _, out _));
                Assert.DoesNotThrow(() => GhostCodes.TryDecode(GhostCodes.Prefix + junk, out _, out _));
                Assert.DoesNotThrow(() => Gallery.Parse(junk));
                Assert.DoesNotThrow(() => FriendScore.Parse(junk));
                Assert.DoesNotThrow(() => BoardPage.Parse(junk));
                Assert.DoesNotThrow(() => Shop.ParseProductList(junk));
                Assert.DoesNotThrow(() => StoreEvent.TryParse(junk, out _));
                Assert.DoesNotThrow(() => Gallery.IsBlocked(junk));
            }
        }

        [Test]
        public void DamagedRealCodes_AreRejectedOrSanitized_NeverThrow()
        {
            var rng = new Random(23);
            var park = CodeLimitTests.FullPark();
            string parkCode = ShareCodes.EncodePark(park);
            string ghost = GhostCodes.Encode(CodeLimitTests.LongestGhost(park));
            for (int i = 0; i < 1500; i++)
            {
                var p = new StringBuilder(parkCode);
                for (int k = 0; k < 1 + rng.Next(4); k++) p[3 + rng.Next(p.Length - 3)] = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"[rng.Next(32)];
                CustomPark decoded = null;
                Assert.DoesNotThrow(() => ShareCodes.TryDecodePark(p.ToString(), out decoded, out _));
                if (decoded != null)
                {
                    Assert.LessOrEqual(decoded.pieces.Count, CustomPark.MaxPieces);
                    Assert.LessOrEqual(decoded.name.Length, CustomPark.MaxNameLength);
                }
                var g = new StringBuilder(ghost);
                g[GhostCodes.Prefix.Length + rng.Next(g.Length - GhostCodes.Prefix.Length)] = 'Q';
                Assert.DoesNotThrow(() => GhostCodes.TryDecode(g.ToString(), out _, out _));
                // Cut short anywhere.
                Assert.DoesNotThrow(() => GhostCodes.TryDecode(ghost.Substring(0, rng.Next(ghost.Length)), out _, out _));
            }
        }

        [Test]
        public void DuelParser_SurvivesRandomBytes_AndSanitizeNeverPassesBadFrames()
        {
            var rng = new Random(29);
            for (int i = 0; i < 5000; i++)
            {
                var bytes = new byte[rng.Next(DuelGuard.MaxMessageBytes + 40)];
                rng.NextBytes(bytes);
                if (bytes.Length > 1) { bytes[0] = DuelMessage.ProtocolVersion; bytes[1] = (byte)(1 + rng.Next(8)); }
                DuelMessage m = null;
                Assert.DoesNotThrow(() => DuelMessage.TryParse(bytes, out m));
                if (m == null) continue;
                Assert.LessOrEqual(bytes.Length, DuelGuard.MaxMessageBytes);
                bool ok = false;
                Assert.DoesNotThrow(() => ok = DuelGuard.Sanitize(m));
                if (!ok) continue;
                if (m.Type == DuelMessageType.Frame) Assert.IsTrue(DuelGuard.FrameOk(m.Frame));
                if (m.Type == DuelMessageType.AttemptResult) Assert.LessOrEqual(m.Points, ScoreLimits.MaxCombo);
                if (m.Type == DuelMessageType.Hello) Assert.LessOrEqual(m.Name.Length, DuelGuard.MaxNameChars);
            }
        }
    }

    public class GalleryLockdownTests
    {
        [Test]
        public void Parse_ChecksEveryField()
        {
            long now = 1800000000L;
            string text =
                "rec-1\t0\tMY PARK\tOMARI\t12\tharbor_plaza\t1790000000\tuser_abc\n" +
                "rec-1\t0\tDUPLICATE\tX\t1\t\t1790000000\n" +                    // duplicate id
                "bad id\t0\tX\tX\t1\t\t1790000000\n" +                           // unsafe id
                "rec-2\t1\t" + new string('N', 500) + "\tA\t999999999999\tnowhere_park\t99999999999\tbad user\n" +
                "rec-3\t7\tX\tX\t1\t\t1\n";                                       // unknown kind
            var list = Gallery.Parse(text, now);
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("user_abc", list[0].Creator);
            Assert.AreEqual("harbor_plaza", list[0].Location);
            var ghost = list[1];
            Assert.AreEqual(CodeLimits.MaxRawNameLength, ghost.Name.Length, "names are capped");
            Assert.AreEqual(ScoreLimits.MaxRunScore, ghost.Detail, "scores are capped");
            Assert.AreEqual("", ghost.Location, "unknown parks are dropped");
            Assert.AreEqual("", ghost.Creator, "unsafe creator ids are dropped");
            Assert.AreEqual(now, ghost.Created, "future dates are pulled back");
        }

        [Test]
        public void Parse_ReadsOnlyAPagesWorth()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 500; i++) sb.Append($"r{i}\t0\tP\tA\t1\t\t1790000000\n");
            Assert.LessOrEqual(Gallery.Parse(sb.ToString()).Count, CodeLimits.MaxGalleryLines);
            Assert.AreEqual(0, Gallery.Parse(new string('x', CodeLimits.MaxGalleryText + 1)).Count);
        }

        [Test]
        public void WordFilter_CatchesDisguises_ButNotOrdinaryWords()
        {
            foreach (var bad in new[] { "FUUUCK", "F.U.C.K", "5H1T", "SH!T", "B1TCH", "N4Z1", "K Y S" })
                Assert.IsTrue(Gallery.IsBlocked(bad), bad);
            foreach (var ok in new[] { "MOONLIGHT PIER", "CUCUMBER", "JANUS", "SPICY RAILS", "VACUUM", "KICKFLIP KING", "DRIVE-IN" })
                Assert.IsFalse(Gallery.IsBlocked(ok), ok);
        }

        [Test]
        public void Posts_AreSpacedOut_AndNotRepeated()
        {
            var s = new GalleryState();
            long now = 1800000000L;
            Assert.AreEqual(0, Gallery.WaitBeforeUpload(s, now));
            Gallery.CountUpload(s, 100, "rec-9", "RP-ABCDE", now);
            Assert.AreEqual(Gallery.UploadSpacingSeconds, Gallery.WaitBeforeUpload(s, now));
            Assert.AreEqual(0, Gallery.WaitBeforeUpload(s, now + Gallery.UploadSpacingSeconds));
            Assert.IsTrue(Gallery.AlreadyPosted(s, "RP-ABCDE"));
            Assert.IsFalse(Gallery.AlreadyPosted(s, "RP-ABCDF"));
            Assert.AreEqual(Gallery.Fingerprint("x"), Gallery.Fingerprint("x"));
        }

        [Test]
        public void Block_UsesTheICloudCreator_SoCopiedNamesDontMatter()
        {
            var s = new GalleryState();
            var troll = new GalleryEntry { Id = "a", Author = "OMARI", Creator = "user_troll" };
            var real = new GalleryEntry { Id = "b", Author = "OMARI", Creator = "user_omari" };
            s.blockedAuthors.Add(Gallery.BlockKey(troll));
            var shown = Gallery.Visible(new[] { troll, real }, s);
            Assert.AreEqual(1, shown.Count);
            Assert.AreEqual("b", shown[0].Id, "the real OMARI still shows");
            Assert.AreEqual("OMARI", Gallery.BlockKey(new GalleryEntry { Author = "OMARI" }), "falls back to the name");
        }

        [Test]
        public void Downloads_MustMatchTheirKind()
        {
            string park = ShareCodes.EncodePark(CodeLimitTests.FullPark());
            Assert.IsNull(Gallery.CheckDownload(GalleryKind.Park, park));
            Assert.IsNotNull(Gallery.CheckDownload(GalleryKind.Ghost, park), "a park code in a ghost post");
            Assert.IsNotNull(Gallery.CheckDownload(GalleryKind.Park, ""));
            Assert.IsNotNull(Gallery.CheckDownload(GalleryKind.Park, new string('A', CodeLimits.MaxParkCodeChars + 1)));
        }
    }

    public class AntiCheatTests
    {
        [Test]
        public void Boards_HaveRanges_AndUnknownBoardsAreNeverSent()
        {
            Assert.IsTrue(ScoreLimits.IsPlausible("retrosk8.score.harbor_plaza", 250000));
            Assert.IsFalse(ScoreLimits.IsPlausible("retrosk8.score.harbor_plaza", ScoreLimits.MaxRunScore + 1));
            Assert.IsFalse(ScoreLimits.IsPlausible("retrosk8.score.harbor_plaza", 0));
            Assert.IsTrue(ScoreLimits.IsPlausible(Leaderboards.SkateWins, 12));
            Assert.IsFalse(ScoreLimits.IsPlausible("retrosk8.unknown", 5));
            Assert.IsFalse(ScoreLimits.IsPlausible(null, 5));
            Assert.AreEqual(ScoreLimits.BoardKind.Weekly, ScoreLimits.KindOf(WeeklyEvents.LeaderboardId));
        }

        [Test]
        public void RaceFloors_AreBelowEveryGoldTime_ButAboveZero()
        {
            foreach (var race in RetroCityLayout.Races)
            {
                long min = ScoreLimits.MinRaceHundredths(race);
                Assert.Greater(min, 0, race.Id);
                Assert.Less(min, (long)(race.Gold * 100f), race.Id + ": gold must stay reachable");
                Assert.IsFalse(ScoreLimits.IsPlausible(Leaderboards.Race(race.Id), min - 1), race.Id + " impossible time");
                Assert.IsTrue(ScoreLimits.IsPlausible(Leaderboards.Race(race.Id), (long)(race.Gold * 100f)), race.Id + " gold time");
            }
        }

        [Test]
        public void Runs_AreJudgedByTheirLength()
        {
            Assert.IsTrue(ScoreLimits.IsPlausibleRun(500000, 120f));
            Assert.IsFalse(ScoreLimits.IsPlausibleRun(500000, 2f));
            Assert.IsFalse(ScoreLimits.IsPlausibleRun(-1, 120f));
            Assert.AreEqual(ScoreLimits.MaxCombo, ScoreLimits.ClampCombo(long.MaxValue));
            Assert.AreEqual(0, ScoreLimits.ClampCombo(-50));
        }

        [Test]
        public void SetupList_GivesEveryBoardAScoreRange()
        {
            var boards = LeaderboardHub.Boards(new[] { ("harbor_plaza", "Harbor Plaza") });
            string md = GameCenterSetup.Markdown(boards, Achievements.All);
            StringAssert.Contains("Score range", md);
            foreach (var b in boards)
            {
                var (min, max) = ScoreLimits.RangeFor(b.Id);
                Assert.Greater(max, min, b.Id);
                StringAssert.Contains($"{min} – {max}", md);
            }
        }
    }

    public class SaveSealTests
    {
        private static readonly byte[] Key = SaveSeal.FromHex("00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff");

        [Test]
        public void ASealedSave_Checks_AndAnyEditBreaksIt()
        {
            string json = "{\"tapeTokens\": 120}";
            string seal = SaveSeal.Seal(Key, json);
            Assert.AreEqual(64, seal.Length);
            Assert.AreEqual(SaveIntegrity.Sealed, SaveSeal.Check(Key, false, json, seal));
            Assert.AreEqual(SaveIntegrity.Sealed, SaveSeal.Check(Key, false, json, seal.ToUpperInvariant()), "hex case doesn't matter");
            Assert.AreEqual(SaveIntegrity.Edited, SaveSeal.Check(Key, false, "{\"tapeTokens\": 999999}", seal));
            Assert.AreEqual(SaveIntegrity.Edited, SaveSeal.Check(Key, false, json, "deadbeef"));
            var otherKey = SaveSeal.NewKey();
            Assert.AreEqual(SaveIntegrity.Edited, SaveSeal.Check(otherKey, false, json, seal), "a different install's key");
        }

        [Test]
        public void OldSaves_AndNewKeys_AreUnsealed_NotEdited()
        {
            Assert.AreEqual(SaveIntegrity.Unsealed, SaveSeal.Check(Key, false, "{}", null), "a save from before Phase 19");
            Assert.AreEqual(SaveIntegrity.Unsealed, SaveSeal.Check(Key, true, "{}", "abcd"), "a restored phone with a fresh key");
            Assert.AreEqual(SaveIntegrity.Unknown, SaveSeal.Check(Key, false, null, "abcd"));
        }

        [Test]
        public void Keys_AreRandom_AndHexRoundTrips()
        {
            var a = SaveSeal.NewKey();
            var b = SaveSeal.NewKey();
            Assert.AreEqual(SaveSeal.KeyBytes, a.Length);
            Assert.AreNotEqual(SaveSeal.ToHex(a), SaveSeal.ToHex(b));
            Assert.AreEqual(SaveSeal.ToHex(a), SaveSeal.ToHex(SaveSeal.FromHex(SaveSeal.ToHex(a))));
            Assert.IsNull(SaveSeal.FromHex("abc"));
            Assert.IsNull(SaveSeal.FromHex("zz"));
            Assert.IsTrue(SaveSeal.FixedTimeEquals("abc", "abc"));
            Assert.IsFalse(SaveSeal.FixedTimeEquals("abc", "abd"));
            Assert.IsFalse(SaveSeal.FixedTimeEquals("abc", null));
        }
    }

    public class PurchaseCheckTests
    {
        [Test]
        public void Reconcile_MatchesTheSaveToApplesSignedRecord()
        {
            var a = Shop.Packs[0];
            var b = Shop.Packs[1];
            var owned = new List<string> { a.Id, b.Id };
            var (added, removed) = Shop.Reconcile(owned, new[] { a.ProductId, "com.someone.else.thing" });
            CollectionAssert.AreEqual(new[] { a.Id }, owned, "the unverified pack is gone");
            CollectionAssert.AreEqual(new[] { b.Id }, removed);
            Assert.AreEqual(0, added.Count, "unknown products are ignored");

            var (added2, _) = Shop.Reconcile(owned, new[] { a.ProductId, b.ProductId });
            CollectionAssert.AreEqual(new[] { b.Id }, added2, "a verified purchase missing from the save is restored");
        }

        [Test]
        public void ProductList_IsParsedDefensively()
        {
            CollectionAssert.AreEqual(new[] { "a.b", "c" }, Shop.ParseProductList(" a.b , ,c,"));
            Assert.AreEqual(0, Shop.ParseProductList(new string('x', 5000)).Count);
            Assert.AreEqual(0, Shop.ParseProductList(null).Count);
        }
    }

    public class DuelGuardTests
    {
        [Test]
        public void OversizedMessages_AreRefusedUnread()
        {
            var bytes = new byte[DuelGuard.MaxMessageBytes + 1];
            bytes[0] = DuelMessage.ProtocolVersion;
            bytes[1] = (byte)DuelMessageType.Leave;
            Assert.IsFalse(DuelMessage.TryParse(bytes, out _));
            var ok = new DuelMessage { Type = DuelMessageType.Leave }.ToBytes();
            Assert.IsTrue(DuelMessage.TryParse(ok, out _));
        }

        [Test]
        public void Sanitize_CleansNamesAndPoints_AndDropsBrokenFrames()
        {
            var hello = new DuelMessage { Type = DuelMessageType.Hello, Name = "sh1t lord 2000 \u0007", Style = 99 };
            Assert.IsTrue(DuelGuard.Sanitize(hello));
            Assert.AreEqual("SKATER", hello.Name);
            Assert.AreEqual(0, hello.Style);

            var result = new DuelMessage { Type = DuelMessageType.AttemptResult, Turn = 3, Points = long.MaxValue, Label = "KICKFLIP\n+ FIFTY" };
            Assert.IsTrue(DuelGuard.Sanitize(result));
            Assert.AreEqual(ScoreLimits.MaxCombo, result.Points);
            Assert.AreEqual("KICKFLIP+ FIFTY", result.Label);
            Assert.IsFalse(DuelGuard.Sanitize(new DuelMessage { Type = DuelMessageType.AttemptResult, Turn = -1 }));
            Assert.IsFalse(DuelGuard.Sanitize(new DuelMessage { Type = DuelMessageType.AttemptBegin, Turn = DuelGuard.MaxTurn + 1 }));

            var good = new ReplayFrame { Time = 1f, Position = new RVec3(1, 2, 3), Rotation = RQuat.Identity, Pose = RQuat.Identity, Body = RQuat.Identity, Board = RQuat.Identity };
            Assert.IsTrue(DuelGuard.FrameOk(good));
            var nan = good; nan.Position = new RVec3(float.NaN, 0, 0);
            Assert.IsFalse(DuelGuard.FrameOk(nan));
            var far = good; far.Position = new RVec3(1e9f, 0, 0);
            Assert.IsFalse(DuelGuard.FrameOk(far));
            var squashed = good; squashed.Rotation = new RQuat(0, 0, 0, 0);
            Assert.IsFalse(DuelGuard.FrameOk(squashed));
        }

        [Test]
        public void RateLimiter_AllowsABurst_ThenTheSteadyRate()
        {
            var r = new RateLimiter(10f, 5f);
            int allowed = 0;
            for (int i = 0; i < 20; i++) if (r.Allow(0.0)) allowed++;
            Assert.AreEqual(5, allowed, "the burst");
            Assert.AreEqual(15, r.Dropped);
            Assert.IsTrue(r.Allow(0.1), "one token back after a tenth of a second");
            Assert.IsFalse(r.Allow(0.1));
            Assert.IsFalse(r.Allow(0.05), "the clock going backwards doesn't refill");
        }
    }
    public class BonkWeekTests
    {
        [Test]
        public void BonkWeek_IsInTheRotation_AndCountsBonks()
        {
            var ev = Array.Find(WeeklyEvents.Rotation, e => e.Id == "bonk_week");
            Assert.IsNotNull(ev);
            Assert.AreEqual(WeeklyModifier.BonkPoints, ev.Modifier);
            Assert.IsTrue(Array.Exists(ev.Goals, g => g.Counter == WeeklyCounters.Bonks));
            var s = new WeeklyState();
            s.Count(ev, WeeklyCounters.Bonks, 15);
            Assert.AreEqual(1f, s.Progress(Array.Find(ev.Goals, g => g.Counter == WeeklyCounters.Bonks)));
        }
    }
}
