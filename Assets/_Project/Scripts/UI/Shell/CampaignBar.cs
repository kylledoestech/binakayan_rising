using BinakayanRising.Core.Localization;
using BinakayanRising.Core.Meta;
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
        private TextMeshProUGUI reales;
        private TextMeshProUGUI rations;
        private TextMeshProUGUI scrap;
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

        private static TextMeshProUGUI Chip(RectTransform row, Sprite icon, TextKey name)
        {
            RectTransform chip = UiKit.Well(row, "Chip " + name);
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
            return value;
        }

        /// <summary>Starts following a campaign. Null detaches.</summary>
        public void Bind(MetaGame game)
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
            reales.SetText("{0}", bound.Balance(Currency.Reales));
            rations.SetText("{0}", bound.Balance(Currency.Rations));
            scrap.SetText("{0}", bound.Balance(Currency.Scrap));

            Objective next = bound.CurrentObjective;
            objective.text = next != null ? next.Text.Get() : string.Empty;
        }
    }
}
