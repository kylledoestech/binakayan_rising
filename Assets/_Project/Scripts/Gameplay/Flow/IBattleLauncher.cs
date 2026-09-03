using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using BinakayanRising.Data;
using BinakayanRising.Gameplay.Deployment;

namespace BinakayanRising.Gameplay.Flow
{
    /// <summary>
    /// The seam between the deployment/flow half of the Gameplay assembly and the battle adapter
    /// that constructs and runs a <see cref="BattleSimulator"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Implemented by <c>BinakayanRising.Gameplay.Adapters.BattleSetup</c></b>, which owns
    /// everything this interface deliberately hides: turning <see cref="MissionData"/> into a
    /// <see cref="BattleGrid"/> with terrain and a deployment mask, turning
    /// <see cref="UnitData"/> plus a cell into a <see cref="CombatUnit"/> with a
    /// <see cref="Team"/> and a unique id, assembling the <see cref="CombatConfig"/>, the damage
    /// formula, the targeting strategy, the terrain modifier provider and the Kapatiran resolver,
    /// and finally calling <see cref="BattleSimulator.RunToCompletion"/>.
    /// </para>
    /// <para>
    /// The interface is intentionally two members wide. Anything more would leak the adapter's
    /// construction order into the flow layer, which only ever needs "give me the map so I can draw
    /// the deployment zone" and "run the battle and hand me the log".
    /// </para>
    /// <para>
    /// <b>Wiring.</b> <c>BattleSetup</c> is a static factory rather than a component, so the scene
    /// needs one small bridge <see cref="UnityEngine.MonoBehaviour"/> that implements this interface
    /// by calling <c>BattleSetup.Create(...)</c> and then
    /// <c>BattleSetupResult.Simulator.RunToCompletion()</c>. Drop that bridge into
    /// <c>CombatPhaseController</c>'s "Battle Launcher Source" slot, or inject it in code with
    /// <c>CombatPhaseController.SetBattleLauncher</c>. The same bridge is the natural place to
    /// implement <see cref="IBattleReplayControl"/>, because it is the only object that holds both
    /// the grid and <c>BattleSetupResult.CaptureReplayRoster()</c>, which
    /// <c>BattleReplayer.Load</c> needs.
    /// </para>
    /// </remarks>
    public interface IBattleLauncher
    {
        /// <summary>
        /// Builds the battle map for a mission: its dimensions, its terrain, and the deployment mask
        /// that <see cref="IBattleGrid.IsDeployable"/> reports.
        /// </summary>
        /// <remarks>
        /// Called once when the Deployment state is entered, before any portrait is dragged, so the
        /// deployment phase and the battle cannot disagree about the map. Implementations should
        /// return the same grid instance to a later <see cref="Launch"/> call for the same mission.
        /// </remarks>
        /// <param name="mission">The mission being loaded. Never null.</param>
        /// <returns>
        /// The map, or <c>null</c> when the mission asset is unconfigured — for example while
        /// <see cref="MissionData.HasConfiguredGrid"/> is false. Callers must handle null rather
        /// than substituting a default board.
        /// </returns>
        IBattleGrid CreateGrid(MissionData mission);

        /// <summary>
        /// Runs one complete battle headlessly and returns its full event log.
        /// </summary>
        /// <remarks>
        /// This is a synchronous, deterministic call: it resolves the whole battle before returning.
        /// Nothing is animated here. The caller hands the returned log to an
        /// <see cref="IBattleReplayControl"/>, which is what the player actually watches.
        /// </remarks>
        /// <param name="mission">
        /// The mission supplying the map and the enemy formation from
        /// <see cref="MissionData.EnemySpawns"/>. Never null.
        /// </param>
        /// <param name="placements">
        /// The player's locked formation, as emitted by
        /// <c>DeploymentController.FormationLocked</c>. Order is stable and should be used to assign
        /// unit ids, because every deterministic tie-break in the simulator falls back to unit id.
        /// Never null and never empty.
        /// </param>
        /// <param name="seed">
        /// The value handed to <see cref="CombatConfig.RandomSeed"/>. The same mission, the same
        /// placements and the same seed must produce a byte-identical event log.
        /// </param>
        /// <returns>
        /// The finished battle: its <see cref="BattleOutcome"/>, its turn count, its survivor counts
        /// and its complete <see cref="BattleEvent"/> log. Never null.
        /// </returns>
        BattleResult Launch(MissionData mission, IReadOnlyList<UnitPlacement> placements, int seed);
    }
}
