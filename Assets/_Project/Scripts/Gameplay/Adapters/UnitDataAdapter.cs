using System;
using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using BinakayanRising.Data;
using UnityEngine;

namespace BinakayanRising.Gameplay.Adapters
{
    /// <summary>
    /// Turns authored <see cref="UnitData"/> assets into the plain-C# <see cref="CombatUnit"/>
    /// objects the simulation runs on, and remembers which asset each runtime unit id came from so
    /// the presentation layer can find a sprite for a unit it only knows by number.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The event log the simulation produces identifies units by <see cref="CombatUnit.Id"/> and
    /// nothing else — that is what keeps <c>BinakayanRising.Core</c> free of engine types. The
    /// replayer therefore needs exactly one thing Core cannot give it: id to
    /// <see cref="UnitData.Portrait"/>. This class is that map, and it is populated as a side effect
    /// of building the roster, so the two can never drift apart.
    /// </para>
    /// <para>
    /// Ids are handed out by the adapter rather than authored, because <see cref="CombatUnit.Id"/>
    /// is the simulation's tie-breaker of last resort: it must be unique within a battle and stable
    /// across runs of the same seed. Allocating them in placement order satisfies both.
    /// </para>
    /// </remarks>
    public sealed class UnitDataAdapter
    {
        private readonly Dictionary<int, UnitData> sourcesById = new Dictionary<int, UnitData>();
        private readonly Dictionary<int, Team> teamsById = new Dictionary<int, Team>();
        private int nextUnitId;

        /// <summary>Creates an adapter that starts allocating ids at <paramref name="firstUnitId"/>.</summary>
        /// <param name="firstUnitId">
        /// First id handed out by <see cref="AllocateUnitId"/>. Defaults to 1 so that 0 stays free
        /// as an "unset" value in Inspector-facing code.
        /// </param>
        public UnitDataAdapter(int firstUnitId = 1)
        {
            nextUnitId = firstUnitId;
        }

        /// <summary>Every runtime unit id built by this adapter, mapped to its source asset.</summary>
        public IReadOnlyDictionary<int, UnitData> SourcesById
        {
            get { return sourcesById; }
        }

        /// <summary>How many units this adapter has built.</summary>
        public int Count
        {
            get { return sourcesById.Count; }
        }

        /// <summary>Reserves and returns the next unused runtime unit id.</summary>
        public int AllocateUnitId()
        {
            int id = nextUnitId;
            nextUnitId++;
            return id;
        }

        /// <summary>
        /// Converts an authored stat block into the immutable Core one.
        /// </summary>
        /// <param name="data">Source asset. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        /// <remarks>
        /// Parameter order below matches <see cref="UnitStats"/>, which matches the order the
        /// capstone document lists the stats in. <c>MaxHP</c> and <c>AttackRange</c> are authored as
        /// integers and widen to float here, which is what lets a Kapatiran "+1 Attack Range" and a
        /// terrain "-15% Movement Speed" be applied by the same modifier stack.
        /// </remarks>
        public static UnitStats ToUnitStats(UnitData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return new UnitStats(
                data.MaxHP,
                data.AttackDamage,
                data.Defense,
                data.Evasion,
                data.RangedAccuracy,
                data.AttackRange,
                data.CriticalHitChance,
                data.MovementSpeed);
        }

        /// <summary>
        /// The archetype identifier used to match Kapatiran bonds, derived from the asset.
        /// </summary>
        /// <param name="data">Source asset. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        /// <remarks>
        /// TODO(design): not specified in capstone document. <see cref="UnitData"/> carries a display
        /// name, a historical name and a blurb, but no stable archetype key, and Core matches bonds
        /// by <see cref="CombatUnit.ArchetypeId"/> string equality. The asset's own object name is
        /// used because it is unique within a folder, stable in source control, and independent of
        /// any localised display string. If design adds a real archetype field to
        /// <see cref="UnitData"/>, change this one method and nothing else moves.
        /// </remarks>
        public static string ResolveArchetypeId(UnitData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (!string.IsNullOrEmpty(data.name))
            {
                return data.name;
            }

            return string.IsNullOrEmpty(data.DisplayName) ? "Unknown" : data.DisplayName;
        }

