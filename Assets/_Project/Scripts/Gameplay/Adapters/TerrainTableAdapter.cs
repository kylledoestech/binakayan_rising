using System;
using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using BinakayanRising.Data;
using UnityEngine;

namespace BinakayanRising.Gameplay.Adapters
{
    /// <summary>
    /// Converts authored <see cref="TerrainModifierData"/> assets into the tables
    /// <see cref="TerrainModifierProvider"/> consumes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Capstone Table 2's numbers live in the assets, never here.</b> Core ships
    /// <see cref="TerrainModifierProvider.CreateCapstoneTable2ForTesting"/> as a clearly-labelled
    /// transcription for unit tests; shipping code must go through this adapter so that designers
    /// own the values. Nothing in this file contains a percentage.
    /// </para>
    /// <para>
    /// The mapping is mechanical: each asset field becomes one percentage
    /// <see cref="StatModifier"/> tagged <see cref="ModifierSource.Terrain"/>, and the HP
    /// regeneration fraction goes into the provider's separate regeneration table because it is
    /// applied per AI turn rather than folded into a stat. Zero-valued fields produce no modifier,
    /// so a Standard Grid asset yields an empty list instead of three no-op entries cluttering the
    /// event log.
    /// </para>
    /// <para>
    /// <see cref="TerrainModifierData.IsImpassable"/> is deliberately not converted: passability is
    /// a property of the grid, not a stat, and <c>BattleGrid</c> already owns that rule. The adapter
    /// warns when an asset's flag disagrees with what the grid does for that terrain, so the
    /// contradiction surfaces at load rather than mid-battle.
    /// </para>
    /// </remarks>
    public static class TerrainTableAdapter
    {
        private static readonly StatModifier[] NoModifiers = new StatModifier[0];

        /// <summary>
        /// Builds a ready-to-inject provider from a collection of authored terrain assets.
        /// </summary>
        /// <param name="assets">
        /// Terrain assets, ideally one per <see cref="TerrainType"/> member. Must not be null.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="assets"/> is null.</exception>
        public static TerrainModifierProvider CreateProvider(IReadOnlyList<TerrainModifierData> assets)
        {
            return new TerrainModifierProvider(BuildModifierTable(assets), BuildRegenTable(assets));
        }

        /// <summary>
        /// Builds the stat-modifier table keyed by terrain type.
        /// </summary>
        /// <param name="assets">Terrain assets. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="assets"/> is null.</exception>
        /// <remarks>
        /// Every <see cref="TerrainType"/> member gets a key, even when its asset is missing or grants
        /// nothing, so a lookup never depends on authoring completeness. A duplicate terrain type is
        /// reported and the first asset wins, because silently merging two tuning passes would make
        /// the resulting numbers untraceable.
        /// </remarks>
        public static IReadOnlyDictionary<TerrainType, IReadOnlyList<StatModifier>> BuildModifierTable(
            IReadOnlyList<TerrainModifierData> assets)
        {
            if (assets == null)
            {
                throw new ArgumentNullException(nameof(assets));
            }

            Dictionary<TerrainType, IReadOnlyList<StatModifier>> table =
                new Dictionary<TerrainType, IReadOnlyList<StatModifier>>();

            foreach (TerrainType terrain in AllTerrainTypes())
            {
                table[terrain] = NoModifiers;
            }

            HashSet<TerrainType> seen = new HashSet<TerrainType>();

            for (int i = 0; i < assets.Count; i++)
            {
                TerrainModifierData asset = assets[i];

                if (asset == null)
                {
                    Debug.LogWarning("Terrain modifier asset at index " + i + " is null. Entry ignored.");
                    continue;
                }

                if (!seen.Add(asset.TerrainType))
                {
                    Debug.LogWarning(
                        "Two TerrainModifierData assets both claim " + asset.TerrainType
                            + " ('" + asset.name + "' is the duplicate). The first asset wins; "
                            + "delete or retype one of them.");
                    continue;
                }

                WarnOnPassabilityMismatch(asset);
                table[asset.TerrainType] = ToModifiers(asset);
            }

            return table;
        }

        /// <summary>
        /// Builds the per-turn HP regeneration table keyed by terrain type.
        /// </summary>
        /// <param name="assets">Terrain assets. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="assets"/> is null.</exception>
        public static IReadOnlyDictionary<TerrainType, float> BuildRegenTable(
            IReadOnlyList<TerrainModifierData> assets)
        {
            if (assets == null)
            {
                throw new ArgumentNullException(nameof(assets));
            }

            Dictionary<TerrainType, float> table = new Dictionary<TerrainType, float>();
            HashSet<TerrainType> seen = new HashSet<TerrainType>();

            for (int i = 0; i < assets.Count; i++)
            {
                TerrainModifierData asset = assets[i];

                if (asset == null || !seen.Add(asset.TerrainType))
                {
                    continue;
                }

                if (asset.HPRegenPercentPerAITurn != 0f)
                {
                    table[asset.TerrainType] = asset.HPRegenPercentPerAITurn;
                }
            }

            return table;
        }

