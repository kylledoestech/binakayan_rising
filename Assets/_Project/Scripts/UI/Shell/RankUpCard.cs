using System;
using System.Collections;
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
    /// The card for a new rank: the insignia's stars land one by one under a turning sun, the
    /// rank's name comes up, and Tadah, the Senador, announces it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the panel's "name identification per level": the player sees the title they now
    /// hold, what it means, and why they earned it, in the narrator's words.
    /// </para>
    /// <para>
    /// Showing the card records the rank as shown, so it plays once per rank. Continue finishes
    /// the animation first, then closes, so the card never holds up someone who has seen it.
    /// </para>
    /// </remarks>
    public sealed class RankUpCard : MonoBehaviour
    {
        private const float CardWidth = 760f;
        private const float CardHeight = 700f;
        private const float SunSize = 200f;
        private const float StarSize = 40f;
        private const float StarStagger = 0.12f;
        private const float SunTurnDegreesPerSecond = 14f;
        private const float PortraitSize = 96f;

        private Action onClosed;
        private RectTransform card;
        private CanvasGroup cardGroup;
        private RectTransform sun;
        private RectTransform stars;
        private TextMeshProUGUI rankName;
        private TextMeshProUGUI rankGloss;
        private TextMeshProUGUI rankStep;
        private TextMeshProUGUI line;
        private Image portrait;
        private TextMeshProUGUI speaker;
        private TextMeshProUGUI speakerRole;
        private PlayerRank rank;
        private float typed;
        private bool settled;
        private int openedFrame;
        private int renderedVersion = -1;

        /// <summary>The card showing now, or null. For screenshots and tests.</summary>
        public static RankUpCard Current { get; private set; }

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
        /// Shows <paramref name="game"/>'s current rank and records it as shown, then calls
        /// <paramref name="closed"/> when the player continues.
        /// </summary>
        public static RankUpCard Show(MetaGame game, Action closed)
        {
            if (game == null || Current != null)
            {
                if (Current != null)
                {
                    Current.onClosed += closed;
                    return Current;
                }

                if (closed != null)
                {
                    closed();
                }

                return null;
            }

            PlayerRank earned = game.Rank;
            game.AcknowledgeRank();

            Canvas canvas = UiKit.Screen("Rank Up", Theme.Layer.Modal);
            UiKit.EnsureEventSystem();
            var view = canvas.gameObject.AddComponent<RankUpCard>();
            view.Build(canvas.transform);
            view.onClosed = closed;
            view.rank = earned;
            Current = view;
            view.StartCoroutine(view.Play());
            return view;
        }

        private void Build(Transform root)
        {
            UiKit.Scrim(root);

            card = UiKit.Panel(root, "Card");
            UiKit.Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
            cardGroup = UiKit.Group(card.gameObject);

            RectTransform column = UiKit.Column(card, "Column", Theme.Space.Tight, Theme.Space.FramePadding + 8f, TextAnchor.UpperCenter);
            UiKit.Stretch(column);
            UiLayout.FillWidth(column);

            TextMeshProUGUI title = UiKit.Display(column, Loc.Get(TextKey.RankUpTitle), Theme.Type.Title);
            title.color = Theme.Revolution;
            UiKit.Localize(title, TextKey.RankUpTitle);
            UiLayout.OneLine(title, Theme.Type.Title);
            UiLayout.Fix(title.rectTransform, 0f, 48f);

            // The insignia: a turning sun behind a row of stars, one per rank held.
            RectTransform stage = UiKit.NewRect(column, "Insignia");
            UiLayout.Fix(stage, 0f, SunSize);

            Image sunImage = UiKit.Sigil(stage, SunSize, Theme.GoldBright);
            sun = (RectTransform)sunImage.transform.parent;
            UiKit.Anchor(sun, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SunSize, SunSize));
            Color glow = Theme.GoldBright;
            glow.a = 0.45f;
            sunImage.color = glow;

            stars = UiKit.Row(stage, "Stars", 2f, 0f, TextAnchor.MiddleCenter);
            UiKit.Anchor(stars, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth - 80f, StarSize + 12f));
            for (int i = 0; i < PlayerRanks.Count; i++)
            {
                // One small sun per rank; the fonts have no star glyph.
                Image star = UiKit.Sigil(stars, StarSize, Theme.RevolutionDark);
                UiLayout.Fix((RectTransform)star.transform.parent, StarSize, StarSize);
            }

            rankName = UiKit.Display(column, string.Empty, Theme.Type.Display, TextAlignmentOptions.Center);
            rankName.color = Theme.Revolution;
            UiLayout.OneLine(rankName, Theme.Type.Display);
            UiLayout.Fix(rankName.rectTransform, 0f, 70f);

            rankGloss = UiKit.Body(column, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Center);
            rankGloss.fontStyle = FontStyles.Italic;
            UiLayout.OneLine(rankGloss, Theme.Type.Heading);
            UiLayout.Fix(rankGloss.rectTransform, 0f, 32f);

            rankStep = UiKit.Caption(column, string.Empty, TextAlignmentOptions.Center);
            rankStep.fontStyle = FontStyles.UpperCase | FontStyles.Bold;
            UiLayout.OneLine(rankStep, Theme.Type.Small);
            UiLayout.Fix(rankStep.rectTransform, 0f, 22f);

            Image rule = UiKit.Divider(column, 520f);
            UiLayout.Fix(rule.rectTransform, 520f, 16f);

            // Tadah, the Senador, says why.
            RectTransform narrator = UiKit.Row(column, "Narrator", Theme.Space.Base, 0f, TextAnchor.UpperLeft);
            UiLayout.Fix(narrator, 0f, 132f);

            RectTransform frame = UiKit.Well(narrator, "Portrait");
            UiLayout.Fix(frame, PortraitSize + 16f, PortraitSize + 16f);
            portrait = UiKit.Icon(frame, null, PortraitSize, Color.white);
            UiKit.Anchor((RectTransform)portrait.transform.parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PortraitSize, PortraitSize));

            RectTransform words = UiKit.Column(narrator, "Words", 0f, 0f, TextAnchor.UpperLeft);
            UiLayout.Flexible(words);
            UiLayout.Fix(words, 0f, 132f);
            UiLayout.FillWidth(words);

            RectTransform nameRow = UiKit.Row(words, "Speaker", Theme.Space.Tight, 0f, TextAnchor.MiddleLeft);
            UiLayout.Fix(nameRow, 0f, 30f);
            speaker = UiKit.Body(nameRow, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Left);
            speaker.fontStyle = FontStyles.Bold;
            speaker.color = Theme.Revolution;
            UiLayout.OneLine(speaker, Theme.Type.Heading);
            UiLayout.Fix(speaker.rectTransform, 110f, 30f);
            speakerRole = UiKit.Caption(nameRow, string.Empty, TextAlignmentOptions.Left);
            speakerRole.fontStyle = FontStyles.Italic;
            UiLayout.OneLine(speakerRole, Theme.Type.Small);
            UiLayout.Flexible(speakerRole.rectTransform);
            UiLayout.Fix(speakerRole.rectTransform, 0f, 30f);

            line = UiKit.Body(words, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.TopLeft);
            line.textWrappingMode = TextWrappingModes.Normal;
            UiLayout.Fix(line.rectTransform, 0f, 96f);

            RectTransform buttons = UiKit.Row(column, "Buttons", 0f, 0f, TextAnchor.MiddleCenter);
            UiLayout.Fix(buttons, 0f, 72f);
            UiKit.SealButton(buttons, TextKey.MenuContinue, Continue, 280f, 60f, 0f, "Button Continue");
        }

        // ------------------------------------------------------------------ playing

        private void Render()
        {
            rankName.text = rank.Title;
            rankGloss.text = rank.Gloss.Get();
            rankStep.text = Loc.Format(TextKey.RankUpStep, rank.Index + 1, PlayerRanks.Count);

            Character tadah = Characters.Find(Characters.Senador);
            speaker.text = tadah != null ? tadah.Name.Get() : string.Empty;
            speakerRole.text = tadah != null ? tadah.Role.Get() : string.Empty;
            Sprite face = Theme.Assets != null ? Theme.Assets.UnitPortrait(Characters.Senador) : null;
            portrait.sprite = face;
            portrait.enabled = face != null;

            line.text = "“" + PlayerRanks.AnnouncementOf(rank.Index).Get() + "”";
            line.ForceMeshUpdate();
            renderedVersion = Loc.Version;
        }

        private IEnumerator Play()
        {
            openedFrame = Time.frameCount;
            settled = false;
            Render();
            typed = 0f;
            line.maxVisibleCharacters = 0;
            rankName.transform.localScale = Vector3.zero;

            UiSfx.Play(UiSfx.Cue.Victory);
            yield return UiTween.Enter(card, cardGroup, new Vector2(0f, -60f), 0.35f);

            // The earned stars light one by one; the ones still ahead stay dark.
            for (int i = 0; i <= rank.Index && i < stars.childCount; i++)
            {
                Image star = stars.GetChild(i).GetComponentInChildren<Image>();
                star.color = i == rank.Index ? Theme.GoldBright : Theme.Gold;
                StartCoroutine(UiTween.Punch(star.transform, 0.45f, 0.25f));
                UiSfx.Play(UiSfx.Cue.Place);
                yield return new WaitForSecondsRealtime(StarStagger);
            }

            // Then the title grows in from nothing, with a small overshoot.
            float start = Time.unscaledTime;
            const float grow = 0.3f;
            while (Time.unscaledTime - start < grow)
            {
                float t = (Time.unscaledTime - start) / grow;
                float overshoot = 1f + (0.18f * Mathf.Sin(t * Mathf.PI));
                rankName.transform.localScale = Vector3.one * (t * overshoot);
                yield return null;
            }

            rankName.transform.localScale = Vector3.one;
            UiSfx.Play(UiSfx.Cue.Confirm);
            settled = true;
        }

        /// <summary>Everything at rest: every earned star lit, the title full size, the line typed.</summary>
        private void Settle()
        {
            StopAllCoroutines();
            for (int i = 0; i < stars.childCount; i++)
            {
                Image star = stars.GetChild(i).GetComponentInChildren<Image>();
                star.transform.localScale = Vector3.one;
                star.color = i > rank.Index ? Theme.RevolutionDark : (i == rank.Index ? Theme.GoldBright : Theme.Gold);
            }

            card.anchoredPosition = Vector2.zero;
            cardGroup.alpha = 1f;
            rankName.transform.localScale = Vector3.one;
            typed = line.textInfo.characterCount;
            line.maxVisibleCharacters = line.textInfo.characterCount;
            settled = true;
        }

        /// <summary>Finishes the animation if it is still going; otherwise closes.</summary>
        public void Continue()
        {
            UiSfx.Play(UiSfx.Cue.Click);
            if (!settled || line.maxVisibleCharacters < line.textInfo.characterCount)
            {
                Settle();
                return;
            }

            Close();
        }

        private void Close()
        {
            Current = null;
            Action closed = onClosed;
            onClosed = null;
            Destroy(gameObject);
            if (closed != null)
            {
                closed();
            }
        }

        private void Update()
        {
            if (sun != null)
            {
                sun.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * SunTurnDegreesPerSecond);
            }

            if (renderedVersion != Loc.Version)
            {
                Render();
                typed = line.textInfo.characterCount;
            }

            // Tadah speaks once the title has landed, at the player's chosen text speed.
            if (settled && line.maxVisibleCharacters < line.textInfo.characterCount)
            {
                float rate = UserPrefs.CharactersPerSecond(UserPrefs.DialogueSpeed);
                typed = rate <= 0f ? line.textInfo.characterCount : typed + (rate * Time.unscaledDeltaTime);
                line.maxVisibleCharacters = Mathf.Min((int)typed, line.textInfo.characterCount);
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && Time.frameCount != openedFrame
                && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
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
