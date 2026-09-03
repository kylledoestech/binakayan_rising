using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BinakayanRising.Core.Grid;

namespace BinakayanRising.Gameplay.Deployment
{
    /// <summary>
    /// How a single deployment tile is currently being drawn.
    /// </summary>
    /// <remarks>
    /// The capstone document specifies exactly one of these colours: "highlighted blue valid grid
    /// tiles". Everything else here is feedback the document implies but does not colour, so each
    /// shade is a serialized field on <see cref="DeploymentZoneView"/> rather than a constant.
    /// </remarks>
    public enum DeploymentTileState
    {
        /// <summary>The tile is not drawn at all.</summary>
        Hidden = 0,

        /// <summary>A legal, empty drop target. Drawn blue, per the capstone document.</summary>
        Valid = 1,

        /// <summary>A legal drop target the pointer is currently over.</summary>
        Hovered = 2,

        /// <summary>A legal tile that already holds a deployed unit.</summary>
        Occupied = 3,

        /// <summary>An illegal drop target the player just tried to use.</summary>
        Invalid = 4
    }

    /// <summary>
    /// Draws the blue deployment-zone highlight over the isometric board: one sprite per cell for
    /// which <see cref="IBattleGrid.IsDeployable"/> is true, plus hover, occupied and
    /// "invalid drop target" feedback states.
    /// </summary>
    /// <remarks>
    /// <para>
    /// From the capstone document: "the player drags hero portraits ... onto highlighted blue valid
    /// grid tiles". Tile validity is the "Validate Grid Tile Availability" use case, and this view
    /// is only its display half — the ruling itself belongs to
    /// <see cref="DeploymentController.Validate"/>, which this class never duplicates.
    /// </para>
    /// <para>
    /// Tiles are created at runtime as child <see cref="SpriteRenderer"/> objects, so no prefab has
    /// to be authored. If <see cref="tileSprite"/> is left empty the procedural diamond from
    /// <see cref="PlaceholderArt.Tile"/> is used, which already matches the 2:1 projection that
    /// <see cref="IsoGridLayout"/> defines.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DeploymentZoneView : MonoBehaviour
    {
        [Header("Scene References")]
        [Tooltip("Parent for the generated highlight tiles. Leave empty to parent them to this object.")]
        [SerializeField] private Transform tileParent;

        [Tooltip("Sprite drawn for one highlight tile. Leave empty to use the procedural PlaceholderArt diamond.")]
        [SerializeField] private Sprite tileSprite;

        [Header("Isometric Projection")]
        // TODO(design): not specified in capstone document - the document describes a "2.5D
        // isometric grid" but never publishes a tile size. These default to the classic 2:1 diamond
        // that IsoGridLayout documents. Keep them identical to GridRaycaster and DeploymentController.
        [Tooltip("Full width of one tile diamond in world units. TODO(design): unspecified in the document.")]
        [Min(0.0001f)]
        [SerializeField] private float tileWidth = IsoGridLayout.DefaultTileWidth;

        [Tooltip("Full height of one tile diamond in world units. TODO(design): unspecified in the document.")]
        [Min(0.0001f)]
        [SerializeField] private float tileHeight = IsoGridLayout.DefaultTileHeight;

        [Tooltip("World position of grid cell (0, 0). Shifts the whole board without moving this object.")]
        [SerializeField] private Vector2 boardOrigin = Vector2.zero;

        [Tooltip("Z depth the highlight sprites are placed at.")]
        [SerializeField] private float tileZ = 0f;

        [Header("Colours")]
        [Tooltip("Colour of a legal, empty deployment tile. The capstone document specifies blue.")]
        [SerializeField] private Color validColor = new Color(0.24f, 0.55f, 1f, 0.55f);

        [Tooltip("Colour of a legal tile the pointer is hovering.")]
        [SerializeField] private Color hoveredColor = new Color(0.45f, 0.78f, 1f, 0.85f);

        [Tooltip("Colour of a legal tile that already holds a deployed unit.")]
        [SerializeField] private Color occupiedColor = new Color(0.16f, 0.36f, 0.7f, 0.75f);

        [Tooltip("Colour used for the distinct 'invalid drop target' feedback state.")]
        [SerializeField] private Color invalidColor = new Color(0.9f, 0.22f, 0.22f, 0.8f);

        [Header("Feedback")]
        [Tooltip("How long the invalid-drop flash stays on screen, in seconds. " +
                 "TODO(design): not specified in capstone document.")]
        [Min(0f)]
        [SerializeField] private float invalidFlashSeconds = 0.3f;

        [Header("Sorting")]
        [Tooltip("Sorting layer the highlight sprites are drawn on.")]
        [SerializeField] private string sortingLayerName = "Default";

        [Tooltip("Base sorting order. Each tile adds (cell.X + cell.Y) so the diamonds overlap correctly.")]
        [SerializeField] private int baseSortingOrder = -100;

        private readonly Dictionary<GridCoord, SpriteRenderer> tiles = new Dictionary<GridCoord, SpriteRenderer>();
        private readonly Dictionary<GridCoord, DeploymentTileState> states = new Dictionary<GridCoord, DeploymentTileState>();
        private readonly List<GridCoord> zoneCells = new List<GridCoord>();

        private IsoGridLayout layout;
        private IBattleGrid grid;
        private SpriteRenderer strayInvalidMarker;
        private Coroutine invalidFlashRoutine;
        private GridCoord hoveredCell;
        private bool hasHoveredCell;

        /// <summary>The projection this view draws with. Never null; falls back to the serialized tile size.</summary>
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

        /// <summary>World position of grid cell <c>(0, 0)</c>.</summary>
        public Vector2 BoardOrigin
        {
            get { return boardOrigin; }
        }

        /// <summary>Every cell currently highlighted, in the order the zone was built.</summary>
        public IReadOnlyList<GridCoord> ZoneCells
        {
            get { return zoneCells; }
        }

        /// <summary>True once <see cref="ShowDeploymentZone"/> has drawn at least one tile.</summary>
        public bool IsShowing
        {
            get { return zoneCells.Count > 0; }
        }

        /// <summary>
        /// Overrides the projection, so a single owner (normally <see cref="DeploymentController"/>)
        /// can keep this view, the raycaster and the board renderer on one identical layout.
        /// </summary>
        /// <param name="value">The projection to draw with. Ignored when null.</param>
        /// <param name="origin">World position of grid cell <c>(0, 0)</c>.</param>
        public void SetLayout(IsoGridLayout value, Vector2 origin)
        {
            if (value == null)
            {
                return;
            }

            layout = value;
            boardOrigin = origin;

            foreach (KeyValuePair<GridCoord, SpriteRenderer> pair in tiles)
            {
                pair.Value.transform.position = CellToWorld(pair.Key);
            }
        }

        /// <summary>
        /// Rebuilds the highlight from a grid: one tile for every cell where
        /// <see cref="IBattleGrid.IsDeployable"/> is true, all drawn in the valid (blue) state.
        /// </summary>
        /// <param name="battleGrid">
        /// The mission's grid. A null grid clears the highlight, which is the correct display for an
        /// unconfigured mission rather than an invented default zone.
        /// </param>
        public void ShowDeploymentZone(IBattleGrid battleGrid)
        {
            Clear();
            grid = battleGrid;

            if (battleGrid == null)
            {
                return;
            }

            for (int y = 0; y < battleGrid.Height; y++)
            {
                for (int x = 0; x < battleGrid.Width; x++)
                {
                    GridCoord cell = new GridCoord(x, y);

                    if (!battleGrid.IsDeployable(cell))
                    {
                        continue;
                    }

                    zoneCells.Add(cell);
                    SpriteRenderer renderer = GetOrCreateTile(cell);
                    renderer.enabled = true;
                    ApplyState(cell, DeploymentTileState.Valid);
                }
            }
        }

        /// <summary>Removes every highlight tile and forgets the hovered cell.</summary>
        public void Clear()
        {
            StopInvalidFlash();

            foreach (KeyValuePair<GridCoord, SpriteRenderer> pair in tiles)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }

            tiles.Clear();
            states.Clear();
            zoneCells.Clear();
            hasHoveredCell = false;

            if (strayInvalidMarker != null)
            {
                strayInvalidMarker.enabled = false;
            }
        }