        /// <summary>
        /// Converts one terrain asset into the stat modifiers it grants. Returns an empty list when
        /// the asset leaves units on their base statistics.
        /// </summary>
        /// <param name="asset">Terrain asset. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="asset"/> is null.</exception>
        public static IReadOnlyList<StatModifier> ToModifiers(TerrainModifierData asset)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            string tag = ResolveSourceTag(asset);
            List<StatModifier> modifiers = new List<StatModifier>(3);

            if (asset.DefensePercent != 0f)
            {
                modifiers.Add(StatModifier.Percent(
                    StatKind.Defense, asset.DefensePercent, ModifierSource.Terrain, tag));
            }

            if (asset.EvasionPercent != 0f)
            {
                modifiers.Add(StatModifier.Percent(
                    StatKind.Evasion, asset.EvasionPercent, ModifierSource.Terrain, tag));
            }

            if (asset.MovementSpeedPercent != 0f)
            {
                modifiers.Add(StatModifier.Percent(
                    StatKind.MovementSpeed, asset.MovementSpeedPercent, ModifierSource.Terrain, tag));
            }

            if (modifiers.Count == 0)
            {
                return NoModifiers;
            }

            return modifiers;
        }

        /// <summary>
        /// The <see cref="StatModifier.SourceTag"/> written into the event log for this terrain.
        /// </summary>
        /// <param name="asset">Terrain asset. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="asset"/> is null.</exception>
        public static string ResolveSourceTag(TerrainModifierData asset)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (!string.IsNullOrEmpty(asset.DisplayName))
            {
                return asset.DisplayName;
            }

            return string.IsNullOrEmpty(asset.name) ? asset.TerrainType.ToString() : asset.name;
        }

        /// <summary>
        /// Reports terrain types with no asset in the supplied collection, so a loading screen can
        /// tell the team which Table 2 rows are still unauthored.
        /// </summary>
        /// <param name="assets">Terrain assets. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="assets"/> is null.</exception>
        public static IReadOnlyList<TerrainType> FindMissingTerrainTypes(
            IReadOnlyList<TerrainModifierData> assets)
        {
            if (assets == null)
            {
                throw new ArgumentNullException(nameof(assets));
            }

            HashSet<TerrainType> present = new HashSet<TerrainType>();

            for (int i = 0; i < assets.Count; i++)
            {
                if (assets[i] != null)
                {
                    present.Add(assets[i].TerrainType);
                }
            }

            List<TerrainType> missing = new List<TerrainType>();

            foreach (TerrainType terrain in AllTerrainTypes())
            {
                if (!present.Contains(terrain))
                {
                    missing.Add(terrain);
                }
            }

            return missing;
        }

        /// <summary>Every declared <see cref="TerrainType"/> member, in declaration order.</summary>
        public static IReadOnlyList<TerrainType> AllTerrainTypes()
        {
            return AllTerrainTypesCache;
        }

        private static readonly TerrainType[] AllTerrainTypesCache =
        {
            TerrainType.StandardGrid,
            TerrainType.Trench,
            TerrainType.CoastalShallows,
            TerrainType.BambooBarricade,
            TerrainType.EncampmentTent
        };

        /// <summary>
        /// Warns when an asset's impassability flag contradicts the grid's own passability rule.
        /// </summary>
        /// <remarks>
        /// <c>BattleGrid</c> is the single source of truth for passability and blocks exactly
        /// <see cref="TerrainType.BambooBarricade"/>. An asset that claims otherwise is an authoring
        /// mistake that would otherwise show up as a pathfinding bug nobody can reproduce.
        /// </remarks>
        private static void WarnOnPassabilityMismatch(TerrainModifierData asset)
        {
            bool gridBlocksIt = asset.TerrainType == TerrainType.BambooBarricade;

            if (asset.IsImpassable == gridBlocksIt)
            {
                return;
            }

            Debug.LogWarning(
                "TerrainModifierData '" + asset.name + "' marks " + asset.TerrainType
                    + " as " + (asset.IsImpassable ? "impassable" : "passable")
                    + ", but BattleGrid treats it as " + (gridBlocksIt ? "impassable" : "passable")
                    + ". Passability is owned by the grid, not by this asset, so the flag has no "
                    + "effect on the simulation. Fix the asset or raise it with design.");
        }
    }
}
