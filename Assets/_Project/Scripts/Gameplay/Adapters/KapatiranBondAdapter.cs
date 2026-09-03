using System;
using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Data;
using UnityEngine;

namespace BinakayanRising.Gameplay.Adapters
{
    /// <summary>
    /// A bond asset paired with the rank the player has already earned for it. This is what the
    /// progression layer hands the adapter.
    /// </summary>
    /// <remarks>
    /// Core deliberately does not model C/B/A progression: a <see cref="KapatiranBond"/> carries an
    /// already-resolved modifier set and a rank <em>label</em> for the log, nothing more. Deciding
    /// which rank a pair has reached is save-data's job, and it happens before a battle starts.
    /// </remarks>
    public readonly struct KapatiranPairRank
    {
        private readonly KapatiranBondData bond;
        private readonly KapatiranRank rank;

        /// <summary>Creates a pairing of a bond asset with an earned rank.</summary>
        /// <param name="bond">Authored bond definition.</param>
        /// <param name="rank">Rank the player has earned for this pair.</param>
        public KapatiranPairRank(KapatiranBondData bond, KapatiranRank rank)
        {
            this.bond = bond;
            this.rank = rank;
        }

        /// <summary>Authored bond definition.</summary>
        public KapatiranBondData Bond
        {
            get { return bond; }
        }

        /// <summary>Rank the player has earned for this pair.</summary>
        public KapatiranRank Rank
        {
            get { return rank; }
        }
    }

    /// <summary>
    /// Converts <see cref="KapatiranBondData"/> assets plus a resolved
    /// <see cref="KapatiranRank"/> per pair into the Core <see cref="KapatiranBond"/> objects the
    /// simulation reasons about.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The split is the point. Table 3 describes three ranks whose effects accumulate as a pair is
    /// deployed together; that progression is player state, so it lives in save data and the Data
    /// assembly. By the time a battle starts the rank is already known, so Core only ever sees the
    /// flat list of modifiers that rank grants.
    /// </para>
    /// <para>
    /// Table 3's Katipunero Vanguard and Field Medic pair grants "+15% Healing Received", which maps
    /// to <see cref="StatKind.HealingReceived"/> — a bond-only stat that is not part of
    /// <see cref="UnitStats"/> and resolves against a base of 1.0.
    /// </para>
    /// <para>
    /// TODO(design): not specified in capstone document. The document does not say whether a rank's
    /// effects <em>replace</em> the lower ranks' or <em>add to</em> them. This adapter takes the
    /// asset at face value: it emits exactly the deltas authored on the earned rank and does not
    /// accumulate the ranks below it. Use <see cref="ToCoreBondCumulative"/> if design rules the
    /// other way; both readings are implemented so the decision is a one-line call-site change.
    /// </para>
    /// </remarks>
    public static class KapatiranBondAdapter
    {
        /// <summary>
        /// Maps the Data-side stat enum onto Core's. The two enums are numerically identical by
        /// design, but the conversion is written out so that renumbering one of them breaks the
        /// build here instead of silently mis-buffing a unit.
        /// </summary>
        /// <param name="stat">Authored stat identity.</param>
        /// <returns>The Core stat, or <see cref="StatKind.None"/> when the delta applies to nothing.</returns>
        public static StatKind ToStatKind(KapatiranStat stat)
        {
            switch (stat)
            {
                case KapatiranStat.MaxHP: return StatKind.MaxHP;
                case KapatiranStat.AttackDamage: return StatKind.AttackDamage;
                case KapatiranStat.Defense: return StatKind.Defense;
                case KapatiranStat.Evasion: return StatKind.Evasion;
                case KapatiranStat.RangedAccuracy: return StatKind.RangedAccuracy;
                case KapatiranStat.AttackRange: return StatKind.AttackRange;
                case KapatiranStat.CriticalHitChance: return StatKind.CriticalHitChance;
                case KapatiranStat.MovementSpeed: return StatKind.MovementSpeed;
                case KapatiranStat.HealingReceived: return StatKind.HealingReceived;
                case KapatiranStat.None: return StatKind.None;
                default: return StatKind.None;
            }
        }

