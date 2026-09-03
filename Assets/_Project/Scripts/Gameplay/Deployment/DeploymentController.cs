using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BinakayanRising.Core.Grid;
using BinakayanRising.Data;

namespace BinakayanRising.Gameplay.Deployment
{
    /// <summary>
    /// One unit and the cell it was deployed onto: the atom of the formation the deployment phase
    /// hands to the combat phase.
    /// </summary>
    public readonly struct UnitPlacement
    {
        /// <summary>The deployed unit definition.</summary>
        public UnitData Unit { get; }

        /// <summary>The grid cell it starts combat on.</summary>
        public GridCoord Cell { get; }

        /// <summary>Creates a placement.</summary>
        /// <param name="unit">The deployed unit definition.</param>
        /// <param name="cell">The grid cell it starts combat on.</param>
        public UnitPlacement(UnitData unit, GridCoord cell)
        {
            Unit = unit;
            Cell = cell;
        }

        /// <summary>Returns the placement in <c>Unit @ (x, y)</c> form.</summary>
        public override string ToString()
        {
            return (Unit != null ? Unit.name : "<null>") + " @ " + Cell;
        }
    }

    /// <summary>
    /// Every reason a drop can be refused. This enum is the "Validate Grid Tile Availability" use
    /// case in machine-readable form: one member per rule, so the UI can explain the refusal
    /// instead of just flashing red.
    /// </summary>
    public enum PlacementValidation
    {
        /// <summary>The drop is legal and <see cref="DeploymentController.TryPlace"/> will accept it.</summary>
        Valid = 0,

        /// <summary>No unit was supplied.</summary>
        NullUnit = 1,

        /// <summary>The controller has no grid yet — the mission has not been loaded.</summary>
        NotInitialized = 2,

        /// <summary>The formation is already locked; the document forbids further edits.</summary>
        FormationLocked = 3,

        /// <summary>The cell is off the map.</summary>
        OutOfBounds = 4,

        /// <summary>The cell is on the map but is not part of the mission's deployment zone.</summary>
        NotDeployable = 5,

        /// <summary>Another unit already stands on the cell.</summary>
        CellOccupied = 6,

        /// <summary>The squad size cap is reached and this unit is not already on the board.</summary>
        SquadFull = 7,

        /// <summary>
        /// The squad size cap is still at its unconfigured 0 default, so no placement can be judged.
        /// TODO(design): the cap is not specified in the capstone document.
        /// </summary>
        SquadCapUnconfigured = 8
    }

