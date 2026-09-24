using System.Collections.Generic;
using BinakayanRising.Core.Grid;
using NUnit.Framework;

namespace BinakayanRising.Tests.Grid
{
    /// <summary>
    /// Covers the deployment drop rule shared by click-to-place and drag-and-drop: which cells
    /// take a unit, moving a unit already down, occupied cells and the squad cap.
    /// </summary>
    [TestFixture]
    public class DeploymentDropTests
    {
        private BattleGrid grid;
        private Dictionary<int, GridCoord> placements;

        [SetUp]
        public void SetUp()
        {
            // A 4x3 map whose first column is the deployment zone.
            grid = new BattleGrid(4, 3);
            for (int y = 0; y < grid.Height; y++)
            {
                grid.SetDeployable(new GridCoord(0, y), true);
            }

            placements = new Dictionary<int, GridCoord>();
        }

        [Test]
        public void FreeDeployableCell_PlacesReserveUnit()
        {
            Assert.AreEqual(DropVerdict.Placed, DeploymentDrop.Judge(grid, placements, 1, new GridCoord(0, 0), 6));
        }

        [Test]
        public void CellOutsideZone_IsNotDeployable()
        {
            Assert.AreEqual(DropVerdict.NotDeployable, DeploymentDrop.Judge(grid, placements, 1, new GridCoord(2, 1), 6));
        }

        [Test]
        public void CellOffMap_IsOutOfBounds()
        {
            Assert.AreEqual(DropVerdict.OutOfBounds, DeploymentDrop.Judge(grid, placements, 1, new GridCoord(-1, 0), 6));
            Assert.AreEqual(DropVerdict.OutOfBounds, DeploymentDrop.Judge(null, placements, 1, new GridCoord(0, 0), 6));
        }

        [Test]
        public void CellHeldByAnotherUnit_IsOccupied()
        {
            placements[2] = new GridCoord(0, 1);

            Assert.AreEqual(DropVerdict.Occupied, DeploymentDrop.Judge(grid, placements, 1, new GridCoord(0, 1), 6));
            Assert.AreEqual(DropVerdict.Occupied, DeploymentDrop.Judge(grid, placements, 3, new GridCoord(0, 1), 6));
        }

        [Test]
        public void UnitOnBoard_MovesToFreeCell()
        {
            placements[1] = new GridCoord(0, 0);

            Assert.AreEqual(DropVerdict.Moved, DeploymentDrop.Judge(grid, placements, 1, new GridCoord(0, 2), 6));
        }

        [Test]
        public void UnitDroppedOnItsOwnCell_IsUnchanged()
        {
            placements[1] = new GridCoord(0, 0);

            DropVerdict verdict = DeploymentDrop.Judge(grid, placements, 1, new GridCoord(0, 0), 6);

            Assert.AreEqual(DropVerdict.Unchanged, verdict);
            Assert.IsFalse(DeploymentDrop.Accepts(verdict));
        }

        [Test]
        public void FullSquad_RefusesReserveUnit_ButStillMovesPlacedOne()
        {
            placements[1] = new GridCoord(0, 0);
            placements[2] = new GridCoord(0, 1);

            Assert.AreEqual(DropVerdict.SquadFull, DeploymentDrop.Judge(grid, placements, 3, new GridCoord(0, 2), 2));
            Assert.AreEqual(DropVerdict.Moved, DeploymentDrop.Judge(grid, placements, 1, new GridCoord(0, 2), 2));
        }

        [Test]
        public void Accepts_OnlyPlacedAndMoved()
        {
            Assert.IsTrue(DeploymentDrop.Accepts(DropVerdict.Placed));
            Assert.IsTrue(DeploymentDrop.Accepts(DropVerdict.Moved));
            Assert.IsFalse(DeploymentDrop.Accepts(DropVerdict.Occupied));
            Assert.IsFalse(DeploymentDrop.Accepts(DropVerdict.SquadFull));
            Assert.IsFalse(DeploymentDrop.Accepts(DropVerdict.NotDeployable));
            Assert.IsFalse(DeploymentDrop.Accepts(DropVerdict.OutOfBounds));
        }
    }
}