        /// <summary>
        /// Sets the drawn state of a single cell. Cells outside the built zone are ignored, except
        /// for <see cref="DeploymentTileState.Invalid"/>, which is handled by
        /// <see cref="FlashInvalid"/> so that illegal cells can still give feedback.
        /// </summary>
        /// <param name="cell">Cell to restyle.</param>
        /// <param name="state">State to draw it in.</param>
        public void SetTileState(GridCoord cell, DeploymentTileState state)
        {
            if (!tiles.ContainsKey(cell))
            {
                return;
            }

            ApplyState(cell, state);
        }

        /// <summary>
        /// Moves the hover highlight to a cell, restoring whatever the previously hovered cell was.
        /// </summary>
        /// <param name="cell">Cell the pointer is over.</param>
        public void SetHoveredCell(GridCoord cell)
        {
            if (hasHoveredCell && hoveredCell == cell)
            {
                return;
            }

            ClearHover();

            if (!tiles.ContainsKey(cell))
            {
                return;
            }

            DeploymentTileState current = GetTileState(cell);

            if (current == DeploymentTileState.Valid)
            {
                ApplyState(cell, DeploymentTileState.Hovered);
            }

            hoveredCell = cell;
            hasHoveredCell = true;
        }

        /// <summary>Drops the hover highlight, returning the hovered cell to its underlying state.</summary>
        public void ClearHover()
        {
            if (!hasHoveredCell)
            {
                return;
            }

            if (tiles.ContainsKey(hoveredCell) && GetTileState(hoveredCell) == DeploymentTileState.Hovered)
            {
                ApplyState(hoveredCell, DeploymentTileState.Valid);
            }

            hasHoveredCell = false;
        }

