using System.Collections.Generic;

namespace BinakayanRising.Core.Grid
{
    /// <summary>What happens when a unit is put down on a cell during deployment.</summary>
    public enum DropVerdict
    {
        /// <summary>The unit was in reserve and now stands on the cell.</summary>
        Placed,

        /// <summary>The unit was already on the board and moves to the cell.</summary>
        Moved,

        /// <summary>The unit already stands on that very cell; nothing changes.</summary>
        Unchanged,

        /// <summary>The cell is off the map.</summary>
        OutOfBounds,

        /// <summary>The cell is on the map but outside the deployment zone.</summary>
        NotDeployable,

        /// <summary>Another unit, or a fixed piece such as the supply cart, already stands on the cell.</summary>
        Occupied,

        /// <summary>The squad is full and the unit is not already on the board.</summary>
        SquadFull
    }

    /// <summary>
    /// The one rule for putting a unit down during deployment, shared by click-to-place and
    /// drag-and-drop so the two can never disagree about which tiles take a unit.
    /// </summary>
    /// <remarks>
    /// An occupied cell refuses rather than swapping: click-to-place has always treated a click
    /// on a placed unit as "lift it", and a drop that silently shuffled someone else's unit would
    /// be the one place the two gestures differed.
    /// </remarks>
    public static class DeploymentDrop
    {
        /// <summary>Judges putting <paramref name="unitId"/> down on <paramref name="cell"/>.</summary>
        /// <param name="grid">The battle map.</param>
        /// <param name="placements">Every unit already on the board, by id.</param>
        /// <param name="unitId">The unit being put down.</param>
        /// <param name="cell">The cell it is put down on.</param>
        /// <param name="squadCap">How many units the board takes at most.</param>
        /// <param name="blocked">
        /// Cells something other than the squad already stands on, such as the Escort supply cart
        /// (#37). They refuse a unit exactly as a placed unit's cell does. Null for none.
        /// </param>
        public static DropVerdict Judge(
            IBattleGrid grid, IReadOnlyDictionary<int, GridCoord> placements, int unitId, GridCoord cell, int squadCap,
            ICollection<GridCoord> blocked = null)
        {
            if (grid == null || !grid.InBounds(cell))
            {
                return DropVerdict.OutOfBounds;
            }

            if (!grid.IsDeployable(cell))
            {
                return DropVerdict.NotDeployable;
            }

            GridCoord current = default(GridCoord);
            bool onBoard = placements != null && placements.TryGetValue(unitId, out current);
            if (onBoard && current == cell)
            {
                return DropVerdict.Unchanged;
            }

            if (blocked != null && blocked.Contains(cell))
            {
                return DropVerdict.Occupied;
            }

            if (placements != null)
            {
                foreach (KeyValuePair<int, GridCoord> placement in placements)
                {
                    if (placement.Key != unitId && placement.Value == cell)
                    {
                        return DropVerdict.Occupied;
                    }
                }
            }

            if (onBoard)
            {
                return DropVerdict.Moved;
            }

            int count = placements != null ? placements.Count : 0;
            return count >= squadCap ? DropVerdict.SquadFull : DropVerdict.Placed;
        }

        /// <summary>True when the verdict puts the unit on the cell.</summary>
        public static bool Accepts(DropVerdict verdict)
        {
            return verdict == DropVerdict.Placed || verdict == DropVerdict.Moved;
        }
    }
}
