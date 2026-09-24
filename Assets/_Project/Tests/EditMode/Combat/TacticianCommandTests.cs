using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using NUnit.Framework;

namespace BinakayanRising.Tests.Combat
{
    /// <summary>
    /// Capstone Table 4's four Tactician's Command effects (#43): what each does to the battle at
    /// the start of the turn it is issued on, and that re-simulating a pre-resolved battle with a
    /// command reproduces everything before the quiz and may change everything after it.
    /// </summary>
    [TestFixture]
    public class TacticianCommandTests
    {
        private const float Tolerance = 0.0001f;

        private static BattleSimulator Simulator(params CombatUnit[] units)
        {
            return new BattleSimulator(CombatTestFactory.FlatGrid(8, 4), units, CombatTestFactory.Config(0, 30));
        }

        private static List<BattleEvent> OfType(IReadOnlyList<BattleEvent> events, BattleEventType type)
        {
            var found = new List<BattleEvent>();
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    found.Add(events[i]);
                }
            }

            return found;
        }

        // ------------------------------------------------------------------ heal

        [Test]
        public void TheHealRestoresTenPercentOfMaxHpToEveryLivingAlly()
        {
            CombatUnit wounded = CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(movementSpeed: 0f), 0, 0);
            CombatUnit nearlyFull = CombatTestFactory.Unit(2, Team.Katipunan, CombatTestFactory.Stats(movementSpeed: 0f), 0, 2);
            CombatUnit enemy = CombatTestFactory.Unit(3, Team.Spanish, CombatTestFactory.Stats(movementSpeed: 0f), 7, 3);
            wounded.ApplyDamage(50f);
            nearlyFull.ApplyDamage(4f);
            BattleSimulator simulator = Simulator(wounded, nearlyFull, enemy);

            simulator.QueueCommand(TacticianCommand.MapWideHeal, TacticianCommands.HealFraction, 1);
            BattleTurnResult turn = simulator.ExecuteTurn();

            Assert.AreEqual(BattleEventType.CommandIssued, turn.Events[1].Type, "the command lands right after the turn starts");
            Assert.AreEqual(60f, wounded.CurrentHP, Tolerance, "+10% of 100 Max HP");
            Assert.AreEqual(100f, nearlyFull.CurrentHP, Tolerance, "a heal never passes Max HP");
            Assert.AreEqual(100f, enemy.CurrentHP, Tolerance, "the enemy is not healed");
            Assert.AreEqual(2, OfType(turn.Events, BattleEventType.HpRegenerated).Count);
        }

        // ------------------------------------------------------------------ attack buff

        [Test]
        public void TheAttackBuffAddsTenPercentForExactlyOneTurn()
        {
            CombatUnit attacker = CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(attackDamage: 20f, movementSpeed: 0f), 0, 0);
            CombatUnit enemy = CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(maxHP: 1000f, attackDamage: 0f, movementSpeed: 0f), 1, 0);
            BattleSimulator simulator = Simulator(attacker, enemy);

            simulator.ExecuteTurn();
            simulator.QueueCommand(TacticianCommand.AttackBuff, TacticianCommands.AttackFraction, TacticianCommands.AttackTurns);
            BattleTurnResult buffed = simulator.ExecuteTurn();
            BattleTurnResult after = simulator.ExecuteTurn();

            Assert.AreEqual(22f, CombatTestFactory.FirstDamageBy(buffed.Events, 1), Tolerance, "20 attack +10%");
            Assert.AreEqual(20f, CombatTestFactory.FirstDamageBy(after.Events, 1), Tolerance, "the buff lasts one turn");
        }

        // ------------------------------------------------------------------ reset

        [Test]
        public void TheResetSendsEveryEnemyBackToItsStartingCell()
        {
            CombatUnit holder = CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(maxHP: 1000f, movementSpeed: 0f), 0, 0);
            CombatUnit enemy = CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(movementSpeed: 1f), 7, 0);
            BattleSimulator simulator = Simulator(holder, enemy);

            simulator.ExecuteTurn();
            simulator.ExecuteTurn();
            simulator.ExecuteTurn();
            Assert.AreEqual(new GridCoord(4, 0), enemy.Position, "three steps toward the line");

            simulator.QueueCommand(TacticianCommand.ResetEnemyPositions, 0f, 1);
            BattleTurnResult turn = simulator.ExecuteTurn();

            List<BattleEvent> moves = OfType(turn.Events, BattleEventType.UnitMoved);
            Assert.AreEqual(new GridCoord(4, 0), moves[0].From);
            Assert.AreEqual(new GridCoord(7, 0), moves[0].To, "back to where it spawned");
            Assert.AreEqual(new GridCoord(6, 0), enemy.Position, "and then it advances again this turn");
        }

        [Test]
        public void AnEnemyWhoseStartingCellIsTakenGoesToTheNearestFreeCell()
        {
            CombatUnit holder = CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(maxHP: 1000f, movementSpeed: 0f), 0, 0);
            CombatUnit enemy = CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(movementSpeed: 1f), 7, 0);
            BattleSimulator simulator = Simulator(holder, enemy);

            simulator.ExecuteTurn();
            simulator.ExecuteTurn();
            simulator.ExecuteTurn();
            holder.MoveTo(new GridCoord(7, 0));
            simulator.QueueCommand(TacticianCommand.ResetEnemyPositions, 0f, 1);
            BattleTurnResult turn = simulator.ExecuteTurn();

            BattleEvent move = OfType(turn.Events, BattleEventType.UnitMoved)[0];
            Assert.AreEqual(new GridCoord(4, 0), move.From);
            Assert.AreEqual(new GridCoord(6, 0), move.To, "(6,0) and (7,1) are both one cell away; the lower row wins");
        }

        // ------------------------------------------------------------------ revive

        [Test]
        public void TheReviveBringsBackTheLastFallenAllyAtHalfHealthOnItsDeploymentCell()
        {
            UnitStats guard = CombatTestFactory.Stats(maxHP: 1000f, attackDamage: 10f, movementSpeed: 0f);
            CombatUnit first = CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(maxHP: 5f, movementSpeed: 0f), 0, 0);
            CombatUnit second = CombatTestFactory.Unit(2, Team.Katipunan, CombatTestFactory.Stats(maxHP: 15f, movementSpeed: 0f), 0, 3);
            CombatUnit survivor = CombatTestFactory.Unit(3, Team.Katipunan, CombatTestFactory.Stats(maxHP: 1000f, movementSpeed: 0f), 5, 1);
            CombatUnit enemyA = CombatTestFactory.Unit(4, Team.Spanish, guard, 1, 0);
            CombatUnit enemyB = CombatTestFactory.Unit(5, Team.Spanish, guard, 1, 3);
            BattleSimulator simulator = Simulator(first, second, survivor, enemyA, enemyB);

            Assert.IsFalse(simulator.CanIssue(TacticianCommand.ReviveFallenUnit), "nobody has fallen yet");
            simulator.ExecuteTurn();
            Assert.IsFalse(first.IsAlive);
            Assert.IsTrue(simulator.CanIssue(TacticianCommand.ReviveFallenUnit));
            simulator.ExecuteTurn();
            Assert.IsFalse(second.IsAlive, "the second falls a turn later");

            simulator.QueueCommand(TacticianCommand.ReviveFallenUnit, TacticianCommands.ReviveFraction, 1);
            BattleTurnResult turn = simulator.ExecuteTurn();

            BattleEvent revived = OfType(turn.Events, BattleEventType.UnitRevived)[0];
            Assert.AreEqual(2, revived.ActorId, "the most recent death is the one undone");
            Assert.AreEqual(new GridCoord(0, 3), revived.To);
            Assert.AreEqual(7.5f, revived.Amount, Tolerance, "half of 15 Max HP");
            Assert.IsFalse(first.IsAlive, "only one unit comes back");
        }

        [Test]
        public void ARevivedUnitWhoseCellIsTakenReturnsOnTheNearestFreeDeployCell()
        {
            BattleGrid grid = new BattleGrid(8, 4, TerrainType.StandardGrid);
            grid.SetDeployable(new GridCoord(0, 0), true);
            grid.SetDeployable(new GridCoord(0, 3), true);
            CombatUnit fallen = CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(maxHP: 5f, movementSpeed: 0f), 0, 0);
            CombatUnit enemy = CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(maxHP: 1000f, attackDamage: 10f, movementSpeed: 0f), 1, 0);
            CombatUnit ally = CombatTestFactory.Unit(3, Team.Katipunan, CombatTestFactory.Stats(maxHP: 1000f, movementSpeed: 0f), 6, 3);
            var simulator = new BattleSimulator(grid, new[] { fallen, enemy, ally }, CombatTestFactory.Config(0, 30));

            simulator.ExecuteTurn();
            enemy.MoveTo(new GridCoord(0, 0));
            simulator.QueueCommand(TacticianCommand.ReviveFallenUnit, TacticianCommands.ReviveFraction, 1);
            BattleTurnResult turn = simulator.ExecuteTurn();

            Assert.AreEqual(new GridCoord(0, 3), OfType(turn.Events, BattleEventType.UnitRevived)[0].To,
                "a free deploy cell beats a nearer ordinary one");
        }

        // ------------------------------------------------------------------ re-simulation

        /// <summary>
        /// Four defenders dug in against six advancing regulars, with every die in play: dodges,
        /// misses and crits. The same seed rebuilds the same battle.
        /// </summary>
        private static BattleSimulator Skirmish(int seed)
        {
            UnitStats defender = CombatTestFactory.Stats(maxHP: 120f, attackDamage: 16f, defense: 4f, evasion: 0.1f,
                rangedAccuracy: 0.85f, attackRange: 2f, criticalHitChance: 0.15f, movementSpeed: 0f);
            UnitStats regular = CombatTestFactory.Stats(maxHP: 100f, attackDamage: 15f, defense: 4f, evasion: 0.1f,
                rangedAccuracy: 0.85f, attackRange: 1f, criticalHitChance: 0.1f, movementSpeed: 1f);

            var units = new List<CombatUnit>();
            for (int i = 0; i < 4; i++)
            {
                units.Add(CombatTestFactory.Unit(1 + i, Team.Katipunan, defender, 0, i * 2));
            }

            for (int i = 0; i < 6; i++)
            {
                units.Add(CombatTestFactory.Unit(100 + i, Team.Spanish, regular, 9, 1 + i));
            }

            return new BattleSimulator(CombatTestFactory.FlatGrid(10, 8), units, CombatTestFactory.Config(seed, 80));
        }

        private static int IndexOfTurnStart(BattleResult result, int turn)
        {
            for (int i = 0; i < result.Events.Count; i++)
            {
                if (result.Events[i].Type == BattleEventType.TurnStarted && result.Events[i].Turn == turn)
                {
                    return i;
                }
            }

            return -1;
        }

        [TestCase(TacticianCommand.MapWideHeal)]
        [TestCase(TacticianCommand.AttackBuff)]
        [TestCase(TacticianCommand.ResetEnemyPositions)]
        [TestCase(TacticianCommand.ReviveFallenUnit)]
        public void ReSimulatingWithACommandKeepsEverythingBeforeTheQuizTurn(TacticianCommand command)
        {
            const int QuizTurn = 9;
            BattleResult original = Skirmish(1896).RunToCompletion();
            BattleResult commanded = TacticianCommands.RunWithCommand(() => Skirmish(1896), QuizTurn, command,
                TacticianCommands.DefaultMagnitude(command), TacticianCommands.AttackTurns);

            int start = IndexOfTurnStart(original, QuizTurn);
            Assert.Greater(start, 0, "the battle must still be running at the quiz turn");
            for (int i = 0; i <= start; i++)
            {
                Assert.AreEqual(original.Events[i].ToString(), commanded.Events[i].ToString(), "event " + i + " changed");
            }

            Assert.AreEqual(BattleEventType.CommandIssued, commanded.Events[start + 1].Type);
            Assert.AreEqual(command.ToString(), commanded.Events[start + 1].Detail);
            Assert.AreNotEqual(original.ToLogText(), commanded.ToLogText(), "the rest of the battle is fought again");
        }

        [Test]
        public void ReSimulationIsItselfDeterministic()
        {
            BattleResult first = TacticianCommands.RunWithCommand(() => Skirmish(7), 6, TacticianCommand.ReviveFallenUnit, 0.5f, 1);
            BattleResult second = TacticianCommands.RunWithCommand(() => Skirmish(7), 6, TacticianCommand.ReviveFallenUnit, 0.5f, 1);
            Assert.AreEqual(first.ToLogText(), second.ToLogText());
        }

        [Test]
        public void ACommandCanChangeHowTheBattleEnds()
        {
            // Seed and turn found by search: the defenders lose this one unaided, and a heal at
            // turn 12 is enough to turn it into a win.
            BattleResult original = Skirmish(34).RunToCompletion();
            BattleResult healed = TacticianCommands.RunWithCommand(() => Skirmish(34), 12, TacticianCommand.MapWideHeal,
                TacticianCommands.HealFraction, 1);

            Assert.AreEqual(BattleOutcome.Defeat, original.Outcome, original.ToString());
            Assert.AreEqual(BattleOutcome.Victory, healed.Outcome, healed.ToString());
        }

        [Test]
        public void ACommandIssuedAfterTheBattleEndedChangesNothing()
        {
            BattleResult original = Skirmish(3).RunToCompletion();
            BattleResult late = TacticianCommands.RunWithCommand(() => Skirmish(3), original.TurnsElapsed + 5,
                TacticianCommand.MapWideHeal, 0.1f, 1);
            Assert.AreEqual(original.ToLogText(), late.ToLogText());
        }
    }
}
