using System;
using BinakayanRising.Core.Grid;

namespace BinakayanRising.Core.Combat
{
    /// <summary>
    /// Which side of the Battle of Binakayan-Dalahican a unit fights for.
    /// </summary>
    /// <remarks>
    /// The capstone document frames victory as "all enemy units routed" and defeat as the player's
    /// deployed roster being "completely defeated", so exactly two sides are needed:
    /// <see cref="Katipunan"/> is always the player and <see cref="Spanish"/> is always the AI.
    /// </remarks>
    public enum Team
    {
        /// <summary>The player's Katipunero forces under Aguinaldo and Evangelista.</summary>
        Katipunan = 0,

        /// <summary>The Spanish colonial force. Always AI-controlled.</summary>
        Spanish = 1
    }

    /// <summary>
    /// The mutable runtime state of one unit inside a running battle: who it is, where it stands,
    /// how much health it has left, and which modifiers are currently acting on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="CombatUnit"/> is created once per battle from an immutable <see cref="UnitStats"/>
    /// block and is then mutated turn by turn by the <see cref="BattleSimulator"/>. Nothing else
    /// should mutate it while a battle is running; the presentation layer reads the event log
    /// instead.
    /// </para>
    /// <para>
    /// <see cref="Id"/> is the simulation's tie-breaker of last resort. Every ordering decision in
    /// the resolver — acting order, target selection ties, bond pairing — ultimately falls back to
    /// it, so ids must be unique within a battle and stable across runs.
    /// </para>
    /// </remarks>
    public sealed class CombatUnit
    {
        private readonly int id;
        private readonly string name;
        private readonly string archetypeId;
        private readonly Team team;
        private readonly UnitStats baseStats;
        private readonly ModifierStack modifiers;

        private float currentHP;
        private GridCoord position;
        private bool alive;
        private float movementCarry;
        private float healPower;

        /// <summary>
        /// Creates a unit at full health.
        /// </summary>
        /// <param name="id">Unique, stable identifier within one battle. Used as the deterministic tie-breaker.</param>
        /// <param name="name">Display name, used only for log readability.</param>
        /// <param name="archetypeId">
        /// Identifier of the unit archetype, e.g. <c>"CavitenoMarksman"</c>. This is what
        /// <see cref="KapatiranResolver"/> matches bonds against, so two copies of the same
        /// archetype share it while their <see cref="Id"/>s differ. Null falls back to
        /// <paramref name="name"/>.
        /// </param>
        /// <param name="team">Side this unit fights for.</param>
        /// <param name="baseStats">Unmodified stat block, typically converted from a UnitData asset.</param>
        /// <param name="position">Starting cell, normally chosen by the player during the deployment phase.</param>
        /// <param name="stackingPolicy">
        /// How percentage modifiers on this unit combine. Supplied by <see cref="CombatConfig"/> so
        /// that every unit in one battle resolves stats the same way.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        public CombatUnit(
            int id,
            string name,
            string archetypeId,
            Team team,
            UnitStats baseStats,
            GridCoord position,
            ModifierStackingPolicy stackingPolicy = ModifierStackingPolicy.AdditivePercent)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            this.id = id;
            this.name = name;
            this.archetypeId = string.IsNullOrEmpty(archetypeId) ? name : archetypeId;
            this.team = team;
            this.baseStats = baseStats;
            this.position = position;

