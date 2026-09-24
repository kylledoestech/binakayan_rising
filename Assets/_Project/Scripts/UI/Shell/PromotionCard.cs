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
    /// The card a soldier gets on going up a level: the figure under a turning sun, the new
    /// level, and each stat counting up from what it was to what it is now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the panel's "layout animation after training". The same card follows a drill, a
    /// duplicate Hero recruit and, later, a battle; several level-ups queue and show one after
    /// another.
    /// </para>
    /// <para>
    /// The counting is the point of the animation: it shows the player exactly what the level
    /// bought. Continue works at any moment, so the card never holds up someone who has seen it.
    /// </para>
    /// </remarks>
    public sealed class PromotionCard : MonoBehaviour
    {
        private const float CardWidth = 680f;
        private const float CardHeight = 760f;
        private const float StageSize = 240f;
        private const float SunSize = 232f;
        private const float SunTurnDegreesPerSecond = 14f;
        private const float CountSeconds = 0.7f;
        private const float CountStagger = 0.18f;
        private const float RowStagger = 0.09f;
        private const float RowSlide = 0.22f;
        private const float RowOffset = 48f;

        /// <summary>The body sprite drawn at three screen pixels per art pixel.</summary>
        private const float BodyScale = 3f;

        private static readonly TextKey[] StatLabels = { TextKey.TrnHealth, TextKey.TrnAttack, TextKey.TrnDefense };

        private readonly Queue<LevelUp> queue = new Queue<LevelUp>();
        private readonly TextMeshProUGUI[] statBefore = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] statAfter = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] statGain = new TextMeshProUGUI[3];
        private readonly RectTransform[] statRows = new RectTransform[3];
        private readonly CanvasGroup[] statGroups = new CanvasGroup[3];
        private float[] shownBefore;
        private float[] shownAfter;

        private Action onClosed;
        private RectTransform card;
        private CanvasGroup cardGroup;
        private RectTransform sun;
        private Image body;
        private TextMeshProUGUI unitName;
        private TextMeshProUGUI unitRole;
        private TextMeshProUGUI levelLine;
        private Coroutine playing;
        private int openedFrame;

        /// <summary>The card showing now, or null. For screenshots and tests.</summary>
        public static PromotionCard Current { get; private set; }

        /// <summary>The card's frame, for layout checks.</summary>
        public RectTransform Card
        {
            get { return card; }
        }

        /// <summary>Level-ups still waiting behind the one showing.</summary>
        public int Waiting
        {
            get { return queue.Count; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
        }

        /// <summary>
        /// Shows <paramref name="ups"/> one card at a time, then calls <paramref name="closed"/>.
        /// With nothing to show, calls it at once.
        /// </summary>
        public static PromotionCard Show(IList<LevelUp> ups, Action closed)
        {
            if (ups == null || ups.Count == 0)
            {
                if (closed != null)
                {
                    closed();
                }

                return null;
            }

            // A second promotion while one is showing joins its queue rather than stacking cards.
            if (Current != null)
            {
                for (int i = 0; i < ups.Count; i++)
                {
                    Current.queue.Enqueue(ups[i]);
                }

                Current.onClosed += closed;
                return Current;
            }

            Canvas canvas = UiKit.Screen("Promotion", Theme.Layer.Modal);
            UiKit.EnsureEventSystem();
            var view = canvas.gameObject.AddComponent<PromotionCard>();
            view.Build(canvas.transform);
            for (int i = 0; i < ups.Count; i++)
            {
                view.queue.Enqueue(ups[i]);
            }

            view.onClosed = closed;
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

            TextMeshProUGUI title = UiKit.Display(column, Loc.Get(TextKey.PromoTitle), Theme.Type.Title);
            title.color = Theme.Revolution;
            UiKit.Localize(title, TextKey.PromoTitle);
            UiLayout.OneLine(title, Theme.Type.Title);
            UiLayout.Fix(title.rectTransform, 0f, 48f);

            // The figure stands in a well, under a slowly turning sun.
            RectTransform stageRow = UiKit.Row(column, "Stage Row", 0f, 0f, TextAnchor.MiddleCenter);
            UiLayout.Fix(stageRow, 0f, StageSize);
            RectTransform stage = UiKit.Well(stageRow, "Stage");
            UiLayout.Fix(stage, StageSize, StageSize);

            Image sunImage = UiKit.Sigil(stage, SunSize, Theme.GoldBright);
            sun = (RectTransform)sunImage.transform.parent;
            UiKit.Anchor(sun, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SunSize, SunSize));
            Color glow = Theme.GoldBright;
            glow.a = 0.55f;
            sunImage.color = glow;

            body = UiKit.Icon(stage, null, StageSize, Color.white);
            body.preserveAspect = false;

            unitName = UiKit.Body(column, string.Empty, Theme.Type.Heading + 4f, TextAlignmentOptions.Center);
            unitName.fontStyle = FontStyles.Bold;
            unitName.color = Theme.Revolution;
            UiLayout.OneLine(unitName, Theme.Type.Heading + 4f);
            UiLayout.Fix(unitName.rectTransform, 0f, 38f);

            unitRole = UiKit.Caption(column, string.Empty, TextAlignmentOptions.Center);
            unitRole.fontStyle = FontStyles.Italic;
            UiLayout.OneLine(unitRole, Theme.Type.Body);
            UiLayout.Fix(unitRole.rectTransform, 0f, 26f);

            levelLine = UiKit.Display(column, string.Empty, Theme.Type.Heading);
            levelLine.color = Theme.Ink;
            UiLayout.OneLine(levelLine, Theme.Type.Heading);
            UiLayout.Fix(levelLine.rectTransform, 0f, 40f);

            Image rule = UiKit.Divider(column, 440f);
            UiLayout.Fix(rule.rectTransform, 440f, 16f);

            for (int i = 0; i < StatLabels.Length; i++)
            {
                BuildStatRow(column, i);
            }

            RectTransform buttons = UiKit.Row(column, "Buttons", 0f, 0f, TextAnchor.MiddleCenter);
            UiLayout.Fix(buttons, 0f, 72f);
            UiKit.SealButton(buttons, TextKey.MenuContinue, Continue, 280f, 60f, 0f, "Button Continue");
        }

        private void BuildStatRow(RectTransform parent, int index)
        {
            // The row sits in a holder the layout places; the row itself slides inside it.
            RectTransform holder = UiKit.NewRect(parent, "Stat " + index);
            UiLayout.Fix(holder, 0f, 40f);
            RectTransform row = UiKit.Row(holder, "Row", Theme.Space.Base, 0f, TextAnchor.MiddleCenter);
            UiKit.Stretch(row);
            statRows[index] = row;
            statGroups[index] = UiKit.Group(row.gameObject);

            TextMeshProUGUI label = UiKit.Body(row, Loc.Get(StatLabels[index]), Theme.Type.Body + 2f, TextAlignmentOptions.Left);
            UiKit.Localize(label, StatLabels[index]);
            UiLayout.OneLine(label, Theme.Type.Body + 2f);
            UiLayout.Fix(label.rectTransform, 150f, 36f);

            statBefore[index] = UiKit.Body(row, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Right);
            statBefore[index].color = Theme.InkSoft;
            UiLayout.OneLine(statBefore[index], Theme.Type.Heading);
            UiLayout.Fix(statBefore[index].rectTransform, 80f, 36f);

            TextMeshProUGUI arrow = UiKit.Body(row, "▸", Theme.Type.Heading, TextAlignmentOptions.Center);
            arrow.color = Theme.Gold;
            UiLayout.Fix(arrow.rectTransform, 28f, 36f);

            statAfter[index] = UiKit.Body(row, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Left);
            statAfter[index].fontStyle = FontStyles.Bold;
            UiLayout.OneLine(statAfter[index], Theme.Type.Heading);
            UiLayout.Fix(statAfter[index].rectTransform, 80f, 36f);

            statGain[index] = UiKit.Body(row, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Left);
            statGain[index].color = Theme.Success;
            statGain[index].fontStyle = FontStyles.Bold;
            UiLayout.OneLine(statGain[index], Theme.Type.Body + 2f);
            UiLayout.Fix(statGain[index].rectTransform, 80f, 36f);
        }

        // ------------------------------------------------------------------ playing

        private void Next()
        {
            if (queue.Count == 0)
            {
                Close();
                return;
            }

            LevelUp up = queue.Dequeue();
            openedFrame = Time.frameCount;

            // Stop the last card's tweens and put what they were scaling back at rest, so a
            // quick Continue cannot leave a label caught mid-punch.
            StopAllCoroutines();
            body.transform.parent.localScale = Vector3.one;
            levelLine.transform.localScale = Vector3.one;
            for (int i = 0; i < statAfter.Length; i++)
            {
                statAfter[i].transform.localScale = Vector3.one;
                statRows[i].anchoredPosition = Vector2.zero;
                statGroups[i].alpha = 1f;
            }

            playing = StartCoroutine(Play(up));
        }

        private IEnumerator Play(LevelUp up)
        {
            // An interrupted entrance leaves the card part-way; every card starts from rest.
            card.anchoredPosition = Vector2.zero;
            UnitArchetype archetype = UnitCatalog.Find(up.Archetype);
            unitName.text = archetype != null ? archetype.Name.Get() : up.Archetype;
            unitRole.text = archetype != null
                ? UnitCatalog.RoleLabel(archetype.Role).Get() + " · " + UnitCatalog.RarityLabel(archetype.Rarity).Get()
                : string.Empty;
            levelLine.text = Loc.Format(TextKey.PromoLevel, up.FromLevel, up.ToLevel);

            Sprite figure = Theme.Assets != null ? Theme.Assets.UnitBody(up.Archetype) : null;
            body.sprite = figure;
            body.enabled = figure != null;
            if (figure != null)
            {
                // Whole screen pixels per art pixel, so the figure stays crisp.
                RectTransform frame = (RectTransform)body.transform.parent;
                UiKit.Anchor(frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(figure.rect.width * BodyScale, figure.rect.height * BodyScale));
            }

            float[] before = { up.Before.MaxHP, up.Before.AttackDamage, up.Before.Defense };
            float[] after = { up.After.MaxHP, up.After.AttackDamage, up.After.Defense };
            shownBefore = before;
            shownAfter = after;
            for (int i = 0; i < 3; i++)
            {
                statBefore[i].text = StatNumber(before[i]);
                statAfter[i].text = StatNumber(before[i]);
                statGain[i].text = string.Empty;
                statGroups[i].alpha = 0f;
                statRows[i].anchoredPosition = new Vector2(-RowOffset, 0f);
            }

            levelLine.transform.localScale = Vector3.zero;

            UiSfx.Play(UiSfx.Cue.Victory);
            yield return UiTween.Enter(card, cardGroup, new Vector2(0f, -60f), 0.35f);
            StartCoroutine(UiTween.Punch(body.transform.parent, 0.18f, 0.3f));

            // The new level grows in with a small overshoot.
            float grow = Time.unscaledTime;
            while (Time.unscaledTime - grow < 0.28f)
            {
                float g = (Time.unscaledTime - grow) / 0.28f;
                levelLine.transform.localScale = Vector3.one * (g * (1f + (0.2f * Mathf.Sin(g * Mathf.PI))));
                yield return null;
            }

            levelLine.transform.localScale = Vector3.one;

            // The stat rows slide in one after another, easing out.
            float slide = Time.unscaledTime;
            bool entered = false;
            while (!entered)
            {
                entered = true;
                for (int i = 0; i < 3; i++)
                {
                    float t = Mathf.Clamp01((Time.unscaledTime - slide - (i * RowStagger)) / RowSlide);
                    float eased = 1f - ((1f - t) * (1f - t) * (1f - t));
                    statGroups[i].alpha = eased;
                    statRows[i].anchoredPosition = new Vector2(-RowOffset * (1f - eased), 0f);
                    entered &= t >= 1f;
                }

                yield return null;
            }

            // Each stat counts up in turn, and its gain appears as it lands.
            float start = Time.unscaledTime;
            bool[] landed = new bool[3];
            while (!landed[0] || !landed[1] || !landed[2])
            {
                for (int i = 0; i < 3; i++)
                {
                    if (landed[i])
                    {
                        continue;
                    }

                    float t = Mathf.Clamp01((Time.unscaledTime - start - (i * CountStagger)) / CountSeconds);
                    statAfter[i].text = StatNumber(Mathf.Lerp(before[i], after[i], 1f - ((1f - t) * (1f - t))));
                    if (t >= 1f)
                    {
                        Land(i, before[i], after[i]);
                        landed[i] = true;
                    }
                }

                yield return null;
            }

            playing = null;
        }

        private void Land(int index, float before, float after)
        {
            statAfter[index].text = StatNumber(after);
            float gain = after - before;
            statGain[index].text = gain > 0.001f ? "+" + StatNumber(gain) : string.Empty;
            if (gain > 0.001f)
            {
                StartCoroutine(UiTween.Punch(statAfter[index].transform, 0.2f, 0.2f));
                UiSfx.Play(UiSfx.Cue.Place);
            }
        }

        /// <summary>Stats carry one decimal; whole numbers drop it.</summary>
        internal static string StatNumber(float value)
        {
            float rounded = (float)Math.Round(value, 1);
            return Mathf.Approximately(rounded, Mathf.Round(rounded))
                ? Mathf.RoundToInt(rounded).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : rounded.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Finishes the animation if it is still going; otherwise moves to the next card.</summary>
        public void Continue()
        {
            UiSfx.Play(UiSfx.Cue.Click);
            if (playing != null && Time.frameCount != openedFrame)
            {
                Settle();
                return;
            }

            playing = null;
            Next();
        }

        /// <summary>Everything at rest: the card in place, the rows in, every stat landed.</summary>
        private void Settle()
        {
            StopAllCoroutines();
            playing = null;
            card.anchoredPosition = Vector2.zero;
            cardGroup.alpha = 1f;
            body.transform.parent.localScale = Vector3.one;
            levelLine.transform.localScale = Vector3.one;
            for (int i = 0; i < 3; i++)
            {
                statGroups[i].alpha = 1f;
                statRows[i].anchoredPosition = Vector2.zero;
                statAfter[i].transform.localScale = Vector3.one;
                if (shownBefore != null)
                {
                    statAfter[i].text = StatNumber(shownAfter[i]);
                    float gain = shownAfter[i] - shownBefore[i];
                    statGain[i].text = gain > 0.001f ? "+" + StatNumber(gain) : string.Empty;
                }
            }
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

            // Space and Enter continue, but not on the frame the card opened with that key.
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
