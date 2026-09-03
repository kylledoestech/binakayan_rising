using System;
using System.Collections.Generic;
using UnityEngine;
using GridCoord = BinakayanRising.Core.Grid.GridCoord;

namespace BinakayanRising.Data
{
    /// <summary>
    /// An Inspector-authorable grid cell. Mirrors
    /// <see cref="BinakayanRising.Core.Grid.GridCoord"/> field for field.
    /// </summary>
    /// <remarks>
    /// <c>GridCoord</c> itself cannot be serialized by Unity: it is a readonly struct exposing
    /// auto-properties rather than serializable fields, and it lives in the engine-free
    /// <c>BinakayanRising.Core</c> assembly, which must stay free of <c>[SerializeField]</c> and
    /// any other UnityEngine dependency. This wrapper is the serialization boundary — assets store
    /// it, and every public accessor on <see cref="MissionData"/> converts to <c>GridCoord</c>
    /// before handing data to the simulation.
    /// </remarks>
    [Serializable]
    public struct SerializableGridCoord
    {
        [Tooltip("Column index of the cell.")]
        [SerializeField] private int x;

        [Tooltip("Row index of the cell.")]
        [SerializeField] private int y;

        /// <summary>Creates a coordinate at the given column and row.</summary>
        /// <param name="x">Column index.</param>
        /// <param name="y">Row index.</param>
        public SerializableGridCoord(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        /// <summary>Column index of the cell.</summary>
        public int X => x;

        /// <summary>Row index of the cell.</summary>
        public int Y => y;

        /// <summary>Converts to the simulation's coordinate type.</summary>
        public GridCoord ToGridCoord()
        {
            return new GridCoord(x, y);
        }

        /// <summary>Creates a serializable copy of a simulation coordinate.</summary>
        /// <param name="coord">Coordinate to copy.</param>
        public static SerializableGridCoord FromGridCoord(GridCoord coord)
        {
            return new SerializableGridCoord(coord.X, coord.Y);
        }

        /// <summary>Returns the coordinate in <c>(x, y)</c> form.</summary>
        public override string ToString()
        {
            return "(" + x + ", " + y + ")";
        }
    }

    /// <summary>
    /// One enemy placement in a mission: which unit spawns, and on which cell.
    /// </summary>
    [Serializable]
    public sealed class EnemySpawnEntry
    {
        [Tooltip("Unit definition to instantiate for this enemy.")]
        [SerializeField] private UnitData unit;

        [Tooltip("Cell the enemy occupies at the start of combat.")]
        [SerializeField] private SerializableGridCoord coord;

        /// <summary>Unit definition to instantiate for this enemy.</summary>
        public UnitData Unit => unit;

        /// <summary>Cell the enemy occupies at the start of combat.</summary>
        public GridCoord Coord => coord.ToGridCoord();
    }

    /// <summary>
    /// One sub-quest of the linear historical campaign: its narrative framing, its battle grid, the
    /// cells the player may deploy onto, the enemy formation, and what it costs to enter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The campaign is linear and consists of three levels covering ten sub-quests. Author one
    /// asset per sub-quest, in this order:
    /// </para>
    /// <list type="table">
    ///   <listheader>
    ///     <term>Level</term>
    ///     <description>Sub-quests</description>
    ///   </listheader>
    ///   <item>
    ///     <term>Level 1 — "The Architect's Awakening"</term>
    ///     <description>
    ///       The Scholar of Ghent [UI Tutorial]; The Reality Check [Combat Tutorial];
    ///       The Vanguard's Rally [Base Management]; Scavenging for the Cause [Resource Gathering].
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Level 2 — "Preparations for War"</term>
    ///     <description>
    ///       The Intercepted Armada [Synergy Drill]; Forging the Earthworks [Defense/Escort];
    ///       The Silent Sabotage [Stealth/Intelligence].
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Level 3 — "Battle of Binakayan-Dalahican"</term>
    ///     <description>
    ///       The First Wave [Nov 9]; The War of Attrition [Nov 10]; The Decisive Dawn [Nov 11].
    ///     </description>
    ///   </item>
    /// </list>
    /// <para>
    /// TODO(design): grid dimensions are not specified anywhere in the capstone document. It
    /// describes an isometric deployment grid and drag-and-drop placement but never gives a width,
    /// a height, or an aspect. <see cref="GridWidth"/> and <see cref="GridHeight"/> therefore
    /// default to 0 so that an unconfigured mission fails loudly at load instead of quietly
    /// resolving to some invented board size.
    /// </para>
    /// <para>
    /// TODO(design): deployment zones are not specified either. The document says the player drags
    /// hero portraits onto the grid, but never which cells are legal. The deployment zone list
    /// ships empty, which means "no legal cell" rather than "anywhere".
    /// </para>
    /// <para>
    /// TODO(design): the Rations cost per stage is unspecified. The document says only that Rations
    /// are "required to deploy units into the Mission Portal" and deplete on entering a stage.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "NewMission", menuName = "Binakayan Rising/Mission", order = 5)]
    public sealed class MissionData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable identifier used by save data and campaign progression. Must be unique.")]
        [SerializeField] private string missionId = string.Empty;

