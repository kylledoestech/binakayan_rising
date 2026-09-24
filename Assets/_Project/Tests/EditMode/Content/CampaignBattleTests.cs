using BinakayanRising.Core.Content;
using NUnit.Framework;

namespace BinakayanRising.Tests.Content
{
    /// <summary>
    /// The campaign's battles: every enemy is a real Spanish archetype, the tutorial stays plain,
    /// and the Level 2 battles carry their own win rules (#16, #37, #38).
    /// </summary>
    [TestFixture]
    public class CampaignBattleTests
    {
        /// <summary>Most Spanish units the playtest board can form up (front, rear and shore cells).</summary>
        private const int ColumnCapacity = 14;

        [Test]
        public void EveryEnemyIsASpanishArchetypeAndTheColumnFits()
        {
            foreach (Quest quest in Campaign.Quests)
            {
                if (quest.Battle == null)
                {
                    continue;
                }

                Assert.AreEqual(quest.Battle.Enemies.Count, quest.Battle.EnemyCount, quest.Id);
                Assert.LessOrEqual(quest.Battle.EnemyCount, ColumnCapacity, quest.Id);
                foreach (string id in quest.Battle.Enemies)
                {
                    Assert.IsTrue(UnitCatalog.IsSpanish(id), quest.Id + " fields " + id + ", which is not a Spanish archetype.");
                }
            }
        }

        [Test]
        public void TheTutorialBattleFieldsOnlyRegulars()
        {
            Quest tutorial = Campaign.Find("q02");
            Assert.IsTrue(tutorial.Battle.Tutorial);
            Assert.AreEqual(WinRule.Rout, tutorial.Battle.WinRule);
            Assert.AreEqual(tutorial.Battle.EnemyCount, tutorial.EnemiesOf(UnitCatalog.SpanishRegular));
        }

        [Test]
        public void EveryNewSpanishTypeTakesTheFieldSomewhere()
        {
            string[] roster =
            {
                UnitCatalog.SpanishArtillery, UnitCatalog.SpanishCazador,
                UnitCatalog.SpanishOfficer, UnitCatalog.SpanishMarine
            };

            foreach (string id in roster)
            {
                int total = 0;
                foreach (Quest quest in Campaign.Quests)
                {
                    total += quest.EnemiesOf(id);
                }

                Assert.Greater(total, 0, id + " never appears in the campaign.");
            }
        }

        [Test]
        public void ForgingTheEarthworksIsATwentyTurnEscort()
        {
            QuestBattle battle = Campaign.Find("q06").Battle;
            Assert.AreEqual(WinRule.Escort, battle.WinRule);
            Assert.AreEqual(20, battle.TurnCap);
        }

        [Test]
        public void TheSilentSabotageSendsAThreeUnitSquad()
        {
            QuestBattle battle = Campaign.Find("q07").Battle;
            Assert.AreEqual(WinRule.Sabotage, battle.WinRule);
            Assert.AreEqual(3, battle.SquadCap);
            Assert.Greater(battle.TurnCap, 0);
        }

        [Test]
        public void TheNewArchetypesBorrowTheRegularsArtWithATint()
        {
            string[] roster =
            {
                UnitCatalog.SpanishArtillery, UnitCatalog.SpanishCazador,
                UnitCatalog.SpanishOfficer, UnitCatalog.SpanishMarine
            };

            foreach (string id in roster)
            {
                UnitArchetype archetype = UnitCatalog.Find(id);
                Assert.AreEqual(UnitCatalog.SpanishRegular, archetype.ArtId, id);
                Assert.AreNotEqual(0xFFFFFFFFu, archetype.ArtTint, id + " would be indistinguishable from a regular.");
            }

            Assert.AreEqual(UnitCatalog.SpanishRegular, UnitCatalog.Find(UnitCatalog.SpanishRegular).ArtId);
        }
    }
}
