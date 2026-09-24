using System;
using System.Collections.Generic;
using BinakayanRising.Core.Combat;

namespace BinakayanRising.Core.Content
{
    /// <summary>
    /// A Kapatiran pair's support rank, Capstone Table 3: none yet, then C, B and A (maximum).
    /// </summary>
    /// <remarks>
    /// Mirrors <c>BinakayanRising.Data.KapatiranRank</c> value for value; Core cannot reference the
    /// Data assembly. Ordered by strength, so <c>rank &gt;= BondRank.B</c> reads "at least B".
    /// </remarks>
    public enum BondRank
    {
        /// <summary>The pair has not fought side by side yet.</summary>
        None = 0,

        /// <summary>Table 3: unlocks the pair's lore dialogue; no stat bonus.</summary>
        C = 1,

        /// <summary>Table 3: one stat bonus.</summary>
        B = 2,

        /// <summary>Table 3 maximum: two stat bonuses.</summary>
        A = 3
    }

    /// <summary>One of Table 3's four bonded pairs.</summary>
    public sealed class BondPair
    {
        /// <summary>Stable id, stored in the save.</summary>
        public readonly string Id;

        public readonly string ArchetypeA;
        public readonly string ArchetypeB;

        /// <summary>Table 3's lore dialogue number, 1 to 4, unlocked at rank C.</summary>
        public readonly int LoreNumber;

        public BondPair(string id, string archetypeA, string archetypeB, int loreNumber)
        {
            Id = id;
            ArchetypeA = archetypeA;
            ArchetypeB = archetypeB;
            LoreNumber = loreNumber;
        }

        /// <summary>True when the two archetypes are this pair, in either order.</summary>
        public bool Matches(string first, string second)
        {
            return (first == ArchetypeA && second == ArchetypeB) || (first == ArchetypeB && second == ArchetypeA);
        }

        /// <summary>The other half of the pair, or null when <paramref name="archetype"/> is in neither half.</summary>
        public string PartnerOf(string archetype)
        {
            return archetype == ArchetypeA ? ArchetypeB : (archetype == ArchetypeB ? ArchetypeA : null);
        }
    }

    /// <summary>
    /// Capstone Table 3 as data: the four Kapatiran pairs and each rank's modifiers, built as Core
    /// <see cref="KapatiranBond"/> objects so the battle and the offline tests use the same values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A higher rank <em>replaces</em> the lower one's modifiers rather than adding to them, the
    /// same reading <c>KapatiranBondAdapter</c> uses by default: rank A's "+15% Attack Damage,
    /// +10% Defense" is the whole bonus, not "+10% (B) +15% (A)". Rank C carries no modifier —
    /// Table 3 gives it only a lore dialogue — so <see cref="Create"/> returns null for it.
    /// </para>
    /// <para>
    /// Every bonus is a percentage of the unit's own stat, as in Table 3 and the terrain table:
    /// "+10% Evasion" on a 0.05 base gives 0.055, not 0.15. The one flat bonus is the Marksman and
    /// Engineer's "+1 Attack Range".
    /// </para>
    /// </remarks>
    public static class BondCatalog
    {
        /// <summary>Bond id of Gen. Evangelista and Emilio Aguinaldo (Table 3 row 1).</summary>
        public const string EvangelistaAguinaldo = "Evangelista_Aguinaldo";

        /// <summary>Bond id of the Katipunero Vanguard and Field Medic pair (row 2).</summary>
        public const string VanguardFieldMedic = "Vanguard_FieldMedic";

        /// <summary>Bond id of the Caviteño Marksman and Trench Engineer pair (row 3).</summary>
        public const string MarksmanEngineer = "Marksman_Engineer";

        /// <summary>Bond id of the Magdalo and Magdiwang infantry pair (row 4).</summary>
        public const string MagdaloMagdiwang = "Magdalo_Magdiwang";

        /// <summary>Rank label for rank C.</summary>
        public const string RankC = "C";

        /// <summary>Rank label for rank B.</summary>
        public const string RankB = "B";

        /// <summary>Rank label for rank A.</summary>
        public const string RankA = "A";

        private static readonly List<BondPair> pairs = new List<BondPair>
        {
            new BondPair(EvangelistaAguinaldo, UnitCatalog.Evangelista, UnitCatalog.Aguinaldo, 1),
            new BondPair(VanguardFieldMedic, UnitCatalog.Vanguard, UnitCatalog.FieldMedic, 2),
            new BondPair(MarksmanEngineer, UnitCatalog.Marksman, UnitCatalog.Engineer, 3),
            new BondPair(MagdaloMagdiwang, UnitCatalog.MagdaloInfantry, UnitCatalog.MagdiwangInfantry, 4)
        };

        /// <summary>The four pairs in Table 3's order.</summary>
        public static IReadOnlyList<BondPair> Pairs
        {
            get { return pairs; }
        }

        /// <summary>The pair with <paramref name="bondId"/>, or null.</summary>
        public static BondPair Find(string bondId)
        {
            for (int i = 0; i < pairs.Count; i++)
            {
                if (pairs[i].Id == bondId)
                {
                    return pairs[i];
                }
            }

            return null;
        }

