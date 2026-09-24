using System.Collections.Generic;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using BinakayanRising.Core.Meta;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// The Library: the lessons each cleared sub-quest unlocks, and a test per campaign level
    /// once that level is cleared (panel feedback: "learning panel / assessment").
    /// </summary>
    /// <remarks>
    /// A test draws <see cref="MetaRules.AssessmentQuestions"/> of the level's questions in a
    /// fresh order each attempt. Retakes are free; the first pass pays a bonus.
    /// </remarks>
    public sealed class LibraryPanel : MonoBehaviour
    {
        private const float CardWidth = 1360f;
        private const float CardHeight = 800f;
        private const float ListWidth = 460f;
        private const float RowHeight = 48f;

        private GameShell shell;
        private RectTransform card;
        private CanvasGroup cardGroup;
        private TextMeshProUGUI title;
        private readonly List<Button> rows = new List<Button>();
        private readonly List<Image> rims = new List<Image>();
        private readonly List<TextMeshProUGUI> rowLabels = new List<TextMeshProUGUI>();
        private readonly TextMeshProUGUI[] testNotes = new TextMeshProUGUI[Campaign.LevelCount];
        private readonly Button[] testButtons = new Button[Campaign.LevelCount];
        private TextMeshProUGUI lessonTitle;
        private TextMeshProUGUI lessonBody;
        private int selected = -1;
        private int renderedVersion = -1;
        private int openedFrame;

        /// <summary>The Library open now, or null.</summary>
        public static LibraryPanel Current { get; private set; }

        public RectTransform Card
        {
            get { return card; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
        }

        public static LibraryPanel Open(GameShell shell)
        {
            if (Current != null || shell == null || shell.Session == null || shell.Session.Game == null)
            {
                return Current;
            }

            Canvas canvas = UiKit.Screen("Library", Theme.Layer.Modal);
            UiKit.EnsureEventSystem();
            var view = canvas.gameObject.AddComponent<LibraryPanel>();
            view.shell = shell;
            view.openedFrame = Time.frameCount;
            view.Build(canvas.transform);
            Current = view;
            view.SelectFirstUnlocked();
            view.StartCoroutine(UiTween.Enter(view.card, view.cardGroup, new Vector2(0f, -24f), UiTween.PanelDuration));
            UiSfx.Play(UiSfx.Cue.Open);
            return view;
        }

        private MetaGame Game
        {
            get { return shell != null && shell.Session != null ? shell.Session.Game : null; }
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
            UiLayout.Fix(header, 0f, 56f);
            Image sigil = UiKit.Sigil(header, 44f, Theme.Gold);
            UiLayout.Fix((RectTransform)sigil.transform.parent, 44f, 44f);
            title = UiKit.Display(header, string.Empty, Theme.Type.Title, TextAlignmentOptions.Left);
            UiLayout.OneLine(title, Theme.Type.Title);
            UiLayout.Flexible(title.rectTransform);
            UiLayout.Fix(title.rectTransform, 0f, 52f);
            Button close = UiKit.IconButton(header, Theme.IconClose, Close, 52f, "Button Close X");
            UiLayout.Fix((RectTransform)close.transform, 52f, 52f);

            Image rule = UiKit.Divider(column);
            UiLayout.Fix(rule.rectTransform, 0f, 16f);

            RectTransform body = UiKit.Row(column, "Body", Theme.Space.Huge, 0f, TextAnchor.UpperLeft);
            UiLayout.FlexibleHeight(body);

            // The shelf: every lesson, the locked ones named by the quest that opens them.
            RectTransform shelf = UiKit.Column(body, "Shelf", Theme.Space.Hair, 0f, TextAnchor.UpperLeft);
            UiLayout.Fix(shelf, ListWidth, 0f);
            UiLayout.FlexibleHeight(shelf);
            UiLayout.FillWidth(shelf);
            IReadOnlyList<Lesson> lessons = Learning.Lessons;
            for (int i = 0; i < lessons.Count; i++)
            {
                int lesson = i;
                Image rim;
                Button row = UiKit.SelectableRow(shelf, "Lesson " + lessons[i].Id, out rim);
                UiLayout.Fix((RectTransform)row.transform, ListWidth, RowHeight);
                row.onClick.AddListener(() => Select(lesson, true));
                TextMeshProUGUI label = UiKit.Body(row.transform, string.Empty, Theme.Type.Body, TextAlignmentOptions.Left);
                label.raycastTarget = false;
                UiLayout.OneLine(label, Theme.Type.Body);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(Theme.Space.Base, 2f);
                label.rectTransform.offsetMax = new Vector2(-Theme.Space.Base, -2f);
                rows.Add(row);
                rims.Add(rim);
                rowLabels.Add(label);
            }

            // The reading desk.
            RectTransform desk = UiKit.Column(body, "Desk", Theme.Space.Snug, 0f, TextAnchor.UpperLeft);
            UiLayout.Flexible(desk);
            UiLayout.FlexibleHeight(desk);
            UiLayout.FillWidth(desk);

            lessonTitle = UiKit.Display(desk, string.Empty, Theme.Type.Title, TextAlignmentOptions.Left);
            lessonTitle.color = Theme.Revolution;
            UiLayout.OneLine(lessonTitle, Theme.Type.Title);
            UiLayout.Fix(lessonTitle.rectTransform, 0f, 48f);

            RectTransform page = UiKit.Well(desk, "Page");
            UiLayout.Fix(page, 0f, 330f);
            lessonBody = UiKit.Body(page, string.Empty, Theme.Type.Body + 3f, TextAlignmentOptions.TopLeft);
            lessonBody.textWrappingMode = TextWrappingModes.Normal;
            lessonBody.paragraphSpacing = 12f;
            UiKit.Stretch(lessonBody.rectTransform, Theme.Space.Wide);

            // One test per level.
            RectTransform tests = UiKit.Row(desk, "Tests", Theme.Space.Base, 0f, TextAnchor.UpperLeft);
            UiLayout.Fix(tests, 0f, 110f);
            for (int i = 0; i < Campaign.LevelCount; i++)
            {
                int level = i + 1;
                RectTransform slot = UiKit.Column(tests, "Test " + level, Theme.Space.Hair, 0f, TextAnchor.UpperCenter);
                UiLayout.Fix(slot, 248f, 110f);
                testButtons[i] = UiKit.SealButton(slot, Loc.Format(TextKey.LibTest, level), () => StartTest(level), 248f, 58f, Theme.Type.Body, "Button Test " + level);
                UiLayout.Fix((RectTransform)testButtons[i].transform, 248f, 58f);
                testNotes[i] = UiKit.Caption(slot, string.Empty, TextAlignmentOptions.Center);
                testNotes[i].textWrappingMode = TextWrappingModes.Normal;
                UiLayout.Fix(testNotes[i].rectTransform, 248f, 44f);
            }
        }

        // ------------------------------------------------------------------ reading

        private void SelectFirstUnlocked()
        {
            MetaGame game = Game;
            IReadOnlyList<Lesson> lessons = Learning.Lessons;
            int pick = 0;
            for (int i = lessons.Count - 1; i >= 0; i--)
            {
                if (game != null && game.IsLessonUnlocked(lessons[i].Id))
                {
                    pick = i;
                    break;
                }
            }

            Select(pick, false);
        }

        private void Select(int lesson, bool byClick)
        {
            if (byClick)
            {
                UiSfx.Play(UiSfx.Cue.Click);
            }

            selected = lesson;
            Render();
        }

        /// <summary>The quest that unlocks <paramref name="lessonId"/>, or null.</summary>
        private static Quest QuestFor(string lessonId)
        {
            IReadOnlyList<Quest> quests = Campaign.Quests;
            for (int i = 0; i < quests.Count; i++)
            {
                if (quests[i].RewardLesson == lessonId)
                {
                    return quests[i];
                }
            }

            return null;
        }

        private void Render()
        {
            MetaGame game = Game;
            if (game == null)
            {
                return;
            }

            renderedVersion = Loc.Version;
            title.text = Loc.Get(TextKey.LibTitle);

            IReadOnlyList<Lesson> lessons = Learning.Lessons;
            for (int i = 0; i < lessons.Count; i++)
            {
                bool open = game.IsLessonUnlocked(lessons[i].Id);
                rowLabels[i].text = open
                    ? (i + 1) + ". " + lessons[i].Title.Get()
                    : (i + 1) + ". " + Loc.Format(TextKey.LibLockedShort, QuestFor(lessons[i].Id) != null ? QuestFor(lessons[i].Id).Number : i + 1);
                rowLabels[i].color = open ? Theme.Ink : Theme.InkSoft;
                rowLabels[i].fontStyle = open ? FontStyles.Normal : FontStyles.Italic;
                rims[i].color = i == selected ? Theme.Revolution : Theme.ParchmentDeep;
            }

            Lesson lesson = lessons[selected];
            if (game.IsLessonUnlocked(lesson.Id))
            {
                lessonTitle.text = lesson.Title.Get();
                lessonBody.text = lesson.Body.Get();
            }
            else
            {
                Quest quest = QuestFor(lesson.Id);
                lessonTitle.text = "? ? ?";
                lessonBody.text = Loc.Format(TextKey.LibLocked, quest != null ? quest.Number : selected + 1);
            }

            for (int i = 0; i < Campaign.LevelCount; i++)
            {
                int level = i + 1;
                bool open = game.IsAssessmentOpen(level);
                testButtons[i].interactable = open;
                testButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = Loc.Format(TextKey.LibTest, level);
                AssessmentRecord record = game.Assessment(level);
                if (!open)
                {
                    testNotes[i].text = Loc.Format(TextKey.LibTestLocked, level);
                }
                else if (record == null || record.attempts == 0)
                {
                    testNotes[i].text = Loc.Get(TextKey.LibTestReady);
                }
                else
                {
                    testNotes[i].text = Loc.Format(TextKey.LibBest, record.best, record.total) + (record.passed ? "  ✓ " + Loc.Get(TextKey.LibPassed) : string.Empty);
                }
            }
        }

        // ------------------------------------------------------------------ testing

        private void StartTest(int level)
        {
            MetaGame game = Game;
            if (game == null || !game.IsAssessmentOpen(level) || QuizCard.Current != null)
            {
                UiSfx.Play(UiSfx.Cue.Error);
                return;
            }

            int count = game.Rules.AssessmentQuestions;
            AssessmentRecord record = game.Assessment(level);
            int seed = (level * 7919) + (record != null ? record.attempts : 0) * 104729 + System.Environment.TickCount;
            List<Question> test = Learning.Test(level, count, seed);
            card.gameObject.SetActive(false);
            QuizCard.Show(test, Loc.Format(TextKey.LibTest, level), null, score => FinishTest(level, score, test.Count));
        }

        private void FinishTest(int level, int score, int total)
        {
            card.gameObject.SetActive(true);
            MetaGame game = Game;
            if (game == null)
            {
                return;
            }

            AssessmentOutcome outcome = game.RecordAssessment(level, score, total);
            string result = Loc.Format(TextKey.LibResult, outcome.Score, outcome.Total) + " ";
            if (outcome.FirstPass)
            {
                result += Loc.Format(TextKey.LibFirstPass, outcome.Reales);
                UiSfx.Play(UiSfx.Cue.Victory);
            }
            else if (outcome.Passed)
            {
                result += Loc.Get(TextKey.LibPassed);
            }
            else
            {
                result += Loc.Format(TextKey.LibFail, game.Rules.AssessmentPassPercent);
            }

            UiControls.Toast(result, 4f);
            Render();
        }

        public void Close()
        {
            UiSfx.Play(UiSfx.Cue.Close);
            if (Current == this)
            {
                Current = null;
            }

            Destroy(gameObject);
        }

        private void Update()
        {
            if (renderedVersion != Loc.Version)
            {
                Render();
            }

            if (QuizCard.Current == null && Keyboard.current != null && Time.frameCount > openedFrame
                && Keyboard.current.escapeKey.wasPressedThisFrame && (shell == null || !shell.SettingsOpen))
            {
                Close();
                return;
            }

            // Right-click closes as Esc does. The cards that must not be dismissed this way share
            // the Modal layer, so the layer test cannot tell them apart; they are ruled out by name.
            if (QuizCard.Current == null && CutscenePlayer.Current == null && PromotionCard.Current == null
                && RankUpCard.Current == null && Time.frameCount > openedFrame && (shell == null || !shell.SettingsOpen)
                && BinakayanRising.Gameplay.UiPointer.TryClaimRightClick(Theme.Layer.Modal))
            {
                Close();
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