        /// <summary>Marks a zone cell as holding a deployed unit, or clears that mark.</summary>
        /// <param name="cell">Cell to restyle.</param>
        /// <param name="occupied">True when a unit now stands there.</param>
        public void SetOccupied(GridCoord cell, bool occupied)
        {
            if (!tiles.ContainsKey(cell))
            {
                return;
            }

            ApplyState(cell, occupied ? DeploymentTileState.Occupied : DeploymentTileState.Valid);
        }

        /// <summary>
        /// The distinct "invalid drop target" feedback the deployment phase needs when the player
        /// releases a portrait over a cell they may not use.
        /// </summary>
        /// <remarks>
        /// Works for cells inside the zone (the tile itself flashes) and for cells outside it — an
        /// off-board or non-deployable cell — where a single reusable marker sprite is moved into
        /// place instead, so the player still gets feedback exactly where they aimed.
        /// </remarks>
        /// <param name="cell">Cell the player tried to drop onto.</param>
        public void FlashInvalid(GridCoord cell)
        {
            StopInvalidFlash();

            if (!isActiveAndEnabled)
            {
                return;
            }

            invalidFlashRoutine = StartCoroutine(InvalidFlashRoutine(cell));
        }

        /// <summary>Returns the drawn state of a cell, or <see cref="DeploymentTileState.Hidden"/>.</summary>
        /// <param name="cell">Cell to query.</param>
        public DeploymentTileState GetTileState(GridCoord cell)
        {
            DeploymentTileState state;
            return states.TryGetValue(cell, out state) ? state : DeploymentTileState.Hidden;
        }

