using BinakayanRising.Core.Grid;
using UnityEngine;

namespace BinakayanRising.Gameplay
{
    public sealed partial class BattlePlaytest
    {
        // Lit-tile colours while a unit is carried. Blue marks every tile the drop would take;
        // the tile under the pointer is brighter so the landing spot reads at a glance.
        private static readonly Color DragValidTint = new Color(0.36f, 0.62f, 1f, 0.9f);
        private static readonly Color DragHoverTint = new Color(0.62f, 0.84f, 1f, 1f);
        private static readonly Color DragRefusedTint = new Color(1f, 1f, 1f, 0.25f);

        /// <summary>Pixels the pointer must travel with the button held before a press becomes a drag.</summary>
        private const float BoardDragThreshold = 10f;

        private int dragUnitId = -1;
        private bool dragFromBoard;
        private Vector2 dragScreen;
        private GridCoord dragHover = new GridCoord(int.MinValue, int.MinValue);
        private bool dragHoverValid;

        // A left press on a placed unit: a click lifts it on release, a drag carries it.
        private int boardPressUnit = -1;
        private GridCoord boardPressCell;
        private Vector2 boardPressScreen;

        /// <summary>Raised when a carried unit is put down somewhere that does not take it.</summary>
        public event System.Action<DropVerdict> DropRefused;

        /// <summary>The roster unit being carried, or -1.</summary>
        public int DraggingUnitId => dragUnitId;

        /// <summary>Where the carried unit is, in screen pixels.</summary>
        public Vector2 DragScreenPosition => dragScreen;

        /// <summary>True while the carried unit is over a tile that would take it.</summary>
        public bool DragOverValidCell => dragUnitId >= 0 && dragHoverValid;

        /// <summary>
        /// Picks up a roster unit — from the reserve or off the board — and lights the tiles it
        /// may be put down on.
        /// </summary>
        /// <returns>False outside deployment, while board input is locked, or for an unknown unit.</returns>
        public bool BeginDrag(int unitId)
        {
            return BeginDragFrom(unitId, fromBoard: false);
        }

        /// <summary>Moves the carried unit to a screen point and relights the tile under it.</summary>
        public void UpdateDrag(Vector2 screen)
        {
            if (dragUnitId < 0)
            {
                return;
            }

            dragScreen = screen;
            GridCoord cell;
            bool onBoard = TryScreenToCell(screen, out cell) && !UiPointer.IsOverUi(screen);
            GridCoord hover = onBoard ? cell : new GridCoord(int.MinValue, int.MinValue);
            if (hover == dragHover)
            {
                return;
            }

            dragHover = hover;
            DropVerdict verdict = onBoard ? DeploymentDrop.Judge(grid, placements, dragUnitId, cell, SquadCap, FixedCells) : DropVerdict.OutOfBounds;
            dragHoverValid = DeploymentDrop.Accepts(verdict) || verdict == DropVerdict.Unchanged;
            PaintDragHighlights();
        }

        /// <summary>
        /// Puts the carried unit down at a screen point: on a tile that takes it, it is placed
        /// exactly as a click would place it; anywhere else it goes back where it came from and
        /// <see cref="DropRefused"/> is raised. A unit carried off the board and dropped on the
        /// interface returns to the reserve.
        /// </summary>
        /// <returns>True when the unit now stands on the tile under the point.</returns>
        public bool EndDrag(Vector2 screen)
        {
            if (dragUnitId < 0)
            {
                return false;
            }

            int unitId = dragUnitId;
            bool fromBoard = dragFromBoard;
            StopDrag();

            if (phase != Phase.Deployment || boardInputLocked)
            {
                return false;
            }

            if (UiPointer.IsOverUi(screen))
            {
                // Dropped back on the roster: the natural "take it off the line" gesture.
                GridCoord standing;
                if (fromBoard && placements.TryGetValue(unitId, out standing))
                {
                    RequestLift(standing);
                }

                return false;
            }

            GridCoord cell;
            DropVerdict verdict = TryScreenToCell(screen, out cell)
                ? DeploymentDrop.Judge(grid, placements, unitId, cell, SquadCap, FixedCells)
                : DropVerdict.OutOfBounds;

            if (verdict == DropVerdict.Unchanged)
            {
                return true;
            }

            if (!DeploymentDrop.Accepts(verdict))
            {
                DropRefused?.Invoke(verdict);
                return false;
            }

            // Through the click path, so a drop and a click can never place differently.
            int previous = selectedSlot;
            SetSelectionSilently(IndexOfEntry(unitId));
            if (RequestPlace(cell))
            {
                return true;
            }

            SetSelectionSilently(previous);
            DropRefused?.Invoke(verdict);
            return false;
        }

