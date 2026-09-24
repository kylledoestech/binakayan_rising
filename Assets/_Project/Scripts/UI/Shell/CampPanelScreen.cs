using System.Collections.Generic;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using BinakayanRising.Gameplay.Flow;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// A screen opened from a building in the encampment: a card over the dimmed camp, under the
    /// same top bar, with a row of tabs and a way back to the camp.
    /// </summary>
    /// <remarks>
    /// The camp stays drawn underneath, so the player never loses the sense of where they are;
    /// the bar stays on top, so the purse visibly changes as they harvest, sell or reforge.
    /// </remarks>
    public abstract class CampPanelScreen : ShellScreen
    {
        protected const float CardWidth = 1360f;

        private const float CardPadding = Theme.Space.FramePadding + 8f;
        private const float HeaderSigil = 44f;
        private const float HeaderClose = 52f;
        private const float TabWidth = 210f;
        protected const float CardHeight = 780f;

        private readonly List<Button> tabs = new List<Button>();
        private CampaignBar bar;
        private RectTransform card;
        private RectTransform body;
        private TextMeshProUGUI title;
        private int selectedTab = -1;

        /// <summary>The card, for screenshots and layout checks.</summary>
        public RectTransform Card
        {
            get { return card; }
        }

        /// <summary>The purse strip over this panel, so a panel can point at a currency.</summary>
        protected CampaignBar Bar
        {
            get { return bar; }
        }

        /// <summary>Where subclasses build their content: the card below its header.</summary>
        protected RectTransform Body
        {
            get { return body; }
        }

        protected int SelectedTab
        {
            get { return selectedTab; }
        }

        protected sealed override void Build()
        {
            UiKit.Scrim(Root);

            card = UiKit.Panel(Root, "Card");
            // Between the bar and the strip a toast uses, so a harvest or sale message never
            // covers the way back.
            float free = CampaignBar.CoveredHeight - UiControls.ToastClearance;
            UiKit.Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -free * 0.5f), new Vector2(CardWidth, CardHeight));

            RectTransform column = UiKit.Column(card, "Column", Theme.Space.Snug, CardPadding, TextAnchor.UpperLeft);
            UiKit.Stretch(column);
            UiLayout.FillWidth(column);

            RectTransform header = UiKit.Row(column, "Header", Theme.Space.Base, 0f, TextAnchor.MiddleLeft);
            UiLayout.Fix(header, 0f, 60f);

            Image sigil = UiKit.Sigil(header, HeaderSigil, Theme.Gold);
            UiLayout.Fix((RectTransform)sigil.transform, HeaderSigil, HeaderSigil);

            title = UiKit.Display(header, string.Empty, Theme.Type.Title, TextAlignmentOptions.Left);
            UiLayout.OneLine(title, Theme.Type.Title);

            RectTransform tabRow = UiKit.Row(header, "Tabs", Theme.Space.Tight, 0f, TextAnchor.MiddleRight);
            UiLayout.Fix(tabRow, 0f, 56f);
            UiLayout.Flexible(tabRow);

            Button close = UiKit.IconButton(header, Theme.IconClose, Back, HeaderClose, "Button Close X");
            UiLayout.Fix((RectTransform)close.transform, HeaderClose, HeaderClose);

            Image rule = UiKit.Divider(column);
            UiLayout.Fix(rule.rectTransform, 0f, 16f);

            body = UiKit.NewRect(column, "Body");
            UiLayout.FlexibleHeight(body);

            RectTransform footer = UiKit.Row(column, "Footer", 0f, 0f, TextAnchor.MiddleCenter);
            UiLayout.Fix(footer, 0f, 64f);
            UiKit.SealButton(footer, TextKey.ResBack, Back, 320f, 60f, 0f, "Button Back To Camp");

            BuildTabs(tabRow);
            UiLayout.Fix(title.rectTransform, TitleWidth(), 52f);
            BuildBody(body);

            // Last, so it draws over the scrim and the card.
            bar = CampaignBar.Create(Root, Shell);
        }

        /// <summary>
        /// The header width the sigil, the tabs and the close button leave, so a long title (the
        /// Filipino ones run long) is not cut short on a panel with few tabs or none.
        /// </summary>
        private float TitleWidth()
        {
            float header = CardWidth - (2f * CardPadding);
            float tabRow = tabs.Count > 0 ? (tabs.Count * TabWidth) + ((tabs.Count - 1) * Theme.Space.Tight) : 0f;
            return header - HeaderSigil - HeaderClose - tabRow - (3f * Theme.Space.Base);
        }

        /// <summary>Adds the tab buttons, with <see cref="AddTab"/>.</summary>
        protected abstract void BuildTabs(RectTransform row);

        /// <summary>Builds the content under the header.</summary>
        protected abstract void BuildBody(RectTransform area);

        /// <summary>Shows tab <paramref name="index"/>'s content and its title.</summary>
        protected abstract void ShowTab(int index);

        /// <summary>The header title for the current tab.</summary>
        protected void SetTitle(string text)
        {
            title.text = text;
        }

        protected Button AddTab(RectTransform row, string label, int index)
        {
            Button tab = UiKit.SealButton(row, label, () => SelectTab(index), TabWidth, 52f, Theme.Type.Body, "Tab " + index);
            UiLayout.Fix((RectTransform)tab.transform, TabWidth, 52f);
            tabs.Add(tab);
            return tab;
        }

        /// <summary>Relabels a tab, after a language change.</summary>
        protected void SetTabLabel(int index, string label)
        {
            tabs[index].GetComponentInChildren<TextMeshProUGUI>().text = label;
        }

        protected void SelectTab(int index)
        {
            if (index != selectedTab && selectedTab >= 0)
            {
                UiSfx.Play(UiSfx.Cue.Toggle);
            }

            selectedTab = index;
            for (int i = 0; i < tabs.Count; i++)
            {
                // The chosen tab keeps the seal's red; the others fade to ink.
                ((Image)tabs[i].targetGraphic).color = i == index ? Theme.Revolution : Theme.InkSoft;
            }

            ShowTab(index);
        }

        protected override void Refresh()
        {
            bar.Bind(Game);
        }

        protected override void OnHidden()
        {
            bar.Bind(null);
        }

        protected virtual void Update()
        {
            if (IsVisible && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame
                && Shell != null && !Shell.SettingsOpen && PromotionCard.Current == null && RecruitReveal.Current == null)
            {
                Back();
                return;
            }

            // Right-click backs out the same way, but only with nothing over the panel: no
            // settings, no card or Library, and no higher canvas (a confirm) under the pointer.
            if (IsVisible && Shell != null && !Shell.SettingsOpen && !Shell.ModalOpen
                && BinakayanRising.Gameplay.UiPointer.TryClaimRightClick(Theme.Layer.Shell))
            {
                Back();
            }
        }

        protected void Back()
        {
            if (IsVisible)
            {
                GoTo(GameState.BaseHub);
            }
        }

        // ------------------------------------------------------------------ shared pieces

        /// <summary>A keeper's portrait, name and role, stacked, as a column of the given width.</summary>
        protected static KeeperCard BuildKeeper(RectTransform parent, float width)
        {
            var keeper = new KeeperCard();
            keeper.Root = UiKit.Column(parent, "Keeper", Theme.Space.Tight, 0f, TextAnchor.UpperCenter);
            UiLayout.Fix(keeper.Root, width, 0f);
            UiLayout.FlexibleHeight(keeper.Root);

            RectTransform frame = UiKit.Well(keeper.Root, "Portrait");
            UiLayout.Fix(frame, 168f, 168f);
            keeper.Portrait = UiKit.Icon(frame, null, 144f, Color.white);
            UiKit.Anchor((RectTransform)keeper.Portrait.transform.parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(144f, 144f));

            keeper.Name = UiKit.Body(keeper.Root, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Center);
            keeper.Name.fontStyle = FontStyles.Bold;
            keeper.Name.color = Theme.Revolution;
            UiLayout.OneLine(keeper.Name, Theme.Type.Heading);
            UiLayout.Fix(keeper.Name.rectTransform, width, 32f);

            keeper.Role = UiKit.Caption(keeper.Root, string.Empty, TextAlignmentOptions.Center);
            keeper.Role.fontStyle = FontStyles.Italic;
            UiLayout.OneLine(keeper.Role, Theme.Type.Small);
            UiLayout.Fix(keeper.Role.rectTransform, width, 24f);

            Image rule = UiKit.Divider(keeper.Root, width - 40f);
            UiLayout.Fix(rule.rectTransform, width - 40f, 16f);

            keeper.Note = UiKit.Body(keeper.Root, string.Empty, Theme.Type.Body, TextAlignmentOptions.Top);
            keeper.Note.textWrappingMode = TextWrappingModes.Normal;
            UiLayout.Fix(keeper.Note.rectTransform, width, 120f);
            return keeper;
        }

        /// <summary>The portrait, name and role of the keeper beside a panel.</summary>
        protected sealed class KeeperCard
        {
            public RectTransform Root;
            public Image Portrait;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Role;
            public TextMeshProUGUI Note;

            public void Show(string character, string note)
            {
                Character who = Characters.Find(character);
                Name.text = who != null ? who.Name.Get() : string.Empty;
                Role.text = who != null ? who.Role.Get() : string.Empty;
                Note.text = note;

                Sprite face = Theme.Assets != null ? Theme.Assets.UnitPortrait(character) : null;
                Portrait.sprite = face;
                Portrait.enabled = face != null;
            }
        }
    }
}
