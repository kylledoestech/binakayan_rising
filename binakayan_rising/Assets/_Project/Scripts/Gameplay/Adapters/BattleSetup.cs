using System;
using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using BinakayanRising.Data;
using BinakayanRising.Gameplay.Deployment;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BinakayanRising.Gameplay.Adapters
{
    /// <summary>
    /// Everything the replayer needs to know about one unit that the event log cannot tell it:
    /// its identity, its sprite, and where it stood before the first turn.
    /// </summary>
    /// <remarks>
    /// The starting cell matters because the simulator mutates
    /// <see cref="CombatUnit.Position"/> in place as the battle runs. By the time
    /// <see cref="BattleResult"/> exists, a unit's deployment cell survives only in the log — and
    /// only if it ever moved. Snapshotting the roster before the first turn is the reliable way to
    /// get it, which is why <see cref="BattleSetupResult.CaptureReplayRoster"/> must be called
    /// before the simulation runs.
    /// </remarks>
    public readonly struct UnitReplayEntry
    {
        private readonly int unitId;
        private readonly string displayName;
        private readonly Team team;
        private readonly Sprite sprite;
        private readonly GridCoord startCell;
        private readonly float maxHP;

        /// <summary>Creates a replay roster entry.</summary>
        /// <param name="unitId">Runtime unit id, as it appears in the event log.</param>
        /// <param name="displayName">Name shown above the sprite and in tooltips.</param>
        /// <param name="team">Side the unit fights for.</param>
        /// <param name="sprite">Battlefield sprite, or null when the asset has none.</param>
        /// <param name="startCell">Cell the unit occupied before the first turn.</param>
        /// <param name="maxHP">Effective Max HP at deployment, used to scale the health bar.</param>
        public UnitReplayEntry(
            int unitId, string displayName, Team team, Sprite sprite, GridCoord startCell, float maxHP)
        {
            this.unitId = unitId;
            this.displayName = displayName ?? string.Empty;
            this.team = team;
            this.sprite = sprite;
            this.startCell = startCell;
            this.maxHP = maxHP;
        }

        /// <summary>Runtime unit id, as it appears in the event log.</summary>
        public int UnitId
        {
            get { return unitId; }
        }

        /// <summary>Name shown above the sprite and in tooltips.</summary>
        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        /// <summary>Side the unit fights for.</summary>
        public Team Team
        {
            get { return team; }
        }

        /// <summary>Battlefield sprite, or null when the asset has none.</summary>
        public Sprite Sprite
        {
            get { return sprite; }
        }

        /// <summary>Cell the unit occupied before the first turn.</summary>
        public GridCoord StartCell
        {
            get { return startCell; }
        }

        /// <summary>Effective Max HP at deployment.</summary>
        public float MaxHP
        {
            get { return maxHP; }
        }
    }

    /// <summary>
    /// A fully wired battle: the simulator plus the Unity-side lookups the presentation layer needs
    /// once the simulation is over.
    /// </summary>
    public sealed class BattleSetupResult
    {
        private readonly BattleSimulator simulator;
        private readonly BattleGrid grid;
        private readonly UnitDataAdapter units;
        private readonly CombatConfig config;

        /// <summary>Creates a result. Built by <see cref="BattleSetup"/>.</summary>
        /// <param name="simulator">The constructed simulator.</param>
        /// <param name="grid">The grid the battle runs on.</param>
        /// <param name="units">Id-to-asset lookup covering every unit in the battle.</param>
        /// <param name="config">The config the simulator was built with.</param>
        public BattleSetupResult(
            BattleSimulator simulator, BattleGrid grid, UnitDataAdapter units, CombatConfig config)
        {
            this.simulator = simulator;
            this.grid = grid;
            this.units = units;
            this.config = config;
        }

        /// <summary>The constructed simulator, ready to run.</summary>
        public BattleSimulator Simulator
        {
            get { return simulator; }
        }

        /// <summary>The grid the battle runs on.</summary>
        public BattleGrid Grid
        {
            get { return grid; }
        }

        /// <summary>Id-to-asset lookup covering every unit in the battle.</summary>
        public UnitDataAdapter Units
        {
            get { return units; }
        }

        /// <summary>The config the simulator was built with.</summary>
        public CombatConfig Config
        {
            get { return config; }
        }

        /// <summary>
        /// Snapshots every unit's identity, sprite, starting cell and Max HP for the replayer.
        /// </summary>
        /// <remarks>
        /// <b>Call this before running the simulation.</b> The simulator mutates unit positions and
        /// HP in place, so a snapshot taken afterwards records the end of the battle, not its
        /// deployment.
        /// </remarks>
        public IReadOnlyList<UnitReplayEntry> CaptureReplayRoster()
        {
            IReadOnlyList<CombatUnit> live = simulator.Units;
            List<UnitReplayEntry> roster = new List<UnitReplayEntry>(live.Count);

            for (int i = 0; i < live.Count; i++)
            {
                CombatUnit unit = live[i];

                roster.Add(new UnitReplayEntry(
                    unit.Id,
                    unit.Name,
                    unit.Team,
                    units.GetSprite(unit.Id),
                    unit.Position,
                    unit.GetEffectiveStat(StatKind.MaxHP)));
            }

            return roster;
        }
    }

    /// <summary>
    /// The single seam between authored Unity data and the pure simulation: takes a mission, a
    /// deployed roster, the terrain and bond assets and a seed, and returns a constructed
    /// <see cref="BattleSimulator"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the Unity-side equivalent of <c>Tools/battle-sim/BattleDemo.cs</c>, which wires the
    /// same pieces by hand for the headless slice. Everything downstream of this call is engine-free
    /// and deterministic: the same seed and the same inputs produce a byte-identical event log, which
    /// is what lets a battle be watched twice and look exactly the same both times.
    /// </para>
    /// <para>
    /// Nothing here calls <c>RunToCompletion</c>. Deciding when to run — and whether to run it in
    /// one frame or step it — belongs to the flow layer. The contract that matters is that the
    /// simulation finishes <em>before</em> the presentation layer starts replaying it.
    /// </para>
    /// </remarks>
    public static class BattleSetup
    {
        /// <summary>
        /// Builds a battle from a mission's own fields, with every cell on standard ground.
        /// </summary>
        /// <param name="mission">Mission asset. Must not be null and must have a configured grid.</param>
        /// <param name="playerRoster">Katipunan units and the cells the player dropped them on.</param>
        /// <param name="terrainAssets">Capstone Table 2 assets, one per terrain type.</param>
        /// <param name="bondRanks">Capstone Table 3 assets with the rank each pair has earned.</param>
        /// <param name="seed">Battle seed. The same seed replays the same battle.</param>
        /// <param name="options">Optional tuning. Defaults are used when null.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the mission's grid is unconfigured or the battle would contain no units.
        /// </exception>
        public static BattleSetupResult Create(
            MissionData mission,
            IReadOnlyList<UnitPlacement> playerRoster,
            IReadOnlyList<TerrainModifierData> terrainAssets,
            IReadOnlyList<KapatiranPairRank> bondRanks,
            int seed,
            BattleSetupOptions options = null)
        {
            BattleGrid grid = MissionGridBuilder.Build(mission);
            return Create(mission, grid, playerRoster, terrainAssets, bondRanks, seed, options);
        }

        /// <summary>
        /// Builds a battle whose terrain is read off a painted Tilemap.
        /// </summary>
        /// <param name="mission">Mission asset. Must not be null and must have a configured grid.</param>
        /// <param name="terrainTilemap">Tilemap holding the painted terrain. Must not be null.</param>
        /// <param name="tileBindings">Tile-to-terrain table. Must not be null or empty.</param>
        /// <param name="playerRoster">Katipunan units and the cells the player dropped them on.</param>
        /// <param name="terrainAssets">Capstone Table 2 assets, one per terrain type.</param>
        /// <param name="bondRanks">Capstone Table 3 assets with the rank each pair has earned.</param>
        /// <param name="seed">Battle seed.</param>
        /// <param name="tilemapOrigin">Tilemap cell that corresponds to simulation cell (0, 0).</param>
        /// <param name="options">Optional tuning. Defaults are used when null.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the mission's grid is unconfigured or the battle would contain no units.
        /// </exception>
        public static BattleSetupResult CreateFromTilemap(
            MissionData mission,
            Tilemap terrainTilemap,
            IReadOnlyList<TerrainTileBinding> tileBindings,
            IReadOnlyList<UnitPlacement> playerRoster,
            IReadOnlyList<TerrainModifierData> terrainAssets,
            IReadOnlyList<KapatiranPairRank> bondRanks,
            int seed,
            Vector3Int tilemapOrigin = default,
            BattleSetupOptions options = null)
        {
            BattleGrid grid = MissionGridBuilder.BuildFromTilemap(
                mission, terrainTilemap, tileBindings, tilemapOrigin);

            return Create(mission, grid, playerRoster, terrainAssets, bondRanks, seed, options);
        }

        /// <summary>
        /// Builds a battle on a grid the caller has already constructed.
        /// </summary>
        /// <param name="mission">Mission asset supplying the enemy formation. Must not be null.</param>
        /// <param name="grid">Grid the battle runs on. Must not be null.</param>
        /// <param name="playerRoster">Katipunan units and the cells the player dropped them on.</param>
        /// <param name="terrainAssets">Capstone Table 2 assets, one per terrain type.</param>
        /// <param name="bondRanks">Capstone Table 3 assets with the rank each pair has earned.</param>
        /// <param name="seed">Battle seed.</param>
        /// <param name="options">Optional tuning. Defaults are used when null.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the battle would contain no units.</exception>
        public static BattleSetupResult Create(
            MissionData mission,
            BattleGrid grid,
            IReadOnlyList<UnitPlacement> playerRoster,
            IReadOnlyList<TerrainModifierData> terrainAssets,
            IReadOnlyList<KapatiranPairRank> bondRanks,
            int seed,
            BattleSetupOptions options = null)
        {
            if (mission == null)
            {
                throw new ArgumentNullException(nameof(mission));
            }

            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            if (playerRoster == null)
            {
                throw new ArgumentNullException(nameof(playerRoster));
            }

            BattleSetupOptions settings = options ?? new BattleSetupOptions();
            CombatConfig config = settings.CreateConfig(seed);

            UnitDataAdapter adapter = new UnitDataAdapter();
            List<CombatUnit> units = new List<CombatUnit>(playerRoster.Count + mission.EnemySpawns.Count);

            AppendPlayerRoster(units, adapter, grid, playerRoster, config.StackingPolicy);
            AppendEnemySpawns(units, adapter, grid, mission, config.StackingPolicy);

            if (units.Count == 0)
            {
                throw new InvalidOperationException(
                    "Mission '" + mission.MissionId + "' produced no units: the player roster is "
                        + "empty and the mission authors no enemy spawns. A battle needs at least "
                        + "one unit.");
            }

            TerrainModifierProvider terrain = terrainAssets == null
                ? TerrainModifierProvider.CreateEmpty()
                : TerrainTableAdapter.CreateProvider(terrainAssets);

            KapatiranResolver kapatiran = bondRanks == null
                ? KapatiranResolver.CreateEmpty()
                : KapatiranBondAdapter.CreateResolver(
                    bondRanks,
                    settings.KapatiranProximityRule,
                    settings.KapatiranRadius,
                    settings.KapatiranRequiresSameTeam,
                    settings.KapatiranRanksAreCumulative);

            BattleSimulator simulator = new BattleSimulator(
                grid,
                units,
                config,
                formula: null,
                targeting: TargetingStrategies.Default(config.AllowDiagonalMovement),
                terrain: terrain,
                kapatiran: kapatiran,
                movement: null);

            return new BattleSetupResult(simulator, grid, adapter, config);
        }

        private static void AppendPlayerRoster(
            List<CombatUnit> units,
            UnitDataAdapter adapter,
            BattleGrid grid,
            IReadOnlyList<UnitPlacement> roster,
            ModifierStackingPolicy stackingPolicy)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                UnitPlacement placement = roster[i];

                if (placement.Unit == null)
                {
                    Debug.LogWarning("Roster entry " + i + " has no UnitData assigned. Entry ignored.");
                    continue;
                }

                if (!grid.InBounds(placement.Cell))
                {
                    Debug.LogWarning(
                        "Roster entry " + i + " ('" + placement.Unit.name + "') is placed at "
                            + placement.Cell + ", outside the " + grid.Width + "x" + grid.Height
                            + " grid. Entry ignored.");
                    continue;
                }

                units.Add(adapter.CreateCombatUnit(
                    placement.Unit, Team.Katipunan, placement.Cell, stackingPolicy));
            }
        }

        private static void AppendEnemySpawns(
            List<CombatUnit> units,
            UnitDataAdapter adapter,
            BattleGrid grid,
            MissionData mission,
            ModifierStackingPolicy stackingPolicy)
        {
            IReadOnlyList<EnemySpawnEntry> spawns = mission.EnemySpawns;

            for (int i = 0; i < spawns.Count; i++)
            {
                EnemySpawnEntry spawn = spawns[i];

                if (spawn == null || spawn.Unit == null)
                {
                    Debug.LogWarning(
                        "Mission '" + mission.MissionId + "' enemy spawn " + i
                            + " has no UnitData assigned. Entry ignored.");
                    continue;
                }

                if (!grid.InBounds(spawn.Coord))
                {
                    Debug.LogWarning(
                        "Mission '" + mission.MissionId + "' enemy spawn " + i + " sits at "
                            + spawn.Coord + ", outside its own " + grid.Width + "x" + grid.Height
                            + " grid. Entry ignored.");
                    continue;
                }

                units.Add(adapter.CreateCombatUnit(
                    spawn.Unit, Team.Spanish, spawn.Coord, stackingPolicy));
            }
        }
    }

    /// <summary>
    /// The knobs <see cref="BattleSetup"/> exposes, gathered so a MonoBehaviour can serialise them
    /// and a test can construct them inline.
    /// </summary>
    /// <remarks>
    /// Every value here is a rule the capstone document leaves open. They are collected in one type
    /// rather than scattered through call sites so that the list of open questions stays visible.
    /// </remarks>
    [Serializable]
    public sealed class BattleSetupOptions
    {
        [Header("Turn Resolution")]
        [Tooltip("Turns before a draw is declared. TODO(design): not specified in capstone document.")]
        [Min(1)]
        [SerializeField] private int maxTurns = 120;

        [Tooltip("Damage floor for a connecting hit. TODO(design): not specified in capstone document.")]
        [Min(0f)]
        [SerializeField] private float minimumDamage = 1f;

        [Tooltip("Damage multiplier on a critical hit. TODO(design): not specified in capstone document.")]
        [Min(1f)]
        [SerializeField] private float criticalHitMultiplier = 2f;

        [Tooltip("Allow units to move and reach diagonally. TODO(design): not specified in capstone document.")]
        [SerializeField] private bool allowDiagonalMovement = false;

        [Tooltip("How percentage modifiers from different sources combine. TODO(design): not specified in capstone document.")]
        [SerializeField] private ModifierStackingPolicy stackingPolicy = ModifierStackingPolicy.AdditivePercent;

        [Tooltip("How Defense reduces incoming damage. TODO(design): not specified in capstone document.")]
        [SerializeField] private DamageMitigationMode mitigationMode = DamageMitigationMode.Subtractive;

        [Tooltip("Write one event per modifier application. Verbose; useful when a replay desyncs.")]
        [SerializeField] private bool logModifierEvents = false;

        [Header("Kapatiran (Capstone Table 3)")]
        [Tooltip("How close two bonded units must stand. TODO(design): the document says only 'adjacent'.")]
        [SerializeField] private KapatiranProximityRule kapatiranProximityRule = KapatiranProximityRule.Orthogonal;

        [Tooltip("Chebyshev radius used when the proximity rule is Radius.")]
        [Min(1)]
        [SerializeField] private int kapatiranRadius = 1;

        [Tooltip("Only fire a bond between units on the same side.")]
        [SerializeField] private bool kapatiranRequiresSameTeam = true;

        [Tooltip("Rank A also grants ranks C and B. TODO(design): not specified in capstone document.")]
        [SerializeField] private bool kapatiranRanksAreCumulative = false;

        /// <summary>How close two bonded units must stand.</summary>
        public KapatiranProximityRule KapatiranProximityRule
        {
            get { return kapatiranProximityRule; }
        }

        /// <summary>Chebyshev radius used when the proximity rule is Radius.</summary>
        public int KapatiranRadius
        {
            get { return kapatiranRadius; }
        }

        /// <summary>Whether a bond only fires between units on the same side.</summary>
        public bool KapatiranRequiresSameTeam
        {
            get { return kapatiranRequiresSameTeam; }
        }

        /// <summary>Whether a rank's effects include every rank below it.</summary>
        public bool KapatiranRanksAreCumulative
        {
            get { return kapatiranRanksAreCumulative; }
        }

        /// <summary>Builds the Core config these options describe, for a given seed.</summary>
        /// <param name="seed">Battle seed. The same seed replays the same battle.</param>
        public CombatConfig CreateConfig(int seed)
        {
            return new CombatConfig
            {
                RandomSeed = seed,
                MaxTurns = maxTurns,
                MinimumDamage = minimumDamage,
                CriticalHitMultiplier = criticalHitMultiplier,
                AllowDiagonalMovement = allowDiagonalMovement,
                StackingPolicy = stackingPolicy,
                MitigationMode = mitigationMode,
                LogModifierEvents = logModifierEvents
            };
        }
    }
}
