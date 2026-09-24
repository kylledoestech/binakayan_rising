using System;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Localization;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// The Tactician's Command pick (#43): after a right answer the player chooses one of Capstone
    /// Table 4's four effects, and the battle is fought on with it.
    /// </summary>
    /// <remarks>
    /// Styled as the quiz card it follows: the same parchment card, sigil header and selectable
    /// rows, keys 1 to 4 for the rows. A command that would do nothing — a revive with no one
    /// fallen — is shown but cannot be picked, with the reason in place of its description.
    /// There is no cancel: the command is the reward, and it is always the player's to take.
    /// </remarks>
    public sealed class TacticianCommandCard : MonoBehaviour
    {
        private const float CardWidth = 1040f;
        private const float CardHeight = 640f;
        private const float ChoiceHeight = 92f;

        /// <summary>The four commands in Table 4's order, as the rows show them.</summary>
        public static readonly TacticianCommand[] Commands =
        {
            TacticianCommand.MapWideHeal,
            TacticianCommand.AttackBuff,
            TacticianCommand.ResetEnemyPositions,
            TacticianCommand.ReviveFallenUnit
        };

        private readonly Button[] choices = new Button[4];
        private readonly Image[] rims = new Image[4];
        private readonly TextMeshProUGUI[] names = new TextMeshProUGUI[4];
        private readonly TextMeshProUGUI[] hints = new TextMeshProUGUI[4];
        private readonly bool[] allowed = new bool[4];

        private Action<TacticianCommand> onPicked;
        private RectTransform card;
        private CanvasGroup cardGroup;
        private TextMeshProUGUI title;
        private TextMeshProUGUI prompt;
        private bool closing;
        private int openedFrame;
        private int renderedVersion = -1;

        /// <summary>The card showing now, or null. For screenshots and tests.</summary>
        public static TacticianCommandCard Current { get; private set; }

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
        /// Offers the four commands. <paramref name="canIssue"/> says which would do anything now;
        /// <paramref name="picked"/> gets the choice, or <see cref="TacticianCommand.None"/> when
        /// none of them could be given.
        /// </summary>
        public static TacticianCommandCard Show(Func<TacticianCommand, bool> canIssue, Action<TacticianCommand> picked)
        {
            bool any = false;
            var allowed = new bool[Commands.Length];
            for (int i = 0; i < Commands.Length; i++)
            {
                allowed[i] = canIssue == null || canIssue(Commands[i]);
                any |= allowed[i];
            }

            if (!any || Current != null)
            {
                if (picked != null)
                {
                    picked(TacticianCommand.None);
                }

                return null;
            }

            Canvas canvas = UiKit.Screen("Tactician's Command", Theme.Layer.Modal);
            UiKit.EnsureEventSystem();
            var view = canvas.gameObject.AddComponent<TacticianCommandCard>();
            Array.Copy(allowed, view.allowed, allowed.Length);
            view.onPicked = picked;
            view.Build(canvas.transform);
            view.openedFrame = Time.frameCount;
            view.Render();
            Current = view;
            view.StartCoroutine(UiTween.Enter(view.card, view.cardGroup, new Vector2(0f, -40f), 0.3f));
            UiSfx.Play(UiSfx.Cue.Open);
            return view;
        }

        /// <summary>Whether row <paramref name="index"/> can be picked. For screenshots and tests.</summary>
        public bool IsAllowed(int index)
        {
            return index >= 0 && index < allowed.Length && allowed[index];
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
            Image sigil = UiKit.Sigil(header, 40f, Theme.GoldBright);
            UiLayout.Fix((RectTransform)sigil.transform.parent, 40f, 40f);
            title = UiKit.Display(header, string.Empty, Theme.Type.Title, TextAlignmentOptions.Left);
            title.color = Theme.Revolution;
            UiLayout.OneLine(title, Theme.Type.Title);
            UiLayout.Flexible(title.rectTransform);
            UiLayout.Fix(title.rectTransform, 0f, 48f);

            Image rule = UiKit.Divider(column);
            UiLayout.Fix(rule.rectTransform, 0f, 16f);

            prompt = UiKit.Body(column, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Left);
            prompt.fontStyle = FontStyles.Bold;
            UiLayout.OneLine(prompt, Theme.Type.Heading);
            UiLayout.Fix(prompt.rectTransform, 0f, 40f);

            for (int i = 0; i < Commands.Length; i++)
            {
                int choice = i;
                choices[i] = UiKit.SelectableRow(column, "Command " + (i + 1), out rims[i]);
                UiLayout.Fix((RectTransform)choices[i].transform, 0f, ChoiceHeight);
                choices[i].onClick.AddListener(() => Pick(choice));

                names[i] = UiKit.Body(choices[i].transform, string.Empty, Theme.Type.Heading, TextAlignmentOptions.TopLeft);
                names[i].fontStyle = FontStyles.Bold;
                names[i].raycastTarget = false;
                UiLayout.OneLine(names[i], Theme.Type.Heading);
                Place(names[i].rectTransform, 0.5f, 1f);

                hints[i] = UiKit.Caption(choices[i].transform, string.Empty, TextAlignmentOptions.BottomLeft);
                hints[i].fontStyle = FontStyles.Italic;
                hints[i].raycastTarget = false;
                UiLayout.OneLine(hints[i], Theme.Type.Body);
                Place(hints[i].rectTransform, 0f, 0.5f);
            }
        }

        /// <summary>Stretches a label across a row between two heights, inside the row's padding.</summary>
        private static void Place(RectTransform label, float bottom, float top)
        {
            label.anchorMin = new Vector2(0f, bottom);
            label.anchorMax = new Vector2(1f, top);
            label.offsetMin = new Vector2(Theme.Space.Wide, bottom > 0f ? 0f : 8f);
            label.offsetMax = new Vector2(-Theme.Space.Wide, top < 1f ? 0f : -8f);
        }

        private void Render()
        {
            title.text = Loc.Get(TextKey.CmdTitle);
            prompt.text = Loc.Get(TextKey.CmdPrompt);
            int heal = Percent(TacticianCommands.HealFraction);
            for (int i = 0; i < Commands.Length; i++)
            {
                string name;
                string hint;
                switch (Commands[i])
                {
                    case TacticianCommand.MapWideHeal:
                        name = Loc.Format(TextKey.CmdHeal, heal);
                        hint = Loc.Format(TextKey.CmdHealHint, heal);
                        break;
                    case TacticianCommand.AttackBuff:
                        name = Loc.Format(TextKey.CmdAttack, Percent(TacticianCommands.AttackFraction), TacticianCommands.AttackTurns);
                        hint = Loc.Get(TextKey.CmdAttackHint);
                        break;
                    case TacticianCommand.ResetEnemyPositions:
                        name = Loc.Get(TextKey.CmdReset);
                        hint = Loc.Get(TextKey.CmdResetHint);
                        break;
                    default:
                        name = Loc.Get(TextKey.CmdRevive);
                        hint = Loc.Get(allowed[i] ? TextKey.CmdReviveHint : TextKey.CmdReviveNone);
                        break;
                }

                names[i].text = (i + 1) + ".  " + name;
                hints[i].text = hint;
                choices[i].interactable = allowed[i] && !closing;
                names[i].color = allowed[i] ? Theme.Ink : Theme.InkSoft;
                hints[i].color = allowed[i] ? Theme.InkSoft : Theme.Danger;
                rims[i].color = Theme.ParchmentDeep;
            }

            renderedVersion = Loc.Version;
        }

        private static int Percent(float fraction)
        {
            return Mathf.RoundToInt(fraction * 100f);
        }

        /// <summary>Gives the command in row <paramref name="index"/>, if it can be given.</summary>
        public void Pick(int index)
        {
            if (closing || index < 0 || index >= Commands.Length || !allowed[index] || Time.frameCount <= openedFrame)
            {
                return;
            }

            closing = true;
            rims[index].color = Theme.GoldBright;
            UiSfx.Play(UiSfx.Cue.Confirm);
            StartCoroutine(PickThenClose(index));
        }

        private System.Collections.IEnumerator PickThenClose(int index)
        {
            Render();
            rims[index].color = Theme.GoldBright;
            yield return UiTween.Punch(choices[index].transform, 0.08f, 0.22f);

            Current = null;
            Action<TacticianCommand> picked = onPicked;
            onPicked = null;
            Destroy(gameObject);
            if (picked != null)
            {
                picked(Commands[index]);
            }
        }

        private void Update()
        {
            if (renderedVersion != Loc.Version && !closing)
            {
                Render();
            }

            Keyboard keys = Keyboard.current;
            if (keys == null || closing || Time.frameCount <= openedFrame)
            {
                return;
            }

            if (keys.digit1Key.wasPressedThisFrame) Pick(0);
            else if (keys.digit2Key.wasPressedThisFrame) Pick(1);
            else if (keys.digit3Key.wasPressedThisFrame) Pick(2);
            else if (keys.digit4Key.wasPressedThisFrame) Pick(3);
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
