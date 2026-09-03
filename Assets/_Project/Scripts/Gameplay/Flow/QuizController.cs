using System;
using UnityEngine;
using BinakayanRising.Data;

namespace BinakayanRising.Gameplay.Flow
{
    /// <summary>
    /// Everything one answered quiz question produced: what was asked, what was chosen, and what it
    /// paid out.
    /// </summary>
    public readonly struct QuizAnswerResult
    {
        /// <summary>The question that was asked, or null when the quiz was skipped.</summary>
        public QuizQuestionData Question { get; }

        /// <summary>Zero-based index of the chosen option: 0 = A, 1 = B, 2 = C, 3 = D. -1 when skipped.</summary>
        public int ChosenOptionIndex { get; }

        /// <summary>True when the chosen option was the correct one.</summary>
        public bool WasCorrect { get; }

        /// <summary>
        /// Net change to the player's Reales: the reward on a correct answer, or the negated
        /// penalty on a wrong one.
        /// </summary>
        public int RealesDelta { get; }

        /// <summary>The Tactician's Command effect granted, or <see cref="QuizRewardEffect.None"/>.</summary>
        public QuizRewardEffect Effect { get; }

        /// <summary>Magnitude of <see cref="Effect"/> as a fraction: 0.10 is the document's "+10%".</summary>
        public float EffectMagnitude { get; }

        /// <summary>How many AI turns <see cref="Effect"/> lasts, for timed effects.</summary>
        public int EffectDurationTurns { get; }

        /// <summary>Creates a result.</summary>
        /// <param name="question">The question that was asked.</param>
        /// <param name="chosenOptionIndex">Zero-based index of the chosen option, or -1.</param>
        /// <param name="wasCorrect">Whether the answer was correct.</param>
        /// <param name="realesDelta">Net Reales change.</param>
        /// <param name="effect">Gameplay effect granted.</param>
        /// <param name="effectMagnitude">Effect magnitude as a fraction.</param>
        /// <param name="effectDurationTurns">Effect duration in AI turns.</param>
        public QuizAnswerResult(
            QuizQuestionData question,
            int chosenOptionIndex,
            bool wasCorrect,
            int realesDelta,
            QuizRewardEffect effect,
            float effectMagnitude,
            int effectDurationTurns)
        {
            Question = question;
            ChosenOptionIndex = chosenOptionIndex;
            WasCorrect = wasCorrect;
            RealesDelta = realesDelta;
            Effect = effect;
            EffectMagnitude = effectMagnitude;
            EffectDurationTurns = effectDurationTurns;
        }
    }

    /// <summary>
    /// Receives what a quiz answer paid out. Implemented outside this folder by whatever owns the
    /// player's wallet and the live battlefield.
    /// </summary>
    /// <remarks>
    /// Kept as an interface so <see cref="QuizController"/> never reaches into a save file or a unit
    /// list. The economy layer implements <see cref="AwardReales"/>; the battlefield layer
    /// implements <see cref="ApplyRewardEffect"/>.
    /// </remarks>
    public interface IQuizRewardReceiver
    {
        /// <summary>
        /// Credits or debits the player's Reales balance. Capstone Table 4 awards +50 for every
        /// correct sample answer.
        /// </summary>
        /// <param name="amount">Signed amount. Negative for a wrong-answer penalty.</param>
        void AwardReales(int amount);

        /// <summary>
        /// Applies one Tactician's Command effect to the battle in progress.
        /// </summary>
        /// <param name="effect">Which of the four Table 4 effects to apply.</param>
        /// <param name="magnitude">Magnitude as a fraction, e.g. 0.10 for the document's "+10%".</param>
        /// <param name="durationTurns">Duration in AI turns, for timed effects such as the attack buff.</param>
        void ApplyRewardEffect(QuizRewardEffect effect, float magnitude, int durationTurns);
    }

