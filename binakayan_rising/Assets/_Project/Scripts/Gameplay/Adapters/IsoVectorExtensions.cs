using BinakayanRising.Core.Grid;
using UnityEngine;

namespace BinakayanRising.Gameplay.Adapters
{
    /// <summary>
    /// Conversions across the Core/Unity boundary for the two coordinate types the simulation
    /// speaks: <see cref="IsoVector"/> (world-space points) and <see cref="GridCoord"/> (cells).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This file is the only place the two vocabularies are allowed to meet.</b>
    /// <c>BinakayanRising.Core</c> is compiled with <c>noEngineReferences</c>, so it can never see a
    /// <see cref="Vector3"/>, and nothing in the presentation layer should be inventing its own
    /// projection maths. Everything here is a pure function with no side effects.
    /// </para>
    /// <para>
    /// The projection itself belongs to <see cref="IsoGridLayout"/> and is not duplicated here;
    /// <see cref="CellToWorldPosition(IsoGridLayout, GridCoord, float)"/> simply calls it and widens
    /// the result to a <see cref="Vector3"/>.
    /// </para>
    /// <para>
    /// Sorting: <see cref="IsoGridLayout"/> documents <c>X + Y</c> as the depth key of the diamond
    /// grid — cells sharing a sum land on the same screen row, and a higher sum is nearer the
    /// camera. <see cref="DepthKey"/> and <see cref="ToSortingOrder"/> are the single definition of
    /// that rule for every renderer in the game.
    /// </para>
    /// </remarks>
    public static class IsoVectorExtensions
    {
        /// <summary>Widens a Core world point to a Unity <see cref="Vector2"/>.</summary>
        /// <param name="value">Core-side world point.</param>
        public static Vector2 ToVector2(this IsoVector value)
        {
            return new Vector2(value.X, value.Y);
        }

        /// <summary>
        /// Widens a Core world point to a Unity <see cref="Vector3"/> on the given depth plane.
        /// </summary>
        /// <param name="value">Core-side world point.</param>
        /// <param name="z">
        /// World Z to place the point on. Defaults to zero, which is the 2D plane URP's 2D renderer
        /// draws on. Sprite ordering is done with <c>sortingOrder</c>, not with Z; see
        /// <see cref="ToSortingOrder"/>.
        /// </param>
        public static Vector3 ToVector3(this IsoVector value, float z = 0f)
        {
            return new Vector3(value.X, value.Y, z);
        }

        /// <summary>Narrows a Unity <see cref="Vector2"/> to a Core world point.</summary>
        /// <param name="value">Unity world position.</param>
        public static IsoVector ToIsoVector(this Vector2 value)
        {
            return new IsoVector(value.x, value.y);
        }

        /// <summary>Narrows a Unity <see cref="Vector3"/> to a Core world point, dropping Z.</summary>
        /// <param name="value">Unity world position.</param>
        public static IsoVector ToIsoVector(this Vector3 value)
        {
            return new IsoVector(value.x, value.y);
        }

        /// <summary>
        /// Converts a simulation cell to the <see cref="Vector3Int"/> a Unity
        /// <c>UnityEngine.Tilemaps.Tilemap</c> indexes tiles by.
        /// </summary>
        /// <param name="cell">Simulation cell.</param>
        /// <param name="z">Tilemap layer index. Almost always zero for a single-layer terrain map.</param>
        public static Vector3Int ToVector3Int(this GridCoord cell, int z = 0)
        {
            return new Vector3Int(cell.X, cell.Y, z);
        }

        /// <summary>Converts a simulation cell to a <see cref="Vector2Int"/>.</summary>
        /// <param name="cell">Simulation cell.</param>
        public static Vector2Int ToVector2Int(this GridCoord cell)
        {
            return new Vector2Int(cell.X, cell.Y);
        }

        /// <summary>Converts a Tilemap cell index back to a simulation cell, dropping Z.</summary>
        /// <param name="cell">Tilemap cell index.</param>
        public static GridCoord ToGridCoord(this Vector3Int cell)
        {
            return new GridCoord(cell.x, cell.y);
        }

        /// <summary>Converts a <see cref="Vector2Int"/> to a simulation cell.</summary>
        /// <param name="cell">Cell index.</param>
        public static GridCoord ToGridCoord(this Vector2Int cell)
        {
            return new GridCoord(cell.x, cell.y);
        }

        /// <summary>
        /// Projects a cell to the world-space centre of its diamond as a Unity position.
        /// </summary>
        /// <param name="layout">Layout supplying the projection. Must not be null.</param>
        /// <param name="cell">Cell to project.</param>
        /// <param name="z">World Z to place the point on.</param>
        /// <remarks>
        /// Named differently from <see cref="IsoGridLayout.CellToWorld"/> on purpose: an extension
        /// method may not shadow an instance method, and silently binding to the Core overload
        /// would return an <see cref="IsoVector"/> where a <see cref="Vector3"/> was intended.
        /// </remarks>
        public static Vector3 CellToWorldPosition(this IsoGridLayout layout, GridCoord cell, float z = 0f)
        {
            return layout.CellToWorld(cell).ToVector3(z);
        }

        /// <summary>
        /// Finds the cell whose diamond contains a Unity world position. Exact inverse of
        /// <see cref="CellToWorldPosition(IsoGridLayout, GridCoord, float)"/>.
        /// </summary>
        /// <param name="layout">Layout supplying the projection. Must not be null.</param>
        /// <param name="world">Unity world position. Z is ignored.</param>
        public static GridCoord WorldPositionToCell(this IsoGridLayout layout, Vector3 world)
        {
            return layout.WorldToCell(world.ToIsoVector());
        }

        /// <summary>
        /// The isometric depth key of a cell: <c>X + Y</c>. Higher means nearer the camera, so a
        /// sprite with a larger key must draw over one with a smaller key.
        /// </summary>
        /// <param name="cell">Cell to key.</param>
        public static int DepthKey(this GridCoord cell)
        {
            return cell.X + cell.Y;
        }

        /// <summary>
        /// Turns a cell into a <c>SpriteRenderer.sortingOrder</c>.
        /// </summary>
        /// <param name="cell">Cell the sprite stands on.</param>
        /// <param name="step">
        /// Orders reserved per diamond row. Anything layered inside one cell — a health bar, a
        /// floating number, a shadow — offsets within this window, so it must exceed the number of
        /// stacked renderers a single unit owns.
        /// </param>
        /// <param name="offset">Extra order applied after the row, for layering inside one cell.</param>
        public static int ToSortingOrder(this GridCoord cell, int step, int offset = 0)
        {
            return (cell.DepthKey() * step) + offset;
        }
    }
}
