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
                yield return Item("rank " + i + " announcement", PlayerRanks.AnnouncementOf(i));
            }

            foreach (Cutscene scene in Cutscenes.All)
            {
                for (int i = 0; i < scene.Slides.Length; i++)
                {
                    yield return Item(scene.Id + " slide " + i + " caption", scene.Slides[i].Caption);
                    yield return Item(scene.Id + " slide " + i + " line", scene.Slides[i].Line);
                }
            }

            foreach (Lesson lesson in Learning.Lessons)
            {
                yield return Item("lesson " + lesson.Id + " title", lesson.Title);
                yield return Item("lesson " + lesson.Id + " body", lesson.Body);
            }

            foreach (Question question in Learning.Questions)
            {
                yield return Item("question " + question.Id, question.Text);
                yield return Item("question " + question.Id + " explanation", question.Explanation);
                for (int i = 0; i < question.Choices.Length; i++)
                {
                    yield return Item("question " + question.Id + " choice " + i, question.Choices[i]);
                }
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

        [Test]
        public void EveryCutsceneExistsAndIsNarratedByACharacter()
        {
            foreach (Quest quest in Campaign.Quests)
            {
                Assert.IsTrue(quest.PreCutscene == null || Cutscenes.Find(quest.PreCutscene) != null, quest.Id + " opens with a missing cutscene.");
                Assert.IsTrue(quest.PostCutscene == null || Cutscenes.Find(quest.PostCutscene) != null, quest.Id + " closes with a missing cutscene.");
            }

            foreach (Cutscene scene in Cutscenes.All)
            {
                Assert.IsNotNull(Characters.Find(scene.Narrator), scene.Id + " has an unknown narrator.");
                Assert.Greater(scene.Slides.Length, 0, scene.Id + " has no slides.");
            }
        }

        [Test]
        public void EveryQuestUnlocksALessonThatExists()
        {
            foreach (Quest quest in Campaign.Quests)
            {
                Assert.IsNotNull(Learning.FindLesson(quest.RewardLesson), quest.Id + " unlocks a missing lesson " + quest.RewardLesson);
            }
        }

        [Test]
        public void EveryLevelHasTenQuestionsWithFourDistinctChoicesAndAValidAnswer()
        {
            var ids = new HashSet<string>();
            for (int level = 1; level <= Campaign.LevelCount; level++)
            {
                Assert.AreEqual(10, Learning.QuestionsIn(level).Count, "Level " + level + " question count.");
            }

            foreach (Question q in Learning.Questions)
            {
                Assert.IsTrue(ids.Add(q.Id), "Duplicate question id " + q.Id);
                Assert.AreEqual(Learning.ChoiceCount, q.Choices.Length, q.Id);
                Assert.That(q.Answer, Is.InRange(0, Learning.ChoiceCount - 1), q.Id);
                var seen = new HashSet<string>();
                foreach (LocString choice in q.Choices)
                {
                    Assert.IsTrue(seen.Add(choice.English), q.Id + " repeats a choice: " + choice.English);
                }
            }
        }

        [Test]
        public void ALevelTestDrawsDistinctQuestionsOfThatLevel()
        {
            List<Question> test = Learning.Test(2, 5, 42);
            Assert.AreEqual(5, test.Count);
            var ids = new HashSet<string>();
            foreach (Question q in test)
            {
                Assert.AreEqual(2, q.Level);
                Assert.IsTrue(ids.Add(q.Id));
            }
        }

        [Test]
        public void TheBattleQuizAsksEachQuestionOnceBeforeRepeating()
        {
            var asked = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                Question q = Learning.NextQuiz(3, asked, 7);
                Assert.IsFalse(asked.Contains(q.Id), "Repeated " + q.Id + " before all were asked.");
                asked.Add(q.Id);
            }

            Assert.IsNotNull(Learning.NextQuiz(3, asked, 7));
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
