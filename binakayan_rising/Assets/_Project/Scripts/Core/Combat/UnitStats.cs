using System;
using System.Globalization;

namespace BinakayanRising.Core.Combat
{
    /// <summary>
    /// The immutable base stat block of a single unit: the eight statistics the capstone document
    /// names across Table 2 (Environmental Terrain Modifiers) and Table 3 (Kapatiran Synergy Levels).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is pure data with no Unity dependency. The <c>BinakayanRising.Data</c> assembly converts
    /// its <c>UnitData</c> ScriptableObject into one of these at the assembly boundary; Core never
    /// learns that ScriptableObjects exist.
    /// </para>
    /// <para>
    /// Every stat is stored as a <see cref="float"/> — including MaxHP and AttackRange, which are
    /// authored as integers — so that percentage and flat modifiers can be applied uniformly by
    /// <see cref="ModifierStack"/> without a per-stat special case. Use
    /// <see cref="AttackRangeCells"/> when an integer cell count is genuinely required.
    /// </para>
    /// <para>
    /// TODO(design): not specified in capstone document. The document supplies NO base stat values
    /// and NO scale for any of these numbers — it only ever describes how they are modified. Nothing
    /// in this type carries a default other than zero, and nothing in the combat resolver assumes a
    /// magnitude.
    /// </para>
    /// </remarks>
    public readonly struct UnitStats
    {
        /// <summary>A stat block with every statistic at zero.</summary>
        public static readonly UnitStats Zero = new UnitStats(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);

        private readonly float maxHP;
        private readonly float attackDamage;
        private readonly float defense;
        private readonly float evasion;
        private readonly float rangedAccuracy;
        private readonly float attackRange;
        private readonly float criticalHitChance;
        private readonly float movementSpeed;

        /// <summary>
        /// Creates a stat block. Parameter order matches the order the capstone document lists the
        /// stats in, which is also the order of <see cref="StatKind"/>.
        /// </summary>
        /// <param name="maxHP">Maximum hit points.</param>
        /// <param name="attackDamage">Attack damage before mitigation.</param>
        /// <param name="defense">Defense, consumed by the damage formula's mitigation step.</param>
        /// <param name="evasion">Chance to avoid an incoming attack, as a 0..1 fraction.</param>
        /// <param name="rangedAccuracy">Chance for an attack to connect, as a 0..1 fraction.</param>
        /// <param name="attackRange">Attack reach in grid cells.</param>
        /// <param name="criticalHitChance">Chance to land a critical hit, as a 0..1 fraction.</param>
        /// <param name="movementSpeed">Cells traversable per AI turn.</param>
        public UnitStats(
            float maxHP,
            float attackDamage,
            float defense,
            float evasion,
            float rangedAccuracy,
            float attackRange,
            float criticalHitChance,
            float movementSpeed)
        {
            this.maxHP = maxHP;
            this.attackDamage = attackDamage;
            this.defense = defense;
            this.evasion = evasion;
            this.rangedAccuracy = rangedAccuracy;
            this.attackRange = attackRange;
            this.criticalHitChance = criticalHitChance;
            this.movementSpeed = movementSpeed;
        }

        /// <summary>Maximum hit points.</summary>
        public float MaxHP
        {
            get { return maxHP; }
        }

        /// <summary>Attack damage before the defender's mitigation is applied.</summary>
        public float AttackDamage
        {
            get { return attackDamage; }
        }

        /// <summary>Defense, consumed by the damage formula's mitigation step.</summary>
        public float Defense
        {
            get { return defense; }
        }

        /// <summary>Chance to avoid an incoming attack outright, as a 0..1 fraction.</summary>
        public float Evasion
        {
            get { return evasion; }
        }

        /// <summary>Chance for an attack to connect, as a 0..1 fraction.</summary>
        public float RangedAccuracy
        {
            get { return rangedAccuracy; }
        }

        /// <summary>Attack reach in grid cells, as a float so flat and percentage modifiers apply uniformly.</summary>
        public float AttackRange
        {
            get { return attackRange; }
        }

        /// <summary>Chance to land a critical hit, as a 0..1 fraction.</summary>
        public float CriticalHitChance
        {
            get { return criticalHitChance; }
        }

        /// <summary>Cells the unit may traverse per AI turn.</summary>
        public float MovementSpeed
        {
            get { return movementSpeed; }
        }

        /// <summary>
        /// <see cref="AttackRange"/> floored to whole cells, clamped at zero. Provided for callers
        /// that need an integer reach; the simulator itself compares distances against the float so
        /// that a fractional range is never silently rounded away.
        /// </summary>
        public int AttackRangeCells
        {
            get
            {
                double floored = Math.Floor((double)attackRange);
                return floored <= 0d ? 0 : (int)floored;
            }
        }

        /// <summary>
        /// Reads one stat by <see cref="StatKind"/>.
        /// </summary>
        /// <param name="stat">Stat to read.</param>
        /// <returns>The value of that stat.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown for <see cref="StatKind.None"/> and for <see cref="StatKind.HealingReceived"/>,
        /// which is a bond-only stat with no slot in this block.
        /// </exception>
        public float Get(StatKind stat)
        {
            switch (stat)
            {
                case StatKind.MaxHP:
                    return maxHP;
                case StatKind.AttackDamage:
                    return attackDamage;
                case StatKind.Defense:
                    return defense;
                case StatKind.Evasion:
                    return evasion;
                case StatKind.RangedAccuracy:
                    return rangedAccuracy;
                case StatKind.AttackRange:
                    return attackRange;
                case StatKind.CriticalHitChance:
                    return criticalHitChance;
                case StatKind.MovementSpeed:
                    return movementSpeed;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(stat), stat, "UnitStats holds only the eight base statistics; HealingReceived is bond-only.");
            }
        }

