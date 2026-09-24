using System;
using System.Collections.Generic;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// A multiple-choice card: one or more questions, four choices each, the right answer and
    /// why shown after every pick. The mid-battle question and the Library's level tests.
    /// </summary>
    /// <remarks>
    /// Each answer is reported as it is given (<c>answered</c>), and the count right when the
    /// last is done (<c>finished</c>), so the caller pays and records without the card knowing
    /// the rules. Keys 1 to 4 pick; Space or Enter continues.
    /// </remarks>
    public sealed class QuizCard : MonoBehaviour
    {
        private const float CardWidth = 1040f;
        private const float CardHeight = 760f;
        private const float ChoiceHeight = 66f;
        private static readonly string[] Letters = { "A", "B", "C", "D" };

        private IList<Question> questions;
        private string heading;
        private Action<Question, bool> onAnswered;
        private Action<int> onFinished;
        private int index;
        private int picked = -1;
        private int right;
        private int openedFrame;
        private int renderedVersion = -1;

        private RectTransform card;
        private CanvasGroup cardGroup;
        private TextMeshProUGUI title;
        private TextMeshProUGUI progress;
        private TextMeshProUGUI prompt;
        private readonly Button[] choices = new Button[Learning.ChoiceCount];
        private readonly Image[] rims = new Image[Learning.ChoiceCount];
        private readonly TextMeshProUGUI[] choiceLabels = new TextMeshProUGUI[Learning.ChoiceCount];
        private TextMeshProUGUI verdict;
        private TextMeshProUGUI explanation;
        private Button next;

        /// <summary>The card showing now, or null.</summary>
        public static QuizCard Current { get; private set; }

        /// <summary>The question on the card now, or null. For screenshots and tests.</summary>
        public Question Asking
        {
            get { return questions != null && index >= 0 && index < questions.Count ? questions[index] : null; }
        }

        /// <summary>Picks choice <paramref name="choice"/>, 0 to 3, as a click would.</summary>
        public void Choose(int choice)
        {
            Pick(choice);
        }

        /// <summary>The card's frame, for layout checks.</summary>
        public RectTransform Card
        {
            get { return card; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
        }

        /// <summary>
        /// Asks <paramref name="list"/> in order. <paramref name="answered"/> hears each answer;
        /// <paramref name="finished"/> gets the number right. With nothing to ask, finishes at once.
        /// </summary>
        public static QuizCard Show(IList<Question> list, string title, Action<Question, bool> answered, Action<int> finished)
        {
            if (list == null || list.Count == 0 || Current != null)
            {
                if (finished != null)
                {
                    finished(0);
                }

                return null;
            }

            Canvas canvas = UiKit.Screen("Quiz", Theme.Layer.Modal);
            UiKit.EnsureEventSystem();
            var view = canvas.gameObject.AddComponent<QuizCard>();
            view.questions = list;
            view.heading = title;
            view.onAnswered = answered;
            view.onFinished = finished;
            view.Build(canvas.transform);
            Current = view;
            view.Ask(0);
            view.StartCoroutine(UiTween.Enter(view.card, view.cardGroup, new Vector2(0f, -40f), 0.3f));
            UiSfx.Play(UiSfx.Cue.Open);
            return view;
        }

        private void Build(Transform root)
        {
            UiKit.Scrim(root);

            card = UiKit.Panel(root, "Card");
            UiKit.Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
            cardGroup = UiKit.Group(card.gameObject);

            RectTransform column = UiKit.Column(card, "Column", Theme.Space.Snug, Theme.Space.FramePadding + 8f, TextAnchor.UpperLeft);
            UiKit.Stretch(column);
            UiLayout.FillWidth(column);

            RectTransform header = UiKit.Row(column, "Header", Theme.Space.Base, 0f, TextAnchor.MiddleLeft);
            UiLayout.Fix(header, 0f, 48f);
            Image sigil = UiKit.Sigil(header, 40f, Theme.Gold);
            UiLayout.Fix((RectTransform)sigil.transform.parent, 40f, 40f);
            title = UiKit.Display(header, string.Empty, Theme.Type.Title, TextAlignmentOptions.Left);
            title.color = Theme.Revolution;
            UiLayout.OneLine(title, Theme.Type.Title);
            UiLayout.Flexible(title.rectTransform);
            UiLayout.Fix(title.rectTransform, 0f, 48f);
            progress = UiKit.Caption(header, string.Empty, TextAlignmentOptions.Right);
            progress.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            UiLayout.OneLine(progress, Theme.Type.Small);
            UiLayout.Fix(progress.rectTransform, 220f, 48f);

            Image rule = UiKit.Divider(column);
            UiLayout.Fix(rule.rectTransform, 0f, 16f);

            prompt = UiKit.Body(column, string.Empty, Theme.Type.Heading, TextAlignmentOptions.TopLeft);
            prompt.fontStyle = FontStyles.Bold;
            prompt.textWrappingMode = TextWrappingModes.Normal;
            UiLayout.Fix(prompt.rectTransform, 0f, 72f);

            for (int i = 0; i < Learning.ChoiceCount; i++)
            {
                int choice = i;
                choices[i] = UiKit.SelectableRow(column, "Choice " + Letters[i], out rims[i]);
                UiLayout.Fix((RectTransform)choices[i].transform, 0f, ChoiceHeight);
                choices[i].onClick.AddListener(() => Pick(choice));
                choiceLabels[i] = UiKit.Body(choices[i].transform, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Left);
                choiceLabels[i].raycastTarget = false;
                UiLayout.OneLine(choiceLabels[i], Theme.Type.Body + 2f);
                choiceLabels[i].rectTransform.anchorMin = Vector2.zero;
                choiceLabels[i].rectTransform.anchorMax = Vector2.one;
                choiceLabels[i].rectTransform.offsetMin = new Vector2(Theme.Space.Wide, 4f);
                choiceLabels[i].rectTransform.offsetMax = new Vector2(-Theme.Space.Wide, -4f);
            }

            verdict = UiKit.Body(column, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Left);
            verdict.fontStyle = FontStyles.Bold;
            UiLayout.OneLine(verdict, Theme.Type.Heading);
            UiLayout.Fix(verdict.rectTransform, 0f, 32f);

            explanation = UiKit.Body(column, string.Empty, Theme.Type.Body, TextAlignmentOptions.TopLeft);
            explanation.fontStyle = FontStyles.Italic;
            explanation.textWrappingMode = TextWrappingModes.Normal;
            UiLayout.Fix(explanation.rectTransform, 0f, 50f);

            RectTransform footer = UiKit.Row(column, "Footer", 0f, 0f, TextAnchor.MiddleCenter);
            UiLayout.Fix(footer, 0f, 64f);
            next = UiKit.SealButton(footer, TextKey.MenuContinue, Continue, 300f, 60f, 0f, "Button Quiz Continue");
        }

        // ------------------------------------------------------------------ asking

        private void Ask(int question)
        {
            index = question;
            picked = -1;
            openedFrame = Time.frameCount;
            Render();
        }

        private void Render()
        {
            Question q = questions[index];
            title.text = heading;
            progress.text = Loc.Format(TextKey.QuizProgress, index + 1, questions.Count);
            prompt.text = q.Text.Get();
            for (int i = 0; i < Learning.ChoiceCount; i++)
            {
                choiceLabels[i].text = Letters[i] + ".  " + q.Choices[i].Get();
                choices[i].interactable = picked < 0;
                Color rim = Theme.ParchmentDeep;
                if (picked >= 0 && i == q.Answer)
                {
                    rim = Theme.Success;
                }
                else if (picked >= 0 && i == picked)
                {
                    rim = Theme.Danger;
                }

                rims[i].color = rim;
                choiceLabels[i].color = picked >= 0 && i != q.Answer && i != picked ? Theme.InkSoft : Theme.Ink;
            }

            bool answered = picked >= 0;
            verdict.gameObject.SetActive(answered);
            explanation.gameObject.SetActive(answered);
            next.gameObject.SetActive(answered);
            if (answered)
            {
                bool correct = picked == q.Answer;
                verdict.text = correct ? "✓ " + Loc.Get(TextKey.QuizRight) : Loc.Format(TextKey.QuizWrong, Letters[q.Answer]);
                verdict.color = correct ? Theme.Success : Theme.Danger;
                explanation.text = q.Explanation.Get();
            }

            renderedVersion = Loc.Version;
        }

        private void Pick(int choice)
        {
            if (picked >= 0 || Time.frameCount <= openedFrame)
            {
                return;
            }

            Question q = questions[index];
            picked = choice;
            bool correct = choice == q.Answer;
            if (correct)
            {
                right++;
            }

            UiSfx.Play(correct ? UiSfx.Cue.Confirm : UiSfx.Cue.Error);
            StartCoroutine(UiTween.Punch(choices[correct ? choice : q.Answer].transform, 0.08f, 0.22f));
            Render();
            if (onAnswered != null)
            {
                onAnswered(q, correct);
            }
        }

        /// <summary>The next question, or closes after the last.</summary>
        public void Continue()
        {
            if (picked < 0)
            {
                return;
            }

            UiSfx.Play(UiSfx.Cue.Click);
            if (index + 1 < questions.Count)
            {
                Ask(index + 1);
                return;
            }

            Current = null;
            Action<int> finished = onFinished;
            onFinished = null;
            int score = right;
            Destroy(gameObject);
            if (finished != null)
            {
                finished(score);
            }
        }

        private void Update()
        {
            if (renderedVersion != Loc.Version)
            {
                Render();
            }

            Keyboard keys = Keyboard.current;
            if (keys == null || Time.frameCount <= openedFrame)
            {
                return;
            }

            if (picked < 0)
            {
                if (keys.digit1Key.wasPressedThisFrame) Pick(0);
                else if (keys.digit2Key.wasPressedThisFrame) Pick(1);
                else if (keys.digit3Key.wasPressedThisFrame) Pick(2);
                else if (keys.digit4Key.wasPressedThisFrame) Pick(3);
            }
            else if (keys.spaceKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame)
            {
                Continue();
            }
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }
    }
}
