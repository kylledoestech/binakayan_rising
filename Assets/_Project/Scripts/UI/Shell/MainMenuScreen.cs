using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using BinakayanRising.Core.Meta;
using BinakayanRising.Gameplay.Flow;
using BinakayanRising.Gameplay.Meta;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// The title screen: Continue, New Campaign, Settings, Quit.
    /// </summary>
    /// <remarks>
    /// Continue carries a one-line summary of the save under it — rank, current mission, time
    /// played — so a player can tell at a glance whose campaign it is before opening it.
    /// </remarks>
    public sealed class MainMenuScreen : ShellScreen
    {
        private const float CardWidth = 540f;
        private const float ButtonHeight = 68f;
        private const float SummaryHeight = 28f;
        private const float ButtonWidth = 420f;

        private Button continueButton;
        private TextMeshProUGUI summary;
        private int renderedVersion = -1;

        public override GameState State
        {
            get { return GameState.MainMenu; }
        }

        protected override void Build()
        {
            Image backdrop = UiKit.NewRect(Root, "Backdrop").gameObject.AddComponent<Image>();
            backdrop.color = Theme.Backdrop;
            backdrop.raycastTarget = false;
            UiKit.Stretch(backdrop.rectTransform);

            // A large, faint sun behind everything: the one mark that says whose side this is.
            Image watermark = UiKit.Sigil(Root, 900f, new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.06f));
            UiKit.Anchor((RectTransform)watermark.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(900f, 900f));

            // Title block.
            RectTransform titleBlock = UiKit.Column(Root, "Title", Theme.Space.Tight, 0f, TextAnchor.UpperCenter);
            UiKit.Anchor(titleBlock, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1400f, 250f));
            UiLayout.FillWidth(titleBlock);

            Image sigil = UiKit.Sigil(titleBlock, 96f, Theme.Gold);
            UiLayout.Fix((RectTransform)sigil.transform, 96f, 96f);

            TextMeshProUGUI title = UiKit.Display(titleBlock, "Binakayan Rising", 84f);
            UiLayout.OneLine(title, 84f);
            UiLayout.Fix(title.rectTransform, 0f, 100f);

            TextMeshProUGUI tagline = UiKit.Body(titleBlock, Loc.Get(TextKey.MenuTagline), Theme.Type.Heading, TextAlignmentOptions.Center);
            tagline.color = Theme.ParchmentDeep;
            tagline.fontStyle = FontStyles.Italic;
            UiKit.Localize(tagline, TextKey.MenuTagline);
            UiLayout.Fix(tagline.rectTransform, 0f, 40f);

            // Menu card.
            // Sized to its contents: four buttons, the save summary, and the gaps between them.
            const float padding = Theme.Space.FramePadding + 12f;
            float cardHeight = padding * 2f + ButtonHeight * 4f + SummaryHeight + Theme.Space.Snug * 4f;
            RectTransform card = UiKit.Panel(Root, "Card");
            UiKit.Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(CardWidth, cardHeight));

            RectTransform column = UiKit.Column(card, "Buttons", Theme.Space.Snug, padding, TextAnchor.UpperCenter);
            UiKit.Stretch(column);

            continueButton = UiKit.SealButton(column, TextKey.MenuContinue, () => Shell.ContinueCampaign(), ButtonWidth, ButtonHeight, 0f, "Button Continue");

            summary = UiKit.Caption(column, string.Empty, TextAlignmentOptions.Center);
            summary.color = Theme.Ink;
            UiLayout.OneLine(summary, Theme.Type.Body);
            UiLayout.Fix(summary.rectTransform, ButtonWidth, SummaryHeight);

            UiKit.SealButton(column, TextKey.MenuNewCampaign, OnNewCampaign, ButtonWidth, ButtonHeight, 0f, "Button New Campaign");
            UiKit.SealButton(column, TextKey.MenuSettings, () => Shell.OpenSettings(), ButtonWidth, ButtonHeight, 0f, "Button Settings");
            UiKit.SealButton(column, TextKey.MenuQuit, () => Shell.Quit(), ButtonWidth, ButtonHeight, 0f, "Button Quit");

            TextMeshProUGUI version = UiKit.Caption(Root, "v" + Application.version, TextAlignmentOptions.BottomLeft);
            version.color = Theme.InkSoft;
            UiKit.Anchor(version.rectTransform, Vector2.zero, Vector2.zero, new Vector2(Theme.Space.Wide, Theme.Space.Base), new Vector2(300f, 24f));
        }

        protected override void Refresh()
        {
            MetaGame saved = Shell.Session.Peek();
            continueButton.interactable = saved != null;

            if (saved == null)
            {
                summary.text = Loc.Get(TextKey.SetNoSave);
                summary.color = Theme.InkSoft;
            }
            else
            {
                Quest quest = saved.CurrentQuest;
                string where = quest != null
                    ? quest.Title.Get()
                    : Campaign.Level(Campaign.LevelCount).Title.Get();
                summary.text = Loc.Format(
                    TextKey.MenuSaveSummary,
                    saved.Rank.Title,
                    where,
                    GameSession.FormatPlayTime(saved.Data.playSeconds));
                summary.color = Theme.Ink;
            }

            renderedVersion = Loc.Version;
        }

        private void LateUpdate()
        {
            // The summary is composed text, not a bound key, so a language switch re-reads it.
            if (IsVisible && renderedVersion != Loc.Version)
            {
                Refresh();
            }
        }

        private void OnNewCampaign()
        {
            if (!Shell.Session.HasSave)
            {
                Shell.StartNewCampaign();
                return;
            }

            UiControls.Confirm(TextKey.MenuOverwriteTitle, TextKey.MenuOverwriteBody, TextKey.CommonConfirm, () => Shell.StartNewCampaign());
        }
    }
}
