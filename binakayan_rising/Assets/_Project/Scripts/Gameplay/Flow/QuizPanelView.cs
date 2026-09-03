using System;
using UnityEngine;
using UnityEngine.UI;
using BinakayanRising.Data;

namespace BinakayanRising.Gameplay.Flow
{
    /// <summary>
    /// The uGUI pop-up for the historical trivia quiz: the question, four option buttons labelled
    /// A-D, correct/wrong feedback, and the reward line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Presentation only. It never decides whether an answer is right, never touches the wallet and
    /// never pauses anything — it raises <see cref="OptionChosen"/> and waits to be told what
    /// happened by <see cref="QuizController"/>.
    /// </para>
    /// <para>
    /// The four options come from <b>Capstone Table 4</b>, whose every sample row lists exactly four
    /// choices labelled A through D; <see cref="QuizQuestionData.OptionCount"/> is that four. Assign
    /// exactly four buttons and four labels in the Inspector, in A, B, C, D order.
    /// </para>
    /// <para>
    /// Everything animates in unscaled time, because the Quiz state may have set
    /// <c>Time.timeScale</c> to zero to freeze combat timers.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class QuizPanelView : MonoBehaviour
    {
        [Header("Root")]
        [Tooltip("Object toggled on and off to show and hide the pop-up. Leave empty to use this object.")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("Optional CanvasGroup used to fade the pop-up in and out.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Question")]
        [Tooltip("Text field showing the historical question.")]
        [SerializeField] private Text questionText;

        [Header("Options (A, B, C, D in order)")]
        [Tooltip("Exactly four option buttons, in display order A, B, C, D.")]
        [SerializeField] private Button[] optionButtons = new Button[QuizQuestionData.OptionCount];

        [Tooltip("Exactly four option labels, matching the buttons above one for one.")]
        [SerializeField] private Text[] optionLabels = new Text[QuizQuestionData.OptionCount];

        [Tooltip("Prefix each option with its letter, e.g. 'C. Edilberto Evangelista'.")]
        [SerializeField] private bool prefixOptionLetters = true;

        [Header("Feedback")]
        [Tooltip("Text field showing 'Correct!' or 'Incorrect'.")]
        [SerializeField] private Text feedbackText;

        [Tooltip("Text field showing the reward line, e.g. '+50 Reales - Map-wide +10% HP heal'.")]
        [SerializeField] private Text rewardText;

        [Tooltip("Message shown on a correct answer.")]
        [SerializeField] private string correctMessage = "Correct!";

        [Tooltip("Message shown on a wrong answer.")]
        [SerializeField] private string wrongMessage = "Incorrect.";

        [Header("Colours")]
        [Tooltip("Tint of the correct option once the answer is revealed.")]
        [SerializeField] private Color correctColor = new Color(0.25f, 0.7f, 0.35f, 1f);

        [Tooltip("Tint of a wrongly chosen option once the answer is revealed.")]
        [SerializeField] private Color wrongColor = new Color(0.85f, 0.28f, 0.28f, 1f);

        [Tooltip("Tint an option button returns to when a new question is shown.")]
        [SerializeField] private Color neutralColor = Color.white;

        [Header("Animation")]
        [Tooltip("Unscaled seconds the pop-up takes to fade in. TODO(design): not specified in capstone document.")]
        [Min(0f)]
        [SerializeField] private float fadeInSeconds = 0.15f;

        private readonly Color[] originalButtonColors = new Color[QuizQuestionData.OptionCount];

        private float fadeStartUnscaledTime;
        private bool fading;

        /// <summary>
        /// Raised when the player left-clicks an option, carrying its zero-based index:
        /// 0 = A, 1 = B, 2 = C, 3 = D.
        /// </summary>
        public event Action<int> OptionChosen;

        /// <summary>True while the pop-up is on screen.</summary>
        public bool IsVisible
        {
            get { return Root != null && Root.activeSelf; }
        }

        /// <summary>
        /// Fills the pop-up with a question and shows it, clearing any feedback from the last one.
        /// </summary>
        /// <param name="question">Question to display. Null is ignored.</param>
        public void Show(QuizQuestionData question)
        {
            if (question == null)
            {
                return;
            }

            if (questionText != null)
            {
                questionText.text = question.QuestionText;
            }

            for (int i = 0; i < QuizQuestionData.OptionCount; i++)
            {
                Text label = GetLabel(i);

                if (label != null)
                {
                    string option = question.GetOption(i);
                    label.text = prefixOptionLetters ? LetterFor(i) + ". " + option : option;
                }

                Button button = GetButton(i);

                if (button != null)
                {
                    button.interactable = true;
                    SetButtonTint(button, originalButtonColors[i]);
                }
            }

            if (feedbackText != null)
            {
                feedbackText.text = string.Empty;
            }

            if (rewardText != null)
            {
                rewardText.text = string.Empty;
            }

            if (Root != null)
            {
                Root.SetActive(true);
            }

            BeginFadeIn();
        }

        /// <summary>Hides the pop-up.</summary>
        public void Hide()
        {
            fading = false;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (Root != null)
            {
                Root.SetActive(false);
            }
        }

        /// <summary>Enables or disables all four option buttons at once.</summary>
        /// <param name="interactable">True to let the player answer.</param>
        public void SetOptionsInteractable(bool interactable)
        {
            for (int i = 0; i < QuizQuestionData.OptionCount; i++)
            {
                Button button = GetButton(i);

                if (button != null)
                {
                    button.interactable = interactable;
                }
            }
        }

        /// <summary>
        /// Reveals the outcome: writes the feedback message and the reward line, tints the correct
        /// option green, and leaves any wrongly chosen option red.
        /// </summary>
        /// <param name="wasCorrect">Whether the player answered correctly.</param>
        /// <param name="correctOptionIndex">Zero-based index of the correct option.</param>
        /// <param name="rewardSummary">
        /// One line describing the payout, from <see cref="QuizController.DescribeReward"/>. May be
        /// empty.
        /// </param>
        public void ShowFeedback(bool wasCorrect, int correctOptionIndex, string rewardSummary)
        {
            if (feedbackText != null)
            {
                feedbackText.text = wasCorrect ? correctMessage : wrongMessage;
                feedbackText.color = wasCorrect ? correctColor : wrongColor;
            }

            if (rewardText != null)
            {
                rewardText.text = rewardSummary ?? string.Empty;
            }

            for (int i = 0; i < QuizQuestionData.OptionCount; i++)
            {
                Button button = GetButton(i);

                if (button == null)
                {
                    continue;
                }

                if (i == correctOptionIndex)
                {
                    SetButtonTint(button, correctColor);
                }
                else if (!wasCorrect)
                {
                    SetButtonTint(button, wrongColor);
                }
            }
        }

        /// <summary>Returns the display letter for an option index: 0 becomes "A", 3 becomes "D".</summary>
        /// <param name="index">Zero-based option index.</param>
        public static string LetterFor(int index)
        {
            if (index < 0 || index >= QuizQuestionData.OptionCount)
            {
                return "?";
            }

            return ((char)('A' + index)).ToString();
        }

        private GameObject Root
        {
            get { return panelRoot != null ? panelRoot : gameObject; }
        }

        private void Awake()
        {
            for (int i = 0; i < QuizQuestionData.OptionCount; i++)
            {
                Button button = GetButton(i);

                if (button == null)
                {
                    Debug.LogWarning(
                        "[QuizPanelView] Option button " + LetterFor(i) + " is not assigned. Capstone " +
                        "Table 4 requires four options per question.",
                        this);
                    continue;
                }

                originalButtonColors[i] = button.targetGraphic != null
                    ? button.targetGraphic.color
                    : neutralColor;

                int captured = i;
                button.onClick.AddListener(delegate { RaiseOptionChosen(captured); });
            }

            Hide();
        }

        private void Update()
        {
            if (!fading || canvasGroup == null)
            {
                return;
            }

            if (fadeInSeconds <= 0f)
            {
                canvasGroup.alpha = 1f;
                fading = false;
                return;
            }

            float t = (Time.unscaledTime - fadeStartUnscaledTime) / fadeInSeconds;
            canvasGroup.alpha = Mathf.Clamp01(t);

            if (t >= 1f)
            {
                fading = false;
            }
        }

        private void BeginFadeIn()
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = fadeInSeconds > 0f ? 0f : 1f;

            fadeStartUnscaledTime = Time.unscaledTime;
            fading = fadeInSeconds > 0f;
        }

