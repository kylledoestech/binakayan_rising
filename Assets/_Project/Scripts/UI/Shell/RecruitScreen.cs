using System.Collections.Generic;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using BinakayanRising.Core.Meta;
using BinakayanRising.Gameplay.Flow;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// The Recruitment Hall: recruit one unit or ten for Reales, with the odds, the pity counter
    /// and the duplicate rule printed beside the buttons.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rates are read from <see cref="MetaRules"/> and shown as percentages, with every unit
    /// each tier can give, before anything is spent. That is the whole of the design stance on
    /// recruiting in an educational game: nothing about it is hidden.
    /// </para>
    /// <para>
    /// The recruits are revealed on <see cref="RecruitReveal"/>; a Hero recruited twice trains
    /// the one already in the roster, and any level that buys follows on a
    /// <see cref="PromotionCard"/>.
    /// </para>
    /// </remarks>
    public sealed class RecruitScreen : CampPanelScreen
    {
        private const float RatesWidth = 440f;

        /// <summary>Tier portraits: 24 art pixels at two screen pixels each.</summary>
        private const float TierFace = 48f;

        /// <summary>The line-up of bodies: 48x64 art pixels at two screen pixels each.</summary>
        private const float LineupScale = 2f;

        private const float OfferButtonHeight = 72f;
        private const float OfferCostHeight = 30f;
        private const float OfferNoteHeight = 24f;

        /// <summary>Button, cost and saving note stacked, with the column's two gaps between.</summary>
        private const float OfferHeight = OfferButtonHeight + OfferCostHeight + OfferNoteHeight + (2f * Theme.Space.Tight);

        private static readonly UnitRarity[] Tiers = { UnitRarity.Hero, UnitRarity.Rare, UnitRarity.Common };

        private readonly TextMeshProUGUI[] tierLabels = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] tierChances = new TextMeshProUGUI[3];
        private TextMeshProUGUI ratesHeading;
        private TextMeshProUGUI pityLine;
        private BarView pityBar;
        private TextMeshProUGUI duplicateLine;
        private TextMeshProUGUI introLine;
        private TextMeshProUGUI rosterLine;

        private Button recruitOne;
        private Button recruitMany;
        private TextMeshProUGUI oneCost;
        private TextMeshProUGUI manyCost;
        private TextMeshProUGUI manySaving;

        private MetaGame bound;
        private bool stale = true;
        private int renderedVersion = -1;

        public override GameState State
        {
            get { return GameState.HeroSummoning; }
        }

        protected override void BuildTabs(RectTransform row)
        {
        }

        protected override void BuildBody(RectTransform area)
        {
            RectTransform row = UiKit.Row(area, "Row", Theme.Space.Wide, 0f, TextAnchor.UpperLeft);
            UiKit.Stretch(row);

            BuildRates(row);
            BuildHall(row);
        }

        private void BuildRates(RectTransform parent)
        {
            RectTransform well = UiKit.Well(parent, "Rates");
            UiLayout.Fix(well, RatesWidth, 0f);
            UiLayout.FlexibleHeight(well);

            RectTransform column = UiKit.Column(well, "Column", Theme.Space.Tight, 0f, TextAnchor.UpperLeft);
            UiKit.Stretch(column, Theme.Space.Base);
            UiLayout.FillWidth(column);

            ratesHeading = UiKit.Body(column, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Left);
            ratesHeading.fontStyle = FontStyles.Bold;
            UiLayout.OneLine(ratesHeading, Theme.Type.Heading);
            UiLayout.Fix(ratesHeading.rectTransform, 0f, 32f);

            for (int i = 0; i < Tiers.Length; i++)
            {
                RectTransform head = UiKit.Row(column, "Tier " + Tiers[i], Theme.Space.Base, 0f, TextAnchor.MiddleLeft);
                UiLayout.Fix(head, 0f, 28f);

                tierLabels[i] = UiKit.Body(head, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Left);
                tierLabels[i].fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                tierLabels[i].color = Theme.Rarity.Of(Tiers[i]);
                UiLayout.OneLine(tierLabels[i], Theme.Type.Body + 2f);
                UiLayout.Flexible(tierLabels[i].rectTransform);
                UiLayout.Fix(tierLabels[i].rectTransform, 0f, 28f);

                tierChances[i] = UiKit.Body(head, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Right);
                tierChances[i].fontStyle = FontStyles.Bold;
                tierChances[i].color = Theme.Rarity.Of(Tiers[i]);
                UiLayout.OneLine(tierChances[i], Theme.Type.Heading);
                UiLayout.Fix(tierChances[i].rectTransform, 100f, 28f);

                // Every unit the tier can give, by face.
                RectTransform faces = UiKit.Row(column, "Faces " + Tiers[i], Theme.Space.Hair, 0f, TextAnchor.MiddleLeft);
                UiLayout.Fix(faces, 0f, TierFace + 8f);
                foreach (UnitArchetype archetype in UnitCatalog.Recruitable(Tiers[i]))
                {
                    RectTransform frame = UiKit.Well(faces, "Face " + archetype.Id);
                    UiLayout.Fix(frame, TierFace + 8f, TierFace + 8f);
                    Sprite face = Theme.Assets != null ? Theme.Assets.UnitPortrait(archetype.Id) : null;
                    Image image = UiKit.Icon(frame, face, TierFace, Color.white);
                    UiKit.Anchor((RectTransform)image.transform.parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TierFace, TierFace));
                    image.enabled = face != null;
                }
            }

            Image rule = UiKit.Divider(column);
            UiLayout.Fix(rule.rectTransform, 0f, 16f);

            pityLine = UiKit.Body(column, string.Empty, Theme.Type.Body, TextAlignmentOptions.TopLeft);
            pityLine.textWrappingMode = TextWrappingModes.Normal;
            UiLayout.Fix(pityLine.rectTransform, 0f, 48f);

            pityBar = UiKit.Bar(column, 0f, 16f, Theme.Rarity.Hero);
            UiLayout.Fix((RectTransform)pityBar.transform, 0f, 16f);

            duplicateLine = UiKit.Caption(column, string.Empty, TextAlignmentOptions.TopLeft);
            duplicateLine.fontStyle = FontStyles.Italic;
            duplicateLine.textWrappingMode = TextWrappingModes.Normal;
            UiLayout.Fix(duplicateLine.rectTransform, 0f, 48f);
        }

        private void BuildHall(RectTransform parent)
        {
            RectTransform column = UiKit.Column(parent, "Hall", Theme.Space.Base, 0f, TextAnchor.UpperCenter);
            UiLayout.Flexible(column);
            UiLayout.FlexibleHeight(column);
            UiLayout.FillWidth(column);

            // The whole army that can answer the call, standing in a line.
            RectTransform lineup = UiKit.Row(column, "Lineup", Theme.Space.Hair, 0f, TextAnchor.LowerCenter);
            UiLayout.Fix(lineup, 0f, (64f * LineupScale) + 28f);
            foreach (UnitArchetype archetype in UnitCatalog.All)
            {
                if (archetype.Team != Core.Combat.Team.Katipunan)
                {
                    continue;
                }

                RectTransform slot = UiKit.Column(lineup, "Recruit " + archetype.Id, 0f, 0f, TextAnchor.LowerCenter);
                UiLayout.Fix(slot, 48f * LineupScale, (64f * LineupScale) + 28f);

                Sprite figure = Theme.Assets != null ? Theme.Assets.UnitBody(archetype.Id) : null;
                Image body = UiKit.Icon(slot, figure, 48f * LineupScale, Color.white);
                body.enabled = figure != null;
                UiLayout.Fix((RectTransform)body.transform.parent, 48f * LineupScale, 64f * LineupScale);

                TextMeshProUGUI name = UiKit.Caption(slot, archetype.ShortName, TextAlignmentOptions.Center);
                name.fontStyle = FontStyles.Bold;
                name.color = Theme.Rarity.Of(archetype.Rarity);
                UiLayout.OneLine(name, Theme.Type.Small);
                UiLayout.Fix(name.rectTransform, 48f * LineupScale, 24f);
            }

            Image rule = UiKit.Divider(column, 640f);
            UiLayout.Fix(rule.rectTransform, 640f, 16f);

            introLine = UiKit.Body(column, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Center);
            UiLayout.OneLine(introLine, Theme.Type.Body + 2f);
            UiLayout.Fix(introLine.rectTransform, 0f, 28f);

            rosterLine = UiKit.Caption(column, string.Empty, TextAlignmentOptions.Center);
            UiLayout.OneLine(rosterLine, Theme.Type.Body);
            UiLayout.Fix(rosterLine.rectTransform, 0f, 26f);

            RectTransform offers = UiKit.Row(column, "Offers", Theme.Space.Huge, 0f, TextAnchor.UpperCenter);
            UiLayout.Fix(offers, 0f, OfferHeight);
            TextMeshProUGUI noSaving;
            recruitOne = BuildOffer(offers, "Button Recruit One", () => Recruit(1), out oneCost, out noSaving);
            noSaving.gameObject.SetActive(false);
            recruitMany = BuildOffer(offers, "Button Recruit Many", () => Recruit(Game != null ? Game.Rules.MultiPullCount : 10), out manyCost, out manySaving);
        }

        private static Button BuildOffer(RectTransform parent, string name, UnityEngine.Events.UnityAction onClick,
            out TextMeshProUGUI cost, out TextMeshProUGUI note)
        {
            RectTransform column = UiKit.Column(parent, name + " Offer", Theme.Space.Tight, 0f, TextAnchor.UpperCenter);
            UiLayout.Fix(column, 300f, OfferHeight);

            Button button = UiKit.SealButton(column, string.Empty, onClick, 300f, OfferButtonHeight, 0f, name);
            UiLayout.Fix((RectTransform)button.transform, 300f, OfferButtonHeight);

            RectTransform costRow = UiKit.Row(column, "Cost", Theme.Space.Tight, 0f, TextAnchor.MiddleCenter);
            UiLayout.Fix(costRow, 300f, OfferCostHeight);
            Image coin = UiKit.Icon(costRow, Theme.IconReales, 24f, Color.white);
            UiLayout.Fix((RectTransform)coin.transform.parent, 24f, 24f);
            cost = UiKit.Body(costRow, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Left);
            cost.fontStyle = FontStyles.Bold;
            UiLayout.OneLine(cost, Theme.Type.Body + 2f);
            UiLayout.Fix(cost.rectTransform, 160f, OfferCostHeight);

            note = UiKit.Caption(column, string.Empty, TextAlignmentOptions.Center);
            note.color = Theme.Success;
            note.fontStyle = FontStyles.Bold;
            UiLayout.OneLine(note, Theme.Type.Body);
            UiLayout.Fix(note.rectTransform, 300f, OfferNoteHeight);
            return button;
        }

        // ------------------------------------------------------------------ showing

        protected override void Refresh()
        {
            base.Refresh();
            Bind(Game);
            ShowTab(0);
        }

        protected override void OnHidden()
        {
            base.OnHidden();
            Bind(null);
        }

        private void OnDestroy()
        {
            Bind(null);
        }

        private void Bind(MetaGame game)
        {
            if (bound != null)
            {
                bound.Changed -= MarkStale;
            }

            bound = game;
            if (bound != null)
            {
                bound.Changed += MarkStale;
            }

            stale = true;
        }

        private void MarkStale()
        {
            stale = true;
        }

        protected override void ShowTab(int index)
        {
            stale = true;
            Redraw();
        }

        protected override void Update()
        {
            base.Update();
            if (IsVisible && (stale || renderedVersion != Loc.Version))
            {
                Redraw();
            }
        }

        private void Redraw()
        {
            MetaGame game = Game;
            if (game == null)
            {
                return;
            }

            stale = false;
            renderedVersion = Loc.Version;
            SetTitle(Encampment.Site(Places.Recruitment).Name.Get());

            ratesHeading.text = Loc.Get(TextKey.RecRates);
            for (int i = 0; i < Tiers.Length; i++)
            {
                tierLabels[i].text = UnitCatalog.RarityLabel(Tiers[i]).Get();
                tierChances[i].text = Percent(game.RarityChance(Tiers[i]));
            }

            int left = game.PullsToPity;
            pityLine.text = left <= 1 ? Loc.Get(TextKey.RecPityNext) : Loc.Format(TextKey.RecPity, left);
            pityBar.Value = 1f - ((left - 1f) / Mathf.Max(1f, game.Rules.PityThreshold - 1f));
            duplicateLine.text = Loc.Format(TextKey.RecDuplicate, game.Rules.DuplicateHeroXp);

            introLine.text = Loc.Get(TextKey.RecIntro);
            rosterLine.text = Loc.Format(TextKey.RecRoster, game.Units.Count);

            int many = game.Rules.MultiPullCount;
            SetLabel(recruitOne, Loc.Get(TextKey.RecOne));
            SetLabel(recruitMany, Loc.Format(TextKey.RecTen, many));
            int costOne = game.PullCost(1);
            int costMany = game.PullCost(many);
            oneCost.text = Loc.Format(TextKey.RecCost, costOne);
            manyCost.text = Loc.Format(TextKey.RecCost, costMany);
            int saving = (costOne * many) - costMany;
            manySaving.text = saving > 0 ? Loc.Format(TextKey.RecSave, saving) : string.Empty;

            oneCost.color = game.Balance(Currency.Reales) >= costOne ? Theme.Ink : Theme.Danger;
            manyCost.color = game.Balance(Currency.Reales) >= costMany ? Theme.Ink : Theme.Danger;
            recruitOne.interactable = game.Balance(Currency.Reales) >= costOne;
            recruitMany.interactable = game.Balance(Currency.Reales) >= costMany;
        }

        private static void SetLabel(Button button, string text)
        {
            button.GetComponentInChildren<TextMeshProUGUI>().text = text;
        }

        /// <summary>Whole percentages, with one decimal only when the rate needs it.</summary>
        private static string Percent(float chance)
        {
            float percent = chance * 100f;
            return Mathf.Approximately(percent, Mathf.Round(percent))
                ? Mathf.RoundToInt(percent) + "%"
                : percent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }

        // ------------------------------------------------------------------ recruiting

        private void Recruit(int count)
        {
            if (Game == null || RecruitReveal.Current != null)
            {
                return;
            }

            List<PullResult> results = Game.TryPull(count);
            if (results == null)
            {
                UiSfx.Play(UiSfx.Cue.Error);
                UiControls.Toast(Loc.Get(TextKey.RecCantAfford));
                return;
            }

            UiSfx.Play(UiSfx.Cue.Confirm);
            RecruitReveal.Show(results, () =>
            {
                var ups = new List<LevelUp>();
                for (int i = 0; i < results.Count; i++)
                {
                    if (results[i].LevelUp != null)
                    {
                        ups.Add(results[i].LevelUp);
                    }
                }

                PromotionCard.Show(ups, null);
            });
        }
    }
}
