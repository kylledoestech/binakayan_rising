using System;
using System.Collections.Generic;

namespace BinakayanRising.Core.Grid
{
    /// <summary>
    /// Shortest walks across a rectangular grid of open and blocked cells, for anything that
    /// moves freely rather than by the battle's movement rules — the player walking the
    /// encampment, keepers stepping aside.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A* over the eight neighbours, costing 10 for a straight step and 14 for a diagonal. A
    /// diagonal is only taken when both cells it squeezes between are open, so a walk never
    /// cuts the corner of a building.
    /// </para>
    /// <para>
    /// Ties are broken by the order neighbours are tried, which is fixed, so the same request
    /// always returns the same path: the player sees the same route every time they click.
    /// </para>
    /// </remarks>
    public static class GridPathfinder
    {
        private const int StraightCost = 10;
        private const int DiagonalCost = 14;

        private static readonly GridCoord[] Steps =
        {
            new GridCoord(1, 0), new GridCoord(0, 1), new GridCoord(-1, 0), new GridCoord(0, -1),
            new GridCoord(1, 1), new GridCoord(-1, 1), new GridCoord(-1, -1), new GridCoord(1, -1)
        };

        /// <summary>
        /// The cells from <paramref name="from"/> to <paramref name="to"/>, both included, or null
        /// when there is no way through.
        /// </summary>
        /// <param name="width">Grid width; cells run 0..width-1 on X.</param>
        /// <param name="height">Grid height; cells run 0..height-1 on Y.</param>
        /// <param name="walkable">Whether a cell inside the grid can be stood on.</param>
        /// <param name="from">The start. It does not have to be walkable itself.</param>
        /// <param name="to">The goal. Must be walkable.</param>
        public static List<GridCoord> FindPath(int width, int height, Func<GridCoord, bool> walkable, GridCoord from, GridCoord to)
        {
            if (walkable == null) throw new ArgumentNullException("walkable");

            if (!Inside(width, height, from) || !Inside(width, height, to) || !walkable(to))
            {
                return null;
            }

            if (from == to)
            {
                return new List<GridCoord> { from };
            }

            int count = width * height;
            if (count > 0xFFFF)
            {
                // The queue key packs a cell index into 16 bits.
                throw new ArgumentOutOfRangeException("width", "Grids above 65535 cells are not supported.");
            }

            var cost = new int[count];
            var came = new int[count];
            var closed = new bool[count];
            for (int i = 0; i < count; i++)
            {
                cost[i] = int.MaxValue;
                came[i] = -1;
            }

            // A sorted set keyed on (estimate, insertion order) is an ordered priority queue;
            // the grids here are a few hundred cells, so anything cleverer is wasted.
            var open = new SortedSet<long>();
            int start = Index(width, from);
            cost[start] = 0;
            long order = 0;
            open.Add(Key(Estimate(from, to), order++, start));

            int goal = Index(width, to);
            while (open.Count > 0)
            {
                long best = open.Min;
                open.Remove(best);
                int current = (int)(best & 0xFFFF);
                if (closed[current])
                {
                    continue;
                }

                if (current == goal)
                {
                    return Walk(width, came, goal);
                }

                closed[current] = true;
                var here = new GridCoord(current % width, current / width);

                for (int s = 0; s < Steps.Length; s++)
                {
                    GridCoord step = Steps[s];
                    var next = new GridCoord(here.X + step.X, here.Y + step.Y);
                    if (!Inside(width, height, next) || !walkable(next))
                    {
                        continue;
                    }

                    bool diagonal = step.X != 0 && step.Y != 0;
                    if (diagonal && (!walkable(new GridCoord(here.X + step.X, here.Y)) || !walkable(new GridCoord(here.X, here.Y + step.Y))))
                    {
                        continue;
                    }

                    int index = Index(width, next);
                    int tentative = cost[current] + (diagonal ? DiagonalCost : StraightCost);
                    if (closed[index] || tentative >= cost[index])
                    {
                        continue;
                    }

                    cost[index] = tentative;
                    came[index] = current;
                    open.Add(Key(tentative + Estimate(next, to), order++, index));
                }
            }

            return null;
        }

        /// <summary>True when every walkable cell can reach every other.</summary>
        public static bool IsConnected(int width, int height, Func<GridCoord, bool> walkable)
        {
            GridCoord? first = null;
            int open = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var cell = new GridCoord(x, y);
                    if (walkable(cell))
                    {
                        open++;
                        if (first == null)
                        {
                            first = cell;
                        }
                    }
                }
            }

            if (first == null)
            {
                return true;
            }

            var seen = new HashSet<GridCoord> { first.Value };
            var queue = new Queue<GridCoord>();
            queue.Enqueue(first.Value);
            while (queue.Count > 0)
            {
                GridCoord here = queue.Dequeue();
                for (int s = 0; s < 4; s++)
                {
                    var next = new GridCoord(here.X + Steps[s].X, here.Y + Steps[s].Y);
                    if (Inside(width, height, next) && walkable(next) && seen.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return seen.Count == open;
        }

        private static bool Inside(int width, int height, GridCoord cell)
        {
            return cell.X >= 0 && cell.Y >= 0 && cell.X < width && cell.Y < height;
        }

        private static int Index(int width, GridCoord cell)
        {
            return (cell.Y * width) + cell.X;
        }

        /// <summary>Octile distance: exact on an empty grid, so the search never overestimates.</summary>
        private static int Estimate(GridCoord a, GridCoord b)
        {
            int dx = Math.Abs(a.X - b.X);
            int dy = Math.Abs(a.Y - b.Y);
            int diagonal = Math.Min(dx, dy);
            return (DiagonalCost * diagonal) + (StraightCost * (Math.Max(dx, dy) - diagonal));
        }

        /// <summary>Priority in the high bits, insertion order next, the cell index in the low 16.</summary>
        private static long Key(int priority, long order, int index)
        {
            return ((long)priority << 40) | ((order & 0xFFFFFF) << 16) | (long)index;
        }

        private static List<GridCoord> Walk(int width, int[] came, int goal)
        {
            var path = new List<GridCoord>();
            for (int at = goal; at >= 0; at = came[at])
            {
                path.Add(new GridCoord(at % width, at / width));
            }

            path.Reverse();
            return path;
        }
    }
}
