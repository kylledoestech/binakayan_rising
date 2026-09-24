using System;
using System.Collections;
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
    /// The card for a Kapatiran pair's new rank (#19), shown after a battle: the two partners'
    /// portraits under a turning sun, the new rank letter, and what that rank gives them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built on <see cref="RankUpCard"/>'s pattern: one card per pair that ranked up, in Table 3's
    /// order; Continue finishes the animation first, then moves to the next pair, then closes.
    /// </para>
    /// <para>
    /// Rank C carries no stat bonus but opens the pair's lore dialogue (#20), so its card says so
    /// and offers Listen, which plays the dialogue and comes back to the card.
    /// </para>
    /// </remarks>
    public sealed class BondRankCard : MonoBehaviour
    {
        private const float CardWidth = 820f;
        private const float CardHeight = 720f;
        private const float SunSize = 220f;
        private const float PortraitSize = 128f;
        private const float SunTurnDegreesPerSecond = 14f;

        private readonly Queue<BondRankUp> pending = new Queue<BondRankUp>();
        private MetaGame game;
        private Action onClosed;
        private BondRankUp showing;
        private RectTransform card;
        private CanvasGroup cardGroup;
        private RectTransform sun;
        private Image leftFace;
        private Image rightFace;
        private TextMeshProUGUI pairName;
        private TextMeshProUGUI rankLetter;
        private TextMeshProUGUI rankStep;
        private TextMeshProUGUI bonus;
        private TextMeshProUGUI loreLine;
        private Button listen;
        private bool settled;
        private int openedFrame;
        private int renderedVersion = -1;

        /// <summary>The card showing now, or null. For screenshots and tests.</summary>
        public static BondRankCard Current { get; private set; }

        /// <summary>The card's frame, for layout checks.</summary>
        public RectTransform Card
        {
            get { return card; }
        }

        /// <summary>The rank-up on the card now.</summary>
        public BondRankUp Showing
        {
            get { return showing; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
        }

        /// <summary>
        /// Shows a card for each of <paramref name="ups"/> in turn, then calls
        /// <paramref name="closed"/>. With none, calls it at once.
        /// </summary>
        public static BondRankCard Show(MetaGame game, IList<BondRankUp> ups, Action closed)
        {
            if (ups == null || ups.Count == 0 || Current != null)
            {
                if (closed != null)
                {
                    closed();
                }

                return null;
            }

            Canvas canvas = UiKit.Screen("Bond Rank Up", Theme.Layer.Modal);
            UiKit.EnsureEventSystem();
            var view = canvas.gameObject.AddComponent<BondRankCard>();
            view.game = game;
            view.onClosed = closed;
            for (int i = 0; i < ups.Count; i++)
            {
                view.pending.Enqueue(ups[i]);
            }

            view.Build(canvas.transform);
            Current = view;
            view.Next();
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

            TextMeshProUGUI title = UiKit.Display(column, Loc.Get(TextKey.BondTitle), Theme.Type.Title);
            title.color = Theme.Revolution;
            UiKit.Localize(title, TextKey.BondTitle);
            UiLayout.OneLine(title, Theme.Type.Title);
            UiLayout.Fix(title.rectTransform, 0f, 48f);

            // The two partners stand either side of a turning sun with the rank letter on it.
            RectTransform stage = UiKit.NewRect(column, "Stage");
            UiLayout.Fix(stage, 0f, SunSize);

            Image sunImage = UiKit.Sigil(stage, SunSize, Theme.GoldBright);
            sun = (RectTransform)sunImage.transform.parent;
            UiKit.Anchor(sun, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SunSize, SunSize));
            Color glow = Theme.GoldBright;
            glow.a = 0.45f;
            sunImage.color = glow;

            leftFace = Portrait(stage, "Left", -(SunSize * 0.5f + PortraitSize * 0.5f + 12f));
            rightFace = Portrait(stage, "Right", SunSize * 0.5f + PortraitSize * 0.5f + 12f);

            rankLetter = UiKit.Display(stage, string.Empty, Theme.Type.Display * 1.6f, TextAlignmentOptions.Center);
            rankLetter.color = Theme.Revolution;
            rankLetter.raycastTarget = false;
            UiKit.Anchor(rankLetter.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SunSize, SunSize));

            pairName = UiKit.Display(column, string.Empty, Theme.Type.Heading + 8f, TextAlignmentOptions.Center);
            pairName.color = Theme.Revolution;
            UiLayout.OneLine(pairName, Theme.Type.Heading + 8f);
            UiLayout.Fix(pairName.rectTransform, 0f, 44f);

            rankStep = UiKit.Caption(column, string.Empty, TextAlignmentOptions.Center);
            rankStep.fontStyle = FontStyles.UpperCase | FontStyles.Bold;
            UiLayout.OneLine(rankStep, Theme.Type.Small);
            UiLayout.Fix(rankStep.rectTransform, 0f, 22f);

            Image rule = UiKit.Divider(column, 560f);
            UiLayout.Fix(rule.rectTransform, 560f, 16f);

            bonus = UiKit.Body(column, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Center);
            bonus.textWrappingMode = TextWrappingModes.Normal;
            UiLayout.Fix(bonus.rectTransform, 0f, 72f);

            loreLine = UiKit.Body(column, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Center);
            loreLine.fontStyle = FontStyles.Italic;
            loreLine.color = Theme.InkSoft;
            UiLayout.OneLine(loreLine, Theme.Type.Body + 2f);
            UiLayout.Fix(loreLine.rectTransform, 0f, 30f);

            RectTransform buttons = UiKit.Row(column, "Buttons", Theme.Space.Base, 0f, TextAnchor.MiddleCenter);
            UiLayout.Fix(buttons, 0f, 72f);
            listen = UiKit.SealButton(buttons, TextKey.BondListen, Listen, 240f, 60f, 0f, "Button Listen");
            UiLayout.Fix((RectTransform)listen.transform, 240f, 60f);
            Button next = UiKit.SealButton(buttons, TextKey.MenuContinue, Continue, 280f, 60f, 0f, "Button Continue");
            UiLayout.Fix((RectTransform)next.transform, 280f, 60f);
        }

        private static Image Portrait(RectTransform stage, string name, float x)
        {
            RectTransform frame = UiKit.Well(stage, "Portrait " + name);
            UiKit.Anchor(frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(PortraitSize + 16f, PortraitSize + 16f));
            Image face = UiKit.Icon(frame, null, PortraitSize, Color.white);
            UiKit.Anchor((RectTransform)face.transform.parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PortraitSize, PortraitSize));
            return face;
        }

        // ------------------------------------------------------------------ playing

        private void Next()
        {
            showing = pending.Dequeue();
            StopAllCoroutines();
            StartCoroutine(Play());
        }

        private void Render()
        {
            BondPair pair = showing.Pair;
            pairName.text = KapatiranText.PairName(pair);
            rankLetter.text = BondCatalog.Label(showing.To);
            rankStep.text = Loc.Format(TextKey.BondRankStep, BondCatalog.Label(showing.From), BondCatalog.Label(showing.To));
            SetFace(leftFace, pair.ArchetypeA);
            SetFace(rightFace, pair.ArchetypeB);

            string effects = KapatiranText.Bonus(pair.Id, showing.To);
            bonus.text = effects != null ? Loc.Format(TextKey.BondBonus, effects) : Loc.Get(TextKey.BondNoBonus);
            bonus.color = effects != null ? Theme.Ink : Theme.InkSoft;

            LoreDialogue lore = showing.UnlockedLore ? BondLore.For(pair.Id) : null;
            loreLine.gameObject.SetActive(lore != null);
            listen.gameObject.SetActive(lore != null);
            if (lore != null)
            {
                loreLine.text = Loc.Format(TextKey.BondLoreUnlocked, lore.Title.Get());
            }

            renderedVersion = Loc.Version;
        }

        private static void SetFace(Image face, string archetypeId)
        {
            Sprite sprite = Theme.Assets != null ? Theme.Assets.UnitPortrait(archetypeId) : null;
            face.sprite = sprite;
            face.enabled = sprite != null;
        }

        private IEnumerator Play()
        {
            openedFrame = Time.frameCount;
            settled = false;
            Render();
            rankLetter.transform.localScale = Vector3.zero;

            UiSfx.Play(UiSfx.Cue.Victory);
            yield return UiTween.Enter(card, cardGroup, new Vector2(0f, -60f), 0.35f);

            StartCoroutine(UiTween.Punch(leftFace.transform.parent, 0.2f, 0.25f));
            StartCoroutine(UiTween.Punch(rightFace.transform.parent, 0.2f, 0.25f));
            UiSfx.Play(UiSfx.Cue.Place);
            yield return new WaitForSecondsRealtime(0.15f);

            // The letter grows in from nothing, with a small overshoot, as the rank name does.
            float start = Time.unscaledTime;
            const float grow = 0.3f;
            while (Time.unscaledTime - start < grow)
            {
                float t = (Time.unscaledTime - start) / grow;
                float overshoot = 1f + (0.18f * Mathf.Sin(t * Mathf.PI));
                rankLetter.transform.localScale = Vector3.one * (t * overshoot);
                yield return null;
            }

            UiSfx.Play(UiSfx.Cue.Confirm);
            Settle();
        }

        /// <summary>Everything at rest: the card in place, the portraits and the letter full size.</summary>
        private void Settle()
        {
            StopAllCoroutines();
            card.anchoredPosition = Vector2.zero;
            cardGroup.alpha = 1f;
            leftFace.transform.parent.localScale = Vector3.one;
            rightFace.transform.parent.localScale = Vector3.one;
            rankLetter.transform.localScale = Vector3.one;
            settled = true;
        }

        /// <summary>Plays the pair's newly opened lore dialogue, then returns to the card.</summary>
        public void Listen()
        {
            if (showing == null || LorePlayer.Current != null)
            {
                return;
            }

            UiSfx.Play(UiSfx.Cue.Click);
            Settle();
            LorePlayer.Play(BondLore.For(showing.Pair.Id), game, () => openedFrame = Time.frameCount);
        }

        /// <summary>Finishes the animation if it is still going; otherwise the next pair, or closes.</summary>
        public void Continue()
        {
            if (LorePlayer.Current != null)
            {
                return;
            }

            UiSfx.Play(UiSfx.Cue.Click);
            if (!settled)
            {
                Settle();
                return;
            }

            if (pending.Count > 0)
            {
                Next();
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

            if (showing != null && renderedVersion != Loc.Version)
            {
                Render();
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && LorePlayer.Current == null && Time.frameCount != openedFrame
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