        /// <summary>
        /// Converts one authored delta into a Core modifier tagged
        /// <see cref="ModifierSource.Kapatiran"/>.
        /// </summary>
        /// <param name="delta">Authored stat delta.</param>
        /// <param name="sourceTag">Bond identifier written into the event log.</param>
        /// <returns>
        /// The modifier, or null when the delta names no stat — an unset row in the Inspector.
        /// </returns>
        /// <remarks>
        /// <see cref="StatDeltaMode.Percent"/> becomes a percentage modifier and
        /// <see cref="StatDeltaMode.Flat"/> becomes a flat one. The distinction is load-bearing:
        /// Table 3's rank A bonus for the Caviteño Marksman and Trench Engineer is a flat
        /// "+1 Attack Range", and storing that as a percentage would read as +100%.
        /// </remarks>
        public static StatModifier? ToStatModifier(KapatiranStatDelta delta, string sourceTag)
        {
            StatKind stat = ToStatKind(delta.Stat);

            if (stat == StatKind.None)
            {
                return null;
            }

            if (delta.Mode == StatDeltaMode.Flat)
            {
                return StatModifier.Flat(stat, delta.Value, ModifierSource.Kapatiran, sourceTag);
            }

            return StatModifier.Percent(stat, delta.Value, ModifierSource.Kapatiran, sourceTag);
        }

        /// <summary>
        /// Converts a bond asset at a given rank into a Core bond carrying only that rank's deltas.
        /// </summary>
        /// <param name="data">Authored bond. Must not be null.</param>
        /// <param name="rank">Rank the pair has earned.</param>
        /// <returns>
        /// The Core bond, or null when the pair has not reached rank C or the asset is missing one
        /// of its two units.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        public static KapatiranBond ToCoreBond(KapatiranBondData data, KapatiranRank rank)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (rank == KapatiranRank.None)
            {
                return null;
            }

            if (!HasBothUnits(data))
            {
                return null;
            }

            string bondId = ResolveBondId(data);
            List<StatModifier> modifiers = new List<StatModifier>();
            AppendRank(modifiers, data.GetEffect(rank), bondId);

            return BuildBond(data, bondId, modifiers, rank);
        }

        /// <summary>
        /// Converts a bond asset at a given rank into a Core bond carrying the deltas of that rank
        /// <em>and every rank below it</em>.
        /// </summary>
        /// <param name="data">Authored bond. Must not be null.</param>
        /// <param name="rank">Highest rank the pair has earned.</param>
        /// <returns>The Core bond, or null when the pair has not reached rank C.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        /// <remarks>
        /// TODO(design): not specified in capstone document. Provided so that the cumulative reading
        /// of Table 3 can be adopted without touching anything else. See the type-level note.
        /// </remarks>
        public static KapatiranBond ToCoreBondCumulative(KapatiranBondData data, KapatiranRank rank)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (rank == KapatiranRank.None || !HasBothUnits(data))
            {
                return null;
            }

            string bondId = ResolveBondId(data);
            List<StatModifier> modifiers = new List<StatModifier>();

            if (rank >= KapatiranRank.C)
            {
                AppendRank(modifiers, data.RankC, bondId);
            }

            if (rank >= KapatiranRank.B)
            {
                AppendRank(modifiers, data.RankB, bondId);
            }

            if (rank >= KapatiranRank.A)
            {
                AppendRank(modifiers, data.RankA, bondId);
            }

