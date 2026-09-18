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
    /// The Farm, the Mine and the Exchange, one tab each: harvest what the farm and mine have
    /// made, and sell it for Reales.
    /// </summary>
    /// <remarks>
    /// This is the panel's "harvest purpose — conversion to coins": the two producing buildings
    /// fill a store over real time, and the Exchange turns what they make into the coin that
    /// recruits and drills soldiers. Every number shown comes from <see cref="MetaRules"/>, so
    /// tuning a rate changes the screen and nothing else.
    /// </remarks>
    public sealed class ResourceScreen : CampPanelScreen
    {
        private const int FarmTab = 0;
        private const int MineTab = 1;
        private const int ExchangeTab = 2;
        private const float KeeperWidth = 360f;

        private static readonly string[] TabPlaces = { Places.Farm, Places.Mine, Places.Exchange };
        private static readonly Currency[] Goods = { Currency.Rations, Currency.Scrap };

        private KeeperCard keeper;
        private RectTransform producer;
        private RectTransform exchange;

        private Image producerIcon;
        private TextMeshProUGUI storedLabel;
        private TextMeshProUGUI storedValue;
        private BarView progress;
        private TextMeshProUGUI nextLabel;
        private TextMeshProUGUI rateLabel;
        private Button harvest;
        private TextMeshProUGUI harvestNote;

        private readonly TextMeshProUGUI[] lotLabels = new TextMeshProUGUI[2];
        private readonly TextMeshProUGUI[] haveLabels = new TextMeshProUGUI[2];
        private readonly Button[] sellOne = new Button[2];
        private readonly Button[] sellAll = new Button[2];

        private int shownStored = -1;
        private int shownNext = -1;
        private int renderedVersion = -1;

        public override GameState State
        {
            get { return GameState.ResourceManagement; }
        }

        /// <summary>The Harvest button, for the screenshot autopilot.</summary>
        public Button HarvestButton
        {
            get { return harvest; }
        }

        protected override void BuildTabs(RectTransform row)
        {
            for (int i = 0; i < TabPlaces.Length; i++)
            {
                AddTab(row, Encampment.Site(TabPlaces[i]).Name.Get(), i);
            }
        }

        protected override void BuildBody(RectTransform area)
        {
            RectTransform row = UiKit.Row(area, "Row", Theme.Space.Huge, 0f, TextAnchor.UpperLeft);
            UiKit.Stretch(row);

            keeper = BuildKeeper(row, KeeperWidth);

            RectTransform content = UiKit.NewRect(row, "Content");
            UiLayout.Flexible(content);
            UiLayout.FlexibleHeight(content);

            producer = BuildProducer(content);
            exchange = BuildExchange(content);
        }

        private RectTransform BuildProducer(RectTransform parent)
        {
            RectTransform column = UiKit.Column(parent, "Producer", Theme.Space.Base, 0f, TextAnchor.UpperLeft);
            UiKit.Stretch(column);
            UiLayout.FillWidth(column);

            RectTransform stock = UiKit.Well(column, "Stock");
            UiLayout.Fix(stock, 0f, 150f);
            RectTransform stockRow = UiKit.Row(stock, "Row", Theme.Space.Wide, 0f, TextAnchor.MiddleLeft);
            UiKit.Stretch(stockRow, Theme.Space.Wide);

            producerIcon = UiKit.Icon(stockRow, null, 96f, Color.white);
            UiLayout.Fix((RectTransform)producerIcon.transform.parent, 96f, 96f);

            RectTransform numbers = UiKit.Column(stockRow, "Numbers", 0f, 0f, TextAnchor.MiddleLeft);
            UiLayout.Flexible(numbers);
            UiLayout.FillWidth(numbers);

            storedLabel = UiKit.Caption(numbers, string.Empty, TextAlignmentOptions.BottomLeft);
            storedLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            UiLayout.OneLine(storedLabel, Theme.Type.Small);
            UiLayout.Fix(storedLabel.rectTransform, 0f, 26f);

            storedValue = UiKit.Display(numbers, "0", Theme.Type.Display, TextAlignmentOptions.Left);
            storedValue.color = Theme.Revolution;
            UiLayout.OneLine(storedValue, Theme.Type.Display);
            UiLayout.Fix(storedValue.rectTransform, 0f, 72f);

            progress = UiKit.Bar(column, 0f, 30f, Theme.Gold);
            UiLayout.Fix((RectTransform)progress.transform, 0f, 30f);

            nextLabel = UiKit.Body(column, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Left);
            UiLayout.OneLine(nextLabel, Theme.Type.Body + 2f);
            UiLayout.Fix(nextLabel.rectTransform, 0f, 28f);

            rateLabel = UiKit.Caption(column, string.Empty, TextAlignmentOptions.Left);
            UiLayout.OneLine(rateLabel, Theme.Type.Body);
            UiLayout.Fix(rateLabel.rectTransform, 0f, 26f);

            RectTransform actions = UiKit.Row(column, "Actions", Theme.Space.Wide, 0f, TextAnchor.MiddleLeft);
            UiLayout.Fix(actions, 0f, 80f);
            harvest = UiKit.SealButton(actions, TextKey.ResHarvest, Harvest, 320f, 72f, 0f, "Button Harvest");
            UiLayout.Fix((RectTransform)harvest.transform, 320f, 72f);

            harvestNote = UiKit.Caption(actions, string.Empty, TextAlignmentOptions.Left);
            harvestNote.fontStyle = FontStyles.Italic;
            UiLayout.OneLine(harvestNote, Theme.Type.Body);
            UiLayout.Flexible(harvestNote.rectTransform);
            UiLayout.Fix(harvestNote.rectTransform, 0f, 40f);
            return column;
        }

        private RectTransform BuildExchange(RectTransform parent)
        {
            RectTransform column = UiKit.Column(parent, "Exchange", Theme.Space.Base, 0f, TextAnchor.UpperLeft);
            UiKit.Stretch(column);
            UiLayout.FillWidth(column);

            for (int i = 0; i < Goods.Length; i++)
            {
                Currency goods = Goods[i];
                RectTransform lot = UiKit.Well(column, "Lot " + goods);
                UiLayout.Fix(lot, 0f, 168f);
                RectTransform row = UiKit.Row(lot, "Row", Theme.Space.Wide, 0f, TextAnchor.MiddleLeft);
                UiKit.Stretch(row, Theme.Space.Wide);

                Image icon = UiKit.Icon(row, goods == Currency.Rations ? Theme.IconRations : Theme.IconScrap, 80f, Color.white);
                UiLayout.Fix((RectTransform)icon.transform.parent, 80f, 80f);

                RectTransform text = UiKit.Column(row, "Text", Theme.Space.Hair, 0f, TextAnchor.MiddleLeft);
                UiLayout.Flexible(text);
                UiLayout.FillWidth(text);

                lotLabels[i] = UiKit.Body(text, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Left);
                lotLabels[i].fontStyle = FontStyles.Bold;
                UiLayout.OneLine(lotLabels[i], Theme.Type.Heading);
                UiLayout.Fix(lotLabels[i].rectTransform, 0f, 34f);

                haveLabels[i] = UiKit.Caption(text, string.Empty, TextAlignmentOptions.Left);
                UiLayout.OneLine(haveLabels[i], Theme.Type.Body);
                UiLayout.Fix(haveLabels[i].rectTransform, 0f, 28f);

                RectTransform buttons = UiKit.Column(row, "Buttons", Theme.Space.Tight, 0f, TextAnchor.MiddleRight);
                UiLayout.Fix(buttons, 230f, 112f);
                sellOne[i] = UiKit.SealButton(buttons, TextKey.ExSellOne, () => Sell(goods, false), 230f, 52f, Theme.Type.Body, "Button Sell One " + goods);
                UiLayout.Fix((RectTransform)sellOne[i].transform, 230f, 52f);
                sellAll[i] = UiKit.SealButton(buttons, TextKey.ExSellAll, () => Sell(goods, true), 230f, 52f, Theme.Type.Body, "Button Sell All " + goods);
                UiLayout.Fix((RectTransform)sellAll[i].transform, 230f, 52f);
            }

            return column;
        }

        // ------------------------------------------------------------------ showing

        protected override void Refresh()
        {
            base.Refresh();
            int tab = System.Array.IndexOf(TabPlaces, Shell.VisitingPlace);
            SelectTab(tab < 0 ? FarmTab : tab);
        }

        protected override void ShowTab(int index)
        {
            CampSite site = Encampment.Site(TabPlaces[index]);
            SetTitle(site.Name.Get());
            keeper.Show(site.Keeper, site.Purpose.Get());
            Shell.VisitingPlace = site.Place;

            producer.gameObject.SetActive(index != ExchangeTab);
            exchange.gameObject.SetActive(index == ExchangeTab);
            shownStored = -1;
            shownNext = -1;
            renderedVersion = -1;
            Redraw();
        }

        protected override void Update()
        {
            base.Update();
            if (IsVisible && Game != null && SelectedTab >= 0)
            {
                Redraw();
            }
        }

        private Facility CurrentFacility
        {
            get { return SelectedTab == MineTab ? Facility.Mine : Facility.Farm; }
        }

        private void Redraw()
        {
            MetaGame game = Game;
            if (game == null)
            {
                return;
            }

            bool relabel = renderedVersion != Loc.Version;
            renderedVersion = Loc.Version;
            if (relabel)
            {
                for (int i = 0; i < TabPlaces.Length; i++)
                {
                    SetTabLabel(i, Encampment.Site(TabPlaces[i]).Name.Get());
                }

                CampSite site = Encampment.Site(TabPlaces[SelectedTab]);
                SetTitle(site.Name.Get());
                keeper.Show(site.Keeper, site.Purpose.Get());
            }

            if (SelectedTab == ExchangeTab)
            {
                RedrawExchange(game, relabel);
                return;
            }

            Facility facility = CurrentFacility;
            FacilityRule rule = game.Rules.For(facility);
            int stored = game.Stored(facility);
            int next = game.SecondsToNext(facility);
            progress.Value = game.Progress(facility);

            if (!relabel && stored == shownStored && next == shownNext)
            {
                return;
            }

            shownStored = stored;
            shownNext = next;

            string goodsName = CurrencyName(rule.Produces);
            producerIcon.sprite = rule.Produces == Currency.Rations ? Theme.IconRations : Theme.IconScrap;
            storedLabel.text = Loc.Get(TextKey.ResStored) + " · " + goodsName;
            storedValue.SetText("{0} / {1}", stored, rule.StorageCap);
            nextLabel.text = game.IsFull(facility) ? Loc.Get(TextKey.ResFull) : Loc.Format(TextKey.ResNext, next);
            nextLabel.color = game.IsFull(facility) ? Theme.Danger : Theme.Ink;
            rateLabel.text = Loc.Format(TextKey.ResRate, rule.SecondsPerUnit, rule.StorageCap);

            harvest.interactable = stored > 0;
            harvestNote.text = stored > 0 ? string.Empty : Loc.Get(TextKey.ResNothing);
        }

        private void RedrawExchange(MetaGame game, bool relabel)
        {
            for (int i = 0; i < Goods.Length; i++)
            {
                ExchangeRate rate = game.Rules.RateFor(Goods[i]);
                int have = game.Balance(Goods[i]);
                int lots = game.SellableLots(Goods[i]);
                string goodsName = CurrencyName(Goods[i]);

                lotLabels[i].text = rate != null ? Loc.Format(TextKey.ExLot, rate.LotSize, goodsName, rate.RealesPerLot) : goodsName;
                haveLabels[i].text = Loc.Format(TextKey.ExHave, have + " " + goodsName)
                    + (lots > 0 ? string.Empty : "   " + Loc.Get(TextKey.ExShort));
                sellOne[i].interactable = lots > 0;
                sellAll[i].interactable = lots > 0;
            }
        }

        private static string CurrencyName(Currency currency)
        {
            switch (currency)
            {
                case Currency.Rations: return Loc.Get(TextKey.CurRations);
                case Currency.Scrap: return Loc.Get(TextKey.CurScrap);
                default: return Loc.Get(TextKey.CurReales);
            }
        }

        // ------------------------------------------------------------------ actions

        private void Harvest()
        {
            if (Game == null)
            {
                return;
            }

            Facility facility = CurrentFacility;
            int got = Game.Harvest(facility);
            if (got <= 0)
            {
                UiSfx.Play(UiSfx.Cue.Error);
                UiControls.Toast(Loc.Get(TextKey.ResNothing));
                return;
            }

            UiSfx.Play(UiSfx.Cue.Confirm);
            UiControls.Toast(Loc.Format(TextKey.ResGot, got, CurrencyName(Game.Rules.For(facility).Produces)));
            CoroutineHost.Run(UiTween.Punch(storedValue.transform));
            shownStored = -1;
        }

        private void Sell(Currency goods, bool everything)
        {
            if (Game == null)
            {
                return;
            }

            int lots = everything ? Game.SellableLots(goods) : 1;
            int reales = Game.Exchange(goods, lots);
            if (reales <= 0)
            {
                UiSfx.Play(UiSfx.Cue.Error);
                UiControls.Toast(Loc.Get(TextKey.ExShort));
                return;
            }

            UiSfx.Play(UiSfx.Cue.Confirm);
            UiControls.Toast(Loc.Format(TextKey.ExSold, reales));
        }
    }
}
