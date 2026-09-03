using System.Collections.Generic;
using System.Linq;
using BinakayanRising.Core.Grid;
using NUnit.Framework;

namespace BinakayanRising.Tests.Grid
{
    /// <summary>
    /// Covers the square-grid distance metrics and the adjacency enumeration the pathfinder relies on.
    /// </summary>
    [TestFixture]
    public class GridDistanceTests
    {
        [TestCase(0, 0, 0, 0, 0)]
        [TestCase(0, 0, 1, 0, 1)]
        [TestCase(0, 0, 0, 1, 1)]
        [TestCase(0, 0, 1, 1, 2)]
        [TestCase(0, 0, 3, 4, 7)]
        [TestCase(3, 4, 0, 0, 7)]
        [TestCase(-2, -3, 2, 3, 10)]
        [TestCase(5, 5, 5, 9, 4)]
        public void Manhattan_ReturnsOrthogonalStepCount(int ax, int ay, int bx, int by, int expected)
        {
            Assert.AreEqual(expected, GridDistance.Manhattan(new GridCoord(ax, ay), new GridCoord(bx, by)));
        }

        [TestCase(0, 0, 0, 0, 0)]
        [TestCase(0, 0, 1, 0, 1)]
        [TestCase(0, 0, 1, 1, 1)]
        [TestCase(0, 0, 3, 4, 4)]
        [TestCase(3, 4, 0, 0, 4)]
        [TestCase(-2, -3, 2, 3, 6)]
        [TestCase(5, 5, 9, 6, 4)]
        public void Chebyshev_ReturnsDiagonalStepCount(int ax, int ay, int bx, int by, int expected)
        {
            Assert.AreEqual(expected, GridDistance.Chebyshev(new GridCoord(ax, ay), new GridCoord(bx, by)));
        }

        [Test]
        public void Manhattan_IsSymmetric()
        {
            GridCoord a = new GridCoord(2, -7);
            GridCoord b = new GridCoord(-4, 3);

            Assert.AreEqual(GridDistance.Manhattan(a, b), GridDistance.Manhattan(b, a));
        }

        [Test]
        public void Chebyshev_NeverExceedsManhattan()
        {
            GridCoord a = new GridCoord(2, -7);
            GridCoord b = new GridCoord(-4, 3);

            Assert.LessOrEqual(GridDistance.Chebyshev(a, b), GridDistance.Manhattan(a, b));
        }

        [Test]
        public void Neighbors_WithoutDiagonals_ReturnsFourCells()
        {
            List<GridCoord> neighbors = GridDistance.Neighbors(new GridCoord(4, 7), false).ToList();

            Assert.AreEqual(4, neighbors.Count);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    new GridCoord(5, 7),
                    new GridCoord(3, 7),
                    new GridCoord(4, 8),
                    new GridCoord(4, 6)
                },
                neighbors);
        }

        [Test]
        public void Neighbors_WithDiagonals_ReturnsEightCells()
        {
            List<GridCoord> neighbors = GridDistance.Neighbors(new GridCoord(0, 0), true).ToList();

            Assert.AreEqual(8, neighbors.Count);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    new GridCoord(1, 0),
                    new GridCoord(-1, 0),
                    new GridCoord(0, 1),
                    new GridCoord(0, -1),
                    new GridCoord(1, 1),
                    new GridCoord(1, -1),
                    new GridCoord(-1, 1),
                    new GridCoord(-1, -1)
                },
                neighbors);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Neighbors_AreDistinctAndExcludeTheCentre(bool includeDiagonals)
        {
            GridCoord centre = new GridCoord(-3, 6);

            List<GridCoord> neighbors = GridDistance.Neighbors(centre, includeDiagonals).ToList();

            CollectionAssert.DoesNotContain(neighbors, centre);
            Assert.AreEqual(neighbors.Count, neighbors.Distinct().Count(), "neighbours must be distinct");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Neighbors_AreAllOneChebyshevStepAway(bool includeDiagonals)
        {
            GridCoord centre = new GridCoord(11, -2);

            foreach (GridCoord neighbor in GridDistance.Neighbors(centre, includeDiagonals))
            {
                Assert.AreEqual(1, GridDistance.Chebyshev(centre, neighbor), "for " + neighbor);
            }
        }

        [Test]
        public void Neighbors_WithoutDiagonals_AreAllOneManhattanStepAway()
        {
            GridCoord centre = new GridCoord(11, -2);

            foreach (GridCoord neighbor in GridDistance.Neighbors(centre, false))
            {
                Assert.AreEqual(1, GridDistance.Manhattan(centre, neighbor), "for " + neighbor);
            }
        }

        [Test]
        public void Neighbors_WithDiagonals_IsSupersetOfOrthogonalNeighbors()
        {
            GridCoord centre = new GridCoord(2, 2);

            List<GridCoord> orthogonal = GridDistance.Neighbors(centre, false).ToList();
            List<GridCoord> all = GridDistance.Neighbors(centre, true).ToList();

            CollectionAssert.IsSubsetOf(orthogonal, all);
        }
    }
}