    /// <summary>
    /// Runs the Quiz state: presents one historical trivia question with four options A-D, freezes
    /// combat while it is open, evaluates the answer, pays out the reward, and returns control to
    /// combat.
    /// </summary>
    /// <remarks>
    /// <para>
    /// From the capstone document: the Quiz State "freezes combat physics and timers", then "returns
    /// to the combat state after an answer is submitted and rewards are calculated". Capstone
    /// Table 4 pays <b>+50 Reales</b> for a correct answer plus exactly one gameplay effect —
    /// map-wide +10% HP heal, +10% attack buff for one turn, reset enemy AI positions, or revive one
    /// fallen unit — all four of which are members of <see cref="QuizRewardEffect"/>.
    /// </para>
    /// <para>
    /// <b>TODO(design): the wrong-answer penalty is not specified anywhere in the document.</b> It
    /// says what a correct answer earns and is silent on what a wrong one costs — whether it charges
    /// Reales, skips the bonus silently, or damages the roster.
    /// <see cref="wrongAnswerPenaltyReales"/> defaults to 0, which is a placeholder, not a ruling
    /// that wrong answers are free. Per-question overrides live on
    /// <see cref="QuizQuestionData.WrongAnswerPenaltyReales"/>.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class QuizController : MonoBehaviour
    {
        [Header("Content")]
        [Tooltip("Pool the pop-up draws from. Questions are not repeated within a session.")]
        [SerializeField] private QuizQuestionBank questionBank;

        [Tooltip("Campaign economy config. Supplies the default Reales reward when a question does not.")]
        [SerializeField] private EconomyConfig economyConfig;

        [Header("Scene References")]
        [Tooltip("The pop-up UI this controller drives.")]
        [SerializeField] private QuizPanelView panel;

        [Tooltip("MonoBehaviour implementing IBattleReplayControl, frozen while the pop-up is open. " +
                 "Normally injected by CombatPhaseController instead.")]
        [SerializeField] private MonoBehaviour battleReplaySource;

        [Tooltip("MonoBehaviour implementing IQuizRewardReceiver - the wallet and battlefield sink.")]
        [SerializeField] private MonoBehaviour rewardReceiverSource;

        [Header("Rewards")]
        [Tooltip("Reales paid when a question asset does not carry its own reward. " +
                 "Capstone Table 4 awards +50 in every sample row.")]
        [Min(0)]
        [SerializeField] private int defaultRewardReales = 50;

        // TODO(design): not specified in capstone document. The document states only what a correct
        // answer earns; the cost of a wrong one is undefined. 0 is a placeholder, not a decision.
        [Tooltip("Reales deducted for a wrong answer, when the question asset does not override it. " +
                 "TODO(design): the wrong-answer penalty is not specified in the capstone document.")]
        [Min(0)]
        [SerializeField] private int wrongAnswerPenaltyReales = 0;

        [Header("Freeze")]
        [Tooltip("Pause the battle replay while the pop-up is open. The document requires the quiz " +
                 "to freeze combat physics and timers.")]
        [SerializeField] private bool freezeReplayWhileOpen = true;

        [Tooltip("Also set Time.timeScale to zero while the pop-up is open, freezing every timer in " +
                 "the scene. The panel must then animate in unscaled time.")]
        [SerializeField] private bool freezeTimeScaleWhileOpen = true;

        [Header("Pacing")]
        [Tooltip("Unscaled seconds the feedback and reward text stay on screen before the pop-up " +
                 "closes. TODO(design): not specified in capstone document.")]
        [Min(0f)]
        [SerializeField] private float feedbackSeconds = 1.5f;

        private IBattleReplayControl replay;
        private IQuizRewardReceiver rewardReceiver;
        private QuizQuestionData activeQuestion;
        private QuizAnswerResult pendingResult;
        private float restoreTimeScale = 1f;
        private bool isOpen;
        private bool answered;
        private float closeAtUnscaledTime;

        /// <summary>Raised the moment a question is shown.</summary>
        public event Action<QuizQuestionData> QuizPresented;

        /// <summary>Raised when an answer is evaluated, before the pop-up closes.</summary>
        public event Action<QuizAnswerResult> QuizAnswered;

        /// <summary>
        /// Raised once the pop-up has closed and combat may resume. Carries the answer result, or a
        /// default value when the quiz was skipped because no question was available.
        /// </summary>
        public event Action<QuizAnswerResult> QuizClosed;

        /// <summary>True while the pop-up is on screen and combat is frozen.</summary>
        public bool IsOpen
        {
            get { return isOpen; }
        }

        /// <summary>The question currently on screen, or null.</summary>
        public QuizQuestionData ActiveQuestion
        {
            get { return activeQuestion; }
        }

        /// <summary>False when the bank is missing or every question has already been asked this session.</summary>
        public bool HasQuestionsRemaining
        {
            get { return questionBank != null && questionBank.RemainingCount > 0; }
        }

        /// <summary>Injects the replay component this controller freezes.</summary>
        /// <param name="value">The replayer. Null is ignored.</param>
        public void SetReplayControl(IBattleReplayControl value)
        {
            if (value != null)
            {
                replay = value;
            }
        }

        /// <summary>Injects the sink that receives Reales and gameplay effects.</summary>
        /// <param name="value">The receiver. Null is ignored.</param>
        public void SetRewardReceiver(IQuizRewardReceiver value)
        {
            if (value != null)
            {
                rewardReceiver = value;
            }
        }

        /// <summary>
        /// Draws an unasked question from the bank and shows it.
        /// </summary>
        /// <returns>False when no bank is assigned or every question has already been asked.</returns>
        public bool PresentNextQuestion()
        {
            if (questionBank == null)
            {
                Debug.LogWarning("[QuizController] No QuizQuestionBank assigned; the quiz was skipped.", this);
                return false;
            }

            QuizQuestionData drawn = questionBank.DrawUnaskedQuestion();

            if (drawn == null)
            {
                return false;
            }

            return Present(drawn);
        }

        /// <summary>
        /// Shows a specific question, freezing combat first.
        /// </summary>
        /// <param name="question">Question to ask. Must not be null.</param>
        /// <returns>False when the question is null or the pop-up is already open.</returns>
        public bool Present(QuizQuestionData question)
        {
            if (question == null || isOpen)
            {
                return false;
            }

            activeQuestion = question;
            isOpen = true;
            answered = false;

            ApplyFreeze(true);

            if (panel != null)
            {
                panel.Show(question);
                panel.SetOptionsInteractable(true);
            }

            Action<QuizQuestionData> handler = QuizPresented;

            if (handler != null)
            {
                handler(question);
            }

            return true;
        }

        /// <summary>
        /// Evaluates the player's choice, pays out the reward, and starts the close timer.
        /// </summary>
        /// <param name="optionIndex">Zero-based index of the chosen option: 0 = A, 1 = B, 2 = C, 3 = D.</param>
        public void SubmitAnswer(int optionIndex)
        {
            if (!isOpen || answered || activeQuestion == null)
            {
                return;
            }

            answered = true;

            bool correct = activeQuestion.IsCorrect(optionIndex);
            int delta = correct ? RewardFor(activeQuestion) : -PenaltyFor(activeQuestion);

            QuizRewardEffect effect = correct ? activeQuestion.RewardEffect : QuizRewardEffect.None;
            float magnitude = correct ? activeQuestion.EffectMagnitude : 0f;
            int duration = correct ? activeQuestion.EffectDurationTurns : 0;

            if (rewardReceiver != null)
            {
                if (delta != 0)
                {
                    rewardReceiver.AwardReales(delta);
                }

                if (effect != QuizRewardEffect.None)
                {
                    rewardReceiver.ApplyRewardEffect(effect, magnitude, duration);
                }
            }

            QuizAnswerResult result = new QuizAnswerResult(
                activeQuestion, optionIndex, correct, delta, effect, magnitude, duration);

            if (panel != null)
            {
                panel.SetOptionsInteractable(false);
                panel.ShowFeedback(correct, activeQuestion.CorrectOptionIndex, DescribeReward(result));
            }

            Action<QuizAnswerResult> answeredHandler = QuizAnswered;

            if (answeredHandler != null)
            {
                answeredHandler(result);
            }

            pendingResult = result;
            closeAtUnscaledTime = Time.unscaledTime + feedbackSeconds;
        }

        /// <summary>
        /// Closes the pop-up immediately, unfreezes combat, and returns control to the combat state.
        /// Safe to call when nothing is open.
        /// </summary>
        public void Close()
        {
            if (!isOpen)
            {
                return;
            }

            isOpen = false;
            activeQuestion = null;

            if (panel != null)
            {
                panel.Hide();
            }

            ApplyFreeze(false);

            QuizAnswerResult result = pendingResult;
            pendingResult = default(QuizAnswerResult);

            Action<QuizAnswerResult> handler = QuizClosed;

            if (handler != null)
            {
                handler(result);
            }
        }

        /// <summary>
        /// Renders one line of reward text for the panel, e.g. <c>"+50 Reales - Map-wide +10% HP heal"</c>.
        /// </summary>
        /// <param name="result">The evaluated answer.</param>
        public static string DescribeReward(QuizAnswerResult result)
        {
            if (result.RealesDelta == 0 && result.Effect == QuizRewardEffect.None)
            {
                return string.Empty;
            }

            string currency = result.RealesDelta > 0
                ? "+" + result.RealesDelta + " Reales"
                : (result.RealesDelta < 0 ? result.RealesDelta + " Reales" : string.Empty);

            string effect = DescribeEffect(result.Effect, result.EffectMagnitude, result.EffectDurationTurns);

            if (currency.Length == 0)
            {
                return effect;
            }

            return effect.Length == 0 ? currency : currency + "  -  " + effect;
        }

        /// <summary>Renders one Tactician's Command effect in the document's own wording.</summary>
        /// <param name="effect">Effect to describe.</param>
        /// <param name="magnitude">Magnitude as a fraction, e.g. 0.10.</param>
        /// <param name="durationTurns">Duration in AI turns.</param>
        public static string DescribeEffect(QuizRewardEffect effect, float magnitude, int durationTurns)
        {
            int percent = Mathf.RoundToInt(magnitude * 100f);

            switch (effect)
            {
                case QuizRewardEffect.MapWideHeal:
                    return "Map-wide +" + percent + "% HP heal";
                case QuizRewardEffect.AttackBuff:
                    return "+" + percent + "% Attack buff for " + durationTurns +
                           (durationTurns == 1 ? " turn" : " turns");
                case QuizRewardEffect.ResetEnemyPositions:
                    return "Resets AI enemy positions";
                case QuizRewardEffect.ReviveFallenUnit:
                    return "Instantly revives 1 fallen unit";
                default:
                    return string.Empty;
            }
        }

        private void Awake()
        {
            ResolveSources();
        }

        private void OnEnable()
        {
            if (panel != null)
            {
                panel.OptionChosen += SubmitAnswer;
            }
        }

        private void OnDisable()
        {
            if (panel != null)
            {
                panel.OptionChosen -= SubmitAnswer;
            }

            // Never leave the game frozen because this object was disabled mid-question.
            if (isOpen)
            {
                ApplyFreeze(false);
                isOpen = false;
                activeQuestion = null;
            }
        }

        private void Update()
        {
            if (isOpen && answered && Time.unscaledTime >= closeAtUnscaledTime)
            {
                Close();
            }
        }

        private void ResolveSources()
        {
            if (battleReplaySource != null)
            {
                replay = battleReplaySource as IBattleReplayControl;

                if (replay == null)
                {
                    Debug.LogError(
                        "[QuizController] '" + battleReplaySource.GetType().Name +
                        "' does not implement IBattleReplayControl.",
                        this);
                }
            }

            if (rewardReceiverSource != null)
            {
                rewardReceiver = rewardReceiverSource as IQuizRewardReceiver;

                if (rewardReceiver == null)
                {
                    Debug.LogError(
                        "[QuizController] '" + rewardReceiverSource.GetType().Name +
                        "' does not implement IQuizRewardReceiver.",
                        this);
                }
            }
        }

        private void ApplyFreeze(bool freeze)
        {
            if (freezeReplayWhileOpen && replay != null)
            {
                if (freeze)
                {
                    replay.Pause();
                }
                else
                {
                    replay.Resume();
                }
            }

            if (!freezeTimeScaleWhileOpen)
            {
                return;
            }

            if (freeze)
            {
                restoreTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = restoreTimeScale <= 0f ? 1f : restoreTimeScale;
            }
        }

        private int RewardFor(QuizQuestionData question)
        {
            if (question.RewardReales > 0)
            {
                return question.RewardReales;
            }

            if (economyConfig != null && economyConfig.QuizCorrectAnswerRewardReales > 0)
            {
                return economyConfig.QuizCorrectAnswerRewardReales;
            }

            return defaultRewardReales;
        }

        private int PenaltyFor(QuizQuestionData question)
        {
            // TODO(design): not specified in capstone document. Both values below are placeholders.
            return question.WrongAnswerPenaltyReales > 0
                ? question.WrongAnswerPenaltyReales
                : wrongAnswerPenaltyReales;
        }
    }
}
