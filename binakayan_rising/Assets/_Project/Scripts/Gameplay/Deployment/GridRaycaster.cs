using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using BinakayanRising.Core.Grid;

namespace BinakayanRising.Gameplay.Deployment
{
    /// <summary>
    /// Turns a screen-space pointer position into a <see cref="GridCoord"/> on the isometric board,
    /// with strict orthographic snapping, and rejects pointer positions that are over UI rather than
    /// over the board.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The capstone document specifies that "placement detection is handled by raycasting from the
    /// mouse pointer onto grid tiles and UI hitboxes" with "strict orthographic grid snapping".
    /// This class is both halves of that: the UI hitbox test runs first through the
    /// <see cref="EventSystem"/>, and only if nothing UI was hit does the board test run.
    /// </para>
    /// <para>
    /// <b>No physics colliders are involved.</b> Snapping is analytic:
    /// <see cref="Camera.ScreenToWorldPoint"/> followed by <see cref="IsoGridLayout.WorldToCell"/>,
    /// which is the exact inverse of the projection the board is drawn with. That is what makes the
    /// snap "strict" — every screen pixel maps to exactly one cell, with no collider gaps, no
    /// tolerance radius and no dependence on tile art. It also means the projection here must match
    /// the one <see cref="DeploymentZoneView"/> draws with; inject both from one owner via
    /// <see cref="SetLayout"/>.
    /// </para>
    /// <para>
    /// Pointer input comes from the new Input System (<c>UnityEngine.InputSystem.Pointer</c>), never
    /// from legacy <c>Input.mousePosition</c>.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class GridRaycaster : MonoBehaviour
    {
        [Header("Scene References")]
        [Tooltip("Camera the board is viewed through. Leave empty to use Camera.main at Awake.")]
        [SerializeField] private Camera boardCamera;

        [Header("Isometric Projection")]
        // TODO(design): not specified in capstone document - no tile size is published. Defaults to
        // the classic 2:1 diamond documented on IsoGridLayout. Must match DeploymentZoneView.
        [Tooltip("Full width of one tile diamond in world units. TODO(design): unspecified in the document.")]
        [Min(0.0001f)]
        [SerializeField] private float tileWidth = IsoGridLayout.DefaultTileWidth;

        [Tooltip("Full height of one tile diamond in world units. TODO(design): unspecified in the document.")]
        [Min(0.0001f)]
        [SerializeField] private float tileHeight = IsoGridLayout.DefaultTileHeight;

        [Tooltip("World position of grid cell (0, 0). Must match DeploymentZoneView.")]
        [SerializeField] private Vector2 boardOrigin = Vector2.zero;

        [Header("Behaviour")]
        [Tooltip("Reject cells that fall outside the bound grid. Turn off to allow probing off-board cells.")]
        [SerializeField] private bool requireInBounds = true;

        [Tooltip("Reject pointer positions that land on a UI hitbox, per the document's control scheme.")]
        [SerializeField] private bool blockWhenPointerOverUI = true;

        [Tooltip("Log a warning when the camera is not orthographic. The document requires strict " +
                 "orthographic snapping, which a perspective camera cannot provide.")]
        [SerializeField] private bool warnOnPerspectiveCamera = true;

        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        private IsoGridLayout layout;
        private IBattleGrid grid;
        private PointerEventData pointerEventData;

        /// <summary>The projection used to invert screen positions into cells. Never null.</summary>
        public IsoGridLayout Layout
        {
            get
            {
                if (layout == null)
                {
                    layout = new IsoGridLayout(tileWidth, tileHeight);
                }

                return layout;
            }
        }

        /// <summary>The grid used for the bounds test, or null while none is bound.</summary>
        public IBattleGrid Grid
        {
            get { return grid; }
        }

        /// <summary>World position of grid cell <c>(0, 0)</c>.</summary>
        public Vector2 BoardOrigin
        {
            get { return boardOrigin; }
        }

        /// <summary>The camera screen positions are unprojected through.</summary>
        public Camera BoardCamera
        {
            get { return boardCamera; }
        }

        /// <summary>
        /// The current pointer position in screen pixels, or <see cref="Vector2.zero"/> when no
        /// pointer device is present.
        /// </summary>
        public Vector2 PointerPosition
        {
            get
            {
                Vector2 position;
                return TryGetPointerPosition(out position) ? position : Vector2.zero;
            }
        }

        /// <summary>Binds the grid used for the in-bounds test.</summary>
        /// <param name="battleGrid">The mission's grid. Null disables the bounds test.</param>
        public void SetGrid(IBattleGrid battleGrid)
        {
            grid = battleGrid;
        }

        /// <summary>
        /// Overrides the projection so this raycaster and the board view share one identical layout.
        /// </summary>
        /// <param name="value">The projection to invert with. Ignored when null.</param>
        /// <param name="origin">World position of grid cell <c>(0, 0)</c>.</param>
        public void SetLayout(IsoGridLayout value, Vector2 origin)
        {
            if (value == null)
            {
                return;
            }

            layout = value;
            boardOrigin = origin;
        }

        /// <summary>Replaces the camera screen positions are unprojected through.</summary>
        /// <param name="value">The new camera. Ignored when null.</param>
        public void SetCamera(Camera value)
        {
            if (value != null)
            {
                boardCamera = value;
            }
        }

        /// <summary>
        /// The full document behaviour: read the pointer, reject it if it is over a UI hitbox, and
        /// otherwise snap it to the cell underneath it.
        /// </summary>
        /// <param name="cell">The cell under the pointer when this returns true.</param>
        /// <returns>
        /// False when there is no pointer device, when the pointer is over UI, or when the snapped
        /// cell is off the bound grid.
        /// </returns>
        public bool TryGetCellUnderPointer(out GridCoord cell)
        {
            cell = GridCoord.Zero;

            Vector2 screenPosition;

            if (!TryGetPointerPosition(out screenPosition))
            {
                return false;
            }

            if (blockWhenPointerOverUI && IsPointerOverUI(screenPosition))
            {
                return false;
            }

            return TryGetCellAt(screenPosition, out cell);
        }

        /// <summary>
        /// Snaps an arbitrary screen position to a cell, ignoring UI entirely.
        /// </summary>
        /// <param name="screenPosition">Screen-space position in pixels, origin bottom-left.</param>
        /// <param name="cell">The cell containing that position when this returns true.</param>
        /// <returns>
        /// False when there is no camera, or when <see cref="requireInBounds"/> is set and the cell
        /// falls outside the bound grid.
        /// </returns>
        public bool TryGetCellAt(Vector2 screenPosition, out GridCoord cell)
        {
            cell = GridCoord.Zero;

            if (boardCamera == null)
            {
                return false;
            }

            Vector3 world = ScreenToBoardPlane(screenPosition);
            cell = Layout.WorldToCell(new IsoVector(world.x - boardOrigin.x, world.y - boardOrigin.y));

            if (requireInBounds && grid != null && !grid.InBounds(cell))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true when the screen position lands on a UI hitbox — a roster portrait, a button,
        /// a panel — and therefore must not be treated as a board click.
        /// </summary>
        /// <remarks>
        /// Implemented with <see cref="EventSystem.RaycastAll"/> against a synthetic pointer event
        /// rather than <c>EventSystem.IsPointerOverGameObject()</c>, because the latter is tied to
        /// the active input module's pointer ids and silently returns false for arbitrary positions
        /// and for pointer ids the module has not seen. This form works for any position and any
        /// input module.
        /// </remarks>
        /// <param name="screenPosition">Screen-space position in pixels.</param>
        public bool IsPointerOverUI(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;

            if (eventSystem == null)
            {
                return false;
            }

            if (pointerEventData == null)
            {
                pointerEventData = new PointerEventData(eventSystem);
            }

            pointerEventData.Reset();
            pointerEventData.position = screenPosition;

            uiHits.Clear();
            eventSystem.RaycastAll(pointerEventData, uiHits);

            bool hit = uiHits.Count > 0;
            uiHits.Clear();

            return hit;
        }

        /// <summary>Convenience overload testing the live pointer position.</summary>
        public bool IsPointerOverUI()
        {
            Vector2 screenPosition;
            return TryGetPointerPosition(out screenPosition) && IsPointerOverUI(screenPosition);
        }

        /// <summary>
        /// Reads the current pointer position from the new Input System.
        /// </summary>
        /// <param name="screenPosition">The position in screen pixels when this returns true.</param>
        /// <returns>False when no pointer device is connected.</returns>
        public bool TryGetPointerPosition(out Vector2 screenPosition)
        {
            Pointer pointer = Pointer.current;

            if (pointer == null)
            {
                screenPosition = Vector2.zero;
                return false;
            }

            screenPosition = pointer.position.ReadValue();
            return true;
        }

        /// <summary>World-space centre of a cell's diamond, on the board plane.</summary>
        /// <param name="cell">Cell to project.</param>
        public Vector3 CellToWorld(GridCoord cell)
        {
            IsoVector point = Layout.CellToWorld(cell);
            return new Vector3(point.X + boardOrigin.x, point.Y + boardOrigin.y, 0f);
        }

        /// <summary>
        /// Unprojects a screen position onto the board plane, without snapping. Useful for a drag
        /// ghost that must follow the pointer smoothly while it is off any legal cell.
        /// </summary>
        /// <param name="screenPosition">Screen-space position in pixels.</param>
        public Vector3 ScreenToBoardPlane(Vector2 screenPosition)
        {
            if (boardCamera == null)
            {
                return Vector3.zero;
            }

            // For an orthographic camera the Z passed in only selects the plane, so the board plane
            // (world Z = 0) is reached with the camera's own distance to it.
            float planeDistance = Mathf.Abs(boardCamera.transform.position.z);
            Vector3 world = boardCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, planeDistance));
            world.z = 0f;

            return world;
        }

        private void Awake()
        {
            if (boardCamera == null)
            {
                boardCamera = Camera.main;
            }

            if (warnOnPerspectiveCamera && boardCamera != null && !boardCamera.orthographic)
            {
                Debug.LogWarning(
                    "[GridRaycaster] The board camera is perspective. The capstone document requires " +
                    "strict orthographic grid snapping; cell picking will drift with depth.",
                    this);
            }
        }
    }
}
