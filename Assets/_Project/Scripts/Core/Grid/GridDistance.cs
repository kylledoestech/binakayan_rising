using System.Collections.Generic;

namespace BinakayanRising.Core.Grid
{
    /// <summary>
    /// Distance and adjacency helpers for the logically-square battle grid.
    /// </summary>
    /// <remarks>
    /// The grid is square in simulation space even though it is drawn as an isometric diamond, so
    /// these are the ordinary square-grid metrics. Nothing here consults
    /// <see cref="IsoGridLayout"/>: rendering geometry must never influence rules geometry.
    /// </remarks>
    public static class GridDistance
    {
        /// <summary>
        /// Manhattan (taxicab) distance: the number of orthogonal steps between two cells.
        /// Use this when diagonal movement is disallowed.
        /// </summary>
        public static int Manhattan(GridCoord a, GridCoord b)
        {
            return Abs(a.X - b.X) + Abs(a.Y - b.Y);
        }

        /// <summary>
        /// Chebyshev (chessboard) distance: the number of steps when diagonals cost the same as
        /// orthogonals. Use this when diagonal movement is allowed.
        /// </summary>
        public static int Chebyshev(GridCoord a, GridCoord b)
        {
            int dx = Abs(a.X - b.X);
            int dy = Abs(a.Y - b.Y);
            return dx > dy ? dx : dy;
        }

        /// <summary>
        /// Enumerates the cells adjacent to <paramref name="c"/>.
        /// </summary>
        /// <param name="c">The centre cell, which is never itself returned.</param>
        /// <param name="includeDiagonals">
        /// When <c>false</c>, yields the four orthogonal neighbours <c>(+/-1, 0)</c> and
        /// <c>(0, +/-1)</c>. When <c>true</c>, yields those four followed by the four diagonals
        /// <c>(+/-1, +/-1)</c>, for eight in total.
        /// </param>
        /// <returns>
        /// The neighbouring cells in a fixed, deterministic order. No bounds checking is performed;
        /// callers filter with <see cref="IBattleGrid.InBounds"/>.
        /// </returns>
        public static IEnumerable<GridCoord> Neighbors(GridCoord c, bool includeDiagonals)
        {
            yield return new GridCoord(c.X + 1, c.Y);
            yield return new GridCoord(c.X - 1, c.Y);
            yield return new GridCoord(c.X, c.Y + 1);
            yield return new GridCoord(c.X, c.Y - 1);

            if (!includeDiagonals)
            {
                yield break;
            }

            yield return new GridCoord(c.X + 1, c.Y + 1);
            yield return new GridCoord(c.X + 1, c.Y - 1);
            yield return new GridCoord(c.X - 1, c.Y + 1);
            yield return new GridCoord(c.X - 1, c.Y - 1);
        }

        /// <summary>
        /// Local integer absolute value. Hand-rolled because <c>UnityEngine.Mathf</c> is unavailable
        /// in this assembly and <c>System.Math.Abs</c> throws on <see cref="int.MinValue"/>; grid
        /// deltas never approach that, but this keeps the helper allocation- and branch-cheap.
        /// </summary>
        private static int Abs(int value)
        {
            return value < 0 ? -value : value;
        }
    }
}
