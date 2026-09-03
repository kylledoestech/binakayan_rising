using System.Collections.Generic;
using UnityEngine;

namespace BinakayanRising.Data
{
    /// <summary>
    /// An authored pool of <see cref="QuizQuestionData"/> assets that hands out questions for the
    /// mid-battle pop-up quiz without repeating one inside the same play session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Corresponds to the "Historical Trivia Database" of <b>Capstone Table 4</b>, which the
    /// document describes as a sample set rather than the full bank; the shipping bank is authored
    /// by the team.
    /// </para>
    /// <para>
    /// The asked-question set is runtime-only state that is deliberately NOT serialized. Note that
    /// ScriptableObject instances survive between Play Mode runs inside the editor, so the set is
    /// cleared in <c>OnEnable</c> and can be cleared explicitly with <see cref="ResetSession"/>.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "NewQuizQuestionBank", menuName = "Binakayan Rising/Quiz Question Bank", order = 4)]
    public sealed class QuizQuestionBank : ScriptableObject
    {
        [Header("Question Pool")]
        [Tooltip("Every question this bank can draw from. Order does not matter; draws are random.")]
        [SerializeField] private List<QuizQuestionData> questions = new List<QuizQuestionData>();

        private readonly HashSet<QuizQuestionData> askedThisSession = new HashSet<QuizQuestionData>();

        private readonly List<QuizQuestionData> drawBuffer = new List<QuizQuestionData>();

        /// <summary>Every question this bank can draw from, in authoring order.</summary>
        public IReadOnlyList<QuizQuestionData> Questions => questions;

        /// <summary>How many questions have already been drawn this session.</summary>
        public int AskedCount => askedThisSession.Count;

        /// <summary>How many usable questions remain undrawn this session.</summary>
        public int RemainingCount
        {
            get
            {
                int remaining = 0;

                for (int i = 0; i < questions.Count; i++)
                {
                    QuizQuestionData question = questions[i];

                    if (question != null && !askedThisSession.Contains(question))
                    {
                        remaining++;
                    }
                }

                return remaining;
            }
        }

        /// <summary>
        /// Draws a random question that has not been asked yet this session and marks it as asked.
        /// Returns <c>null</c> when every question in the bank has already been drawn, letting the
        /// caller decide whether to skip the quiz or call <see cref="ResetSession"/> and reuse the
        /// pool.
        /// </summary>
        /// <param name="random">
        /// Optional deterministic source. When supplied — for example the simulation's seeded
        /// generator, so a replay draws the same questions — it is used instead of
        /// <see cref="UnityEngine.Random"/>.
        /// </param>
        public QuizQuestionData DrawUnaskedQuestion(System.Random random = null)
        {
            drawBuffer.Clear();

            for (int i = 0; i < questions.Count; i++)
            {
                QuizQuestionData question = questions[i];

                if (question != null && !askedThisSession.Contains(question))
                {
                    drawBuffer.Add(question);
                }
            }

            if (drawBuffer.Count == 0)
            {
                return null;
            }

            int pick = random != null
                ? random.Next(0, drawBuffer.Count)
                : UnityEngine.Random.Range(0, drawBuffer.Count);

            QuizQuestionData drawn = drawBuffer[pick];
            drawBuffer.Clear();
            askedThisSession.Add(drawn);

            return drawn;
        }

        /// <summary>
        /// True when the question has already been drawn during this session.
        /// </summary>
        /// <param name="question">Question to test. A <c>null</c> question is never "asked".</param>
        public bool HasBeenAsked(QuizQuestionData question)
        {
            return question != null && askedThisSession.Contains(question);
        }

        /// <summary>
        /// Marks a question as already asked without drawing it, so a restored save can rebuild the
        /// session state before the next draw.
        /// </summary>
        /// <param name="question">Question to mark. <c>null</c> is ignored.</param>
        public void MarkAsked(QuizQuestionData question)
        {
            if (question != null)
            {
                askedThisSession.Add(question);
            }
        }

        /// <summary>
        /// Forgets every question drawn so far, making the whole bank drawable again.
        /// </summary>
        public void ResetSession()
        {
            askedThisSession.Clear();
        }

        private void OnEnable()
        {
            askedThisSession.Clear();
            drawBuffer.Clear();
        }
    }
}
