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
    /// The Armory, which is the inventory: what the camp holds and what each thing is for, and the
    /// weapons, which can be handed to a soldier or reforged into the next kind.
    /// </summary>
    /// <remarks>
    /// Resources list where each comes from and what spends it, so the page doubles as a legend
    /// for the economy. Weapons carry their tier and every bonus they give; reforging shows its
    /// full price before the button is pressed, and the button stays off until the purse can pay.
    /// A weapon reserved to one kind of soldier (the Lantaka) greys out everyone else and says why.
    /// The list pages once the rack outgrows it.
    /// </remarks>
    public sealed class InventoryScreen : CampPanelScreen
    {
        private const int ResourcesTab = 0;
        private const int WeaponsTab = 1;
        private const float ListWidth = 420f;
        private const float RowHeight = 66f;
        private const int PerPage = 6;

        private static readonly Currency[] Kinds = { Currency.Reales, Currency.Rations, Currency.Scrap };

        private RectTransform resources;
        private RectTransform weapons;
        private readonly TextMeshProUGUI[] kindAmount = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] kindUse = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] kindWhere = new TextMeshProUGUI[3];

        private RectTransform weaponList;
        private TextMeshProUGUI emptyNote;
        private RectTransform detail;
        private TextMeshProUGUI weaponName;
        private TextMeshProUGUI weaponFacts;
        private TextMeshProUGUI weaponNote;
        private TextMeshProUGUI holderLine;
        private TextMeshProUGUI restrictLine;
        private RectTransform giveRow;
        private RectTransform pager;
        private TextMeshProUGUI pageLabel;
        private TextMeshProUGUI reforgeLine;
        private TextMeshProUGUI reforgeCost;
        private Button reforge;

        private readonly List<Button> weaponRows = new List<Button>();
        private readonly List<Image> weaponRims = new List<Image>();
        private readonly List<int> weaponIds = new List<int>();
        private int selectedWeapon;
        private int page;
        private MetaGame bound;
        private bool stale = true;
        private int renderedVersion = -1;

        public override GameState State
        {
            get { return GameState.Inventory; }
        }

        /// <summary>The weapon shown in the detail. Setting it also turns to its page.</summary>
        public int SelectedWeapon
        {
            get { return selectedWeapon; }
            set
            {
                selectedWeapon = value;
                MetaGame game = Game;
                if (game != null)
                {
                    for (int i = 0; i < game.Weapons.Count; i++)
                    {
                        if (game.Weapons[i].id == value)
                        {
                            page = i / PerPage;
                        }
                    }
                }

                stale = true;
            }
        }

        /// <summary>
        /// A weapon's bonuses in one short line, e.g. "ATK +7 · ACC –5% · RNG +1". Shared with the
        /// Training Grounds.
        /// </summary>
        public static string BonusText(WeaponDef weapon)
        {
            var parts = new List<string>();
            parts.Add(Loc.Get(TextKey.TrnStatAtk) + " " + Signed(weapon.AttackBonus, false));
            if (weapon.AccuracyBonus != 0f)
            {
                parts.Add(Loc.Get(TextKey.TrnStatAcc) + " " + Signed(weapon.AccuracyBonus * 100f, true));
            }

            if (weapon.CritBonus != 0f)
            {
                parts.Add(Loc.Get(TextKey.TrnStatCrit) + " " + Signed(weapon.CritBonus * 100f, true));
            }

            if (weapon.RangeBonus != 0f)
            {
                parts.Add(Loc.Get(TextKey.TrnStatRng) + " " + Signed(weapon.RangeBonus, false));
            }

            return string.Join(" · ", parts);
        }

        /// <summary>"+3" or "–3", with an en dash: the fonts carry no true minus sign.</summary>
        private static string Signed(float value, bool percent)
        {
            int whole = Mathf.RoundToInt(value);
            return (whole < 0 ? "–" : "+") + Mathf.Abs(whole) + (percent ? "%" : string.Empty);
        }

        protected override void BuildTabs(RectTransform row)
        {
            AddTab(row, Loc.Get(TextKey.InvResources), ResourcesTab);
            AddTab(row, Loc.Get(TextKey.InvWeapons), WeaponsTab);
        }

        protected override void BuildBody(RectTransform area)
        {
            resources = BuildResources(area);
            weapons = BuildWeapons(area);
        }

        private RectTransform BuildResources(RectTransform parent)
        {
            RectTransform column = UiKit.Column(parent, "Resources", Theme.Space.Base, 0f, TextAnchor.UpperLeft);
            UiKit.Stretch(column);
            UiLayout.FillWidth(column);

            for (int i = 0; i < Kinds.Length; i++)
            {
                RectTransform well = UiKit.Well(column, "Kind " + Kinds[i]);
                UiLayout.Fix(well, 0f, 130f);
                RectTransform row = UiKit.Row(well, "Row", Theme.Space.Wide, 0f, TextAnchor.MiddleLeft);
                UiKit.Stretch(row, Theme.Space.Wide);

                Image icon = UiKit.Icon(row, IconOf(Kinds[i]), 80f, Color.white);
                UiLayout.Fix((RectTransform)icon.transform.parent, 80f, 80f);

                kindAmount[i] = UiKit.Display(row, "0", Theme.Type.Title, TextAlignmentOptions.Left);
                kindAmount[i].color = Theme.Revolution;
                UiLayout.OneLine(kindAmount[i], Theme.Type.Title);
                UiLayout.Fix(kindAmount[i].rectTransform, 300f, 50f);

                RectTransform text = UiKit.Column(row, "Text", Theme.Space.Hair, 0f, TextAnchor.MiddleLeft);
                UiLayout.Flexible(text);
                UiLayout.FillWidth(text);

                kindUse[i] = UiKit.Body(text, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Left);
                UiLayout.OneLine(kindUse[i], Theme.Type.Body + 2f);
                UiLayout.Fix(kindUse[i].rectTransform, 0f, 30f);

                kindWhere[i] = UiKit.Caption(text, string.Empty, TextAlignmentOptions.Left);
                kindWhere[i].fontStyle = FontStyles.Italic;
                UiLayout.OneLine(kindWhere[i], Theme.Type.Body);
                UiLayout.Fix(kindWhere[i].rectTransform, 0f, 26f);
            }

            return column;
        }

        private RectTransform BuildWeapons(RectTransform parent)
        {
            RectTransform row = UiKit.Row(parent, "Weapons", Theme.Space.Huge, 0f, TextAnchor.UpperLeft);
            UiKit.Stretch(row);

            weaponList = UiKit.Column(row, "List", Theme.Space.Tight, 0f, TextAnchor.UpperLeft);
            UiLayout.Fix(weaponList, ListWidth, 0f);
            UiLayout.FlexibleHeight(weaponList);
            UiLayout.FillWidth(weaponList);

            emptyNote = UiKit.Body(weaponList, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.TopLeft);
            emptyNote.textWrappingMode = TextWrappingModes.Normal;
            UiLayout.Fix(emptyNote.rectTransform, 0f, 80f);

            // Every row up front, so the pager sits below the last of them.
            for (int i = 0; i < PerPage; i++)
            {
                SyncWeaponRows(i + 1);
            }

            pager = UiKit.Row(weaponList, "Pager", Theme.Space.Base, 0f, TextAnchor.MiddleCenter);
            UiLayout.Fix(pager, 0f, 52f);
            Button previous = UiKit.SealButton(pager, "◂", () => Turn(-1), 64f, 48f, Theme.Type.Heading, "Button Weapon Page Back");
            UiLayout.Fix((RectTransform)previous.transform, 64f, 48f);
            pageLabel = UiKit.Body(pager, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Center);
            UiLayout.OneLine(pageLabel, Theme.Type.Body + 2f);
            UiLayout.Fix(pageLabel.rectTransform, 96f, 40f);
            Button next = UiKit.SealButton(pager, "▸", () => Turn(1), 64f, 48f, Theme.Type.Heading, "Button Weapon Page Next");
            UiLayout.Fix((RectTransform)next.transform, 64f, 48f);

            detail = UiKit.Column(row, "Detail", Theme.Space.Tight, 0f, TextAnchor.UpperLeft);
            UiLayout.Flexible(detail);
            UiLayout.FlexibleHeight(detail);
            UiLayout.FillWidth(detail);

            weaponName = UiKit.Display(detail, string.Empty, Theme.Type.Title, TextAlignmentOptions.Left);
            weaponName.color = Theme.Revolution;
            UiLayout.OneLine(weaponName, Theme.Type.Title);
            UiLayout.Fix(weaponName.rectTransform, 0f, 46f);

            weaponFacts = UiKit.Body(detail, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Left);
            weaponFacts.fontStyle = FontStyles.Bold;
            UiLayout.OneLine(weaponFacts, Theme.Type.Body + 2f);
            UiLayout.Fix(weaponFacts.rectTransform, 0f, 28f);

            weaponNote = UiKit.Body(detail, string.Empty, Theme.Type.Body, TextAlignmentOptions.TopLeft);
            weaponNote.fontStyle = FontStyles.Italic;
            weaponNote.textWrappingMode = TextWrappingModes.Normal;
            UiLayout.Fix(weaponNote.rectTransform, 0f, 52f);

            holderLine = UiKit.Body(detail, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Left);
            UiLayout.OneLine(holderLine, Theme.Type.Body + 2f);
            UiLayout.Fix(holderLine.rectTransform, 0f, 30f);

            Image rule = UiKit.Divider(detail);
            UiLayout.Fix(rule.rectTransform, 0f, 16f);

            RectTransform giveHead = UiKit.Row(detail, "Give Heading", Theme.Space.Base, 0f, TextAnchor.MiddleLeft);
            UiLayout.Fix(giveHead, 0f, 24f);
            TextMeshProUGUI giveHeading = UiKit.Caption(giveHead, Loc.Get(TextKey.InvGiveTo), TextAlignmentOptions.Left);
            giveHeading.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            UiKit.Localize(giveHeading, TextKey.InvGiveTo);
            UiLayout.OneLine(giveHeading, Theme.Type.Small);
            UiLayout.Fix(giveHeading.rectTransform, 130f, 24f);

            restrictLine = UiKit.Caption(giveHead, string.Empty, TextAlignmentOptions.Left);
            restrictLine.fontStyle = FontStyles.Italic;
            restrictLine.color = Theme.Danger;
            UiLayout.OneLine(restrictLine, Theme.Type.Body);
            UiLayout.Flexible(restrictLine.rectTransform);
            UiLayout.Fix(restrictLine.rectTransform, 0f, 24f);

            giveRow = UiKit.Row(detail, "Give To", Theme.Space.Tight, 0f, TextAnchor.MiddleLeft);
            UiLayout.Fix(giveRow, 0f, 112f);

            Image rule2 = UiKit.Divider(detail);
            UiLayout.Fix(rule2.rectTransform, 0f, 16f);

            reforgeLine = UiKit.Body(detail, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Left);
            reforgeLine.fontStyle = FontStyles.Bold;
            UiLayout.OneLine(reforgeLine, Theme.Type.Body + 2f);
            UiLayout.Fix(reforgeLine.rectTransform, 0f, 30f);

            RectTransform reforgeRow = UiKit.Row(detail, "Reforge", Theme.Space.Wide, 0f, TextAnchor.MiddleLeft);
            UiLayout.Fix(reforgeRow, 0f, 64f);
            reforge = UiKit.SealButton(reforgeRow, TextKey.InvReforge, Reforge, 260f, 60f, 0f, "Button Reforge");
            UiLayout.Fix((RectTransform)reforge.transform, 260f, 60f);
            reforgeCost = UiKit.Body(reforgeRow, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Left);
            UiLayout.OneLine(reforgeCost, Theme.Type.Body + 2f);
            UiLayout.Flexible(reforgeCost.rectTransform);
            UiLayout.Fix(reforgeCost.rectTransform, 0f, 40f);

            return row;
        }

        private static Sprite IconOf(Currency kind)
        {
            switch (kind)
            {
                case Currency.Rations: return Theme.IconRations;
                case Currency.Scrap: return Theme.IconScrap;
                default: return Theme.IconReales;
            }
        }

        // ------------------------------------------------------------------ showing

        protected override void Refresh()
        {
            base.Refresh();
            Bind(Game);
            SelectTab(SelectedTab < 0 ? ResourcesTab : SelectedTab);
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
            resources.gameObject.SetActive(index == ResourcesTab);
            weapons.gameObject.SetActive(index == WeaponsTab);
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
            if (game == null || SelectedTab < 0)
            {
                return;
            }

            stale = false;
            renderedVersion = Loc.Version;
            SetTabLabel(ResourcesTab, Loc.Get(TextKey.InvResources));
            SetTabLabel(WeaponsTab, Loc.Get(TextKey.InvWeapons));
            SetTitle(Encampment.Site(Places.Armory).Name.Get());

            if (SelectedTab == ResourcesTab)
            {
                RedrawResources(game);
            }
            else
            {
                RedrawWeapons(game);
            }
        }

        private void RedrawResources(MetaGame game)
        {
            for (int i = 0; i < Kinds.Length; i++)
            {
                Currency kind = Kinds[i];
                kindAmount[i].SetText("{0}", game.Balance(kind));
                switch (kind)
                {
                    case Currency.Rations:
                        kindUse[i].text = Loc.Get(TextKey.CurRations) + " · " + Loc.Get(TextKey.InvRationsUse);
                        kindWhere[i].text = Loc.Format(TextKey.InvWhere, Encampment.Site(Places.Farm).Name.Get());
                        break;
                    case Currency.Scrap:
                        kindUse[i].text = Loc.Get(TextKey.CurScrap) + " · " + Loc.Get(TextKey.InvScrapUse);
                        kindWhere[i].text = Loc.Format(TextKey.InvWhere, Encampment.Site(Places.Mine).Name.Get());
                        break;
                    default:
                        kindUse[i].text = Loc.Get(TextKey.CurReales) + " · " + Loc.Get(TextKey.InvRealesUse);
                        kindWhere[i].text = Loc.Format(TextKey.InvWhere, Encampment.Site(Places.Exchange).Name.Get());
                        break;
                }
            }
        }

        private void RedrawWeapons(MetaGame game)
        {
            IReadOnlyList<OwnedWeapon> owned = game.Weapons;
            int pages = Mathf.Max(1, (owned.Count + PerPage - 1) / PerPage);
            page = Mathf.Clamp(page, 0, pages - 1);
            int first = page * PerPage;
            int shown = Mathf.Clamp(owned.Count - first, 0, PerPage);
            SyncWeaponRows(shown);
            pager.gameObject.SetActive(pages > 1);
            pageLabel.text = Loc.Format(TextKey.TrnPage, page + 1, pages);

            if (game.FindWeapon(selectedWeapon) == null)
            {
                selectedWeapon = shown > 0 ? owned[first].id : 0;
            }

            for (int i = 0; i < shown; i++)
            {
                OwnedWeapon item = owned[first + i];
                WeaponDef def = WeaponCatalog.Find(item.weapon);
                OwnedUnit holder = game.HolderOf(item.id);
                weaponIds[i] = item.id;

                TextMeshProUGUI[] labels = weaponRows[i].GetComponentsInChildren<TextMeshProUGUI>(true);
                labels[0].text = def != null ? def.Name.Get() : item.weapon;
                labels[1].text = (def != null ? Loc.Format(TextKey.InvTier, def.Tier) + " · " : string.Empty)
                    + (holder != null ? Loc.Format(TextKey.InvHeldBy, UnitName(holder)) : Loc.Get(TextKey.InvOnRack));
                weaponRims[i].color = item.id == selectedWeapon ? Theme.Gold : Theme.ParchmentDeep;
            }

            emptyNote.gameObject.SetActive(owned.Count == 0);
            emptyNote.text = Loc.Get(TextKey.InvNone);
            detail.gameObject.SetActive(owned.Count > 0);
            if (owned.Count == 0)
            {
                return;
            }

            OwnedWeapon weapon = game.FindWeapon(selectedWeapon);
            WeaponDef chosen = WeaponCatalog.Find(weapon.weapon);
            OwnedUnit carrier = game.HolderOf(weapon.id);
            weaponName.text = chosen != null ? chosen.Name.Get() : weapon.weapon;
            weaponFacts.text = chosen != null
                ? Loc.Format(TextKey.InvTier, chosen.Tier) + "   " + BonusText(chosen)
                : string.Empty;
            weaponNote.text = chosen != null ? chosen.Note.Get() : string.Empty;
            holderLine.text = carrier != null ? Loc.Format(TextKey.InvHeldBy, UnitName(carrier)) : Loc.Get(TextKey.InvOnRack);
            UnitArchetype only = chosen != null && chosen.RequiredArchetype != null ? UnitCatalog.Find(chosen.RequiredArchetype) : null;
            restrictLine.text = only != null ? Loc.Format(TextKey.InvOnlyFor, only.Name.Get()) : string.Empty;

            RebuildGiveRow(game, weapon, carrier);

            WeaponDef next = chosen != null && chosen.UpgradesTo != null ? WeaponCatalog.Find(chosen.UpgradesTo) : null;
            reforge.gameObject.SetActive(next != null);
            if (next == null)
            {
                reforgeLine.text = Loc.Get(chosen != null && chosen.RequiredArchetype != null ? TextKey.InvNoReforge : TextKey.InvTopTier);
                reforgeCost.text = string.Empty;
                return;
            }

            reforgeLine.text = Loc.Format(TextKey.InvReforgeInto, next.Name.Get());
            reforgeCost.text = CostText(chosen.SynthesisCost);
            reforgeCost.color = game.CanAfford(chosen.SynthesisCost) ? Theme.Ink : Theme.Danger;
            reforge.interactable = game.CanSynthesize(weapon);
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

        private static string UnitName(OwnedUnit unit)
        {
            UnitArchetype archetype = UnitCatalog.Find(unit.archetype);
            return archetype != null ? archetype.Name.Get() : unit.archetype;
        }

        /// <summary>Keeps one list row per owned weapon, adding rows as the armory grows.</summary>
        private void SyncWeaponRows(int count)
        {
            while (weaponRows.Count < count)
            {
                int index = weaponRows.Count;
                Image rim;
                Button row = UiKit.SelectableRow(weaponList, "Weapon " + index, out rim);
                row.transform.SetSiblingIndex(index + 1);
                UiLayout.Fix((RectTransform)row.transform, 0f, RowHeight);
                row.onClick.AddListener(() => SelectWeapon(index));

                RectTransform text = UiKit.Column(row.transform, "Text", 0f, 0f, TextAnchor.MiddleLeft);
                UiKit.Stretch(text);
                text.offsetMin = new Vector2(Theme.Space.Base, Theme.Space.Tight);
                text.offsetMax = new Vector2(-Theme.Space.Base, -Theme.Space.Tight);
                UiLayout.FillWidth(text);

                TextMeshProUGUI name = UiKit.Body(text, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Left);
                name.fontStyle = FontStyles.Bold;
                UiLayout.OneLine(name, Theme.Type.Heading);
                UiLayout.Fix(name.rectTransform, 0f, 28f);

                TextMeshProUGUI note = UiKit.Caption(text, string.Empty, TextAlignmentOptions.Left);
                UiLayout.OneLine(note, Theme.Type.Small);
                UiLayout.Fix(note.rectTransform, 0f, 22f);

                weaponRows.Add(row);
                weaponRims.Add(rim);
                weaponIds.Add(0);
            }

            for (int i = 0; i < weaponRows.Count; i++)
            {
                weaponRows[i].gameObject.SetActive(i < count);
            }
        }

        private void Turn(int step)
        {
            page += step;
            UiSfx.Play(UiSfx.Cue.Toggle);
            stale = true;
        }

        private void SelectWeapon(int row)
        {
            selectedWeapon = weaponIds[row];
            UiSfx.Play(UiSfx.Cue.Click);
            stale = true;
        }

        /// <summary>One portrait button per soldier; the current carrier's is lit gold.</summary>
        private void RebuildGiveRow(MetaGame game, OwnedWeapon weapon, OwnedUnit carrier)
        {
            for (int i = giveRow.childCount - 1; i >= 0; i--)
            {
                Destroy(giveRow.GetChild(i).gameObject);
            }

            IReadOnlyList<OwnedUnit> units = game.Units;
            for (int i = 0; i < units.Count; i++)
            {
                OwnedUnit unit = units[i];
                Image rim;
                Button button = UiKit.SelectableRow(giveRow, "Give " + unit.id, out rim);
                UiLayout.Fix((RectTransform)button.transform, 104f, 112f);
                rim.color = carrier != null && carrier.id == unit.id ? Theme.Gold : Theme.ParchmentDeep;
                bool allowed = game.CanWield(unit, weapon);
                button.interactable = allowed;
                int unitId = unit.id;
                int weaponId = weapon.id;
                button.onClick.AddListener(() => Equip(unitId, weaponId));

                Sprite face = Theme.Assets != null ? Theme.Assets.UnitPortrait(unit.archetype) : null;
                Image portrait = UiKit.Icon(button.transform, face, 72f, Color.white);
                UiKit.Anchor((RectTransform)portrait.transform.parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(72f, 72f));
                portrait.enabled = face != null;
                if (!allowed)
                {
                    portrait.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                }

                UnitArchetype archetype = UnitCatalog.Find(unit.archetype);
                TextMeshProUGUI label = UiKit.Caption(button.transform, archetype != null ? archetype.ShortName : unit.archetype, TextAlignmentOptions.Center);
                label.fontStyle = FontStyles.Bold;
                UiKit.Anchor(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(100f, 24f));
                UiLayout.OneLine(label, Theme.Type.Small);
                if (!allowed)
                {
                    label.color = Theme.InkSoft;
                }
            }
        }

        // ------------------------------------------------------------------ actions

        private void Equip(int unitId, int weaponId)
        {
            if (Game == null)
            {
                return;
            }

            OwnedUnit holder = Game.HolderOf(weaponId);
            if (holder != null && holder.id == unitId)
            {
                return;
            }

            if (!Game.TryEquip(unitId, weaponId))
            {
                UiSfx.Play(UiSfx.Cue.Error);
                OwnedWeapon refused = Game.FindWeapon(weaponId);
                WeaponDef refusedDef = refused != null ? WeaponCatalog.Find(refused.weapon) : null;
                if (refusedDef != null && Game.FindUnit(unitId) != null)
                {
                    UiControls.Toast(Loc.Format(TextKey.InvRefused, UnitName(Game.FindUnit(unitId)), refusedDef.Name.Get()));
                }

                return;
            }

            WeaponDef def = WeaponCatalog.Find(Game.FindWeapon(weaponId).weapon);
            UiSfx.Play(UiSfx.Cue.Confirm);
            UiControls.Toast(Loc.Format(TextKey.InvEquipped, UnitName(Game.FindUnit(unitId)), def != null ? def.Name.Get() : string.Empty));
        }

        private void Reforge()
        {
            if (Game == null)
            {
                return;
            }

            OwnedWeapon weapon = Game.FindWeapon(selectedWeapon);
            if (!Game.TrySynthesize(selectedWeapon))
            {
                UiSfx.Play(UiSfx.Cue.Error);
                UiControls.Toast(Loc.Get(TextKey.InvCantAfford));
                return;
            }

            WeaponDef def = WeaponCatalog.Find(weapon.weapon);
            UiSfx.Play(UiSfx.Cue.Victory);
            UiControls.Toast(Loc.Format(TextKey.InvReforged, def != null ? def.Name.Get() : weapon.weapon));
            CoroutineHost.Run(UiTween.Punch(weaponName.transform));
        }
    }
}
