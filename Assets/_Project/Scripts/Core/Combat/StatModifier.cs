using System;
using System.Collections.Generic;
using System.Globalization;

namespace BinakayanRising.Core.Combat
{
    /// <summary>
    /// Identifies which statistic a <see cref="StatModifier"/> operates on.
    /// </summary>
    /// <remarks>
    /// The first eight members are exactly the stat block named by the capstone document and mirror
    /// <see cref="UnitStats"/> one-for-one. <see cref="HealingReceived"/> is a ninth, bond-only
    /// member: Capstone Table 3 grants "+15% / +25% Healing Received" to the Katipunero Vanguard and
    /// Field Medic pair, but healing received is not part of a unit's base stat block, so it has no
    /// slot in <see cref="UnitStats"/>. It is resolved against an implicit base of <c>1.0</c>
    /// (i.e. "100% of the incoming heal") wherever healing is applied.
    /// <para>
    /// Values are assigned explicitly because modifiers may end up serialised into a saved battle
    /// or a replay log; never renumber an existing member.
    /// </para>
    /// </remarks>
    public enum StatKind
    {
        /// <summary>Unset. A modifier left on this value applies to nothing and is rejected.</summary>
        None = 0,

        /// <summary>Maximum hit points.</summary>
        MaxHP = 1,

        /// <summary>Attack damage before mitigation.</summary>
        AttackDamage = 2,

        /// <summary>Defense, consumed by the damage formula's mitigation step.</summary>
        Defense = 3,

        /// <summary>Chance to avoid an incoming attack outright, as a 0..1 fraction.</summary>
        Evasion = 4,

        /// <summary>Chance for an attack to connect, as a 0..1 fraction.</summary>
        RangedAccuracy = 5,

        /// <summary>Attack reach in grid cells. Capstone Table 3 rank A grants a FLAT +1 here.</summary>
        AttackRange = 6,

        /// <summary>Chance to land a critical hit, as a 0..1 fraction.</summary>
        CriticalHitChance = 7,

        /// <summary>Cells the unit may traverse per AI turn.</summary>
        MovementSpeed = 8,

        /// <summary>
        /// Multiplier on healing this unit receives. Bond-only: named by Capstone Table 3 but not
        /// part of <see cref="UnitStats"/>. Resolved against a base of <c>1.0</c>.
        /// </summary>
        HealingReceived = 9
    }

    /// <summary>
    /// Where a <see cref="StatModifier"/> came from. Used for event-log attribution and for
    /// selectively clearing one class of modifier without disturbing the others.
    /// </summary>
    public enum ModifierSource
    {
        /// <summary>Origin not recorded.</summary>
        Unknown = 0,

        /// <summary>Granted by the terrain the unit is standing on (Capstone Table 2).</summary>
        Terrain = 1,

        /// <summary>Granted by a Kapatiran (Brotherhood) bond in proximity (Capstone Table 3).</summary>
        Kapatiran = 2,

        /// <summary>Granted by a correct answer in the educational quiz module (Capstone Table 4).</summary>
        Quiz = 3,

        /// <summary>Granted by a unit ability or a scripted mission event.</summary>
        Ability = 4
    }

