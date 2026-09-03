using System;
using System.Collections.Generic;
using BinakayanRising.Core.Grid;
using BinakayanRising.Data;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BinakayanRising.Gameplay.Adapters
{
    /// <summary>
    /// One row of the Inspector table that maps a painted <see cref="TileBase"/> to the
    /// <see cref="TerrainType"/> the simulation reasons about.
    /// </summary>
    /// <remarks>
    /// The same list is read in both directions: <see cref="MissionGridBuilder"/> reads a painted
    /// Tilemap into a <see cref="BattleGrid"/>, and the presentation layer's terrain renderer paints
    /// a <see cref="BattleGrid"/> back onto a Tilemap. Keeping one asset-side table means the two can
    /// never disagree about what a tile means.
    /// </remarks>
    [Serializable]
    public sealed class TerrainTileBinding
    {
        [Tooltip("Tile asset painted on the terrain Tilemap.")]
        [SerializeField] private TileBase tile;

        [Tooltip("Simulation terrain type this tile stands for.")]
        [SerializeField] private TerrainType terrain = TerrainType.StandardGrid;

        /// <summary>Creates an empty binding, for the Inspector.</summary>
        public TerrainTileBinding()
        {
        }

        /// <summary>Creates a binding in code, for tests and editor tooling.</summary>
        /// <param name="tile">Tile asset.</param>
        /// <param name="terrain">Terrain type the tile stands for.</param>
        public TerrainTileBinding(TileBase tile, TerrainType terrain)
        {
            this.tile = tile;
            this.terrain = terrain;
        }

        /// <summary>Tile asset painted on the terrain Tilemap.</summary>
        public TileBase Tile
        {
            get { return tile; }
        }

        /// <summary>Simulation terrain type this tile stands for.</summary>
        public TerrainType Terrain
        {
            get { return terrain; }
        }
    }

    /// <summary>
    /// Builds the Core <see cref="BattleGrid"/> a battle runs on from a <see cref="MissionData"/>
    /// asset, either from the asset's own fields or by reading terrain off a painted Unity Tilemap.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An unconfigured mission fails loudly.</b> <see cref="MissionData.GridWidth"/> and
    /// <see cref="MissionData.GridHeight"/> default to 0 precisely so that a half-authored asset
    /// cannot quietly resolve to an invented board size, and this builder honours that: it throws
    /// with the mission's id in the message rather than substituting a default. Use
    /// <see cref="TryBuild(MissionData, out BattleGrid, out string)"/> when a caller wants to report
    /// the problem in UI instead of taking an exception.
    /// </para>
    /// <para>
    /// TODO(design): not specified in capstone document. <see cref="MissionData"/> carries no
    /// per-cell terrain, so the mission-only path produces a board of
    /// <see cref="TerrainType.StandardGrid"/> with the authored deployment zone marked. Terrain must
    /// come from a painted Tilemap until design adds a terrain layer to the mission asset.
    /// </para>
    /// </remarks>
    public static class MissionGridBuilder
    {
        /// <summary>
        /// Builds a grid from the mission's own fields: its dimensions and its deployment zone.
        /// Every cell is <see cref="TerrainType.StandardGrid"/>.
        /// </summary>
        /// <param name="mission">Mission asset. Must not be null and must have a configured grid.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mission"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the mission's grid is still at its unconfigured 0x0 default.
        /// </exception>
        public static BattleGrid Build(MissionData mission)
        {
            RequireConfiguredGrid(mission);

            BattleGrid grid = new BattleGrid(
                mission.GridWidth, mission.GridHeight, TerrainType.StandardGrid);

            ApplyDeploymentZone(grid, mission);
            return grid;
        }

        /// <summary>
        /// Builds a grid from the mission's dimensions, filling terrain by reading tiles off a
        /// painted Tilemap.
        /// </summary>
        /// <param name="mission">Mission asset. Must not be null and must have a configured grid.</param>
        /// <param name="terrainTilemap">Tilemap holding the painted terrain. Must not be null.</param>
        /// <param name="bindings">
        /// Tile-to-terrain table. Must not be null or empty. A tile absent from the table is treated
        /// as <paramref name="fallback"/>.
        /// </param>
        /// <param name="origin">
        /// Tilemap cell that corresponds to simulation cell <c>(0, 0)</c>. Lets the artist paint
        /// anywhere on the Tilemap without the simulation caring.
        /// </param>
        /// <param name="fallback">
        /// Terrain used for an empty cell or an unrecognised tile. Defaults to
        /// <see cref="TerrainType.StandardGrid"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when any required argument is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="bindings"/> is empty.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the mission's grid is still at its unconfigured 0x0 default.
        /// </exception>
        public static BattleGrid BuildFromTilemap(
            MissionData mission,
            Tilemap terrainTilemap,
            IReadOnlyList<TerrainTileBinding> bindings,
            Vector3Int origin = default,
            TerrainType fallback = TerrainType.StandardGrid)
        {
            RequireConfiguredGrid(mission);

            if (terrainTilemap == null)
            {
                throw new ArgumentNullException(nameof(terrainTilemap));
            }

            Dictionary<TileBase, TerrainType> lookup = BuildTileLookup(bindings);

            BattleGrid grid = new BattleGrid(mission.GridWidth, mission.GridHeight, fallback);

            for (int y = 0; y < mission.GridHeight; y++)
            {
                for (int x = 0; x < mission.GridWidth; x++)
                {
                    Vector3Int tilemapCell = new Vector3Int(origin.x + x, origin.y + y, origin.z);
                    TileBase tile = terrainTilemap.GetTile(tilemapCell);

                    TerrainType terrain;
                    if (tile == null || !lookup.TryGetValue(tile, out terrain))
                    {
                        terrain = fallback;
                    }

                    grid.SetTerrain(new GridCoord(x, y), terrain);
                }
            }

            ApplyDeploymentZone(grid, mission);
            return grid;
        }

        /// <summary>
        /// Non-throwing form of <see cref="Build(MissionData)"/> for UI that would rather show the
        /// authoring mistake than crash.
        /// </summary>
        /// <param name="mission">Mission asset.</param>
        /// <param name="grid">Receives the grid, or null on failure.</param>
        /// <param name="error">Receives a human-readable reason, or null on success.</param>
        /// <returns>True when a grid was built.</returns>
        public static bool TryBuild(MissionData mission, out BattleGrid grid, out string error)
        {
            grid = null;
            error = DescribeGridProblem(mission);

            if (error != null)
            {
                return false;
            }

            grid = Build(mission);
            return true;
        }

        /// <summary>
        /// Returns a human-readable description of why a mission cannot produce a grid, or null when
        /// it can.
        /// </summary>
        /// <param name="mission">Mission asset, possibly null.</param>
        public static string DescribeGridProblem(MissionData mission)
        {
            if (mission == null)
            {
                return "No MissionData asset was supplied.";
            }

            if (!mission.HasConfiguredGrid)
            {
                return "MissionData '" + DescribeMission(mission) + "' has an unconfigured "
                    + mission.GridWidth + "x" + mission.GridHeight + " grid. The 0x0 default is "
                    + "deliberate: the capstone document never specifies board dimensions, so an "
                    + "unauthored mission must fail rather than load an invented board size. Set "
                    + "Grid Width and Grid Height on the asset.";
            }

            if (mission.DeploymentZoneCount == 0)
            {
                // Not fatal: a mission may legitimately script every placement. Reported by the
                // deployment layer, not here, so a cutscene battle can still build its grid.
                return null;
            }

            return null;
        }

        /// <summary>
        /// Marks the mission's authored deployment cells on an already-built grid, skipping any that
        /// fall outside it.
        /// </summary>
        /// <param name="grid">Grid to mark. Must not be null.</param>
        /// <param name="mission">Mission supplying the zone. Must not be null.</param>
        /// <returns>How many cells were marked.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
        public static int ApplyDeploymentZone(BattleGrid grid, MissionData mission)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            if (mission == null)
            {
                throw new ArgumentNullException(nameof(mission));
            }

            int marked = 0;

            for (int i = 0; i < mission.DeploymentZoneCount; i++)
            {
                GridCoord cell = mission.GetDeploymentCell(i);

                if (!grid.InBounds(cell))
                {
                    Debug.LogWarning(
                        "MissionData '" + DescribeMission(mission) + "' lists deployment cell "
                            + cell + ", which is outside its own " + grid.Width + "x" + grid.Height
                            + " grid. Cell ignored.");
                    continue;
                }

                grid.SetDeployable(cell, true);
                marked++;
            }

            return marked;
        }

        /// <summary>
        /// Builds the tile-to-terrain lookup, warning about duplicate and unassigned rows.
        /// </summary>
        /// <param name="bindings">Inspector-authored table. Must not be null or empty.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bindings"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when no usable row is present.</exception>
        public static Dictionary<TileBase, TerrainType> BuildTileLookup(
            IReadOnlyList<TerrainTileBinding> bindings)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }

            Dictionary<TileBase, TerrainType> lookup = new Dictionary<TileBase, TerrainType>();

            for (int i = 0; i < bindings.Count; i++)
            {
                TerrainTileBinding binding = bindings[i];

                if (binding == null || binding.Tile == null)
                {
                    Debug.LogWarning(
                        "Terrain tile binding at index " + i + " has no tile assigned. Row ignored.");
                    continue;
                }

                if (lookup.ContainsKey(binding.Tile))
                {
                    Debug.LogWarning(
                        "Tile '" + binding.Tile.name + "' is bound twice in the terrain table; the "
                            + "first binding (" + lookup[binding.Tile] + ") wins.");
                    continue;
                }

                lookup[binding.Tile] = binding.Terrain;
            }

            if (lookup.Count == 0)
            {
                throw new ArgumentException(
                    "The terrain tile table has no usable rows, so every painted cell would read as "
                        + "the fallback terrain. Assign one tile per TerrainType in the Inspector.",
                    nameof(bindings));
            }

            return lookup;
        }

        private static void RequireConfiguredGrid(MissionData mission)
        {
            if (mission == null)
            {
                throw new ArgumentNullException(nameof(mission));
            }

            string problem = DescribeGridProblem(mission);

            if (problem != null)
            {
                throw new InvalidOperationException(problem);
            }
        }

        private static string DescribeMission(MissionData mission)
        {
            if (!string.IsNullOrEmpty(mission.MissionId))
            {
                return mission.MissionId;
            }

            return string.IsNullOrEmpty(mission.name) ? "<unnamed>" : mission.name;
        }
    }
}
