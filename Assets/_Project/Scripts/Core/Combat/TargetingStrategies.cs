using System;
using System.Collections.Generic;
using BinakayanRising.Core.Grid;

namespace BinakayanRising.Core.Combat
{
    /// <summary>
    /// Picks the closest enemy, breaking ties on the lowest <see cref="CombatUnit.Id"/>.
    /// </summary>
    /// <remarks>
    /// This is the resolver's default. TODO(design): not specified in capstone document — see
    /// <see cref="ITargetingStrategy"/>.
    /// </remarks>
    public sealed class NearestTargetStrategy : ITargetingStrategy
    {
        private readonly bool allowDiagonals;

        /// <summary>
        /// Creates the strategy.
        /// </summary>
        /// <param name="allowDiagonals">
        /// When true, distance is Chebyshev (diagonal steps cost one); when false, Manhattan. Must
        /// match <see cref="CombatConfig.AllowDiagonalMovement"/> or "nearest" and "reachable
        /// soonest" will disagree.
        /// </param>
        public NearestTargetStrategy(bool allowDiagonals)
        {
            this.allowDiagonals = allowDiagonals;
        }

        /// <inheritdoc />
        public CombatUnit SelectTarget(CombatUnit self, IReadOnlyList<CombatUnit> candidates, IBattleGrid grid)
        {
            if (self == null || candidates == null)
            {
                return null;
            }

            CombatUnit best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                CombatUnit candidate = candidates[i];
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                int distance = allowDiagonals
                    ? GridDistance.Chebyshev(self.Position, candidate.Position)
                    : GridDistance.Manhattan(self.Position, candidate.Position);

                if (best == null || distance < bestDistance || (distance == bestDistance && candidate.Id < best.Id))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }
    }

    /// <summary>
    /// Picks the enemy with the least remaining health — a "finish the wounded" heuristic — breaking
    /// ties first on distance and then on the lowest <see cref="CombatUnit.Id"/>.
    /// </summary>
    /// <remarks>TODO(design): not specified in capstone document. Offered as a tuning option.</remarks>
    public sealed class LowestHpTargetStrategy : ITargetingStrategy
    {
        private readonly bool allowDiagonals;

        /// <summary>Creates the strategy.</summary>
        /// <param name="allowDiagonals">Distance metric used for the secondary tie-break.</param>
        public LowestHpTargetStrategy(bool allowDiagonals)
        {
            this.allowDiagonals = allowDiagonals;
        }

        /// <inheritdoc />
        public CombatUnit SelectTarget(CombatUnit self, IReadOnlyList<CombatUnit> candidates, IBattleGrid grid)
        {
            if (self == null || candidates == null)
            {
                return null;
            }

            CombatUnit best = null;
            float bestHP = 0f;
            int bestDistance = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                CombatUnit candidate = candidates[i];
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                int distance = allowDiagonals
                    ? GridDistance.Chebyshev(self.Position, candidate.Position)
                    : GridDistance.Manhattan(self.Position, candidate.Position);

                if (best == null
                    || candidate.CurrentHP < bestHP
                    || (candidate.CurrentHP == bestHP && distance < bestDistance)
                    || (candidate.CurrentHP == bestHP && distance == bestDistance && candidate.Id < best.Id))
                {
                    best = candidate;
                    bestHP = candidate.CurrentHP;
                    bestDistance = distance;
                }
            }

            return best;
        }
    }

    /// <summary>
    /// Picks the most dangerous enemy by a caller-supplied threat score, breaking ties first on
    /// distance and then on the lowest <see cref="CombatUnit.Id"/>.
    /// </summary>
    /// <remarks>
    /// TODO(design): not specified in capstone document, including what "threat" means. The default
    /// score is simply the enemy's effective Attack Damage, which is the only stat the document
    /// unambiguously ties to how much harm a unit can do. Pass a custom scorer to weigh anything
    /// else. The scorer must be a pure function of the unit, or determinism is lost.
    /// </remarks>
    public sealed class HighestThreatTargetStrategy : ITargetingStrategy
    {
        private readonly bool allowDiagonals;
        private readonly Func<CombatUnit, float> threatScore;

        /// <summary>Creates the strategy.</summary>
        /// <param name="allowDiagonals">Distance metric used for the secondary tie-break.</param>
        /// <param name="threatScore">
        /// Scores how dangerous a unit is; higher is more dangerous. Null uses effective Attack
        /// Damage.
        /// </param>
        public HighestThreatTargetStrategy(bool allowDiagonals, Func<CombatUnit, float> threatScore = null)
        {
            this.allowDiagonals = allowDiagonals;
            this.threatScore = threatScore ?? DefaultThreatScore;
        }

        /// <summary>Effective Attack Damage. The default threat metric.</summary>
        /// <param name="unit">Unit to score.</param>
        public static float DefaultThreatScore(CombatUnit unit)
        {
            return unit == null ? 0f : unit.GetEffectiveStat(StatKind.AttackDamage);
        }

        /// <inheritdoc />
        public CombatUnit SelectTarget(CombatUnit self, IReadOnlyList<CombatUnit> candidates, IBattleGrid grid)
        {
            if (self == null || candidates == null)
            {
                return null;
            }

            CombatUnit best = null;
            float bestScore = 0f;
            int bestDistance = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                CombatUnit candidate = candidates[i];
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                float score = threatScore(candidate);
                int distance = allowDiagonals
                    ? GridDistance.Chebyshev(self.Position, candidate.Position)
                    : GridDistance.Manhattan(self.Position, candidate.Position);

                if (best == null
                    || score > bestScore
                    || (score == bestScore && distance < bestDistance)
                    || (score == bestScore && distance == bestDistance && candidate.Id < best.Id))
                {
                    best = candidate;
                    bestScore = score;
                    bestDistance = distance;
                }
            }

            return best;
        }
    }

    /// <summary>
    /// Factory helpers for the built-in targeting strategies.
    /// </summary>
    public static class TargetingStrategies
    {
        /// <summary>Closest enemy first. Ties break on the lowest unit id.</summary>
        /// <param name="allowDiagonals">Match <see cref="CombatConfig.AllowDiagonalMovement"/>.</param>
        public static ITargetingStrategy Nearest(bool allowDiagonals)
        {
            return new NearestTargetStrategy(allowDiagonals);
        }

        /// <summary>Weakest enemy first. Ties break on distance, then on the lowest unit id.</summary>
        /// <param name="allowDiagonals">Match <see cref="CombatConfig.AllowDiagonalMovement"/>.</param>
        public static ITargetingStrategy LowestHp(bool allowDiagonals)
        {
            return new LowestHpTargetStrategy(allowDiagonals);
        }

        /// <summary>Most dangerous enemy first. Ties break on distance, then on the lowest unit id.</summary>
        /// <param name="allowDiagonals">Match <see cref="CombatConfig.AllowDiagonalMovement"/>.</param>
        /// <param name="threatScore">Optional custom threat metric. Null uses effective Attack Damage.</param>
        public static ITargetingStrategy HighestThreat(bool allowDiagonals, Func<CombatUnit, float> threatScore = null)
        {
            return new HighestThreatTargetStrategy(allowDiagonals, threatScore);
        }

        /// <summary>
        /// The resolver's default when no strategy is injected.
        /// TODO(design): not specified in capstone document; see <see cref="ITargetingStrategy"/>.
        /// </summary>
        /// <param name="allowDiagonals">Match <see cref="CombatConfig.AllowDiagonalMovement"/>.</param>
        public static ITargetingStrategy Default(bool allowDiagonals)
        {
            return new NearestTargetStrategy(allowDiagonals);
        }
    }
}
