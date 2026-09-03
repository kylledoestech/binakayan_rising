using System;
using System.Collections.Generic;
using UnityEngine;

namespace BinakayanRising.Data
{
    /// <summary>
    /// Which stat a <see cref="KapatiranStatDelta"/> operates on.
    /// </summary>
    /// <remarks>
    /// The first eight members mirror the stat block on <see cref="UnitData"/>.
    /// <see cref="HealingReceived"/> is bond-only: it is named by Capstone Table 3
    /// (Katipunero Vanguard &amp; Field Medic) but is not part of the base unit stat block.
    /// </remarks>
    public enum KapatiranStat
    {
        /// <summary>Unset. A delta left on this value applies to nothing.</summary>
        None = 0,

        /// <summary>Maximum hit points.</summary>
        MaxHP = 1,

        /// <summary>Attack damage.</summary>
        AttackDamage = 2,

        /// <summary>Defense.</summary>
        Defense = 3,

        /// <summary>Evasion.</summary>
        Evasion = 4,

        /// <summary>Ranged accuracy.</summary>
        RangedAccuracy = 5,

        /// <summary>Attack range, in grid cells.</summary>
        AttackRange = 6,

        /// <summary>Critical hit chance.</summary>
        CriticalHitChance = 7,

        /// <summary>Movement speed, in grid cells per AI turn.</summary>
        MovementSpeed = 8,

        /// <summary>
        /// Multiplier on healing this unit receives. Bond-only stat, named by Capstone Table 3.
        /// </summary>
        HealingReceived = 9
    }

    /// <summary>
    /// Whether a stat delta is expressed as a percentage of the base stat or as a flat amount.
    /// </summary>
    /// <remarks>
    /// Capstone Table 3 mixes the two: every bonus is a percentage <em>except</em> the rank A
    /// bonus on the Caviteño Marksman and Trench Engineer pair, which grants a flat
    /// "+1 Attack Range". Both forms must therefore be representable.
    /// </remarks>
    public enum StatDeltaMode
    {
        /// <summary>Value is a fraction of the base stat: <c>0.20</c> means "+20%".</summary>
        Percent = 0,

        /// <summary>Value is an absolute amount added to the stat: <c>1</c> means "+1".</summary>
        Flat = 1
    }

    /// <summary>
    /// A single stat change granted by a Kapatiran rank: which stat, whether the value is a
    /// percentage or a flat amount, and the value itself.
    /// </summary>
    [Serializable]
    public struct KapatiranStatDelta
    {
        [Tooltip("Stat this delta applies to.")]
        [SerializeField] private KapatiranStat stat;

        [Tooltip("Percent (0.20 == +20%) or Flat (1 == +1). Table 3 uses Flat only for +1 Attack Range.")]
        [SerializeField] private StatDeltaMode mode;

        [Tooltip("Signed magnitude. Percent mode expects a fraction, not 0-100.")]
        [SerializeField] private float value;

        /// <summary>Creates a stat delta.</summary>
        /// <param name="stat">Stat the delta applies to.</param>
        /// <param name="mode">Whether <paramref name="value"/> is a fraction or a flat amount.</param>
        /// <param name="value">Signed magnitude of the change.</param>
        public KapatiranStatDelta(KapatiranStat stat, StatDeltaMode mode, float value)
        {
            this.stat = stat;
            this.mode = mode;
            this.value = value;
        }

        /// <summary>Stat this delta applies to.</summary>
        public KapatiranStat Stat => stat;

        /// <summary>Whether <see cref="Value"/> is a fraction of the base stat or a flat amount.</summary>
        public StatDeltaMode Mode => mode;

        /// <summary>
        /// Signed magnitude. In <see cref="StatDeltaMode.Percent"/> mode this is a fraction
        /// (<c>0.15</c> == +15%); in <see cref="StatDeltaMode.Flat"/> mode it is an absolute amount.
        /// </summary>
        public float Value => value;
    }

