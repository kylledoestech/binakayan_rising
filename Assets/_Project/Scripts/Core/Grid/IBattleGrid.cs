namespace BinakayanRising.Core.Grid
{
    /// <summary>
    /// Read-only view of a battle map, as consumed by the combat resolver, the pathfinder, and the
    /// deployment UI. Deliberately narrow: consumers ask questions about cells, they do not mutate
    /// the map.
    /// </summary>
    /// <remarks>
    /// Cells are addressed by <see cref="GridCoord"/> with the origin at <c>(0, 0)</c> and valid
    /// indices running <c>0 .. Width - 1</c> and <c>0 .. Height - 1</c>.
    /// </remarks>
    public interface IBattleGrid
    {
        /// <summary>Number of columns. Valid <c>X</c> values are <c>0 .. Width - 1</c>.</summary>
        int Width { get; }

        /// <summary>Number of rows. Valid <c>Y</c> values are <c>0 .. Height - 1</c>.</summary>
        int Height { get; }

        /// <summary>Returns <c>true</c> when the cell lies inside the map.</summary>
        bool InBounds(GridCoord c);

        /// <summary>
        /// Returns the terrain of a cell.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when <paramref name="c"/> is outside the map. Reading terrain off-map is a caller
        /// bug, so it fails loudly rather than returning a default.
        /// </exception>
        TerrainType GetTerrain(GridCoord c);

        /// <summary>
        /// Returns <c>true</c> when the cell is part of a deployment zone, i.e. a highlighted tile
        /// the player may drop a hero portrait onto during the deployment phase.
        /// Off-map cells return <c>false</c> rather than throwing.
        /// </summary>
        bool IsDeployable(GridCoord c);

        /// <summary>
        /// Returns <c>true</c> when a land unit may occupy or path through the cell.
        /// Off-map cells return <c>false</c> rather than throwing, so pathfinders can probe
        /// neighbours without pre-filtering.
        /// </summary>
        bool IsWalkable(GridCoord c);
    }
}
