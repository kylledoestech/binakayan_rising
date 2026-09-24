using System.Collections.Generic;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using NUnit.Framework;

namespace BinakayanRising.Tests.Content
{
    /// <summary>
    /// The story acts, the aftermath and the historical glossary (#34, #40, #50): each exists,
    /// is placed where the proposal puts it, and is written in both languages with glyphs the
    /// fonts have.
    /// </summary>
    [TestFixture]
    public class StoryContentTests
    {
        [Test]
        public void TheFourActsOpenTheirQuestsAndTheAftermathClosesTheLastBattle()
        {
            Assert.AreEqual(Cutscenes.Act1, Campaign.Find("q01").PreCutscene);
            Assert.AreEqual(Cutscenes.Act2, Campaign.Find("q02").PreCutscene);
            Assert.AreEqual(Cutscenes.Act3, Campaign.Find("q03").PreCutscene);
            Assert.AreEqual(Cutscenes.Act4, Campaign.Find("q08").PreCutscene);
            Assert.AreEqual(Cutscenes.Aftermath, Campaign.Find("q10").PostCutscene);

            // Act 4 opens Level 3, the battle of November 9.
            Assert.AreEqual(3, Campaign.Find("q08").Level);
        }

        [Test]
        public void EachActIsNamedOnItsFirstSlide()
        {
            string[] acts = { Cutscenes.Act1, Cutscenes.Act2, Cutscenes.Act3, Cutscenes.Act4 };
            string[] names = { "The Scholar of Ghent", "The Catalyst of Defeat", "Forging the Brotherhood", "The Masterpiece of Binakayan-Dalahican" };
            for (int i = 0; i < acts.Length; i++)
            {
                Cutscene scene = Cutscenes.Find(acts[i]);
                Assert.IsNotNull(scene, acts[i]);
                Assert.GreaterOrEqual(scene.Slides.Length, 3, acts[i] + " is too short to tell an act.");
                StringAssert.Contains("Act " + (i + 1), scene.Slides[0].Caption.English);
                StringAssert.Contains(names[i], scene.Slides[0].Caption.English);
            }
        }

        [Test]
        public void TheAftermathCoversNovemberElevenAndEndsOnRemembrance()
        {
            Cutscene scene = Cutscenes.Find(Cutscenes.Aftermath);
            Assert.IsNotNull(scene);
            Assert.GreaterOrEqual(scene.Slides.Length, 4);
            StringAssert.Contains("November 11, 1896", scene.Slides[0].Caption.English);
            StringAssert.Contains("Nobyembre 11, 1896", scene.Slides[0].Caption.Filipino);
        }

        [Test]
        public void CutsceneIdsAndSlideImagesAreUnique()
        {
            var ids = new HashSet<string>();
            var images = new HashSet<string>();
            foreach (Cutscene scene in Cutscenes.All)
            {
                Assert.IsTrue(ids.Add(scene.Id), "Duplicate cutscene " + scene.Id);
                foreach (CutsceneSlide slide in scene.Slides)
                {
                    Assert.IsTrue(images.Add(slide.Image), "Two slides share the illustration name " + slide.Image);
                }
            }
        }

        [Test]
        public void TheGlossaryHasTwentyFiveToThirtyUniqueTerms()
        {
            Assert.That(Glossary.All.Count, Is.InRange(25, 30));
            var ids = new HashSet<string>();
            var english = new HashSet<string>();
            var filipino = new HashSet<string>();
            foreach (GlossaryTerm term in Glossary.All)
            {
                Assert.IsTrue(ids.Add(term.Id), "Duplicate glossary id " + term.Id);
                Assert.IsTrue(english.Add(term.Term.English), "Duplicate English headword " + term.Term.English);
                Assert.IsTrue(filipino.Add(term.Term.Filipino), "Duplicate Filipino headword " + term.Term.Filipino);
                Assert.AreSame(term, Glossary.Find(term.Id));
            }
        }

        [Test]
        public void TheGlossaryCoversTheTermsTheStoryNames()
        {
            foreach (string id in new[] { "katipunan", "magdalo", "magdiwang", "binakayan", "dalahican", "cavite", "kawit", "aguinaldo", "bonifacio", "evangelista", "trench", "bolo", "reales", "guardiacivil", "cazadores" })
            {
                Assert.IsNotNull(Glossary.Find(id), "The glossary is missing " + id);
            }
        }

        [Test]
        public void TheGlossaryIsSortedByHeadwordInEachLanguage()
        {
            foreach (Language language in new[] { Language.English, Language.Filipino })
            {
                List<GlossaryTerm> sorted = Glossary.Sorted(language);
                Assert.AreEqual(Glossary.All.Count, sorted.Count);
                for (int i = 1; i < sorted.Count; i++)
                {
                    string before = Glossary.SortKey(sorted[i - 1].Term.Get(language));
                    string after = Glossary.SortKey(sorted[i].Term.Get(language));
                    Assert.LessOrEqual(string.CompareOrdinal(before, after), 0, language + ": " + before + " sorts after " + after);
                }
            }

            // Accents fold: Andrés sorts among the As, not after Z.
            Assert.AreEqual("andres bonifacio", Glossary.SortKey("Andrés Bonifacio"));
            Assert.AreEqual("andrés bonifacio".Length, Glossary.SortKey("Andrés Bonifacio").Length);
        }

        [Test]
        public void EveryGlossaryEntryIsWrittenInBothLanguagesWithKnownGlyphs()
        {
            foreach (GlossaryTerm term in Glossary.All)
            {
                foreach (LocString text in new[] { term.Term, term.Definition })
                {
                    Assert.IsFalse(string.IsNullOrEmpty(text.English), term.Id + " has no English.");
                    Assert.IsFalse(string.IsNullOrEmpty(text.Filipino), term.Id + " has no Filipino.");
                    Assert.AreEqual('\0', BakedGlyphs.FirstMissing(text.English), term.Id + " English");
                    Assert.AreEqual('\0', BakedGlyphs.FirstMissing(text.Filipino), term.Id + " Filipino");
                }

                // The definition well shows about six lines; keep each entry short enough to fit.
                Assert.LessOrEqual(term.Definition.English.Length, 300, term.Id + " English definition is too long for the page.");
                Assert.LessOrEqual(term.Definition.Filipino.Length, 330, term.Id + " Filipino definition is too long for the page.");
            }
        }
    }
}
