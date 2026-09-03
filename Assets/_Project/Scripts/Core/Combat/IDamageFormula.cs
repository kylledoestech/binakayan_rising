using System.Globalization;

namespace BinakayanRising.Core.Combat
{
    /// <summary>
    /// The outcome of one attack resolution.
    /// </summary>
    /// <remarks>
    /// A miss and a dodge are reported separately even though both deal zero damage, because the
    /// presentation layer replays this log to animate the battle and the two need different feedback
    /// — a whiffed shot reads differently from a sidestep.
    /// </remarks>
    public readonly struct DamageResult
    {
        private readonly float amount;
        private readonly bool wasCrit;
        private readonly bool wasEvaded;
        private readonly bool wasMissed;

        /// <summary>Creates a result. Prefer the named factory methods.</summary>
        /// <param name="amount">Health removed from the defender. Never negative.</param>
        /// <param name="wasCrit">True when the critical hit roll succeeded.</param>
        /// <param name="wasEvaded">True when the defender dodged.</param>
        /// <param name="wasMissed">True when the attacker's accuracy roll failed.</param>
        public DamageResult(float amount, bool wasCrit, bool wasEvaded, bool wasMissed)
        {
            this.amount = amount < 0f ? 0f : amount;
            this.wasCrit = wasCrit;
            this.wasEvaded = wasEvaded;
            this.wasMissed = wasMissed;
        }

        /// <summary>Health removed from the defender. Never negative: an attack can never heal.</summary>
        public float Amount
        {
            get { return amount; }
        }

        /// <summary>True when the attack was a critical hit.</summary>
        public bool WasCrit
        {
            get { return wasCrit; }
        }

        /// <summary>True when the defender dodged the attack entirely.</summary>
        public bool WasEvaded
        {
            get { return wasEvaded; }
        }

        /// <summary>True when the attacker's accuracy roll failed.</summary>
        public bool WasMissed
        {
            get { return wasMissed; }
        }

        /// <summary>True when the attack connected, whether or not it was a critical hit.</summary>
        public bool Connected
        {
            get { return !wasEvaded && !wasMissed; }
        }

        /// <summary>A connecting hit for the given amount.</summary>
        /// <param name="amount">Health removed.</param>
        /// <param name="wasCrit">Whether the critical hit roll succeeded.</param>
        public static DamageResult Hit(float amount, bool wasCrit)
        {
            return new DamageResult(amount, wasCrit, false, false);
        }

        /// <summary>An attack the defender dodged.</summary>
        public static DamageResult Evaded()
        {
            return new DamageResult(0f, false, true, false);
        }

        /// <summary>An attack that failed its accuracy roll.</summary>
        public static DamageResult Missed()
        {
            return new DamageResult(0f, false, false, true);
        }

        /// <summary>Culture-invariant description, safe to embed in a deterministic log.</summary>
        public override string ToString()
        {
            if (wasEvaded)
            {
                return "evaded";
            }

            if (wasMissed)
            {
                return "missed";
            }

            return amount.ToString("0.####", CultureInfo.InvariantCulture) + (wasCrit ? " crit" : string.Empty);
        }
    }

    /// <summary>
    /// Turns an attacker and a defender into a <see cref="DamageResult"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exists as an interface because the capstone document supplies no damage formula at all.
    /// <see cref="StandardDamageFormula"/> is one defensible reading of the eight named statistics,
    /// not a specification, and the team must be able to swap it out without touching
    /// <see cref="BattleSimulator"/>.
    /// </para>
    /// <para>
    /// Implementations receive <em>effective</em> stats — terrain and Kapatiran modifiers are already
    /// resolved — so a formula never needs to know about the grid or about bonds.
    /// </para>
    /// <para>
    /// <b>Determinism contract.</b> An implementation may only draw randomness from the supplied
    /// <paramref name="rng"/>, must draw the same number of values for the same inputs, and must not
    /// read any ambient state (clock, static counters, thread id). Violating this breaks battle
    /// replay.
    /// </para>
    /// </remarks>
    public interface IDamageFormula
    {
        /// <summary>
        /// Resolves one attack.
        /// </summary>
        /// <param name="attacker">Attacker's effective stats, modifiers already applied.</param>
        /// <param name="defender">Defender's effective stats, modifiers already applied.</param>
        /// <param name="rng">The battle's generator. The only permitted source of randomness.</param>
        /// <returns>The damage dealt and how the attack resolved.</returns>
        DamageResult Compute(UnitStats attacker, UnitStats defender, DeterministicRandom rng);
    }
}