    /// <summary>
    /// Owns the deployment phase: the map from <see cref="UnitData"/> to <see cref="GridCoord"/>,
    /// the rules that decide whether a drop is legal, and the "lock formation and start" action that
    /// ends the phase and hands the formation to combat.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The capstone document describes the phase as: the player drags hero portraits from a roster
    /// strip onto highlighted blue valid grid tiles on the isometric grid; tile validity is its own
    /// use case; and "once the player confirms, the formation is locked" and all player input is
    /// locked for the whole combat phase. This class is the rules half of that. Input locking itself
    /// belongs to <c>CombatPhaseController</c>, which listens for <see cref="FormationLocked"/>.
    /// </para>
    /// <para>
    /// <b>Two things the document does not specify</b>, both marked TODO(design) below:
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     <b>The squad size cap.</b> The document never says how many heroes may be deployed at
    ///     once. <see cref="maxSquadSize"/> therefore ships at 0, which is treated as "unconfigured"
    ///     and refuses every placement with <see cref="PlacementValidation.SquadCapUnconfigured"/>,
    ///     rather than silently inventing a number.
    ///   </description></item>
    ///   <item><description>
    ///     <b>The deployment zone shape.</b> The document never says which cells are legal — not a
    ///     back row, not a rectangle, not a count. The zone is read cell by cell from the mission
    ///     asset through <see cref="IBattleGrid.IsDeployable"/>, so the shape stays an authoring
    ///     decision and no geometry is hardcoded here.
    ///   </description></item>
    /// </list>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DeploymentController : MonoBehaviour
    {
        [Header("Mission")]
        [Tooltip("Mission being deployed into. May instead be supplied at runtime through Initialize().")]
        [SerializeField] private MissionData mission;

        [Tooltip("Units the player may deploy on this mission, in roster-strip order. " +
                 "May instead be supplied at runtime through Initialize().")]
        [SerializeField] private List<UnitData> availableRoster = new List<UnitData>();

        [Header("Rules")]
        // TODO(design): not specified in capstone document. The document describes drag-and-drop
        // deployment but never publishes a squad size cap. 0 means "unconfigured" and refuses every
        // placement, mirroring how MissionData ships an unconfigured 0x0 grid rather than guessing.
        [Tooltip("Maximum number of units the player may deploy. " +
                 "TODO(design): not specified in capstone document; 0 means unconfigured.")]
        [Min(0)]
        [SerializeField] private int maxSquadSize = 0;

        [Tooltip("At least this many units must be placed before the formation can be locked. " +
                 "The document requires the confirm action to need at least one placed unit.")]
        [Min(1)]
        [SerializeField] private int minimumUnitsToLock = 1;

        [Header("Scene References")]
        [Tooltip("Draws the blue deployment-zone highlight.")]
        [SerializeField] private DeploymentZoneView zoneView;

        [Tooltip("The bottom-of-screen hero portrait strip.")]
        [SerializeField] private RosterStripView rosterStrip;

        [Tooltip("Converts pointer positions into grid cells.")]
        [SerializeField] private GridRaycaster raycaster;

        [Tooltip("Optional 'Lock Formation & Start' button. Kept non-interactable until a unit is placed.")]
        [SerializeField] private Button lockFormationButton;

        [Header("Isometric Projection")]
        // TODO(design): not specified in capstone document - no tile size is published. This is the
        // single source of truth: it is pushed into the zone view and the raycaster on Initialize.
        [Tooltip("Full width of one tile diamond in world units. TODO(design): unspecified in the document.")]
        [Min(0.0001f)]
        [SerializeField] private float tileWidth = IsoGridLayout.DefaultTileWidth;

        [Tooltip("Full height of one tile diamond in world units. TODO(design): unspecified in the document.")]
        [Min(0.0001f)]
        [SerializeField] private float tileHeight = IsoGridLayout.DefaultTileHeight;

        [Tooltip("World position of grid cell (0, 0).")]
        [SerializeField] private Vector2 boardOrigin = Vector2.zero;

        [Header("Fallback")]
        [Tooltip("When no grid is injected, build a plain BattleGrid from the mission asset so the " +
                 "phase can run without the battle adapter. Clear this to require an injected grid.")]
        [SerializeField] private bool buildGridFromMissionWhenNotInjected = true;

        private readonly Dictionary<UnitData, GridCoord> placements = new Dictionary<UnitData, GridCoord>();
        private readonly Dictionary<GridCoord, UnitData> occupants = new Dictionary<GridCoord, UnitData>();
        private readonly List<UnitPlacement> placementBuffer = new List<UnitPlacement>();

        private IBattleGrid grid;
        private IsoGridLayout layout;
        private bool formationLocked;

        /// <summary>Raised whenever a unit is placed, moved, removed, or all placements are cleared.</summary>
        public event Action PlacementsChanged;

        /// <summary>
        /// Raised when a drop is refused, carrying the unit, the cell and the exact rule that
        /// refused it, so the UI can give the player a reason rather than a generic rejection.
        /// </summary>
        public event Action<UnitData, GridCoord, PlacementValidation> PlacementRejected;

        /// <summary>
        /// Raised once, when the player confirms and the formation is locked. The payload is the
        /// final placement list that the combat phase runs the battle from.
        /// </summary>
        public event Action<IReadOnlyList<UnitPlacement>> FormationLocked;

        /// <summary>The mission being deployed into, or null before initialization.</summary>
        public MissionData Mission
        {
            get { return mission; }
        }

        /// <summary>The battle grid the deployment zone is read from, or null before initialization.</summary>
        public IBattleGrid Grid
        {
            get { return grid; }
        }

        /// <summary>The isometric projection this phase draws and picks with. Never null.</summary>
        public IsoGridLayout Layout
        {
            get
            {
                if (layout == null)
                {
                    layout = new IsoGridLayout(tileWidth, tileHeight);
                }

                return layout;
            }
        }

        /// <summary>World position of grid cell <c>(0, 0)</c>.</summary>
        public Vector2 BoardOrigin
        {
            get { return boardOrigin; }
        }

        /// <summary>True once a grid has been bound and the phase can accept placements.</summary>
        public bool IsInitialized
        {
            get { return grid != null; }
        }

        /// <summary>
        /// Maximum number of units the player may deploy.
        /// TODO(design): not specified in capstone document; 0 means unconfigured.
        /// </summary>
        public int MaxSquadSize
        {
            get { return maxSquadSize; }
        }

        /// <summary>How many units are currently on the board.</summary>
        public int PlacedCount
        {
            get { return placements.Count; }
        }

        /// <summary>The current placements, keyed by unit. Read-only; mutate through <see cref="TryPlace"/>.</summary>
        public IReadOnlyDictionary<UnitData, GridCoord> Placements
        {
            get { return placements; }
        }

        /// <summary>The units the player may deploy this mission, in roster-strip order.</summary>
        public IReadOnlyList<UnitData> AvailableRoster
        {
            get { return availableRoster; }
        }

        /// <summary>
        /// True once the player has confirmed. The document says the formation is locked at that
        /// point, so every mutating call is refused from here on.
        /// </summary>
        public bool IsFormationLocked
        {
            get { return formationLocked; }
        }

        /// <summary>
        /// True when the "lock formation and start" action may be taken: the phase is initialized,
        /// not already locked, and at least <see cref="minimumUnitsToLock"/> unit is placed.
        /// </summary>
        public bool CanLockFormation
        {
            get { return IsInitialized && !formationLocked && placements.Count >= minimumUnitsToLock; }
        }

        /// <summary>
        /// Loads a mission into the phase: binds the grid, pushes the shared projection into the
        /// view and the raycaster, draws the blue zone, and fills the roster strip.
        /// </summary>
        /// <param name="missionData">Mission to deploy into. Must not be null.</param>
        /// <param name="battleGrid">
        /// The grid combat will run on. Pass the one the battle adapter built, so deployment and
        /// combat cannot disagree about the map. Null falls back to building a plain
        /// <see cref="BattleGrid"/> from <paramref name="missionData"/> when
        /// <see cref="buildGridFromMissionWhenNotInjected"/> is set.
        /// </param>
        /// <param name="roster">
        /// Units the player may deploy. Null keeps the roster authored in the Inspector.
        /// </param>
        public void Initialize(MissionData missionData, IBattleGrid battleGrid, IReadOnlyList<UnitData> roster = null)
        {
            mission = missionData;
            formationLocked = false;
            placements.Clear();
            occupants.Clear();

            grid = battleGrid;

            if (grid == null && buildGridFromMissionWhenNotInjected)
            {
                grid = BuildGridFromMission(missionData);
            }

            layout = new IsoGridLayout(tileWidth, tileHeight);

            if (roster != null)
            {
                availableRoster.Clear();

                for (int i = 0; i < roster.Count; i++)
                {
                    availableRoster.Add(roster[i]);
                }
            }

            if (raycaster != null)
            {
                raycaster.SetLayout(layout, boardOrigin);
                raycaster.SetGrid(grid);
            }

            if (zoneView != null)
            {
                zoneView.SetLayout(layout, boardOrigin);
                zoneView.ShowDeploymentZone(grid);
            }

            if (rosterStrip != null)
            {
                rosterStrip.SetRoster(availableRoster);
            }

            if (grid == null)
            {
                Debug.LogError(
                    "[DeploymentController] No battle grid. Pass one from the battle adapter, or " +
                    "author GridWidth/GridHeight on the mission asset.",
                    this);
            }
            else if (CountDeployableCells() == 0)
            {
                Debug.LogWarning(
                    "[DeploymentController] The mission's deployment zone is empty, so no cell is a " +
                    "legal drop target. TODO(design): deployment zone shape is not specified in the " +
                    "capstone document and must be authored on the MissionData asset.",
                    this);
            }

            RefreshUi();
            RaisePlacementsChanged();
        }

        /// <summary>
        /// The "Validate Grid Tile Availability" use case: decides whether one unit may be dropped
        /// on one cell, and says exactly why not when it may not.
        /// </summary>
        /// <param name="unit">The unit being dropped.</param>
        /// <param name="cell">The cell it is being dropped on.</param>
        /// <returns><see cref="PlacementValidation.Valid"/>, or the first rule that refused.</returns>
        public PlacementValidation Validate(UnitData unit, GridCoord cell)
        {
            if (unit == null)
            {
                return PlacementValidation.NullUnit;
            }

            if (!IsInitialized)
            {
                return PlacementValidation.NotInitialized;
            }

            if (formationLocked)
            {
                return PlacementValidation.FormationLocked;
            }

            if (!grid.InBounds(cell))
            {
                return PlacementValidation.OutOfBounds;
            }

            if (!grid.IsDeployable(cell))
            {
                return PlacementValidation.NotDeployable;
            }

            UnitData occupant;

            if (occupants.TryGetValue(cell, out occupant) && occupant != unit)
            {
                return PlacementValidation.CellOccupied;
            }

            // A unit already on the board is being moved, not added, so the cap does not apply to it.
            bool isRelocation = placements.ContainsKey(unit);

            if (!isRelocation)
            {
                if (maxSquadSize <= 0)
                {
                    return PlacementValidation.SquadCapUnconfigured;
                }

                if (placements.Count >= maxSquadSize)
                {
                    return PlacementValidation.SquadFull;
                }
            }

            return PlacementValidation.Valid;
        }

        /// <summary>
        /// True when the cell is a highlighted blue tile that is free right now — the question the
        /// drag ghost asks every frame.
        /// </summary>
        /// <param name="cell">Cell to test.</param>
        public bool IsCellAvailable(GridCoord cell)
        {
            return IsInitialized
                && grid.IsDeployable(cell)
                && !occupants.ContainsKey(cell);
        }

        /// <summary>True when the cell belongs to the mission's deployment zone, occupied or not.</summary>
        /// <param name="cell">Cell to test.</param>
        public bool IsCellDeployable(GridCoord cell)
        {
            return IsInitialized && grid.IsDeployable(cell);
        }

        /// <summary>
        /// Places a unit on a cell, or moves it there when it is already deployed. Refused drops
        /// raise <see cref="PlacementRejected"/> and change nothing.
        /// </summary>
        /// <param name="unit">Unit to deploy.</param>
        /// <param name="cell">Target cell.</param>
        /// <returns>True when the placement was accepted.</returns>
        public bool TryPlace(UnitData unit, GridCoord cell)
        {
            PlacementValidation validation = Validate(unit, cell);

            if (validation != PlacementValidation.Valid)
            {
                RaisePlacementRejected(unit, cell, validation);
                return false;
            }

            GridCoord previous;

            if (placements.TryGetValue(unit, out previous))
            {
                if (previous == cell)
                {
                    return true;
                }

                occupants.Remove(previous);

                if (zoneView != null)
                {
                    zoneView.SetOccupied(previous, false);
                }
            }

            placements[unit] = cell;
            occupants[cell] = unit;

            if (zoneView != null)
            {
                zoneView.SetOccupied(cell, true);
            }

            RefreshUi();
            RaisePlacementsChanged();
            return true;
        }

        /// <summary>Takes a unit off the board and returns it to the roster strip.</summary>
        /// <param name="unit">Unit to remove.</param>
        /// <returns>True when the unit was deployed and has now been removed.</returns>
        public bool RemoveUnit(UnitData unit)
        {
            if (unit == null || formationLocked)
            {
                return false;
            }

            GridCoord cell;

            if (!placements.TryGetValue(unit, out cell))
            {
                return false;
            }

            placements.Remove(unit);
            occupants.Remove(cell);

            if (zoneView != null)
            {
                zoneView.SetOccupied(cell, false);
            }

            RefreshUi();
            RaisePlacementsChanged();
            return true;
        }

        /// <summary>Takes whatever unit stands on a cell off the board.</summary>
        /// <param name="cell">Cell to clear.</param>
        /// <returns>True when a unit stood there and has now been removed.</returns>
        public bool RemoveAt(GridCoord cell)
        {
            UnitData occupant;
            return occupants.TryGetValue(cell, out occupant) && RemoveUnit(occupant);
        }

        /// <summary>Clears every placement, returning the whole squad to the roster strip.</summary>
        public void ClearPlacements()
        {
            if (formationLocked || placements.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<GridCoord, UnitData> pair in occupants)
            {
                if (zoneView != null)
                {
                    zoneView.SetOccupied(pair.Key, false);
                }
            }

            placements.Clear();
            occupants.Clear();

            RefreshUi();
            RaisePlacementsChanged();
        }

        /// <summary>True when the unit is currently on the board.</summary>
        /// <param name="unit">Unit to query.</param>
        public bool IsPlaced(UnitData unit)
        {
            return unit != null && placements.ContainsKey(unit);
        }

        /// <summary>Finds where a unit was deployed.</summary>
        /// <param name="unit">Unit to query.</param>
        /// <param name="cell">The cell it stands on when this returns true.</param>
        public bool TryGetPlacement(UnitData unit, out GridCoord cell)
        {
            if (unit != null)
            {
                return placements.TryGetValue(unit, out cell);
            }

            cell = GridCoord.Zero;
            return false;
        }

        /// <summary>Finds which unit stands on a cell.</summary>
        /// <param name="cell">Cell to query.</param>
        /// <param name="unit">The unit standing there when this returns true.</param>
        public bool TryGetUnitAt(GridCoord cell, out UnitData unit)
        {
            return occupants.TryGetValue(cell, out unit);
        }

        /// <summary>
        /// Copies the current formation into a fresh list, in a stable order: roster order first,
        /// then any placed unit not in the roster. Stability matters because the combat simulator
        /// assigns unit ids from this order and its determinism keys off those ids.
        /// </summary>
        public IReadOnlyList<UnitPlacement> GetPlacements()
        {
            placementBuffer.Clear();

            for (int i = 0; i < availableRoster.Count; i++)
            {
                UnitData unit = availableRoster[i];
                GridCoord cell;

                if (unit != null && placements.TryGetValue(unit, out cell))
                {
                    placementBuffer.Add(new UnitPlacement(unit, cell));
                }
            }

            if (placementBuffer.Count != placements.Count)
            {
                foreach (KeyValuePair<UnitData, GridCoord> pair in placements)
                {
                    if (availableRoster.Contains(pair.Key))
                    {
                        continue;
                    }

                    placementBuffer.Add(new UnitPlacement(pair.Key, pair.Value));
                }
            }

            return placementBuffer;
        }

        /// <summary>
        /// The document's "Lock Formation &amp; Start" action. Freezes the formation, raises
        /// <see cref="FormationLocked"/> with the final placement list, and refuses every later edit.
        /// </summary>
        /// <returns>False when <see cref="CanLockFormation"/> is false; nothing is changed then.</returns>
        public bool LockFormation()
        {
            if (!CanLockFormation)
            {
                Debug.LogWarning(
                    "[DeploymentController] Lock Formation refused: " +
                    (IsInitialized ? string.Empty : "no mission loaded; ") +
                    (formationLocked ? "the formation is already locked; " : string.Empty) +
                    "placed " + placements.Count + " of the " + minimumUnitsToLock + " unit(s) required.",
                    this);
                return false;
            }

            formationLocked = true;
            IReadOnlyList<UnitPlacement> finalPlacements = GetPlacements();

            RefreshUi();

            Action<IReadOnlyList<UnitPlacement>> handler = FormationLocked;

            if (handler != null)
            {
                handler(finalPlacements);
            }

            return true;
        }

        /// <summary>
        /// Reopens the phase after a locked formation was abandoned — the Figure 2
        /// "Defeat (Restart)" path back to the Encampment and in again. Placements are kept.
        /// </summary>
        public void UnlockFormation()
        {
            formationLocked = false;
            RefreshUi();
        }

        /// <summary>Repaints the zone highlight and the roster strip from the current placements.</summary>
        public void RefreshUi()
        {
            if (zoneView != null && zoneView.IsShowing)
            {
                for (int i = 0; i < zoneView.ZoneCells.Count; i++)
                {
                    GridCoord cell = zoneView.ZoneCells[i];
                    zoneView.SetOccupied(cell, occupants.ContainsKey(cell));
                }
            }

            if (rosterStrip != null)
            {
                rosterStrip.RefreshFrom(this);
            }

            if (lockFormationButton != null)
            {
                lockFormationButton.interactable = CanLockFormation;
            }
        }

        private void Awake()
        {
            if (lockFormationButton != null)
            {
                lockFormationButton.onClick.AddListener(OnLockFormationButtonClicked);
                lockFormationButton.interactable = false;
            }
        }

        private void OnDestroy()
        {
            if (lockFormationButton != null)
            {
                lockFormationButton.onClick.RemoveListener(OnLockFormationButtonClicked);
            }
        }

        private void OnLockFormationButtonClicked()
        {
            LockFormation();
        }

        private int CountDeployableCells()
        {
            if (grid == null)
            {
                return 0;
            }

            int count = 0;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (grid.IsDeployable(new GridCoord(x, y)))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Standalone fallback: builds a plain <see cref="BattleGrid"/> straight from the mission
        /// asset, so the deployment phase can be exercised without the battle adapter. The adapter's
        /// grid, when one is injected, always wins.
        /// </summary>
        private static IBattleGrid BuildGridFromMission(MissionData missionData)
        {
            if (missionData == null || !missionData.HasConfiguredGrid)
            {
                return null;
            }

            BattleGrid built = new BattleGrid(missionData.GridWidth, missionData.GridHeight);

            for (int i = 0; i < missionData.DeploymentZoneCount; i++)
            {
                GridCoord cell = missionData.GetDeploymentCell(i);

                if (built.InBounds(cell))
                {
                    built.SetDeployable(cell, true);
                }
            }

            return built;
        }

        private void RaisePlacementsChanged()
        {
            Action handler = PlacementsChanged;

            if (handler != null)
            {
                handler();
            }
        }

        private void RaisePlacementRejected(UnitData unit, GridCoord cell, PlacementValidation reason)
        {
            Action<UnitData, GridCoord, PlacementValidation> handler = PlacementRejected;

            if (handler != null)
            {
                handler(unit, cell, reason);
            }
        }
    }
}
