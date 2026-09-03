using System;
using UnityEngine;
using BinakayanRising.Core.Grid;
using BinakayanRising.Data;

namespace BinakayanRising.Gameplay.Deployment
{
    /// <summary>
    /// Drags a hero portrait from the roster strip onto the isometric board: a ghost preview that
    /// follows the pointer and snaps to the hovered cell, left-click to confirm, right-click to
    /// cancel, and a refusal with feedback on any illegal cell.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The capstone document specifies drag-and-drop deployment onto highlighted blue tiles, with
    /// left-click confirming a placement and right-click cancelling an action. This class implements
    /// pick-then-place: a left-click on a roster portrait (or on an already-deployed unit) picks it
    /// up, the ghost then tracks the pointer with strict grid snapping, and the next left-click
    /// confirms. Right-click at any point cancels and returns the unit to the strip.
    /// </para>
    /// <para>
    /// It rules on nothing itself. Every accept/refuse decision comes from
    /// <see cref="DeploymentController.Validate"/>, and every cell comes from
    /// <see cref="GridRaycaster"/>, which has already rejected pointer positions that were over UI.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class UnitDragHandler : MonoBehaviour
    {
        [Header("Scene References")]
        [Tooltip("Owns the placements and the placement rules.")]
        [SerializeField] private DeploymentController controller;

        [Tooltip("Turns pointer positions into grid cells and rejects UI hits.")]
        [SerializeField] private GridRaycaster raycaster;

        [Tooltip("Draws the blue zone, the hover highlight and the invalid-drop flash.")]
        [SerializeField] private DeploymentZoneView zoneView;

        [Tooltip("The bottom-of-screen portrait strip a drag starts from.")]
        [SerializeField] private RosterStripView rosterStrip;

        [Tooltip("Supplies the document's left-click / right-click control scheme.")]
        [SerializeField] private DeploymentInputActions input;

        [Header("Ghost Preview")]
        [Tooltip("Optional SpriteRenderer used as the drag ghost. Leave empty to build one at runtime.")]
        [SerializeField] private SpriteRenderer ghostRenderer;

        [Tooltip("Sorting layer of the runtime-built ghost.")]
        [SerializeField] private string ghostSortingLayerName = "Default";

        [Tooltip("Sorting order of the runtime-built ghost. Keep it above the highlight tiles.")]
        [SerializeField] private int ghostSortingOrder = 500;

        [Tooltip("Ghost tint while it hovers a legal, free tile.")]
        [SerializeField] private Color ghostValidColor = new Color(1f, 1f, 1f, 0.85f);

        [Tooltip("Ghost tint while it hovers a cell that would be refused.")]
        [SerializeField] private Color ghostInvalidColor = new Color(1f, 0.4f, 0.4f, 0.6f);

        [Tooltip("Uniform scale of the ghost sprite. TODO(design): not specified in capstone document.")]
        [Min(0.01f)]
        [SerializeField] private float ghostScale = 1f;

        [Tooltip("World-space vertical offset so the ghost stands on the tile rather than in it. " +
                 "TODO(design): not specified in capstone document.")]
        [SerializeField] private float ghostVerticalOffset = 0.2f;

        [Header("Behaviour")]
        [Tooltip("Left-clicking a deployed unit picks it back up so it can be re-placed.")]
        [SerializeField] private bool leftClickPicksUpPlacedUnits = true;

        [Tooltip("Right-clicking a deployed unit removes it, per the document's 'right-click cancels' rule. " +
                 "TODO(design): the document does not say how a placed unit is taken back off the board.")]
        [SerializeField] private bool rightClickRemovesPlacedUnits = true;

        private UnitData draggedUnit;
        private GridCoord hoveredCell;
        private bool hasHoveredCell;
        private bool hoveredCellIsValid;
        private SpriteRenderer runtimeGhost;

        /// <summary>Raised when a drag begins, carrying the picked unit.</summary>
        public event Action<UnitData> DragStarted;

        /// <summary>Raised when a drag ends without a placement, carrying the cancelled unit.</summary>
        public event Action<UnitData> DragCancelled;

        /// <summary>Raised when a drag ends in an accepted placement.</summary>
        public event Action<UnitPlacement> DragConfirmed;

        /// <summary>The unit currently being dragged, or null when no drag is in progress.</summary>
        public UnitData DraggedUnit
        {
            get { return draggedUnit; }
        }

        /// <summary>True while a portrait is attached to the pointer.</summary>
        public bool IsDragging
        {
            get { return draggedUnit != null; }
        }

        /// <summary>True while the ghost is snapped to a cell on the board.</summary>
        public bool HasHoveredCell
        {
            get { return hasHoveredCell; }
        }

        /// <summary>The cell the ghost is snapped to. Only meaningful while <see cref="HasHoveredCell"/> is true.</summary>
        public GridCoord HoveredCell
        {
            get { return hoveredCell; }
        }

        /// <summary>
        /// True when confirming right now would be accepted, i.e. the hovered cell passes
        /// <see cref="DeploymentController.Validate"/>.
        /// </summary>
        public bool IsHoveredCellValid
        {
            get { return hasHoveredCell && hoveredCellIsValid; }
        }

        /// <summary>
        /// Picks a unit up and attaches it to the pointer. A unit that is already deployed is lifted
        /// off its cell first, so confirming elsewhere moves it rather than duplicating it.
        /// </summary>
        /// <param name="unit">Unit to drag. Null or a locked formation is ignored.</param>
        public void BeginDrag(UnitData unit)
        {
            if (unit == null || controller == null || controller.IsFormationLocked)
            {
                return;
            }

            if (IsDragging)
            {
                CancelDrag();
            }

            draggedUnit = unit;

            if (controller.IsPlaced(unit))
            {
                controller.RemoveUnit(unit);
            }

            ShowGhost(unit);

            Action<UnitData> handler = DragStarted;

            if (handler != null)
            {
                handler(unit);
            }
        }

        /// <summary>
        /// Ends the drag with no placement — the document's right-click cancel. The unit simply
        /// returns to the roster strip.
        /// </summary>
        public void CancelDrag()
        {
            if (!IsDragging)
            {
                return;
            }

            UnitData cancelled = draggedUnit;
            EndDrag();

            Action<UnitData> handler = DragCancelled;

            if (handler != null)
            {
                handler(cancelled);
            }
        }

        /// <summary>
        /// Tries to drop the dragged unit on a cell — the document's left-click confirm. An illegal
        /// cell flashes the invalid feedback state and leaves the drag running.
        /// </summary>
        /// <param name="cell">Cell to drop onto.</param>
        /// <returns>True when the placement was accepted and the drag ended.</returns>
        public bool TryConfirmAt(GridCoord cell)
        {
            if (!IsDragging || controller == null)
            {
                return false;
            }

            UnitData unit = draggedUnit;

            if (!controller.TryPlace(unit, cell))
            {
                if (zoneView != null)
                {
                    zoneView.FlashInvalid(cell);
                }

                return false;
            }

            EndDrag();

            Action<UnitPlacement> handler = DragConfirmed;

            if (handler != null)
            {
                handler(new UnitPlacement(unit, cell));
            }

            return true;
        }

        private void OnEnable()
        {
            if (rosterStrip != null)
            {
                rosterStrip.EntryPicked += OnRosterEntryPicked;
            }

            if (input != null)
            {
                input.LeftClicked += OnLeftClicked;
                input.RightClicked += OnRightClicked;
            }
        }

        private void OnDisable()
        {
            if (rosterStrip != null)
            {
                rosterStrip.EntryPicked -= OnRosterEntryPicked;
            }

            if (input != null)
            {
                input.LeftClicked -= OnLeftClicked;
                input.RightClicked -= OnRightClicked;
            }

            CancelDrag();
        }

        private void Update()
        {
            if (!IsDragging)
            {
                return;
            }

            UpdateGhost();
        }

        private void OnRosterEntryPicked(UnitData unit)
        {
            BeginDrag(unit);
        }

        private void OnLeftClicked(Vector2 screenPosition)
        {
            if (controller == null || controller.IsFormationLocked || raycaster == null)
            {
                return;
            }

            if (raycaster.IsPointerOverUI(screenPosition))
            {
                // The roster strip's own Button handles this click; the board must ignore it.
                return;
            }

            GridCoord cell;

            if (!raycaster.TryGetCellAt(screenPosition, out cell))
            {
                if (IsDragging && zoneView != null)
                {
                    zoneView.ClearHover();
                }

                return;
            }

            if (IsDragging)
            {
                TryConfirmAt(cell);
                return;
            }

            if (!leftClickPicksUpPlacedUnits)
            {
                return;
            }

            UnitData occupant;

            if (controller.TryGetUnitAt(cell, out occupant))
            {
                BeginDrag(occupant);
            }
        }

        private void OnRightClicked(Vector2 screenPosition)
        {
            if (IsDragging)
            {
                CancelDrag();
                return;
            }

            if (!rightClickRemovesPlacedUnits || controller == null || controller.IsFormationLocked)
            {
                return;
            }

            if (raycaster == null || raycaster.IsPointerOverUI(screenPosition))
            {
                return;
            }

            GridCoord cell;

            if (raycaster.TryGetCellAt(screenPosition, out cell))
            {
                controller.RemoveAt(cell);
            }
        }

        private void UpdateGhost()
        {
            SpriteRenderer ghost = ActiveGhost();

            if (ghost == null || raycaster == null)
            {
                return;
            }

            Vector2 screenPosition = raycaster.PointerPosition;
            bool overUi = raycaster.IsPointerOverUI(screenPosition);

            GridCoord cell = GridCoord.Zero;
            bool onBoard = false;

            if (!overUi)
            {
                onBoard = raycaster.TryGetCellAt(screenPosition, out cell);
            }

            if (!onBoard)
            {
                hasHoveredCell = false;
                hoveredCellIsValid = false;

                if (zoneView != null)
                {
                    zoneView.ClearHover();
                }

                // Off the board, the ghost follows the raw pointer instead of snapping, so the
                // player can see it is not over a legal tile.
                Vector3 loose = raycaster.ScreenToBoardPlane(screenPosition);
                ghost.transform.position = new Vector3(loose.x, loose.y + ghostVerticalOffset, loose.z);
                ghost.color = ghostInvalidColor;
                return;
            }

            hoveredCell = cell;
            hasHoveredCell = true;
            hoveredCellIsValid = controller != null
                && controller.Validate(draggedUnit, cell) == PlacementValidation.Valid;

            if (zoneView != null)
            {
                zoneView.SetHoveredCell(cell);
            }

            // Strict orthographic snapping: the ghost sits on the cell centre, never between cells.
            Vector3 snapped = raycaster.CellToWorld(cell);
            ghost.transform.position = new Vector3(snapped.x, snapped.y + ghostVerticalOffset, snapped.z);
            ghost.color = hoveredCellIsValid ? ghostValidColor : ghostInvalidColor;
        }

        private void ShowGhost(UnitData unit)
        {
            SpriteRenderer ghost = ActiveGhost();

            if (ghost == null)
            {
                return;
            }

            ghost.sprite = unit.Portrait != null ? unit.Portrait : PlaceholderArt.Token;
            ghost.color = ghostInvalidColor;
            ghost.transform.localScale = new Vector3(ghostScale, ghostScale, 1f);
            ghost.enabled = true;
        }

        private void HideGhost()
        {
            SpriteRenderer ghost = ActiveGhost();

            if (ghost != null)
            {
                ghost.enabled = false;
            }
        }

        private SpriteRenderer ActiveGhost()
        {
            if (ghostRenderer != null)
            {
                return ghostRenderer;
            }

            if (runtimeGhost == null)
            {
                GameObject host = new GameObject("Deployment Drag Ghost");
                host.transform.SetParent(transform, false);

                runtimeGhost = host.AddComponent<SpriteRenderer>();
                runtimeGhost.sortingLayerName = ghostSortingLayerName;
                runtimeGhost.sortingOrder = ghostSortingOrder;
                runtimeGhost.enabled = false;
            }

            return runtimeGhost;
        }

        private void EndDrag()
        {
            draggedUnit = null;
            hasHoveredCell = false;
            hoveredCellIsValid = false;

            HideGhost();

            if (zoneView != null)
            {
                zoneView.ClearHover();
            }
        }
    }
}
