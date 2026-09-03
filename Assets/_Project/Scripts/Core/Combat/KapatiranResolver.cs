using System;
using System.Collections.Generic;
using BinakayanRising.Core.Grid;

namespace BinakayanRising.Core.Combat
{
    /// <summary>
    /// How close two bonded units must be for their Kapatiran bond to be active.
    /// </summary>
    /// <remarks>
    /// TODO(design): not specified in capstone document. The document says bonds build and apply
    /// when the two units are "adjacent to each other on the deployment grid" and never defines
    /// adjacency. Four-way, eight-way and "within N cells" are all consistent with the text and they
    /// produce materially different deployment strategies, so the rule is injected rather than
    /// assumed. <see cref="Orthogonal"/> is the default because it is the strictest reading of
    /// "adjacent".
    /// </remarks>
    public enum KapatiranProximityRule
    {
        /// <summary>Four-way adjacency: Manhattan distance of exactly 1.</summary>
        Orthogonal = 0,

        /// <summary>Eight-way adjacency: Chebyshev distance of exactly 1, diagonals included.</summary>
        Diagonal = 1,

        /// <summary>Within a Chebyshev radius, set by the resolver's radius parameter.</summary>
        Radius = 2
    }

    /// <summary>
    /// One Kapatiran (Brotherhood) pairing as the simulation sees it: two archetypes and the
    /// modifiers their bond currently grants.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Core deliberately does NOT model the C / B / A rank progression. The capstone document ties
    /// rank to a "Support Level" meter that builds across deployments — a metagame concern that
    /// lives in the save file, not in a single battle. By the time a battle starts the rank is
    /// already known, so the Data or Gameplay layer resolves it and hands Core only the modifier set
    /// that rank grants. <see cref="RankLabel"/> carries the rank through for the event log and has
    /// no effect on the simulation.
    /// </para>
    /// <para>
    /// This is why the rank A "+1 Attack Range" bonus needs no special case here: it arrives as an
    /// ordinary flat <see cref="StatModifier"/> alongside the percentage ones.
    /// </para>
    /// </remarks>
    public sealed class KapatiranBond
    {
        private static readonly StatModifier[] EmptyModifiers = new StatModifier[0];

        private readonly string bondId;
        private readonly string archetypeA;
        private readonly string archetypeB;
        private readonly string rankLabel;
        private readonly StatModifier[] modifiers;

        /// <summary>
        /// Creates a bond definition.
        /// </summary>
        /// <param name="bondId">Stable identifier, used for log attribution and tie-breaking.</param>
        /// <param name="archetypeA">First bonded archetype, matched against <see cref="CombatUnit.ArchetypeId"/>.</param>
        /// <param name="archetypeB">Second bonded archetype. May equal <paramref name="archetypeA"/> for a self-pairing bond.</param>
        /// <param name="modifiers">
        /// Modifiers granted to BOTH units while the bond is active. Copied on construction.
        /// </param>
        /// <param name="rankLabel">Optional rank name (<c>"C"</c>, <c>"B"</c>, <c>"A"</c>) for the log.</param>
        /// <exception cref="ArgumentException">Thrown when any of the three identifiers is null or empty.</exception>
        public KapatiranBond(
            string bondId,
            string archetypeA,
            string archetypeB,
            IReadOnlyList<StatModifier> modifiers,
            string rankLabel = null)
        {
            if (string.IsNullOrEmpty(bondId))
            {
                throw new ArgumentException("A bond needs a stable id.", nameof(bondId));
            }

            if (string.IsNullOrEmpty(archetypeA))
            {
                throw new ArgumentException("A bond needs its first archetype id.", nameof(archetypeA));
            }

            if (string.IsNullOrEmpty(archetypeB))
            {
                throw new ArgumentException("A bond needs its second archetype id.", nameof(archetypeB));
            }

            this.bondId = bondId;
            this.archetypeA = archetypeA;
            this.archetypeB = archetypeB;
            this.rankLabel = rankLabel ?? string.Empty;

            if (modifiers == null || modifiers.Count == 0)
            {
                this.modifiers = EmptyModifiers;
            }
            else
            {
                this.modifiers = new StatModifier[modifiers.Count];
                for (int i = 0; i < modifiers.Count; i++)
                {
                    this.modifiers[i] = modifiers[i];
                }
            }
        }

        /// <summary>Stable identifier for this bond.</summary>
        public string BondId
        {
            get { return bondId; }
        }

