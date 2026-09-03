using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using NUnit.Framework;

namespace BinakayanRising.Tests.Combat
{
    /// <summary>
    /// Covers the targeting strategies, with particular attention to tie-breaking: a tie broken by
    /// list order would make the whole simulation non-reproducible the moment anything reordered the
    /// unit list.
    /// </summary>
    [TestFixture]
    public class TargetingStrategyTests
    {
        private BattleGrid grid;
        private CombatUnit self;

        [SetUp]
        public void SetUp()
        {
            grid = CombatTestFactory.FlatGrid(10, 10);
            self = CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(), 0, 0);
        }

        [Test]
        public void NearestPicksTheClosestCandidate()
        {
            CombatUnit far = CombatTestFactory.Unit(5, Team.Spanish, CombatTestFactory.Stats(), 6, 0);
            CombatUnit near = CombatTestFactory.Unit(9, Team.Spanish, CombatTestFactory.Stats(), 1, 0);

            ITargetingStrategy strategy = TargetingStrategies.Nearest(false);
            CombatUnit chosen = strategy.SelectTarget(self, new List<CombatUnit> { far, near }, grid);

            Assert.AreEqual(9, chosen.Id);
        }

        [Test]
        public void NearestBreaksTiesOnTheLowestIdNotOnListOrder()
        {
            CombatUnit highId = CombatTestFactory.Unit(7, Team.Spanish, CombatTestFactory.Stats(), 2, 0);
            CombatUnit lowId = CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(), 0, 2);

            ITargetingStrategy strategy = TargetingStrategies.Nearest(false);

            CombatUnit chosenA = strategy.SelectTarget(self, new List<CombatUnit> { highId, lowId }, grid);
            CombatUnit chosenB = strategy.SelectTarget(self, new List<CombatUnit> { lowId, highId }, grid);

            Assert.AreEqual(2, chosenA.Id);
            Assert.AreEqual(2, chosenB.Id, "Reversing the candidate list must not change the choice.");
        }

        [Test]
        public void NearestSkipsDeadCandidates()
        {
            CombatUnit dead = CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(), 1, 0);
            CombatUnit alive = CombatTestFactory.Unit(3, Team.Spanish, CombatTestFactory.Stats(), 5, 0);
            dead.Kill();

            CombatUnit chosen = TargetingStrategies.Nearest(false).SelectTarget(self, new List<CombatUnit> { dead, alive }, grid);

            Assert.AreEqual(3, chosen.Id);
        }

        [Test]
        public void NearestReturnsNullWhenThereAreNoCandidates()
        {
            Assert.IsNull(TargetingStrategies.Nearest(false).SelectTarget(self, new List<CombatUnit>(), grid));
        }

        [Test]
        public void DiagonalMovementChangesWhatCountsAsNearest()
        {
            // (3,3) is Chebyshev 3 but Manhattan 6; (0,4) is Chebyshev 4 but Manhattan 4.
            CombatUnit diagonal = CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(), 3, 3);
            CombatUnit straight = CombatTestFactory.Unit(3, Team.Spanish, CombatTestFactory.Stats(), 0, 4);
            List<CombatUnit> candidates = new List<CombatUnit> { diagonal, straight };

            Assert.AreEqual(3, TargetingStrategies.Nearest(false).SelectTarget(self, candidates, grid).Id);
            Assert.AreEqual(2, TargetingStrategies.Nearest(true).SelectTarget(self, candidates, grid).Id);
        }

        [Test]
        public void LowestHpPicksTheMostWoundedCandidate()
        {
            CombatUnit healthy = CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(maxHP: 100f), 1, 0);
            CombatUnit wounded = CombatTestFactory.Unit(3, Team.Spanish, CombatTestFactory.Stats(maxHP: 100f), 8, 0);
            wounded.ApplyDamage(80f);

            CombatUnit chosen = TargetingStrategies.LowestHp(false).SelectTarget(self, new List<CombatUnit> { healthy, wounded }, grid);

            Assert.AreEqual(3, chosen.Id);
        }

        [Test]
        public void LowestHpBreaksTiesDeterministically()
        {
            CombatUnit a = CombatTestFactory.Unit(8, Team.Spanish, CombatTestFactory.Stats(maxHP: 50f), 1, 0);
            CombatUnit b = CombatTestFactory.Unit(4, Team.Spanish, CombatTestFactory.Stats(maxHP: 50f), 0, 1);

            Assert.AreEqual(4, TargetingStrategies.LowestHp(false).SelectTarget(self, new List<CombatUnit> { a, b }, grid).Id);
            Assert.AreEqual(4, TargetingStrategies.LowestHp(false).SelectTarget(self, new List<CombatUnit> { b, a }, grid).Id);
        }

        [Test]
        public void HighestThreatPicksTheHardestHitterByDefault()
        {
            CombatUnit weak = CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(attackDamage: 5f), 1, 0);
            CombatUnit strong = CombatTestFactory.Unit(3, Team.Spanish, CombatTestFactory.Stats(attackDamage: 50f), 9, 9);

            CombatUnit chosen = TargetingStrategies.HighestThreat(false).SelectTarget(self, new List<CombatUnit> { weak, strong }, grid);

            Assert.AreEqual(3, chosen.Id);
        }

        [Test]
        public void HighestThreatAcceptsACustomScorer()
        {
            CombatUnit a = CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(attackDamage: 50f), 1, 0);
            CombatUnit b = CombatTestFactory.Unit(3, Team.Spanish, CombatTestFactory.Stats(attackDamage: 5f, maxHP: 500f), 2, 0);

            ITargetingStrategy strategy = TargetingStrategies.HighestThreat(false, unit => unit.CurrentHP);
            CombatUnit chosen = strategy.SelectTarget(self, new List<CombatUnit> { a, b }, grid);

            Assert.AreEqual(3, chosen.Id);
        }

        [Test]
        public void HighestThreatSeesModifiedAttackNotBaseAttack()
        {
            CombatUnit buffed = CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(attackDamage: 10f), 1, 0);
            CombatUnit plain = CombatTestFactory.Unit(3, Team.Spanish, CombatTestFactory.Stats(attackDamage: 15f), 2, 0);
            buffed.Modifiers.Add(StatModifier.Percent(StatKind.AttackDamage, 1f, ModifierSource.Kapatiran));

            CombatUnit chosen = TargetingStrategies.HighestThreat(false).SelectTarget(self, new List<CombatUnit> { buffed, plain }, grid);

            Assert.AreEqual(2, chosen.Id, "Threat must be evaluated on effective stats, not base stats.");
        }
    }
}
