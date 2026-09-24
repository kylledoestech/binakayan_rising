using System;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Meta;
using NUnit.Framework;

namespace BinakayanRising.Tests.Meta
{
    /// <summary>
    /// Which story scene the encampment owes the player (#34): a hub-task quest's opening scene,
    /// once; never a battle's, which plays at launch.
    /// </summary>
    [TestFixture]
    public class StoryProgressTests
    {
        private MetaGame game;

        [SetUp]
        public void SetUp()
        {
            DateTime now = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
            MetaRules rules = MetaRules.Default();
            game = new MetaGame(MetaGame.NewGame(rules, now, 42), rules, () => now);
        }

        [Test]
        public void ANewCampaignOwesActOneOnce()
        {
            Assert.AreEqual(Cutscenes.Act1, game.PendingStoryScene);
            Assert.IsTrue(game.MarkSceneSeen(Cutscenes.Act1));
            Assert.IsNull(game.PendingStoryScene);
            Assert.IsFalse(game.MarkSceneSeen(Cutscenes.Act1), "A scene is only marked seen once.");
        }

        [Test]
        public void ABattleQuestOwesNothingToTheCamp()
        {
            game.Data.clearedQuests.Add("q01");
            Assert.AreEqual(QuestKind.Battle, game.CurrentQuest.Kind);
            Assert.IsNull(game.PendingStoryScene);
        }

        [Test]
        public void ActThreeIsOwedWhenTheRallyBecomesCurrent()
        {
            game.MarkSceneSeen(Cutscenes.Act1);
            game.Data.clearedQuests.Add("q01");
            game.Data.clearedQuests.Add("q02");
            Assert.AreEqual("q03", game.CurrentQuest.Id);
            Assert.AreEqual(Cutscenes.Act3, game.PendingStoryScene);
        }

        [Test]
        public void TheSeenFlagSurvivesInTheSave()
        {
            game.MarkSceneSeen(Cutscenes.Act1);
            Assert.IsTrue(game.Data.HasFlag(MetaGame.SceneFlag(Cutscenes.Act1)));
        }
    }
}
