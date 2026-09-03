using System;
using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using BinakayanRising.Data;
using BinakayanRising.Gameplay.Adapters;
using BinakayanRising.Gameplay.Deployment;
using BinakayanRising.Gameplay.Flow;
using BinakayanRising.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BinakayanRising.Gameplay.Integration
{
    /// <summary>
    /// One bond asset paired with the rank that pair has currently earned.
    /// </summary>
    /// <remarks>
    /// This exists because <see cref="KapatiranPairRank"/> is a readonly struct and therefore cannot
    /// be serialized by Unity. The bridge converts these rows into that struct at battle start.
    /// Which rank a pair has reached is save-data's concern; the simulation never computes it.
    /// </remarks>
    [Serializable]
    public sealed class KapatiranBondRankRow
    {
        [SerializeField]
        [Tooltip("Capstone Table 3 bond asset for one character pair.")]
        private KapatiranBondData bond;

        [SerializeField]
        [Tooltip("Rank this pair has currently earned. None disables the bond for this battle.")]
        private KapatiranRank rank = KapatiranRank.None;

        /// <summary>The bond asset for this pair.</summary>
        public KapatiranBondData Bond
        {
            get { return bond; }
        }

        /// <summary>The rank this pair has currently earned.</summary>
        public KapatiranRank Rank
        {
            get { return rank; }
        }
    }

    /// <summary>
    /// The single component that joins the deployment/flow half of the game to the simulation and
    /// the replay half.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>BattleSetup</c> is a static factory and <c>BattleReplayer</c> is a presentation component;
    /// neither can satisfy <see cref="IBattleLauncher"/> or <see cref="IBattleReplayControl"/> on its
    /// own, because running a battle and replaying it need one object that holds the grid, the
    /// pre-simulation roster snapshot and the finished result together. This is that object.
    /// </para>
    /// <para>
    /// <b>Order of operations, and why it matters.</b> The battle resolves completely and
    /// synchronously inside <see cref="Launch"/>, before a single sprite moves. The roster must be
    /// snapshotted <em>before</em> that call, because the simulator mutates unit positions in place
    /// and a unit's deployment cell would otherwise be lost. Everything the player then watches is
    /// the event log being replayed, which is why the same mission, formation and seed always
    /// produce the same battle on screen.
    /// </para>
    /// <para>
    /// Drop this component into <c>CombatPhaseController</c>'s battle-launcher and replay-control
    /// slots, both of them.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Binakayan Rising/Battle Bridge")]
    public sealed class BattleBridge : MonoBehaviour, IBattleLauncher, IBattleReplayControl
    {
        [Header("Presentation")]
        [SerializeField]
        [Tooltip("Replayer that animates the finished event log. Required.")]
        private BattleReplayer replayer;

        [Header("Capstone Table 2 — Environmental Terrain Modifiers")]
        [SerializeField]
        [Tooltip("One TerrainModifierData asset per TerrainType. Missing types fall back to no modifiers.")]
        private List<TerrainModifierData> terrainAssets = new List<TerrainModifierData>();

        [Header("Capstone Table 3 — Kapatiran Synergy")]
        [SerializeField]
        [Tooltip("Bond assets and the rank each pair has currently earned.")]
        private List<KapatiranBondRankRow> bondRanks = new List<KapatiranBondRankRow>();

        [Header("Terrain source")]
        [SerializeField]
        [Tooltip(
            "When set, terrain is read off this painted Tilemap instead of the mission asset. " +
            "MissionData stores no per-cell terrain, so without a Tilemap every cell is Standard Grid.")]
        private Tilemap terrainTilemap;

        [SerializeField]
        [Tooltip("Tile-to-terrain table. Required when a terrain Tilemap is assigned.")]
        private List<TerrainTileBinding> tileBindings = new List<TerrainTileBinding>();

        [SerializeField]
        [Tooltip("Tilemap cell that corresponds to simulation cell (0, 0).")]
        private Vector3Int tilemapOrigin = Vector3Int.zero;

        [Header("Simulation tuning")]
        [SerializeField]
        [Tooltip(
            "Turn cap, damage floor, crit multiplier, modifier stacking, mitigation mode and the " +
            "Kapatiran proximity rule. Every one of these is a TODO(design): the capstone document " +
            "specifies none of them.")]
        private BattleSetupOptions options = new BattleSetupOptions();

        private BattleGrid cachedGrid;
        private MissionData cachedGridMission;
        private BattleSetupResult setupResult;
        private BattleResult lastResult;
        private IReadOnlyList<UnitReplayEntry> replayRoster;

        /// <inheritdoc />
        public event Action<int, BattleOutcome> TurnEnded;

        /// <inheritdoc />
        public event Action<BattleResult> BattleFinished;

        /// <inheritdoc />
        public bool IsPlaying
        {
            get { return replayer != null && replayer.IsPlaying; }
        }

        /// <inheritdoc />
        public bool IsPaused
        {
            get { return replayer != null && replayer.IsPaused; }
        }

        /// <summary>The most recently resolved battle, or null before the first <see cref="Launch"/>.</summary>
        public BattleResult LastResult
        {
            get { return lastResult; }
        }

        private void OnEnable()
        {
            if (replayer != null)
            {
                replayer.TurnEnded += HandleReplayerTurnEnded;
                replayer.BattleFinished += HandleReplayerBattleFinished;
            }
        }

        private void OnDisable()
        {
            if (replayer != null)
            {
                replayer.TurnEnded -= HandleReplayerTurnEnded;
                replayer.BattleFinished -= HandleReplayerBattleFinished;
            }
        }

        /// <inheritdoc />
        public IBattleGrid CreateGrid(MissionData mission)
        {
            if (mission == null)
            {
                throw new ArgumentNullException(nameof(mission));
            }

            if (cachedGrid != null && cachedGridMission == mission)
            {
                return cachedGrid;
            }

            if (!mission.HasConfiguredGrid)
            {
                Debug.LogError(
                    "BattleBridge cannot build a grid for mission '" + mission.name +
                    "': its Grid Width and Grid Height are still 0. Configure the mission asset " +
                    "rather than relying on a default board.",
                    this);
                return null;
            }

            cachedGrid = HasTilemapTerrain()
                ? MissionGridBuilder.BuildFromTilemap(mission, terrainTilemap, tileBindings, tilemapOrigin)
                : MissionGridBuilder.Build(mission);

            cachedGridMission = mission;
            return cachedGrid;
        }

        /// <inheritdoc />
        public BattleResult Launch(
            MissionData mission, IReadOnlyList<UnitPlacement> placements, int seed)
        {
            if (mission == null)
            {
                throw new ArgumentNullException(nameof(mission));
            }

            if (placements == null)
            {
                throw new ArgumentNullException(nameof(placements));
            }

            if (placements.Count == 0)
            {
                throw new InvalidOperationException(
                    "BattleBridge.Launch was called with an empty formation. The deployment phase " +
                    "must place at least one unit before combat starts.");
            }

            BattleGrid grid = cachedGrid;
            if (grid == null || cachedGridMission != mission)
            {
                CreateGrid(mission);
                grid = cachedGrid;
            }

            if (grid == null)
            {
                throw new InvalidOperationException(
                    "BattleBridge.Launch could not build a grid for mission '" + mission.name + "'.");
            }

            setupResult = BattleSetup.Create(
                mission, grid, placements, terrainAssets, BuildBondRanks(), seed, options);

            // Snapshot before running: the simulator mutates unit positions in place, so a unit's
            // deployment cell does not survive the battle it is about to fight.
            replayRoster = setupResult.CaptureReplayRoster();

            lastResult = setupResult.Simulator.RunToCompletion();
            return lastResult;
        }

        /// <inheritdoc />
        public void StartReplay(BattleResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (replayer == null)
            {
                Debug.LogError(
                    "BattleBridge has no BattleReplayer assigned, so the battle cannot be shown. " +
                    "The battle itself already resolved: " + result.Outcome + ".",
                    this);
                return;
            }

            if (replayRoster == null)
            {
                Debug.LogError(
                    "BattleBridge.StartReplay was called before Launch, so there is no roster " +
                    "snapshot to resolve the log's unit ids against.",
                    this);
                return;
            }

            lastResult = result;
            replayer.Load(result, cachedGrid, replayRoster);
            replayer.Play();
        }

        /// <inheritdoc />
        public void Pause()
        {
            if (replayer != null)
            {
                replayer.Pause();
            }
        }

        /// <inheritdoc />
        public void Resume()
        {
            if (replayer != null)
            {
                replayer.Resume();
            }
        }

        /// <inheritdoc />
        public void SkipToEnd()
        {
            if (replayer != null)
            {
                replayer.SkipToEnd();
            }
        }

        private bool HasTilemapTerrain()
        {
            return terrainTilemap != null && tileBindings != null && tileBindings.Count > 0;
        }

        private IReadOnlyList<KapatiranPairRank> BuildBondRanks()
        {
            List<KapatiranPairRank> ranks = new List<KapatiranPairRank>();
            if (bondRanks == null)
            {
                return ranks;
            }

            for (int i = 0; i < bondRanks.Count; i++)
            {
                KapatiranBondRankRow row = bondRanks[i];
                if (row == null || row.Bond == null || row.Rank == KapatiranRank.None)
                {
                    continue;
                }

                ranks.Add(new KapatiranPairRank(row.Bond, row.Rank));
            }

            return ranks;
        }

        private void HandleReplayerTurnEnded(int turnNumber, BattleOutcome outcome)
        {
            Action<int, BattleOutcome> handler = TurnEnded;
            if (handler != null)
            {
                handler(turnNumber, outcome);
            }
        }

        private void HandleReplayerBattleFinished(BattleResult result)
        {
            Action<BattleResult> handler = BattleFinished;
            if (handler != null)
            {
                handler(result);
            }
        }
    }
}
