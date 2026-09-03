using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using NUnit.Framework;

namespace BinakayanRising.Tests.Combat
{
    /// <summary>
    /// Covers <see cref="BattleSimulator"/> end to end: reproducibility, terrain effects, bond
    /// effects, movement legality, and every way a battle can terminate.
    /// </summary>
    [TestFixture]
    public class BattleSimulatorTests
    {
        /// <summary>
        /// Builds a three-versus-three skirmish with enough randomness in it that two different
        /// seeds must diverge. Rebuilt from scratch per call so that no state leaks between runs.
        /// </summary>
        private static BattleSimulator BuildSkirmish(int seed)
        {
            BattleGrid grid = CombatTestFactory.FlatGrid(8, 8);
            UnitStats stats = CombatTestFactory.Stats(
                maxHP: 200f,
                attackDamage: 25f,
                defense: 5f,
                evasion: 0.25f,
                rangedAccuracy: 0.9f,
                attackRange: 1f,
                criticalHitChance: 0.35f,
                movementSpeed: 1f);

            List<CombatUnit> units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, Team.Katipunan, stats, 0, 1),
                CombatTestFactory.Unit(2, Team.Katipunan, stats, 0, 3),
                CombatTestFactory.Unit(3, Team.Katipunan, stats, 0, 5),
                CombatTestFactory.Unit(4, Team.Spanish, stats, 7, 1),
                CombatTestFactory.Unit(5, Team.Spanish, stats, 7, 3),
                CombatTestFactory.Unit(6, Team.Spanish, stats, 7, 5)
            };

