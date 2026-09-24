using BinakayanRising.Core.Localization;
using BinakayanRising.Core.Meta;
using System.Collections;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// The strip across the top of every encampment screen: the player's rank on the left, the
    /// purse on the right, and the current objective hanging under the left end.
    /// </summary>
    /// <remarks>
    /// Redraws from <see cref="MetaGame.Changed"/> rather than every frame. The objective is the
    /// first half of the navigation guide; the pointer on the target building is the second.
    /// </remarks>
    public sealed class CampaignBar : MonoBehaviour
    {
        public const float Height = 88f;

        public const float ObjectiveHeight = 58f;

        /// <summary>How far down from the top of the canvas the bar and the objective reach.</summary>
        public const float CoveredHeight = Height + Theme.Space.Snug + ObjectiveHeight;

        private GameShell shell;
        private MetaGame bound;

        private TextMeshProUGUI rankTitle;
        /// <summary>How long a purse counter takes to roll to its new value.</summary>
        private const float TickDuration = 0.4f;

        /// <summary>How long the "not enough" red flash takes to fade back.</summary>
        private const float FlashDuration = 1.2f;

        private Purse reales;
        private Purse rations;
        private Purse scrap;
        private TextMeshProUGUI objective;
        private RectTransform objectiveRoot;
        private int renderedVersion = -1;
        private bool stale = true;

        /// <summary>The objective line's rect, for the navigation pointer and screenshots.</summary>
        public RectTransform ObjectiveRect
        {
            get { return objectiveRoot; }
        }

        public static CampaignBar Create(RectTransform parent, GameShell shell)
        {
            RectTransform root = UiKit.NewRect(parent, "Campaign Bar");
            UiKit.Stretch(root);
            var bar = root.gameObject.AddComponent<CampaignBar>();
            bar.shell = shell;
            bar.Build(root);
            return bar;
        }

        private void Build(RectTransform root)
        {
            RectTransform strip = UiKit.Panel(root, "Strip");
            strip.anchorMin = new Vector2(0f, 1f);
            strip.anchorMax = new Vector2(1f, 1f);
            strip.pivot = new Vector2(0.5f, 1f);
            strip.anchoredPosition = Vector2.zero;
            strip.sizeDelta = new Vector2(0f, Height);

            RectTransform row = UiKit.Row(strip, "Row", Theme.Space.Base, 0f, TextAnchor.MiddleLeft);
            UiKit.Stretch(row);
            row.offsetMin = new Vector2(Theme.Space.FramePadding, 10f);
            row.offsetMax = new Vector2(-Theme.Space.FramePadding, -10f);

            // Rank.
            Image sigil = UiKit.Sigil(row, 52f, Theme.Gold);
            UiLayout.Fix((RectTransform)sigil.transform, 52f, 52f);

            RectTransform rankColumn = UiKit.Column(row, "Rank", 0f, 0f, TextAnchor.MiddleLeft);
            UiLayout.Fix(rankColumn, 300f, 64f);
            UiLayout.FillWidth(rankColumn);

            TextMeshProUGUI rankLabel = UiKit.Caption(rankColumn, Loc.Get(TextKey.HubRank), TextAlignmentOptions.BottomLeft);
            rankLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            UiKit.Localize(rankLabel, TextKey.HubRank);
            UiLayout.Fix(rankLabel.rectTransform, 0f, 22f);

            rankTitle = UiKit.Display(rankColumn, string.Empty, Theme.Type.Title, TextAlignmentOptions.Left);
            rankTitle.color = Theme.Revolution;
            UiLayout.OneLine(rankTitle, Theme.Type.Title);
            UiLayout.Fix(rankTitle.rectTransform, 0f, 40f);

            RectTransform spacer = UiKit.NewRect(row, "Spacer");
            UiLayout.Flexible(spacer);

            // Purse.
            reales = Chip(row, Theme.IconReales, TextKey.CurReales);
            rations = Chip(row, Theme.IconRations, TextKey.CurRations);
            scrap = Chip(row, Theme.IconScrap, TextKey.CurScrap);

            RectTransform gap = UiKit.NewRect(row, "Gap");
            UiLayout.Fix(gap, Theme.Space.Base, 10f);

            Button settings = UiKit.IconButton(row, Theme.IconSettings, () => shell.OpenSettings(), 56f, "Button Settings");
            UiLayout.Fix((RectTransform)settings.transform, 56f, 56f);

            UiKit.SealButton(row, TextKey.HubToTitle, () => shell.ReturnToTitle(), 250f, 56f, Theme.Type.Small + 2f, "Button Main Menu");

            // Objective, hanging under the strip's left end.
            objectiveRoot = UiKit.Well(root, "Objective");
            UiKit.Anchor(objectiveRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Theme.Space.Wide, -(Height + Theme.Space.Snug)), new Vector2(900f, ObjectiveHeight));

            RectTransform objectiveRow = UiKit.Row(objectiveRoot, "Row", Theme.Space.Snug, 0f, TextAnchor.MiddleLeft);
            UiKit.Stretch(objectiveRow);
            objectiveRow.offsetMin = new Vector2(Theme.Space.Base, 0f);
            objectiveRow.offsetMax = new Vector2(-Theme.Space.Base, 0f);

            Image star = UiKit.Icon(objectiveRow, Theme.IconStar != null ? Theme.IconStar : Theme.Sigil, 28f, Theme.Gold);
            UiLayout.Fix((RectTransform)star.transform, 28f, 28f);

            TextMeshProUGUI objectiveLabel = UiKit.Caption(objectiveRow, Loc.Get(TextKey.HubObjective), TextAlignmentOptions.Left);
            objectiveLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            objectiveLabel.color = Theme.Revolution;
            UiKit.Localize(objectiveLabel, TextKey.HubObjective);
            UiLayout.OneLine(objectiveLabel, Theme.Type.Small);
            UiLayout.Fix(objectiveLabel.rectTransform, 110f, 30f);

            objective = UiKit.Body(objectiveRow, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Left);
            UiLayout.OneLine(objective, Theme.Type.Body + 2f);
            UiLayout.Flexible(objective.rectTransform);
        }

        private static Purse Chip(RectTransform row, Sprite icon, TextKey name)
        {
            RectTransform chip = UiKit.Well(row, "Chip " + name);
            Image fill = chip.GetChild(0).GetComponent<Image>();
            UiLayout.Fix(chip, 200f, 56f);

            RectTransform inner = UiKit.Row(chip, "Row", Theme.Space.Tight, 0f, TextAnchor.MiddleLeft);
            UiKit.Stretch(inner);
            inner.offsetMin = new Vector2(Theme.Space.Snug, 0f);
            inner.offsetMax = new Vector2(-Theme.Space.Snug, 0f);

            if (icon != null)
            {
                Image image = UiKit.Icon(inner, icon, 32f, Color.white);
                UiLayout.Fix((RectTransform)image.transform, 32f, 32f);
            }

            TextMeshProUGUI value = UiKit.Body(inner, "0", Theme.Type.Heading, TextAlignmentOptions.Left);
            value.fontStyle = FontStyles.Bold;
            UiLayout.OneLine(value, Theme.Type.Heading);
            UiLayout.Fix(value.rectTransform, 70f, 40f);

            TextMeshProUGUI label = UiKit.Caption(inner, Loc.Get(name), TextAlignmentOptions.Left);
            UiKit.Localize(label, name);
            UiLayout.OneLine(label, Theme.Type.Small);
            UiLayout.Flexible(label.rectTransform);
            return new Purse(chip, fill, value);
        }

        /// <summary>
        /// Flashes a purse chip red, for when an action costs more of that currency than the
        /// player holds.
        /// </summary>
        public void FlashShort(Currency currency)
        {
            Purse purse = PurseFor(currency);
            if (purse == null || !isActiveAndEnabled)
            {
                return;
            }

            if (purse.Flash != null)
            {
                StopCoroutine(purse.Flash);
            }

            purse.Flash = StartCoroutine(FlashRoutine(purse));
            Punch(purse, 0.1f);
        }

        private Purse PurseFor(Currency currency)
        {
            switch (currency)
            {
                case Currency.Reales: return reales;
                case Currency.Rations: return rations;
                case Currency.Scrap: return scrap;
                default: return null;
            }
        }

        private IEnumerator FlashRoutine(Purse purse)
        {
            float elapsed = 0f;
            while (elapsed < FlashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / FlashDuration);

                // Three quick pulses that fade out, so it reads as a warning rather than a state.
                float pulse = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f)) * (1f - (0.6f * t));
                // Ink on a red fill goes muddy, so the number lifts to parchment as the fill reddens.
                purse.Fill.color = Color.Lerp(purse.FillColor, Theme.Danger, pulse * 0.85f);
                purse.Value.color = Color.Lerp(purse.ValueColor, Theme.Parchment, pulse);
                yield return null;
            }

            purse.Fill.color = purse.FillColor;
            purse.Value.color = purse.ValueColor;
            purse.Flash = null;
        }

        /// <summary>Rolls a purse's counter to its balance, or snaps it there on first sight.</summary>
        private void Show(Purse purse, int balance)
        {
            if (!purse.HasShown || !isActiveAndEnabled)
            {
                purse.HasShown = true;
                purse.Target = balance;
                purse.Shown = balance;
                purse.Value.SetText("{0}", balance);
                return;
            }

            if (balance == purse.Target)
            {
                return;
            }

            int from = Mathf.RoundToInt(purse.Shown);
            purse.Target = balance;
            if (purse.Tick != null)
            {
                StopCoroutine(purse.Tick);
            }

            purse.Tick = StartCoroutine(TickRoutine(purse, from, balance));
            Punch(purse, 0.08f);

            if (purse == reales && balance > from)
            {
                UiSfx.Play(UiSfx.Cue.Coin);
            }
        }

        private IEnumerator TickRoutine(Purse purse, int from, int to)
        {
            float elapsed = 0f;
            while (elapsed < TickDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / TickDuration);
                float inverted = 1f - t;
                float eased = 1f - (inverted * inverted * inverted);
                purse.Shown = Mathf.Lerp(from, to, eased);
                purse.Value.SetText("{0}", Mathf.RoundToInt(purse.Shown));
                yield return null;
            }

            purse.Shown = to;
            purse.Value.SetText("{0}", to);
            purse.Tick = null;
        }

        private void Punch(Purse purse, float strength)
        {
            // Stop any running punch first and reset the scale, or the next one captures a
            // mid-pulse scale as its base and the chip creeps larger.
            if (purse.Punch != null)
            {
                StopCoroutine(purse.Punch);
            }

            purse.Chip.localScale = Vector3.one;
            purse.Punch = StartCoroutine(UiTween.Punch(purse.Chip, strength, 0.22f));
        }

        /// <summary>Starts following a campaign. Null detaches.</summary>
        public void Bind(MetaGame game)
        {
            if (bound != null)
            {
                bound.Changed -= MarkStale;
            }

            bound = game;
            ResetPurse(reales);
            ResetPurse(rations);
            ResetPurse(scrap);
            if (bound != null)
            {
                bound.Changed += MarkStale;
            }

            stale = true;
        }

        private void ResetPurse(Purse purse)
        {
            if (purse == null)
            {
                return;
            }

            // A new campaign's balance is a fresh start, not a gain: snap to it silently.
            purse.HasShown = false;
            if (purse.Tick != null)
            {
                StopCoroutine(purse.Tick);
                purse.Tick = null;
            }
        }

        private void OnDisable()
        {
            // Coroutines die with the behaviour; land every animation on its end state so a
            // hidden bar comes back clean.
            foreach (Purse purse in new[] { reales, rations, scrap })
            {
                if (purse == null)
                {
                    continue;
                }

                purse.Tick = null;
                purse.Punch = null;
                purse.Flash = null;
                purse.Shown = purse.Target;
                purse.Value.SetText("{0}", purse.Target);
                purse.Chip.localScale = Vector3.one;
                purse.Fill.color = purse.FillColor;
                purse.Value.color = purse.ValueColor;
            }
        }

        private void MarkStale()
        {
            stale = true;
        }

        private void OnDestroy()
        {
            Bind(null);
        }

        private void LateUpdate()
        {
            if (bound == null || (!stale && renderedVersion == Loc.Version))
            {
                return;
            }

            stale = false;
            renderedVersion = Loc.Version;

            rankTitle.text = bound.Rank.Title;
            Show(reales, bound.Balance(Currency.Reales));
            Show(rations, bound.Balance(Currency.Rations));
            Show(scrap, bound.Balance(Currency.Scrap));

            Objective next = bound.CurrentObjective;
            objective.text = next != null ? next.Text.Get() : string.Empty;
        }

        /// <summary>One purse chip and the state of its counter animation.</summary>
        private sealed class Purse
        {
            public readonly RectTransform Chip;
            public readonly Image Fill;
            public readonly TextMeshProUGUI Value;
            public readonly Color FillColor;
            public readonly Color ValueColor;

            public bool HasShown;
            public int Target;
            public float Shown;
            public Coroutine Tick;
            public Coroutine Punch;
            public Coroutine Flash;

            public Purse(RectTransform chip, Image fill, TextMeshProUGUI value)
            {
                Chip = chip;
                Fill = fill;
                Value = value;
                FillColor = fill != null ? fill.color : Color.white;
                ValueColor = value.color;
            }
        }
    }
}
