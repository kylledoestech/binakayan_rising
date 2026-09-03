using System;
using UnityEngine;
using UnityEngine.UI;
using BinakayanRising.Core.Combat;

namespace BinakayanRising.Gameplay.Flow
{
    /// <summary>
    /// The Victory and Defeat screen overlays, and the route back to the Encampment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The capstone document treats these as overlays rather than as boxes on Figure 2: "the system
    /// calculates remaining unit HP at the end of every AI turn to trigger the Victory/Defeat
    /// screen". <see cref="BinakayanRising.Core.Combat.BattleSimulator"/> already performs that
    /// evaluation and records it as the outcome of the final
    /// <see cref="BattleEventType.TurnEnded"/> event, so nothing is recomputed here — this class
    /// only displays what the simulator decided.
    /// </para>
    /// <para>
    /// Both overlays lead to the same place. Figure 2 gives Defeat the "Defeat (Restart)" arrow back
    /// to the Encampment; Victory returns there too, since the campaign is linear and the next
    /// sub-quest is chosen from the Mission Portal.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BattleOutcomeOverlay : MonoBehaviour
    {
        [Header("Flow")]
        [Tooltip("Game Manager driven when the player leaves the overlay.")]
        [SerializeField] private GameStateMachine stateMachine;

        [Tooltip("Combat phase whose input lock is released when the player leaves the overlay.")]
        [SerializeField] private CombatPhaseController combatPhase;

        [Header("Roots")]
        [Tooltip("Root object of the Victory overlay.")]
        [SerializeField] private GameObject victoryRoot;

        [Tooltip("Root object of the Defeat overlay.")]
        [SerializeField] private GameObject defeatRoot;

        [Header("Text")]
        [Tooltip("Headline text, shared by both overlays. Leave empty if each root has its own art.")]
        [SerializeField] private Text titleText;

        [Tooltip("Summary line: turns elapsed and survivors on each side.")]
        [SerializeField] private Text summaryText;

        [Tooltip("Headline shown on a win.")]
        [SerializeField] private string victoryTitle = "VICTORY";

        [Tooltip("Headline shown on a loss.")]
        [SerializeField] private string defeatTitle = "DEFEAT";

        [Header("Buttons")]
        [Tooltip("Returns to the Encampment. Shown on both overlays.")]
        [SerializeField] private Button continueButton;

        [Tooltip("Optional 'Retry' button on the Defeat overlay. Figure 2 routes a restart through " +
                 "the Encampment, so this returns there as well.")]
        [SerializeField] private Button retryButton;

        [Header("Behaviour")]
        [Tooltip("Return to the Encampment automatically after a delay instead of waiting for the button. " +
                 "TODO(design): the capstone document does not specify an auto-continue.")]
        [SerializeField] private bool autoContinue = false;

        [Tooltip("Unscaled seconds before auto-continue fires. TODO(design): not specified in capstone document.")]
        [Min(0f)]
        [SerializeField] private float autoContinueSeconds = 4f;

        private BattleResult shownResult;
        private bool isShowing;
        private bool wasVictory;
        private float autoContinueAtUnscaledTime;

        /// <summary>Raised when either overlay is shown, carrying the outcome it is showing.</summary>
        public event Action<BattleOutcome> OutcomeShown;

        /// <summary>
        /// Raised when the player leaves the overlay, just before the state machine is asked to move
        /// to the Encampment.
        /// </summary>
        public event Action ReturnToEncampmentRequested;

        /// <summary>True while either overlay is on screen.</summary>
        public bool IsShowing
        {
            get { return isShowing; }
        }

        /// <summary>The battle being summarised, or null when nothing is shown.</summary>
        public BattleResult ShownResult
        {
            get { return shownResult; }
        }

        /// <summary>Shows the Victory overlay.</summary>
        /// <param name="result">The finished battle, used for the summary line. May be null.</param>
        public void ShowVictory(BattleResult result)
        {
            Show(result, true);
        }

        /// <summary>Shows the Defeat overlay.</summary>
        /// <param name="result">The finished battle, used for the summary line. May be null.</param>
        public void ShowDefeat(BattleResult result)
        {
            Show(result, false);
        }

        /// <summary>
        /// Shows whichever overlay matches an outcome. A
        /// <see cref="BattleOutcome.Draw"/> shows the Defeat overlay, and
        /// <see cref="BattleOutcome.InProgress"/> shows nothing.
        /// </summary>
        /// <param name="result">The finished battle. Null is ignored.</param>
        public void ShowFor(BattleResult result)
        {
            if (result == null || result.Outcome == BattleOutcome.InProgress)
            {
                return;
            }

            Show(result, result.Outcome == BattleOutcome.Victory);
        }

        /// <summary>Hides both overlays.</summary>
        public void Hide()
        {
            isShowing = false;
            shownResult = null;

            if (victoryRoot != null)
            {
                victoryRoot.SetActive(false);
            }

            if (defeatRoot != null)
            {
                defeatRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Leaves the overlay for the Encampment: the far end of Figure 2's "Defeat (Restart)" arrow
        /// and of the Victory overlay's Continue button.
        /// </summary>
        public void ContinueToEncampment()
        {
            if (!isShowing)
            {
                return;
            }

            Action handler = ReturnToEncampmentRequested;

            if (handler != null)
            {
                handler();
            }

            Hide();

            if (combatPhase != null)
            {
                // Unlocks player input and performs the state transition itself.
                combatPhase.ReturnToEncampment();
                return;
            }

            if (stateMachine != null)
            {
                stateMachine.TryTransitionTo(GameState.Encampment);
            }
        }

        private void Awake()
        {
            if (continueButton != null)
            {
                continueButton.onClick.AddListener(ContinueToEncampment);
            }

            if (retryButton != null)
            {
                retryButton.onClick.AddListener(ContinueToEncampment);
            }

            Hide();
        }

        private void OnDestroy()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(ContinueToEncampment);
            }

            if (retryButton != null)
            {
                retryButton.onClick.RemoveListener(ContinueToEncampment);
            }
        }

        private void Update()
        {
            if (!isShowing || !autoContinue)
            {
                return;
            }

            if (Time.unscaledTime >= autoContinueAtUnscaledTime)
            {
                ContinueToEncampment();
            }
        }

        private void Show(BattleResult result, bool victory)
        {
            shownResult = result;
            wasVictory = victory;
            isShowing = true;

            if (victoryRoot != null)
            {
                victoryRoot.SetActive(victory);
            }

            if (defeatRoot != null)
            {
                defeatRoot.SetActive(!victory);
            }

            if (titleText != null)
            {
                titleText.text = victory ? victoryTitle : defeatTitle;
            }

            if (summaryText != null)
            {
                summaryText.text = BuildSummary(result);
            }

            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(!victory);
            }

            autoContinueAtUnscaledTime = Time.unscaledTime + autoContinueSeconds;

            Action<BattleOutcome> handler = OutcomeShown;

            if (handler != null)
            {
                handler(result != null ? result.Outcome : (victory ? BattleOutcome.Victory : BattleOutcome.Defeat));
            }
        }

        private string BuildSummary(BattleResult result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            return "Turns: " + result.TurnsElapsed
                + "    Katipunan standing: " + result.KatipunanAlive
                + "    Spanish standing: " + result.SpanishAlive;
        }

        /// <summary>True when the overlay currently on screen is the Victory one.</summary>
        public bool WasVictory
        {
            get { return wasVictory; }
        }
    }
}
