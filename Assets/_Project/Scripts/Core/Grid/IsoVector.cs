using System.Globalization;

namespace BinakayanRising.Core.Grid
{
    /// <summary>
    /// A two-component float point in world units. This is the Core-side stand-in for a world-space
    /// 2D position so that the simulation never has to touch <c>UnityEngine.Vector2</c>.
    /// </summary>
    /// <remarks>
    /// Gameplay code converts this to a <c>Vector3</c> at the assembly boundary, e.g.
    /// <c>new Vector3(iso.X, iso.Y, 0f)</c>, choosing whatever depth/sorting rule the renderer needs.
    /// Kept intentionally minimal: this is a transport type, not a general-purpose math library.
    /// </remarks>
    public readonly struct IsoVector
    {
        /// <summary>The point <c>(0, 0)</c>.</summary>
        public static readonly IsoVector Zero = new IsoVector(0f, 0f);

        /// <summary>Horizontal world component.</summary>
        public float X { get; }

        /// <summary>Vertical world component.</summary>
        public float Y { get; }

        /// <summary>Creates a point from its two components.</summary>
        /// <param name="x">Horizontal world component.</param>
        /// <param name="y">Vertical world component.</param>
        public IsoVector(float x, float y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Component-wise addition.</summary>
        public static IsoVector operator +(IsoVector left, IsoVector right)
        {
            return new IsoVector(left.X + right.X, left.Y + right.Y);
        }

        /// <summary>Component-wise subtraction.</summary>
        public static IsoVector operator -(IsoVector left, IsoVector right)
        {
            return new IsoVector(left.X - right.X, left.Y - right.Y);
        }

        /// <summary>Scales both components by <paramref name="scalar"/>.</summary>
        public static IsoVector operator *(IsoVector value, float scalar)
        {
            return new IsoVector(value.X * scalar, value.Y * scalar);
        }

        /// <summary>Scales both components by <paramref name="scalar"/>.</summary>
        public static IsoVector operator *(float scalar, IsoVector value)
        {
            return new IsoVector(value.X * scalar, value.Y * scalar);
        }

        /// <summary>
        /// Returns the point in <c>(x, y)</c> form using the invariant culture, so log output is
        /// identical regardless of the machine's locale.
        /// </summary>
        public override string ToString()
        {
            return "("
                + X.ToString("0.###", CultureInfo.InvariantCulture)
                + ", "
                + Y.ToString("0.###", CultureInfo.InvariantCulture)
                + ")";
        }
    }
}
