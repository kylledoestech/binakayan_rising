using System.Collections.Generic;
using System.Text.RegularExpressions;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using NUnit.Framework;

namespace BinakayanRising.Tests.Content
{
    /// <summary>
    /// Holds authored content to the same rules as the interface string table: both languages
    /// written, the same placeholders and markup in each, and no character the fonts lack.
    /// </summary>
    [TestFixture]
    public class ContentTextTests
    {
        private static readonly Regex Placeholder = new Regex(@"\{(\d+)(:[^}]*)?\}");
        private static readonly Regex RichTag = new Regex(@"</?[a-z]+[^>]*>");

        private static IEnumerable<KeyValuePair<string, LocString>> Everything()
        {
            foreach (Character character in Characters.All)
            {
                yield return Item("character " + character.Id + " name", character.Name);
                yield return Item("character " + character.Id + " role", character.Role);
                yield return Item("character " + character.Id + " bio", character.Bio);
            }

            int line = 0;
            foreach (DialogueLine dialogue in Dialogue.AllLines())
            {
                yield return Item("dialogue line " + line++ + " (" + dialogue.Speaker + ")", dialogue.Text);
            }

            yield return Item("aide reminder", Dialogue.AideReminder);

            foreach (CampSite site in Encampment.Sites)
            {
                yield return Item("site " + site.Place + " name", site.Name);
                yield return Item("site " + site.Place + " purpose", site.Purpose);
            }

            foreach (CampaignLevel level in Campaign.Levels)
            {
                yield return Item("level " + level.Number, level.Title);
            }

            foreach (Quest quest in Campaign.Quests)
            {
                yield return Item(quest.Id + " title", quest.Title);
                yield return Item(quest.Id + " tag", quest.Tag);
                yield return Item(quest.Id + " place", quest.Place);
                yield return Item(quest.Id + " briefing", quest.Briefing);
            }

            foreach (HubTask task in Campaign.Tasks)
            {
                yield return Item("task " + task.Id, task.Text);
            }

            foreach (UnitArchetype unit in UnitCatalog.All)
            {
                yield return Item("unit " + unit.Id + " name", unit.Name);
                yield return Item("unit " + unit.Id + " summary", unit.Summary);
                yield return Item("unit " + unit.Id + " bio", unit.Bio);
            }

            foreach (WeaponDef weapon in WeaponCatalog.All)
            {
                yield return Item("weapon " + weapon.Id + " name", weapon.Name);
                yield return Item("weapon " + weapon.Id + " note", weapon.Note);
            }

            for (int i = 0; i < PlayerRanks.Count; i++)
            {
                yield return Item("rank " + i, PlayerRanks.At(i).Gloss);
            }
        }

        private static KeyValuePair<string, LocString> Item(string where, LocString text)
        {
            return new KeyValuePair<string, LocString>(where, text);
        }

        [Test]
        public void EveryTextHasEnglishAndFilipino()
        {
            foreach (KeyValuePair<string, LocString> item in Everything())
            {
                Assert.IsFalse(string.IsNullOrEmpty(item.Value.English), item.Key + " has no English.");
                Assert.IsFalse(string.IsNullOrEmpty(item.Value.Filipino), item.Key + " has no Filipino.");
            }
        }

        [Test]
        public void BothLanguagesUseTheSamePlaceholdersAndTags()
        {
            foreach (KeyValuePair<string, LocString> item in Everything())
            {
                Assert.AreEqual(Signature(Placeholder, item.Value.English), Signature(Placeholder, item.Value.Filipino),
                    item.Key + " has different placeholders in English and Filipino.");
                Assert.AreEqual(RichTag.Matches(item.Value.English).Count, RichTag.Matches(item.Value.Filipino).Count,
                    item.Key + " has different rich-text tags in English and Filipino.");
            }
        }

        [Test]
        public void EveryCharacterIsInTheFontAtlases()
        {
            foreach (KeyValuePair<string, LocString> item in Everything())
            {
                foreach (string text in new[] { item.Value.English, item.Value.Filipino })
                {
                    char missing = BakedGlyphs.FirstMissing(text);
                    Assert.AreEqual('\0', missing,
                        string.Format("{0} uses U+{1:X4}, which no font atlas has: \"{2}\"", item.Key, (int)missing, text));
                }
            }
        }

        [Test]
        public void EveryKeeperAndSpeakerIsACharacter()
        {
            foreach (DialogueLine line in Dialogue.AllLines())
            {
                Assert.IsNotNull(Characters.Find(line.Speaker), "Unknown speaker " + line.Speaker);
            }

            foreach (CampSite site in Encampment.Sites)
            {
                Assert.IsTrue(site.Keeper == null || Characters.Find(site.Keeper) != null, site.Place + " has an unknown keeper.");
            }
        }

        private static string Signature(Regex pattern, string text)
        {
            MatchCollection matches = pattern.Matches(text ?? string.Empty);
            var parts = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
            {
                parts[i] = matches[i].Groups[1].Value;
            }

            System.Array.Sort(parts);
            return string.Join(",", parts);
        }
    }
}
