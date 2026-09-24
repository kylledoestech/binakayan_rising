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
    /// tuning a rate changes the screen and nothing else. Each Exchange line has a stepper — a
    /// count of lots from one to all the purse holds, a Max button and a preview of the trade —
    /// and one Sell button that makes the whole trade at once.
    /// </remarks>
    public sealed class ResourceScreen : CampPanelScreen
    {
        private const int FarmTab = 0;
        private const int MineTab = 1;
        private const int ExchangeTab = 2;
        private const float KeeperWidth = 360f;

        /// <summary>The stepper row: 48 + 96 + 48 + 92 and three gaps of 8.</summary>
        private const float ControlsWidth = 308f;

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
        private readonly TextMeshProUGUI[] previewLabels = new TextMeshProUGUI[2];
        private readonly TextMeshProUGUI[] lotCounts = new TextMeshProUGUI[2];
        private readonly Button[] fewer = new Button[2];
        private readonly Button[] more = new Button[2];
        private readonly Button[] most = new Button[2];
        private readonly Button[] sell = new Button[2];

        /// <summary>The stepper's count per line, before clamping to what the purse holds.</summary>
        private readonly int[] lots = { 1, 1 };

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

                previewLabels[i] = UiKit.Body(text, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Left);
                previewLabels[i].fontStyle = FontStyles.Bold;
                UiLayout.OneLine(previewLabels[i], Theme.Type.Body + 2f);
                UiLayout.Fix(previewLabels[i].rectTransform, 0f, 30f);

                // [-] N lots [+] [Max] over one wide Sell.
                int line = i;
                RectTransform controls = UiKit.Column(row, "Controls", Theme.Space.Tight, 0f, TextAnchor.MiddleRight);
                UiLayout.Fix(controls, ControlsWidth, 112f);

                RectTransform stepper = UiKit.Row(controls, "Stepper", Theme.Space.Tight, 0f, TextAnchor.MiddleLeft);
                UiLayout.Fix(stepper, ControlsWidth, 52f);
                fewer[i] = UiKit.SealButton(stepper, "–", () => Step(line, -1), 48f, 48f, Theme.Type.Heading, "Button Lots Fewer " + goods);
                UiLayout.Fix((RectTransform)fewer[i].transform, 48f, 48f);
                lotCounts[i] = UiKit.Body(stepper, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Center);
                lotCounts[i].fontStyle = FontStyles.Bold;
                UiLayout.OneLine(lotCounts[i], Theme.Type.Body + 2f);
                UiLayout.Fix(lotCounts[i].rectTransform, 96f, 48f);
                more[i] = UiKit.SealButton(stepper, "+", () => Step(line, 1), 48f, 48f, Theme.Type.Heading, "Button Lots More " + goods);
                UiLayout.Fix((RectTransform)more[i].transform, 48f, 48f);
                most[i] = UiKit.SealButton(stepper, TextKey.ExMax, () => Max(line), 92f, 48f, Theme.Type.Body, "Button Lots Max " + goods);
                UiLayout.Fix((RectTransform)most[i].transform, 92f, 48f);

                sell[i] = UiKit.SealButton(controls, TextKey.ExSell, () => Sell(line), ControlsWidth, 52f, Theme.Type.Body, "Button Sell " + goods);
                UiLayout.Fix((RectTransform)sell[i].transform, ControlsWidth, 52f);
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
                int sellable = game.SellableLots(Goods[i]);
                string goodsName = CurrencyName(Goods[i]);

                bool can = sellable > 0;
                int count = game.ClampLots(Goods[i], lots[i]);
                lots[i] = count;

                lotLabels[i].text = rate != null ? Loc.Format(TextKey.ExLot, rate.LotSize, goodsName, rate.RealesPerLot) : goodsName;
                haveLabels[i].text = Loc.Format(TextKey.ExHave, have + " " + goodsName);
                lotCounts[i].text = count == 1 ? Loc.Get(TextKey.ExLotsOne) : Loc.Format(TextKey.ExLots, count);
                lotCounts[i].color = can ? Theme.Ink : Theme.InkSoft;
                previewLabels[i].text = can && rate != null
                    ? Loc.Format(TextKey.ExPreview, count * rate.LotSize, goodsName, count * rate.RealesPerLot)
                    : Loc.Get(TextKey.ExShort);
                previewLabels[i].color = can ? Theme.Revolution : Theme.Danger;

                fewer[i].interactable = can && count > 1;
                more[i].interactable = can && count < sellable;
                most[i].interactable = can && count < sellable;
                sell[i].interactable = can;
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

        private void Step(int line, int by)
        {
            if (Game == null)
            {
                return;
            }

            lots[line] = Game.ClampLots(Goods[line], lots[line] + by);
            UiSfx.Play(UiSfx.Cue.Toggle);
        }

        private void Max(int line)
        {
            if (Game == null)
            {
                return;
            }

            lots[line] = Game.ClampLots(Goods[line], int.MaxValue);
            UiSfx.Play(UiSfx.Cue.Toggle);
        }

        /// <summary>Sells the stepper's count in one trade, then sets the stepper back to one lot.</summary>
        private void Sell(int line)
        {
            if (Game == null)
            {
                return;
            }

            Currency goods = Goods[line];
            int reales = Game.Exchange(goods, Game.ClampLots(goods, lots[line]));
            if (reales <= 0)
            {
                UiSfx.Play(UiSfx.Cue.Error);
                UiControls.Toast(Loc.Get(TextKey.ExShort));
                return;
            }

            lots[line] = 1;
            UiSfx.Play(UiSfx.Cue.Confirm);
            UiControls.Toast(Loc.Format(TextKey.ExSold, reales));
        }

        /// <summary>Sets a line's stepper, clamped. For the screenshot autopilot.</summary>
        public void SetLots(Currency goods, int count)
        {
            int line = System.Array.IndexOf(Goods, goods);
            if (line >= 0 && Game != null)
            {
                lots[line] = Game.ClampLots(goods, count);
            }
        }
    }
}