        [Tooltip("Sub-quest title shown in the Mission Portal, e.g. \"The Decisive Dawn\".")]
        [SerializeField] private string displayTitle = string.Empty;

        [Tooltip("Campaign level this sub-quest belongs to: 1, 2 or 3.")]
        [Min(0)]
        [SerializeField] private int levelNumber = 0;

        [Tooltip("Visual-novel narrative blurb shown before deployment.")]
        [TextArea(3, 10)]
        [SerializeField] private string narrativeBlurb = string.Empty;

        [Header("Battle Grid")]
        // TODO(design): not specified in capstone document. Left at 0x0 on purpose so an
        // unconfigured mission asset is rejected at load rather than silently sized by code.
        [Tooltip("Grid width in cells. TODO(design): unspecified in the document; 0 means unconfigured.")]
        [Min(0)]
        [SerializeField] private int gridWidth = 0;

        [Tooltip("Grid height in cells. TODO(design): unspecified in the document; 0 means unconfigured.")]
        [Min(0)]
        [SerializeField] private int gridHeight = 0;

        [Tooltip("Cells the player may deploy onto. " +
                 "TODO(design): deployment zones are unspecified in the document; empty means no legal cell.")]
        [SerializeField] private List<SerializableGridCoord> deploymentZone = new List<SerializableGridCoord>();

        [Header("Enemy Formation")]
        [Tooltip("Enemy units and the cells they start on.")]
        [SerializeField] private List<EnemySpawnEntry> enemySpawns = new List<EnemySpawnEntry>();

        [Header("Entry Cost")]
        [Tooltip("Rations spent to enter this stage. " +
                 "TODO(design): not specified in the capstone document.")]
        [Min(0)]
        [SerializeField] private int rationsCost = 0;

        /// <summary>Stable identifier used by save data and campaign progression.</summary>
        public string MissionId => missionId;

        /// <summary>Sub-quest title shown in the Mission Portal.</summary>
        public string DisplayTitle => displayTitle;

        /// <summary>Campaign level this sub-quest belongs to: 1, 2 or 3.</summary>
        public int LevelNumber => levelNumber;

        /// <summary>Visual-novel narrative blurb shown before deployment.</summary>
        public string NarrativeBlurb => narrativeBlurb;

        /// <summary>
        /// Grid width in cells. TODO(design): unspecified in the capstone document; 0 marks an
        /// unconfigured asset.
        /// </summary>
        public int GridWidth => gridWidth;

        /// <summary>
        /// Grid height in cells. TODO(design): unspecified in the capstone document; 0 marks an
        /// unconfigured asset.
        /// </summary>
        public int GridHeight => gridHeight;

        /// <summary>
        /// Rations spent to enter this stage.
        /// TODO(design): not specified in the capstone document.
        /// </summary>
        public int RationsCost => rationsCost;

        /// <summary>How many cells the player may deploy onto.</summary>
        public int DeploymentZoneCount => deploymentZone.Count;

        /// <summary>Enemy units and the cells they start on, in authoring order.</summary>
        public IReadOnlyList<EnemySpawnEntry> EnemySpawns => enemySpawns;

        /// <summary>
        /// False while the grid is still at its unconfigured 0x0 default, so callers can reject the
        /// asset instead of loading an empty battlefield.
        /// </summary>
        public bool HasConfiguredGrid => gridWidth > 0 && gridHeight > 0;

        /// <summary>Returns one deployable cell by index.</summary>
        /// <param name="index">Zero-based index into the deployment zone.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="index"/> is outside the deployment zone.
        /// </exception>
        public GridCoord GetDeploymentCell(int index)
        {
            if (index < 0 || index >= deploymentZone.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return deploymentZone[index].ToGridCoord();
        }

        /// <summary>
        /// Copies every deployable cell into a fresh array of simulation coordinates.
        /// </summary>
        public GridCoord[] GetDeploymentZone()
        {
            GridCoord[] cells = new GridCoord[deploymentZone.Count];

            for (int i = 0; i < deploymentZone.Count; i++)
            {
                cells[i] = deploymentZone[i].ToGridCoord();
            }

            return cells;
        }

        /// <summary>
        /// Appends every deployable cell to a caller-supplied buffer, avoiding the allocation of
        /// <see cref="GetDeploymentZone()"/>. The buffer is not cleared first.
        /// </summary>
        /// <param name="buffer">Destination list. Must not be <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is null.</exception>
        public void GetDeploymentZone(List<GridCoord> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            for (int i = 0; i < deploymentZone.Count; i++)
            {
                buffer.Add(deploymentZone[i].ToGridCoord());
            }
        }

        /// <summary>True when the cell lies inside the mission's grid bounds.</summary>
        /// <param name="coord">Cell to test.</param>
        public bool IsInsideGrid(GridCoord coord)
        {
            return coord.X >= 0 && coord.X < gridWidth && coord.Y >= 0 && coord.Y < gridHeight;
        }

        /// <summary>True when the player is allowed to deploy a unit onto the cell.</summary>
        /// <param name="coord">Cell to test.</param>
        public bool IsDeployable(GridCoord coord)
        {
            for (int i = 0; i < deploymentZone.Count; i++)
            {
                if (deploymentZone[i].X == coord.X && deploymentZone[i].Y == coord.Y)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
