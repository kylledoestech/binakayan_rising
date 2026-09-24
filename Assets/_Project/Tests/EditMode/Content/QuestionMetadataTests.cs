using System;
using System.Collections.Generic;
using BinakayanRising.Core.Content;
using NUnit.Framework;

namespace BinakayanRising.Tests.Content
{
    /// <summary>
    /// Every question carries a difficulty and a category, and each level's test mixes
    /// difficulties rather than being all easy or all hard.
    /// </summary>
    [TestFixture]
    public class QuestionMetadataTests
    {
        [Test]
        public void EveryQuestionHasADefinedDifficultyAndCategory()
        {
            Assert.AreEqual(30, Learning.Questions.Count);
            foreach (Question question in Learning.Questions)
            {
                Assert.IsTrue(Enum.IsDefined(typeof(QuestionDifficulty), question.Difficulty),
                    question.Id + " has no difficulty");
                Assert.IsTrue(Enum.IsDefined(typeof(QuestionCategory), question.Category),
                    question.Id + " has no category");
            }
        }

        [Test]
        public void EachLevelCoversAtLeastTwoDifficulties()
        {
            foreach (CampaignLevel level in Campaign.Levels)
            {
                var seen = new HashSet<QuestionDifficulty>();
                foreach (Question question in Learning.QuestionsIn(level.Number))
                {
                    seen.Add(question.Difficulty);
                }

                Assert.GreaterOrEqual(seen.Count, 2, "level " + level.Number + " difficulties");
            }
        }

        [Test]
        public void EveryCategoryIsUsed()
        {
            var seen = new HashSet<QuestionCategory>();
            foreach (Question question in Learning.Questions)
            {
                seen.Add(question.Category);
            }

            foreach (QuestionCategory category in Enum.GetValues(typeof(QuestionCategory)))
            {
                Assert.IsTrue(seen.Contains(category), category + " has no questions");
            }
        }
    }
}
