using BinakayanRising.Core.Grid;
using UnityEngine;

namespace BinakayanRising.UI.Camp
{
    /// <summary>
    /// Where the encampment's cells are in the world, at the pixel density of its sprites.
    /// </summary>
    /// <remarks>
    /// A camp tile is a 60x30 pixel diamond and every camp sprite imports at 80 pixels per unit
    /// (<c>UiArtPostprocessor</c>), so a cell centre always lands on a whole pixel and the painted
    /// ground, the Blender-rendered buildings and the figures share one pixel grid. Cells follow
    /// the battle board's convention: X runs up and to the right, Y up and to the left.
    /// </remarks>
    public static class CampIso
    {
        public const float PixelsPerUnit = 80f;

        /// <summary>Half a tile's width and height in world units: 30 and 15 pixels.</summary>
        public const float HalfWidth = 30f / PixelsPerUnit;
        public const float HalfHeight = 15f / PixelsPerUnit;

        /// <summary>Sorting-order steps per world unit of depth.</summary>
        private const float SortPerUnit = 100f;

        public static Vector2 ToWorld(float x, float y)
        {
            return new Vector2((x - y) * HalfWidth, (x + y) * HalfHeight);
        }

        public static Vector2 ToWorld(GridCoord cell)
        {
            return ToWorld(cell.X, cell.Y);
        }

        /// <summary>Fractional cell coordinates of a world point.</summary>
        public static Vector2 ToGrid(Vector2 world)
        {
            float a = world.x / HalfWidth;
            float b = world.y / HalfHeight;
            return new Vector2((a + b) * 0.5f, (b - a) * 0.5f);
        }

        /// <summary>The cell whose diamond contains a world point.</summary>
        public static GridCoord Cell(Vector2 world)
        {
            Vector2 grid = ToGrid(world);
            return new GridCoord(Mathf.RoundToInt(grid.x), Mathf.RoundToInt(grid.y));
        }

        /// <summary>
        /// Draw order for something standing at <paramref name="worldY"/>: lower on screen is
        /// nearer the viewer and draws later. Footprints sort by their centre, which puts every
        /// cell beside a building correctly in front of or behind it.
        /// </summary>
        public static int SortOrder(float worldY)
        {
            return Mathf.RoundToInt(-worldY * SortPerUnit);
        }

        /// <summary>A world point moved onto the nearest art pixel, so sprites never shimmer.</summary>
        public static Vector2 Snap(Vector2 world)
        {
            return new Vector2(Mathf.Round(world.x * PixelsPerUnit) / PixelsPerUnit, Mathf.Round(world.y * PixelsPerUnit) / PixelsPerUnit);
        }
    }
}
