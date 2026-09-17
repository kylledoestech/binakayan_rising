using System;
using System.Collections.Generic;
using BinakayanRising.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BinakayanRising.UI.Kit
{
    /// <summary>
    /// Dims the screen except for the things being pointed at, rings them, and shows an explanatory
    /// card beside them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A view with no opinions about what it teaches. The tutorial director decides which rectangles
    /// to light, which accept clicks, and what the card says; this class only draws that and keeps
    /// it in place.
    /// </para>
    /// <para>
    /// Targets are supplied as functions returning a screen rectangle, re-read every frame. A HUD
    /// row moves when its panel scrolls and the deployment zone moves when the camera zooms, so a
    /// rectangle captured once at the start of a step would drift off its target.
    /// </para>
    /// <para>
    /// <b>Card placement.</b> The card tries the right of the first target, then its left, below
    /// and above, then the screen corners, and takes the first spot that covers no target. When
    /// everything overlaps — the whole board is lit, say — it takes the spot that covers least.
    /// </para>
    /// </remarks>
    [AddComponentMenu("")]
    public sealed class SpotlightOverlay : MonoBehaviour
    {
        private const float CardWidth = 500f;
        private const float HolePadding = 8f;
        private const float RingOutset = 6f;
        private const float CardGap = 20f;
        private const float EdgeMargin = 16f;

        private readonly List<Func<Rect>> targets = new List<Func<Rect>>();
        private readonly List<bool> targetPassThrough = new List<bool>();
        private readonly List<Rect> screenHoles = new List<Rect>();
        private readonly List<Rect> canvasHoles = new List<Rect>();
        private readonly List<Image> rings = new List<Image>();
        private readonly List<Vector2> candidates = new List<Vector2>();

        private Canvas canvas;
        private RectTransform root;
        private SpotlightScrim scrim;

        private RectTransform card;
        private TextMeshProUGUI stepLabel;
        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI bodyLabel;
        private RectTransform languageRow;
        private TextMeshProUGUI englishLabel;
        private TextMeshProUGUI filipinoLabel;
        private Button primaryButton;
        private TextMeshProUGUI primaryLabel;
        private Button skipButton;
        private TextMeshProUGUI skipLabel;
        private UnityAction primaryAction;
        private UnityAction skipAction;

        private bool centred;
        private bool cardVisible = true;

        /// <summary>Whether the overlay is showing.</summary>
        public bool IsVisible => root != null && root.gameObject.activeSelf;

        /// <summary>The card, for tests and for measuring where it landed.</summary>
        public RectTransform Card => card;

        /// <summary>The scrim, for raycast checks.</summary>
        public SpotlightScrim Scrim => scrim;

        /// <summary>Creates a hidden overlay on its own canvas.</summary>
        public static SpotlightOverlay Create(Transform parent, string name, int sortOrder)
        {
            Canvas canvas = UiKit.Screen(name, sortOrder);
            if (parent != null)
            {
                canvas.transform.SetParent(parent, worldPositionStays: false);
            }

            var overlay = canvas.gameObject.AddComponent<SpotlightOverlay>();
            overlay.Build(canvas);
            overlay.SetVisible(false);
            return overlay;
        }

        /// <summary>Shows or hides the whole overlay.</summary>
        public void SetVisible(bool visible)
        {
            if (root != null && root.gameObject.activeSelf != visible)
            {
                root.gameObject.SetActive(visible);
            }
        }

        /// <summary>Removes every target.</summary>
        public void ClearTargets()
        {
            targets.Clear();
            targetPassThrough.Clear();
        }

        /// <summary>Lights a rectangle, given in screen pixels, recomputed every frame.</summary>
        /// <param name="screenRect">Returns the target; an empty rectangle lights nothing this frame.</param>
        /// <param name="passThrough">Whether clicks inside the hole reach what is underneath.</param>
        public void AddTarget(Func<Rect> screenRect, bool passThrough)
        {
            if (screenRect == null)
            {
                return;
            }

            targets.Add(screenRect);
            targetPassThrough.Add(passThrough);
        }

        /// <summary>Fills in the card.</summary>
        /// <param name="step">Small caption above the title, e.g. "Step 2 of 9".</param>
        /// <param name="primary">Caption of the main button; null hides it, for steps that advance on an action.</param>
        /// <param name="skip">Caption of the skip button; null hides it.</param>
        /// <param name="centre">Centre the card on screen regardless of targets.</param>
        /// <param name="showLanguages">Offer the English and Filipino buttons.</param>
        public void SetCard(
            string step, string title, string body,
            string primary, UnityAction onPrimary,
            string skip, UnityAction onSkip,
            bool centre, bool showLanguages)
        {
            stepLabel.text = step ?? string.Empty;
            titleLabel.text = title ?? string.Empty;
            bodyLabel.text = body ?? string.Empty;

            primaryAction = onPrimary;
            primaryButton.gameObject.SetActive(!string.IsNullOrEmpty(primary));
            primaryLabel.text = primary ?? string.Empty;

            skipAction = onSkip;
            skipButton.gameObject.SetActive(!string.IsNullOrEmpty(skip));
            skipLabel.text = skip ?? string.Empty;

            languageRow.gameObject.SetActive(showLanguages);
            centred = centre;

            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
        }

        /// <summary>Hides the card while keeping the scrim, or shows it again.</summary>
        public void SetCardVisible(bool visible)
        {
            cardVisible = visible;
            if (card != null)
            {
                card.gameObject.SetActive(visible);
            }
        }

        // ------------------------------------------------------------------ build

        private void Build(Canvas owner)
        {
            canvas = owner;

            root = UiKit.NewRect(canvas.transform, "Spotlight");
            UiKit.Stretch(root);

            var scrimObject = UiKit.NewRect(root, "Scrim");
            UiKit.Stretch(scrimObject);
            // Added explicitly: the scrim was observed in play mode with no CanvasRenderer, and a
            // graphic without one draws nothing and reports no error.
            if (scrimObject.GetComponent<CanvasRenderer>() == null)
            {
                scrimObject.gameObject.AddComponent<CanvasRenderer>();
            }

            scrim = scrimObject.gameObject.AddComponent<SpotlightScrim>();
            scrim.color = Theme.SpotlightScrim;
            scrim.raycastTarget = true;

            card = UiKit.Panel(root, "Card");
            card.anchorMin = Vector2.zero;
            card.anchorMax = Vector2.zero;
            card.pivot = Vector2.zero;
            card.sizeDelta = new Vector2(CardWidth, 200f);

            RectTransform column = UiKit.Column(card, "Body", Theme.Space.Snug, Theme.Space.FramePadding, TextAnchor.UpperLeft);
            UiKit.Stretch(column);
            column.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = true;

            // The card's height follows its text, so a long Filipino body grows the card rather
            // than overflowing it.
            var cardLayout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;
            var fitter = card.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // The panel's own fill layers must not be laid out as rows of the card.
            for (int i = 0; i < card.childCount; i++)
            {
                Transform child = card.GetChild(i);
                if (child != column)
                {
                    child.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                }
            }

            stepLabel = UiKit.Caption(column, string.Empty, TextAlignmentOptions.Left);
            stepLabel.color = Theme.RevolutionDark;
            stepLabel.fontStyle = FontStyles.UpperCase;

            titleLabel = UiKit.Display(column, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Left);
            titleLabel.color = Theme.Revolution;

            bodyLabel = UiKit.Body(column, string.Empty, Theme.Type.Body, TextAlignmentOptions.TopLeft);
            bodyLabel.lineSpacing = 6f;

            languageRow = UiKit.Row(column, "Languages", Theme.Space.Snug, 0f, TextAnchor.MiddleCenter);
            Button english = UiKit.SealButton(languageRow, "English", () => UserPrefs.ChooseLanguage(Language.English), 180f, 48f, Theme.Type.Small, "Button English");
            englishLabel = english.GetComponentInChildren<TextMeshProUGUI>();
            Button filipino = UiKit.SealButton(languageRow, "Filipino", () => UserPrefs.ChooseLanguage(Language.Filipino), 180f, 48f, Theme.Type.Small, "Button Filipino");
            filipinoLabel = filipino.GetComponentInChildren<TextMeshProUGUI>();

            RectTransform actions = UiKit.Row(column, "Actions", Theme.Space.Snug, 0f, TextAnchor.MiddleRight);
            actions.GetComponent<HorizontalLayoutGroup>().padding.top = (int)Theme.Space.Tight;

            skipButton = UiKit.SealButton(actions, "Skip", () => skipAction?.Invoke(), 210f, 44f, Theme.Type.Small, "Button Skip");
            skipLabel = skipButton.GetComponentInChildren<TextMeshProUGUI>();
            skipButton.GetComponentInChildren<Image>().color = Theme.InkSoft;

            primaryButton = UiKit.SealButton(actions, "Next", () => primaryAction?.Invoke(), 160f, 52f, Theme.Type.Body, "Button Primary");
            primaryLabel = primaryButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        // ------------------------------------------------------------------ per-frame

        private void LateUpdate()
        {
            Refresh();
        }

        /// <summary>
        /// Recomputes holes, rings and card placement now instead of at the end of the frame, so a
        /// step's new hole blocks and passes clicks from the moment the step is entered.
        /// </summary>
        public void Refresh()
        {
            if (!IsVisible)
            {
                return;
            }

            float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;

            screenHoles.Clear();
            canvasHoles.Clear();
            for (int i = 0; i < targets.Count; i++)
            {
                Rect target = targets[i]();
                if (target.width <= 0f || target.height <= 0f)
                {
                    target = Rect.zero;
                }
                else
                {
                    float pad = HolePadding * scale;
                    target = Rect.MinMaxRect(target.xMin - pad, target.yMin - pad, target.xMax + pad, target.yMax + pad);
                }

                screenHoles.Add(target);
                canvasHoles.Add(new Rect(target.x / scale, target.y / scale, target.width / scale, target.height / scale));
            }

            scrim.SetHoles(screenHoles, targetPassThrough, scale);
            UpdateRings();
            UpdateLanguageButtons();

            if (cardVisible)
            {
                PlaceCard();
            }
        }

        private void UpdateRings()
        {
            while (rings.Count < canvasHoles.Count)
            {
                RectTransform ringRect = UiKit.NewRect(root, "Ring");
                ringRect.anchorMin = Vector2.zero;
                ringRect.anchorMax = Vector2.zero;
                ringRect.pivot = Vector2.zero;
                Image ring = ringRect.gameObject.AddComponent<Image>();
                ring.sprite = Theme.FrameHollow;
                ring.type = Image.Type.Sliced;
                ring.raycastTarget = false;
                rings.Add(ring);

                // Under the card, over the scrim.
                ringRect.SetSiblingIndex(scrim.transform.GetSiblingIndex() + 1);
            }

            float pulse = 0.55f + (0.45f * (0.5f + (0.5f * Mathf.Sin(Time.unscaledTime * 4f))));
            Color tint = Theme.SpotlightRing;
            tint.a = pulse;

            for (int i = 0; i < rings.Count; i++)
            {
                bool used = i < canvasHoles.Count && canvasHoles[i].width > 0f;
                Image ring = rings[i];
                if (ring.gameObject.activeSelf != used)
                {
                    ring.gameObject.SetActive(used);
                }

                if (!used)
                {
                    continue;
                }

                Rect hole = canvasHoles[i];
                RectTransform rect = ring.rectTransform;
                rect.anchoredPosition = new Vector2(hole.x - RingOutset, hole.y - RingOutset);
                rect.sizeDelta = new Vector2(hole.width + (2f * RingOutset), hole.height + (2f * RingOutset));
                ring.color = tint;
            }
        }

        private void UpdateLanguageButtons()
        {
            if (!languageRow.gameObject.activeSelf)
            {
                return;
            }

            englishLabel.color = Loc.Current == Language.English ? Theme.GoldBright : Theme.Parchment;
            filipinoLabel.color = Loc.Current == Language.Filipino ? Theme.GoldBright : Theme.Parchment;
        }

        private void PlaceCard()
        {
            Rect area = root.rect;
            Vector2 size = card.rect.size;
            float minX = EdgeMargin;
            float minY = EdgeMargin;
            float maxX = Mathf.Max(minX, area.width - size.x - EdgeMargin);
            float maxY = Mathf.Max(minY, area.height - size.y - EdgeMargin);

            Rect primary = Rect.zero;
            for (int i = 0; i < canvasHoles.Count; i++)
            {
                if (canvasHoles[i].width > 0f)
                {
                    primary = canvasHoles[i];
                    break;
                }
            }

            if (centred || primary.width <= 0f)
            {
                card.anchoredPosition = new Vector2(
                    Mathf.Round((area.width - size.x) * 0.5f),
                    Mathf.Round((area.height - size.y) * 0.5f));
                return;
            }

            candidates.Clear();
            float alongY = primary.center.y - (size.y * 0.5f);
            float alongX = primary.center.x - (size.x * 0.5f);
            candidates.Add(new Vector2(primary.xMax + CardGap, alongY));
            candidates.Add(new Vector2(primary.xMin - CardGap - size.x, alongY));
            candidates.Add(new Vector2(alongX, primary.yMin - CardGap - size.y));
            candidates.Add(new Vector2(alongX, primary.yMax + CardGap));
            candidates.Add(new Vector2(maxX, minY));
            candidates.Add(new Vector2(minX, minY));
            candidates.Add(new Vector2(maxX, maxY));
            candidates.Add(new Vector2(minX, maxY));

            // Beside the target and fully on screen wins outright. Clamping a side placement back on
            // screen can drag the card over neighbouring HUD (a hole in the top bar would pull a
            // side card up over the rest of the bar), so clamped spots only compete on overlap.
            for (int c = 0; c < 4; c++)
            {
                Vector2 position = candidates[c];
                bool onScreen = position.x >= minX && position.x <= maxX && position.y >= minY && position.y <= maxY;
                if (onScreen && OverlapWithHoles(new Rect(position, size)) <= 0f)
                {
                    card.anchoredPosition = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
                    return;
                }
            }

            Vector2 best = candidates[0];
            float bestOverlap = float.MaxValue;
            for (int c = 0; c < candidates.Count; c++)
            {
                Vector2 position = new Vector2(
                    Mathf.Clamp(candidates[c].x, minX, maxX),
                    Mathf.Clamp(candidates[c].y, minY, maxY));

                float overlap = OverlapWithHoles(new Rect(position, size));
                if (overlap < bestOverlap - 0.5f)
                {
                    bestOverlap = overlap;
                    best = position;
                    if (overlap <= 0f)
                    {
                        break;
                    }
                }
            }

            card.anchoredPosition = new Vector2(Mathf.Round(best.x), Mathf.Round(best.y));
        }

        private float OverlapWithHoles(Rect rect)
        {
            float total = 0f;
            for (int i = 0; i < canvasHoles.Count; i++)
            {
                Rect hole = canvasHoles[i];
                float width = Mathf.Min(rect.xMax, hole.xMax) - Mathf.Max(rect.xMin, hole.xMin);
                float height = Mathf.Min(rect.yMax, hole.yMax) - Mathf.Max(rect.yMin, hole.yMin);
                if (width > 0f && height > 0f)
                {
                    total += width * height;
                }
            }

            return total;
        }
    }
}
