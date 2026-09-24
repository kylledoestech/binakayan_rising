using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using NUnit.Framework;

namespace BinakayanRising.Tests.Combat
{
    /// <summary>
    /// Covers the Level 2 win rules (DESIGN-DECISIONS #21): Escort, where a supply cart must
    /// outlast the turn cap (#37), and Sabotage, where a squad must end a turn on a target cell (#38).
    /// </summary>
    [TestFixture]
    public class BattleObjectiveTests
    {
        private const int CartId = 50;

        private static CombatUnit Cart(int x, int y, float maxHP = 100f)
        {
            CombatUnit cart = CombatTestFactory.Unit(CartId, Team.Katipunan,
                CombatTestFactory.Stats(maxHP: maxHP, attackDamage: 0f, movementSpeed: 0f), x, y);
            cart.Abilities = UnitAbilities.NonCombatantCargo();
            return cart;
        }

        private static CombatConfig Config(BattleObjective objective, int maxTurns)
        {
            CombatConfig config = CombatTestFactory.Config(0, maxTurns);
            config.Objective = objective;
            return config;
        }

        // ---------------------------------------------------------------- Escort

        [Test]
        public void EscortIsLostTheTurnTheCartFallsEvenWithTheSquadStanding()
        {
            var units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(maxHP: 1000f, attackDamage: 1f, movementSpeed: 0f), 0, 0),
                Cart(5, 0, maxHP: 10f),
                CombatTestFactory.Unit(100, Team.Spanish, CombatTestFactory.Stats(maxHP: 1000f, attackDamage: 50f, movementSpeed: 0f), 6, 0)
            };

            BattleResult result = new BattleSimulator(CombatTestFactory.FlatGrid(8, 1), units, Config(BattleObjective.Escort(CartId), 20)).RunToCompletion();

