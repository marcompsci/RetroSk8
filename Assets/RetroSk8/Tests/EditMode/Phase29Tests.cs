using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    /// <summary>Phase 29: 1.0 ships English only; Spanish and Japanese wait for native review (tester preview).</summary>
    public class LangReleaseTests
    {
        [Test]
        public void OnlyEnglishShipsInOnePointZero()
        {
            Assert.IsTrue(LangRelease.IsShipped(Lang.English));
            Assert.IsFalse(LangRelease.IsShipped(Lang.Spanish));
            Assert.IsFalse(LangRelease.IsShipped(Lang.Japanese));
            Assert.IsFalse(LangRelease.IsShipped((Lang)99));
        }

        [Test]
        public void ASpanishOrJapanesePhone_GetsEnglish_UntilThePreviewIsOn()
        {
            Assert.AreEqual(Lang.English, LangRelease.Gate(Loc.Resolve(-1, "es-MX"), false));
            Assert.AreEqual(Lang.English, LangRelease.Gate(Loc.Resolve(-1, "ja-JP"), false));
            Assert.AreEqual(Lang.English, LangRelease.Gate(Loc.Resolve((int)Lang.Japanese, "en"), false), "an old saved choice too");
            Assert.AreEqual(Lang.Spanish, LangRelease.Gate(Loc.Resolve(-1, "es-MX"), true));
            Assert.AreEqual(Lang.Japanese, LangRelease.Gate(Loc.Resolve((int)Lang.Japanese, "en"), true));
        }

        [Test]
        public void ThePicker_IsHiddenWithOneLanguage_AndCyclesOnlyAvailableOnes()
        {
            Assert.IsFalse(LangRelease.ShowPicker(false));
            Assert.IsTrue(LangRelease.ShowPicker(true));
            Assert.AreEqual(0, LangRelease.NextChoice(-1, false));
            Assert.AreEqual(-1, LangRelease.NextChoice(0, false));
            Assert.AreEqual(-1, LangRelease.NextChoice(2, false), "a stale saved choice goes back to AUTO");
            Assert.AreEqual(0, LangRelease.NextChoice(-1, true));
            Assert.AreEqual(1, LangRelease.NextChoice(0, true));
            Assert.AreEqual(2, LangRelease.NextChoice(1, true));
            Assert.AreEqual(-1, LangRelease.NextChoice(2, true));
        }

        [Test]
        public void ThePreviewCode_IsForgivingAboutHowItsTyped()
        {
            Assert.IsTrue(LangRelease.IsPreviewCode("LANG PREVIEW"));
            Assert.IsTrue(LangRelease.IsPreviewCode(" lang-preview "));
            Assert.IsTrue(LangRelease.IsPreviewCode("LangPreview"));
            Assert.IsFalse(LangRelease.IsPreviewCode("LANG"));
            Assert.IsFalse(LangRelease.IsPreviewCode("RP1234"));
            Assert.IsFalse(LangRelease.IsPreviewCode(null));
        }
    }
}
