using System;

namespace BinakayanRising.Core.Grid
{
    /// <summary>
    /// Plain-C# mutable battle map: a dense <see cref="TerrainType"/> array plus a parallel bool mask
    /// marking deployment tiles.
    /// </summary>
    /// <remarks>
    /// Both arrays are row-major, indexed <c>y * Width + x</c>. There is no Unity dependency here by
    /// design, so a map can be built and simulated in a unit test with no scene, no assets, and no
    /// editor. Authoring tools in the Data/Gameplay assemblies construct one of these and hand it to
    /// the combat resolver as an <see cref="IBattleGrid"/>.
    /// </remarks>
    public sealed class BattleGrid : IBattleGrid
    {
        private readonly TerrainType[] terrain;
        private readonly bool[] deployable;
        private readonly int width;
        private readonly int height;

        /// <summary>
        /// Creates a map of the given size, with every cell set to <paramref name="fill"/> and no
        /// cell marked deployable.
        /// </summary>
        /// <param name="width">Number of columns. Must be positive.</param>
        /// <param name="height">Number of rows. Must be positive.</param>
        /// <param name="fill">Terrain written into every cell. Defaults to <see cref="TerrainType.StandardGrid"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension is zero or negative.</exception>
        public BattleGrid(int width, int height, TerrainType fill = TerrainType.StandardGrid)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(width), width, "Grid width must be greater than zero.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(height), height, "Grid height must be greater than zero.");
            }

            this.width = width;
            this.height = height;

            int cellCount = width * height;
            terrain = new TerrainType[cellCount];
            deployable = new bool[cellCount];

            for (int i = 0; i < cellCount; i++)
            {
                terrain[i] = fill;
            }
        }

        /// <inheritdoc />
        public int Width
        {
            get { return width; }
        }

        /// <inheritdoc />
        public int Height
        {
            get { return height; }
        }

        /// <inheritdoc />
        public bool InBounds(GridCoord c)
        {
            return c.X >= 0 && c.X < width && c.Y >= 0 && c.Y < height;
        }

        /// <inheritdoc />
        public TerrainType GetTerrain(GridCoord c)
        {
            ThrowIfOutOfBounds(c, nameof(c));
            return terrain[Index(c)];
        }

        /// <summary>
        /// Overwrites the terrain of a single cell.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is outside the map.</exception>
        public void SetTerrain(GridCoord c, TerrainType value)
        {
            ThrowIfOutOfBounds(c, nameof(c));
            terrain[Index(c)] = value;
        }

        /// <inheritdoc />
        public bool IsDeployable(GridCoord c)
        {
            if (!InBounds(c))
            {
                return false;
            }

            return deployable[Index(c)];
        }

        /// <summary>
        /// Marks a cell as part of a deployment zone, or clears that mark.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is outside the map.</exception>
        public void SetDeployable(GridCoord c, bool value)
        {
            ThrowIfOutOfBounds(c, nameof(c));
            deployable[Index(c)] = value;
        }

        /// <inheritdoc />
        public bool IsWalkable(GridCoord c)
        {
            if (!InBounds(c))
            {
                return false;
            }

            return IsWalkableTerrain(terrain[Index(c)]);
        }

        /// <summary>
        /// The single source of truth for passability: only <see cref="TerrainType.BambooBarricade"/>
        /// blocks movement; every other terrain type is walkable, though some carry movement penalties
        /// applied elsewhere from design data.
        /// </summary>
        /// <remarks>
        /// TODO(design): the capstone document states the bamboo barricade "blocks enemy pathfinding"
        /// and is silent on whether it also blocks player units. This implementation currently blocks
        /// both. Confirm with design before any code starts branching on faction here.
        /// </remarks>
        private static bool IsWalkableTerrain(TerrainType value)
        {
            return value != TerrainType.BambooBarricade;
        }

        /// <summary>Row-major flat index of a cell. Callers must have validated bounds first.</summary>
        private int Index(GridCoord c)
        {
            return (c.Y * width) + c.X;
        }

        private void ThrowIfOutOfBounds(GridCoord c, string paramName)
        {
            if (InBounds(c))
            {
                return;
            }

            throw new ArgumentOutOfRangeException(
                paramName,
                c.ToString(),
                "Cell is outside the grid. Valid range is (0, 0) to ("
                    + (width - 1) + ", " + (height - 1) + ").");
        }
    }
}