        private void RaiseOptionChosen(int index)
        {
            Action<int> handler = OptionChosen;

            if (handler != null)
            {
                handler(index);
            }
        }

        private Button GetButton(int index)
        {
            if (optionButtons == null || index < 0 || index >= optionButtons.Length)
            {
                return null;
            }

            return optionButtons[index];
        }

        private Text GetLabel(int index)
        {
            if (optionLabels == null || index < 0 || index >= optionLabels.Length)
            {
                return null;
            }

            return optionLabels[index];
        }

        private static void SetButtonTint(Button button, Color color)
        {
            if (button.targetGraphic != null)
            {
                button.targetGraphic.color = color;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only guard keeping both option arrays at exactly four entries, matching the A-D
        /// schema of Capstone Table 4.
        /// </summary>
        private void OnValidate()
        {
            optionButtons = Resize(optionButtons);
            optionLabels = Resize(optionLabels);
        }

        private static T[] Resize<T>(T[] source)
        {
            if (source != null && source.Length == QuizQuestionData.OptionCount)
            {
                return source;
            }

            T[] resized = new T[QuizQuestionData.OptionCount];

            if (source != null)
            {
                int count = source.Length < QuizQuestionData.OptionCount
                    ? source.Length
                    : QuizQuestionData.OptionCount;

                for (int i = 0; i < count; i++)
                {
                    resized[i] = source[i];
                }
            }

            return resized;
        }
#endif
    }
}