            return BuildBond(data, bondId, modifiers, rank);
        }

        /// <summary>
        /// Converts a whole set of ranked pairs, dropping any that have not reached rank C.
        /// </summary>
        /// <param name="pairs">Bond assets with their earned ranks. Must not be null.</param>
        /// <param name="cumulative">
        /// When true, each bond carries every rank up to and including the earned one; when false
        /// (the default) it carries only the earned rank's deltas.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pairs"/> is null.</exception>
        public static List<KapatiranBond> ToCoreBonds(
            IReadOnlyList<KapatiranPairRank> pairs, bool cumulative = false)
        {
            if (pairs == null)
            {
                throw new ArgumentNullException(nameof(pairs));
            }

            List<KapatiranBond> bonds = new List<KapatiranBond>(pairs.Count);
            HashSet<string> seenIds = new HashSet<string>();

            for (int i = 0; i < pairs.Count; i++)
            {
                KapatiranBondData data = pairs[i].Bond;

                if (data == null)
                {
                    Debug.LogWarning("Kapatiran pair at index " + i + " has no bond asset. Entry ignored.");
                    continue;
                }

                KapatiranBond bond = cumulative
                    ? ToCoreBondCumulative(data, pairs[i].Rank)
                    : ToCoreBond(data, pairs[i].Rank);

                if (bond == null)
                {
                    continue;
                }

                if (!seenIds.Add(bond.BondId))
                {
                    Debug.LogWarning(
                        "Kapatiran bond id '" + bond.BondId + "' appears twice in this battle's bond "
                            + "list. The first entry wins; duplicate ids would make log attribution "
                            + "ambiguous.");
                    continue;
                }

                bonds.Add(bond);
            }

            return bonds;
        }

        /// <summary>
        /// Builds the resolver the simulator takes, from a set of ranked pairs.
        /// </summary>
        /// <param name="pairs">Bond assets with their earned ranks. Must not be null.</param>
        /// <param name="proximityRule">
        /// How close two bonded units must stand. TODO(design): not specified in capstone document —
        /// the document says only that bonded units must be deployed "adjacent" to one another.
        /// </param>
        /// <param name="radius">Chebyshev radius used by <see cref="KapatiranProximityRule.Radius"/>.</param>
        /// <param name="requireSameTeam">
        /// When true (the default), a bond only fires between units on the same side.
        /// </param>
        /// <param name="cumulative">Whether lower ranks stack; see <see cref="ToCoreBonds"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pairs"/> is null.</exception>
        public static KapatiranResolver CreateResolver(
            IReadOnlyList<KapatiranPairRank> pairs,
            KapatiranProximityRule proximityRule = KapatiranProximityRule.Orthogonal,
            int radius = 1,
            bool requireSameTeam = true,
            bool cumulative = false)
        {
            return new KapatiranResolver(
                ToCoreBonds(pairs, cumulative), proximityRule, radius, requireSameTeam);
        }

        /// <summary>
        /// The stable identifier written into the event log for a bond.
        /// </summary>
        /// <param name="data">Authored bond. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        /// <remarks>
        /// The asset's own object name is used, for the same reason
        /// <see cref="UnitDataAdapter.ResolveArchetypeId"/> uses it: unique within a folder, stable
        /// in source control, and independent of any localised string.
        /// </remarks>
        public static string ResolveBondId(KapatiranBondData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (!string.IsNullOrEmpty(data.name))
            {
                return data.name;
            }

            return UnitDataAdapter.ResolveArchetypeId(data.UnitA)
                + "_"
                + UnitDataAdapter.ResolveArchetypeId(data.UnitB);
        }

        private static KapatiranBond BuildBond(
            KapatiranBondData data, string bondId, List<StatModifier> modifiers, KapatiranRank rank)
        {
            return new KapatiranBond(
                bondId,
                UnitDataAdapter.ResolveArchetypeId(data.UnitA),
                UnitDataAdapter.ResolveArchetypeId(data.UnitB),
                modifiers,
                rank.ToString());
        }

        private static void AppendRank(
            List<StatModifier> destination, KapatiranRankEffect effect, string bondId)
        {
            if (effect == null)
            {
                return;
            }

            IReadOnlyList<KapatiranStatDelta> deltas = effect.StatDeltas;

            for (int i = 0; i < deltas.Count; i++)
            {
                StatModifier? modifier = ToStatModifier(deltas[i], bondId);

                if (modifier.HasValue)
                {
                    destination.Add(modifier.Value);
                }
            }
        }

        private static bool HasBothUnits(KapatiranBondData data)
        {
            if (data.UnitA != null && data.UnitB != null)
            {
                return true;
            }

            Debug.LogWarning(
                "KapatiranBondData '" + data.name + "' is missing one of its two units, so it can "
                    + "never match a pair. Bond ignored.");
            return false;
        }
    }
}
