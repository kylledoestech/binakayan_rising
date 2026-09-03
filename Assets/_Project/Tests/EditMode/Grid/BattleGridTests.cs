using System;
using BinakayanRising.Core.Grid;
using NUnit.Framework;

namespace BinakayanRising.Tests.Grid
{
    /// <summary>
    /// Covers the plain-C# battle map: bounds handling, terrain storage, deployment marking, and the
    /// passability rule (only <see cref="TerrainType.BambooBarricade"/> blocks movement).
    /// </summary>
    [TestFixture]
    public class BattleGridTests
    {
        [Test]
        public void Constructor_StoresDimensions()
        {
            BattleGrid grid = new BattleGrid(12, 8);

            Assert.AreEqual(12, grid.Width);
            Assert.AreEqual(8, grid.Height);
        }

        [Test]
        public void Constructor_DefaultFill_IsStandardGrid()
        {
            BattleGrid grid = new BattleGrid(3, 3);

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Assert.AreEqual(TerrainType.StandardGrid, grid.GetTerrain(new GridCoord(x, y)));
                }
            }
        }

        [Test]
        public void Constructor_ExplicitFill_AppliesToEveryCell()
        {
            BattleGrid grid = new BattleGrid(3, 2, TerrainType.Trench);

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Assert.AreEqual(TerrainType.Trench, grid.GetTerrain(new GridCoord(x, y)));
                }
            }
        }

        [Test]
        public void Constructor_NoCellIsDeployableByDefault()
        {
            BattleGrid grid = new BattleGrid(4, 4);

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Assert.IsFalse(grid.IsDeployable(new GridCoord(x, y)));
                }
            }
        }

        [TestCase(0, 0)]
        [TestCase(0, 5)]
        [TestCase(5, 0)]
        [TestCase(-1, 5)]
        [TestCase(5, -1)]
        [TestCase(-3, -3)]
        public void Constructor_NonPositiveDimensions_Throws(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleGrid(width, height));
        }

        [TestCase(0, 0, true)]
        [TestCase(4, 2, true)]
        [TestCase(0, 2, true)]
        [TestCase(4, 0, true)]
        [TestCase(-1, 0, false)]
        [TestCase(0, -1, false)]
        [TestCase(5, 0, false)]
        [TestCase(0, 3, false)]
        [TestCase(5, 3, false)]
        [TestCase(-1, -1, false)]
        public void InBounds_MatchesGridExtent(int x, int y, bool expected)
        {
            BattleGrid grid = new BattleGrid(5, 3);

            Assert.AreEqual(expected, grid.InBounds(new GridCoord(x, y)));
        }

        [Test]
        public void SetTerrain_ThenGetTerrain_ReturnsTheStoredValue()
        {
            BattleGrid grid = new BattleGrid(6, 4);
            GridCoord cell = new GridCoord(2, 3);

            grid.SetTerrain(cell, TerrainType.CoastalShallows);

            Assert.AreEqual(TerrainType.CoastalShallows, grid.GetTerrain(cell));
        }

        [Test]
        public void SetTerrain_DoesNotDisturbNeighbouringCells()
        {
            BattleGrid grid = new BattleGrid(4, 4);
            GridCoord cell = new GridCoord(1, 2);

            grid.SetTerrain(cell, TerrainType.BambooBarricade);

            Assert.AreEqual(TerrainType.BambooBarricade, grid.GetTerrain(cell));
            Assert.AreEqual(TerrainType.StandardGrid, grid.GetTerrain(new GridCoord(2, 2)));
            Assert.AreEqual(TerrainType.StandardGrid, grid.GetTerrain(new GridCoord(0, 2)));
            Assert.AreEqual(TerrainType.StandardGrid, grid.GetTerrain(new GridCoord(1, 1)));
            Assert.AreEqual(TerrainType.StandardGrid, grid.GetTerrain(new GridCoord(1, 3)));
        }

        [Test]
        public void SetTerrain_EachCellIsIndependent()
        {
            BattleGrid grid = new BattleGrid(3, 3);
            TerrainType[] pattern =
            {
                TerrainType.StandardGrid,
                TerrainType.Trench,
                TerrainType.CoastalShallows,
                TerrainType.BambooBarricade,
                TerrainType.EncampmentTent
            };

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    grid.SetTerrain(new GridCoord(x, y), pattern[((y * grid.Width) + x) % pattern.Length]);
                }
            }

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Assert.AreEqual(
                        pattern[((y * grid.Width) + x) % pattern.Length],
                        grid.GetTerrain(new GridCoord(x, y)),
                        "at (" + x + ", " + y + ")");
                }
            }
        }

        [Test]
        public void SetDeployable_ThenIsDeployable_ReturnsTheStoredValue()
        {
            BattleGrid grid = new BattleGrid(5, 5);
            GridCoord cell = new GridCoord(0, 4);

            grid.SetDeployable(cell, true);

            Assert.IsTrue(grid.IsDeployable(cell));
            Assert.IsFalse(grid.IsDeployable(new GridCoord(1, 4)));

            grid.SetDeployable(cell, false);

            Assert.IsFalse(grid.IsDeployable(cell));
        }

        [Test]
        public void SetDeployable_DoesNotChangeTerrain()
        {
            BattleGrid grid = new BattleGrid(3, 3, TerrainType.Trench);
            GridCoord cell = new GridCoord(1, 1);

            grid.SetDeployable(cell, true);

            Assert.AreEqual(TerrainType.Trench, grid.GetTerrain(cell));
        }

        [Test]
        public void IsWalkable_BambooBarricade_IsNotWalkable()
        {
            BattleGrid grid = new BattleGrid(3, 3);
            GridCoord cell = new GridCoord(1, 1);

            grid.SetTerrain(cell, TerrainType.BambooBarricade);

            Assert.IsFalse(grid.IsWalkable(cell));
        }

        [TestCase(TerrainType.StandardGrid)]
        [TestCase(TerrainType.Trench)]
        [TestCase(TerrainType.CoastalShallows)]
        [TestCase(TerrainType.EncampmentTent)]
        public void IsWalkable_EveryTerrainExceptBambooBarricade_IsWalkable(TerrainType terrain)
        {
            BattleGrid grid = new BattleGrid(3, 3);
            GridCoord cell = new GridCoord(2, 0);

            grid.SetTerrain(cell, terrain);

            Assert.IsTrue(grid.IsWalkable(cell), terrain + " should be walkable");
        }

        [Test]
        public void IsWalkable_RecoversAfterBarricadeIsCleared()
        {
            BattleGrid grid = new BattleGrid(2, 2);
            GridCoord cell = new GridCoord(0, 0);

            grid.SetTerrain(cell, TerrainType.BambooBarricade);
            Assert.IsFalse(grid.IsWalkable(cell));

            grid.SetTerrain(cell, TerrainType.StandardGrid);
            Assert.IsTrue(grid.IsWalkable(cell));
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(4, 0)]
        [TestCase(0, 4)]
        [TestCase(99, 99)]
        public void GetTerrain_OutOfBounds_Throws(int x, int y)
        {
            BattleGrid grid = new BattleGrid(4, 4);

            Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetTerrain(new GridCoord(x, y)));
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(4, 0)]
        [TestCase(0, 4)]
        public void SetTerrain_OutOfBounds_Throws(int x, int y)
        {
            BattleGrid grid = new BattleGrid(4, 4);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => grid.SetTerrain(new GridCoord(x, y), TerrainType.Trench));
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(4, 0)]
        [TestCase(0, 4)]
        public void SetDeployable_OutOfBounds_Throws(int x, int y)
        {
            BattleGrid grid = new BattleGrid(4, 4);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => grid.SetDeployable(new GridCoord(x, y), true));
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(4, 0)]
        [TestCase(0, 4)]
        public void Predicates_OutOfBounds_ReturnFalseInsteadOfThrowing(int x, int y)
        {
            BattleGrid grid = new BattleGrid(4, 4);
            GridCoord cell = new GridCoord(x, y);

            Assert.IsFalse(grid.IsWalkable(cell), "IsWalkable off-map");
            Assert.IsFalse(grid.IsDeployable(cell), "IsDeployable off-map");
        }

        [Test]
        public void Grid_IsUsableThroughTheInterface()
        {
            BattleGrid concrete = new BattleGrid(3, 2, TerrainType.EncampmentTent);
            concrete.SetTerrain(new GridCoord(2, 1), TerrainType.BambooBarricade);
            concrete.SetDeployable(new GridCoord(0, 0), true);

            IBattleGrid grid = concrete;

            Assert.AreEqual(3, grid.Width);
            Assert.AreEqual(2, grid.Height);
            Assert.AreEqual(TerrainType.EncampmentTent, grid.GetTerrain(new GridCoord(0, 0)));
            Assert.IsTrue(grid.IsDeployable(new GridCoord(0, 0)));
            Assert.IsFalse(grid.IsWalkable(new GridCoord(2, 1)));
            Assert.IsTrue(grid.IsWalkable(new GridCoord(1, 1)));
        }
    }
}