        /// <summary>First bonded archetype id.</summary>
        public string ArchetypeA
        {
            get { return archetypeA; }
        }

        /// <summary>Second bonded archetype id.</summary>
        public string ArchetypeB
        {
            get { return archetypeB; }
        }

        /// <summary>Rank name for the log. Never null; empty when unspecified.</summary>
        public string RankLabel
        {
            get { return rankLabel; }
        }

        /// <summary>Modifiers granted to both units while the bond is active.</summary>
        public IReadOnlyList<StatModifier> Modifiers
        {
            get { return modifiers; }
        }

        /// <summary>True when the two archetype ids form this pairing, in either order.</summary>
        /// <param name="firstArchetype">One archetype id.</param>
        /// <param name="secondArchetype">The other archetype id.</param>
        public bool Matches(string firstArchetype, string secondArchetype)
        {
            if (firstArchetype == null || secondArchetype == null)
            {
                return false;
            }

            return (string.Equals(archetypeA, firstArchetype, StringComparison.Ordinal)
                    && string.Equals(archetypeB, secondArchetype, StringComparison.Ordinal))
                || (string.Equals(archetypeA, secondArchetype, StringComparison.Ordinal)
                    && string.Equals(archetypeB, firstArchetype, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// One bond found to be active this turn: which bond, and the two units that triggered it.
    /// </summary>
    public readonly struct KapatiranActivation
    {
        private readonly KapatiranBond bond;
        private readonly CombatUnit unitA;
        private readonly CombatUnit unitB;

        /// <summary>Creates an activation record.</summary>
        /// <param name="bond">The bond that triggered.</param>
        /// <param name="unitA">The lower-id unit of the pair.</param>
        /// <param name="unitB">The higher-id unit of the pair.</param>
        public KapatiranActivation(KapatiranBond bond, CombatUnit unitA, CombatUnit unitB)
        {
            this.bond = bond;
            this.unitA = unitA;
            this.unitB = unitB;
        }

        /// <summary>The bond that triggered.</summary>
        public KapatiranBond Bond
        {
            get { return bond; }
        }

        /// <summary>The lower-id unit of the pair.</summary>
        public CombatUnit UnitA
        {
            get { return unitA; }
        }

        /// <summary>The higher-id unit of the pair.</summary>
        public CombatUnit UnitB
        {
            get { return unitB; }
        }

        /// <summary>Modifiers this activation grants to both units. Never null.</summary>
        public IReadOnlyList<StatModifier> Modifiers
        {
            get { return bond == null ? new StatModifier[0] : bond.Modifiers; }
        }
    }

    /// <summary>
    /// Finds which Kapatiran bonds are active given where the units are standing, and reports the
    /// modifiers they grant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bond definitions are injected. Nothing about the four pairings in Capstone Table 3 — Gen.
    /// Evangelista and Aguinaldo, the Katipunero Vanguard and Field Medic, the Caviteño Marksman and
    /// Trench Engineer, the Magdalo and Magdiwang Infantry — appears in Core. They are authored as
    /// <c>KapatiranBondData</c> assets and converted at the assembly boundary.
    /// </para>
    /// <para>
    /// The resolver is stateless and is re-run from scratch every AI turn, because units move and a
    /// bond that was active last turn may not be this turn. Results are produced in a fully
    /// deterministic order: units are visited in ascending id, pairs in ascending
    /// <c>(lower id, higher id)</c>, and bonds in the order they were injected.
    /// </para>
    /// </remarks>
    public sealed class KapatiranResolver
    {
        private readonly KapatiranBond[] bonds;
        private readonly KapatiranProximityRule proximityRule;
        private readonly int radius;
        private readonly bool requireSameTeam;

        /// <summary>
        /// Creates a resolver.
        /// </summary>
        /// <param name="bonds">Bond definitions. Null or empty means no bond can ever trigger.</param>
        /// <param name="proximityRule">
        /// How close bonded units must be. TODO(design): not specified in capstone document; see
        /// <see cref="KapatiranProximityRule"/>. Defaults to four-way adjacency.
        /// </param>
        /// <param name="radius">
        /// Chebyshev radius used only by <see cref="KapatiranProximityRule.Radius"/>. Must be at
        /// least 1.
        /// </param>
        /// <param name="requireSameTeam">
        /// When true (the default), a bond only triggers between units on the same side. The
        /// document frames Kapatiran as brotherhood among the Katipunan, so cross-team bonds are
        /// almost certainly nonsense, but the flag keeps the rule visible rather than buried.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="radius"/> is less than 1.</exception>
        public KapatiranResolver(
            IReadOnlyList<KapatiranBond> bonds,
            KapatiranProximityRule proximityRule = KapatiranProximityRule.Orthogonal,
            int radius = 1,
            bool requireSameTeam = true)
        {
            if (radius < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "Proximity radius must be at least 1.");
            }

            if (bonds == null || bonds.Count == 0)
            {
                this.bonds = new KapatiranBond[0];
            }
            else
            {
                List<KapatiranBond> copy = new List<KapatiranBond>(bonds.Count);
                for (int i = 0; i < bonds.Count; i++)
                {
                    if (bonds[i] != null)
                    {
                        copy.Add(bonds[i]);
                    }
                }

                this.bonds = copy.ToArray();
            }

            this.proximityRule = proximityRule;
            this.radius = radius;
            this.requireSameTeam = requireSameTeam;
        }

        /// <summary>Bond definitions this resolver knows about, in injection order.</summary>
        public IReadOnlyList<KapatiranBond> Bonds
        {
            get { return bonds; }
        }

        /// <summary>The proximity rule in force.</summary>
        public KapatiranProximityRule ProximityRule
        {
            get { return proximityRule; }
        }

        /// <summary>Chebyshev radius used by <see cref="KapatiranProximityRule.Radius"/>.</summary>
        public int Radius
        {
            get { return radius; }
        }

        /// <summary>A resolver with no bonds. Useful for isolating a test.</summary>
        public static KapatiranResolver CreateEmpty()
        {
            return new KapatiranResolver(new KapatiranBond[0]);
        }

        /// <summary>
        /// Returns true when two cells satisfy the configured proximity rule.
        /// </summary>
        /// <param name="a">First cell.</param>
        /// <param name="b">Second cell.</param>
        public bool IsInProximity(GridCoord a, GridCoord b)
        {
            switch (proximityRule)
            {
                case KapatiranProximityRule.Diagonal:
                    return GridDistance.Chebyshev(a, b) == 1;
                case KapatiranProximityRule.Radius:
                    int chebyshev = GridDistance.Chebyshev(a, b);
                    return chebyshev >= 1 && chebyshev <= radius;
                default:
                    return GridDistance.Manhattan(a, b) == 1;
            }
        }

        /// <summary>
        /// Finds every bond currently active among the given units.
        /// </summary>
        /// <param name="units">
        /// Units to consider. Dead units are skipped: a bond with a fallen brother grants nothing.
        /// </param>
        /// <param name="grid">
        /// The battle map. Not consulted by the default proximity rules — line of sight and terrain
        /// blocking are not mentioned anywhere in the document — but part of the signature so a
        /// future rule can use it without a breaking change.
        /// </param>
        /// <returns>Activations in a deterministic order. Never null.</returns>
        public IReadOnlyList<KapatiranActivation> Resolve(IReadOnlyList<CombatUnit> units, IBattleGrid grid)
        {
            List<KapatiranActivation> activations = new List<KapatiranActivation>();

            if (units == null || units.Count < 2 || bonds.Length == 0)
            {
                return activations;
            }

            List<CombatUnit> living = new List<CombatUnit>(units.Count);
            for (int i = 0; i < units.Count; i++)
            {
                CombatUnit unit = units[i];
                if (unit != null && unit.IsAlive)
                {
                    living.Add(unit);
                }
            }

            living.Sort(CompareById);

            for (int i = 0; i < living.Count; i++)
            {
                for (int j = i + 1; j < living.Count; j++)
                {
                    CombatUnit first = living[i];
                    CombatUnit second = living[j];

                    if (requireSameTeam && first.Team != second.Team)
                    {
                        continue;
                    }

                    if (!IsInProximity(first.Position, second.Position))
                    {
                        continue;
                    }

                    for (int b = 0; b < bonds.Length; b++)
                    {
                        KapatiranBond bond = bonds[b];
                        if (bond.Matches(first.ArchetypeId, second.ArchetypeId))
                        {
                            activations.Add(new KapatiranActivation(bond, first, second));
                        }
                    }
                }
            }

            return activations;
        }

        /// <summary>Orders units by id so that pair enumeration is reproducible.</summary>
        private static int CompareById(CombatUnit left, CombatUnit right)
        {
            return left.Id.CompareTo(right.Id);
        }
    }
}
