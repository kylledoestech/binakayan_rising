using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace BinakayanRising.Gameplay.Flow
{
    /// <summary>
    /// Thrown when code asks for a transition that <b>Figure 2</b> does not contain.
    /// </summary>
    /// <remarks>
    /// The state machine is a diagram in the capstone document, so an illegal transition is a
    /// specification violation, not a recoverable runtime condition. It is never swallowed.
    /// </remarks>
    public sealed class IllegalGameStateTransitionException : InvalidOperationException
    {
        /// <summary>Creates the exception.</summary>
        /// <param name="message">Description of the refused transition and the legal alternatives.</param>
        /// <param name="from">State the machine was in.</param>
        /// <param name="to">State that was asked for.</param>
        public IllegalGameStateTransitionException(string message, GameState from, GameState to)
            : base(message)
        {
            From = from;
            To = to;
        }

        /// <summary>State the machine was in when the transition was refused.</summary>
        public GameState From { get; }

        /// <summary>State that was asked for.</summary>
        public GameState To { get; }
    }

    /// <summary>
    /// One directed edge of the game state machine, with the document label that names it.
    /// </summary>
    public readonly struct GameStateTransition
    {
        /// <summary>Source state.</summary>
        public GameState From { get; }

        /// <summary>Destination state.</summary>
        public GameState To { get; }

        /// <summary>The arrow's label in the capstone document, or the reason for an inferred edge.</summary>
        public string Trigger { get; }

        /// <summary>
        /// True when this edge is drawn in Figure 2. False marks an edge the diagram omits but the
        /// game cannot run without — always a return path out of a leaf screen.
        /// </summary>
        public bool IsInFigure2 { get; }

        /// <summary>Creates a transition record.</summary>
        /// <param name="from">Source state.</param>
        /// <param name="to">Destination state.</param>
        /// <param name="trigger">The document's arrow label.</param>
        /// <param name="isInFigure2">Whether Figure 2 draws this edge.</param>
        public GameStateTransition(GameState from, GameState to, string trigger, bool isInFigure2)
        {
            From = from;
            To = to;
            Trigger = trigger;
            IsInFigure2 = isInFigure2;
        }

        /// <summary>Returns the edge in <c>From --Trigger--&gt; To</c> form.</summary>
        public override string ToString()
        {
            return From + " --" + Trigger + "--> " + To;
        }
    }

    /// <summary>
    /// The Game Manager of <b>Figure 2</b>: the single authority on which screen the game is in, and
    /// the only place a screen change may happen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The transition table in <see cref="Transitions"/> is the diagram, transcribed edge for edge.
    /// Anything not in it is refused loudly — <see cref="TryTransitionTo"/> logs an error naming the
    /// legal destinations and returns false, and <see cref="TransitionTo"/> throws
    /// <see cref="IllegalGameStateTransitionException"/>. Nothing silently ignores a bad request,
    /// because a state machine that silently ignores one is a state machine nobody can trust to
    /// match its own specification.
    /// </para>
    /// <para>
    /// <b>Composite states.</b> Figure 2 draws Encampment as a composite whose initial sub-state is
    /// BaseHub. Entering <see cref="GameState.Encampment"/> therefore immediately enters
    /// <see cref="GameState.BaseHub"/>, so <c>Encampment</c> is never the resting state — exactly as
    /// a UML composite behaves.
    /// </para>
    /// <para>
    /// <b>Six edges are inferred rather than drawn.</b> Figure 2 gives no way back out of
    /// ResourceManagement, HeroSummoning, RosterTraining or MissionPortal, which would strand the
    /// player in a leaf screen forever, and no way from the encampment back to the title. Those five
    /// returns plus <c>Quiz -&gt; Combat</c> (which the prose does specify: the quiz "returns to the
    /// combat state after an answer is submitted") are flagged <c>IsInFigure2 == false</c> so the
    /// divergence is auditable rather than hidden.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class GameStateMachine : MonoBehaviour
    {
        /// <summary>A <see cref="UnityEvent"/> carrying the state just entered, for Inspector wiring.</summary>
        [Serializable]
        public sealed class GameStateUnityEvent : UnityEvent<GameState>
        {
        }

        private static readonly GameStateTransition[] TransitionTable =
        {
            // MainMenuState --Load Save / New Game--> EncampmentState
            new GameStateTransition(GameState.MainMenu, GameState.Encampment, "Load Save / New Game", true),

            // EncampmentState composite: (start) --> BaseHub
            new GameStateTransition(GameState.Encampment, GameState.BaseHub, "Composite initial state", true),

            // BaseHub --Farm / Mine--> ResourceManagement
            new GameStateTransition(GameState.BaseHub, GameState.ResourceManagement, "Farm / Mine", true),

            // BaseHub --Gacha System--> HeroSummoning
            new GameStateTransition(GameState.BaseHub, GameState.HeroSummoning, "Gacha System", true),

            // BaseHub --Upgrade Units--> RosterTraining
            new GameStateTransition(GameState.BaseHub, GameState.RosterTraining, "Upgrade Units", true),

            // Inferred: Figure 2 draws no way out of the Encampment leaf screens.
            new GameStateTransition(GameState.ResourceManagement, GameState.BaseHub, "Back to Base Hub (inferred)", false),
            new GameStateTransition(GameState.HeroSummoning, GameState.BaseHub, "Back to Base Hub (inferred)", false),
            new GameStateTransition(GameState.RosterTraining, GameState.BaseHub, "Back to Base Hub (inferred)", false),

            // Inferred: the Armory's inventory, a further Encampment leaf the panel asked for.
            new GameStateTransition(GameState.BaseHub, GameState.Inventory, "Armory / Inventory (inferred)", false),
            new GameStateTransition(GameState.Inventory, GameState.BaseHub, "Back to Base Hub (inferred)", false),

            // Inferred: saving and returning to the title screen from the encampment.
            new GameStateTransition(GameState.BaseHub, GameState.MainMenu, "Save and quit to title (inferred)", false),

            // EncampmentState --Select Stage--> MissionPortal. Taken from the resting sub-state.
            new GameStateTransition(GameState.BaseHub, GameState.MissionPortal, "Select Stage", true),

            // Inferred: backing out of stage select.
            new GameStateTransition(GameState.MissionPortal, GameState.BaseHub, "Back to Base Hub (inferred)", false),

            // MissionPortal --Load Isometric Map--> DeploymentState
            new GameStateTransition(GameState.MissionPortal, GameState.Deployment, "Load Isometric Map", true),

            // DeploymentState --Lock Formation & Start--> CombatState
            new GameStateTransition(GameState.Deployment, GameState.Combat, "Lock Formation & Start", true),

            // CombatState/DeploymentState --Defeat (Restart)--> EncampmentState
            new GameStateTransition(GameState.Deployment, GameState.Encampment, "Defeat (Restart)", true),
            new GameStateTransition(GameState.Combat, GameState.Encampment, "Defeat (Restart)", true),

            // CombatState --Mid-Combat or End-Stage Trigger--> QuizState
            new GameStateTransition(GameState.Combat, GameState.Quiz, "Mid-Combat or End-Stage Trigger", true),

            // QuizState --Reward Virtual Currency--> EncampmentState
            new GameStateTransition(GameState.Quiz, GameState.Encampment, "Reward Virtual Currency", true),

            // Prose, not the diagram: the quiz "returns to the combat state after an answer is
            // submitted and rewards are calculated".
            new GameStateTransition(GameState.Quiz, GameState.Combat, "Answer submitted, rewards calculated", false),

            // Victory/Defeat overlays, raised by the end-of-turn HP evaluation.
            new GameStateTransition(GameState.Combat, GameState.Victory, "Remaining HP check: stage clear", true),
            new GameStateTransition(GameState.Combat, GameState.Defeat, "Remaining HP check: roster defeated", true),
            new GameStateTransition(GameState.Victory, GameState.Encampment, "Continue", true),
            new GameStateTransition(GameState.Defeat, GameState.Encampment, "Defeat (Restart)", true)
        };

        [Header("Boot")]
        [Tooltip("Enter MainMenu automatically on Start, i.e. the diagram's 'Launch Application' arrow.")]
        [SerializeField] private bool launchOnStart = true;

        [Tooltip("Entering the Encampment composite immediately enters its initial sub-state, BaseHub. " +
                 "Clear this only to step through the composite entry manually in a test.")]
        [SerializeField] private bool autoEnterCompositeInitialState = true;

        [Header("Diagnostics")]
        [Tooltip("Log every accepted transition. Useful while wiring the scene graph.")]
        [SerializeField] private bool logTransitions = false;

        [Header("Events")]
        [Tooltip("Fired after every accepted state change, carrying the state just entered.")]
        [SerializeField] private GameStateUnityEvent onStateChanged = new GameStateUnityEvent();

        private GameState currentState = GameState.MainMenu;
        private bool hasLaunched;

        /// <summary>
        /// Fired after every accepted state change. The first argument is the state left, the second
        /// the state entered. On "Launch Application" both are <see cref="GameState.MainMenu"/>.
        /// </summary>
        public event Action<GameState, GameState> StateChanged;

        /// <summary>Every legal edge of the machine, in diagram order.</summary>
        public static IReadOnlyList<GameStateTransition> Transitions
        {
            get { return TransitionTable; }
        }

        /// <summary>The state the game is in right now.</summary>
        public GameState CurrentState
        {
            get { return currentState; }
        }

        /// <summary>False until "Launch Application" has run; no transition is legal before that.</summary>
        public bool HasLaunched
        {
            get { return hasLaunched; }
        }

        /// <summary>True when the game is inside the Encampment composite state.</summary>
        public bool IsInEncampment
        {
            get { return IsEncampmentSubState(currentState) || currentState == GameState.Encampment; }
        }

        /// <summary>True when the transition exists in the machine's table.</summary>
        /// <param name="from">Source state.</param>
        /// <param name="to">Destination state.</param>
        public static bool IsTransitionLegal(GameState from, GameState to)
        {
            for (int i = 0; i < TransitionTable.Length; i++)
            {
                if (TransitionTable[i].From == from && TransitionTable[i].To == to)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Every state reachable in one step from a state, in diagram order.</summary>
        /// <param name="from">Source state.</param>
        public static GameState[] GetLegalTargets(GameState from)
        {
            List<GameState> targets = new List<GameState>();

            for (int i = 0; i < TransitionTable.Length; i++)
            {
                if (TransitionTable[i].From == from)
                {
                    targets.Add(TransitionTable[i].To);
                }
            }

            return targets.ToArray();
        }

        /// <summary>
        /// Finds the document's label for an edge.
        /// </summary>
        /// <param name="from">Source state.</param>
        /// <param name="to">Destination state.</param>
        /// <returns>The trigger label, or an empty string when the edge does not exist.</returns>
        public static string GetTrigger(GameState from, GameState to)
        {
            for (int i = 0; i < TransitionTable.Length; i++)
            {
                if (TransitionTable[i].From == from && TransitionTable[i].To == to)
                {
                    return TransitionTable[i].Trigger;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// The diagram's <c>(start) --Launch Application--&gt; MainMenuState</c> arrow. Legal exactly
        /// once, before any other transition.
        /// </summary>
        /// <returns>False when the machine has already launched.</returns>
        public bool LaunchApplication()
        {
            if (hasLaunched)
            {
                Debug.LogError(
                    "[GameStateMachine] 'Launch Application' fired twice. The start arrow of Figure 2 " +
                    "is taken once, at boot; the machine is already in " + currentState + ".",
                    this);
                return false;
            }

            hasLaunched = true;
            currentState = GameState.MainMenu;
            Announce(GameState.MainMenu, GameState.MainMenu);
            return true;
        }

        /// <summary>
        /// Moves to a new state, or refuses loudly. This is the checked form: it logs an error
        /// naming every legal destination and changes nothing.
        /// </summary>
        /// <param name="next">State to enter.</param>
        /// <returns>True when the transition was legal and taken.</returns>
        public bool TryTransitionTo(GameState next)
        {
            string reason;

            if (!CanTransitionTo(next, out reason))
            {
                Debug.LogError(reason, this);
                return false;
            }

            Commit(next);
            return true;
        }

        /// <summary>
        /// Moves to a new state, or throws. Use this where a refused transition means the code is
        /// wrong rather than the player did something unexpected.
        /// </summary>
        /// <param name="next">State to enter.</param>
        /// <exception cref="IllegalGameStateTransitionException">
        /// Thrown when Figure 2 has no edge from the current state to <paramref name="next"/>.
        /// </exception>
        public void TransitionTo(GameState next)
        {
            string reason;

            if (!CanTransitionTo(next, out reason))
            {
                throw new IllegalGameStateTransitionException(reason, currentState, next);
            }

            Commit(next);
        }

        /// <summary>
        /// Tests a transition without taking it, and explains a refusal.
        /// </summary>
        /// <param name="next">State to test.</param>
        /// <param name="reason">A full explanation when this returns false; empty otherwise.</param>
        public bool CanTransitionTo(GameState next, out string reason)
        {
            if (!hasLaunched)
            {
                reason = "[GameStateMachine] Refused " + currentState + " -> " + next +
                         ": the machine has not launched. Call LaunchApplication() first, which is " +
                         "Figure 2's '(start) --Launch Application--> MainMenuState' arrow.";
                return false;
            }

            if (IsTransitionLegal(currentState, next))
            {
                reason = string.Empty;
                return true;
            }

            reason = "[GameStateMachine] Illegal transition " + currentState + " -> " + next +
                     ". Figure 2 does not contain that edge. Legal from " + currentState + ": " +
                     DescribeLegalTargets(currentState) + ".";
            return false;
        }

        /// <summary>Figure 2: <c>MainMenuState --Load Save / New Game--&gt; EncampmentState</c>.</summary>
        public bool LoadSaveOrNewGame()
        {
            return TryTransitionTo(GameState.Encampment);
        }

        /// <summary>Figure 2: <c>BaseHub --Farm / Mine--&gt; ResourceManagement</c>.</summary>
        public bool OpenResourceManagement()
        {
            return TryTransitionTo(GameState.ResourceManagement);
        }

        /// <summary>Figure 2: <c>BaseHub --Gacha System--&gt; HeroSummoning</c>.</summary>
        public bool OpenHeroSummoning()
        {
            return TryTransitionTo(GameState.HeroSummoning);
        }

        /// <summary>Figure 2: <c>BaseHub --Upgrade Units--&gt; RosterTraining</c>.</summary>
        public bool OpenRosterTraining()
        {
            return TryTransitionTo(GameState.RosterTraining);
        }

        /// <summary>Inferred edge: opens the Armory's inventory from the hub.</summary>
        public bool OpenInventory()
        {
            return TryTransitionTo(GameState.Inventory);
        }

        /// <summary>Inferred edge: returns from an Encampment leaf screen or the Mission Portal to the hub.</summary>
        public bool ReturnToBaseHub()
        {
            return TryTransitionTo(GameState.BaseHub);
        }

        /// <summary>Figure 2: <c>EncampmentState --Select Stage--&gt; MissionPortal</c>.</summary>
        public bool SelectStage()
        {
            return TryTransitionTo(GameState.MissionPortal);
        }

        /// <summary>Figure 2: <c>MissionPortal --Load Isometric Map--&gt; DeploymentState</c>.</summary>
        public bool LoadIsometricMap()
        {
            return TryTransitionTo(GameState.Deployment);
        }

        /// <summary>Figure 2: <c>DeploymentState --Lock Formation &amp; Start--&gt; CombatState</c>.</summary>
        public bool LockFormationAndStart()
        {
            return TryTransitionTo(GameState.Combat);
        }

        /// <summary>Figure 2: <c>CombatState --Mid-Combat or End-Stage Trigger--&gt; QuizState</c>.</summary>
        public bool TriggerQuiz()
        {
            return TryTransitionTo(GameState.Quiz);
        }

        /// <summary>
        /// The document's prose: the quiz "returns to the combat state after an answer is submitted
        /// and rewards are calculated".
        /// </summary>
        public bool ReturnToCombat()
        {
            return TryTransitionTo(GameState.Combat);
        }

        /// <summary>Figure 2: <c>QuizState --Reward Virtual Currency--&gt; EncampmentState</c>.</summary>
        public bool RewardVirtualCurrencyAndReturn()
        {
            return TryTransitionTo(GameState.Encampment);
        }

        /// <summary>Raises the Victory overlay after the end-of-turn remaining-HP check.</summary>
        public bool RaiseVictory()
        {
            return TryTransitionTo(GameState.Victory);
        }

        /// <summary>Raises the Defeat overlay after the end-of-turn remaining-HP check.</summary>
        public bool RaiseDefeat()
        {
            return TryTransitionTo(GameState.Defeat);
        }

        /// <summary>
        /// Figure 2: <c>CombatState/DeploymentState --Defeat (Restart)--&gt; EncampmentState</c>, and
        /// the Continue button on either outcome overlay.
        /// </summary>
        public bool ReturnToEncampment()
        {
            return TryTransitionTo(GameState.Encampment);
        }

        // ------------------------------------------------------------------ live battle

        /// <summary>True in the three states a battle is fought in: Deployment, Combat and Quiz.</summary>
        public bool IsInBattle
        {
            get
            {
                return currentState == GameState.Deployment || currentState == GameState.Combat
                    || currentState == GameState.Quiz;
            }
        }

        /// <summary>
        /// The replay has started: "Lock Formation &amp; Start" from Deployment. Already in Combat
        /// (a battle redeployed and fought again) it is a no-op, since the state already holds.
        /// </summary>
        /// <returns>True when the machine is in Combat afterwards.</returns>
        public bool EnterCombat()
        {
            if (currentState == GameState.Combat)
            {
                return true;
            }

            return LockFormationAndStart();
        }

        /// <summary>
        /// The battle has resolved: walks from whichever battle state the machine is in (Deployment
        /// if the replay start was never reported, Quiz if the battle ended under a question) through
        /// Combat to the Victory or Defeat overlay, using only Figure 2's edges.
        /// </summary>
        /// <param name="won">True for Victory, false for Defeat.</param>
        /// <returns>True when the machine is on the outcome overlay afterwards.</returns>
        public bool SettleBattle(bool won)
        {
            GameState outcome = won ? GameState.Victory : GameState.Defeat;
            if (currentState == outcome)
            {
                return true;
            }

            if (currentState == GameState.Deployment && !LockFormationAndStart())
            {
                return false;
            }

            if (currentState == GameState.Quiz && !ReturnToCombat())
            {
                return false;
            }

            return won ? RaiseVictory() : RaiseDefeat();
        }

        /// <summary>
        /// Leaves a battle for the camp from any battle state or outcome overlay: a retreat from
        /// Deployment, Combat or Quiz, or Continue on Victory or Defeat. Every one of those states
        /// has a direct edge to Encampment.
        /// </summary>
        /// <returns>True when the machine is back in the encampment afterwards.</returns>
        public bool LeaveBattle()
        {
            if (IsInEncampment)
            {
                return true;
            }

            return ReturnToEncampment();
        }

        /// <summary>True when the state is one of the Encampment composite's sub-states.</summary>
        /// <param name="state">State to test.</param>
        public static bool IsEncampmentSubState(GameState state)
        {
            return state == GameState.BaseHub
                || state == GameState.ResourceManagement
                || state == GameState.HeroSummoning
                || state == GameState.RosterTraining
                || state == GameState.Inventory;
        }

        /// <summary>
        /// Renders the whole transition table as text, one edge per line, marking inferred edges.
        /// Useful for proving in a build log that the implementation matches Figure 2.
        /// </summary>
        public static string DescribeTable()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("(start) --Launch Application--> MainMenu   [Figure 2]");

            for (int i = 0; i < TransitionTable.Length; i++)
            {
                GameStateTransition transition = TransitionTable[i];
                builder.Append(transition.ToString());
                builder.AppendLine(transition.IsInFigure2 ? "   [Figure 2]" : "   [inferred]");
            }

            return builder.ToString();
        }

        private void Start()
        {
            if (launchOnStart && !hasLaunched)
            {
                LaunchApplication();
            }
        }

        private void Commit(GameState next)
        {
            GameState previous = currentState;
            currentState = next;
            Announce(previous, next);

            if (autoEnterCompositeInitialState && next == GameState.Encampment)
            {
                // Figure 2's Encampment is composite with BaseHub as its initial sub-state, so the
                // machine never rests on the composite itself.
                Commit(GameState.BaseHub);
            }
        }

        private void Announce(GameState previous, GameState next)
        {
            if (logTransitions)
            {
                Debug.Log("[GameStateMachine] " + previous + " -> " + next +
                          " (" + GetTrigger(previous, next) + ")", this);
            }

            Action<GameState, GameState> handler = StateChanged;

            if (handler != null)
            {
                handler(previous, next);
            }

            if (onStateChanged != null)
            {
                onStateChanged.Invoke(next);
            }
        }

        private static string DescribeLegalTargets(GameState from)
        {
            GameState[] targets = GetLegalTargets(from);

            if (targets.Length == 0)
            {
                return "nothing (this is a terminal state in Figure 2)";
            }

            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < targets.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(targets[i]);
                builder.Append(" via '");
                builder.Append(GetTrigger(from, targets[i]));
                builder.Append("'");
            }

            return builder.ToString();
        }
    }
}
