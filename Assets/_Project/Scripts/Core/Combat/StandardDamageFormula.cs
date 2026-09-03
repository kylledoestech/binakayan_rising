using System;

namespace BinakayanRising.Core.Combat
{
    /// <summary>
    /// The project's placeholder damage formula.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>TODO(design): NOT SPECIFIED IN THE CAPSTONE DOCUMENT. THIS ENTIRE CLASS IS A PLACEHOLDER
    /// AND MUST BE RATIFIED BY THE DESIGN TEAM BEFORE ANY BALANCING WORK.</b> The document names
    /// eight statistics and states that terrain (Table 2) and Kapatiran bonds (Table 3) modify them
    /// by percentages. It does not contain a damage equation, a crit multiplier, a mitigation rule,
    /// an accuracy mechanic, a definition of what Evasion does, or a single base stat value. Every
    /// numeric decision below therefore comes from an injected <see cref="CombatConfig"/> and every
    /// structural decision is called out here.
    /// </para>
    /// <para><b>The formula, in resolution order:</b></para>
    /// <list type="number">
    ///   <item><description>
    ///     <b>Evasion roll</b> (skipped when <see cref="CombatConfig.ApplyEvasionRoll"/> is false).
    ///     The defender dodges with probability <c>defender.Evasion</c>. On success the attack ends
    ///     immediately with zero damage and no further dice are drawn.
    ///     TODO(design): the document never says Evasion is a dodge chance.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Accuracy roll</b> (skipped when <see cref="CombatConfig.ApplyAccuracyRoll"/> is false).
    ///     The attack connects with probability <c>attacker.RangedAccuracy</c>. On failure the attack
    ///     ends with zero damage. TODO(design): the document does not distinguish melee from ranged
    ///     attacks, so this applies to every attack.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Critical roll.</b> Succeeds with probability <c>attacker.CriticalHitChance</c>.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Mitigation.</b> <see cref="DamageMitigationMode.Subtractive"/> gives
    ///     <c>attack - defense</c>; <see cref="DamageMitigationMode.Multiplicative"/> gives
    ///     <c>attack * (1 - clamp01(defense))</c>.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Critical multiplier.</b> On a critical hit the mitigated value is multiplied by
    ///     <see cref="CombatConfig.CriticalHitMultiplier"/>. Applied AFTER mitigation, so a critical
    ///     hit multiplies what actually got through rather than what was swung.
    ///     TODO(design): the opposite order is equally defensible.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Damage floor.</b> The result is raised to <see cref="CombatConfig.MinimumDamage"/>,
    ///     which guarantees an attack never heals and never stalls a battle at zero damage.
    ///   </description></item>
    /// </list>
    /// <para>
    /// <b>Dice consumption is part of the contract.</b> Rolls are drawn in the order above and an
    /// evaded attack draws exactly one value while a critical hit draws three. Reordering or
    /// short-circuiting differently changes every subsequent draw in the battle and therefore
    /// changes the whole replay, even though no individual rule changed. Treat the roll order as
    /// load-bearing.
    /// </para>
    /// </remarks>
    public sealed class StandardDamageFormula : IDamageFormula
    {
        private readonly float criticalHitMultiplier;
        private readonly float minimumDamage;
        private readonly DamageMitigationMode mitigationMode;
        private readonly bool applyEvasionRoll;
        private readonly bool applyAccuracyRoll;

        /// <summary>
        /// Creates a formula bound to a configuration. The relevant values are copied, so later
        /// edits to <paramref name="config"/> cannot change a battle already in progress.
        /// </summary>
        /// <param name="config">Supplies the crit multiplier, damage floor, mitigation mode and roll toggles.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
        public StandardDamageFormula(CombatConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            criticalHitMultiplier = config.CriticalHitMultiplier;
            minimumDamage = config.MinimumDamage;
            mitigationMode = config.MitigationMode;
            applyEvasionRoll = config.ApplyEvasionRoll;
            applyAccuracyRoll = config.ApplyAccuracyRoll;
        }

        /// <summary>Multiplier applied to mitigated damage on a critical hit.</summary>
        public float CriticalHitMultiplier
        {
            get { return criticalHitMultiplier; }
        }

        /// <summary>Floor applied to any connecting attack.</summary>
        public float MinimumDamage
        {
            get { return minimumDamage; }
        }

        /// <summary>How Defense reduces Attack Damage.</summary>
        public DamageMitigationMode MitigationMode
        {
            get { return mitigationMode; }
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rng"/> is null.</exception>
        public DamageResult Compute(UnitStats attacker, UnitStats defender, DeterministicRandom rng)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            if (applyEvasionRoll && rng.Chance(defender.Evasion))
            {
                return DamageResult.Evaded();
            }

            if (applyAccuracyRoll && !rng.Chance(attacker.RangedAccuracy))
            {
                return DamageResult.Missed();
            }

            bool isCritical = rng.Chance(attacker.CriticalHitChance);

            float mitigated = Mitigate(attacker.AttackDamage, defender.Defense);

            if (isCritical)
            {
                mitigated *= criticalHitMultiplier;
            }

            float final = mitigated < minimumDamage ? minimumDamage : mitigated;

            // Belt and braces: a negative MinimumDamage is rejected by CombatConfig, but a caller
            // constructing this class by hand must still never be able to turn an attack into a heal.
            if (final < 0f)
            {
                final = 0f;
            }

            return DamageResult.Hit(final, isCritical);
        }

        /// <summary>Applies the configured mitigation mode. Result may be negative; the caller floors it.</summary>
        private float Mitigate(float attack, float defense)
        {
            if (mitigationMode == DamageMitigationMode.Multiplicative)
            {
                float reduction = defense;
                if (reduction < 0f)
                {
                    reduction = 0f;
                }
                else if (reduction > 1f)
                {
                    reduction = 1f;
                }

                return attack * (1f - reduction);
            }

            return attack - defense;
        }
    }
}
