using System.Collections.Generic;
using BinakayanRising.Core.Grid;

namespace BinakayanRising.Core.Combat
{
    /// <summary>
    /// Decides the single next cell a unit steps to while advancing on its target.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the extension seam for pathfinding.</b> The capstone document's state machine
    /// names a stage called <c>AI_Pathfinding</c> and says nothing whatsoever about what algorithm
    /// it runs. The vertical slice therefore ships <see cref="GreedyStepMovementPolicy"/>, a
    /// one-cell greedy descent, which is enough to make units close distance and engage. When the
    /// team needs units to route around a bamboo barricade instead of stalling in front of it, drop
    /// an A* implementation in here — <see cref="BattleSimulator"/> requires no change.
    /// </para>
    /// <para>
    /// The interface returns one step rather than a whole path on purpose: units move
    /// simultaneously enough that a path computed at the start of a turn is stale by the end of it,
    /// and re-planning per step keeps the simulator's state model to "positions only", which is what
    /// makes a battle cheap to snapshot and replay.
    /// </para>
    /// <para>
    /// <b>Determinism contract.</b> Implementations must be pure functions of their arguments and
    /// must break ties in a fixed order — <see cref="GridDistance.Neighbors"/> enumerates in a fixed
    /// order precisely so that a policy can lean on it.
    /// </para>
    /// </remarks>
    public interface IMovementPolicy
    {
        /// <summary>
        /// Chooses the next cell for <paramref name="mover"/> on its way to <paramref name="destination"/>.
        /// </summary>
        /// <param name="mover">The moving unit.</param>
        /// <param name="destination">The cell the unit is trying to reach or get adjacent to.</param>
        /// <param name="grid">The battle map. Consult <see cref="IBattleGrid.IsWalkable"/> before entering any cell.</param>
        /// <param name="allUnits">Every unit in the battle, used to reject occupied cells.</param>
        /// <param name="allowDiagonals">Whether diagonal steps are permitted.</param>
        /// <param name="next">The chosen cell, valid only when the method returns <c>true</c>.</param>
        /// <returns><c>true</c> when a legal improving step exists; <c>false</c> when the unit should hold position.</returns>
        bool TryGetNextStep(
            CombatUnit mover,
            GridCoord destination,
            IBattleGrid grid,
            IReadOnlyList<CombatUnit> allUnits,
            bool allowDiagonals,
            out GridCoord next);
    }

    /// <summary>
    /// A one-cell greedy descent: of the legal neighbouring cells, take the one that most reduces
    /// the distance to the destination, and hold position if none improves it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Deliberately not A*.</b> The document asks only for "autonomous pathfinding" and the
    /// vertical slice needs units that close and engage, not units that solve mazes. Greedy descent
    /// is a few lines, is trivially deterministic, and cannot loop: because a step is only taken
    /// when it strictly reduces the distance, a unit can never oscillate between two cells and can
    /// never wander, which is the failure mode that would turn a hung battle into a hung editor.
    /// </para>
    /// <para>
    /// <b>Its known limitation is intentional.</b> A unit whose direct approach is blocked by a
    /// bamboo barricade will stop at the wall rather than walk around it, because no neighbouring
    /// cell reduces the distance. That is a visible, debuggable behaviour rather than a subtle one,
    /// and the turn cap in <see cref="CombatConfig.MaxTurns"/> guarantees the battle still
    /// terminates. Replace this policy with A* when barricade layouts become non-trivial.
    /// </para>
    /// <para>
    /// Ties are broken by <see cref="GridDistance.Neighbors"/>'s fixed enumeration order: +X, -X,
    /// +Y, -Y, then the four diagonals. The first cell achieving the best distance wins.
    /// </para>
    /// </remarks>
    public sealed class GreedyStepMovementPolicy : IMovementPolicy
    {
        /// <inheritdoc />
        public bool TryGetNextStep(
            CombatUnit mover,
            GridCoord destination,
            IBattleGrid grid,
            IReadOnlyList<CombatUnit> allUnits,
            bool allowDiagonals,
            out GridCoord next)
        {
            next = mover != null ? mover.Position : GridCoord.Zero;

            if (mover == null || grid == null)
            {
                return false;
            }

            int currentDistance = Distance(mover.Position, destination, allowDiagonals);
            int bestDistance = currentDistance;
            bool found = false;

            foreach (GridCoord candidate in GridDistance.Neighbors(mover.Position, allowDiagonals))
            {
                if (!grid.IsWalkable(candidate))
                {
                    // IsWalkable is false both off-map and on a bamboo barricade, so this single
                    // check enforces the document's "impassable" rule and the map bounds at once.
                    continue;
                }

                if (IsOccupied(candidate, allUnits, mover))
                {
                    continue;
                }

                int candidateDistance = Distance(candidate, destination, allowDiagonals);
                if (candidateDistance < bestDistance)
                {
                    bestDistance = candidateDistance;
                    next = candidate;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>Distance under the movement metric in force.</summary>
        private static int Distance(GridCoord a, GridCoord b, bool allowDiagonals)
        {
            return allowDiagonals ? GridDistance.Chebyshev(a, b) : GridDistance.Manhattan(a, b);
        }

        /// <summary>True when a living unit other than <paramref name="mover"/> stands on the cell.</summary>
        private static bool IsOccupied(GridCoord cell, IReadOnlyList<CombatUnit> allUnits, CombatUnit mover)
        {
            if (allUnits == null)
            {
                return false;
            }

            for (int i = 0; i < allUnits.Count; i++)
            {
                CombatUnit other = allUnits[i];
                if (other == null || !other.IsAlive || ReferenceEquals(other, mover))
                {
                    continue;
                }

                if (other.Position == cell)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
