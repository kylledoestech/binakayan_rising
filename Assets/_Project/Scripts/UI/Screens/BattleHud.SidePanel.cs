using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Localization;
using BinakayanRising.Gameplay;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BinakayanRising.UI.Screens
{
    public sealed partial class BattleHud
    {
        /// <summary>Portraits are 24px art shown at exactly 2x, so every texel stays square.</summary>
        private const float PortraitSize = 48f;

        private readonly List<RosterRow> rosterRows = new List<RosterRow>();
        private readonly List<OrderRow> katipunanRows = new List<OrderRow>();
        private readonly List<OrderRow> spanishRows = new List<OrderRow>();
        private readonly Dictionary<int, OrderRow> orderRowsById = new Dictionary<int, OrderRow>();

        private RectTransform sideBody;
        private RectTransform deploymentSection;
        private RectTransform combatSection;
        private RectTransform katipunanList;
        private RectTransform spanishList;

        private TextMeshProUGUI columnLabel;
        private Button columnFewer;
        private Button columnMore;
        private Button assaultButton;
        private LocalizedText tipsToggleText;
        private TextMeshProUGUI tipsBody;

        private bool deploymentDirty = true;
        private bool orderNamesDirty;
        private int shownColumnCount = -1;
        private int shownColumnVersion = -1;

        /// <summary>One roster line in the deployment list.</summary>
        private sealed class RosterRow
        {
            public int Id;
            public Button Button;
            public Image Rim;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Stats;
            public int ShownVersion = -1;
            public int ShownState = -1;
        }

        /// <summary>One unit's line in the order of battle, bound to a unit when combat starts.</summary>
        private sealed class OrderRow
        {
            public GameObject Root;
            public CanvasGroup Group;
            public Image Portrait;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Health;
            public BarView Bar;
            public int Id;
            public string ArchetypeId;
            public int Ordinal;
            public string Fallback;
            public int ShownHealth = int.MinValue;
            public float ShownFraction = -1f;
            public int ShownAlive = -1;
        }

        private void BuildSidePanel(Transform parent)
        {
            sidePanel = Register("side.panel", UiKit.Panel(parent, "Side Panel"));
            sidePanel.anchorMin = new Vector2(0f, 0f);
            sidePanel.anchorMax = new Vector2(0f, 1f);
            sidePanel.pivot = new Vector2(0f, 0.5f);
            sidePanel.offsetMin = new Vector2(Theme.Space.Base, Theme.Space.Base);
            sidePanel.offsetMax = new Vector2(Theme.Space.Base + SidePanelWidth, -(TopBarHeight + Theme.Space.Base));

            // The roster, the column controls and the bond tips together are taller than the
            // panel at 1080, and taller still on a short window. Scrolling keeps every control
            // reachable instead of letting the last ones fall off the bottom edge unnoticed.
            RectTransform viewport = UiKit.NewRect(sidePanel, "Viewport");
            UiKit.Stretch(viewport, Theme.Space.Snug);
            viewport.gameObject.AddComponent<RectMask2D>();

            var scroll = sidePanel.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 32f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = viewport;

            sideBody = UiKit.Column(viewport, "Body", Theme.Space.Snug, Theme.Space.Base, TextAnchor.UpperLeft);
            sideBody.anchorMin = new Vector2(0f, 1f);
            sideBody.anchorMax = new Vector2(1f, 1f);
            sideBody.pivot = new Vector2(0.5f, 1f);
            sideBody.anchoredPosition = Vector2.zero;
            sideBody.sizeDelta = new Vector2(0f, sideBody.sizeDelta.y);
            ExpandChildren(sideBody);

            var fitter = sideBody.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = sideBody;

            BuildDeploymentSection();
            BuildCombatSection();
        }

        private RectTransform Section(string name)
        {
            RectTransform section = UiKit.Column(sideBody, name, Theme.Space.Snug, 0f, TextAnchor.UpperLeft);
            ExpandChildren(section);
            return section;
        }

        private TextMeshProUGUI SectionTitle(Transform parent, TextKey key)
        {
            TextMeshProUGUI title = UiKit.Display(parent, Loc.Get(key), Theme.Type.Heading, TextAlignmentOptions.Left);
            FitLine(title, Theme.Type.Heading);
            FixHeight(title.rectTransform, 34f);
            UiKit.Localize(title, key);
            return title;
        }

        // ------------------------------------------------------------------ deployment

        private void BuildDeploymentSection()
        {
            deploymentSection = Section("Deployment");
            SectionTitle(deploymentSection, TextKey.DeployTitle);

            TextMeshProUGUI hint = UiKit.Caption(deploymentSection, Loc.Get(TextKey.DeployHint), TextAlignmentOptions.TopLeft);
            UiKit.Localize(hint, TextKey.DeployHint);

            RectTransform roster = Register("roster", UiKit.Column(deploymentSection, "Roster", Theme.Space.Tight, 0f, TextAnchor.UpperLeft));
            ExpandChildren(roster);

            IReadOnlyList<RosterEntry> entries = battle.Roster;
            for (int i = 0; i < entries.Count; i++)
            {
                rosterRows.Add(BuildRosterRow(roster, entries[i], i));
            }

            RectTransform columnRow = Register("deploy.column", UiKit.Row(deploymentSection, "Spanish Column", Theme.Space.Tight, 0f, TextAnchor.MiddleLeft));
            FixHeight(columnRow, 44f);

            columnLabel = UiKit.Caption(columnRow, string.Empty, TextAlignmentOptions.Left);
            columnLabel.color = Theme.Ink;
            columnLabel.fontStyle = FontStyles.UpperCase;
            FitLine(columnLabel, Theme.Type.Small);
            Element(columnLabel.rectTransform).flexibleWidth = 1f;

            columnFewer = UiKit.SealButton(columnRow, "-", () => battle.SetSpanishCount(battle.SpanishCount - 1), 44f, 40f, Theme.Type.Body, "Button Column Fewer");
            columnMore = UiKit.SealButton(columnRow, "+", () => battle.SetSpanishCount(battle.SpanishCount + 1), 44f, 40f, Theme.Type.Body, "Button Column More");

            Button auto = UiKit.SealButton(deploymentSection, TextKey.AutoDeploy, AutoDeploy, SidePanelWidth - 56f, 48f, 0f, "Button Auto Deploy");
            Register("deploy.auto", RectOf(auto));
            FixHeight(RectOf(auto), 48f);

            assaultButton = UiKit.SealButton(deploymentSection, TextKey.BeginAssault, BeginAssault, SidePanelWidth - 56f, 64f, 0f, "Button Begin Assault");
            Register("deploy.assault", RectOf(assaultButton));
            FixHeight(RectOf(assaultButton), 64f);

            UiKit.Divider(deploymentSection);

            Button toggle = UiKit.SealButton(
                deploymentSection, TextKey.ShowTips, () => battle.SetShowHelp(!battle.ShowHelp), 200f, 40f, Theme.Type.Small, "Button Tips");
            FixHeight(RectOf(toggle), 40f);
            tipsToggleText = toggle.GetComponentInChildren<LocalizedText>();

            tipsBody = UiKit.Caption(deploymentSection, Loc.Get(TextKey.TipsBody), TextAlignmentOptions.TopLeft);
            UiKit.Localize(tipsBody, TextKey.TipsBody);
            Register("deploy.tips", tipsBody.rectTransform);
        }

        private RosterRow BuildRosterRow(Transform parent, RosterEntry entry, int index)
        {
            Image rim;
            Button button = UiKit.SelectableRow(parent, "Roster " + entry.ShortName, out rim);
            RectTransform rect = RectOf(button);
            FixHeight(rect, 62f);
            Register("roster." + index, rect);

            int captured = index;
            button.onClick.AddListener(() =>
            {
                battle.SelectSlot(captured);
                Activated("roster." + captured);
            });

            RectTransform line = UiKit.Row(rect, "Line", Theme.Space.Snug, 0f, TextAnchor.MiddleLeft);
            UiKit.Stretch(line);
            line.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(
                (int)Theme.Space.Tight, (int)Theme.Space.Base, (int)Theme.Space.Hair, (int)Theme.Space.Hair);

            Image portrait = Portrait(line);
            SetPortrait(portrait, entry.ArchetypeId, Team.Katipunan);

            RectTransform column = UiKit.Column(line, "Text", 2f, 0f, TextAnchor.MiddleLeft);
            Element(column).flexibleWidth = 1f;
            ExpandChildren(column);

            TextMeshProUGUI name = UiKit.Body(column, string.Empty, Theme.Type.Body, TextAlignmentOptions.Left);
            FitLine(name, Theme.Type.Body);
            FixHeight(name.rectTransform, 24f);

            TextMeshProUGUI stats = UiKit.Caption(column, string.Empty, TextAlignmentOptions.Left);
            FitLine(stats, Theme.Type.Small);
            FixHeight(stats.rectTransform, 18f);

            return new RosterRow { Id = entry.Id, Button = button, Rim = rim, Name = name, Stats = stats };
        }

        /// <summary>An empty portrait frame of fixed size, filled by <see cref="SetPortrait"/>.</summary>
        private static Image Portrait(Transform parent)
        {
            Image image = UiKit.Icon(parent, null, PortraitSize, Color.white);
            image.name = "Portrait";
            RectTransform frame = (RectTransform)image.transform.parent;
            FixWidth(frame, PortraitSize);
            FixHeight(frame, PortraitSize);
            return image;
        }

        /// <summary>
        /// Shows a unit's portrait, or a small diamond in its side's colour when none was rendered.
        /// </summary>
        /// <remarks>
        /// Pooled order rows are rebound to different units each battle, so this sets every
        /// property the fallback changes, not only the sprite.
        /// </remarks>
        private static void SetPortrait(Image image, string archetypeId, Team team)
        {
            ThemeAssets assets = Theme.Assets;
            Sprite sprite = assets != null ? assets.UnitPortrait(archetypeId) : null;
            bool drawn = sprite != null;

            image.sprite = sprite;
            image.color = drawn ? Color.white : (team == Team.Katipunan ? Theme.Revolution : Theme.Colonial);
            image.rectTransform.localRotation = drawn ? Quaternion.identity : Quaternion.Euler(0f, 0f, 45f);
            image.rectTransform.localScale = drawn ? Vector3.one : Vector3.one * 0.5f;
        }

        private void AutoDeploy()
        {
            battle.RequestAutoDeploy();
            Activated("deploy.auto");
        }

        private void BeginAssault()
        {
            if (battle.RequestAssault())
            {
                Activated("deploy.assault");
            }
            else
            {
                UiSfx.Play(UiSfx.Cue.Error);
            }
        }

        private void RefreshDeployment()
        {
            if (!deploymentSection.gameObject.activeSelf)
            {
                return;
            }

            IReadOnlyList<RosterEntry> entries = battle.Roster;
            for (int i = 0; i < rosterRows.Count && i < entries.Count; i++)
            {
                RosterRow row = rosterRows[i];
                RosterEntry entry = entries[i];
                bool placed = battle.IsPlaced(entry.Id);
                bool selected = i == battle.SelectedSlot;
                int state = (placed ? 1 : 0) | (selected ? 2 : 0);

                if (row.ShownState == state && row.ShownVersion == Loc.Version)
                {
                    continue;
                }

                row.Rim.color = selected ? Theme.GoldBright : Theme.ParchmentDeep;
                row.Name.color = placed ? Theme.Success : Theme.Ink;

                if (row.ShownVersion != Loc.Version)
                {
                    row.Name.text = entry.ShortName + "  " + BattleText.UnitName(entry.ArchetypeId, 0, entry.DisplayName);
                }

                string stats = Loc.Format(
                    TextKey.RosterStats,
                    entry.Stats.MaxHP.ToString("0"),
                    entry.Stats.AttackDamage.ToString("0"),
                    entry.Stats.Defense.ToString("0"),
                    entry.Stats.AttackRange.ToString("0"));
                row.Stats.text = placed ? stats + "   " + Loc.Get(TextKey.RosterDeployed) : stats;
                row.Stats.color = placed ? Theme.Success : Theme.InkSoft;

                row.ShownState = state;
                row.ShownVersion = Loc.Version;
            }

            if (shownColumnCount != battle.SpanishCount || shownColumnVersion != Loc.Version)
            {
                shownColumnCount = battle.SpanishCount;
                shownColumnVersion = Loc.Version;
                columnLabel.text = Loc.Get(TextKey.SpanishColumn) + "   " + battle.SpanishCount;
            }

            columnFewer.interactable = battle.SpanishCount > 1;
            columnMore.interactable = battle.SpanishCount < 14;

            // Nothing to attack with is a real state, not a bug — say so by disabling rather than
            // by letting the button do nothing when pressed.
            assaultButton.interactable = battle.PlacementCount > 0;

            tipsToggleText.SetKey(battle.ShowHelp ? TextKey.HideTips : TextKey.ShowTips);
            if (tipsBody.gameObject.activeSelf != battle.ShowHelp)
            {
                tipsBody.gameObject.SetActive(battle.ShowHelp);
            }
        }

        // ------------------------------------------------------------------ order of battle

        private void BuildCombatSection()
        {
            combatSection = Section("Order of Battle");
            SectionTitle(combatSection, TextKey.OrderOfBattle);

            TeamHeading(combatSection, TextKey.TeamKatipunan);
            katipunanList = UiKit.Column(combatSection, "Katipunan", Theme.Space.Tight, 0f, TextAnchor.UpperLeft);
            ExpandChildren(katipunanList);

            TeamHeading(combatSection, TextKey.TeamSpanish);
            spanishList = UiKit.Column(combatSection, "Spanish", Theme.Space.Tight, 0f, TextAnchor.UpperLeft);
            ExpandChildren(spanishList);

            combatSection.gameObject.SetActive(false);
        }

        private static void TeamHeading(Transform parent, TextKey key)
        {
            TextMeshProUGUI label = UiKit.Caption(parent, Loc.Get(key), TextAlignmentOptions.Left);
            label.color = Theme.RevolutionDark;
            label.fontStyle = FontStyles.UpperCase | FontStyles.Bold;
            FixHeight(label.rectTransform, 22f);
            UiKit.Localize(label, key);
        }

        /// <summary>Points the pooled rows at the units that marched out, creating rows only when short.</summary>
        private void BindOrderOfBattle()
        {
            battle.GetUnits(units);
            orderRowsById.Clear();

            int katipunan = 0;
            int spanish = 0;
            for (int i = 0; i < units.Count; i++)
            {
                BattlePlaytest.UnitSnapshot unit = units[i];
                bool ours = unit.Team == Team.Katipunan;
                List<OrderRow> pool = ours ? katipunanRows : spanishRows;
                int index = ours ? katipunan++ : spanish++;

                while (pool.Count <= index)
                {
                    pool.Add(BuildOrderRow(ours ? katipunanList : spanishList, ours ? Theme.Revolution : Theme.Colonial));
                }

                OrderRow row = pool[index];
                row.Id = unit.Id;
                row.ArchetypeId = unit.ArchetypeId;
                row.Ordinal = unit.Ordinal;
                row.Fallback = unit.DisplayName;
                row.ShownHealth = int.MinValue;
                row.ShownFraction = -1f;
                row.ShownAlive = -1;
                row.Name.text = unit.ShortName + "  " + BattleText.UnitName(unit.ArchetypeId, unit.Ordinal, unit.DisplayName);
                SetPortrait(row.Portrait, unit.ArchetypeId, unit.Team);
                if (!row.Root.activeSelf)
                {
                    row.Root.SetActive(true);
                }

                orderRowsById[unit.Id] = row;
            }

            HideSurplus(katipunanRows, katipunan);
            HideSurplus(spanishRows, spanish);
            orderNamesDirty = false;
        }

        private static void HideSurplus(List<OrderRow> pool, int used)
        {
            for (int i = used; i < pool.Count; i++)
            {
                if (pool[i].Root.activeSelf)
                {
                    pool[i].Root.SetActive(false);
                }
            }
        }

        private OrderRow BuildOrderRow(Transform parent, Color barColor)
        {
            RectTransform row = UiKit.NewRect(parent, "Unit");
            FixHeight(row, 52f);

            RectTransform line = UiKit.Row(row, "Line", Theme.Space.Tight, 0f, TextAnchor.MiddleLeft);
            UiKit.Stretch(line);
            Image portrait = Portrait(line);

            RectTransform column = UiKit.Column(line, "Text", 2f, 0f, TextAnchor.UpperLeft);
            Element(column).flexibleWidth = 1f;
            ExpandChildren(column);

            RectTransform header = UiKit.Row(column, "Header", Theme.Space.Tight, 0f, TextAnchor.MiddleLeft);
            FixHeight(header, 22f);

            TextMeshProUGUI name = UiKit.Caption(header, string.Empty, TextAlignmentOptions.Left);
            name.color = Theme.Ink;
            FitLine(name, Theme.Type.Small);
            Element(name.rectTransform).flexibleWidth = 1f;

            TextMeshProUGUI health = UiKit.Caption(header, string.Empty, TextAlignmentOptions.Right);
            health.color = Theme.InkSoft;
            FixWidth(health.rectTransform, 56f);

            BarView bar = UiKit.Bar(column, SidePanelWidth - 72f - PortraitSize - Theme.Space.Tight, 12f, barColor);
            FixHeight(RectOf(bar), 12f);

            return new OrderRow
            {
                Root = row.gameObject,
                Group = UiKit.Group(row.gameObject),
                Portrait = portrait,
                Name = name,
                Health = health,
                Bar = bar,
            };
        }

        private void RefreshOrderOfBattle()
        {
            if (!combatSection.gameObject.activeSelf || orderRowsById.Count == 0)
            {
                return;
            }

            battle.GetUnits(units);
            for (int i = 0; i < units.Count; i++)
            {
                BattlePlaytest.UnitSnapshot unit = units[i];
                OrderRow row;
                if (!orderRowsById.TryGetValue(unit.Id, out row))
                {
                    continue;
                }

                // Written only on change: every write to a bar or a label dirties the canvas, and a
                // replay would otherwise rebuild the whole panel's geometry every frame.
                float fraction = unit.HealthFraction;
                if (Mathf.Abs(fraction - row.ShownFraction) > 0.001f)
                {
                    row.ShownFraction = fraction;
                    row.Bar.Value = fraction;
                }

                int health = Mathf.CeilToInt(unit.CurrentHP);
                if (health != row.ShownHealth)
                {
                    row.ShownHealth = health;
                    row.Health.SetText("{0}", health);
                }

                // A fallen unit stays listed but dims, so the order of battle still reads as a
                // record of who marched out rather than only who is left.
                int alive = unit.Alive ? 1 : 0;
                if (alive != row.ShownAlive)
                {
                    row.ShownAlive = alive;
                    row.Group.alpha = unit.Alive ? 1f : 0.4f;
                }

                if (orderNamesDirty)
                {
                    row.Name.text = unit.ShortName + "  " + BattleText.UnitName(row.ArchetypeId, row.Ordinal, row.Fallback);
                }
            }

            orderNamesDirty = false;
        }
    }
}