            CombatConfig config = CombatTestFactory.Config(seed, 60);
            return new BattleSimulator(grid, units, config, terrain: TerrainModifierProvider.CreateCapstoneTable2ForTesting());
        }

        [Test]
        public void SameSeedAndSameSetupProduceIdenticalEventLogs()
        {
            BattleResult first = BuildSkirmish(1234).RunToCompletion();
            BattleResult second = BuildSkirmish(1234).RunToCompletion();

            Assert.AreEqual(first.Outcome, second.Outcome);
            Assert.AreEqual(first.TurnsElapsed, second.TurnsElapsed);
            Assert.AreEqual(first.Events.Count, second.Events.Count);
            Assert.AreEqual(first.ToLogText(), second.ToLogText(), "Identical seeds must replay identically.");
        }

        [Test]
        public void DifferentSeedsProduceDifferentEventLogs()
        {
            BattleResult first = BuildSkirmish(1234).RunToCompletion();
            BattleResult second = BuildSkirmish(4321).RunToCompletion();

            Assert.AreNotEqual(first.ToLogText(), second.ToLogText(), "The seed must actually drive the battle.");
        }

        [Test]
        public void ABattleWithNoRandomnessReplaysIdenticallyUnderAnySeed()
        {
            // With certain accuracy, no evasion and no crit chance, no dice are drawn at all, so the
            // seed is irrelevant. This pins the "degenerate probabilities consume no dice" contract
            // at the level of a whole battle.
            BattleResult first = BuildDuel(seed: 1).RunToCompletion();
            BattleResult second = BuildDuel(seed: 999999).RunToCompletion();

            Assert.AreEqual(first.ToLogText(), second.ToLogText());
        }

        private static BattleSimulator BuildDuel(int seed)
        {
            BattleGrid grid = CombatTestFactory.FlatGrid(6, 1);
            List<CombatUnit> units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(maxHP: 60f, attackDamage: 12f), 0, 0),
                CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(maxHP: 60f, attackDamage: 12f), 5, 0)
            };

            return new BattleSimulator(grid, units, CombatTestFactory.Config(seed, 60));
        }

        [Test]
        public void AUnitInATrenchTakesLessDamageThanTheSameUnitOnStandardGrid()
        {
            float standardDamage = DamageDealtToDefenderOn(TerrainType.StandardGrid);
            float trenchDamage = DamageDealtToDefenderOn(TerrainType.Trench);

            // Base defense 10; Capstone Table 2 gives the trench +20% Defense, so 20 - 12 rather than 20 - 10.
            Assert.AreEqual(10f, standardDamage, 0.0001f);
            Assert.AreEqual(8f, trenchDamage, 0.0001f);
            Assert.Less(trenchDamage, standardDamage, "Trench cover must measurably reduce incoming damage.");
        }

        [Test]
        public void CoastalShallowsMakesTheDefenderTakeMoreDamage()
        {
            float standardDamage = DamageDealtToDefenderOn(TerrainType.StandardGrid);
            float shallowsDamage = DamageDealtToDefenderOn(TerrainType.CoastalShallows);

            // Table 2 gives the shallows -10% Defense, so 20 - 9.
            Assert.AreEqual(11f, shallowsDamage, 0.0001f);
            Assert.Greater(shallowsDamage, standardDamage);
        }

        /// <summary>
        /// Runs one turn of a fixed duel in which unit 1 attacks unit 2, with unit 2 standing on the
        /// given terrain, and returns the damage unit 1 dealt.
        /// </summary>
        private static float DamageDealtToDefenderOn(TerrainType terrainUnderDefender)
        {
            BattleGrid grid = CombatTestFactory.FlatGrid(4, 1);
            grid.SetTerrain(new GridCoord(1, 0), terrainUnderDefender);

            List<CombatUnit> units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(maxHP: 1000f, attackDamage: 20f), 0, 0),
                CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(maxHP: 1000f, attackDamage: 0f, defense: 10f), 1, 0)
            };

            BattleSimulator simulator = new BattleSimulator(
                grid,
                units,
                CombatTestFactory.Config(),
                terrain: TerrainModifierProvider.CreateCapstoneTable2ForTesting());

            BattleTurnResult turn = simulator.ExecuteTurn();
            return CombatTestFactory.FirstDamageBy(turn.Events, 1);
        }

        [Test]
        public void AMovingUnitNeverEntersABambooBarricade()
        {
            BattleGrid grid = CombatTestFactory.FlatGrid(5, 5);
            GridCoord barricade = new GridCoord(2, 2);
            grid.SetTerrain(barricade, TerrainType.BambooBarricade);

            List<CombatUnit> units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(), 0, 2),
                CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(), 4, 2)
            };

            BattleSimulator simulator = new BattleSimulator(grid, units, CombatTestFactory.Config(0, 10));
            BattleResult result = simulator.RunToCompletion();

            for (int i = 0; i < result.Events.Count; i++)
            {
                BattleEvent e = result.Events[i];
                if (e.Type == BattleEventType.UnitMoved)
                {
                    Assert.AreNotEqual(barricade, e.To, "A unit stepped onto an impassable bamboo barricade.");
                    Assert.AreNotEqual(
                        TerrainType.BambooBarricade,
                        grid.GetTerrain(e.To),
                        "A unit stepped onto an impassable bamboo barricade.");
                }
            }

            for (int i = 0; i < simulator.Units.Count; i++)
            {
                Assert.AreNotEqual(barricade, simulator.Units[i].Position);
            }
        }

        [Test]
        public void AFullBarricadeWallStallsTheBattleIntoTheTurnCapRatherThanLoopingForever()
        {
            BattleGrid grid = CombatTestFactory.FlatGrid(5, 5);
            for (int y = 0; y < 5; y++)
            {
                grid.SetTerrain(new GridCoord(2, y), TerrainType.BambooBarricade);
            }

            List<CombatUnit> units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(), 0, 0),
                CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(), 4, 0)
            };

            BattleResult result = new BattleSimulator(grid, units, CombatTestFactory.Config(0, 5)).RunToCompletion();

            Assert.AreEqual(BattleOutcome.Draw, result.Outcome);
            Assert.AreEqual(5, result.TurnsElapsed);
            Assert.AreEqual(1, result.KatipunanAlive);
            Assert.AreEqual(1, result.SpanishAlive);
        }

        [Test]
        public void ALoneKatipunanUnitBeatingALoneSpanishUnitEndsInVictory()
        {
            BattleGrid grid = CombatTestFactory.FlatGrid(4, 1);
            List<CombatUnit> units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(maxHP: 100f, attackDamage: 100f), 0, 0),
                CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(maxHP: 30f, attackDamage: 5f), 1, 0)
            };

            BattleResult result = new BattleSimulator(grid, units, CombatTestFactory.Config()).RunToCompletion();

            Assert.AreEqual(BattleOutcome.Victory, result.Outcome);
            Assert.AreEqual(1, result.TurnsElapsed);
            Assert.AreEqual(0, result.SpanishAlive);
        }

        [Test]
        public void ALoneKatipunanUnitLosingToALoneSpanishUnitEndsInDefeat()
        {
            BattleGrid grid = CombatTestFactory.FlatGrid(4, 1);
            List<CombatUnit> units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, Team.Spanish, CombatTestFactory.Stats(maxHP: 100f, attackDamage: 100f), 0, 0),
                CombatTestFactory.Unit(2, Team.Katipunan, CombatTestFactory.Stats(maxHP: 30f, attackDamage: 5f), 1, 0)
            };

            BattleResult result = new BattleSimulator(grid, units, CombatTestFactory.Config()).RunToCompletion();

            Assert.AreEqual(BattleOutcome.Defeat, result.Outcome);
            Assert.AreEqual(0, result.KatipunanAlive);
        }

        [Test]
        public void MutualAnnihilationResolvesToTheConfiguredOutcome()
        {
            Assert.AreEqual(BattleOutcome.Draw, RunMutualAnnihilation(BattleOutcome.Draw));
            Assert.AreEqual(BattleOutcome.Defeat, RunMutualAnnihilation(BattleOutcome.Defeat));
        }

        private static BattleOutcome RunMutualAnnihilation(BattleOutcome configured)
        {
            BattleGrid grid = CombatTestFactory.FlatGrid(4, 1);
            CombatUnit katipunan = CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(), 0, 0);
            CombatUnit spanish = CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(), 1, 0);

            CombatConfig config = CombatTestFactory.Config();
            config.MutualAnnihilationOutcome = configured;

            BattleSimulator simulator = new BattleSimulator(
                grid,
                new List<CombatUnit> { katipunan, spanish },
                config);

            // Simultaneous death cannot arise from sequential activation, so it is staged directly:
            // the point under test is the end-of-turn HP evaluation, not how the HP got to zero.
            katipunan.Kill();
            spanish.Kill();

            return simulator.ExecuteTurn().Outcome;
        }

        [Test]
        public void ExecutingATurnAfterTheBattleHasFinishedThrows()
        {
            BattleGrid grid = CombatTestFactory.FlatGrid(4, 1);
            List<CombatUnit> units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(attackDamage: 100f), 0, 0),
                CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(maxHP: 10f), 1, 0)
            };

            BattleSimulator simulator = new BattleSimulator(grid, units, CombatTestFactory.Config());
            simulator.RunToCompletion();

            Assert.Throws<System.InvalidOperationException>(() => simulator.ExecuteTurn());
        }

        [Test]
        public void TheLogOpensWithATurnStartAndClosesWithABattleEnd()
        {
            BattleResult result = BuildDuel(3).RunToCompletion();

            Assert.Greater(result.Events.Count, 0);
            Assert.AreEqual(BattleEventType.TurnStarted, result.Events[0].Type);
            Assert.AreEqual(BattleEventType.BattleEnded, result.Events[result.Events.Count - 1].Type);
            Assert.AreEqual(result.Outcome.ToString(), result.Events[result.Events.Count - 1].Detail);
        }

        [Test]
        public void EncampmentTentRegeneratesFivePercentOfMaxHpPerTurn()
        {
            BattleGrid grid = CombatTestFactory.FlatGrid(6, 6);
            grid.SetTerrain(new GridCoord(0, 0), TerrainType.EncampmentTent);

            CombatUnit resting = CombatTestFactory.Unit(
                1, Team.Katipunan, CombatTestFactory.Stats(maxHP: 100f, movementSpeed: 0f), 0, 0);
            CombatUnit distant = CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(), 5, 5);
            resting.ApplyDamage(50f);

            BattleSimulator simulator = new BattleSimulator(
                grid,
                new List<CombatUnit> { resting, distant },
                CombatTestFactory.Config(0, 10),
                terrain: TerrainModifierProvider.CreateCapstoneTable2ForTesting());

            simulator.ExecuteTurn();

            Assert.AreEqual(55f, resting.CurrentHP, 0.0001f);
        }

        [Test]
        public void StandardGridDoesNotRegenerate()
        {
            BattleGrid grid = CombatTestFactory.FlatGrid(6, 6);

            CombatUnit resting = CombatTestFactory.Unit(
                1, Team.Katipunan, CombatTestFactory.Stats(maxHP: 100f, movementSpeed: 0f), 0, 0);
            CombatUnit distant = CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(), 5, 5);
            resting.ApplyDamage(50f);

            BattleSimulator simulator = new BattleSimulator(
                grid,
                new List<CombatUnit> { resting, distant },
                CombatTestFactory.Config(0, 10),
                terrain: TerrainModifierProvider.CreateCapstoneTable2ForTesting());

            simulator.ExecuteTurn();

            Assert.AreEqual(50f, resting.CurrentHP, 0.0001f);
        }

        [Test]
        public void BondedUnitsPlacedAdjacentReceiveTheBuffDuringTheTurn()
        {
            CombatUnit buffed = RunBondScenario(partnerX: 0, partnerY: 1);
            Assert.AreEqual(12f, buffed.GetEffectiveStat(StatKind.AttackDamage), 0.0001f);
        }

        [Test]
        public void TheSameBondedUnitsPlacedFarApartReceiveNothing()
        {
            CombatUnit unbuffed = RunBondScenario(partnerX: 5, partnerY: 5);
            Assert.AreEqual(10f, unbuffed.GetEffectiveStat(StatKind.AttackDamage), 0.0001f);
        }

        /// <summary>
        /// Places Evangelista at the origin and Aguinaldo at the given cell with a distant enemy, runs
        /// one turn, and returns Evangelista so the caller can inspect his effective stats.
        /// </summary>
        private static CombatUnit RunBondScenario(int partnerX, int partnerY)
        {
            BattleGrid grid = CombatTestFactory.FlatGrid(10, 10);
            UnitStats stats = CombatTestFactory.Stats(attackDamage: 10f, movementSpeed: 0f);

            CombatUnit evangelista = CombatTestFactory.Unit(1, Team.Katipunan, stats, 0, 0, "Evangelista");
            CombatUnit aguinaldo = CombatTestFactory.Unit(2, Team.Katipunan, stats, partnerX, partnerY, "Aguinaldo");
            CombatUnit enemy = CombatTestFactory.Unit(3, Team.Spanish, CombatTestFactory.Stats(movementSpeed: 0f), 9, 9);

            BattleSimulator simulator = new BattleSimulator(
                grid,
                new List<CombatUnit> { evangelista, aguinaldo, enemy },
                CombatTestFactory.Config(0, 10),
                kapatiran: new KapatiranResolver(new List<KapatiranBond> { CombatTestFactory.AttackBond() }));

            simulator.ExecuteTurn();
            return evangelista;
        }

        [Test]
        public void TheRankAFlatBonusExtendsAttackRangeByExactlyOneCell()
        {
            // Base range 2, target at Manhattan distance 3: only a flat +1 puts it in reach.
            BattleTurnResult withBond = RunRangeScenario(enemyDistance: 3, withBond: true);
            BattleTurnResult withoutBond = RunRangeScenario(enemyDistance: 3, withBond: false);

            Assert.IsTrue(CombatTestFactory.HasEvent(withBond.Events, BattleEventType.UnitAttacked, 1),
                "Range 2 plus a flat +1 must reach a target three cells away.");
            Assert.IsFalse(CombatTestFactory.HasEvent(withBond.Events, BattleEventType.UnitMoved, 1),
                "A unit already in range must attack rather than advance.");

            Assert.IsFalse(CombatTestFactory.HasEvent(withoutBond.Events, BattleEventType.UnitAttacked, 1),
                "Without the bond, range 2 cannot reach three cells.");
            Assert.IsTrue(CombatTestFactory.HasEvent(withoutBond.Events, BattleEventType.UnitMoved, 1));
        }

        [Test]
        public void TheFlatRangeBonusIsNotTreatedAsAPercentage()
        {
            // If "+1 Attack Range" were stored as a percentage it would read as +100%, taking base
            // range 2 to 4 and putting a target four cells away in reach. It must not.
            BattleTurnResult result = RunRangeScenario(enemyDistance: 4, withBond: true);

            Assert.IsFalse(CombatTestFactory.HasEvent(result.Events, BattleEventType.UnitAttacked, 1),
                "A flat +1 must not behave like +100%.");
            Assert.IsTrue(CombatTestFactory.HasEvent(result.Events, BattleEventType.UnitMoved, 1));
        }

        /// <summary>
        /// Marksman (base Attack Range 2) at the origin with the Trench Engineer adjacent, and an
        /// enemy the given number of cells away along the X axis. Runs one turn.
        /// </summary>
        private static BattleTurnResult RunRangeScenario(int enemyDistance, bool withBond)
        {
            BattleGrid grid = CombatTestFactory.FlatGrid(10, 10);

            CombatUnit marksman = CombatTestFactory.Unit(
                1, Team.Katipunan, CombatTestFactory.Stats(attackRange: 2f), 0, 0, "Marksman");
            CombatUnit engineer = CombatTestFactory.Unit(
                2, Team.Katipunan, CombatTestFactory.Stats(movementSpeed: 0f), 0, 1, "Engineer");
            CombatUnit enemy = CombatTestFactory.Unit(
                3, Team.Spanish, CombatTestFactory.Stats(maxHP: 500f, movementSpeed: 0f), enemyDistance, 0);

            KapatiranResolver resolver = withBond
                ? new KapatiranResolver(new List<KapatiranBond> { CombatTestFactory.MarksmanEngineerRankA() })
                : KapatiranResolver.CreateEmpty();

            BattleSimulator simulator = new BattleSimulator(
                grid,
                new List<CombatUnit> { marksman, engineer, enemy },
                CombatTestFactory.Config(0, 10),
                kapatiran: resolver);

            return simulator.ExecuteTurn();
        }

        [Test]
        public void ActivationOrderFollowsUnitIdNotConstructionOrder()
        {
            BattleGrid grid = CombatTestFactory.FlatGrid(4, 1);
            CombatUnit high = CombatTestFactory.Unit(9, Team.Katipunan, CombatTestFactory.Stats(), 0, 0);
            CombatUnit low = CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(), 1, 0);

            BattleSimulator simulator = new BattleSimulator(
                grid, new List<CombatUnit> { high, low }, CombatTestFactory.Config());

            Assert.AreEqual(2, simulator.Units[0].Id);
            Assert.AreEqual(9, simulator.Units[1].Id);

            BattleTurnResult turn = simulator.ExecuteTurn();

            int firstAttackerId = -1;
            for (int i = 0; i < turn.Events.Count; i++)
            {
                if (turn.Events[i].Type == BattleEventType.UnitAttacked)
                {
                    firstAttackerId = turn.Events[i].ActorId;
                    break;
                }
            }

            Assert.AreEqual(2, firstAttackerId, "The lower id must act first regardless of list order.");
        }

        [Test]
        public void DuplicateUnitIdsAreRejected()
        {
            BattleGrid grid = CombatTestFactory.FlatGrid(4, 1);
            List<CombatUnit> units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(), 0, 0),
                CombatTestFactory.Unit(1, Team.Spanish, CombatTestFactory.Stats(), 1, 0)
            };

            Assert.Throws<System.ArgumentException>(
                () => new BattleSimulator(grid, units, CombatTestFactory.Config()));
        }

        [Test]
        public void CoastalShallowsSlowsAUnitWithoutFreezingIt()
        {
            // Movement Speed 1.0 reduced by 15% is 0.85, which truncates to zero whole cells. The
            // carry mechanism must still let the unit advance, just less often than on open ground.
            BattleGrid grid = CombatTestFactory.FlatGrid(10, 1);
            for (int x = 0; x < 10; x++)
            {
                grid.SetTerrain(new GridCoord(x, 0), TerrainType.CoastalShallows);
            }

            CombatUnit wader = CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(), 0, 0);
            CombatUnit distant = CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(movementSpeed: 0f), 9, 0);

            BattleSimulator simulator = new BattleSimulator(
                grid,
                new List<CombatUnit> { wader, distant },
                CombatTestFactory.Config(0, 6),
                terrain: TerrainModifierProvider.CreateCapstoneTable2ForTesting());

            for (int turn = 0; turn < 6; turn++)
            {
                if (simulator.IsFinished)
                {
                    break;
                }

                simulator.ExecuteTurn();
            }

            // Six turns at 0.85 cells per turn banks 5.1 cells, so exactly five steps are taken.
            Assert.AreEqual(5, wader.Position.X, "Expected five steps in six turns at 85% movement speed.");
        }
    }
}
