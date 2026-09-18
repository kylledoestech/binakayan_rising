using System;
using System.Collections.Generic;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// A speech box along the bottom of the screen: the speaker's portrait, full name and role,
    /// then the line itself typed out at the player's chosen text speed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every line carries its speaker's portrait, name and role, so the player always knows who is
    /// telling them what and why that person would know it — the panel's "instructions labeling
    /// of characters".
    /// </para>
    /// <para>
    /// A click on the box, Space or Enter first finishes the line being typed, then moves to the
    /// next. Skip ends the conversation at once and still counts it as heard: a player who skips
    /// the aide's welcome should not be held at the first objective. The card's parchment takes the
    /// advancing click (a panel blocks clicks, and the event bubbles up to this component); its
    /// buttons take their own.
    /// </para>
    /// </remarks>
    public sealed class DialoguePanel : MonoBehaviour, IPointerClickHandler
    {
        public const float Width = 1360f;
        public const float Height = 260f;
        private const float PortraitSize = 168f;

        private RectTransform card;
        private Image portrait;
        private TextMeshProUGUI speakerName;
        private TextMeshProUGUI speakerRole;
        private TextMeshProUGUI body;

        private IReadOnlyList<DialogueLine> lines;
        private int index;
        private float shown;
        private Action finished;
        private int renderedVersion = -1;
        private int openedFrame;
        private Coroutine entering;

        public bool IsOpen
        {
            get { return gameObject.activeSelf; }
        }

        /// <summary>The card, for screenshots and layout checks.</summary>
        public RectTransform Card
        {
            get { return card; }
        }

        /// <summary>True while the current line is still being typed.</summary>
        public bool IsTyping
        {
            get { return body != null && body.maxVisibleCharacters < body.textInfo.characterCount; }
        }

        public static DialoguePanel Create(RectTransform parent)
        {
            RectTransform root = UiKit.NewRect(parent, "Dialogue");
            UiKit.Stretch(root);
            var panel = root.gameObject.AddComponent<DialoguePanel>();
            panel.Build(root);
            root.gameObject.SetActive(false);
            return panel;
        }

        private void Build(RectTransform root)
        {
            card = UiKit.Panel(root, "Card");
            UiKit.Anchor(card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, Theme.Space.Loose), new Vector2(Width, Height));

            RectTransform row = UiKit.Row(card, "Row", Theme.Space.Wide, 0f, TextAnchor.UpperLeft);
            UiKit.Stretch(row, Theme.Space.FramePadding + 4f);

            // The portrait sits in a sunken well, like a framed photograph on the page.
            RectTransform frame = UiKit.Well(row, "Portrait");
            UiLayout.Fix(frame, PortraitSize, PortraitSize);
            portrait = UiKit.Icon(frame, null, PortraitSize - 24f, Color.white);
            UiKit.Anchor((RectTransform)portrait.transform.parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PortraitSize - 24f, PortraitSize - 24f));

            RectTransform column = UiKit.Column(row, "Text", Theme.Space.Hair, 0f, TextAnchor.UpperLeft);
            UiLayout.Flexible(column);
            UiLayout.FlexibleHeight(column);
            UiLayout.FillWidth(column);

            speakerName = UiKit.Display(column, string.Empty, Theme.Type.Title, TextAlignmentOptions.BottomLeft);
            speakerName.color = Theme.Revolution;
            UiLayout.OneLine(speakerName, Theme.Type.Title);
            UiLayout.Fix(speakerName.rectTransform, 0f, 40f);

            speakerRole = UiKit.Caption(column, string.Empty, TextAlignmentOptions.TopLeft);
            speakerRole.fontStyle = FontStyles.Italic;
            UiLayout.OneLine(speakerRole, Theme.Type.Body);
            UiLayout.Fix(speakerRole.rectTransform, 0f, 24f);

            Image rule = UiKit.Divider(column);
            UiLayout.Fix(rule.rectTransform, 0f, 14f);

            body = UiKit.Body(column, string.Empty, Theme.Type.Body + 4f, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.lineSpacing = 6f;
            UiLayout.FlexibleHeight(body.rectTransform);

            // Controls down the right: Next above Skip, with the keyboard hint under them.
            RectTransform controls = UiKit.Column(row, "Controls", Theme.Space.Tight, 0f, TextAnchor.LowerRight);
            UiLayout.Fix(controls, 200f, 0f);
            UiLayout.FlexibleHeight(controls);

            Button next = UiKit.SealButton(controls, TextKey.DlgNext, Advance, 200f, 60f, Theme.Type.Heading, "Button Next");
            UiLayout.Fix((RectTransform)next.transform, 200f, 60f);

            Button skip = UiKit.SealButton(controls, TextKey.DlgSkip, Finish, 200f, 48f, Theme.Type.Small + 2f, "Button Skip");
            UiLayout.Fix((RectTransform)skip.transform, 200f, 48f);

            TextMeshProUGUI hint = UiKit.Caption(controls, Loc.Get(TextKey.DlgHint), TextAlignmentOptions.Right);
            UiKit.Localize(hint, TextKey.DlgHint);
            UiLayout.OneLine(hint, Theme.Type.Small);
            UiLayout.Fix(hint.rectTransform, 200f, 24f);
        }

        /// <summary>Plays <paramref name="script"/> from its first line.</summary>
        /// <param name="onFinished">Called once, when the last line is dismissed or Skip is pressed.</param>
        public void Play(IReadOnlyList<DialogueLine> script, Action onFinished)
        {
            if (script == null || script.Count == 0)
            {
                if (onFinished != null)
                {
                    onFinished();
                }

                return;
            }

            lines = script;
            finished = onFinished;
            index = 0;
            openedFrame = Time.frameCount;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ShowLine();
            UiSfx.Play(UiSfx.Cue.Open);
            if (entering != null)
            {
                CoroutineHost.Stop(entering);
            }

            card.anchoredPosition = new Vector2(0f, Theme.Space.Loose);
            entering = CoroutineHost.Run(UiTween.Enter(card, UiKit.Group(card.gameObject), new Vector2(0f, -24f), UiTween.PanelDuration));
        }

        /// <summary>Plays a single line.</summary>
        public void Say(string speaker, LocString text, Action onFinished)
        {
            Play(new[] { new DialogueLine(speaker, text) }, onFinished);
        }

        private void ShowLine()
        {
            DialogueLine line = lines[index];
            Character who = Characters.Find(line.Speaker);
            speakerName.text = who != null ? who.Name.Get() : line.Speaker;
            speakerRole.text = who != null ? who.Role.Get() : string.Empty;

            Sprite face = Theme.Assets != null ? Theme.Assets.UnitPortrait(line.Speaker) : null;
            portrait.sprite = face;
            portrait.enabled = face != null;

            body.text = line.Text.Get();
            body.ForceMeshUpdate();
            shown = UserPrefs.CharactersPerSecond(UserPrefs.DialogueSpeed) <= 0f ? body.textInfo.characterCount : 0f;
            body.maxVisibleCharacters = (int)shown;
            renderedVersion = Loc.Version;
        }

        private void Update()
        {
            if (lines == null)
            {
                return;
            }

            // A language switch mid-conversation re-reads the line and shows it whole.
            if (renderedVersion != Loc.Version)
            {
                ShowLine();
                shown = body.textInfo.characterCount;
            }

            if (IsTyping)
            {
                shown += UserPrefs.CharactersPerSecond(UserPrefs.DialogueSpeed) * Time.unscaledDeltaTime;
                body.maxVisibleCharacters = Mathf.Min((int)shown, body.textInfo.characterCount);
            }

            // The keypress that opened the box must not also dismiss its first line.
            Keyboard keys = Keyboard.current;
            if (keys != null && Time.frameCount > openedFrame
                && (keys.spaceKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame))
            {
                Advance();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Time.frameCount > openedFrame)
            {
                Advance();
            }
        }

        /// <summary>Finishes the line being typed, or moves to the next one.</summary>
        public void Advance()
        {
            if (lines == null)
            {
                return;
            }

            if (IsTyping)
            {
                shown = body.textInfo.characterCount;
                body.maxVisibleCharacters = body.textInfo.characterCount;
                return;
            }

            index++;
            if (index >= lines.Count)
            {
                Finish();
                return;
            }

            UiSfx.Play(UiSfx.Cue.Click);
            ShowLine();
        }

        /// <summary>Closes the box and reports the conversation as heard.</summary>
        public void Finish()
        {
            if (lines == null)
            {
                return;
            }

            lines = null;
            gameObject.SetActive(false);
            UiSfx.Play(UiSfx.Cue.Close);

            Action done = finished;
            finished = null;
            if (done != null)
            {
                done();
            }
        }

        /// <summary>Closes the box without reporting anything, when the screen leaves.</summary>
        public void Cancel()
        {
            lines = null;
            finished = null;
            gameObject.SetActive(false);
        }
    }
}