            modifiers = new ModifierStack(stackingPolicy);
            currentHP = baseStats.MaxHP;
            alive = currentHP > 0f;
            movementCarry = 0f;
        }

        /// <summary>Unique, stable identifier within one battle.</summary>
        public int Id
        {
            get { return id; }
        }

        /// <summary>Display name. Never null.</summary>
        public string Name
        {
            get { return name; }
        }

        /// <summary>Archetype identifier used to match Kapatiran bonds. Never null.</summary>
        public string ArchetypeId
        {
            get { return archetypeId; }
        }

        /// <summary>
        /// Health this unit restores to a wounded ally in place of attacking, before the ally's
        /// Healing Received multiplier. Zero, the default, means the unit never heals.
        /// </summary>
        /// <remarks>
        /// A property of the unit rather than of its stat block: healing is an ability only the
        /// Field Medic has, and <see cref="UnitStats"/> mirrors the document's eight stats exactly.
        /// </remarks>
        public float HealPower
        {
            get { return healPower; }
            set { healPower = value > 0f ? value : 0f; }
        }

        /// <summary>Side this unit fights for.</summary>
        public Team Team
        {
            get { return team; }
        }

        /// <summary>The unmodified stat block this unit was created from.</summary>
        public UnitStats BaseStats
        {
            get { return baseStats; }
        }

        /// <summary>Modifiers currently acting on this unit. Rebuilt at the start of every AI turn.</summary>
        public ModifierStack Modifiers
        {
            get { return modifiers; }
        }

        /// <summary>Remaining hit points. Never negative.</summary>
        public float CurrentHP
        {
            get { return currentHP; }
        }

        /// <summary>
        /// True while the unit is still fighting. Tracked as an explicit flag rather than inferred
        /// from <see cref="CurrentHP"/> so that a unit killed by a scripted effect stays dead even
        /// if something later heals it.
        /// </summary>
        public bool IsAlive
        {
            get { return alive; }
        }

        /// <summary>The cell this unit occupies.</summary>
        public GridCoord Position
        {
            get { return position; }
        }

        /// <summary>
        /// Leftover fractional movement carried into the next AI turn.
        /// </summary>
        /// <remarks>
        /// TODO(design): not specified in capstone document. The document gives no turn duration and
        /// no unit for Movement Speed, yet Table 2 applies a "-15% Movement Speed" penalty in the
        /// Coastal Shallows. If movement were simply truncated to whole cells per turn, a speed of
        /// 1.0 reduced to 0.85 would round to zero and freeze the unit forever, making the terrain
        /// penalty a hard stop rather than a slowdown. Carrying the fraction across turns makes the
        /// penalty behave as a percentage of distance travelled over time, which is the only reading
        /// consistent with calling it a percentage at all. Revisit if design defines a turn length.
        /// </remarks>
        public float MovementCarry
        {
            get { return movementCarry; }
        }

        /// <summary>
        /// Resolves one statistic through this unit's current modifier stack.
        /// </summary>
        /// <param name="stat">Stat to resolve.</param>
        /// <returns>
        /// The effective value. <see cref="StatKind.HealingReceived"/> resolves against an implicit
        /// base of <c>1.0</c> because it has no slot in <see cref="UnitStats"/>.
        /// </returns>
        public float GetEffectiveStat(StatKind stat)
        {
            if (stat == StatKind.HealingReceived)
            {
                return modifiers.Resolve(StatKind.HealingReceived, 1f);
            }

            return modifiers.Resolve(stat, baseStats.Get(stat));
        }

        /// <summary>
        /// Resolves the whole stat block through this unit's current modifier stack. This is what
        /// the damage formula is handed.
        /// </summary>
        public UnitStats GetEffectiveStats()
        {
            return baseStats.WithModifiers(modifiers);
        }

        /// <summary>
        /// Moves the unit to a new cell. The simulator validates walkability and occupancy before
        /// calling this; the unit itself holds no opinion about the map.
        /// </summary>
        /// <param name="destination">Cell to move to.</param>
        public void MoveTo(GridCoord destination)
        {
            position = destination;
        }

        /// <summary>
        /// Adds this turn's movement allowance to the carry and returns how many whole cells the
        /// unit may step, leaving the remainder banked for later turns.
        /// </summary>
        /// <param name="movementAllowance">Effective Movement Speed for this turn.</param>
        /// <returns>Whole cells the unit may step this turn; never negative.</returns>
        public int TakeMovementSteps(float movementAllowance)
        {
            if (movementAllowance > 0f)
            {
                movementCarry += movementAllowance;
            }

            if (movementCarry < 1f)
            {
                return 0;
            }

            double whole = Math.Floor((double)movementCarry);
            int steps = whole > int.MaxValue ? int.MaxValue : (int)whole;
            movementCarry -= steps;
            return steps;
        }

        /// <summary>
        /// Applies damage, clamping health at zero and marking the unit dead when it reaches it.
        /// </summary>
        /// <param name="amount">
        /// Damage to apply. Non-positive amounts are ignored rather than healing the target: a
        /// mitigation formula that produced a negative number must never turn an attack into a heal.
        /// </param>
        /// <returns>The health actually removed.</returns>
        public float ApplyDamage(float amount)
        {
            if (!alive || amount <= 0f)
            {
                return 0f;
            }

            float applied = amount > currentHP ? currentHP : amount;
            currentHP -= applied;

            if (currentHP <= 0f)
            {
                currentHP = 0f;
                alive = false;
            }

            return applied;
        }

        /// <summary>
        /// Restores health, clamped at the unit's effective Max HP. Dead units are not revived.
        /// </summary>
        /// <param name="amount">Health to restore. Non-positive amounts are ignored.</param>
        /// <returns>The health actually restored.</returns>
        public float Heal(float amount)
        {
            if (!alive || amount <= 0f)
            {
                return 0f;
            }

            float ceiling = GetEffectiveStat(StatKind.MaxHP);
            if (currentHP >= ceiling)
            {
                return 0f;
            }

            float headroom = ceiling - currentHP;
            float applied = amount > headroom ? headroom : amount;
            currentHP += applied;
            return applied;
        }

        /// <summary>
        /// Brings a fallen unit back on <paramref name="cell"/> with <paramref name="health"/> HP,
        /// its modifiers cleared and its movement carry reset. Does nothing to a living unit.
        /// </summary>
        /// <param name="health">Health to return with; at least 1.</param>
        /// <param name="cell">Where it stands again.</param>
        /// <returns>True when the unit was revived.</returns>
        public bool Revive(float health, GridCoord cell)
        {
            if (alive)
            {
                return false;
            }

            currentHP = health < 1f ? 1f : health;
            alive = true;
            position = cell;
            movementCarry = 0f;
            modifiers.Clear();
            return true;
        }

        /// <summary>Kills the unit outright, bypassing the damage formula.</summary>
        public void Kill()
        {
            currentHP = 0f;
            alive = false;
        }

        /// <summary>Short identifier used in log lines. Stable and culture-invariant.</summary>
        public override string ToString()
        {
            return "#" + id + " " + name + " (" + team + ")";
        }
    }
}