        /// <summary>The name shown on the roster, falling back to the asset name when unauthored.</summary>
        /// <param name="data">Source asset. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        public static string ResolveDisplayName(UnitData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return string.IsNullOrEmpty(data.DisplayName) ? data.name : data.DisplayName;
        }

        /// <summary>
        /// Builds a runtime unit from an asset, allocating a fresh id and recording the reverse
        /// lookup.
        /// </summary>
        /// <param name="data">Source asset. Must not be null.</param>
        /// <param name="team">Side the unit fights for.</param>
        /// <param name="cell">Cell the unit is deployed on.</param>
        /// <param name="stackingPolicy">
        /// How this unit combines percentage modifiers. Pass the same value as
        /// <see cref="CombatConfig.StackingPolicy"/> so every unit in one battle resolves stats the
        /// same way.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        public CombatUnit CreateCombatUnit(
            UnitData data,
            Team team,
            GridCoord cell,
            ModifierStackingPolicy stackingPolicy = ModifierStackingPolicy.AdditivePercent)
        {
            return CreateCombatUnit(data, team, cell, AllocateUnitId(), stackingPolicy);
        }

        /// <summary>
        /// Builds a runtime unit from an asset using a caller-chosen id, and records the reverse
        /// lookup.
        /// </summary>
        /// <param name="data">Source asset. Must not be null.</param>
        /// <param name="team">Side the unit fights for.</param>
        /// <param name="cell">Cell the unit is deployed on.</param>
        /// <param name="unitId">Runtime id. Must be unique within one battle.</param>
        /// <param name="stackingPolicy">How this unit combines percentage modifiers.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="unitId"/> is already in use.</exception>
        public CombatUnit CreateCombatUnit(
            UnitData data,
            Team team,
            GridCoord cell,
            int unitId,
            ModifierStackingPolicy stackingPolicy = ModifierStackingPolicy.AdditivePercent)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (sourcesById.ContainsKey(unitId))
            {
                throw new ArgumentException(
                    "Unit id " + unitId + " has already been issued in this battle. Ids are the "
                        + "simulation's final tie-breaker and must be unique.",
                    nameof(unitId));
            }

            CombatUnit unit = new CombatUnit(
                unitId,
                ResolveDisplayName(data),
                ResolveArchetypeId(data),
                team,
                ToUnitStats(data),
                cell,
                stackingPolicy);

            sourcesById[unitId] = data;
            teamsById[unitId] = team;

            if (unitId >= nextUnitId)
            {
                nextUnitId = unitId + 1;
            }

            return unit;
        }

        /// <summary>Finds the asset a runtime unit was built from.</summary>
        /// <param name="unitId">Runtime unit id, as it appears in the event log.</param>
        /// <param name="data">Receives the source asset, or null when the id is unknown.</param>
        /// <returns>True when the id was issued by this adapter.</returns>
        public bool TryGetSource(int unitId, out UnitData data)
        {
            return sourcesById.TryGetValue(unitId, out data);
        }

        /// <summary>
        /// The battlefield sprite for a runtime unit, or null when the id is unknown or the asset
        /// has no portrait assigned.
        /// </summary>
        /// <param name="unitId">Runtime unit id, as it appears in the event log.</param>
        public Sprite GetSprite(int unitId)
        {
            UnitData data;
            return sourcesById.TryGetValue(unitId, out data) && data != null ? data.Portrait : null;
        }

        /// <summary>Finds the side a runtime unit fights for.</summary>
        /// <param name="unitId">Runtime unit id.</param>
        /// <param name="team">Receives the team, or <see cref="Team.Katipunan"/> when unknown.</param>
        /// <returns>True when the id was issued by this adapter.</returns>
        public bool TryGetTeam(int unitId, out Team team)
        {
            return teamsById.TryGetValue(unitId, out team);
        }

        /// <summary>Forgets every unit built so far, without resetting the id counter.</summary>
        /// <remarks>
        /// The counter is deliberately left alone: reusing an id that appears in an event log
        /// already handed to the replayer would make two different units answer to the same number.
        /// </remarks>
        public void Clear()
        {
            sourcesById.Clear();
            teamsById.Clear();
        }
    }
}
