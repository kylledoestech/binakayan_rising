using System.Collections.Generic;
using BinakayanRising.Core.Grid;
using NUnit.Framework;

namespace BinakayanRising.Tests.Grid
{
    [TestFixture]
    public class GridPathfinderTests
    {
        private static bool Open(GridCoord cell)
        {
            return true;
        }

        [Test]
        public void AnOpenGridIsCrossedDiagonally()
        {
            List<GridCoord> path = GridPathfinder.FindPath(5, 5, Open, new GridCoord(0, 0), new GridCoord(4, 4));
            Assert.AreEqual(5, path.Count);
            Assert.AreEqual(new GridCoord(0, 0), path[0]);
            Assert.AreEqual(new GridCoord(4, 4), path[4]);
        }

        [Test]
        public void AWallIsWalkedAroundWithoutCuttingItsCorner()
        {
            // A wall on x = 2 from y = 0 to y = 3; the only gap is at y = 4.
            System.Func<GridCoord, bool> walkable = c => !(c.X == 2 && c.Y <= 3);
            List<GridCoord> path = GridPathfinder.FindPath(5, 5, walkable, new GridCoord(0, 0), new GridCoord(4, 0));

            Assert.IsNotNull(path);
            for (int i = 0; i < path.Count; i++)
            {
                Assert.IsTrue(walkable(path[i]), "Walked through the wall at " + path[i]);
                if (i > 0)
                {
                    GridCoord a = path[i - 1];
                    GridCoord b = path[i];
                    Assert.LessOrEqual(System.Math.Abs(a.X - b.X), 1);
                    Assert.LessOrEqual(System.Math.Abs(a.Y - b.Y), 1);
                    if (a.X != b.X && a.Y != b.Y)
                    {
                        Assert.IsTrue(walkable(new GridCoord(b.X, a.Y)) && walkable(new GridCoord(a.X, b.Y)),
                            "Cut a corner between " + a + " and " + b);
                    }
                }
            }
        }

        [Test]
        public void ABlockedGoalOrASealedRoomHasNoPath()
        {
            Assert.IsNull(GridPathfinder.FindPath(3, 3, c => c != new GridCoord(2, 2), new GridCoord(0, 0), new GridCoord(2, 2)));

            // (2, 2) is open but walled in by (1, 2), (2, 1) and the grid's edge.
            System.Func<GridCoord, bool> sealedIn = c => c != new GridCoord(1, 2) && c != new GridCoord(2, 1) && c != new GridCoord(1, 1);
            Assert.IsNull(GridPathfinder.FindPath(3, 3, sealedIn, new GridCoord(0, 0), new GridCoord(2, 2)));
            Assert.IsFalse(GridPathfinder.IsConnected(3, 3, sealedIn));
        }

        [Test]
        public void TheSameRequestAlwaysGivesTheSamePath()
        {
            List<GridCoord> first = GridPathfinder.FindPath(8, 8, Open, new GridCoord(0, 3), new GridCoord(7, 5));
            List<GridCoord> second = GridPathfinder.FindPath(8, 8, Open, new GridCoord(0, 3), new GridCoord(7, 5));
            CollectionAssert.AreEqual(first, second);
        }
    }
}
