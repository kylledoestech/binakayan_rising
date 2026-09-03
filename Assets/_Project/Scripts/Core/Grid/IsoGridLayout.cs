using System;

namespace BinakayanRising.Core.Grid
{
    /// <summary>
    /// Converts between logical <see cref="GridCoord"/> cells and world-space <see cref="IsoVector"/>
    /// points using a classic 2:1 diamond ("isometric") projection.
    /// </summary>
    /// <remarks>
    /// <para><b>Projection formula</b> (the only definition of the convention; everything else follows):</para>
    /// <code>
    /// worldX = (cell.X - cell.Y) * (TileWidth  / 2)
    /// worldY = (cell.X + cell.Y) * (TileHeight / 2)
    /// </code>
    /// <para><b>Screen directions.</b> Assuming Unity's default 2D orientation, where world +X points
    /// right on screen and world +Y points up on screen:</para>
    /// <list type="bullet">
    ///   <item><description>Grid <b>+X</b> runs up and to the <b>right</b> on screen (the north-east edge of the diamond).</description></item>
    ///   <item><description>Grid <b>+Y</b> runs up and to the <b>left</b> on screen (the north-west edge of the diamond).</description></item>
    ///   <item><description>Therefore cell <c>(0, 0)</c> sits at the <b>bottom (south) corner</b> of a grid whose cells are all non-negative, and the grid opens upward and away from the camera.</description></item>
    /// </list>
    /// <para>
    /// Consequences worth knowing: cells sharing a constant <c>X + Y</c> land on the same horizontal
    /// screen line (one diamond "row"), and cells sharing a constant <c>X - Y</c> land on the same
    /// vertical screen line. Depth sorting for sprites therefore keys off <c>X + Y</c>.
    /// </para>
    /// <para>
    /// If the renderer wants the grid to descend down the screen instead (a screen-space Y-down
    /// convention), negate <see cref="IsoVector.Y"/> at the Unity boundary rather than changing this
    /// class, so that the simulation's coordinate contract stays fixed.
    /// </para>
    /// <para>
    /// <see cref="CellToWorld"/> returns the <b>centre</b> of the cell's diamond, and
    /// <see cref="WorldToCell"/> is its exact inverse, so
    /// <c>WorldToCell(CellToWorld(c)) == c</c> holds for every cell.
    /// </para>
    /// </remarks>
    public sealed class IsoGridLayout
    {
        /// <summary>Default tile width in world units for the classic 2:1 diamond.</summary>
        public const float DefaultTileWidth = 1.0f;

        /// <summary>Default tile height in world units for the classic 2:1 diamond.</summary>
        public const float DefaultTileHeight = 0.5f;

        /// <summary>A shared layout using the classic 2:1 diamond (width 1.0, height 0.5).</summary>
        public static readonly IsoGridLayout Default = new IsoGridLayout(DefaultTileWidth, DefaultTileHeight);

        private readonly float tileWidth;
        private readonly float tileHeight;

        // Kept as double so the forward projection and its inverse both run at double precision;
        // only the final results are narrowed. This keeps the round trip exact for any sane tile size.
        private readonly double halfWidth;
        private readonly double halfHeight;

        /// <summary>
        /// Creates a layout for tiles of the given size in world units.
        /// </summary>
        /// <param name="tileWidth">Full width of one tile diamond, in world units. Must be positive.</param>
        /// <param name="tileHeight">Full height of one tile diamond, in world units. Must be positive.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when either dimension is zero or negative, which would make the projection non-invertible.
        /// </exception>
        public IsoGridLayout(float tileWidth = DefaultTileWidth, float tileHeight = DefaultTileHeight)
        {
            if (tileWidth <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tileWidth), tileWidth, "Tile width must be greater than zero.");
            }

            if (tileHeight <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tileHeight), tileHeight, "Tile height must be greater than zero.");
            }

            this.tileWidth = tileWidth;
            this.tileHeight = tileHeight;
            halfWidth = tileWidth * 0.5d;
            halfHeight = tileHeight * 0.5d;
        }

        /// <summary>Full width of one tile diamond, in world units.</summary>
        public float TileWidth
        {
            get { return tileWidth; }
        }

        /// <summary>Full height of one tile diamond, in world units.</summary>
        public float TileHeight
        {
            get { return tileHeight; }
        }

        /// <summary>
        /// Projects a cell to the world-space centre of its diamond.
        /// </summary>
        /// <param name="cell">The cell to project.</param>
        /// <returns>
        /// <c>((cell.X - cell.Y) * TileWidth / 2, (cell.X + cell.Y) * TileHeight / 2)</c>.
        /// </returns>
        public IsoVector CellToWorld(GridCoord cell)
        {
            double worldX = (cell.X - (double)cell.Y) * halfWidth;
            double worldY = (cell.X + (double)cell.Y) * halfHeight;
            return new IsoVector((float)worldX, (float)worldY);
        }

        /// <summary>
        /// Finds the cell whose diamond contains the given world-space point.
        /// </summary>
        /// <param name="world">A world-space point, in the same units as <see cref="TileWidth"/>.</param>
        /// <returns>The nearest cell, which for points inside a diamond is the cell that owns it.</returns>
        /// <remarks>
        /// Inverting the projection: let <c>d = worldX / (TileWidth / 2) = X - Y</c> and
        /// <c>s = worldY / (TileHeight / 2) = X + Y</c>. Then <c>X = (s + d) / 2</c> and
        /// <c>Y = (s - d) / 2</c>. The two results are rounded to the nearest integer, so any point
        /// within a cell's diamond maps back to that cell. Points landing exactly on a boundary round
        /// away from zero, which is arbitrary but deterministic.
        /// </remarks>
        public GridCoord WorldToCell(IsoVector world)
        {
            double difference = world.X / halfWidth;  // == cell.X - cell.Y
            double sum = world.Y / halfHeight;        // == cell.X + cell.Y

            double cellX = (sum + difference) * 0.5d;
            double cellY = (sum - difference) * 0.5d;

            return new GridCoord(
                (int)Math.Round(cellX, MidpointRounding.AwayFromZero),
                (int)Math.Round(cellY, MidpointRounding.AwayFromZero));
        }
    }
}
