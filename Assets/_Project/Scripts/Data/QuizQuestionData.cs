using UnityEngine;

namespace BinakayanRising.Data
{
    /// <summary>
    /// One historical trivia question for the mid-battle pop-up quiz: the prompt, exactly four
    /// options A-D, which of them is correct, and what answering correctly awards.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Schema transcribed from <b>Capstone Table 4: Sample Historical Trivia Database (Educational
    /// Module)</b>, whose columns are Historical Question, Multiple Choice Options, Correct Answer
    /// and In-Game Reward. All four sample rows offer four options labelled A-D and award
    /// <c>+50 Reales</c> plus one gameplay effect, so <see cref="RewardReales"/> defaults to 50 and
    /// the effect is chosen per asset from <see cref="QuizRewardEffect"/>.
    /// </para>
    /// <para>
    /// The sample rows, for reference when authoring assets:
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     "Who was the primary engineer responsible for designing the trench networks in Cavite?"
    ///     — A. Andres Bonifacio / B. Emilio Aguinaldo / C. Edilberto Evangelista / D. Antonio Luna
    ///     — correct: C — reward: +50 Reales, Map-wide +10% HP Heal.
    ///   </description></item>
    ///   <item><description>
    ///     "When did the simultaneous Battle of Binakayan-Dalahican take place?"
    ///     — A. August 1896 / B. November 9-11, 1896 / C. June 12, 1898 / D. December 30, 1896
    ///     — correct: B — reward: +50 Reales, +10% Attack Buff for 1 turn.
    ///   </description></item>
    ///   <item><description>
    ///     "Who was the Spanish Governor-General that launched the attack on Cavite during this battle?"
    ///     — A. Ramón Blanco / B. Camilo de Polavieja / C. Miguel López de Legazpi / D. Fernando Primo de Rivera
    ///     — correct: A — reward: +50 Reales, Resets AI Enemy positions.
    ///   </description></item>
    ///   <item><description>
    ///     "The victory at Binakayan-Dalahican is historically recognized as the first major Filipino
    ///     victory in which province?"
    ///     — A. Manila / B. Bulacan / C. Laguna / D. Cavite
    ///     — correct: D — reward: +50 Reales, Instantly revives 1 fallen unit.
    ///   </description></item>
    /// </list>
    /// <para>
    /// TODO(design): the sample rewards quantify two of the effects — "+10% HP Heal" and
    /// "+10% Attack Buff for 1 turn" — but those magnitudes vary per row rather than being global
    /// constants, so <see cref="EffectMagnitude"/> and <see cref="EffectDurationTurns"/> ship at 0
    /// and are authored per question. Enter 0.10 and 1 respectively to reproduce the sample rows.
    /// </para>
    /// <para>
    /// TODO(design): the penalty for a wrong answer is not specified anywhere in the document. It
    /// states only that a correct answer rewards Reales and a Tactician's Command effect; it never
    /// says whether a wrong answer costs currency, skips the bonus silently, or damages the roster.
    /// <see cref="WrongAnswerPenaltyReales"/> exists so the decision has a home, and its 0 default
    /// is a placeholder rather than a ruling that wrong answers are free.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "NewQuizQuestion", menuName = "Binakayan Rising/Quiz Question", order = 3)]
    public sealed class QuizQuestionData : ScriptableObject
    {
        /// <summary>
        /// Number of multiple-choice options every question carries. Capstone Table 4 lists exactly
        /// four, labelled A through D, in every sample row.
        /// </summary>
        public const int OptionCount = 4;

        [Header("Capstone Table 4: Sample Historical Trivia Database")]
        [Tooltip("The historical question shown in the pop-up.")]
        [TextArea(2, 6)]
        [SerializeField] private string questionText = string.Empty;

        [Tooltip("Exactly four options, in display order A, B, C, D.")]
        [SerializeField] private string[] options = new string[OptionCount];

        [Tooltip("Index of the correct option: 0 = A, 1 = B, 2 = C, 3 = D.")]
        [Range(0, OptionCount - 1)]
        [SerializeField] private int correctOptionIndex = 0;

        [Header("Reward")]
        [Tooltip("Reales awarded for a correct answer. Capstone Table 4 awards +50 in every sample row.")]
        [Min(0)]
        [SerializeField] private int rewardReales = 50;

        [Tooltip("Tactician's Command effect applied on a correct answer.")]
        [SerializeField] private QuizRewardEffect rewardEffect = QuizRewardEffect.None;

