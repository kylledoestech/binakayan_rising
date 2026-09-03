using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;

namespace BinakayanRising.Tests.Combat
{
    /// <summary>
    /// Shared construction helpers for the combat tests.
    /// </summary>
    /// <remarks>
    /// The capstone document supplies no base stat values, so every number these helpers default to
    /// was chosen purely to make a test's arithmetic legible — attack 10 against defense 0 deals 10.
    /// Nothing here is a balance proposal and nothing in the resolver depends on these values.
    /// </remarks>
    internal static class CombatTestFactory
    {
        /// <summary>
        /// Builds a stat block. Defaults give a unit that always hits, never crits, never dodges,
        /// and moves one cell per turn, so any randomness in a test has to be opted into.
        /// </summary>
        internal static UnitStats Stats(
            float maxHP = 100f,
            float attackDamage = 10f,
            float defense = 0f,
            float evasion = 0f,
            float rangedAccuracy = 1f,
            float attackRange = 1f,
            float criticalHitChance = 0f,
            float movementSpeed = 1f)
        {
            return new UnitStats(
                maxHP, attackDamage, defense, evasion, rangedAccuracy, attackRange, criticalHitChance, movementSpeed);
        }

        /// <summary>Builds a combat unit at a cell.</summary>
        internal static CombatUnit Unit(
            int id,
            Team team,
            UnitStats stats,
            int x,
            int y,
            string archetypeId = null,
            ModifierStackingPolicy policy = ModifierStackingPolicy.AdditivePercent)
        {
            string name = team + "_" + id;
            return new CombatUnit(id, name, archetypeId ?? name, team, stats, new GridCoord(x, y), policy);
        }

        /// <summary>An all-StandardGrid map with every cell deployable.</summary>
        internal static BattleGrid FlatGrid(int width, int height)
        {
            BattleGrid grid = new BattleGrid(width, height, TerrainType.StandardGrid);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    grid.SetDeployable(new GridCoord(x, y), true);
                }
            }

            return grid;
        }

        /// <summary>A config with deterministic defaults suited to assertions.</summary>
        internal static CombatConfig Config(int seed = 0, int maxTurns = 50)
        {
            return new CombatConfig
            {
                RandomSeed = seed,
                MaxTurns = maxTurns,
                MinimumDamage = 1f,
                CriticalHitMultiplier = 2f,
                AllowDiagonalMovement = false,
                StackingPolicy = ModifierStackingPolicy.AdditivePercent
            };
        }

        /// <summary>
        /// The Capstone Table 3 bond between the Caviteño Marksman and the Trench Engineer at rank A:
        /// "+20% Ranged Accuracy, +1 Attack Range". The flat range bonus is the one modifier in the
        /// whole document that is not a percentage, which is exactly why it is the one used here.
        /// </summary>
        internal static KapatiranBond MarksmanEngineerRankA()
        {
            return new KapatiranBond(
                "Marksman_Engineer",
                "Marksman",
                "Engineer",
                new List<StatModifier>
                {
                    StatModifier.Percent(StatKind.RangedAccuracy, 0.20f, ModifierSource.Kapatiran, "Marksman_Engineer"),
                    StatModifier.Flat(StatKind.AttackRange, 1f, ModifierSource.Kapatiran, "Marksman_Engineer")
                },
                "A");
        }

        /// <summary>A simple bond granting a percentage attack buff, for adjacency tests.</summary>
        internal static KapatiranBond AttackBond(float percent = 0.20f)
        {
            return new KapatiranBond(
                "Evangelista_Aguinaldo",
                "Evangelista",
                "Aguinaldo",
                new List<StatModifier>
                {
                    StatModifier.Percent(StatKind.AttackDamage, percent, ModifierSource.Kapatiran, "Evangelista_Aguinaldo")
                },
                "B");
        }

        /// <summary>Finds the first DamageDealt event authored by a unit, or returns -1.</summary>
        internal static float FirstDamageBy(IReadOnlyList<BattleEvent> events, int attackerId)
        {
            for (int i = 0; i < events.Count; i++)
            {
                BattleEvent e = events[i];
                if (e.Type == BattleEventType.DamageDealt && e.ActorId == attackerId)
                {
                    return e.Amount;
                }
            }

            return -1f;
        }

        /// <summary>True when the log contains an event of the given type authored by a unit.</summary>
        internal static bool HasEvent(IReadOnlyList<BattleEvent> events, BattleEventType type, int actorId)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type && events[i].ActorId == actorId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
