using System.Text.RegularExpressions;
using BinakayanRising.Core.Localization;
using NUnit.Framework;

namespace BinakayanRising.Tests.Localization
{
    /// <summary>
    /// Keeps the English and Filipino tables in step: every key authored in both, and both
    /// languages agreeing on the placeholders and markup a caller depends on.
    /// </summary>
    [TestFixture]
    public class StringTableTests
    {
        private static readonly Regex Placeholder = new Regex(@"\{(\d+)(:[^}]*)?\}");
        private static readonly Regex RichTag = new Regex(@"</?[a-z]+[^>]*>");

        [Test]
        public void EveryKeyHasEnglishAndFilipinoText()
        {
            for (TextKey key = TextKey.None + 1; key < TextKey.Count; key++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(StringTable.GetAuthored(Language.English, key)), key + " has no English text.");
                Assert.IsFalse(string.IsNullOrEmpty(StringTable.GetAuthored(Language.Filipino, key)), key + " has no Filipino text.");
            }
        }

        [Test]
        public void BothLanguagesUseTheSamePlaceholders()
        {
            for (TextKey key = TextKey.None + 1; key < TextKey.Count; key++)
            {
                Assert.AreEqual(
                    Signature(Placeholder, StringTable.GetAuthored(Language.English, key)),
                    Signature(Placeholder, StringTable.GetAuthored(Language.Filipino, key)),
                    key + " has different placeholders in English and Filipino.");
            }
        }

        [Test]
        public void BothLanguagesUseTheSameRichTextTags()
        {
            for (TextKey key = TextKey.None + 1; key < TextKey.Count; key++)
            {
                Assert.AreEqual(
                    RichTag.Matches(StringTable.GetAuthored(Language.English, key) ?? string.Empty).Count,
                    RichTag.Matches(StringTable.GetAuthored(Language.Filipino, key) ?? string.Empty).Count,
                    key + " has a different number of rich-text tags in English and Filipino.");
            }
        }

        [Test]
        public void NoStringUsesTheUnbakedMinusSign()
        {
            // U+2212 is not in the static font atlases and renders as a missing-glyph box.
            for (TextKey key = TextKey.None + 1; key < TextKey.Count; key++)
            {
                StringAssert.DoesNotContain("−", StringTable.GetAuthored(Language.English, key), key.ToString());
                StringAssert.DoesNotContain("−", StringTable.GetAuthored(Language.Filipino, key), key.ToString());
            }
        }

        [Test]
        public void SwitchingLanguageChangesTextAndBumpsTheVersion()
        {
            Language before = Loc.Current;
            try
            {
                Loc.SetLanguage(Language.English, notify: false);
                int version = Loc.Version;

                int raised = 0;
                System.Action handler = () => raised++;
                Loc.LanguageChanged += handler;
                Loc.SetLanguage(Language.Filipino);
                Loc.LanguageChanged -= handler;

                Assert.AreEqual("Tagumpay", Loc.Get(TextKey.OutcomeVictory));
                Assert.AreEqual(version + 1, Loc.Version);
                Assert.AreEqual(1, raised);
            }
            finally
            {
                Loc.SetLanguage(before, notify: false);
            }
        }

        [Test]
        public void FormatFillsPlaceholders()
        {
            Assert.AreEqual(
                "Spanish Regular 3",
                string.Format(StringTable.Get(Language.English, TextKey.UnitSpanishRegular), 3));
        }

        private static string Signature(Regex pattern, string text)
        {
            MatchCollection matches = pattern.Matches(text ?? string.Empty);
            string[] parts = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
            {
                parts[i] = matches[i].Groups[1].Value;
            }

            System.Array.Sort(parts);
            return string.Join(",", parts);
        }
    }
}