        /// <summary>World-space centre of a cell's diamond, including the board origin and Z depth.</summary>
        /// <param name="cell">Cell to project.</param>
        public Vector3 CellToWorld(GridCoord cell)
        {
            IsoVector point = Layout.CellToWorld(cell);
            return new Vector3(point.X + boardOrigin.x, point.Y + boardOrigin.y, tileZ);
        }

        private void OnDisable()
        {
            StopInvalidFlash();
        }

        private void OnDestroy()
        {
            Clear();
        }

        private IEnumerator InvalidFlashRoutine(GridCoord cell)
        {
            SpriteRenderer renderer;
            DeploymentTileState restore = DeploymentTileState.Hidden;
            bool isZoneTile = tiles.TryGetValue(cell, out renderer);

            if (isZoneTile)
            {
                restore = GetTileState(cell);
                ApplyState(cell, DeploymentTileState.Invalid);
            }
            else
            {
                renderer = GetOrCreateStrayMarker();
                renderer.transform.position = CellToWorld(cell);
                renderer.color = invalidColor;
                renderer.enabled = true;
            }

            yield return new WaitForSecondsRealtime(invalidFlashSeconds);

            if (isZoneTile)
            {
                ApplyState(cell, restore == DeploymentTileState.Invalid ? DeploymentTileState.Valid : restore);
            }
            else if (strayInvalidMarker != null)
            {
                strayInvalidMarker.enabled = false;
            }

            invalidFlashRoutine = null;
        }

        private void StopInvalidFlash()
        {
            if (invalidFlashRoutine != null)
            {
                StopCoroutine(invalidFlashRoutine);
                invalidFlashRoutine = null;
            }
        }

        private void ApplyState(GridCoord cell, DeploymentTileState state)
        {
            states[cell] = state;

            SpriteRenderer renderer;

            if (!tiles.TryGetValue(cell, out renderer) || renderer == null)
            {
                return;
            }

            renderer.enabled = state != DeploymentTileState.Hidden;
            renderer.color = ColorFor(state);
        }

        private Color ColorFor(DeploymentTileState state)
        {
            switch (state)
            {
                case DeploymentTileState.Valid:
                    return validColor;
                case DeploymentTileState.Hovered:
                    return hoveredColor;
                case DeploymentTileState.Occupied:
                    return occupiedColor;
                case DeploymentTileState.Invalid:
                    return invalidColor;
                default:
                    return Color.clear;
            }
        }

        private SpriteRenderer GetOrCreateTile(GridCoord cell)
        {
            SpriteRenderer existing;

            if (tiles.TryGetValue(cell, out existing) && existing != null)
            {
                return existing;
            }

            SpriteRenderer created = CreateRenderer("DeploymentTile " + cell);
            created.transform.position = CellToWorld(cell);
            created.sortingOrder = baseSortingOrder + cell.X + cell.Y;

            tiles[cell] = created;
            return created;
        }

        private SpriteRenderer GetOrCreateStrayMarker()
        {
            if (strayInvalidMarker == null)
            {
                strayInvalidMarker = CreateRenderer("DeploymentTile Invalid Marker");
                strayInvalidMarker.sortingOrder = baseSortingOrder + 1000;
            }

            return strayInvalidMarker;
        }

        private SpriteRenderer CreateRenderer(string objectName)
        {
            GameObject host = new GameObject(objectName);
            host.transform.SetParent(tileParent != null ? tileParent : transform, false);

            SpriteRenderer renderer = host.AddComponent<SpriteRenderer>();
            renderer.sprite = tileSprite != null ? tileSprite : PlaceholderArt.Tile;
            renderer.sortingLayerName = sortingLayerName;
            renderer.enabled = false;

            // The procedural diamond is authored one cell wide, so a non-default tile size has to be
            // matched by scaling rather than by re-rasterising the sprite.
            if (tileSprite == null)
            {
                renderer.transform.localScale = new Vector3(
                    tileWidth / IsoGridLayout.DefaultTileWidth,
                    tileHeight / IsoGridLayout.DefaultTileHeight,
                    1f);
            }

            return renderer;
        }
    }
}
