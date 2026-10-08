using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class NightSessionTests
    {
        [Test]
        public void EveryBuiltInPark_ButTheCity_HasANightLine()
        {
            Assert.AreEqual(10, NightSessions.ParkCount);
            Assert.IsFalse(NightSessions.HasLine("retro_city"), "the city keeps its own clock");
            Assert.IsFalse(NightSessions.HasLine("custom_1"));
            foreach (var id in NightSessions.Parks)
            {
                long t = NightSessions.TargetFor(id);
                Assert.Greater(t, 10000L, id);
                Assert.AreEqual(0L, t % 1000L, id + " rounds to the thousand");
            }
            Assert.Greater(NightSessions.TargetFor("subway_depot"), NightSessions.TargetFor("harbor_plaza"), "harder parks ask more");
        }

        [Test]
        public void NightLine_PaysOnce_AndOnlyAtNight()
        {
            var done = new List<string>();
            long t = NightSessions.TargetFor("shipyard");
            Assert.IsFalse(NightSessions.Clears("shipyard", ParkTime.Day, t * 2, done), "daylight doesn't count");
            Assert.IsFalse(NightSessions.Clears("shipyard", ParkTime.Dusk, t * 2, done), "dusk doesn't count");
            Assert.IsFalse(NightSessions.Clears("shipyard", ParkTime.Night, t - 1, done));
            Assert.IsTrue(NightSessions.Clears("shipyard", ParkTime.Night, t, done));
            done.Add("shipyard");
            Assert.IsFalse(NightSessions.Clears("shipyard", ParkTime.Night, t * 3, done), "only the first time");
        }

        [Test]
        public void LessonsDuelsReplaysAndTheCity_StayInDaylight()
        {
            Assert.AreEqual(ParkTime.Night, NightSessions.Effective(ParkTime.Night, false, false, false));
            Assert.AreEqual(ParkTime.Day, NightSessions.Effective(ParkTime.Night, true, false, false));
            Assert.AreEqual(ParkTime.Day, NightSessions.Effective(ParkTime.Night, false, true, false));
            Assert.AreEqual(ParkTime.Day, NightSessions.Effective(ParkTime.Dusk, false, false, true));
            Assert.AreEqual(ParkTime.Day, NightSessions.FromSaved(99), "a bad saved value is daytime");
            Assert.AreEqual("NIGHT", NightSessions.Label(ParkTime.Night));
        }

        [Test]
        public void NightMix_IsSlowerSofterAndStillALoop()
        {
            var day = MusicComposer.Harbor;
            var night = NightSessions.NightMix(day);
            Assert.AreEqual(day.Name + "_night", night.Name);
            Assert.Less(night.Bpm, day.Bpm);
            Assert.Less(night.Drive, day.Drive);
            Assert.GreaterOrEqual(night.Pad, day.Pad);
            CollectionAssert.AreEqual(day.Progression, night.Progression, "same chords");
            Assert.IsFalse(ReferenceEquals(day.Progression, night.Progression), "a copy");
            var samples = MusicComposer.Render(night, 8000);
            Assert.AreEqual(MusicComposer.LoopSamples(night, 8000), samples.Length);
            Assert.Greater(samples.Max(x => Math.Abs(x)), 0.01f, "not silent");
            Assert.LessOrEqual(samples.Max(x => Math.Abs(x)), 1f, "no clipping");
            Assert.AreEqual(MusicScale.Dorian, NightSessions.NightMix(new SongSpec { Scale = MusicScale.Major }).Scale);
        }

        [Test]
        public void NightStickers_UnlockAtOneFiveAndEveryPark()
        {
            var moon = Stickers.Find(13);
            var lamp = Stickers.Find(14);
            var skyline = Stickers.Find(15);
            Assert.AreEqual("MOON", moon.Name);
            Assert.IsFalse(moon.UnlockedFor(99, 99, 0));
            Assert.IsTrue(moon.UnlockedFor(0, 0, 1));
            Assert.IsFalse(lamp.UnlockedFor(0, 0, 4));
            Assert.IsTrue(lamp.UnlockedFor(0, 0, 5));
            Assert.IsFalse(skyline.UnlockedFor(0, 0, NightSessions.ParkCount - 1));
            Assert.IsTrue(skyline.UnlockedFor(0, 0, NightSessions.ParkCount));
            Assert.AreEqual(NightSessions.ParkCount, skyline.UnlockNightLines);
            Assert.IsTrue(Stickers.Find(16).UnlockedFor(0, 0), "designer shapes are free");
        }
    }

    public class DeckDesignTests
    {
        private static BoardArt Plain() => new BoardArt { pattern = (int)BoardPattern.Solid, primary = 0, secondary = 1, sticker = 0 };

        [Test]
        public void ColorAt_StacksPatternStickersThenPaint()
        {
            var art = Plain();
            Assert.AreEqual(0, DeckDesign.ColorAt(art, 3, 3), "solid deck");
            art.layers.Add(new StickerLayer { sticker = 16, color = 5, x = 0, y = 0 }); // CIRCLE fills the middle of its 8x8
            Assert.AreEqual(5, DeckDesign.ColorAt(art, 3, 3));
            art.layers.Add(new StickerLayer { sticker = 16, color = 6, x = 0, y = 0 });
            Assert.AreEqual(6, DeckDesign.ColorAt(art, 3, 3), "later layers sit on top");
            Assert.IsTrue(DeckDesign.Paint(art, 3, 3, 2));
            Assert.AreEqual(DeckDesign.PaintColor(art, 1), DeckDesign.ColorAt(art, 3, 3), "paint is on top of everything");
            Assert.IsFalse(DeckDesign.Paint(art, 3, 3, 2), "no change, no edit");
            Assert.IsTrue(DeckDesign.Paint(art, 3, 3, 0));
            Assert.AreEqual(6, DeckDesign.ColorAt(art, 3, 3), "erasing shows what's under");
            Assert.AreEqual("", art.paint, "an empty paint layer stores nothing");
        }

        [Test]
        public void BigStickers_AreTheFullDeckWidth_AndStayOnTheBoard()
        {
            var l = new StickerLayer { sticker = 16, x = 6, y = 60 };
            DeckDesign.Move(l, 0, 0);
            Assert.AreEqual(56, l.y, "kept on the deck");
            DeckDesign.ToggleBig(l);
            Assert.IsTrue(l.big);
            Assert.AreEqual(0, l.x, "a big sticker is 16 wide, so it starts at the edge");
            Assert.LessOrEqual(l.y + l.Size, DeckDesign.Height);
            int ink = 0;
            for (int y = 0; y < DeckDesign.Height; y++)
            for (int x = 0; x < DeckDesign.Width; x++)
                if (DeckDesign.LayerInk(l, x, y)) ink++;
            int small = 0;
            foreach (var row in Stickers.Find(16).Rows) small += row.Count(c => c == '#');
            Assert.AreEqual(small * 4, ink, "double size is four times the ink");
        }

        [Test]
        public void Turning_FourTimes_IsTheSame_AndHalfTurnIsBothMirrors()
        {
            var a = new StickerLayer { sticker = 3, x = 4, y = 20 }; // BOLT isn't symmetric
            var half = new StickerLayer { sticker = 3, x = 4, y = 20, rot = 2 };
            var full = new StickerLayer { sticker = 3, x = 4, y = 20, rot = 4 };
            bool anyDifferent = false;
            for (int y = 20; y < 28; y++)
            for (int x = 4; x < 12; x++)
            {
                Assert.AreEqual(DeckDesign.LayerInk(a, x, y), DeckDesign.LayerInk(full, x, y));
                // A half turn maps (x, y) to the opposite corner.
                Assert.AreEqual(DeckDesign.LayerInk(a, x, y), DeckDesign.LayerInk(half, 4 + 11 - x, 20 + 27 - y));
                if (DeckDesign.LayerInk(a, x, y) != DeckDesign.LayerInk(new StickerLayer { sticker = 3, x = 4, y = 20, rot = 1 }, x, y)) anyDifferent = true;
            }
            Assert.IsTrue(anyDifferent, "a quarter turn changes the bolt");
        }

        [Test]
        public void Fill_ColoursTheTouchingArea_Only()
        {
            var art = Plain();
            for (int y = 0; y < DeckDesign.Height; y++) DeckDesign.Paint(art, 8, y, 1); // a wall down the middle
            int filled = DeckDesign.Fill(art, 0, 0, 2);
            Assert.AreEqual(8 * DeckDesign.Height, filled, "the left half");
            Assert.AreEqual(2, DeckDesign.PaintAt(art, 7, 40));
            Assert.AreEqual(0, DeckDesign.PaintAt(art, 9, 40), "the wall stops it");
            Assert.AreEqual(0, DeckDesign.Fill(art, 0, 0, 2), "same colour, nothing to do");
            DeckDesign.ClearPaint(art);
            Assert.AreEqual(0, DeckDesign.PaintedCount(art));
        }

        [Test]
        public void Sanitize_FixesWhatASaveCouldBreak()
        {
            var art = Plain();
            art.layers = null;
            art.paintColors = new[] { 1 };
            art.paint = "9999x";
            DeckDesign.Sanitize(art);
            Assert.IsNotNull(art.layers);
            Assert.AreEqual(DeckDesign.PaintColors, art.paintColors.Length);
            Assert.AreEqual("", art.paint, "junk paint is cleared");
            for (int i = 0; i < 7; i++) art.layers.Add(new StickerLayer { sticker = 500, color = -2, x = 99, y = -5, rot = 9 });
            art.layers.Add(null);
            DeckDesign.Sanitize(art);
            Assert.AreEqual(DeckDesign.MaxLayers, art.layers.Count);
            foreach (var l in art.layers)
            {
                Assert.Less(l.sticker, Stickers.All.Length);
                Assert.GreaterOrEqual(l.color, 0);
                Assert.LessOrEqual(l.x + l.Size, DeckDesign.Width);
                Assert.GreaterOrEqual(l.y, 0);
                Assert.Less(l.rot, 4);
            }
        }

        [Test]
        public void Clone_IsDeep()
        {
            var art = Plain();
            art.layers.Add(new StickerLayer { sticker = 2 });
            var copy = art.Clone();
            copy.layers[0].sticker = 5;
            copy.paintColors[0] = 9;
            Assert.AreEqual(2, art.layers[0].sticker);
            Assert.AreNotEqual(9, art.paintColors[0]);
            var look = new SkaterLook { customGrip = true };
            var lc = look.Clone();
            lc.grip.pattern = 5;
            Assert.AreNotEqual(5, look.grip.pattern);
        }

        [Test]
        public void Key_ChangesWithEveryVisibleEdit()
        {
            var art = Plain();
            var keys = new HashSet<string> { art.Key };
            art.layers.Add(new StickerLayer());
            Assert.IsTrue(keys.Add(art.Key));
            art.layers[0].flip = true;
            Assert.IsTrue(keys.Add(art.Key));
            DeckDesign.Paint(art, 1, 1, 1);
            Assert.IsTrue(keys.Add(art.Key));
            art.paintColors[0] = 7;
            Assert.IsTrue(keys.Add(art.Key));
        }

        [Test]
        public void Codes_RoundTrip_AWholePaintedBoard()
        {
            var art = new BoardArt { pattern = (int)BoardPattern.Bolt, primary = 5, secondary = 9, sticker = 7, stickerColor = 2, stickerSpot = 2 };
            art.layers.Add(new StickerLayer { sticker = 13, color = 4, x = 8, y = 50, rot = 3, flip = true });
            art.layers.Add(new StickerLayer { sticker = 19, color = 11, x = 0, y = 10, big = true });
            art.paintColors = new[] { 3, 4, 5, 6 };
            var r = new Random(7);
            for (int i = 0; i < 300; i++) DeckDesign.Paint(art, r.Next(16), r.Next(64), r.Next(5));
            var grip = new GripArt { pattern = (int)GripPattern.Frame, color = 8, accent = 1 };
            string code = DeckDesign.Encode(art, grip, "OMARI");
            StringAssert.StartsWith(DeckDesign.CodePrefix, code);
            Assert.LessOrEqual(code.Length, DeckDesign.MaxCodeChars);
            Assert.IsTrue(DeckDesign.TryDecode(" " + code + "\n", out var back, out var g, out var from, out var error), error);
            Assert.AreEqual("OMARI", from);
            Assert.AreEqual(art.Key, back.Key, "every pixel decision survives");
            for (int y = 0; y < 64; y++) for (int x = 0; x < 16; x++) Assert.AreEqual(DeckDesign.ColorAt(art, x, y), DeckDesign.ColorAt(back, x, y));
            Assert.AreEqual(grip.pattern, g.pattern);
            Assert.AreEqual(grip.accent, g.accent);

            Assert.IsTrue(DeckDesign.TryDecode(DeckDesign.Encode(Plain(), null, ""), out _, out var noGrip, out _, out _));
            Assert.IsNull(noGrip, "a board shared without its grip keeps the friend's own grip");
        }

        [Test]
        public void FullyRandomPaint_StillFitsInACode()
        {
            var art = Plain();
            var r = new Random(1);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 16; x++) DeckDesign.Paint(art, x, y, r.Next(5));
            for (int i = 0; i < DeckDesign.MaxLayers; i++) art.layers.Add(new StickerLayer { sticker = 4 });
            string code = DeckDesign.Encode(art, new GripArt(), "ABCDEFGHIJKL");
            Assert.LessOrEqual(code.Length, DeckDesign.MaxCodeChars, "worst case: " + code.Length);
            Assert.IsTrue(DeckDesign.TryDecode(code, out var back, out _, out _, out var error), error);
            Assert.AreEqual(art.paint, back.paint);
        }

        [Test]
        public void BadCodes_AreRefused()
        {
            string code = DeckDesign.Encode(Plain(), null, "");
            Assert.IsFalse(DeckDesign.TryDecode(code.Substring(0, code.Length - 2), out _, out _, out _, out _));
            Assert.IsFalse(DeckDesign.TryDecode("RT:" + code.Substring(3), out _, out _, out _, out _));
            Assert.IsFalse(DeckDesign.TryDecode(new string('A', DeckDesign.MaxCodeChars + 10), out _, out _, out _, out var tooLong));
            StringAssert.Contains("TOO LONG", tooLong);
            Assert.IsFalse(DeckDesign.TryDecode("", out _, out _, out _, out _));
        }

        [Test]
        public void LockedStickers_FromAFriendsBoard_BecomeStars()
        {
            var art = Plain();
            art.sticker = 9;                                         // SKULL: chapter 8
            art.layers.Add(new StickerLayer { sticker = 15 });       // SKYLINE: every night line
            art.layers.Add(new StickerLayer { sticker = 2 });        // HEART: free
            int swapped = DeckDesign.ReplaceLocked(art, s => s.UnlockedFor(0, 0, 0));
            Assert.AreEqual(2, swapped);
            Assert.AreEqual(1, art.sticker);
            Assert.AreEqual(1, art.layers[0].sticker);
            Assert.AreEqual(2, art.layers[1].sticker);
        }

        [Test]
        public void GripPatterns_AllShowTheirAccent_ExceptSolid()
        {
            for (int p = 0; p < DeckDesign.GripNames.Length; p++)
            {
                int accent = 0;
                for (int y = 0; y < 64; y++) for (int x = 0; x < 16; x++) if (DeckDesign.GripAccent((GripPattern)p, x, y)) accent++;
                if (p == (int)GripPattern.Solid) Assert.AreEqual(0, accent);
                else
                {
                    Assert.Greater(accent, 0, DeckDesign.GripNames[p]);
                    Assert.Less(accent, 16 * 64 / 2, DeckDesign.GripNames[p] + " is mostly tape");
                }
            }
        }

        [Test]
        public void OldBoards_LookExactlyAsTheyDid()
        {
            // Before Phase 28 a board was pattern + sticker; ColorAt must agree with BoardPainter for those.
            var art = new BoardArt { pattern = (int)BoardPattern.Chevron, primary = 3, secondary = 4, sticker = 5, stickerColor = 7, stickerSpot = 0 };
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 16; x++)
            {
                int slot = BoardPainter.Pixel(art, x, y);
                int expected = slot == BoardPainter.Primary ? 3 : slot == BoardPainter.Secondary ? 4 : 7;
                Assert.AreEqual(expected, DeckDesign.ColorAt(art, x, y));
            }
        }
    }

    public class LocalizationTests
    {
        private static List<KeyValuePair<string, string>> T(params string[] pairs)
        {
            var list = new List<KeyValuePair<string, string>>();
            for (int i = 0; i + 1 < pairs.Length; i += 2) list.Add(new KeyValuePair<string, string>(pairs[i], pairs[i + 1]));
            return list;
        }

        private static readonly List<KeyValuePair<string, string>> Table = T(
            "Harbor Plaza", "Plaza del Puerto",
            "BEST {0}", "MEJOR {0}",
            "BACK", "ATRÁS",
            "Score more than {0} at {1}", "Supera {0} en {1}",
            "CHALLENGE FROM {0}\nBEAT {1}", "RETO DE {0}\nSUPERA {1}",
            "NEW BEST!", "¡NUEVO RÉCORD!",
            "LAND A BIG ONE AT", "CLAVA UNO GRANDE EN",
            "{0} {1}", "nope");

        [Test]
        public void ExactCapsAndUnknown()
        {
            Assert.AreEqual("Plaza del Puerto", Loc.TranslateWithTable(Table, Lang.Spanish, "Harbor Plaza"));
            Assert.AreEqual("PLAZA DEL PUERTO", Loc.TranslateWithTable(Table, Lang.Spanish, "HARBOR PLAZA"), "shown in capitals, comes back in capitals");
            Assert.AreEqual("  ATRÁS ", Loc.TranslateWithTable(Table, Lang.Spanish, "  BACK "), "spacing kept");
            Assert.AreEqual("SOMETHING NEW", Loc.TranslateWithTable(Table, Lang.Spanish, "SOMETHING NEW"), "unknown text stays English");
            Assert.AreEqual("12,400", Loc.TranslateWithTable(Table, Lang.Spanish, "12,400"), "numbers alone are left alone");
        }

        [Test]
        public void Templates_FillAndTranslateTheirParts()
        {
            Assert.AreEqual("MEJOR 12,400", Loc.TranslateWithTable(Table, Lang.Spanish, "BEST 12,400"));
            Assert.AreEqual("Supera 9,000 en Plaza del Puerto", Loc.TranslateWithTable(Table, Lang.Spanish, "Score more than 9,000 at Harbor Plaza"), "a known park name inside is translated too");
            Assert.AreEqual("SUPERA 9,000 EN PLAZA DEL PUERTO", Loc.TranslateWithTable(Table, Lang.Spanish, "SCORE MORE THAN 9,000 AT HARBOR PLAZA"), "a mixed-case template matches the same text in capitals");
            Assert.AreEqual("RETO DE OMARI\nSUPERA 5,000", Loc.TranslateWithTable(Table, Lang.Spanish, "CHALLENGE FROM OMARI\nBEAT 5,000"), "templates span lines");
            Assert.AreEqual("A B", Loc.TranslateWithTable(Table, Lang.Spanish, "A B"), "a template that is only placeholders is ignored");
        }

        [Test]
        public void LinesPartsAndGluedText()
        {
            Assert.AreEqual("¡NUEVO RÉCORD!\nATRÁS", Loc.TranslateWithTable(Table, Lang.Spanish, "NEW BEST!\nBACK"));
            Assert.AreEqual("PLAZA DEL PUERTO  ·  MEJOR 300", Loc.TranslateWithTable(Table, Lang.Spanish, "HARBOR PLAZA  ·  BEST 300"));
            Assert.AreEqual("CLAVA UNO GRANDE EN PLAZA DEL PUERTO", Loc.TranslateWithTable(Table, Lang.Spanish, "LAND A BIG ONE AT HARBOR PLAZA"), "a known phrase glued to a name");
            Assert.AreEqual("¡NUEVO RÉCORD! OMARI", Loc.TranslateWithTable(Table, Lang.Spanish, "NEW BEST! OMARI"));
            Assert.AreEqual("BACK OMARI", Loc.TranslateWithTable(Table, Lang.Spanish, "BACK OMARI"), "a single common word doesn't half-translate a sentence");
        }

        [Test]
        public void Decorations_AndStrictCoverage()
        {
            var t = T("NIGHT LINE: {0} IN ONE COMBO AT NIGHT", "LÍNEA NOCTURNA: {0} EN UN COMBO DE NOCHE",
                      "Grind three separate rails", "Grindea tres barandillas distintas", "TIME: {0}", "TIEMPO: {0}", "NIGHT", "NOCHE");
            Assert.AreEqual("□ LÍNEA NOCTURNA: 12,000 EN UN COMBO DE NOCHE", Loc.TranslateWithTable(t, Lang.Spanish, "□ NIGHT LINE: 12,000 IN ONE COMBO AT NIGHT"), "a leading box");
            Assert.AreEqual("□ Grindea tres barandillas distintas  1/3", Loc.TranslateWithTable(t, Lang.Spanish, "□ Grind three separate rails  1/3"), "a box and a trailing count");
            Assert.AreEqual("TIEMPO: NOCHE", Loc.TranslateWithTable(t, Lang.Spanish, "TIME: NIGHT"));
            Assert.IsTrue(Loc.KnowsWithTable(t, Lang.Spanish, "TIME: NIGHT"));
            Assert.IsTrue(Loc.KnowsWithTable(t, Lang.Spanish, "TIME: OMARI"), "a placeholder's value needn't be known");
            Assert.IsFalse(Loc.KnowsWithTable(t, Lang.Spanish, "NIGHT\nSOMETHING NEW"), "half-translated text isn't covered");
            Assert.AreEqual("NOCHE\nSOMETHING NEW", Loc.TranslateWithTable(t, Lang.Spanish, "NIGHT\nSOMETHING NEW"), "...but still shows what it can");
        }

        [Test]
        public void Japanese_IsNeverUpperCased()
        {
            var ja = T("Harbor Plaza", "ハーバープラザ", "BEST {0}", "ベスト {0}");
            Assert.AreEqual("ハーバープラザ", Loc.TranslateWithTable(ja, Lang.Japanese, "HARBOR PLAZA"));
            Assert.AreEqual("ベスト 99", Loc.TranslateWithTable(ja, Lang.Japanese, "BEST 99"));
        }

        [Test]
        public void TheChoice_OrThePhone_PicksTheLanguage()
        {
            Assert.AreEqual(Lang.Spanish, Loc.Resolve(-1, "es"));
            Assert.AreEqual(Lang.Spanish, Loc.Resolve(-1, "es-MX"));
            Assert.AreEqual(Lang.Japanese, Loc.Resolve(-1, "ja-JP"));
            Assert.AreEqual(Lang.English, Loc.Resolve(-1, "fr"));
            Assert.AreEqual(Lang.English, Loc.Resolve(0, "ja"), "a saved choice beats the phone");
            Assert.AreEqual(Lang.Japanese, Loc.Resolve(2, "en"));
            Assert.AreEqual(Lang.English, Loc.Resolve(7, "en"), "a bad saved value follows the phone");
        }

        [Test]
        public void ParseReadsTheTableFormat()
        {
            var rows = Loc.Parse("A\tB\r\nno tab here\n\nLINE\\nTWO\tUNO\\nDOS\n");
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("LINE\nTWO", rows[1].Key);
            Assert.AreEqual("UNO\nDOS", rows[1].Value);
        }

        [Test]
        public void RealTables_AreCompleteAndWellFormed()
        {
            foreach (var lang in new[] { Lang.Spanish, Lang.Japanese })
            {
                var rows = Loc.Parse(LocTables.For(lang));
                Assert.Greater(rows.Count, 1500f, lang + " table size");
                var keys = new HashSet<string>();
                var ph = new System.Text.RegularExpressions.Regex(@"\{(\d)\}");
                foreach (var kv in rows)
                {
                    Assert.IsTrue(keys.Add(kv.Key), lang + " duplicate: " + kv.Key);
                    Assert.IsFalse(kv.Value.Contains("\t"), lang + " tab in " + kv.Key);
                    foreach (System.Text.RegularExpressions.Match m in ph.Matches(kv.Value))
                        StringAssert.Contains(m.Value, kv.Key, lang + " invented placeholder");
                }
            }
        }

        [Test]
        public void RealTables_TranslateTheMenus()
        {
            var es = Loc.Parse(LocTables.For(Lang.Spanish));
            var ja = Loc.Parse(LocTables.For(Lang.Japanese));
            foreach (var s in new[] { "SETTINGS", "TWO-MINUTE RUN", "FREE SKATE", "PICK A SPOT", "DECK DESIGNER", "NIGHT LINE!  +{0}" })
            {
                Assert.AreNotEqual(s, Loc.TranslateWithTable(es, Lang.Spanish, s), "es: " + s);
                Assert.AreNotEqual(s, Loc.TranslateWithTable(ja, Lang.Japanese, s), "ja: " + s);
            }
            string best = Loc.TranslateWithTable(es, Lang.Spanish, "NIGHT LINE!  +250");
            StringAssert.Contains("250", best);
            Assert.AreNotEqual("NIGHT LINE!  +250", best);
            Assert.AreEqual("12:34", Loc.TranslateWithTable(ja, Lang.Japanese, "12:34"));
        }
    }
}