        /// <summary>
        /// Returns a copy with one stat replaced.
        /// </summary>
        /// <param name="stat">Stat to overwrite.</param>
        /// <param name="value">New value.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for stats this block does not hold.</exception>
        public UnitStats With(StatKind stat, float value)
        {
            switch (stat)
            {
                case StatKind.MaxHP:
                    return new UnitStats(value, attackDamage, defense, evasion, rangedAccuracy, attackRange, criticalHitChance, movementSpeed);
                case StatKind.AttackDamage:
                    return new UnitStats(maxHP, value, defense, evasion, rangedAccuracy, attackRange, criticalHitChance, movementSpeed);
                case StatKind.Defense:
                    return new UnitStats(maxHP, attackDamage, value, evasion, rangedAccuracy, attackRange, criticalHitChance, movementSpeed);
                case StatKind.Evasion:
                    return new UnitStats(maxHP, attackDamage, defense, value, rangedAccuracy, attackRange, criticalHitChance, movementSpeed);
                case StatKind.RangedAccuracy:
                    return new UnitStats(maxHP, attackDamage, defense, evasion, value, attackRange, criticalHitChance, movementSpeed);
                case StatKind.AttackRange:
                    return new UnitStats(maxHP, attackDamage, defense, evasion, rangedAccuracy, value, criticalHitChance, movementSpeed);
                case StatKind.CriticalHitChance:
                    return new UnitStats(maxHP, attackDamage, defense, evasion, rangedAccuracy, attackRange, value, movementSpeed);
                case StatKind.MovementSpeed:
                    return new UnitStats(maxHP, attackDamage, defense, evasion, rangedAccuracy, attackRange, criticalHitChance, value);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(stat), stat, "UnitStats holds only the eight base statistics; HealingReceived is bond-only.");
            }
        }

        /// <summary>
        /// Produces the effective stat block by resolving every one of the eight statistics through
        /// <paramref name="stack"/>. This is the single place where "base + modifiers" is defined.
        /// </summary>
        /// <param name="stack">Modifiers to apply. Null returns this block unchanged.</param>
        /// <returns>A new stat block; this instance is not mutated.</returns>
        public UnitStats WithModifiers(ModifierStack stack)
        {
            if (stack == null || stack.Count == 0)
            {
                return this;
            }

            return new UnitStats(
                stack.Resolve(StatKind.MaxHP, maxHP),
                stack.Resolve(StatKind.AttackDamage, attackDamage),
                stack.Resolve(StatKind.Defense, defense),
                stack.Resolve(StatKind.Evasion, evasion),
                stack.Resolve(StatKind.RangedAccuracy, rangedAccuracy),
                stack.Resolve(StatKind.AttackRange, attackRange),
                stack.Resolve(StatKind.CriticalHitChance, criticalHitChance),
                stack.Resolve(StatKind.MovementSpeed, movementSpeed));
        }

        /// <summary>Culture-invariant one-line description, safe to embed in a deterministic log.</summary>
        public override string ToString()
        {
            return "hp=" + Format(maxHP)
                + " atk=" + Format(attackDamage)
                + " def=" + Format(defense)
                + " eva=" + Format(evasion)
                + " acc=" + Format(rangedAccuracy)
                + " rng=" + Format(attackRange)
                + " crit=" + Format(criticalHitChance)
                + " spd=" + Format(movementSpeed);
        }

        private static string Format(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
