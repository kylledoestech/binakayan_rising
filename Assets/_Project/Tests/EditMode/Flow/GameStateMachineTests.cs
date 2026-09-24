using System.Collections.Generic;
using BinakayanRising.Gameplay.Flow;
using NUnit.Framework;
using UnityEngine;

namespace BinakayanRising.Tests.Flow
{
    /// <summary>
    /// The live battle path through Figure 2: the replay starting enters Combat, the mid-battle
    /// question enters Quiz and returns, and the end of the battle leaves from whichever state the
    /// machine is in, using only legal edges.
    /// </summary>
    /// <remarks>
    /// Runs in the Unity EditMode runner only (the machine is a MonoBehaviour); the offline
    /// runner in Tools/battle-sim covers the engine-free Core assembly.
    /// </remarks>
    [TestFixture]
    public class GameStateMachineTests
    {
        private GameObject host;
        private GameStateMachine machine;
        private List<GameState> entered;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("GameStateMachineTests");
            machine = host.AddComponent<GameStateMachine>();
            entered = new List<GameState>();
            machine.StateChanged += (from, to) => entered.Add(to);
            Assert.IsTrue(machine.LaunchApplication());
            Assert.IsTrue(machine.LoadSaveOrNewGame());
            Assert.IsTrue(machine.SelectStage());
            Assert.IsTrue(machine.LoadIsometricMap());
            Assert.AreEqual(GameState.Deployment, machine.CurrentState);
            entered.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }

        [Test]
        public void FullBattle_DeploymentCombatQuizCombatVictory_ThenCamp()
        {
            Assert.IsTrue(machine.EnterCombat());
            Assert.IsTrue(machine.TriggerQuiz());
            Assert.IsTrue(machine.ReturnToCombat());
            Assert.IsTrue(machine.SettleBattle(true));
            Assert.IsTrue(machine.LeaveBattle());

            CollectionAssert.AreEqual(
                new[]
                {
                    GameState.Combat, GameState.Quiz, GameState.Combat, GameState.Victory,
                    GameState.Encampment, GameState.BaseHub
                },
                entered);
        }

        [Test]
        public void FullBattle_Defeat_RaisesDefeatOverlay()
        {
            machine.EnterCombat();
            machine.TriggerQuiz();
            machine.ReturnToCombat();
            Assert.IsTrue(machine.SettleBattle(false));
            Assert.AreEqual(GameState.Defeat, machine.CurrentState);
            Assert.IsTrue(machine.LeaveBattle());
            Assert.AreEqual(GameState.BaseHub, machine.CurrentState);
        }

        [Test]
        public void EnterCombat_WhenAlreadyInCombat_IsANoOp()
        {
            machine.EnterCombat();
            entered.Clear();

            Assert.IsTrue(machine.EnterCombat());
            Assert.AreEqual(GameState.Combat, machine.CurrentState);
            Assert.IsEmpty(entered);
        }

        [Test]
        public void SettleBattle_FromDeployment_PassesThroughCombat()
        {
            Assert.IsTrue(machine.SettleBattle(true));
            CollectionAssert.AreEqual(new[] { GameState.Combat, GameState.Victory }, entered);
        }

        [Test]
        public void SettleBattle_FromQuiz_ReturnsToCombatFirst()
        {
            machine.EnterCombat();
            machine.TriggerQuiz();
            entered.Clear();

            Assert.IsTrue(machine.SettleBattle(false));
            CollectionAssert.AreEqual(new[] { GameState.Combat, GameState.Defeat }, entered);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void LeaveBattle_RetreatFromAnyBattleState_ReachesBaseHub(int depth)
        {
            if (depth >= 1)
            {
                machine.EnterCombat();
            }

            if (depth >= 2)
            {
                machine.TriggerQuiz();
            }

            Assert.IsTrue(machine.IsInBattle);
            Assert.IsTrue(machine.LeaveBattle());
            Assert.AreEqual(GameState.BaseHub, machine.CurrentState);
            Assert.IsFalse(machine.IsInBattle);
        }

        [Test]
        public void EveryEdgeTheLiveBattleUses_IsInTheTable()
        {
            Assert.IsTrue(GameStateMachine.IsTransitionLegal(GameState.Deployment, GameState.Combat));
            Assert.IsTrue(GameStateMachine.IsTransitionLegal(GameState.Combat, GameState.Quiz));
            Assert.IsTrue(GameStateMachine.IsTransitionLegal(GameState.Quiz, GameState.Combat));
            Assert.IsTrue(GameStateMachine.IsTransitionLegal(GameState.Combat, GameState.Victory));
            Assert.IsTrue(GameStateMachine.IsTransitionLegal(GameState.Combat, GameState.Defeat));
            Assert.IsTrue(GameStateMachine.IsTransitionLegal(GameState.Quiz, GameState.Encampment));
            Assert.IsFalse(GameStateMachine.IsTransitionLegal(GameState.Deployment, GameState.Quiz));
            Assert.IsFalse(GameStateMachine.IsTransitionLegal(GameState.Quiz, GameState.Victory));
        }
    }
}