    /// <summary>
    /// Everything one Kapatiran rank grants: the lore dialogue it unlocks and the stat deltas it
    /// adds. A rank may carry a dialogue, stat deltas, or both.
    /// </summary>
    [Serializable]
    public sealed class KapatiranRankEffect
    {
        [Tooltip("Identifier of the visual-novel dialogue unlocked at this rank. Empty = none. " +
                 "Capstone Table 3 names \"Lore Dialogue 1\" through \"Lore Dialogue 4\" at rank C.")]
        [SerializeField] private string loreDialogueId = string.Empty;

        [Tooltip("Stat changes granted at this rank. Capstone Table 3: rank C grants none, " +
                 "rank B grants one, rank A grants two.")]
        [SerializeField] private List<KapatiranStatDelta> statDeltas = new List<KapatiranStatDelta>();

        /// <summary>
        /// Identifier of the dialogue scene unlocked at this rank, or an empty string when the
        /// rank unlocks no dialogue.
        /// </summary>
        public string LoreDialogueId => loreDialogueId;

        /// <summary>True when this rank unlocks a lore dialogue.</summary>
        public bool HasLoreDialogue => !string.IsNullOrEmpty(loreDialogueId);

        /// <summary>Stat changes granted at this rank, in authoring order.</summary>
        public IReadOnlyList<KapatiranStatDelta> StatDeltas => statDeltas;
    }

    /// <summary>
    /// One Kapatiran (Brotherhood) pairing: the two units that bond, and the effects unlocked at
    /// each of the three support ranks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Transcribed from <b>Capstone Table 3: Kapatiran (Brotherhood) Synergy Levels</b>. Author one
    /// asset per row:
    /// </para>
    /// <list type="table">
    ///   <listheader>
    ///     <term>Character Pair</term>
    ///     <description>Rank C — Rank B — Rank A (Maximum)</description>
    ///   </listheader>
    ///   <item>
    ///     <term>Gen. Evangelista &amp; Emilio Aguinaldo</term>
    ///     <description>Unlocks Lore Dialogue 1 — +10% Attack Damage — +15% Attack Damage, +10% Defense.</description>
    ///   </item>
    ///   <item>
    ///     <term>Katipunero Vanguard &amp; Field Medic</term>
    ///     <description>Unlocks Lore Dialogue 2 — +15% Healing Received — +25% Healing Received, +5% Max HP.</description>
    ///   </item>
    ///   <item>
    ///     <term>Caviteño Marksman &amp; Trench Engineer</term>
    ///     <description>Unlocks Lore Dialogue 3 — +10% Ranged Accuracy — +20% Ranged Accuracy, +1 Attack Range.</description>
    ///   </item>
    ///   <item>
    ///     <term>Magdalo Infantry &amp; Magdiwang Infantry</term>
    ///     <description>Unlocks Lore Dialogue 4 — +5% Critical Hit Chance — +15% Critical Hit Chance, +10% Evasion.</description>
    ///   </item>
    /// </list>
    /// <para>
    /// Note that the rank A bonus on the Marksman pair, "+1 Attack Range", is a FLAT integer rather
    /// than a percentage — hence <see cref="StatDeltaMode"/>. Every other bonus in the table is a
    /// percentage.
    /// </para>
    /// <para>
    /// <b>TODO(design): the promotion thresholds for C -&gt; B -&gt; A are not specified.</b> The
    /// document only says that placing bonded units adjacent to each other "builds their Support
    /// Levels" and that "maximizing this meter" awards rank A. It gives no meter scale, no points
    /// per turn or per battle, and no cap. The three threshold fields below therefore ship at 0 and
    /// must be authored before the system can promote anything.
    /// </para>
    /// <para>
    /// <b>TODO(design): "adjacent" is never defined.</b> The document says bonds build when units
    /// are "adjacent to each other on the deployment grid" but does not say whether that means
    /// 4-way orthogonal neighbours, 8-way including diagonals, or any unit within some radius. The
    /// distinction changes deployment strategy materially, so this asset stores no adjacency rule
    /// and the gameplay layer must not hard-code one until the team decides.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "NewKapatiranBond", menuName = "Binakayan Rising/Kapatiran Bond", order = 2)]
    public sealed class KapatiranBondData : ScriptableObject
    {
        [Header("Pairing")]
        [Tooltip("First unit of the bonded pair.")]
        [SerializeField] private UnitData unitA;