        [Tooltip("Magnitude of the reward effect as a fraction (0.10 == 10%). " +
                 "TODO(design): varies per row; Table 4 samples use 0.10 for the heal and the attack buff.")]
        [SerializeField] private float effectMagnitude = 0f;

        [Tooltip("How many turns the reward effect lasts, for timed effects. " +
                 "TODO(design): varies per row; Table 4's attack buff sample lasts 1 turn.")]
        [Min(0)]
        [SerializeField] private int effectDurationTurns = 0;

        [Tooltip("Reales deducted for a wrong answer. " +
                 "TODO(design): the wrong-answer penalty is not specified in the capstone document.")]
        [Min(0)]
        [SerializeField] private int wrongAnswerPenaltyReales = 0;

        /// <summary>The historical question shown in the pop-up.</summary>
        public string QuestionText => questionText;

        /// <summary>
        /// The four multiple-choice options in display order A, B, C, D. Never <c>null</c>; the
        /// array is normalised to <see cref="OptionCount"/> entries.
        /// </summary>
        public string[] Options => options;

        /// <summary>Index of the correct option: 0 = A, 1 = B, 2 = C, 3 = D.</summary>
        public int CorrectOptionIndex => correctOptionIndex;

        /// <summary>Reales awarded for a correct answer. Capstone Table 4: +50 in every sample row.</summary>
        public int RewardReales => rewardReales;

        /// <summary>Tactician's Command effect applied on a correct answer.</summary>
        public QuizRewardEffect RewardEffect => rewardEffect;

        /// <summary>
        /// Magnitude of <see cref="RewardEffect"/> as a fraction (0.10 == 10%).
        /// TODO(design): authored per question; Table 4 samples imply 0.10 where quantified.
        /// </summary>
        public float EffectMagnitude => effectMagnitude;

        /// <summary>
        /// Duration of <see cref="RewardEffect"/> in AI turns, for timed effects.
        /// TODO(design): authored per question; Table 4's attack buff sample lasts 1 turn.
        /// </summary>
        public int EffectDurationTurns => effectDurationTurns;

        /// <summary>
        /// Reales deducted for a wrong answer.
        /// TODO(design): the wrong-answer penalty is not specified in the capstone document; the
        /// 0 default is a placeholder, not a decision.
        /// </summary>
        public int WrongAnswerPenaltyReales => wrongAnswerPenaltyReales;

        /// <summary>
        /// Returns the option text at an index, or an empty string when the index is out of range.
        /// </summary>
        /// <param name="index">Zero-based option index: 0 = A, 1 = B, 2 = C, 3 = D.</param>
        public string GetOption(int index)
        {
            if (options == null || index < 0 || index >= options.Length)
            {
                return string.Empty;
            }

            return options[index] ?? string.Empty;
        }

        /// <summary>Returns true when the chosen option index is the correct one.</summary>
        /// <param name="chosenOptionIndex">Zero-based index of the option the player picked.</param>
        public bool IsCorrect(int chosenOptionIndex)
        {
            return chosenOptionIndex == correctOptionIndex;
        }

        /// <summary>
        /// True when the asset is fully authored: it has question text, four non-empty options and
        /// a correct index inside that range.
        /// </summary>
        public bool IsAuthored
        {
            get
            {
                if (string.IsNullOrEmpty(questionText) || options == null || options.Length != OptionCount)
                {
                    return false;
                }

                for (int i = 0; i < options.Length; i++)
                {
                    if (string.IsNullOrEmpty(options[i]))
                    {
                        return false;
                    }
                }

                return correctOptionIndex >= 0 && correctOptionIndex < OptionCount;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only guard keeping the option array at exactly <see cref="OptionCount"/> entries
        /// and the correct index inside it, so the A-D schema of Table 4 cannot be broken by hand.
        /// </summary>
        private void OnValidate()
        {
            if (options == null || options.Length != OptionCount)
            {
                string[] resized = new string[OptionCount];

                if (options != null)
                {
                    int copyCount = options.Length < OptionCount ? options.Length : OptionCount;

                    for (int i = 0; i < copyCount; i++)
                    {
                        resized[i] = options[i];
                    }
                }

                options = resized;
            }

            if (correctOptionIndex < 0)
            {
                correctOptionIndex = 0;
            }
            else if (correctOptionIndex >= OptionCount)
            {
                correctOptionIndex = OptionCount - 1;
            }
        }
#endif
    }
}
