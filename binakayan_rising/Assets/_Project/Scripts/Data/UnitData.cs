using UnityEngine;

namespace BinakayanRising.Data
{
    /// <summary>
    /// Authored definition of a single unit (playable Katipunero or Spanish AI opponent):
    /// its presentation data, its historical framing, and its base stat block.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stat names below are the ones the capstone document actually uses. They appear
    /// across Table 2 (Environmental Terrain Modifiers) and Table 3 (Kapatiran Synergy
    /// Levels), which modify Defense, Evasion, Movement Speed, HP regeneration, Attack
    /// Damage, Max HP, Ranged Accuracy, Attack Range, Critical Hit Chance and Healing
    /// Received.
    /// </para>
    /// <para>
    /// SOURCE NOTE: the capstone document specifies how these stats are <em>modified</em>
    /// but never publishes a base stat block, a damage formula, or a stat scale. Every
    /// numeric field here therefore ships at a neutral default and is tuned per asset in
    /// the Inspector. Nothing in this file is a balancing decision.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "NewUnit", menuName = "Binakayan Rising/Unit", order = 0)]
    public sealed class UnitData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Name shown on the roster and deployment UI.")]
        [SerializeField] private string displayName = string.Empty;

        [Tooltip("Name of the historical figure or unit archetype this asset represents.")]
        [SerializeField] private string historicalName = string.Empty;

        [Tooltip("Short educational blurb surfaced in the glossary and hero camp screens.")]
        [TextArea(3, 8)]
        [SerializeField] private string historicalBlurb = string.Empty;

        [Tooltip("Portrait / battlefield sprite for this unit.")]
        [SerializeField] private Sprite portrait;

        [Header("Base Stat Block")]
        // TODO(design): not specified in capstone document - no base stat values are given
        // anywhere in the manuscript. Every field in this block defaults to a neutral value
        // and MUST be authored per unit asset before balancing playtests.

        [Tooltip("Maximum hit points. TODO(design): unspecified in the capstone document.")]
        [Min(0)]
        [SerializeField] private int maxHP = 0;

        [Tooltip("Base attack damage before terrain and Kapatiran modifiers. TODO(design): unspecified.")]
        [Min(0f)]
        [SerializeField] private float attackDamage = 0f;

        [Tooltip("Base defense. Modified by Table 2 terrain and Table 3 bonds. TODO(design): unspecified.")]
        [Min(0f)]
        [SerializeField] private float defense = 0f;

        [Tooltip("Chance to avoid an incoming attack, 0..1. TODO(design): unspecified (scale also unconfirmed).")]
        [Range(0f, 1f)]
        [SerializeField] private float evasion = 0f;

        [Tooltip("Hit chance for ranged attacks, 0..1. TODO(design): unspecified (scale also unconfirmed).")]
        [Range(0f, 1f)]
        [SerializeField] private float rangedAccuracy = 0f;

        [Tooltip("Attack reach in grid cells. Kapatiran rank A can grant a flat +1. TODO(design): unspecified.")]
        [Min(0)]
        [SerializeField] private int attackRange = 0;

        [Tooltip("Chance to land a critical hit, 0..1. TODO(design): unspecified (scale also unconfirmed).")]
        [Range(0f, 1f)]
        [SerializeField] private float criticalHitChance = 0f;

        [Tooltip("Cells traversed per AI turn. TODO(design): unspecified (units also unconfirmed).")]
        [Min(0f)]
        [SerializeField] private float movementSpeed = 0f;

        /// <summary>Name shown on the roster and deployment UI.</summary>
        public string DisplayName => displayName;

        /// <summary>Name of the historical figure or unit archetype this asset represents.</summary>
        public string HistoricalName => historicalName;

        /// <summary>Short educational blurb surfaced in the glossary and hero camp screens.</summary>
        public string HistoricalBlurb => historicalBlurb;

        /// <summary>Portrait / battlefield sprite for this unit.</summary>
        public Sprite Portrait => portrait;

        /// <summary>Maximum hit points. Authored per asset; no value is given by the capstone document.</summary>
        public int MaxHP => maxHP;

        /// <summary>Base attack damage, before terrain (Table 2) and Kapatiran (Table 3) modifiers.</summary>
        public float AttackDamage => attackDamage;

        /// <summary>Base defense, before terrain (Table 2) and Kapatiran (Table 3) modifiers.</summary>
        public float Defense => defense;

        /// <summary>Base chance to avoid an incoming attack, expressed as a 0..1 fraction.</summary>
        public float Evasion => evasion;

        /// <summary>Base ranged hit chance, expressed as a 0..1 fraction.</summary>
        public float RangedAccuracy => rangedAccuracy;

        /// <summary>Base attack reach in grid cells.</summary>
        public int AttackRange => attackRange;

        /// <summary>Base critical hit chance, expressed as a 0..1 fraction.</summary>
        public float CriticalHitChance => criticalHitChance;

        /// <summary>Base movement speed in grid cells per AI turn.</summary>
        public float MovementSpeed => movementSpeed;
    }
}