    /// <summary>
    /// A single buff or debuff: a percentage delta, a flat delta, or both, applied to one stat.
    /// </summary>
    /// <remarks>
    /// Both forms are required because the capstone document mixes them. Every modifier in Table 2
    /// and almost every modifier in Table 3 is a percentage ("+20% Defense", "+15% Evasion"), but
    /// the rank A bonus on the Caviteño Marksman and Trench Engineer pair is a flat "+1 Attack
    /// Range". Storing a flat +1 as a percentage would silently turn it into +100%.
    /// <para>
    /// <see cref="PercentDelta"/> is a fraction, not a 0..100 number: <c>0.20f</c> means "+20%".
    /// </para>
    /// </remarks>
    public readonly struct StatModifier
    {
        private readonly StatKind stat;
        private readonly float percentDelta;
        private readonly float flatDelta;
        private readonly ModifierSource source;
        private readonly string sourceTag;

        /// <summary>
        /// Creates a modifier carrying any combination of percentage and flat delta.
        /// </summary>
        /// <param name="stat">Stat affected. Must not be <see cref="StatKind.None"/>.</param>
        /// <param name="percentDelta">Fraction of the base stat to add. <c>0.20f</c> == +20%.</param>
        /// <param name="flatDelta">Absolute amount to add after percentages resolve.</param>
        /// <param name="source">Classification used for log attribution and selective clearing.</param>
        /// <param name="sourceTag">
        /// Free-form identifier of the specific source, e.g. <c>"Trench"</c> or a bond id. Null is
        /// normalised to the empty string so that log output is stable.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="stat"/> is <see cref="StatKind.None"/>.</exception>
        public StatModifier(StatKind stat, float percentDelta, float flatDelta, ModifierSource source, string sourceTag)
        {
            if (stat == StatKind.None)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stat), stat, "A stat modifier must name a stat; StatKind.None is not a valid target.");
            }

            this.stat = stat;
            this.percentDelta = percentDelta;
            this.flatDelta = flatDelta;
            this.source = source;
            this.sourceTag = sourceTag ?? string.Empty;
        }

        /// <summary>Stat this modifier affects.</summary>
        public StatKind Stat
        {
            get { return stat; }
        }

        /// <summary>Fraction of the base stat added by this modifier. <c>0.20f</c> == +20%.</summary>
        public float PercentDelta
        {
            get { return percentDelta; }
        }

        /// <summary>Absolute amount added by this modifier, applied after percentages resolve.</summary>
        public float FlatDelta
        {
            get { return flatDelta; }
        }

        /// <summary>Classification of where this modifier came from.</summary>
        public ModifierSource Source
        {
            get { return source; }
        }

        /// <summary>Identifier of the specific source, e.g. a terrain name or a bond id. Never null.</summary>
        public string SourceTag
        {
            get { return sourceTag ?? string.Empty; }
        }

        /// <summary>True when this modifier contributes a non-zero percentage.</summary>
        public bool HasPercent
        {
            get { return percentDelta != 0f; }
        }

        /// <summary>True when this modifier contributes a non-zero flat amount.</summary>
        public bool HasFlat
        {
            get { return flatDelta != 0f; }
        }

        /// <summary>Creates a pure percentage modifier. <c>0.15f</c> means "+15%".</summary>
        /// <param name="stat">Stat affected.</param>
        /// <param name="percentDelta">Fraction of the base stat to add.</param>
        /// <param name="source">Classification used for log attribution.</param>
        /// <param name="sourceTag">Optional identifier of the specific source.</param>
        public static StatModifier Percent(StatKind stat, float percentDelta, ModifierSource source, string sourceTag = null)
        {
            return new StatModifier(stat, percentDelta, 0f, source, sourceTag);
        }

        /// <summary>
        /// Creates a pure flat modifier. This is the form Capstone Table 3 rank A needs for its
        /// "+1 Attack Range" bonus.
        /// </summary>
        /// <param name="stat">Stat affected.</param>
        /// <param name="flatDelta">Absolute amount to add.</param>
        /// <param name="source">Classification used for log attribution.</param>
        /// <param name="sourceTag">Optional identifier of the specific source.</param>
        public static StatModifier Flat(StatKind stat, float flatDelta, ModifierSource source, string sourceTag = null)
        {
            return new StatModifier(stat, 0f, flatDelta, source, sourceTag);
        }

        /// <summary>
        /// Stable, culture-invariant description used in the battle event log. Log output must be
        /// byte-identical across machines, so this never uses the ambient culture.
        /// </summary>
        public override string ToString()
        {
            return stat
                + "[" + source + ":" + SourceTag + "]"
                + " pct=" + percentDelta.ToString("0.####", CultureInfo.InvariantCulture)
                + " flat=" + flatDelta.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// How multiple percentage modifiers on the same stat combine.
    /// </summary>
    /// <remarks>
    /// TODO(design): not specified in capstone document. Neither Table 2 (terrain) nor Table 3
    /// (Kapatiran) says whether "+20% Defense" and "+10% Defense" produce +30% or +32%. The two
    /// readings diverge the moment a unit is affected by more than one source, which happens as
    /// soon as a bonded pair stands in a trench. <see cref="AdditivePercent"/> is the default
    /// because it is the easier of the two for a player to reason about, NOT because the document
    /// asked for it. The design team must ratify this.
    /// </remarks>
    public enum ModifierStackingPolicy
    {
        /// <summary>
        /// Sum every percentage, apply the sum once, then add flats:
        /// <c>final = base * (1 + p1 + p2 + ...) + f1 + f2 + ...</c>.
        /// </summary>
        AdditivePercent = 0,

        /// <summary>
        /// Multiply the percentages together, then add flats:
        /// <c>final = base * (1 + p1) * (1 + p2) * ... + f1 + f2 + ...</c>.
        /// </summary>
        MultiplicativePercent = 1
    }

    /// <summary>
    /// A mutable collection of <see cref="StatModifier"/>s that resolves a final value for any stat.
    /// One instance lives on each <see cref="CombatUnit"/> and is rebuilt from scratch at the start
    /// of every AI turn from the unit's current terrain and Kapatiran proximity.
    /// </summary>
    /// <remarks>
    /// Resolution is intentionally allocation-free and branch-simple: it is called several times per
    /// unit per turn and must never introduce a source of non-determinism. Modifiers are stored and
    /// enumerated in insertion order, and the simulator always inserts in a deterministic order, so
    /// even the floating-point rounding of the accumulated sum is reproducible.
    /// </remarks>
    public sealed class ModifierStack
    {
        private readonly List<StatModifier> modifiers = new List<StatModifier>();
        private readonly ModifierStackingPolicy policy;

        /// <summary>
        /// Creates an empty stack.
        /// </summary>
        /// <param name="policy">
        /// How percentages combine. Defaults to <see cref="ModifierStackingPolicy.AdditivePercent"/>;
        /// see that member for the TODO(design) note.
        /// </param>
        public ModifierStack(ModifierStackingPolicy policy = ModifierStackingPolicy.AdditivePercent)
        {
            this.policy = policy;
        }

        /// <summary>How percentages on this stack combine.</summary>
        public ModifierStackingPolicy Policy
        {
            get { return policy; }
        }

        /// <summary>Number of modifiers currently held.</summary>
        public int Count
        {
            get { return modifiers.Count; }
        }

        /// <summary>The modifiers, in insertion order. Exposed for the event log and for tests.</summary>
        public IReadOnlyList<StatModifier> Modifiers
        {
            get { return modifiers; }
        }

        /// <summary>Appends a modifier.</summary>
        /// <param name="modifier">The modifier to add.</param>
        public void Add(StatModifier modifier)
        {
            modifiers.Add(modifier);
        }

        /// <summary>Appends every modifier in <paramref name="source"/>, preserving its order.</summary>
        /// <param name="source">Modifiers to add. Null is treated as empty.</param>
        public void AddRange(IReadOnlyList<StatModifier> source)
        {
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                modifiers.Add(source[i]);
            }
        }

        /// <summary>Removes every modifier.</summary>
        public void Clear()
        {
            modifiers.Clear();
        }

        /// <summary>
        /// Removes every modifier whose <see cref="StatModifier.Source"/> matches, leaving the rest
        /// untouched. Used when one class of modifier (terrain, say) is recomputed independently.
        /// </summary>
        /// <param name="source">Source classification to strip.</param>
        public void ClearSource(ModifierSource source)
        {
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                if (modifiers[i].Source == source)
                {
                    modifiers.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Resolves the final value of one stat.
        /// </summary>
        /// <param name="stat">Stat to resolve. <see cref="StatKind.None"/> returns the base value unchanged.</param>
        /// <param name="baseValue">
        /// The unmodified stat value. For <see cref="StatKind.HealingReceived"/> callers pass
        /// <c>1.0f</c>, since that stat has no slot in <see cref="UnitStats"/>.
        /// </param>
        /// <returns>
        /// Under <see cref="ModifierStackingPolicy.AdditivePercent"/>,
        /// <c>baseValue * (1 + sum of percents) + sum of flats</c>. Under
        /// <see cref="ModifierStackingPolicy.MultiplicativePercent"/>,
        /// <c>baseValue * product of (1 + percent) + sum of flats</c>.
        /// </returns>
        public float Resolve(StatKind stat, float baseValue)
        {
            if (stat == StatKind.None)
            {
                return baseValue;
            }

            float percentAccumulator = policy == ModifierStackingPolicy.MultiplicativePercent ? 1f : 0f;
            float flatSum = 0f;

            for (int i = 0; i < modifiers.Count; i++)
            {
                StatModifier modifier = modifiers[i];
                if (modifier.Stat != stat)
                {
                    continue;
                }

                if (policy == ModifierStackingPolicy.MultiplicativePercent)
                {
                    percentAccumulator *= 1f + modifier.PercentDelta;
                }
                else
                {
                    percentAccumulator += modifier.PercentDelta;
                }

                flatSum += modifier.FlatDelta;
            }

            float scale = policy == ModifierStackingPolicy.MultiplicativePercent
                ? percentAccumulator
                : 1f + percentAccumulator;

            return (baseValue * scale) + flatSum;
        }
    }
}
