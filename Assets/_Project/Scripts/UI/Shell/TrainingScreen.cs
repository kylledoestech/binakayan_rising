using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using BinakayanRising.Core.Meta;
using BinakayanRising.Gameplay;
using BinakayanRising.Gameplay.Flow;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// The Training Grounds: the whole roster, one soldier's level and stats, and a paid drill
    /// that buys XP. A drill that finishes a level plays the <see cref="PromotionCard"/>.
    /// </summary>
    /// <remarks>
    /// The stat table shows the next level beside the current one, so the player can see what a
    /// level is worth before spending on it. The roster pages fifteen at a time; recruiting can
    /// grow it past what one page holds.
    /// </remarks>
    public sealed class TrainingScreen : CampPanelScreen
    {
        private const float KeeperWidth = 280f;
        private const int Columns = 5;
        private const int Rows = 3;
        private const int PerPage = Columns * Rows;
        private const float TileWidth = 96f;
        private const float TileHeight = 124f;

        /// <summary>Portraits are 24 art pixels; three screen pixels each.</summary>
        private const float PortraitSize = 72f;

        /// <summary>
        /// Height of an HP/ATK/DEF row. The detail column is budgeted against the card body
        /// (532 px): 15 rows totalling 408 px plus 14 gaps of 8 px leaves 12 px spare.
        /// </summary>
        private const float StatRowHeight = 28f;

        private static readonly TextKey[] StatLabels = { TextKey.TrnHealth, TextKey.TrnAttack, TextKey.TrnDefense };

        /// <summary>The combat stats that do not grow with level, shown in one compact row.</summary>
        private static readonly TextKey[] CombatLabels = { TextKey.TrnStatEva, TextKey.TrnStatAcc, TextKey.TrnStatRng, TextKey.TrnStatCrit };

        /// <summary>Kapatiran pairs, read once: the same table the battle resolves bonds from.</summary>
        private static List<KapatiranBond> bonds;

        private KeeperCard keeper;
        private readonly List<Button> tiles = new List<Button>();
        private readonly List<Image> tileRims = new List<Image>();
        private readonly List<Image> tileFaces = new List<Image>();
        private readonly List<TextMeshProUGUI> tileTags = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> tileLevels = new List<TextMeshProUGUI>();
        private readonly int[] tileUnits = new int[PerPage];
        private RectTransform pager;
        private TextMeshProUGUI pageLabel;

        private TextMeshProUGUI unitName;
        private TextMeshProUGUI unitRole;
        private TextMeshProUGUI levelLabel;
        private BarView xpBar;
        private TextMeshProUGUI xpLabel;
        private readonly TextMeshProUGUI[] statNow = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] statNext = new TextMeshProUGUI[3];
        private TextMeshProUGUI nextHeading;
        private TextMeshProUGUI weaponLine;
        private readonly TextMeshProUGUI[] combatValues = new TextMeshProUGUI[4];
        private TextMeshProUGUI bondLine;
        private TextMeshProUGUI drillLine;
        private Button drill;
        private TextMeshProUGUI drillCost;

        private int page;
        private int selectedUnit;
        private MetaGame bound;
        private bool stale = true;
        private int renderedVersion = -1;

        public override GameState State
        {
            get { return GameState.RosterTraining; }
        }

        /// <summary>The unit whose details are showing, for the screenshot autopilot.</summary>
        public int SelectedUnit
        {
            get { return selectedUnit; }
            set
            {
                selectedUnit = value;
                stale = true;
            }
        }

        protected override void BuildTabs(RectTransform row)
        {
        }

        protected override void BuildBody(RectTransform area)
        {
            RectTransform row = UiKit.Row(area, "Row", Theme.Space.Wide, 0f, TextAnchor.UpperLeft);
            UiKit.Stretch(row);

            keeper = BuildKeeper(row, KeeperWidth);
            BuildRoster(row);
            BuildDetail(row);
        }

        private void BuildRoster(RectTransform parent)
        {
            RectTransform column = UiKit.Column(parent, "Roster", Theme.Space.Tight, 0f, TextAnchor.UpperCenter);
            float gridWidth = (Columns * TileWidth) + ((Columns - 1) * Theme.Space.Tight);
            UiLayout.Fix(column, gridWidth, 0f);
            UiLayout.FlexibleHeight(column);

            RectTransform grid = UiKit.NewRect(column, "Grid");
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(TileWidth, TileHeight);
            layout.spacing = new Vector2(Theme.Space.Tight, Theme.Space.Tight);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = Columns;
            layout.childAlignment = TextAnchor.UpperLeft;
            UiLayout.Fix(grid, gridWidth, (Rows * TileHeight) + ((Rows - 1) * Theme.Space.Tight));

            for (int i = 0; i < PerPage; i++)
            {
                BuildTile(grid, i);
            }

            pager = UiKit.Row(column, "Pager", Theme.Space.Base, 0f, TextAnchor.MiddleCenter);
            UiLayout.Fix(pager, gridWidth, 52f);
            Button previous = UiKit.SealButton(pager, "◂", () => Turn(-1), 64f, 48f, Theme.Type.Heading, "Button Page Back");
            UiLayout.Fix((RectTransform)previous.transform, 64f, 48f);
            pageLabel = UiKit.Body(pager, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Center);
            UiLayout.Fix(pageLabel.rectTransform, 96f, 40f);
            Button next = UiKit.SealButton(pager, "▸", () => Turn(1), 64f, 48f, Theme.Type.Heading, "Button Page Next");
            UiLayout.Fix((RectTransform)next.transform, 64f, 48f);
        }

        private void BuildTile(RectTransform grid, int index)
        {
            Image rim;
            Button tile = UiKit.SelectableRow(grid, "Unit " + index, out rim);
            int slot = index;
            tile.onClick.AddListener(() => Select(tileUnits[slot]));

            Image face = UiKit.Icon(tile.transform, null, PortraitSize, Color.white);
            UiKit.Anchor((RectTransform)face.transform.parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(PortraitSize, PortraitSize));

            TextMeshProUGUI tag = UiKit.Caption(tile.transform, string.Empty, TextAlignmentOptions.Center);
            tag.fontStyle = FontStyles.Bold;
            UiLayout.OneLine(tag, Theme.Type.Small);
            UiKit.Anchor(tag.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(TileWidth - 8f, 20f));

            TextMeshProUGUI level = UiKit.Caption(tile.transform, string.Empty, TextAlignmentOptions.Center);
            UiLayout.OneLine(level, Theme.Type.Small);
            UiKit.Anchor(level.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(TileWidth - 8f, 20f));

            tiles.Add(tile);
            tileRims.Add(rim);
            tileFaces.Add(face);
            tileTags.Add(tag);
            tileLevels.Add(level);
        }

        private void BuildDetail(RectTransform parent)
        {
            RectTransform detail = UiKit.Column(parent, "Detail", Theme.Space.Tight, 0f, TextAnchor.UpperLeft);
            UiLayout.Flexible(detail);
            UiLayout.FlexibleHeight(detail);
            UiLayout.FillWidth(detail);

            unitName = UiKit.Body(detail, string.Empty, Theme.Type.Heading + 4f, TextAlignmentOptions.Left);
            unitName.fontStyle = FontStyles.Bold;
            unitName.color = Theme.Revolution;
            UiLayout.OneLine(unitName, Theme.Type.Heading + 4f);
            UiLayout.Fix(unitName.rectTransform, 0f, 36f);

            unitRole = UiKit.Caption(detail, string.Empty, TextAlignmentOptions.Left);
            unitRole.fontStyle = FontStyles.Italic;
            UiLayout.OneLine(unitRole, Theme.Type.Body);
            UiLayout.Fix(unitRole.rectTransform, 0f, 24f);

            RectTransform levelRow = UiKit.Row(detail, "Level", Theme.Space.Base, 0f, TextAnchor.MiddleLeft);
            UiLayout.Fix(levelRow, 0f, 32f);
            levelLabel = UiKit.Body(levelRow, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Left);
            levelLabel.fontStyle = FontStyles.Bold;
            UiLayout.OneLine(levelLabel, Theme.Type.Heading);
            UiLayout.Fix(levelLabel.rectTransform, 150f, 32f);
            xpLabel = UiKit.Caption(levelRow, string.Empty, TextAlignmentOptions.Right);
            UiLayout.OneLine(xpLabel, Theme.Type.Body);
            UiLayout.Flexible(xpLabel.rectTransform);
            UiLayout.Fix(xpLabel.rectTransform, 0f, 32f);

            xpBar = UiKit.Bar(detail, 0f, 20f, Theme.Gold);
            UiLayout.Fix((RectTransform)xpBar.transform, 0f, 20f);

            Image rule = UiKit.Divider(detail);
            UiLayout.Fix(rule.rectTransform, 0f, 12f);

            RectTransform header = StatRow(detail, "Stat Header", 24f);
            UiLayout.Fix(StatCell(header, string.Empty, 150f, TextAlignmentOptions.Left).rectTransform, 150f, 24f);
            TextMeshProUGUI nowHeading = StatCell(header, Loc.Get(TextKey.TrnNow), 100f, TextAlignmentOptions.Right);
            nowHeading.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            UiKit.Localize(nowHeading, TextKey.TrnNow);
            nextHeading = StatCell(header, Loc.Get(TextKey.TrnNext), 0f, TextAlignmentOptions.Right);
            nextHeading.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            UiKit.Localize(nextHeading, TextKey.TrnNext);

            for (int i = 0; i < StatLabels.Length; i++)
            {
                RectTransform row = StatRow(detail, "Stat " + i, StatRowHeight);
                TextMeshProUGUI label = UiKit.Body(row, Loc.Get(StatLabels[i]), Theme.Type.Body + 2f, TextAlignmentOptions.Left);
                UiKit.Localize(label, StatLabels[i]);
                UiLayout.OneLine(label, Theme.Type.Body + 2f);
                UiLayout.Fix(label.rectTransform, 150f, StatRowHeight);

                statNow[i] = UiKit.Body(row, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Right);
                statNow[i].fontStyle = FontStyles.Bold;
                UiLayout.OneLine(statNow[i], Theme.Type.Heading);
                UiLayout.Fix(statNow[i].rectTransform, 100f, StatRowHeight);

                statNext[i] = UiKit.Body(row, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Right);
                statNext[i].color = Theme.Success;
                UiLayout.OneLine(statNext[i], Theme.Type.Body + 2f);
                UiLayout.Flexible(statNext[i].rectTransform);
                UiLayout.Fix(statNext[i].rectTransform, 0f, StatRowHeight);
            }

            // EVA / ACC / RNG / CRIT: fixed by archetype, so no "next" column, four to a row.
            RectTransform combat = StatRow(detail, "Combat Stats", 26f);
            for (int i = 0; i < CombatLabels.Length; i++)
            {
                RectTransform cell = UiKit.Row(combat, "Cell " + i, Theme.Space.Hair, 0f, TextAnchor.MiddleLeft);
                UiLayout.Flexible(cell);
                UiLayout.Fix(cell, 0f, 26f);

                TextMeshProUGUI label = UiKit.Caption(cell, Loc.Get(CombatLabels[i]), TextAlignmentOptions.Left);
                label.fontStyle = FontStyles.Bold;
                UiKit.Localize(label, CombatLabels[i]);
                UiLayout.OneLine(label, Theme.Type.Small);
                UiLayout.Fix(label.rectTransform, 58f, 26f);

                combatValues[i] = UiKit.Body(cell, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Left);
                combatValues[i].fontStyle = FontStyles.Bold;
                UiLayout.OneLine(combatValues[i], Theme.Type.Body + 2f);
                UiLayout.Flexible(combatValues[i].rectTransform);
                UiLayout.Fix(combatValues[i].rectTransform, 0f, 26f);
            }

            weaponLine = UiKit.Caption(detail, string.Empty, TextAlignmentOptions.Left);
            UiLayout.OneLine(weaponLine, Theme.Type.Body);
            UiLayout.Fix(weaponLine.rectTransform, 0f, 26f);

            bondLine = UiKit.Caption(detail, string.Empty, TextAlignmentOptions.Left);
            UiLayout.OneLine(bondLine, Theme.Type.Body);
            UiLayout.Fix(bondLine.rectTransform, 0f, 26f);

            Image rule2 = UiKit.Divider(detail);
            UiLayout.Fix(rule2.rectTransform, 0f, 12f);

            drillLine = UiKit.Body(detail, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Left);
            drillLine.fontStyle = FontStyles.Bold;
            UiLayout.OneLine(drillLine, Theme.Type.Body + 2f);
            UiLayout.Fix(drillLine.rectTransform, 0f, 26f);

            RectTransform actions = UiKit.Row(detail, "Actions", Theme.Space.Base, 0f, TextAnchor.MiddleLeft);
            UiLayout.Fix(actions, 0f, 60f);
            drill = UiKit.SealButton(actions, TextKey.TrnDrill, Drill, 200f, 60f, 0f, "Button Drill");
            UiLayout.Fix((RectTransform)drill.transform, 200f, 60f);
            drillCost = UiKit.Body(actions, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Left);
            UiLayout.OneLine(drillCost, Theme.Type.Body + 2f);
            UiLayout.Flexible(drillCost.rectTransform);
            UiLayout.Fix(drillCost.rectTransform, 0f, 40f);
        }

        private static RectTransform StatRow(RectTransform parent, string name, float height)
        {
            RectTransform row = UiKit.Row(parent, name, 0f, 0f, TextAnchor.MiddleLeft);
            UiLayout.Fix(row, 0f, height);
            return row;
        }

        private static TextMeshProUGUI StatCell(RectTransform row, string text, float width, TextAlignmentOptions align)
        {
            TextMeshProUGUI cell = UiKit.Caption(row, text, align);
            UiLayout.OneLine(cell, Theme.Type.Small);
            if (width > 0f)
            {
                UiLayout.Fix(cell.rectTransform, width, 24f);
            }
            else
            {
                UiLayout.Flexible(cell.rectTransform);
                UiLayout.Fix(cell.rectTransform, 0f, 24f);
            }

            return cell;
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
            SetTitle(Encampment.Site(Places.Training).Name.Get());
            keeper.Show(Characters.Sergeant, Loc.Get(TextKey.TrnKeeperNote));

            IReadOnlyList<OwnedUnit> units = game.Units;
            if (game.FindUnit(selectedUnit) == null)
            {
                selectedUnit = units.Count > 0 ? units[0].id : 0;
            }

            RedrawRoster(game, units);
            RedrawDetail(game, game.FindUnit(selectedUnit));
        }

        private void RedrawRoster(MetaGame game, IReadOnlyList<OwnedUnit> units)
        {
            int pages = Mathf.Max(1, (units.Count + PerPage - 1) / PerPage);
            page = Mathf.Clamp(page, 0, pages - 1);
            pager.gameObject.SetActive(pages > 1);
            pageLabel.text = Loc.Format(TextKey.TrnPage, page + 1, pages);

            for (int i = 0; i < PerPage; i++)
            {
                int index = (page * PerPage) + i;
                bool shown = index < units.Count;
                tiles[i].gameObject.SetActive(shown);
                if (!shown)
                {
                    tileUnits[i] = 0;
                    continue;
                }

                OwnedUnit unit = units[index];
                UnitArchetype archetype = UnitCatalog.Find(unit.archetype);
                tileUnits[i] = unit.id;
                tileRims[i].color = unit.id == selectedUnit ? Theme.Gold : Theme.ParchmentDeep;

                Sprite face = Theme.Assets != null ? Theme.Assets.UnitPortrait(unit.archetype) : null;
                tileFaces[i].sprite = face;
                tileFaces[i].enabled = face != null;
                tileTags[i].text = archetype != null ? archetype.ShortName : unit.archetype;
                tileTags[i].color = archetype != null ? Theme.Rarity.Of(archetype.Rarity) : Theme.Ink;
                tileLevels[i].text = Loc.Format(TextKey.TrnLevel, unit.level);
            }
        }

        private void RedrawDetail(MetaGame game, OwnedUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            UnitArchetype archetype = UnitCatalog.Find(unit.archetype);
            unitName.text = archetype != null ? archetype.Name.Get() : unit.archetype;
            unitRole.text = archetype != null
                ? UnitCatalog.RoleLabel(archetype.Role).Get() + " · " + UnitCatalog.RarityLabel(archetype.Rarity).Get()
                : string.Empty;
            levelLabel.text = Loc.Format(TextKey.TrnLevel, unit.level);

            bool top = game.IsMaxLevel(unit);
            int needed = game.XpToNext(unit);
            xpBar.Value = top ? 1f : Mathf.Clamp01(unit.xp / (float)needed);
            xpLabel.text = top ? Loc.Get(TextKey.TrnTopLevel) : Loc.Format(TextKey.TrnXp, unit.xp, needed);

            UnitStats now = game.StatsOf(unit);
            UnitStats next = game.StatsAt(unit.archetype, unit.level + 1);
            UnitStats bare = game.StatsAt(unit.archetype, unit.level);
            float[] nowValues = { now.MaxHP, now.AttackDamage, now.Defense };

            // The next level's gain on top of what the unit has now, weapon included.
            float[] gains = { next.MaxHP - bare.MaxHP, next.AttackDamage - bare.AttackDamage, next.Defense - bare.Defense };
            nextHeading.gameObject.SetActive(!top);
            for (int i = 0; i < 3; i++)
            {
                statNow[i].text = PromotionCard.StatNumber(nowValues[i]);
                statNext[i].text = top ? string.Empty : "▸ " + PromotionCard.StatNumber(nowValues[i] + gains[i]) + "   (+" + PromotionCard.StatNumber(gains[i]) + ")";
            }

            WeaponDef weapon = game.WeaponOf(unit);
            weaponLine.text = weapon != null
                ? Loc.Format(TextKey.TrnWeapon, weapon.Name.Get() + "  " + Loc.Format(TextKey.InvAttack, weapon.AttackBonus))
                : Loc.Get(TextKey.TrnNoWeapon);

            combatValues[0].text = Percent(now.Evasion);
            combatValues[1].text = Percent(now.RangedAccuracy);
            combatValues[2].text = PromotionCard.StatNumber(now.AttackRange);
            combatValues[3].text = Percent(now.CriticalHitChance);

            string bond = BondText(unit.archetype);
            bondLine.text = bond != null ? Loc.Format(TextKey.TrnBond, bond) : Loc.Get(TextKey.TrnNoBond);
            bondLine.color = bond != null ? Theme.Revolution : Theme.InkSoft;

            drill.gameObject.SetActive(!top);
            if (top)
            {
                drillLine.text = Loc.Get(TextKey.TrnTopLevel);
                drillCost.text = string.Empty;
                return;
            }

            drillLine.text = Loc.Format(TextKey.TrnDrillGives, game.Rules.DrillXp);
            drillCost.text = CostText(game.Rules.DrillCost);
            drillCost.color = game.CanAfford(game.Rules.DrillCost) ? Theme.Ink : Theme.Danger;
            drill.interactable = game.CanDrill(unit);
        }

        private static string Percent(float fraction)
        {
            return Mathf.RoundToInt(fraction * 100f) + "%";
        }

        /// <summary>
        /// "Partner — effect" for every Kapatiran pair the archetype is in, or null when it has none.
        /// </summary>
        private static string BondText(string archetypeId)
        {
            if (bonds == null)
            {
                bonds = PlaytestScenario.Bonds();
            }

            var parts = new List<string>();
            for (int i = 0; i < bonds.Count; i++)
            {
                KapatiranBond bond = bonds[i];
                string partner = bond.ArchetypeA == archetypeId ? bond.ArchetypeB
                    : (bond.ArchetypeB == archetypeId ? bond.ArchetypeA : null);
                if (partner == null)
                {
                    continue;
                }

                UnitArchetype other = UnitCatalog.Find(partner);
                var effects = new List<string>();
                for (int m = 0; m < bond.Modifiers.Count; m++)
                {
                    effects.Add(ModifierText(bond.Modifiers[m]));
                }

                parts.Add((other != null ? other.Name.Get() : partner) + " — " + string.Join(", ", effects));
            }

            return parts.Count > 0 ? string.Join("  ·  ", parts) : null;
        }

        private static string ModifierText(StatModifier modifier)
        {
            string name = Loc.Get(StatShort(modifier.Stat));
            var text = new System.Text.StringBuilder(name);
            if (modifier.HasPercent)
            {
                text.Append(' ').Append(modifier.PercentDelta >= 0f ? "+" : string.Empty)
                    .Append(Mathf.RoundToInt(modifier.PercentDelta * 100f)).Append('%');
            }

            if (modifier.HasFlat)
            {
                text.Append(' ').Append(modifier.FlatDelta >= 0f ? "+" : string.Empty)
                    .Append(PromotionCard.StatNumber(modifier.FlatDelta));
            }

            return text.ToString();
        }

        private static TextKey StatShort(StatKind stat)
        {
            switch (stat)
            {
                case StatKind.MaxHP: return TextKey.TrnStatHp;
                case StatKind.AttackDamage: return TextKey.TrnStatAtk;
                case StatKind.Defense: return TextKey.TrnStatDef;
                case StatKind.Evasion: return TextKey.TrnStatEva;
                case StatKind.RangedAccuracy: return TextKey.TrnStatAcc;
                case StatKind.AttackRange: return TextKey.TrnStatRng;
                case StatKind.CriticalHitChance: return TextKey.TrnStatCrit;
                case StatKind.MovementSpeed: return TextKey.TrnStatMove;
                default: return TextKey.TrnStatHeal;
            }
        }

        private static string CostText(Cost cost)
        {
            var parts = new List<string>();
            if (cost.Reales > 0)
            {
                parts.Add(cost.Reales + " " + Loc.Get(TextKey.CurReales));
            }

            if (cost.Rations > 0)
            {
                parts.Add(cost.Rations + " " + Loc.Get(TextKey.CurRations));
            }

            if (cost.Scrap > 0)
            {
                parts.Add(cost.Scrap + " " + Loc.Get(TextKey.CurScrap));
            }

            return string.Join("  +  ", parts);
        }

        // ------------------------------------------------------------------ actions

        private void Select(int unitId)
        {
            if (unitId == 0 || unitId == selectedUnit)
            {
                return;
            }

            selectedUnit = unitId;
            UiSfx.Play(UiSfx.Cue.Click);
            stale = true;
        }

        private void Turn(int step)
        {
            page += step;
            UiSfx.Play(UiSfx.Cue.Toggle);
            stale = true;
        }

        private void Drill()
        {
            if (Game == null)
            {
                return;
            }

            OwnedUnit unit = Game.FindUnit(selectedUnit);
            LevelUp up;
            if (!Game.TryDrill(selectedUnit, out up))
            {
                UiSfx.Play(UiSfx.Cue.Error);
                UiControls.Toast(Loc.Get(TextKey.InvCantAfford));
                return;
            }

            if (up != null)
            {
                PromotionCard.Show(new[] { up }, () => Shell.ShowRankUp(null));
                return;
            }

            UnitArchetype archetype = UnitCatalog.Find(unit.archetype);
            UiSfx.Play(UiSfx.Cue.Confirm);
            UiControls.Toast(Loc.Format(TextKey.TrnDrilled, archetype != null ? archetype.Name.Get() : unit.archetype, Game.Rules.DrillXp));
            CoroutineHost.Run(UiTween.Punch(xpBar.transform, 0.08f, 0.2f));
        }
    }
}
