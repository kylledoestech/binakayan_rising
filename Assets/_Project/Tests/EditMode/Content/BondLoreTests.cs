using System.Collections.Generic;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using NUnit.Framework;

namespace BinakayanRising.Tests.Content
{
    /// <summary>The four Kapatiran lore dialogues (#20): one per pair, bilingual, speakable.</summary>
    [TestFixture]
    public class BondLoreTests
    {
        private static IEnumerable<LocString> Texts(LoreDialogue lore)
        {
            yield return lore.Title;
            yield return lore.Setting;
            for (int i = 0; i < lore.Lines.Count; i++)
            {
                yield return lore.Lines[i].Text;
            }
        }

        [Test]
        public void EachPairHasItsOwnNumberedDialogue()
        {
            Assert.AreEqual(BondCatalog.Pairs.Count, BondLore.All.Count);
            for (int i = 0; i < BondCatalog.Pairs.Count; i++)
            {
                BondPair pair = BondCatalog.Pairs[i];
                LoreDialogue lore = BondLore.For(pair.Id);
                Assert.IsNotNull(lore, pair.Id);
                Assert.AreEqual(pair.LoreNumber, lore.Number, pair.Id);
                Assert.AreEqual(i + 1, lore.Number, "Table 3 order");
            }
        }

        [Test]
        public void EveryLineIsSpokenByOneHalfOfItsPair()
        {
            foreach (LoreDialogue lore in BondLore.All)
            {
                BondPair pair = BondCatalog.Find(lore.BondId);
                Assert.Greater(lore.Lines.Count, 2, lore.BondId);
                foreach (DialogueLine line in lore.Lines)
                {
                    Assert.IsNotNull(UnitCatalog.Find(line.Speaker), line.Speaker);
                    Assert.IsNotNull(pair.PartnerOf(line.Speaker), line.Speaker + " is not in " + lore.BondId);
                }
            }
        }

        [Test]
        public void EveryTextHasEnglishAndFilipinoInTheFontAtlases()
        {
            foreach (LoreDialogue lore in BondLore.All)
            {
                foreach (LocString text in Texts(lore))
                {
                    Assert.IsFalse(string.IsNullOrEmpty(text.English), lore.BondId);
                    Assert.IsFalse(string.IsNullOrEmpty(text.Filipino), lore.BondId + ": " + text.English);
                    Assert.AreNotEqual(text.English, text.Filipino, "untranslated: " + text.English);
                    Assert.AreEqual('\0', BakedGlyphs.FirstMissing(text.English), text.English);
                    Assert.AreEqual('\0', BakedGlyphs.FirstMissing(text.Filipino), text.Filipino);
                }
            }
        }
    }
}
