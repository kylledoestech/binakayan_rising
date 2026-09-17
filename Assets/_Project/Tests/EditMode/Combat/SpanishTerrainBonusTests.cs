using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using NUnit.Framework;

namespace BinakayanRising.Tests.Combat
{
    /// <summary>
    /// Covers <see cref="CombatConfig.SpanishReceivesTerrainBonuses"/>: fortifications stop helping
    /// the Spanish when it is off, keep helping the Katipunan, and never shield anyone from a penalty.
    /// </summary>
    [TestFixture]
    public class SpanishTerrainBonusTests
    {
        [Test]
        public void ByDefaultASpanishDefenderInATrenchStillGetsCover()
        {
            // Base defense 10, +20% in the trench: 20 - 12.
            Assert.AreEqual(8f, DamageTo(Team.Spanish, TerrainType.Trench, spanishBonuses: true), 0.0001f);
        }

        [Test]
        public void WhenOffASpanishDefenderInATrenchTakesFullDamage()
        {
            Assert.AreEqual(10f, DamageTo(Team.Spanish, TerrainType.Trench, spanishBonuses: false), 0.0001f);
        }

        [Test]
        public void WhenOffAKatipunanDefenderInATrenchKeepsItsCover()
        {
            Assert.AreEqual(8f, DamageTo(Team.Katipunan, TerrainType.Trench, spanishBonuses: false), 0.0001f);
        }

        [Test]
        public void WhenOffASpanishDefenderInTheShallowsStillSuffersThePenalty()
        {
            // -10% defense: 20 - 9.
            Assert.AreEqual(11f, DamageTo(Team.Spanish, TerrainType.CoastalShallows, spanishBonuses: false), 0.0001f);
        }

        [Test]
        public void WhenOffASpanishUnitOnATentDoesNotRegenerate()
        {
            Assert.AreEqual(50f, HpAfterOneTurnOnTent(spanishBonuses: false), 0.0001f);
        }

        [Test]
        public void ByDefaultASpanishUnitOnATentRegenerates()
        {
            Assert.AreEqual(55f, HpAfterOneTurnOnTent(spanishBonuses: true), 0.0001f);
        }

        [Test]
        public void CloneKeepsTheSwitch()
        {
            CombatConfig config = CombatTestFactory.Config();
            config.SpanishReceivesTerrainBonuses = false;

            Assert.IsFalse(config.Clone().SpanishReceivesTerrainBonuses);
        }

        /// <summary>
        /// One turn of a fixed duel: an attacker at (0,0) hits a defender of the given team standing
        /// on the given terrain at (1,0). The attacker always has the lower id, so it acts first.
        /// </summary>
        private static float DamageTo(Team defenderTeam, TerrainType terrainUnderDefender, bool spanishBonuses)
        {
            BattleGrid grid = CombatTestFactory.FlatGrid(4, 1);
            grid.SetTerrain(new GridCoord(1, 0), terrainUnderDefender);

            Team attackerTeam = defenderTeam == Team.Spanish ? Team.Katipunan : Team.Spanish;
            List<CombatUnit> units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, attackerTeam, CombatTestFactory.Stats(maxHP: 1000f, attackDamage: 20f), 0, 0),
                CombatTestFactory.Unit(2, defenderTeam, CombatTestFactory.Stats(maxHP: 1000f, attackDamage: 0f, defense: 10f), 1, 0)
            };

            CombatConfig config = CombatTestFactory.Config();
            config.SpanishReceivesTerrainBonuses = spanishBonuses;

            BattleSimulator simulator = new BattleSimulator(
                grid, units, config, terrain: TerrainModifierProvider.CreateCapstoneTable2ForTesting());

            return CombatTestFactory.FirstDamageBy(simulator.ExecuteTurn().Events, 1);
        }

        private static float HpAfterOneTurnOnTent(bool spanishBonuses)
        {
            BattleGrid grid = CombatTestFactory.FlatGrid(6, 6);
            grid.SetTerrain(new GridCoord(0, 0), TerrainType.EncampmentTent);

            CombatUnit resting = CombatTestFactory.Unit(
                2, Team.Spanish, CombatTestFactory.Stats(maxHP: 100f, movementSpeed: 0f), 0, 0);
            CombatUnit distant = CombatTestFactory.Unit(
                1, Team.Katipunan, CombatTestFactory.Stats(movementSpeed: 0f), 5, 5);
            resting.ApplyDamage(50f);

            CombatConfig config = CombatTestFactory.Config(0, 10);
            config.SpanishReceivesTerrainBonuses = spanishBonuses;

            BattleSimulator simulator = new BattleSimulator(
                grid,
                new List<CombatUnit> { resting, distant },
                config,
                terrain: TerrainModifierProvider.CreateCapstoneTable2ForTesting());

            simulator.ExecuteTurn();
            return resting.CurrentHP;
        }
    }
}
