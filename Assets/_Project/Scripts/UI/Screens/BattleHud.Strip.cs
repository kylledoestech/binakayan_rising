using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
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
        private const float StripHeight = 124f;
        private const float StripCellWidth = 68f;
        private const float StripCellHeight = 80f;
        private const float GhostSize = 76f;

        /// <summary>Alpha of a portrait whose unit is already on the board.</summary>
        private const float PlacedAlpha = 0.4f;

        private readonly List<StripCell> stripCells = new List<StripCell>();

        private RectTransform strip;
        private Canvas ghostCanvas;
        private RectTransform ghost;
        private Image ghostPortrait;
        private Image ghostRim;
        private int ghostUnit = -1;

        /// <summary>One portrait in the roster strip.</summary>
        private sealed class StripCell
        {
            public int Id;
            public CanvasGroup Group;
            public Image Rim;
            public TextMeshProUGUI Name;
            public int ShownState = -1;
        }

        /// <summary>
        /// The roster as a row of portraits along the bottom edge, between the side panel and the
        /// minimap: the drag-and-drop half of deployment. Click-to-place in the side panel stays.
        /// </summary>
        private void BuildRosterStrip(Transform parent)
        {
            strip = Register("strip", UiKit.Panel(parent, "Roster Strip"));
            strip.anchorMin = new Vector2(0f, 0f);
            strip.anchorMax = new Vector2(1f, 0f);
            strip.pivot = new Vector2(0.5f, 0f);
            strip.offsetMin = new Vector2((2f * Theme.Space.Base) + SidePanelWidth, Theme.Space.Base);
            strip.offsetMax = new Vector2(
                -(LogWidth + BattleMinimap.Width + (3f * Theme.Space.Base)), Theme.Space.Base + StripHeight);

            TextMeshProUGUI hint = UiKit.Caption(strip, Loc.Get(TextKey.DeployStripHint), TextAlignmentOptions.Center);
            hint.color = Theme.RevolutionDark;
            hint.fontStyle = FontStyles.Bold;
            FitLine(hint, Theme.Type.Small);
            UiKit.Localize(hint, TextKey.DeployStripHint);
            hint.rectTransform.anchorMin = new Vector2(0f, 1f);
            hint.rectTransform.anchorMax = new Vector2(1f, 1f);
            hint.rectTransform.pivot = new Vector2(0.5f, 1f);
            hint.rectTransform.offsetMin = new Vector2(Theme.Space.Wide, -(Theme.Space.Tight + 20f));
            hint.rectTransform.offsetMax = new Vector2(-Theme.Space.Wide, -Theme.Space.Tight);

            // A campaign roster grows with recruiting; past what fits, the wheel scrolls it.
            RectTransform viewport = UiKit.NewRect(strip, "Viewport");
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(Theme.Space.Base, Theme.Space.Tight);
            viewport.offsetMax = new Vector2(-Theme.Space.Base, -(Theme.Space.Tight + 22f));
            viewport.gameObject.AddComponent<RectMask2D>();

            // Sized to its portraits and centred, so a short roster sits in the middle of the
            // strip; a long one is wider than the viewport and the wheel scrolls it.
            RectTransform row = UiKit.Row(viewport, "Portraits", Theme.Space.Tight, 0f, TextAnchor.MiddleCenter);
            Pin(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = strip.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.scrollSensitivity = 32f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = viewport;
            scroll.content = row;

            IReadOnlyList<RosterEntry> entries = battle.Roster;
            for (int i = 0; i < entries.Count; i++)
            {
                stripCells.Add(BuildStripCell(row, entries[i], i));
            }
        }

        private StripCell BuildStripCell(Transform parent, RosterEntry entry, int index)
        {
            RectTransform cell = UiKit.NewRect(parent, "Portrait " + entry.ShortName);
            FixWidth(cell, StripCellWidth);
            FixHeight(cell, StripCellHeight);
            Register("strip." + index, cell);

            RectTransform fill = UiKit.Well(cell, "Fill", blocksClicks: true);
            UiKit.Stretch(fill);

            RectTransform rimRect = UiKit.Frame(cell, "Rim", Theme.ParchmentDeep);
            UiKit.Stretch(rimRect);

            Image portrait = UiKit.Icon(cell, null, PortraitSize, Color.white);
            portrait.name = "Portrait";
            RectTransform frame = (RectTransform)portrait.transform.parent;
            Pin(frame, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -Theme.Space.Hair - 2f),
                new Vector2(PortraitSize, PortraitSize));
            SetPortrait(portrait, entry.ArchetypeId, Team.Katipunan);

            TextMeshProUGUI name = UiKit.Caption(cell, entry.ShortName, TextAlignmentOptions.Center);
            name.color = Theme.Ink;
            name.fontStyle = FontStyles.Bold;
            FitLine(name, Theme.Type.Small);
            Pin(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, Theme.Space.Hair),
                new Vector2(StripCellWidth - 4f, 22f));

            var handler = cell.gameObject.AddComponent<RosterStripCell>();
            handler.Slot = index;
            handler.Pressed = OnStripPressed;
            handler.DragBegan = OnStripDragBegan;
            handler.Dragged = OnStripDragged;
            handler.DragEnded = OnStripDragEnded;

            return new StripCell
            {
                Id = entry.Id,
                Group = UiKit.Group(cell.gameObject),
                Rim = rimRect.GetComponentInChildren<Image>(),
                Name = name,
            };
        }

        /// <summary>
        /// The portrait carried under the pointer. On its own canvas above the tutorial's, so the
        /// spotlight's dimming never falls across the thing being carried.
        /// </summary>
        private void BuildDragGhost()
        {
            ghostCanvas = UiKit.Screen("Drag Ghost", Theme.Layer.Tutorial + 5, raycaster: false);
            ghostCanvas.transform.SetParent(transform, worldPositionStays: false);

            ghost = UiKit.NewRect(ghostCanvas.transform, "Ghost");
            Pin(ghost, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(GhostSize, GhostSize));

            RectTransform fill = UiKit.Well(ghost, "Fill");
            UiKit.Stretch(fill);

            RectTransform rim = UiKit.Frame(ghost, "Rim", Theme.GoldBright);
            UiKit.Stretch(rim);
            ghostRim = rim.GetComponentInChildren<Image>();

            ghostPortrait = UiKit.Icon(ghost, null, PortraitSize, Color.white);
            UiKit.Stretch((RectTransform)ghostPortrait.transform.parent, (GhostSize - PortraitSize) * 0.5f);

            CanvasGroup group = UiKit.Group(ghost.gameObject);
            group.alpha = 0.9f;
            group.blocksRaycasts = false;
            group.interactable = false;

            ghost.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ strip input

        private void OnStripPressed(int slot)
        {
            if (battle.CurrentPhase != BattlePlaytest.Phase.Deployment)
            {
                return;
            }

            UiSfx.Play(UiSfx.Cue.Click);
            battle.SelectSlot(slot);

            // The same id as the side panel's row: to the tutorial, picking a unit is picking a
            // unit whichever list it was picked from.
            Activated("roster." + slot);
        }

        private void OnStripDragBegan(int slot, Vector2 screen)
        {
            IReadOnlyList<RosterEntry> entries = battle.Roster;
            if (slot < 0 || slot >= entries.Count || !battle.BeginDrag(entries[slot].Id))
            {
                return;
            }

            UiSfx.Play(UiSfx.Cue.Toggle);
            battle.UpdateDrag(screen);
        }

        private void OnStripDragged(Vector2 screen)
        {
            battle.UpdateDrag(screen);
        }

        private void OnStripDragEnded(Vector2 screen)
        {
            battle.EndDrag(screen);
        }

        private void OnDropRefused(DropVerdict verdict)
        {
            UiSfx.Play(UiSfx.Cue.Error);
        }

        // ------------------------------------------------------------------ refresh

        /// <summary>Dims the portraits of units already on the board and rims the selected one.</summary>
        private void RefreshStrip()
        {
            if (strip == null || !strip.gameObject.activeSelf)
            {
                return;
            }

            IReadOnlyList<RosterEntry> entries = battle.Roster;
            for (int i = 0; i < stripCells.Count && i < entries.Count; i++)
            {
                StripCell cell = stripCells[i];
                bool placed = battle.IsPlaced(cell.Id);
                bool selected = i == battle.SelectedSlot;
                int state = (placed ? 1 : 0) | (selected ? 2 : 0);
                if (cell.ShownState == state)
                {
                    continue;
                }

                cell.ShownState = state;
                cell.Group.alpha = placed ? PlacedAlpha : 1f;
                cell.Rim.color = selected ? Theme.GoldBright : Theme.ParchmentDeep;
                cell.Name.color = placed ? Theme.Success : Theme.Ink;
            }
        }

        /// <summary>Keeps the carried portrait under the pointer, gold over a tile that takes it.</summary>
        private void RefreshDragGhost()
        {
            if (ghost == null)
            {
                return;
            }

            int carried = battle.DraggingUnitId;
            bool show = carried >= 0;
            if (ghost.gameObject.activeSelf != show)
            {
                ghost.gameObject.SetActive(show);
            }

            if (!show)
            {
                ghostUnit = -1;
                return;
            }

            if (ghostUnit != carried)
            {
                ghostUnit = carried;
                IReadOnlyList<RosterEntry> entries = battle.Roster;
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].Id == carried)
                    {
                        SetPortrait(ghostPortrait, entries[i].ArchetypeId, Team.Katipunan);
                        break;
                    }
                }
            }

            ghostRim.color = battle.DragOverValidCell ? Theme.GoldBright : Theme.Revolution;

            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)ghostCanvas.transform, battle.DragScreenPosition, null, out local))
            {
                ghost.anchoredPosition = local;
            }
        }
    }
}