        /// <summary>Drops the carried unit back where it came from, silently.</summary>
        public void CancelDrag()
        {
            boardPressUnit = -1;
            if (dragUnitId >= 0)
            {
                StopDrag();
            }
        }

        /// <summary>The cell under a screen point, if the point is over the map.</summary>
        public bool TryScreenToCell(Vector2 screen, out GridCoord cell)
        {
            cell = default(GridCoord);
            if (view == null || grid == null || layout == null)
            {
                return false;
            }

            Vector3 world = view.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            cell = layout.WorldToCell(new IsoVector(world.x, world.y));
            return grid.InBounds(cell);
        }

        /// <summary>
        /// Carries a board unit once the pointer has pulled away from the press, and resolves the
        /// press on release: a drag drops it, a plain click lifts it as it always has.
        /// </summary>
        /// <returns>True while a press on a placed unit owns the pointer.</returns>
        private bool HandleBoardPress(UnityEngine.InputSystem.Mouse mouse, Vector2 screen)
        {
            if (boardPressUnit < 0)
            {
                return false;
            }

            if (mouse.leftButton.isPressed)
            {
                if (dragUnitId < 0 && (screen - boardPressScreen).sqrMagnitude > BoardDragThreshold * BoardDragThreshold)
                {
                    BeginDragFrom(boardPressUnit, fromBoard: true);
                }

                UpdateDrag(screen);
                return true;
            }

            boardPressUnit = -1;
            if (dragUnitId >= 0)
            {
                EndDrag(screen);
            }
            else
            {
                RequestLift(boardPressCell);
            }

            return true;
        }

        /// <summary>Remembers a left press on a placed unit, deciding click or drag later.</summary>
        private bool TryPressPlacedUnit(GridCoord cell, Vector2 screen)
        {
            foreach (System.Collections.Generic.KeyValuePair<int, GridCoord> placement in placements)
            {
                if (placement.Value == cell)
                {
                    boardPressUnit = placement.Key;
                    boardPressCell = cell;
                    boardPressScreen = screen;
                    return true;
                }
            }

            return false;
        }

        private bool BeginDragFrom(int unitId, bool fromBoard)
        {
            if (phase != Phase.Deployment || boardInputLocked || grid == null || IndexOfEntry(unitId) < 0)
            {
                return false;
            }

            dragUnitId = unitId;
            dragFromBoard = fromBoard;
            dragHover = new GridCoord(int.MinValue, int.MinValue);
            dragHoverValid = false;
            PaintDragHighlights();
            return true;
        }

        private void StopDrag()
        {
            dragUnitId = -1;
            dragFromBoard = false;
            dragHoverValid = false;
            dragHover = new GridCoord(int.MinValue, int.MinValue);
            PaintDragHighlights();
        }

        /// <summary>
        /// Tints every lit tile: blue where the carried unit may land, faded where it may not, and
        /// the resting colour when nothing is carried.
        /// </summary>
        private void PaintDragHighlights()
        {
            Color resting = BoardArt.DeployMarkerIsThemed ? Color.white : new Color(1f, 1f, 1f, 0.18f);
            for (int i = 0; i < deployHighlights.Count && i < deployHighlightCells.Count; i++)
            {
                SpriteRenderer marker = deployHighlights[i];
                if (marker == null)
                {
                    continue;
                }

                if (dragUnitId < 0)
                {
                    marker.color = resting;
                    continue;
                }

                GridCoord cell = deployHighlightCells[i];
                DropVerdict verdict = DeploymentDrop.Judge(grid, placements, dragUnitId, cell, SquadCap, FixedCells);
                bool takes = DeploymentDrop.Accepts(verdict) || verdict == DropVerdict.Unchanged;
                marker.color = !takes ? DragRefusedTint : (cell == dragHover ? DragHoverTint : DragValidTint);
            }
        }
    }
}
