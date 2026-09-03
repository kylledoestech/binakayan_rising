using UnityEngine;
using CoreTerrainType = BinakayanRising.Core.Grid.TerrainType;

namespace BinakayanRising.Data
{
    /// <summary>
    /// The mathematical modifier a single terrain type applies to any unit standing on it.
    /// One asset is authored per <see cref="CoreTerrainType"/> member.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Transcribed from <b>Capstone Table 2: Environmental Terrain Modifiers</b>:
    /// </para>
    /// <list type="table">
    ///   <listheader>
    ///     <term>Terrain Type</term>
    ///     <description>Visual Description — Mathematical Effect / Modifier</description>
    ///   </listheader>
    ///   <item>
    ///     <term>Standard Grid</term>
    ///     <description>Flat grass, dirt paths, or clearing — None (uses base unit statistics).</description>
    ///   </item>
    ///   <item>
    ///     <term>Evangelista's Trench</term>
    ///     <description>Dug-out earthworks with bamboo — +20% Defense, +15% Evasion.</description>
    ///   </item>
    ///   <item>
    ///     <term>Coastal Shallows</term>
    ///     <description>Water tiles near the Dalahican shore — -15% Movement Speed, -10% Defense.</description>
    ///   </item>
    ///   <item>
    ///     <term>Bamboo Barricade</term>
    ///     <description>Sharp wooden defensive structures — Impassable (blocks enemy pathfinding).</description>
    ///   </item>
    ///   <item>
    ///     <term>Encampment Tent</term>
    ///     <description>Safe zones at the rear of the grid — +5% HP Regeneration per AI turn.</description>
    ///   </item>
    /// </list>
    /// <para>
    /// Because the five rows carry five different value sets, the serialized defaults below are
    /// neutral (0 / false) rather than pre-filled: the documented number for each row is stated in
    /// that field's own doc comment and tooltip, and is entered per asset in the Inspector.
    /// </para>
    /// <para>
    /// <b>TODO(design): the capstone document does not state whether these modifiers are ADDITIVE
    /// or MULTIPLICATIVE</b>, nor how they stack with the Kapatiran percentage bonuses of Table 3.
    /// "+20% Defense" could mean <c>defense + baseDefense * 0.20</c> (additive on the base) or
    /// <c>defense * 1.20</c> applied after other multipliers — the two diverge as soon as a unit is
    /// affected by more than one source. This asset deliberately stores only the raw percentage
    /// deltas and takes NO position on how they combine; the combat layer must not assume one
    /// reading until the design team rules on it and this note is replaced.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "NewTerrainModifier", menuName = "Binakayan Rising/Terrain Modifier", order = 1)]
    public sealed class TerrainModifierData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("The terrain type this asset supplies modifiers for. One asset per enum member.")]
        [SerializeField] private CoreTerrainType terrainType = CoreTerrainType.StandardGrid;

        [Tooltip("Player-facing terrain name, e.g. \"Evangelista's Trench\".")]
        [SerializeField] private string displayName = string.Empty;

        [Tooltip("Visual description column of Capstone Table 2, e.g. \"Dug-out earthworks with bamboo\".")]
        [TextArea(2, 5)]
        [SerializeField] private string visualDescription = string.Empty;

        [Header("Capstone Table 2: Environmental Terrain Modifiers")]
        [Tooltip("Percentage delta applied to Defense. Table 2: Evangelista's Trench = +0.20, " +
                 "Coastal Shallows = -0.10, all other terrain = 0.")]
        [SerializeField] private float defensePercent = 0f;

        [Tooltip("Percentage delta applied to Evasion. Table 2: Evangelista's Trench = +0.15, " +
                 "all other terrain = 0.")]
        [SerializeField] private float evasionPercent = 0f;

        [Tooltip("Percentage delta applied to Movement Speed. Table 2: Coastal Shallows = -0.15, " +
                 "all other terrain = 0.")]
        [SerializeField] private float movementSpeedPercent = 0f;

        [Tooltip("Fraction of Max HP regenerated per AI turn while standing here. " +
                 "Table 2: Encampment Tent = +0.05, all other terrain = 0.")]
        [SerializeField] private float hpRegenPercentPerAITurn = 0f;

        [Tooltip("Table 2: Bamboo Barricade is impassable and blocks pathfinding. True for that " +
                 "asset only; false for every other terrain.")]
        [SerializeField] private bool isImpassable = false;

        /// <summary>The terrain type this asset supplies modifiers for.</summary>
        public CoreTerrainType TerrainType => terrainType;

        /// <summary>Player-facing terrain name.</summary>
        public string DisplayName => displayName;

        /// <summary>Visual description column of Capstone Table 2.</summary>
        public string VisualDescription => visualDescription;

        /// <summary>
        /// Percentage delta applied to Defense, as a fraction (0.20 == +20%).
        /// Capstone Table 2: Evangelista's Trench +20%, Coastal Shallows -10%, others none.
        /// </summary>
        public float DefensePercent => defensePercent;

        /// <summary>
        /// Percentage delta applied to Evasion, as a fraction (0.15 == +15%).
        /// Capstone Table 2: Evangelista's Trench +15%, others none.
        /// </summary>
        public float EvasionPercent => evasionPercent;

        /// <summary>
        /// Percentage delta applied to Movement Speed, as a fraction (-0.15 == -15%).
        /// Capstone Table 2: Coastal Shallows -15%, others none.
        /// </summary>
        public float MovementSpeedPercent => movementSpeedPercent;

        /// <summary>
        /// Fraction of Max HP regenerated per AI turn while a unit occupies this terrain
        /// (0.05 == +5%). Capstone Table 2: Encampment Tent +5% per AI turn, others none.
        /// </summary>
        public float HPRegenPercentPerAITurn => hpRegenPercentPerAITurn;

        /// <summary>
        /// True when the terrain blocks pathfinding entirely.
        /// Capstone Table 2: Bamboo Barricade is impassable; every other terrain is traversable.
        /// </summary>
        public bool IsImpassable => isImpassable;

        /// <summary>
        /// True when this terrain leaves the unit on its base statistics, i.e. no percentage delta
        /// and no regeneration. Capstone Table 2 describes Standard Grid this way.
        /// </summary>
        public bool HasNoStatModifiers =>
            Mathf.Approximately(defensePercent, 0f)
            && Mathf.Approximately(evasionPercent, 0f)
            && Mathf.Approximately(movementSpeedPercent, 0f)
            && Mathf.Approximately(hpRegenPercentPerAITurn, 0f);
    }
}