        [Tooltip("Second unit of the bonded pair.")]
        [SerializeField] private UnitData unitB;

        [Header("Capstone Table 3: Kapatiran (Brotherhood) Synergy Levels")]
        [Tooltip("Rank C effect. Table 3 grants a lore dialogue and no stat deltas at this rank.")]
        [SerializeField] private KapatiranRankEffect rankC = new KapatiranRankEffect();

        [Tooltip("Rank B effect. Table 3 grants exactly one stat delta at this rank.")]
        [SerializeField] private KapatiranRankEffect rankB = new KapatiranRankEffect();

        [Tooltip("Rank A (Maximum) effect. Table 3 grants exactly two stat deltas at this rank.")]
        [SerializeField] private KapatiranRankEffect rankA = new KapatiranRankEffect();

        [Header("Promotion Thresholds")]
        // TODO(design): not specified in capstone document. No meter scale, accrual rate or cap is
        // given for the Support Level meter, so all three thresholds default to 0.
        [Tooltip("Support points required to reach rank C. TODO(design): unspecified in the document.")]
        [Min(0)]
        [SerializeField] private int pointsToRankC = 0;

        [Tooltip("Support points required to reach rank B. TODO(design): unspecified in the document.")]
        [Min(0)]
        [SerializeField] private int pointsToRankB = 0;

        [Tooltip("Support points required to reach rank A. TODO(design): unspecified in the document.")]
        [Min(0)]
        [SerializeField] private int pointsToRankA = 0;

        /// <summary>First unit of the bonded pair.</summary>
        public UnitData UnitA => unitA;

        /// <summary>Second unit of the bonded pair.</summary>
        public UnitData UnitB => unitB;

        /// <summary>Effects unlocked at rank C.</summary>
        public KapatiranRankEffect RankC => rankC;

        /// <summary>Effects unlocked at rank B.</summary>
        public KapatiranRankEffect RankB => rankB;

        /// <summary>Effects unlocked at rank A (maximum).</summary>
        public KapatiranRankEffect RankA => rankA;

        /// <summary>
        /// Support points required to reach rank C.
        /// TODO(design): not specified in the capstone document.
        /// </summary>
        public int PointsToRankC => pointsToRankC;

        /// <summary>
        /// Support points required to reach rank B.
        /// TODO(design): not specified in the capstone document.
        /// </summary>
        public int PointsToRankB => pointsToRankB;

        /// <summary>
        /// Support points required to reach rank A.
        /// TODO(design): not specified in the capstone document.
        /// </summary>
        public int PointsToRankA => pointsToRankA;

        /// <summary>
        /// Returns the effect block for a rank, or <c>null</c> for
        /// <see cref="KapatiranRank.None"/> and any unrecognised value.
        /// </summary>
        /// <param name="rank">Rank to look up.</param>
        public KapatiranRankEffect GetEffect(KapatiranRank rank)
        {
            switch (rank)
            {
                case KapatiranRank.C:
                    return rankC;
                case KapatiranRank.B:
                    return rankB;
                case KapatiranRank.A:
                    return rankA;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Support points required to reach a rank, or <c>0</c> for
        /// <see cref="KapatiranRank.None"/> and any unrecognised value.
        /// </summary>
        /// <param name="rank">Rank to look up.</param>
        public int GetPointsRequired(KapatiranRank rank)
        {
            switch (rank)
            {
                case KapatiranRank.C:
                    return pointsToRankC;
                case KapatiranRank.B:
                    return pointsToRankB;
                case KapatiranRank.A:
                    return pointsToRankA;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// True when this asset describes the bond between the two given units, in either order.
        /// </summary>
        /// <param name="first">One candidate unit.</param>
        /// <param name="second">The other candidate unit.</param>
        public bool Matches(UnitData first, UnitData second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            return (unitA == first && unitB == second) || (unitA == second && unitB == first);
        }
    }
}