            Assert.AreEqual(BattleOutcome.Defeat, result.Outcome);
            Assert.AreEqual(1, result.TurnsElapsed);
            Assert.AreEqual(1, result.KatipunanAlive, "the squad still stood");
        }

        [Test]
        public void EscortIsWonWhenTheCartOutlastsTheTurnCap()
        {
            // Nobody can reach anybody: the cart simply waits out the clock.
            var units = new List<CombatUnit>
            {
                Cart(0, 0),
                CombatTestFactory.Unit(100, Team.Spanish, CombatTestFactory.Stats(movementSpeed: 0f), 7, 0)
            };

            BattleResult result = new BattleSimulator(CombatTestFactory.FlatGrid(8, 1), units, Config(BattleObjective.Escort(CartId), 20)).RunToCompletion();

            Assert.AreEqual(BattleOutcome.Victory, result.Outcome);
            Assert.AreEqual(20, result.TurnsElapsed);
        }

        [Test]
        public void EscortIsWonEarlyByRoutingTheEnemy()
        {
            var units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(attackDamage: 100f, movementSpeed: 0f), 0, 0),
                Cart(3, 0),
                CombatTestFactory.Unit(100, Team.Spanish, CombatTestFactory.Stats(maxHP: 10f, movementSpeed: 0f), 1, 0)
            };

            BattleResult result = new BattleSimulator(CombatTestFactory.FlatGrid(8, 1), units, Config(BattleObjective.Escort(CartId), 20)).RunToCompletion();

            Assert.AreEqual(BattleOutcome.Victory, result.Outcome);
            Assert.AreEqual(1, result.TurnsElapsed);
        }

        [Test]
        public void UnderEscortTheSpanishWalkForTheCartRatherThanTheNearestDefender()
        {
            // The defender at (0,1) is two tiles from the regular; the cart at (3,5) is five.
            var units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(maxHP: 1000f, attackDamage: 0f, movementSpeed: 0f), 0, 1),
                Cart(3, 5),
                CombatTestFactory.Unit(100, Team.Spanish, CombatTestFactory.Stats(movementSpeed: 1f), 0, 3)
            };

            new BattleSimulator(CombatTestFactory.FlatGrid(6, 8), units, Config(BattleObjective.Escort(CartId), 20)).ExecuteTurn();

            // Under Rout it would step to (0,2), beside the defender.
            Assert.AreEqual(4, GridDistance.Manhattan(units[2].Position, units[1].Position));
            Assert.AreEqual(3, GridDistance.Manhattan(units[2].Position, units[0].Position));
        }

        [Test]
        public void UnderEscortTheSpanishStrikeTheCartWhenItIsInReach()
        {
            var units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(maxHP: 1000f, attackDamage: 0f, movementSpeed: 0f), 1, 0),
                Cart(1, 2),
                CombatTestFactory.Unit(100, Team.Spanish, CombatTestFactory.Stats(movementSpeed: 0f), 1, 1)
            };

            BattleTurnResult turn = new BattleSimulator(CombatTestFactory.FlatGrid(4, 4), units, Config(BattleObjective.Escort(CartId), 20)).ExecuteTurn();

            BattleEvent attack = default(BattleEvent);
            for (int i = 0; i < turn.Events.Count; i++)
            {
                if (turn.Events[i].Type == BattleEventType.UnitAttacked && turn.Events[i].ActorId == 100)
                {
                    attack = turn.Events[i];
                }
            }

            Assert.AreEqual(CartId, attack.TargetId, "the defender has the lower id, but the cart comes first");
        }

        // ---------------------------------------------------------------- Sabotage

        [Test]
        public void SabotageIsWonWhenASquadMemberEndsATurnOnTheTarget()
        {
            // A rooted unit (Movement 0) is still brought up to the seeker speed.
            var units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(movementSpeed: 0f), 5, 0),
                CombatTestFactory.Unit(100, Team.Spanish, CombatTestFactory.Stats(movementSpeed: 0f), 0, 3)
            };

            BattleResult result = new BattleSimulator(CombatTestFactory.FlatGrid(8, 4), units,
                Config(BattleObjective.Sabotage(new GridCoord(2, 0)), 30)).RunToCompletion();

            Assert.AreEqual(BattleOutcome.Victory, result.Outcome);
            Assert.AreEqual(3, result.TurnsElapsed, "three tiles at one a turn");
            Assert.AreEqual(1, result.SpanishAlive, "the guard was never fought");
        }

        [Test]
        public void SaboteursFightWhatIsInReachOnTheWay()
        {
            var units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(attackDamage: 30f, movementSpeed: 0f), 3, 0),
                CombatTestFactory.Unit(100, Team.Spanish, CombatTestFactory.Stats(maxHP: 30f, attackDamage: 1f, movementSpeed: 0f), 2, 0)
            };

            BattleTurnResult turn = new BattleSimulator(CombatTestFactory.FlatGrid(6, 1), units,
                Config(BattleObjective.Sabotage(new GridCoord(0, 0)), 30)).ExecuteTurn();

            Assert.IsTrue(CombatTestFactory.HasEvent(turn.Events, BattleEventType.UnitAttacked, 1));
            Assert.IsFalse(CombatTestFactory.HasEvent(turn.Events, BattleEventType.UnitMoved, 1));
        }

        [Test]
        public void RoutingTheGuardDoesNotEndASabotageBattle()
        {
            var units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(attackDamage: 100f, movementSpeed: 0f), 3, 0),
                CombatTestFactory.Unit(100, Team.Spanish, CombatTestFactory.Stats(maxHP: 10f, movementSpeed: 0f), 4, 0)
            };

            var simulator = new BattleSimulator(CombatTestFactory.FlatGrid(6, 1), units,
                Config(BattleObjective.Sabotage(new GridCoord(0, 0)), 30));
            BattleTurnResult first = simulator.ExecuteTurn();
            Assert.AreEqual(0, first.SpanishAlive);
            Assert.AreEqual(BattleOutcome.InProgress, first.Outcome);

            BattleResult result = simulator.RunToCompletion();
            Assert.AreEqual(BattleOutcome.Victory, result.Outcome);
            Assert.AreEqual(new GridCoord(0, 0), units[0].Position);
        }

        [Test]
        public void SabotageIsLostAtTheTurnCap()
        {
            // A barricade wall the squad cannot cross.
            BattleGrid grid = CombatTestFactory.FlatGrid(6, 1);
            grid.SetTerrain(new GridCoord(2, 0), TerrainType.BambooBarricade);
            var units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(movementSpeed: 0f), 4, 0),
                CombatTestFactory.Unit(100, Team.Spanish, CombatTestFactory.Stats(movementSpeed: 0f), 0, 0)
            };

            BattleResult result = new BattleSimulator(grid, units, Config(BattleObjective.Sabotage(new GridCoord(1, 0)), 12)).RunToCompletion();

            Assert.AreEqual(BattleOutcome.Defeat, result.Outcome);
            Assert.AreEqual(12, result.TurnsElapsed);
        }

        [Test]
        public void SabotageIsLostWhenTheSquadFalls()
        {
            var units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(maxHP: 5f, attackDamage: 0f, movementSpeed: 0f), 4, 0),
                CombatTestFactory.Unit(100, Team.Spanish, CombatTestFactory.Stats(attackDamage: 50f, movementSpeed: 0f), 3, 0)
            };

            BattleResult result = new BattleSimulator(CombatTestFactory.FlatGrid(6, 1), units,
                Config(BattleObjective.Sabotage(new GridCoord(0, 0)), 30)).RunToCompletion();

            Assert.AreEqual(BattleOutcome.Defeat, result.Outcome);
            Assert.AreEqual(1, result.TurnsElapsed);
        }

        [Test]
        public void TheDefaultObjectiveIsRout()
        {
            Assert.AreEqual(ObjectiveKind.Rout, new CombatConfig().Objective.Kind);
        }
    }
}