        /// <summary>The pair <paramref name="archetype"/> belongs to, or null.</summary>
        public static BondPair PairOf(string archetype)
        {
            for (int i = 0; i < pairs.Count; i++)
            {
                if (pairs[i].PartnerOf(archetype) != null)
                {
                    return pairs[i];
                }
            }

            return null;
        }

        /// <summary>"C", "B", "A", or "-" for none.</summary>
        public static string Label(BondRank rank)
        {
            switch (rank)
            {
                case BondRank.C:
                    return RankC;
                case BondRank.B:
                    return RankB;
                case BondRank.A:
                    return RankA;
                default:
                    return "-";
            }
        }

        /// <summary>
        /// The bond at <paramref name="rank"/>, or null when that rank grants no stat bonus (none
        /// and C) or the id is unknown.
        /// </summary>
        public static KapatiranBond Create(string bondId, BondRank rank)
        {
            BondPair pair = Find(bondId);
            List<StatModifier> modifiers = Modifiers(bondId, rank);
            if (pair == null || modifiers == null)
            {
                return null;
            }

            return new KapatiranBond(pair.Id, pair.ArchetypeA, pair.ArchetypeB, modifiers, Label(rank));
        }

        /// <summary>
        /// Every pair at the rank <paramref name="rankOf"/> gives it, leaving out the pairs whose
        /// rank grants nothing. This is what a campaign battle resolves bonds from.
        /// </summary>
        public static List<KapatiranBond> AtRanks(Func<string, BondRank> rankOf)
        {
            var bonds = new List<KapatiranBond>();
            for (int i = 0; i < pairs.Count; i++)
            {
                KapatiranBond bond = Create(pairs[i].Id, rankOf != null ? rankOf(pairs[i].Id) : BondRank.None);
                if (bond != null)
                {
                    bonds.Add(bond);
                }
            }

            return bonds;
        }

        /// <summary>Every pair at rank A: the standalone playtest and the teaching battle.</summary>
        public static List<KapatiranBond> AllAtRankA()
        {
            return AtRanks(id => BondRank.A);
        }

        /// <summary>
        /// The Vanguard and Field Medic bond at rank A: +25% Healing Received and +5% Max HP.
        /// </summary>
        public static KapatiranBond VanguardAndMedic()
        {
            return Create(VanguardFieldMedic, BondRank.A);
        }

        /// <summary>
        /// The Magdalo and Magdiwang infantry bond. Rank B: +5% Critical Hit Chance. Rank A: +15%
        /// Critical Hit Chance and +10% Evasion.
        /// </summary>
        /// <param name="rank"><see cref="RankB"/> or <see cref="RankA"/>.</param>
        /// <exception cref="ArgumentException">Thrown for any other rank.</exception>
        public static KapatiranBond MagdaloAndMagdiwang(string rank)
        {
            if (string.Equals(rank, RankA, StringComparison.Ordinal))
            {
                return Create(MagdaloMagdiwang, BondRank.A);
            }

            if (string.Equals(rank, RankB, StringComparison.Ordinal))
            {
                return Create(MagdaloMagdiwang, BondRank.B);
            }

            throw new ArgumentException("The Magdalo and Magdiwang bond has only ranks B and A.", nameof(rank));
        }

        /// <summary>Table 3's modifiers for one pair at one rank, or null when it has none.</summary>
        private static List<StatModifier> Modifiers(string bondId, BondRank rank)
        {
            if (rank != BondRank.B && rank != BondRank.A)
            {
                return null;
            }

            bool a = rank == BondRank.A;
            switch (bondId)
            {
                case EvangelistaAguinaldo:
                    return a
                        ? List(Pct(StatKind.AttackDamage, 0.15f, "Evangelista + Aguinaldo"), Pct(StatKind.Defense, 0.10f, "Evangelista + Aguinaldo"))
                        : List(Pct(StatKind.AttackDamage, 0.10f, "Evangelista + Aguinaldo"));
                case VanguardFieldMedic:
                    return a
                        ? List(Pct(StatKind.HealingReceived, 0.25f, "Vanguard + Field Medic"), Pct(StatKind.MaxHP, 0.05f, "Vanguard + Field Medic"))
                        : List(Pct(StatKind.HealingReceived, 0.15f, "Vanguard + Field Medic"));
                case MarksmanEngineer:
                    return a
                        ? List(Pct(StatKind.RangedAccuracy, 0.20f, "Marksman + Engineer"),
                            StatModifier.Flat(StatKind.AttackRange, 1f, ModifierSource.Kapatiran, "Marksman + Engineer"))
                        : List(Pct(StatKind.RangedAccuracy, 0.10f, "Marksman + Engineer"));
                case MagdaloMagdiwang:
                    return a
                        ? List(Pct(StatKind.CriticalHitChance, 0.15f, "Magdalo + Magdiwang"), Pct(StatKind.Evasion, 0.10f, "Magdalo + Magdiwang"))
                        : List(Pct(StatKind.CriticalHitChance, 0.05f, "Magdalo + Magdiwang"));
                default:
                    return null;
            }
        }

        private static StatModifier Pct(StatKind stat, float percent, string source)
        {
            return StatModifier.Percent(stat, percent, ModifierSource.Kapatiran, source);
        }

        private static List<StatModifier> List(params StatModifier[] modifiers)
        {
            return new List<StatModifier>(modifiers);
        }
    }
}
