using System.Collections.Generic;
using BinakayanRising.Core.Grid;
using BinakayanRising.Gameplay.Adapters;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BinakayanRising.Gameplay.Presentation
{
    /// <summary>
    /// Projects simulation cells to Unity world positions and back.
    /// </summary>
    /// <remarks>
    /// Two implementations exist for a reason. <see cref="IsoTerrainRenderer"/> defers to the
    /// Tilemap's own <c>Grid</c> component, so sprites land exactly on the painted tiles whatever
    /// cell size the artist chose. <see cref="IsoLayoutWorldSpace"/> uses Core's
    /// <see cref="IsoGridLayout"/> instead, which is what a scene with no Tilemap — a test bed, a
    /// gym scene — falls back to.
    /// </remarks>
    public interface IBattleWorldSpace
    {
        /// <summary>The world-space centre of a cell.</summary>
        /// <param name="cell">Simulation cell.</param>
        Vector3 CellToWorld(GridCoord cell);

        /// <summary>The cell containing a world-space point.</summary>
        /// <param name="world">World-space point.</param>
        GridCoord WorldToCell(Vector3 world);
    }

    /// <summary>
    /// Cell-to-world projection backed by Core's <see cref="IsoGridLayout"/>, for scenes with no
    /// Tilemap.
    /// </summary>
    public sealed class IsoLayoutWorldSpace : IBattleWorldSpace
    {
        private readonly IsoGridLayout layout;
        private readonly Vector3 origin;

        /// <summary>Creates a projection.</summary>
        /// <param name="layout">Layout to project with. Null falls back to <see cref="IsoGridLayout.Default"/>.</param>
        /// <param name="origin">World position of cell <c>(0, 0)</c>.</param>
        public IsoLayoutWorldSpace(IsoGridLayout layout = null, Vector3 origin = default)
        {
            this.layout = layout ?? IsoGridLayout.Default;
            this.origin = origin;
        }

        /// <inheritdoc />
        public Vector3 CellToWorld(GridCoord cell)
        {
            return origin + layout.CellToWorldPosition(cell);
        }

        /// <inheritdoc />
        public GridCoord WorldToCell(Vector3 world)
        {
            return layout.WorldPositionToCell(world - origin);
        }
    }

    /// <summary>
    /// Paints a Core <see cref="IBattleGrid"/> onto a Unity isometric <c>Tilemap</c>, one
    /// <see cref="TileBase"/> per <see cref="TerrainType"/>, and answers cell-to-world questions for
    /// everything else in the presentation layer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tile table is the same <see cref="TerrainTileBinding"/> list
    /// <see cref="MissionGridBuilder"/> reads a painted map with, so a battlefield can be authored
    /// by hand in the Tile Palette and re-painted from the simulation's own grid without the two
    /// ever disagreeing about what a tile means.
    /// </para>
    /// <para>
    /// The debug gizmo mode draws the diamond outline of every cell and labels it with its
    /// <see cref="GridCoord"/>. Isometric coordinate bugs are otherwise invisible — a sprite one
    /// cell off looks like a sprite that is simply drawn slightly wrong — so this is the fastest
    /// path from "the units are in the wrong place" to knowing why.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class IsoTerrainRenderer : MonoBehaviour, IBattleWorldSpace
    {
        [Header("Tilemap")]
        [Tooltip("Isometric Tilemap the terrain is painted onto. Required to paint; optional for projection.")]
        [SerializeField] private Tilemap terrainTilemap;

        [Tooltip("Tilemap cell that corresponds to simulation cell (0, 0).")]
        [SerializeField] private Vector3Int tilemapOrigin = Vector3Int.zero;

        [Tooltip("One row per TerrainType. Also read in reverse by MissionGridBuilder.")]
        [SerializeField] private List<TerrainTileBinding> tileBindings = new List<TerrainTileBinding>();

        [Header("Deployment Zone")]
        [Tooltip("Optional overlay Tilemap that highlights the cells the player may deploy onto.")]
        [SerializeField] private Tilemap deploymentOverlayTilemap;

        [Tooltip("Tile painted on every deployable cell of the overlay Tilemap.")]
        [SerializeField] private TileBase deploymentHighlightTile;

        [Header("Fallback Projection")]
        [Tooltip("Tile width in world units used when no Tilemap is assigned. Core's default is 1.0.")]
        [Min(0.0001f)]
        [SerializeField] private float fallbackTileWidth = IsoGridLayout.DefaultTileWidth;

        [Tooltip("Tile height in world units used when no Tilemap is assigned. Core's default is 0.5.")]
        [Min(0.0001f)]
        [SerializeField] private float fallbackTileHeight = IsoGridLayout.DefaultTileHeight;

        [Header("Debug Gizmos")]
        [Tooltip("Draw the grid outline and cell labels in the Scene view.")]
        [SerializeField] private bool drawDebugGrid = false;

        [Tooltip("Label every cell with its GridCoord. Costly with a large grid; halve it with the stride below.")]
        [SerializeField] private bool labelCells = true;

        [Tooltip("Label only every Nth cell in each axis, to keep a large grid readable.")]
        [Min(1)]
        [SerializeField] private int labelStride = 1;

        [Tooltip("Colour of the grid outline.")]
        [SerializeField] private Color gizmoGridColor = new Color(1f, 1f, 1f, 0.25f);

        [Tooltip("Colour used to fill impassable cells.")]
        [SerializeField] private Color gizmoBlockedColor = new Color(0.85f, 0.25f, 0.25f, 0.45f);

        [Tooltip("Colour used to fill deployable cells.")]
        [SerializeField] private Color gizmoDeployableColor = new Color(0.30f, 0.75f, 1f, 0.40f);

        [Tooltip("Grid size drawn when no grid has been painted yet, so gizmos work before Play Mode.")]
        [SerializeField] private Vector2Int gizmoPreviewSize = new Vector2Int(14, 9);

        private IBattleGrid paintedGrid;
        private IsoGridLayout fallbackLayout;
        private Dictionary<TerrainType, TileBase> tilesByTerrain;

        /// <summary>The grid most recently painted, or null before the first <see cref="Paint"/>.</summary>
        public IBattleGrid PaintedGrid
        {
            get { return paintedGrid; }
        }

        /// <summary>The Tilemap terrain is painted onto, or null when the renderer is projection-only.</summary>
        public Tilemap TerrainTilemap
        {
            get { return terrainTilemap; }
        }

        /// <summary>Tilemap cell that corresponds to simulation cell <c>(0, 0)</c>.</summary>
        public Vector3Int TilemapOrigin
        {
            get { return tilemapOrigin; }
        }

        /// <summary>The tile-to-terrain table, for handing to <see cref="MissionGridBuilder"/>.</summary>
        public IReadOnlyList<TerrainTileBinding> TileBindings
        {
            get { return tileBindings; }
        }

        /// <inheritdoc />
        public Vector3 CellToWorld(GridCoord cell)
        {
            if (terrainTilemap != null)
            {
                return terrainTilemap.GetCellCenterWorld(ToTilemapCell(cell));
            }

            return transform.position + FallbackLayout.CellToWorldPosition(cell);
        }

        /// <inheritdoc />
        public GridCoord WorldToCell(Vector3 world)
        {
            if (terrainTilemap != null)
            {
                Vector3Int tilemapCell = terrainTilemap.WorldToCell(world);
                return new GridCoord(tilemapCell.x - tilemapOrigin.x, tilemapCell.y - tilemapOrigin.y);
            }

            return FallbackLayout.WorldPositionToCell(world - transform.position);
        }

        /// <summary>
        /// Paints every cell of a grid onto the terrain Tilemap and remembers the grid for gizmos.
        /// </summary>
        /// <param name="grid">Grid to paint. Must not be null.</param>
        /// <remarks>
        /// Cells whose terrain has no tile bound are cleared rather than left holding whatever was
        /// painted before, so a missing binding shows up as a hole instead of as stale art.
        /// </remarks>
        public void Paint(IBattleGrid grid)
        {
            if (grid == null)
            {
                Debug.LogWarning("IsoTerrainRenderer.Paint was given no grid. Nothing painted.");
                return;
            }

            paintedGrid = grid;

            if (terrainTilemap == null)
            {
                return;
            }

            Dictionary<TerrainType, TileBase> lookup = ResolveTileLookup();

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    GridCoord cell = new GridCoord(x, y);
                    TileBase tile;
                    lookup.TryGetValue(grid.GetTerrain(cell), out tile);
                    terrainTilemap.SetTile(ToTilemapCell(cell), tile);
                }
            }

            PaintDeploymentOverlay(grid);
        }

        /// <summary>Clears every tile this renderer painted, including the deployment overlay.</summary>
        public void Clear()
        {
            if (terrainTilemap != null)
            {
                terrainTilemap.ClearAllTiles();
            }

            if (deploymentOverlayTilemap != null)
            {
                deploymentOverlayTilemap.ClearAllTiles();
            }

            paintedGrid = null;
        }

        /// <summary>
        /// Paints, or clears, the highlight showing where the player may deploy.
        /// </summary>
        /// <param name="grid">Grid supplying the deployable mask, or null to clear the overlay.</param>
        /// <remarks>
        /// The capstone document has this highlight fade out when combat starts, leaving only the
        /// floating health bars; call this with <c>null</c> at that moment.
        /// </remarks>
        public void PaintDeploymentOverlay(IBattleGrid grid)
        {
            if (deploymentOverlayTilemap == null)
            {
                return;
            }

            deploymentOverlayTilemap.ClearAllTiles();

            if (grid == null || deploymentHighlightTile == null)
            {
                return;
            }

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    GridCoord cell = new GridCoord(x, y);

                    if (grid.IsDeployable(cell))
                    {
                        deploymentOverlayTilemap.SetTile(ToTilemapCell(cell), deploymentHighlightTile);
                    }
                }
            }
        }

        /// <summary>Shows or hides the deployment highlight overlay without repainting it.</summary>
        /// <param name="visible">Whether the overlay may draw.</param>
        public void SetDeploymentOverlayVisible(bool visible)
        {
            if (deploymentOverlayTilemap != null)
            {
                deploymentOverlayTilemap.gameObject.SetActive(visible);
            }
        }

        /// <summary>The Tilemap cell index a simulation cell maps to.</summary>
        /// <param name="cell">Simulation cell.</param>
        public Vector3Int ToTilemapCell(GridCoord cell)
        {
            return new Vector3Int(tilemapOrigin.x + cell.X, tilemapOrigin.y + cell.Y, tilemapOrigin.z);
        }

        private IsoGridLayout FallbackLayout
        {
            get
            {
                if (fallbackLayout == null)
                {
                    fallbackLayout = new IsoGridLayout(fallbackTileWidth, fallbackTileHeight);
                }

                return fallbackLayout;
            }
        }

        private Dictionary<TerrainType, TileBase> ResolveTileLookup()
        {
            if (tilesByTerrain != null)
            {
                return tilesByTerrain;
            }

            tilesByTerrain = new Dictionary<TerrainType, TileBase>();

            for (int i = 0; i < tileBindings.Count; i++)
            {
                TerrainTileBinding binding = tileBindings[i];

                if (binding == null || binding.Tile == null)
                {
                    continue;
                }

                if (!tilesByTerrain.ContainsKey(binding.Terrain))
                {
                    tilesByTerrain[binding.Terrain] = binding.Tile;
                }
            }

            IReadOnlyList<TerrainType> all = TerrainTableAdapter.AllTerrainTypes();

            for (int i = 0; i < all.Count; i++)
            {
                if (!tilesByTerrain.ContainsKey(all[i]))
                {
                    Debug.LogWarning(
                        "IsoTerrainRenderer on '" + name + "' has no tile bound for " + all[i]
                            + ". Cells of that terrain will be painted empty.");
                }
            }

            return tilesByTerrain;
        }

        private void OnValidate()
        {
            // Force the caches to rebuild so Inspector edits take effect without a domain reload.
            tilesByTerrain = null;
            fallbackLayout = null;
        }

        private void OnDrawGizmos()
        {
            if (!drawDebugGrid)
            {
                return;
            }

            int width = paintedGrid != null ? paintedGrid.Width : Mathf.Max(0, gizmoPreviewSize.x);
            int height = paintedGrid != null ? paintedGrid.Height : Mathf.Max(0, gizmoPreviewSize.y);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    DrawCellGizmo(new GridCoord(x, y));
                }
            }
        }

        private void DrawCellGizmo(GridCoord cell)
        {
            Vector3 centre = CellToWorld(cell);
            float halfWidth = (terrainTilemap != null ? terrainTilemap.cellSize.x : fallbackTileWidth) * 0.5f;
            float halfHeight = (terrainTilemap != null ? terrainTilemap.cellSize.y : fallbackTileHeight) * 0.5f;

            Vector3 east = centre + new Vector3(halfWidth, 0f, 0f);
            Vector3 north = centre + new Vector3(0f, halfHeight, 0f);
            Vector3 west = centre + new Vector3(-halfWidth, 0f, 0f);
            Vector3 south = centre + new Vector3(0f, -halfHeight, 0f);

            if (paintedGrid != null)
            {
                if (!paintedGrid.IsWalkable(cell))
                {
                    Gizmos.color = gizmoBlockedColor;
                    Gizmos.DrawLine(west, east);
                    Gizmos.DrawLine(south, north);
                }
                else if (paintedGrid.IsDeployable(cell))
                {
                    Gizmos.color = gizmoDeployableColor;
                    Gizmos.DrawLine(west, east);
                    Gizmos.DrawLine(south, north);
                }
            }

            Gizmos.color = gizmoGridColor;
            Gizmos.DrawLine(east, north);
            Gizmos.DrawLine(north, west);
            Gizmos.DrawLine(west, south);
            Gizmos.DrawLine(south, east);

#if UNITY_EDITOR
            if (labelCells && cell.X % labelStride == 0 && cell.Y % labelStride == 0)
            {
                UnityEditor.Handles.color = gizmoGridColor;
                UnityEditor.Handles.Label(centre, cell.ToString());
            }
#endif
        }
    }
}
