using System;
using System.Collections.Generic;
using BinakayanRising.Core.Grid;

namespace BinakayanRising.Core.Combat
{
    /// <summary>
    /// Maps a <see cref="TerrainType"/> to the modifiers a unit standing on it receives, plus the
    /// fraction of Max HP it regenerates each AI turn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Core hardcodes no terrain percentages.</b> The tables are constructor-injected. In the
    /// shipped game they are built at the assembly boundary from the <c>TerrainModifierData</c>
    /// ScriptableObjects in <c>BinakayanRising.Data</c>, so designers tune terrain in the Inspector
    /// without a recompile and without a Core change.
    /// </para>
    /// <para>
    /// HP regeneration is carried separately from <see cref="StatModifier"/> because it is not a
    /// statistic: Capstone Table 2 describes the Encampment Tent as granting "+5% HP Regeneration
    /// per AI turn", which is a per-turn effect on current health rather than a change to any of the
    /// eight stats. Modelling it as a modifier on MaxHP would raise the ceiling instead of healing.
    /// </para>
    /// <para>
    /// Impassability is deliberately NOT handled here. <see cref="IBattleGrid.IsWalkable"/> is the
    /// single source of truth for whether a cell can be entered, and the simulator consults it
    /// directly; duplicating that rule in a modifier table would let the two disagree.
    /// </para>
    /// </remarks>
    public sealed class TerrainModifierProvider
    {
        private static readonly StatModifier[] EmptyModifiers = new StatModifier[0];

        private readonly Dictionary<TerrainType, IReadOnlyList<StatModifier>> modifiersByTerrain;
        private readonly Dictionary<TerrainType, float> regenByTerrain;

        /// <summary>
        /// Creates a provider from injected tables.
        /// </summary>
        /// <param name="modifiersByTerrain">
        /// Stat modifiers per terrain type. Missing keys and null lists mean "no modifiers". Copied
        /// on construction, so later edits to the caller's dictionary cannot change a running battle.
        /// </param>
        /// <param name="hpRegenPerTurn">
        /// Fraction of effective Max HP restored per AI turn per terrain type, as a fraction
        /// (<c>0.05f</c> == +5%). Missing keys mean no regeneration. Null is treated as empty.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="modifiersByTerrain"/> is null.</exception>
        public TerrainModifierProvider(
            IReadOnlyDictionary<TerrainType, IReadOnlyList<StatModifier>> modifiersByTerrain,
            IReadOnlyDictionary<TerrainType, float> hpRegenPerTurn = null)
        {
            if (modifiersByTerrain == null)
            {
                throw new ArgumentNullException(nameof(modifiersByTerrain));
            }

            this.modifiersByTerrain = new Dictionary<TerrainType, IReadOnlyList<StatModifier>>();
            foreach (KeyValuePair<TerrainType, IReadOnlyList<StatModifier>> entry in modifiersByTerrain)
            {
                this.modifiersByTerrain[entry.Key] = entry.Value ?? (IReadOnlyList<StatModifier>)EmptyModifiers;
            }

            regenByTerrain = new Dictionary<TerrainType, float>();
            if (hpRegenPerTurn != null)
            {
                foreach (KeyValuePair<TerrainType, float> entry in hpRegenPerTurn)
                {
                    regenByTerrain[entry.Key] = entry.Value;
                }
            }
        }

        /// <summary>
        /// Modifiers granted by a terrain type. Returns an empty list for terrain with no entry.
        /// </summary>
        /// <param name="terrain">Terrain to look up.</param>
        public IReadOnlyList<StatModifier> GetModifiers(TerrainType terrain)
        {
            IReadOnlyList<StatModifier> result;
            return modifiersByTerrain.TryGetValue(terrain, out result) ? result : EmptyModifiers;
        }

        /// <summary>
        /// Fraction of effective Max HP restored per AI turn on this terrain. Zero when the terrain
        /// has no entry.
        /// </summary>
        /// <param name="terrain">Terrain to look up.</param>
        public float GetHpRegenFraction(TerrainType terrain)
        {
            float result;
            return regenByTerrain.TryGetValue(terrain, out result) ? result : 0f;
        }

        /// <summary>A provider that grants nothing on any terrain. Useful for isolating a test.</summary>
        public static TerrainModifierProvider CreateEmpty()
        {
            return new TerrainModifierProvider(new Dictionary<TerrainType, IReadOnlyList<StatModifier>>());
        }

        /// <summary>
        /// Builds the exact values printed in Capstone Table 2 "Environmental Terrain Modifiers".
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>FOR TESTS AND PROTOTYPES ONLY. Do not call this from shipping code.</b> Production
        /// terrain values must come from the <c>TerrainModifierData</c> ScriptableObjects so that
        /// designers own them. This factory exists so that unit tests can assert against the
        /// document's numbers without depending on asset authoring, and so that the numbers appear
        /// in exactly one place in Core, clearly labelled as a transcription.
        /// </para>
        /// <para>Transcribed values:</para>
        /// <list type="bullet">
        ///   <item><description>Standard Grid — none.</description></item>
        ///   <item><description>Evangelista's Trench — +20% Defense, +15% Evasion.</description></item>
        ///   <item><description>Coastal Shallows — -15% Movement Speed, -10% Defense.</description></item>
        ///   <item><description>Bamboo Barricade — impassable; no stat modifiers (passability lives on the grid).</description></item>
        ///   <item><description>Encampment Tent — +5% HP regeneration per AI turn.</description></item>
        /// </list>
        /// </remarks>
        public static TerrainModifierProvider CreateCapstoneTable2ForTesting()
        {
            Dictionary<TerrainType, IReadOnlyList<StatModifier>> modifiers =
                new Dictionary<TerrainType, IReadOnlyList<StatModifier>>
                {
                    {
                        TerrainType.StandardGrid,
                        EmptyModifiers
                    },
                    {
                        TerrainType.Trench,
                        new[]
                        {
                            StatModifier.Percent(StatKind.Defense, 0.20f, ModifierSource.Terrain, "Trench"),
                            StatModifier.Percent(StatKind.Evasion, 0.15f, ModifierSource.Terrain, "Trench")
                        }
                    },
                    {
                        TerrainType.CoastalShallows,
                        new[]
                        {
                            StatModifier.Percent(StatKind.MovementSpeed, -0.15f, ModifierSource.Terrain, "CoastalShallows"),
                            StatModifier.Percent(StatKind.Defense, -0.10f, ModifierSource.Terrain, "CoastalShallows")
                        }
                    },
                    {
                        TerrainType.BambooBarricade,
                        EmptyModifiers
                    },
                    {
                        TerrainType.EncampmentTent,
                        EmptyModifiers
                    }
                };

            Dictionary<TerrainType, float> regen = new Dictionary<TerrainType, float>
            {
                { TerrainType.EncampmentTent, 0.05f }
            };

            return new TerrainModifierProvider(modifiers, regen);
        }
    }
}
