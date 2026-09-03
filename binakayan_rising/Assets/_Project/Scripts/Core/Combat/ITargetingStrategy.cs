using System.Collections.Generic;
using BinakayanRising.Core.Grid;

namespace BinakayanRising.Core.Combat
{
    /// <summary>
    /// Chooses which enemy a unit attacks or advances on during its activation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// TODO(design): not specified in capstone document. The document says the combat phase is
    /// "fully autonomous" and that the state machine alternates <c>AI_Pathfinding</c> and
    /// <c>DamageCalculation</c>, but it never states how a unit picks whom to path towards. Because
    /// the targeting rule is the single biggest determinant of how an auto-battler feels, it is an
    /// injected strategy rather than a hardcoded rule, and the default is the least surprising one
    /// (<see cref="TargetingStrategies.Nearest"/>).
    /// </para>
    /// <para>
    /// <b>Determinism contract.</b> An implementation must be a pure function of its arguments and
    /// must break every tie on <see cref="CombatUnit.Id"/>. In particular it must never depend on
    /// the order of <c>candidates</c>: the caller supplies them in id order today, but a strategy
    /// that relies on that is one refactor away from producing a different battle from the same
    /// seed. It must not draw randomness — the battle's <see cref="DeterministicRandom"/> is
    /// deliberately not passed in, so that a random-target strategy has to be written as an explicit
    /// exception rather than added by accident.
    /// </para>
    /// </remarks>
    public interface ITargetingStrategy
    {
        /// <summary>
        /// Picks a target from the living enemies of <paramref name="self"/>.
        /// </summary>
        /// <param name="self">The unit currently taking its activation.</param>
        /// <param name="candidates">
        /// Living enemy units. May be empty. Callers supply this in ascending id order, but
        /// implementations must not rely on that.
        /// </param>
        /// <param name="grid">The battle map, for strategies that weigh terrain or reachability.</param>
        /// <returns>The chosen target, or <c>null</c> when <paramref name="candidates"/> is empty.</returns>
        CombatUnit SelectTarget(CombatUnit self, IReadOnlyList<CombatUnit> candidates, IBattleGrid grid);
    }
}
