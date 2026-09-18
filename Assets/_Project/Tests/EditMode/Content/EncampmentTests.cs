using System.Collections.Generic;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Grid;
using NUnit.Framework;

namespace BinakayanRising.Tests.Content
{
    /// <summary>The encampment map's promises: no overlaps, and everything reachable on foot.</summary>
    [TestFixture]
    public class EncampmentTests
    {
        [Test]
        public void EveryObjectivePlaceExistsInTheCamp()
        {
            string[] places =
            {
                Places.Aide, Places.MissionTent, Places.Farm, Places.Mine, Places.Exchange,
                Places.Training, Places.Recruitment, Places.Armory, Places.Library
            };

            foreach (string place in places)
            {
                Assert.IsNotNull(Encampment.StandCell(place), place + " has nowhere to stand.");
            }

            foreach (HubTask task in Campaign.Tasks)
            {
                Assert.IsNotNull(Encampment.StandCell(task.Place), task.Id + " points at " + task.Place + ", which is not in the camp.");
            }
        }

        [Test]
        public void NothingOverlapsAndEverythingIsInsideTheGrid()
        {
            var taken = new Dictionary<GridCoord, string>();
            foreach (CampSite site in Encampment.Sites)
            {
                for (int x = 0; x < site.Width; x++)
                {
                    for (int y = 0; y < site.Height; y++)
                    {
                        Claim(taken, new GridCoord(site.Origin.X + x, site.Origin.Y + y), site.Place);
                    }
                }
            }

            foreach (CampFigure figure in Encampment.Figures)
            {
                Claim(taken, figure.Cell, figure.Character);
            }

            foreach (CampProp prop in Encampment.Props)
            {
                Claim(taken, prop.Cell, prop.Art);
            }
        }

        private static void Claim(Dictionary<GridCoord, string> taken, GridCoord cell, string by)
        {
            Assert.IsTrue(Encampment.Inside(cell), by + " sits outside the grid at " + cell);
            string other;
            Assert.IsFalse(taken.TryGetValue(cell, out other), by + " overlaps " + other + " at " + cell);
            taken[cell] = by;
        }

        [Test]
        public void EveryDoorAndKeeperCanBeReachedFromTheSpawn()
        {
            Assert.IsTrue(Encampment.IsWalkable(Encampment.Spawn), "The player appears inside something.");

            foreach (CampSite site in Encampment.Sites)
            {
                Assert.IsNotNull(Encampment.Path(Encampment.Spawn, site.Door), "Cannot walk to the " + site.Place + " door.");
                Assert.IsTrue(IsBeside(site, site.Door), site.Place + "'s door does not touch the building.");
            }

            foreach (CampFigure figure in Encampment.Figures)
            {
                Assert.IsNotNull(Encampment.Path(Encampment.Spawn, figure.Talk), "Cannot walk to " + figure.Character + ".");
                Assert.LessOrEqual(Chebyshev(figure.Cell, figure.Talk), 1, figure.Character + " is talked to from too far away.");
            }
        }

        [Test]
        public void TheOpenGroundIsOnePiece()
        {
            Assert.IsTrue(GridPathfinder.IsConnected(Encampment.Width, Encampment.Height, Encampment.IsWalkable),
                "Some open cells cannot be reached, which leaves a click on them doing nothing.");
        }

        [Test]
        public void KeepersStandBesideTheirOwnDoor()
        {
            foreach (CampSite site in Encampment.Sites)
            {
                if (site.Keeper == null)
                {
                    continue;
                }

                CampFigure keeper = Encampment.Figure(site.Keeper);
                Assert.IsNotNull(keeper, site.Place + "'s keeper is not standing in the camp.");
                Assert.AreEqual(site.Door, keeper.Talk, site.Place + "'s keeper is talked to from somewhere other than the door.");
            }
        }

        private static bool IsBeside(CampSite site, GridCoord cell)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (site.Covers(new GridCoord(cell.X + dx, cell.Y + dy)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int Chebyshev(GridCoord a, GridCoord b)
        {
            return System.Math.Max(System.Math.Abs(a.X - b.X), System.Math.Abs(a.Y - b.Y));
        }
    }
}
