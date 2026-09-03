using System;

namespace BinakayanRising.Core.Grid
{
    /// <summary>
    /// An integer cell coordinate on the battle grid.
    /// </summary>
    /// <remarks>
    /// The simulation grid is logically square: a cell has four orthogonal neighbours and four
    /// diagonal neighbours. Only the <em>presentation</em> layer is isometric, and that projection
    /// lives entirely in <see cref="IsoGridLayout"/>.
    /// <para>
    /// This type is deliberately free of any UnityEngine dependency (see
    /// <c>BinakayanRising.Core.asmdef</c>, which sets <c>noEngineReferences</c>) so that the battle
    /// simulation stays deterministic and unit-testable outside the Unity runtime.
    /// </para>
    /// </remarks>
    public readonly struct GridCoord : IEquatable<GridCoord>
    {
        /// <summary>The origin cell, <c>(0, 0)</c>.</summary>
        public static readonly GridCoord Zero = new GridCoord(0, 0);

        /// <summary>Column index of the cell.</summary>
        public int X { get; }

        /// <summary>Row index of the cell.</summary>
        public int Y { get; }

        /// <summary>Creates a coordinate at the given column and row.</summary>
        /// <param name="x">Column index.</param>
        /// <param name="y">Row index.</param>
        public GridCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <inheritdoc />
        public bool Equals(GridCoord other)
        {
            return X == other.X && Y == other.Y;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is GridCoord other && Equals(other);
        }

        /// <summary>
        /// Stable hash combining both components. Deterministic across runs and platforms, which
        /// matters because coordinates are used as dictionary keys inside the simulation.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        /// <summary>Returns the coordinate in <c>(x, y)</c> form.</summary>
        public override string ToString()
        {
            return "(" + X + ", " + Y + ")";
        }

        /// <summary>Value equality.</summary>
        public static bool operator ==(GridCoord left, GridCoord right)
        {
            return left.Equals(right);
        }

        /// <summary>Value inequality.</summary>
        public static bool operator !=(GridCoord left, GridCoord right)
        {
            return !left.Equals(right);
        }

        /// <summary>Component-wise addition, typically used to apply an offset to a cell.</summary>
        public static GridCoord operator +(GridCoord left, GridCoord right)
        {
            return new GridCoord(left.X + right.X, left.Y + right.Y);
        }

        /// <summary>Component-wise subtraction, yielding the offset from <paramref name="right"/> to <paramref name="left"/>.</summary>
        public static GridCoord operator -(GridCoord left, GridCoord right)
        {
            return new GridCoord(left.X - right.X, left.Y - right.Y);
        }
    }
}
