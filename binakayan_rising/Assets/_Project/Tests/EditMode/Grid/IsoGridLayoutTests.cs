using System;
using BinakayanRising.Core.Grid;
using NUnit.Framework;

namespace BinakayanRising.Tests.Grid
{
    /// <summary>
    /// Verifies the diamond isometric projection and, above all, that it is exactly invertible.
    /// A broken round trip would mean a click landing on the wrong tile, so it is tested exhaustively.
    /// </summary>
    [TestFixture]
    public class IsoGridLayoutTests
    {
        private const float Tolerance = 1e-5f;

        [Test]
        public void Default_UsesClassicTwoToOneDiamond()
        {
            Assert.AreEqual(1.0f, IsoGridLayout.Default.TileWidth, Tolerance);
            Assert.AreEqual(0.5f, IsoGridLayout.Default.TileHeight, Tolerance);
        }

        [Test]
        public void Constructor_DefaultArguments_MatchDefaultLayout()
        {
            IsoGridLayout layout = new IsoGridLayout();

            Assert.AreEqual(IsoGridLayout.DefaultTileWidth, layout.TileWidth, Tolerance);
            Assert.AreEqual(IsoGridLayout.DefaultTileHeight, layout.TileHeight, Tolerance);
        }

        [TestCase(0f, 0.5f)]
        [TestCase(-1f, 0.5f)]
        [TestCase(1f, 0f)]
        [TestCase(1f, -0.5f)]
        public void Constructor_NonPositiveTileSize_Throws(float tileWidth, float tileHeight)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new IsoGridLayout(tileWidth, tileHeight));
        }

        [Test]
        public void CellToWorld_Origin_IsWorldOrigin()
        {
            IsoVector world = IsoGridLayout.Default.CellToWorld(GridCoord.Zero);

            Assert.AreEqual(0f, world.X, Tolerance);
            Assert.AreEqual(0f, world.Y, Tolerance);
        }

        // Hand-computed against worldX = (X - Y) * 0.5 and worldY = (X + Y) * 0.25
        // for the default 1.0 x 0.5 tile.
        [TestCase(0, 0, 0f, 0f)]
        [TestCase(1, 0, 0.5f, 0.25f)]
        [TestCase(0, 1, -0.5f, 0.25f)]
        [TestCase(1, 1, 0f, 0.5f)]
        [TestCase(3, 1, 1.0f, 1.0f)]
        [TestCase(2, 2, 0f, 1.0f)]
        [TestCase(-2, 1, -1.5f, -0.25f)]
        [TestCase(4, 0, 2.0f, 1.0f)]
        public void CellToWorld_DefaultLayout_MatchesHandComputedProjection(
            int cellX, int cellY, float expectedWorldX, float expectedWorldY)
        {
            IsoVector world = IsoGridLayout.Default.CellToWorld(new GridCoord(cellX, cellY));

            Assert.AreEqual(expectedWorldX, world.X, Tolerance, "world X");
            Assert.AreEqual(expectedWorldY, world.Y, Tolerance, "world Y");
        }

        // Hand-computed for a 2.0 x 1.0 tile: worldX = (X - Y) * 1.0, worldY = (X + Y) * 0.5.
        [TestCase(3, 1, 2.0f, 2.0f)]
        [TestCase(0, 2, -2.0f, 1.0f)]
        public void CellToWorld_CustomLayout_MatchesHandComputedProjection(
            int cellX, int cellY, float expectedWorldX, float expectedWorldY)
        {
            IsoGridLayout layout = new IsoGridLayout(2.0f, 1.0f);

            IsoVector world = layout.CellToWorld(new GridCoord(cellX, cellY));

            Assert.AreEqual(expectedWorldX, world.X, Tolerance, "world X");
            Assert.AreEqual(expectedWorldY, world.Y, Tolerance, "world Y");
        }

        [Test]
        public void CellToWorld_PositiveGridX_MovesRightAndUpOnScreen()
        {
            IsoVector world = IsoGridLayout.Default.CellToWorld(new GridCoord(1, 0));

            Assert.Greater(world.X, 0f, "grid +X should move right on screen");
            Assert.Greater(world.Y, 0f, "grid +X should move up on screen");
        }

        [Test]
        public void CellToWorld_PositiveGridY_MovesLeftAndUpOnScreen()
        {
            IsoVector world = IsoGridLayout.Default.CellToWorld(new GridCoord(0, 1));

            Assert.Less(world.X, 0f, "grid +Y should move left on screen");
            Assert.Greater(world.Y, 0f, "grid +Y should move up on screen");
        }

        [Test]
        public void WorldToCell_IsExactInverseOfCellToWorld_OverTwentyByTwentyRange()
        {
            AssertRoundTripsOverRange(IsoGridLayout.Default, -10, 10);
        }

        [TestCase(1.0f, 0.5f)]
        [TestCase(2.0f, 1.0f)]
        [TestCase(1.28f, 0.64f)]
        [TestCase(0.64f, 0.32f)]
        [TestCase(3.0f, 1.0f)]
        [TestCase(1.0f, 1.0f)]
        public void WorldToCell_IsExactInverseOfCellToWorld_ForVariousTileSizes(
            float tileWidth, float tileHeight)
        {
            AssertRoundTripsOverRange(new IsoGridLayout(tileWidth, tileHeight), -10, 10);
        }

        [Test]
        public void WorldToCell_IsExactInverseOfCellToWorld_ForLargeCoordinates()
        {
            AssertRoundTripsOverRange(IsoGridLayout.Default, 500, 520);
        }

        // Offsets that stay strictly inside the tile diamond for the default 1.0 x 0.5 tile.
        // From the cell centre the diamond reaches +/-0.5 in world X (half a tile width) and
        // +/-0.25 in world Y (half a tile height) along the axes, tapering in between, so every
        // sample below sits comfortably inside it.
        [TestCase(0.2f, 0f)]
        [TestCase(-0.2f, 0f)]
        [TestCase(0f, 0.1f)]
        [TestCase(0f, -0.1f)]
        [TestCase(0.1f, 0.05f)]
        [TestCase(-0.1f, -0.05f)]
        [TestCase(0.15f, -0.02f)]
        [TestCase(-0.15f, 0.02f)]
        public void WorldToCell_SmallOffsetWithinTile_ReturnsSameCell(float offsetX, float offsetY)
        {
            IsoGridLayout layout = IsoGridLayout.Default;
            IsoVector offset = new IsoVector(offsetX, offsetY);

            for (int x = -5; x < 5; x++)
            {
                for (int y = -5; y < 5; y++)
                {
                    GridCoord cell = new GridCoord(x, y);
                    IsoVector nudged = layout.CellToWorld(cell) + offset;

                    Assert.AreEqual(
                        cell,
                        layout.WorldToCell(nudged),
                        "Offset " + offset + " inside the tile should still resolve to " + cell + ".");
                }
            }
        }

        [Test]
        public void WorldToCell_WorldOrigin_IsOriginCell()
        {
            Assert.AreEqual(GridCoord.Zero, IsoGridLayout.Default.WorldToCell(IsoVector.Zero));
        }

        /// <summary>
        /// Asserts <c>WorldToCell(CellToWorld(c)) == c</c> for every cell in the half-open square
        /// <c>[min, max)</c> on both axes.
        /// </summary>
        private static void AssertRoundTripsOverRange(IsoGridLayout layout, int min, int max)
        {
            for (int x = min; x < max; x++)
            {
                for (int y = min; y < max; y++)
                {
                    GridCoord cell = new GridCoord(x, y);
                    IsoVector world = layout.CellToWorld(cell);
                    GridCoord roundTripped = layout.WorldToCell(world);

                    Assert.AreEqual(
                        cell,
                        roundTripped,
                        "Round trip failed for " + cell + " via world " + world
                            + " on a " + layout.TileWidth + " x " + layout.TileHeight + " tile.");
                }
            }
        }
    }
}
