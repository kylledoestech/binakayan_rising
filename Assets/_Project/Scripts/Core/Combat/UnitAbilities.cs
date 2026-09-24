using System.Collections.Generic;
using BinakayanRising.Core.Grid;

namespace BinakayanRising.Core.Combat
{
    /// <summary>
    /// What a unit can do beyond moving and striking with its eight stats: an artillery piece's
    /// splash and reload, an officer's aura, a marine's footing in the shallows, a supply cart's
    /// refusal to fight.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kept out of <see cref="UnitStats"/> on purpose, like <see cref="CombatUnit.HealPower"/>:
    /// the stat block mirrors the capstone document's eight statistics exactly, and these are
    /// behaviours of particular archetypes, not stats every unit has. DESIGN-DECISIONS #19.
    /// </para>
    /// <para>
    /// Immutable, so one instance can be shared by every unit of an archetype. Every number here
    /// arrives from content (<c>UnitCatalog</c>); the simulator holds none of them.
    /// </para>
    /// </remarks>
    public sealed class UnitAbilities
    {
        private static readonly StatModifier[] NoModifiers = new StatModifier[0];

        /// <summary>No abilities. The default for every unit.</summary>
        public static readonly UnitAbilities None = new UnitAbilities(0f, 0, 0, null, false, TerrainType.StandardGrid, null, false);

        private readonly float splashFraction;
        private readonly int reloadTurns;
        private readonly int auraRadius;
        private readonly IReadOnlyList<StatModifier> auraModifiers;
        private readonly bool hasHomeTerrain;
        private readonly TerrainType homeTerrain;
        private readonly IReadOnlyList<StatModifier> homeTerrainModifiers;
        private readonly bool nonCombatant;

        /// <summary>Creates an ability set. Prefer the named factories.</summary>
        /// <param name="splashFraction">Share of a connecting hit dealt to each enemy orthogonally next to the target. 0 for none.</param>
        /// <param name="reloadTurns">Turns spent idle after every attack. 0 attacks every turn.</param>
        /// <param name="auraRadius">Reach of the aura in cells, not counting the unit itself. 0 for none.</param>
        /// <param name="auraModifiers">Modifiers granted to every ally inside the aura.</param>
        /// <param name="hasHomeTerrain">True when <paramref name="homeTerrain"/> is meaningful.</param>
        /// <param name="homeTerrain">Terrain whose penalties the unit ignores.</param>
        /// <param name="homeTerrainModifiers">Modifiers the unit gains while standing on its home terrain.</param>
        /// <param name="nonCombatant">True for a unit that never acts: it neither attacks, heals nor moves.</param>
        public UnitAbilities(
            float splashFraction,
            int reloadTurns,
            int auraRadius,
            IReadOnlyList<StatModifier> auraModifiers,
            bool hasHomeTerrain,
            TerrainType homeTerrain,
            IReadOnlyList<StatModifier> homeTerrainModifiers,
            bool nonCombatant)
        {
            this.splashFraction = splashFraction > 0f ? splashFraction : 0f;
            this.reloadTurns = reloadTurns > 0 ? reloadTurns : 0;
            this.auraRadius = auraRadius > 0 ? auraRadius : 0;
            this.auraModifiers = auraModifiers ?? NoModifiers;
            this.hasHomeTerrain = hasHomeTerrain;
            this.homeTerrain = homeTerrain;
            this.homeTerrainModifiers = homeTerrainModifiers ?? NoModifiers;
            this.nonCombatant = nonCombatant;
        }

        /// <summary>
        /// A gun: a connecting hit also lands <paramref name="splashFraction"/> of its damage on every
        /// enemy standing orthogonally next to the target, and the crew then spends
        /// <paramref name="reloadTurns"/> turns reloading.
        /// </summary>
        public static UnitAbilities Artillery(float splashFraction, int reloadTurns)
        {
            return new UnitAbilities(splashFraction, reloadTurns, 0, null, false, TerrainType.StandardGrid, null, false);
        }

        /// <summary>An officer: every ally within <paramref name="radius"/> cells gains <paramref name="modifiers"/>.</summary>
        public static UnitAbilities Officer(int radius, IReadOnlyList<StatModifier> modifiers)
        {
            return new UnitAbilities(0f, 0, radius, modifiers, false, TerrainType.StandardGrid, null, false);
        }

        /// <summary>
        /// A unit at home on <paramref name="terrain"/>: none of its penalties, and
        /// <paramref name="modifiers"/> while standing on it.
        /// </summary>
        public static UnitAbilities HomeTerrain(TerrainType terrain, IReadOnlyList<StatModifier> modifiers)
        {
            return new UnitAbilities(0f, 0, 0, null, true, terrain, modifiers, false);
        }

        /// <summary>Cargo: never acts. It can be attacked, healed and destroyed.</summary>
        public static UnitAbilities NonCombatantCargo()
        {
            return new UnitAbilities(0f, 0, 0, null, false, TerrainType.StandardGrid, null, true);
        }

        /// <summary>Share of a connecting hit splashed onto the target's orthogonal neighbours.</summary>
        public float SplashFraction
        {
            get { return splashFraction; }
        }

        /// <summary>Turns the unit spends idle after each attack.</summary>
        public int ReloadTurns
        {
            get { return reloadTurns; }
        }

        /// <summary>Aura reach in cells; 0 when the unit has no aura.</summary>
        public int AuraRadius
        {
            get { return auraRadius; }
        }

        /// <summary>What the aura grants each ally inside it.</summary>
        public IReadOnlyList<StatModifier> AuraModifiers
        {
            get { return auraModifiers; }
        }

        /// <summary>True when the unit has a home terrain.</summary>
        public bool HasHomeTerrain
        {
            get { return hasHomeTerrain; }
        }

        /// <summary>The terrain whose penalties the unit ignores. Meaningless unless <see cref="HasHomeTerrain"/>.</summary>
        public TerrainType Terrain
        {
            get { return homeTerrain; }
        }

        /// <summary>What the unit gains while on its home terrain.</summary>
        public IReadOnlyList<StatModifier> HomeTerrainModifiers
        {
            get { return homeTerrainModifiers; }
        }

        /// <summary>True for a unit that never takes an action.</summary>
        public bool NonCombatant
        {
            get { return nonCombatant; }
        }

        /// <summary>True when the unit radiates an aura.</summary>
        public bool HasAura
        {
            get { return auraRadius > 0 && auraModifiers.Count > 0; }
        }
    }
}
