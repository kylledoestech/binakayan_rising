using System.Collections.Generic;
using BinakayanRising.Gameplay.Flow;
using UnityEngine;

namespace BinakayanRising.UI.Kit
{
    /// <summary>
    /// Shows exactly one <see cref="GameScreen"/> at a time, following
    /// <see cref="GameStateMachine"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The state machine already implements every transition in the capstone's Figure 2 diagram and
    /// raises <see cref="GameStateMachine.StateChanged"/> on each one. The router does nothing but
    /// listen: it never decides what may follow what, so there is no second copy of the transition
    /// table to fall out of step with the first.
    /// </para>
    /// <para>
    /// Composite states are transient. Entering <see cref="GameState.Encampment"/> immediately
    /// enters <see cref="GameState.BaseHub"/>, so no screen registers for the composite itself and
    /// the router simply has nothing to show for the microsecond it is current.
    /// </para>
    /// </remarks>
    public sealed class ScreenRouter : MonoBehaviour
    {
        private readonly Dictionary<GameState, GameScreen> screens = new Dictionary<GameState, GameScreen>();

        private GameStateMachine machine;
        private GameScreen current;

        /// <summary>The screen currently on display, or null between states.</summary>
        public GameScreen Current => current;

        /// <summary>Binds the router to a state machine and starts following it.</summary>
        public void Bind(GameStateMachine stateMachine)
        {
            if (machine != null)
            {
                machine.StateChanged -= OnStateChanged;
            }

            machine = stateMachine;

            if (machine != null)
            {
                machine.StateChanged += OnStateChanged;

                foreach (GameScreen screen in screens.Values)
                {
                    screen.Machine = machine;
                }

                // The machine may already have launched before the router was bound, so adopt
                // its current state rather than waiting for the next transition that may not come.
                if (machine.HasLaunched)
                {
                    ShowFor(machine.CurrentState, immediate: true);
                }
            }
        }

        /// <summary>Registers a screen against the state it represents.</summary>
        public void Register(GameScreen screen)
        {
            if (screen == null)
            {
                return;
            }

            screens[screen.State] = screen;
            screen.Machine = machine;

            // Registered screens start hidden. Building them all visible and then hiding them
            // would flash every screen in the game across the first frame.
            screen.gameObject.SetActive(false);
        }

        /// <summary>Looks up a registered screen, typed.</summary>
        public T Get<T>(GameState state) where T : GameScreen
        {
            return screens.TryGetValue(state, out GameScreen screen) ? screen as T : null;
        }

        private void OnStateChanged(GameState from, GameState to)
        {
            ShowFor(to, immediate: false);
        }

        private void ShowFor(GameState state, bool immediate)
        {
            if (!screens.TryGetValue(state, out GameScreen next))
            {
                // Composite states and the in-battle states that are drawn by the board rather
                // than by a screen legitimately have no entry here.
                return;
            }

            if (next == current)
            {
                next.Show();
                return;
            }

            if (current != null)
            {
                current.Hide(immediate);
            }

            current = next;
            current.Show();
        }

        private void OnDestroy()
        {
            if (machine != null)
            {
                machine.StateChanged -= OnStateChanged;
                machine = null;
            }
        }
    }
}
