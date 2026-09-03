using System;
using System.Collections.Generic;
using UnityEngine;
using BinakayanRising.Core.Combat;
using BinakayanRising.Data;
using BinakayanRising.Gameplay.Deployment;

namespace BinakayanRising.Gameplay.Flow
{
    /// <summary>
    /// Runs the Combat state end to end: locks all player input, resolves the battle through
    /// <see cref="IBattleLauncher"/>, hands the event log to <see cref="IBattleReplayControl"/>,
    /// decides after each replayed turn whether the pop-up quiz should fire, and raises Victory or
    /// Defeat when the battle resolves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Input locking.</b> The capstone document is unambiguous: once the player confirms the
    /// formation, "all player input is locked for the entire combat phase". This controller sets
    /// <see cref="DeploymentInputActions.SetInputLocked"/> when combat begins and clears it only
    /// when control leaves combat for the Encampment. The auto-battler takes no player input at all
    /// — the only interactive thing in this whole state is the quiz pop-up.
    /// </para>
    /// <para>
    /// <b>The battle is resolved before it is shown.</b> <see cref="BattleSimulator"/> is a
    /// deterministic headless resolver: one synchronous call produces the whole event log, and the
    /// replay is pure presentation. That is what makes a battle reproducible from a seed.
    /// </para>
    /// <para>
    /// <b>TODO(design): the quiz reward effects cannot alter an already-resolved battle.</b> The
    /// document's Tactician's Command effects — map-wide heal, attack buff, reset enemy positions,
    /// revive a fallen unit — are described as mid-combat rewards, but the combat they would modify
    /// has already been computed by the time the quiz appears. The team must choose one of:
    /// (a) re-simulate the remainder of the battle from the quiz turn with the effect applied, which
    /// costs a resume-from-state API on the simulator; (b) fire the quiz only at end of stage, where
    /// there is nothing left to modify; or (c) treat the effects as presentation-only flourishes.
    /// Nothing here picks one — <see cref="QuizController"/> reports the granted effect and this
    /// controller simply resumes the replay.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CombatPhaseController : MonoBehaviour
    {
        [Header("Flow")]
        [Tooltip("The Game Manager whose state this controller drives.")]
        [SerializeField] private GameStateMachine stateMachine;

        [Tooltip("Deployment phase this combat follows. Its FormationLocked event can start combat.")]
        [SerializeField] private DeploymentController deploymentController;

        [Tooltip("Start combat automatically when the deployment phase locks its formation.")]
        [SerializeField] private bool autoStartOnFormationLocked = true;

        [Header("Adapters")]
        [Tooltip("MonoBehaviour implementing IBattleLauncher - the Adapters/BattleSetup component.")]
        [SerializeField] private MonoBehaviour battleLauncherSource;

        [Tooltip("MonoBehaviour implementing IBattleReplayControl - the Presentation/BattleReplayer component.")]
        [SerializeField] private MonoBehaviour battleReplaySource;

        [Header("Player Input")]
        [Tooltip("Input wrapper locked for the entire combat phase, per the capstone document.")]
        [SerializeField] private DeploymentInputActions playerInput;

        [Header("Quiz")]
        [Tooltip("Presents the historical trivia pop-up. Leave empty to disable the quiz entirely.")]
        [SerializeField] private QuizController quizController;

        // TODO(design): not specified in capstone document. The document says the quiz fires on a
        // "Mid-Combat or End-Stage Trigger" but never says how often mid-combat, or on which turn.
        // 0 disables the mid-combat quiz rather than inventing a cadence.
        [Tooltip("Fire the quiz every N replayed AI turns. " +
                 "TODO(design): the quiz interval is not specified in the capstone document; 0 disables it.")]
        [Min(0)]
        [SerializeField] private int quizTriggerIntervalTurns = 0;

        [Tooltip("Also fire the quiz once when the battle resolves - the document's 'End-Stage Trigger'.")]
        [SerializeField] private bool quizOnStageEnd = true;

        [Header("Outcome")]
        [Tooltip("Shows the Victory and Defeat screen overlays.")]
        [SerializeField] private BattleOutcomeOverlay outcomeOverlay;

        [Tooltip("Treat a Draw - the turn cap expiring, or mutual annihilation - as a Defeat. " +
                 "TODO(design): the capstone document defines no draw outcome.")]
        [SerializeField] private bool treatDrawAsDefeat = true;

        [Header("Determinism")]
        [Tooltip("Draw a fresh seed for every battle instead of using the fixed seed below.")]
        [SerializeField] private bool useRandomSeed = true;

        [Tooltip("Fixed battle seed, used when the random seed is off. The same seed replays the same battle exactly.")]
        [SerializeField] private int fixedBattleSeed = 0;

        private IBattleLauncher launcher;
        private IBattleReplayControl replay;
        private MissionData activeMission;
        private BattleResult activeResult;
        private int lastQuizTurn;
        private bool endStageQuizShown;
        private bool combatRunning;
        private bool subscribedToReplay;

        /// <summary>Raised when combat begins, carrying the mission and the seed the battle ran with.</summary>
        public event Action<MissionData, int> CombatStarted;

        /// <summary>Raised once the battle has resolved and its outcome has been shown.</summary>
        public event Action<BattleResult> CombatResolved;

        /// <summary>The finished battle currently being replayed, or null outside combat.</summary>
        public BattleResult ActiveResult
        {
            get { return activeResult; }
        }

        /// <summary>The mission currently being fought, or null outside combat.</summary>
        public MissionData ActiveMission
        {
            get { return activeMission; }
        }

        /// <summary>True between <see cref="BeginCombat"/> and the outcome being raised.</summary>
        public bool IsCombatRunning
        {
            get { return combatRunning; }
        }

        /// <summary>The seed the next battle will run with.</summary>
        public int NextSeed
        {
            get { return useRandomSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : fixedBattleSeed; }
        }

        /// <summary>Injects the battle adapter in code, for tests or for a non-MonoBehaviour adapter.</summary>
        /// <param name="value">The adapter. Null is ignored.</param>
        public void SetBattleLauncher(IBattleLauncher value)
        {
            if (value != null)
            {
                launcher = value;
            }
        }

        /// <summary>Injects the replay component in code, for tests or for a non-MonoBehaviour replayer.</summary>
        /// <param name="value">The replayer. Null is ignored.</param>
        public void SetBattleReplayControl(IBattleReplayControl value)
        {
            if (value == null)
            {
                return;
            }

            UnsubscribeFromReplay();
            replay = value;
            SubscribeToReplay();

            if (quizController != null)
            {
                quizController.SetReplayControl(replay);
            }
        }

        /// <summary>
        /// The document's "Lock Formation &amp; Start": locks player input, resolves the battle, and
        /// starts the replay.
        /// </summary>
        /// <param name="mission">The mission being fought. Must not be null.</param>
        /// <param name="placements">
        /// The locked formation. Must contain at least one unit — the deployment phase enforces that
        /// before it will lock.
        /// </param>
        /// <returns>False when a prerequisite is missing; combat is not started then.</returns>
        public bool BeginCombat(MissionData mission, IReadOnlyList<UnitPlacement> placements)
        {
            if (combatRunning)
            {
                Debug.LogError("[CombatPhaseController] Combat is already running.", this);
                return false;
            }

            if (mission == null)
            {
                Debug.LogError("[CombatPhaseController] Cannot begin combat without a mission.", this);
                return false;
            }

            if (placements == null || placements.Count == 0)
            {
                Debug.LogError(
                    "[CombatPhaseController] Cannot begin combat with an empty formation. The " +
                    "deployment phase must place at least one unit before locking.",
                    this);
                return false;
            }

            if (launcher == null)
            {
                Debug.LogError(
                    "[CombatPhaseController] No IBattleLauncher. Assign the Adapters/BattleSetup " +
                    "component to 'Battle Launcher Source', or call SetBattleLauncher().",
                    this);
                return false;
            }

            // The document: all player input is locked for the entire combat phase.
            SetPlayerInputLocked(true);

            activeMission = mission;
            lastQuizTurn = 0;
            endStageQuizShown = false;
            combatRunning = true;

            int seed = NextSeed;

            // A copy is taken because the deployment controller reuses its placement buffer.
            UnitPlacement[] snapshot = new UnitPlacement[placements.Count];

            for (int i = 0; i < placements.Count; i++)
            {
                snapshot[i] = placements[i];
            }

            activeResult = launcher.Launch(mission, snapshot, seed);

            if (activeResult == null)
            {
                Debug.LogError("[CombatPhaseController] The battle launcher returned no result.", this);
                combatRunning = false;
                SetPlayerInputLocked(false);
                return false;
            }

            Action<MissionData, int> started = CombatStarted;

            if (started != null)
            {
                started(mission, seed);
            }

            if (replay != null)
            {
                replay.StartReplay(activeResult);
            }
            else
            {
                Debug.LogWarning(
                    "[CombatPhaseController] No IBattleReplayControl assigned; the battle was " +
                    "resolved but nothing will be animated. Resolving the outcome immediately.",
                    this);
                OnReplayFinished(activeResult);
            }

            return true;
        }

        /// <summary>
        /// Skips the remaining animation. The outcome is unchanged: the battle was already resolved
        /// before the first frame of the replay.
        /// </summary>
        public void SkipReplay()
        {
            if (combatRunning && replay != null)
            {
                replay.SkipToEnd();
            }
        }

        /// <summary>
        /// Leaves combat for the Encampment and unlocks player input. This is the far end of both
        /// Figure 2 "Defeat (Restart)" arrows and of the outcome overlay's Continue button.
        /// </summary>
        public void ReturnToEncampment()
        {
            combatRunning = false;
            SetPlayerInputLocked(false);

            if (outcomeOverlay != null)
            {
                outcomeOverlay.Hide();
            }

            if (stateMachine != null)
            {
                stateMachine.TryTransitionTo(GameState.Encampment);
            }
        }

        private void Awake()
        {
            ResolveAdapters();
        }

        private void OnEnable()
        {
            SubscribeToReplay();

            if (deploymentController != null)
            {
                deploymentController.FormationLocked += OnFormationLocked;
            }

            if (quizController != null)
            {
                quizController.QuizClosed += OnQuizClosed;
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromReplay();

            if (deploymentController != null)
            {
                deploymentController.FormationLocked -= OnFormationLocked;
            }

            if (quizController != null)
            {
                quizController.QuizClosed -= OnQuizClosed;
            }
        }

        private void ResolveAdapters()
        {
            if (battleLauncherSource != null)
            {
                launcher = battleLauncherSource as IBattleLauncher;

                if (launcher == null)
                {
                    Debug.LogError(
                        "[CombatPhaseController] '" + battleLauncherSource.GetType().Name +
                        "' does not implement IBattleLauncher.",
                        this);
                }
            }

            if (battleReplaySource != null)
            {
                replay = battleReplaySource as IBattleReplayControl;

                if (replay == null)
                {
                    Debug.LogError(
                        "[CombatPhaseController] '" + battleReplaySource.GetType().Name +
                        "' does not implement IBattleReplayControl.",
                        this);
                }
            }

            if (quizController != null && replay != null)
            {
                quizController.SetReplayControl(replay);
            }
        }

        private void SubscribeToReplay()
        {
            if (replay == null || subscribedToReplay)
            {
                return;
            }

            replay.TurnEnded += OnTurnCompleted;
            replay.BattleFinished += OnReplayFinished;
            subscribedToReplay = true;
        }

        private void UnsubscribeFromReplay()
        {
            if (replay == null || !subscribedToReplay)
            {
                return;
            }

            replay.TurnEnded -= OnTurnCompleted;
            replay.BattleFinished -= OnReplayFinished;
            subscribedToReplay = false;
        }

        private void OnFormationLocked(IReadOnlyList<UnitPlacement> placements)
        {
            if (!autoStartOnFormationLocked)
            {
                return;
            }

            if (stateMachine != null && stateMachine.CurrentState == GameState.Deployment)
            {
                stateMachine.TryTransitionTo(GameState.Combat);
            }

            MissionData mission = deploymentController != null ? deploymentController.Mission : activeMission;
            BeginCombat(mission, placements);
        }

        /// <summary>
        /// The document's per-turn hook: "the system calculates remaining unit HP at the end of every
        /// AI turn". The outcome check itself happened inside the simulator; what is decided here is
        /// only whether the mid-combat quiz interrupts.
        /// </summary>
        private void OnTurnCompleted(int turnNumber, BattleOutcome outcomeAtTurnEnd)
        {
            if (!combatRunning || outcomeAtTurnEnd != BattleOutcome.InProgress)
            {
                return;
            }

            if (!ShouldTriggerQuiz(turnNumber))
            {
                return;
            }

            lastQuizTurn = turnNumber;
            PresentQuiz();
        }

        private bool ShouldTriggerQuiz(int turnNumber)
        {
            if (quizController == null || quizTriggerIntervalTurns <= 0)
            {
                return false;
            }

            return turnNumber - lastQuizTurn >= quizTriggerIntervalTurns;
        }

        private void PresentQuiz()
        {
            if (stateMachine != null && stateMachine.CurrentState == GameState.Combat)
            {
                stateMachine.TryTransitionTo(GameState.Quiz);
            }

            // The quiz controller performs the freeze itself, so the replay is already stopped by
            // the time the first option button is interactable.
            if (!quizController.PresentNextQuestion())
            {
                OnQuizClosed(default(QuizAnswerResult));
            }
        }

        private void OnQuizClosed(QuizAnswerResult result)
        {
            if (stateMachine != null && stateMachine.CurrentState == GameState.Quiz)
            {
                stateMachine.TryTransitionTo(GameState.Combat);
            }

            if (endStageQuizShown && activeResult != null)
            {
                ResolveOutcome(activeResult);
            }
        }

        private void OnReplayFinished(BattleResult result)
        {
            if (!combatRunning)
            {
                return;
            }

            activeResult = result;

            // "Mid-Combat or End-Stage Trigger": the end-stage half fires once, before the overlay,
            // so the player answers while the battlefield is still on screen.
            if (quizOnStageEnd && !endStageQuizShown && quizController != null && quizController.HasQuestionsRemaining)
            {
                endStageQuizShown = true;
                PresentQuiz();
                return;
            }

            ResolveOutcome(result);
        }

        private void ResolveOutcome(BattleResult result)
        {
            if (!combatRunning)
            {
                return;
            }

            combatRunning = false;

            BattleOutcome outcome = result != null ? result.Outcome : BattleOutcome.Draw;
            bool victory = outcome == BattleOutcome.Victory;

            if (outcome == BattleOutcome.Draw && !treatDrawAsDefeat)
            {
                victory = true;
            }

            if (stateMachine != null && stateMachine.CurrentState == GameState.Combat)
            {
                if (victory)
                {
                    stateMachine.TryTransitionTo(GameState.Victory);
                }
                else
                {
                    stateMachine.TryTransitionTo(GameState.Defeat);
                }
            }

            if (outcomeOverlay != null)
            {
                if (victory)
                {
                    outcomeOverlay.ShowVictory(result);
                }
                else
                {
                    outcomeOverlay.ShowDefeat(result);
                }
            }

            Action<BattleResult> handler = CombatResolved;

            if (handler != null)
            {
                handler(result);
            }
        }

        private void SetPlayerInputLocked(bool locked)
        {
            if (playerInput != null)
            {
                playerInput.SetInputLocked(locked);
            }
        }
    }
}
