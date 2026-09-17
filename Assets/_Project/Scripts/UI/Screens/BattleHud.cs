using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Gameplay;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BinakayanRising.UI.Screens
{
    /// <summary>
    /// The in-battle interface: order of battle, deployment roster, field report and outcome card.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This replaces the prototype's IMGUI HUD. It reads <see cref="BattlePlaytest"/> through that
    /// class's public snapshot API and drives it through its command methods, so the dependency
    /// runs one way — interface depends on simulation, never the reverse.
    /// </para>
    /// <para>
    /// <b>Rebuild policy.</b> Widgets are expensive to create and cheap to update, so the HUD
    /// separates the two. Structure — how many roster rows, which units are listed — is rebuilt
    /// only when a signature of that structure actually changes. Values — health, positions,
    /// the log — are written into existing widgets every frame. Rebuilding everything on each
    /// <see cref="BattlePlaytest.StateChanged"/> would mean tearing down and re-creating the whole
    /// order of battle on every line written to the field report.
    /// </para>
    /// </remarks>
    [AddComponentMenu("")]
    public sealed class BattleHud : MonoBehaviour
    {
        private const float TopBarHeight = 72f;
        private const float SidePanelWidth = 380f;
        private const float LogWidth = 560f;
        private const float LogHeight = 210f;
        private const int LogLines = 6;

        private BattlePlaytest battle;

        private Canvas canvas;
        private RectTransform sideBody;
        private RectTransform logBody;
        private TextMeshProUGUI phaseLabel;
        private TextMeshProUGUI seedLabel;
        private TextMeshProUGUI logText;
        private readonly List<Button> speedButtons = new List<Button>();
        private readonly List<float> speedValues = new List<float>();

        private RectTransform outcomeRoot;
        private TextMeshProUGUI outcomeTitle;
        private TextMeshProUGUI outcomeSummary;

        // Order-of-battle rows, kept so health can be written into them without a rebuild.
        private readonly List<OrderRow> orderRows = new List<OrderRow>();

        private readonly List<BattlePlaytest.UnitSnapshot> units = new List<BattlePlaytest.UnitSnapshot>();
        private readonly List<BattlePlaytest.PopupSnapshot> popups = new List<BattlePlaytest.PopupSnapshot>();

        private WorldLabelPool nameLabels;
        private WorldLabelPool popupLabels;

        private string structureSignature;
        private bool structureDirty = true;

        /// <summary>One unit's line in the order of battle.</summary>
        private struct OrderRow
        {
            public int Id;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Health;
            public BarView Bar;
            public CanvasGroup Group;
        }

        private void Awake()
        {
            battle = GetComponent<BattlePlaytest>();
            if (battle == null)
            {
                battle = FindFirstObjectByType<BattlePlaytest>();
            }

            if (battle == null)
            {
                Debug.LogError("BattleHud: no BattlePlaytest to bind to; the HUD will not build.");
                enabled = false;
                return;
            }

            Build();

            battle.StateChanged += OnStateChanged;
            battle.UnitPlaced += OnUnitPlaced;
        }

        private void OnDestroy()
        {
            if (battle != null)
            {
                battle.StateChanged -= OnStateChanged;
                battle.UnitPlaced -= OnUnitPlaced;
            }
        }

        private void OnStateChanged()
        {
            structureDirty = true;
        }

        private void OnUnitPlaced()
        {
            UiSfx.Play(UiSfx.Cue.Place);
        }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            canvas = UiKit.Screen("Battle HUD", sortOrder: 100);
            UiKit.EnsureEventSystem();

            // Parented to the playtest so the HUD dies with the board it describes rather than
            // outliving it as an orphan canvas.
            canvas.transform.SetParent(transform, worldPositionStays: false);

            BuildTopBar(canvas.transform);
            BuildSidePanel(canvas.transform);
            BuildLogPanel(canvas.transform);
            BuildOutcome(canvas.transform);

            nameLabels = new WorldLabelPool(canvas, "Unit Labels", Theme.Type.Small, Theme.Parchment);

            // Damage numbers are set two steps larger than the names they fly off. At body size
            // over a painted board they are gone before the eye finds them, which is the whole
            // point of a damage number.
            popupLabels = new WorldLabelPool(canvas, "Popups", Theme.Type.Heading, Theme.GoldBright);
        }

        private void BuildTopBar(Transform parent)
        {
            RectTransform bar = UiKit.Panel(parent, "Top Bar");
            EdgeStretch(bar, top: true, size: TopBarHeight);

            RectTransform left = UiKit.Row(bar, "Left", Theme.Space.Snug, Theme.Space.Base, TextAnchor.MiddleLeft);
            Pin(left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Theme.Space.Base, 0f), new Vector2(760f, TopBarHeight));

            Image sigil = UiKit.Sigil(left, 40f, Theme.Gold);
            FixWidth(sigil.rectTransform, 40f);

            TextMeshProUGUI title = UiKit.Display(left, "Binakayan Rising", Theme.Type.Heading, TextAlignmentOptions.Left);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            FixWidth(title.rectTransform, 330f);

            phaseLabel = UiKit.Body(left, "DEPLOYMENT", Theme.Type.Body, TextAlignmentOptions.Left);

            // Not gold: the top bar is parchment, and gold on parchment is close enough in value
            // that the phase readout disappears into its own background.
            phaseLabel.color = Theme.Revolution;
            phaseLabel.textWrappingMode = TextWrappingModes.NoWrap;
            FixWidth(phaseLabel.rectTransform, 320f);

            RectTransform right = UiKit.Row(bar, "Right", Theme.Space.Tight, Theme.Space.Base, TextAnchor.MiddleRight);
            Pin(right, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-Theme.Space.Base, 0f), new Vector2(900f, TopBarHeight));

            TextMeshProUGUI seedCaption = UiKit.Caption(right, "SEED", TextAlignmentOptions.Right);
            seedCaption.color = Theme.InkSoft;
            FixWidth(seedCaption.rectTransform, 54f);

            Button minus = UiKit.SealButton(right, "-", () => battle.SetSeed(battle.Seed - 1), 44f, 40f, Theme.Type.Body);
            FixWidth((RectTransform)minus.transform, 44f);

            seedLabel = UiKit.Body(right, "1896", Theme.Type.Body, TextAlignmentOptions.Center);
            FixWidth(seedLabel.rectTransform, 72f);

            Button plus = UiKit.SealButton(right, "+", () => battle.SetSeed(battle.Seed + 1), 44f, 40f, Theme.Type.Body);
            FixWidth((RectTransform)plus.transform, 44f);

            TextMeshProUGUI speedCaption = UiKit.Caption(right, "SPEED", TextAlignmentOptions.Right);
            speedCaption.color = Theme.InkSoft;
            FixWidth(speedCaption.rectTransform, 62f);

            AddSpeedButton(right, 0.5f, "0.5x");
            AddSpeedButton(right, 1f, "1x");
            AddSpeedButton(right, 3f, "3x");
            AddSpeedButton(right, 0f, "Skip");

            Button redeploy = UiKit.SealButton(right, "Redeploy", () => battle.RequestRedeploy(), 150f, 44f, Theme.Type.Small);
            FixWidth((RectTransform)redeploy.transform, 150f);
        }

        private void AddSpeedButton(Transform parent, float value, string label)
        {
            float captured = value;
            Button button = UiKit.SealButton(parent, label, () => battle.SetSpeed(captured), 74f, 40f, Theme.Type.Small);
            FixWidth((RectTransform)button.transform, 74f);

            speedButtons.Add(button);
            speedValues.Add(value);
        }

        private void BuildSidePanel(Transform parent)
        {
            RectTransform panel = UiKit.Panel(parent, "Side Panel");
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.offsetMin = new Vector2(Theme.Space.Base, Theme.Space.Base);
            panel.offsetMax = new Vector2(Theme.Space.Base + SidePanelWidth, -(TopBarHeight + Theme.Space.Base));
            panel.sizeDelta = new Vector2(SidePanelWidth, panel.sizeDelta.y);

            // The roster, the column controls and the bond tips together are taller than the
            // panel at 1080, and taller still on a short window. Scrolling keeps every control
            // reachable instead of letting the last ones fall off the bottom edge unnoticed.
            RectTransform viewport = UiKit.NewRect(panel, "Viewport");
            UiKit.Stretch(viewport, Theme.Space.Snug);
            viewport.gameObject.AddComponent<RectMask2D>();

            var scroll = panel.gameObject.AddComponent<ScrollRect>();
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
        }

        private void BuildLogPanel(Transform parent)
        {
            RectTransform panel = UiKit.Well(parent, "Field Report");
            Pin(panel, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-Theme.Space.Base, Theme.Space.Base), new Vector2(LogWidth, LogHeight));

            logBody = UiKit.Column(panel, "Body", Theme.Space.Tight, Theme.Space.Wide, TextAnchor.UpperLeft);
            UiKit.Stretch(logBody);
            ExpandChildren(logBody);

            TextMeshProUGUI heading = UiKit.Caption(logBody, "FIELD REPORT", TextAlignmentOptions.Left);
            heading.color = Theme.Gold;
            FixHeight(heading.rectTransform, 20f);

            logText = UiKit.Body(logBody, string.Empty, Theme.Type.Small, TextAlignmentOptions.TopLeft);
            logText.color = Theme.InkSoft;
        }

        private void BuildOutcome(Transform parent)
        {
            outcomeRoot = UiKit.NewRect(parent, "Outcome");
            UiKit.Stretch(outcomeRoot);

            UiKit.Scrim(outcomeRoot);

            RectTransform card = UiKit.Panel(outcomeRoot, "Card");
            Pin(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 420f));

            RectTransform column = UiKit.Column(card, "Body", Theme.Space.Base, Theme.Space.Loose, TextAnchor.UpperCenter);
            UiKit.Stretch(column);
            ExpandChildren(column);

            UiKit.Sigil(column, 84f, Theme.Gold);

            outcomeTitle = UiKit.Display(column, "VICTORY", Theme.Type.Display, TextAlignmentOptions.Center);
            FixHeight(outcomeTitle.rectTransform, 76f);

            outcomeSummary = UiKit.Body(column, string.Empty, Theme.Type.Body, TextAlignmentOptions.Center);
            FixHeight(outcomeSummary.rectTransform, 90f);

            RectTransform actions = UiKit.Row(column, "Actions", Theme.Space.Base, 0f, TextAnchor.MiddleCenter);
            FixHeight(actions, 64f);

            Button again = UiKit.SealButton(actions, "New Seed", () => battle.RequestNewSeed(), 260f, 60f);
            FixWidth((RectTransform)again.transform, 260f);

            Button redeploy = UiKit.SealButton(actions, "Redeploy", () => battle.RequestRedeploy(), 260f, 60f);
            FixWidth((RectTransform)redeploy.transform, 260f);

            outcomeRoot.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ per-frame

        private void LateUpdate()
        {
            if (battle == null)
            {
                return;
            }

            if (structureDirty)
            {
                structureDirty = false;
                RebuildIfStructureChanged();
            }

            RefreshValues();
            RefreshWorldOverlays();
        }

        /// <summary>
        /// Rebuilds the side panel only when what it lists has actually changed.
        /// </summary>
        /// <remarks>
        /// The signature is deliberately coarse — phase, counts, selection. Health is not in it,
        /// because health changes constantly during a replay and would defeat the whole point.
        /// </remarks>
        private void RebuildIfStructureChanged()
        {
            string signature = battle.CurrentPhase + "|" + battle.SelectedSlot + "|" + battle.PlacementCount
                + "|" + battle.SpanishCount + "|" + battle.ShowHelp + "|" + units.Count;

            if (signature == structureSignature)
            {
                return;
            }

            structureSignature = signature;

            for (int i = sideBody.childCount - 1; i >= 0; i--)
            {
                Destroy(sideBody.GetChild(i).gameObject);
            }

            orderRows.Clear();

            if (battle.CurrentPhase == BattlePlaytest.Phase.Deployment)
            {
                BuildDeploymentContent();
            }
            else
            {
                BuildOrderOfBattle();
            }
        }

        private void BuildDeploymentContent()
        {
            TextMeshProUGUI title = UiKit.Display(sideBody, "Deploy Your Katipuneros", Theme.Type.Heading, TextAlignmentOptions.Left);
            FixHeight(title.rectTransform, 34f);

            TextMeshProUGUI hint = UiKit.Caption(sideBody, "Choose a unit, then click a lit tile on the trench line or a tent.", TextAlignmentOptions.TopLeft);
            hint.color = Theme.InkSoft;
            FixHeight(hint.rectTransform, 40f);

            IReadOnlyList<RosterEntry> roster = battle.Roster;
            for (int i = 0; i < roster.Count; i++)
            {
                BuildRosterRow(roster[i], i);
            }

            RectTransform columnRow = UiKit.Row(sideBody, "Spanish", Theme.Space.Tight, 0f, TextAnchor.MiddleLeft);
            FixHeight(columnRow, 48f);

            TextMeshProUGUI columnLabel = UiKit.Caption(columnRow, "SPANISH COLUMN  " + battle.SpanishCount, TextAlignmentOptions.Left);
            columnLabel.color = Theme.Ink;
            FixWidth(columnLabel.rectTransform, 186f);

            Button fewer = UiKit.SealButton(columnRow, "-", () => battle.SetSpanishCount(battle.SpanishCount - 1), 44f, 40f, Theme.Type.Body);
            FixWidth((RectTransform)fewer.transform, 44f);

            Button more = UiKit.SealButton(columnRow, "+", () => battle.SetSpanishCount(battle.SpanishCount + 1), 44f, 40f, Theme.Type.Body);
            FixWidth((RectTransform)more.transform, 44f);

            Button auto = UiKit.SealButton(sideBody, "Auto-deploy", () => battle.RequestAutoDeploy(), SidePanelWidth - 56f, 48f);
            FixHeight((RectTransform)auto.transform, 48f);

            Button assault = UiKit.SealButton(sideBody, "Begin Assault", () => battle.RequestAssault(), SidePanelWidth - 56f, 64f);
            FixHeight((RectTransform)assault.transform, 64f);

            // Nothing to attack with is a real state, not a bug — say so by disabling rather than
            // by letting the button do nothing when pressed.
            assault.interactable = battle.PlacementCount > 0;

            UiKit.Divider(sideBody);

            Button toggle = UiKit.SealButton(
                sideBody,
                battle.ShowHelp ? "Hide Tips" : "Show Tips",
                () => battle.SetShowHelp(!battle.ShowHelp),
                200f,
                40f,
                Theme.Type.Small);
            FixHeight((RectTransform)toggle.transform, 40f);

            if (!battle.ShowHelp)
            {
                return;
            }

            TextMeshProUGUI tips = UiKit.Caption(
                sideBody,
                "<b>KAPATIRAN BONDS</b>\n"
                + "MRK + ENG adjacent — +20% accuracy, +1 attack range\n"
                + "EVA + AGU adjacent — +15% attack, +10% defense\n\n"
                + "<b>TERRAIN</b>\n"
                + "Trench — +20% defense, +15% evasion\n"
                + "Tent — +5% health per turn",
                TextAlignmentOptions.TopLeft);
            tips.color = Theme.InkSoft;
            FixHeight(tips.rectTransform, 170f);
        }

        private void BuildRosterRow(RosterEntry entry, int index)
        {
            bool placed = battle.IsPlaced(entry.Id);
            bool selected = index == battle.SelectedSlot;

            RectTransform row = UiKit.Frame(sideBody, "Roster " + entry.ShortName,
                selected ? Theme.GoldBright : Theme.ParchmentDeep);
            FixHeight(row, 68f);

            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = row.GetComponentInChildren<Image>();
            int captured = index;
            button.onClick.AddListener(() =>
            {
                UiSfx.Play(UiSfx.Cue.Click);
                battle.SelectSlot(captured);
            });

            RectTransform column = UiKit.Column(row, "Text", 2f, Theme.Space.Snug, TextAnchor.MiddleLeft);
            UiKit.Stretch(column);
            ExpandChildren(column);

            TextMeshProUGUI name = UiKit.Body(column, entry.ShortName + "  " + entry.DisplayName, Theme.Type.Body, TextAlignmentOptions.Left);
            name.color = placed ? Theme.Success : Theme.Ink;
            FixHeight(name.rectTransform, 24f);

            TextMeshProUGUI stats = UiKit.Caption(
                column,
                "HP " + entry.Stats.MaxHP.ToString("0")
                + "   ATK " + entry.Stats.AttackDamage.ToString("0")
                + "   DEF " + entry.Stats.Defense.ToString("0")
                + "   RNG " + entry.Stats.AttackRange.ToString("0")
                + (placed ? "   ✓ deployed" : string.Empty),
                TextAlignmentOptions.Left);
            stats.color = Theme.InkSoft;
            FixHeight(stats.rectTransform, 20f);
        }

        private void BuildOrderOfBattle()
        {
            TextMeshProUGUI title = UiKit.Display(sideBody, "Order of Battle", Theme.Type.Heading, TextAlignmentOptions.Left);
            FixHeight(title.rectTransform, 34f);

            battle.GetUnits(units);

            BuildTeamSection(Team.Katipunan, "KATIPUNAN", Theme.Revolution);
            BuildTeamSection(Team.Spanish, "SPANISH", Theme.Colonial);
        }

        private void BuildTeamSection(Team team, string heading, Color color)
        {
            TextMeshProUGUI label = UiKit.Caption(sideBody, heading, TextAlignmentOptions.Left);
            label.color = Theme.Gold;
            FixHeight(label.rectTransform, 22f);

            for (int i = 0; i < units.Count; i++)
            {
                BattlePlaytest.UnitSnapshot unit = units[i];
                if (unit.Team != team)
                {
                    continue;
                }

                RectTransform row = UiKit.NewRect(sideBody, "Unit " + unit.ShortName);
                FixHeight(row, 46f);

                RectTransform column = UiKit.Column(row, "Text", 2f, 0f, TextAnchor.UpperLeft);
                UiKit.Stretch(column);
                ExpandChildren(column);

                RectTransform header = UiKit.Row(column, "Header", Theme.Space.Tight, 0f, TextAnchor.MiddleLeft);
                FixHeight(header, 22f);

                TextMeshProUGUI name = UiKit.Caption(header, unit.ShortName + "  " + unit.DisplayName, TextAlignmentOptions.Left);
                name.color = Theme.Ink;

                TextMeshProUGUI health = UiKit.Caption(header, unit.CurrentHP.ToString("0"), TextAlignmentOptions.Right);
                health.color = Theme.InkSoft;
                FixWidth(health.rectTransform, 56f);

                BarView bar = UiKit.Bar(column, SidePanelWidth - 72f, 12f, color);
                FixHeight((RectTransform)bar.transform, 12f);
                bar.Value = unit.HealthFraction;

                orderRows.Add(new OrderRow
                {
                    Id = unit.Id,
                    Name = name,
                    Health = health,
                    Bar = bar,
                    Group = UiKit.Group(row.gameObject),
                });
            }
        }

        private void RefreshValues()
        {
            phaseLabel.text = PhaseText();
            seedLabel.text = battle.Seed.ToString();

            for (int i = 0; i < speedButtons.Count; i++)
            {
                // The active rate is shown by tinting the label rather than by swapping the
                // button's sprite, so the row keeps a single consistent silhouette.
                var text = speedButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    text.color = Mathf.Approximately(battle.Speed, speedValues[i])
                        ? Theme.GoldBright
                        : Theme.Parchment;
                }
            }

            RefreshLog();
            RefreshOrderRows();
            RefreshOutcome();
        }

        private string PhaseText()
        {
            switch (battle.CurrentPhase)
            {
                case BattlePlaytest.Phase.Deployment:
                    return "DEPLOYMENT";
                case BattlePlaytest.Phase.Combat:
                    return "COMBAT — AI TURN " + battle.CurrentTurn;
                default:
                    return "RESOLVED";
            }
        }

        private void RefreshLog()
        {
            IReadOnlyList<string> ticker = battle.Ticker;
            int start = Mathf.Max(0, ticker.Count - LogLines);

            var builder = new System.Text.StringBuilder();
            for (int i = start; i < ticker.Count; i++)
            {
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(ticker[i]);
            }

            logText.text = builder.ToString();
        }

        private void RefreshOrderRows()
        {
            if (orderRows.Count == 0)
            {
                return;
            }

            battle.GetUnits(units);

            for (int i = 0; i < orderRows.Count; i++)
            {
                OrderRow row = orderRows[i];

                for (int u = 0; u < units.Count; u++)
                {
                    if (units[u].Id != row.Id)
                    {
                        continue;
                    }

                    BattlePlaytest.UnitSnapshot unit = units[u];
                    row.Bar.Value = unit.HealthFraction;
                    row.Health.text = unit.CurrentHP.ToString("0");

                    // A fallen unit stays listed but dims, so the order of battle still reads as
                    // a record of who marched out rather than only who is left.
                    if (row.Group != null)
                    {
                        row.Group.alpha = unit.Alive ? 1f : 0.4f;
                    }

                    break;
                }
            }
        }

        private void RefreshOutcome()
        {
            bool show = battle.CurrentPhase == BattlePlaytest.Phase.Finished && battle.Result != null;
            if (outcomeRoot.gameObject.activeSelf != show)
            {
                outcomeRoot.gameObject.SetActive(show);
                if (show)
                {
                    UiSfx.Play(battle.Result.Outcome == BattleOutcome.Victory ? UiSfx.Cue.Victory : UiSfx.Cue.Error);
                }
            }

            if (!show)
            {
                return;
            }

            BattleResult result = battle.Result;
            bool won = result.Outcome == BattleOutcome.Victory;

            outcomeTitle.text = won ? "Victory" : "Defeat";
            outcomeTitle.color = won ? Theme.Gold : Theme.Revolution;

            outcomeSummary.text =
                result.TurnsElapsed + " AI turns\n"
                + result.KatipunanAlive + " Katipuneros standing   ·   " + result.SpanishAlive + " Spanish left\n"
                + result.Events.Count + " events replayed from seed " + battle.Seed;
        }

        // ------------------------------------------------------------------ world overlays

        private void RefreshWorldOverlays()
        {
            Camera camera = battle.BoardCamera;
            if (camera == null)
            {
                return;
            }

            battle.GetUnits(units);
            battle.GetPopups(popups);

            nameLabels.Begin();
            for (int i = 0; i < units.Count; i++)
            {
                BattlePlaytest.UnitSnapshot unit = units[i];
                if (!unit.Alive)
                {
                    continue;
                }

                Vector3 screen = camera.WorldToScreenPoint(unit.World);
                if (screen.z < 0f)
                {
                    continue;
                }

                nameLabels.Place(screen, unit.ShortName, Theme.Parchment);
            }

            nameLabels.End();

            popupLabels.Begin();
            for (int i = 0; i < popups.Count; i++)
            {
                BattlePlaytest.PopupSnapshot popup = popups[i];
                Vector3 screen = camera.WorldToScreenPoint(popup.World);
                if (screen.z < 0f)
                {
                    continue;
                }

                Color tint = popup.Tint;
                tint.a = Mathf.Clamp01(1.4f - popup.Age);

                // Rising as it fades is what separates a damage number from a label that happens
                // to be sitting on a unit. It starts clear of the token so the first frame of a
                // hit is readable rather than stamped across the unit's own name.
                popupLabels.Place(screen, popup.Text, tint, 26f + (popup.Age * 56f));
            }

            popupLabels.End();
        }

        // ------------------------------------------------------------------ layout helpers

        /// <summary>Stretches a rect across the top or bottom edge of its parent.</summary>
        private static void EdgeStretch(RectTransform rect, bool top, float size)
        {
            rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rect.offsetMin = new Vector2(0f, top ? -size : 0f);
            rect.offsetMax = new Vector2(0f, top ? 0f : size);
        }

        /// <summary>Pins a rect at a fixed size against an anchor point.</summary>
        private static void Pin(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 offset, Vector2 size)
        {
            UiKit.Anchor(rect, anchor, pivot, offset, size);
        }

        private static void FixHeight(RectTransform rect, float height)
        {
            LayoutElement element = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0f;
        }

        private static void FixWidth(RectTransform rect, float width)
        {
            LayoutElement element = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            element.flexibleWidth = 0f;
        }

        /// <summary>Makes a column's children span its full width.</summary>
        private static void ExpandChildren(RectTransform rect)
        {
            var layout = rect.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.childForceExpandWidth = true;
            }
        }
    }

    /// <summary>
    /// A recycled set of screen-space labels used to annotate world positions.
    /// </summary>
    /// <remarks>
    /// Unit names and damage numbers appear and vanish constantly, and creating a
    /// <see cref="TextMeshProUGUI"/> per unit per frame would allocate continuously. The pool keeps
    /// the labels it has made, hides the surplus, and reuses the rest.
    /// </remarks>
    internal sealed class WorldLabelPool
    {
        private readonly RectTransform root;
        private readonly Canvas canvas;
        private readonly List<TextMeshProUGUI> labels = new List<TextMeshProUGUI>();
        private readonly float size;
        private readonly Color defaultColor;

        private Material outlineMaterial;
        private int used;

        public WorldLabelPool(Canvas canvas, string name, float size, Color defaultColor)
        {
            this.canvas = canvas;
            root = UiKit.NewRect(canvas.transform, name);
            UiKit.Stretch(root);
            this.size = size;
            this.defaultColor = defaultColor;
        }

        /// <summary>Starts a frame's worth of placements.</summary>
        public void Begin()
        {
            used = 0;
        }

        /// <summary>Positions one label at a screen point, raised by an offset in canvas units.</summary>
        public void Place(Vector3 screenPoint, string text, Color color, float rise = 0f)
        {
            TextMeshProUGUI label;
            if (used < labels.Count)
            {
                label = labels[used];
            }
            else
            {
                label = UiKit.Body(root, string.Empty, size, TextAlignmentOptions.Center);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.fontSharedMaterial = Outline(label);

                // TMP sizes each glyph's quad from the material it had when the mesh was built, so
                // an outline added afterwards is clipped away at the glyph edge until the padding
                // is recomputed. Skip this and the material change looks like it did nothing.
                label.UpdateMeshPadding();
                UiKit.SetSize(label.rectTransform, 200f, 34f);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.zero;
                label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                labels.Add(label);
            }

            label.gameObject.SetActive(true);
            label.text = text;
            label.color = color == default ? defaultColor : color;
            // WorldToScreenPoint returns device pixels, but the canvas scales itself to a
            // 1920x1080 reference. Placing raw pixels into a scaled canvas puts every label at
            // the wrong spot on any display that is not exactly the reference size.
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            label.rectTransform.anchoredPosition = new Vector2(
                screenPoint.x / scale, (screenPoint.y / scale) + rise);
            used++;
        }

        /// <summary>
        /// The ink-outlined material every label in this pool shares.
        /// </summary>
        /// <remarks>
        /// Parchment type over a painted board has nothing to sit against — it reads over grass and
        /// vanishes over canvas. An outline fixes that. It is built once and shared rather than set
        /// through <c>TMP_Text.outlineWidth</c>, which instances a material per label and would
        /// turn a pooled overlay into one draw call per unit on the field.
        /// </remarks>
        private Material Outline(TextMeshProUGUI sample)
        {
            if (outlineMaterial != null)
            {
                return outlineMaterial;
            }

            outlineMaterial = new Material(sample.fontSharedMaterial)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            outlineMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
            outlineMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Theme.Ink);
            outlineMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
            return outlineMaterial;
        }

        /// <summary>Hides whatever was not used this frame.</summary>
        public void End()
        {
            for (int i = used; i < labels.Count; i++)
            {
                labels[i].gameObject.SetActive(false);
            }
        }
    }
}
