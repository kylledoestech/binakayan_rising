using System;
using System.Collections.Generic;
using BinakayanRising.Core.Grid;

namespace BinakayanRising.Core.Combat
{
    /// <summary>
    /// The deterministic, headless auto-battler resolver: the Combat phase of a mission, start to
    /// finish, with no player input and no Unity dependency.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What the capstone document specifies.</b> A mission runs a Deployment phase, in which the
    /// player places units and all input is then locked, followed by a Combat phase that is fully
    /// autonomous. The document's state machine alternates <c>AI_Pathfinding</c> and
    /// <c>DamageCalculation</c> "until stage clear or defeat"; its unit of time is the "AI turn";
    /// and it states that "the system calculates remaining unit HP at the end of every AI turn to
    /// trigger the Victory/Defeat screen". Victory is all enemy units routed, defeat is the player's
    /// deployed roster completely defeated. This class implements exactly that loop.
    /// </para>
    /// <para>
    /// <b>What it does not specify, and how that is handled.</b> No damage formula, no base stats, no
    /// targeting rule, no turn duration, no crit multiplier, no accuracy mechanic, no enemy roster.
    /// Every one of those arrives here as an injected parameter — a <see cref="CombatConfig"/>, an
    /// <see cref="IDamageFormula"/>, an <see cref="ITargetingStrategy"/>, an
    /// <see cref="IMovementPolicy"/>, a <see cref="TerrainModifierProvider"/> or a
    /// <see cref="KapatiranResolver"/>. There is not a single balance number in this file.
    /// </para>
    /// <para>
    /// <b>Order of a turn.</b> Recompute modifiers from current positions, apply terrain HP
    /// regeneration, then activate every living unit in ascending id order, then evaluate the
    /// outcome. Modifiers are recomputed before anything else because both regeneration and combat
    /// depend on where units are standing right now — a unit that stepped into a trench last turn
    /// must be defending from it this turn.
    /// </para>
    /// <para>
    /// <b>Determinism.</b> Given the same grid, the same units in the same starting cells, the same
    /// config and the same seed, this produces a byte-identical event log on every machine and every
    /// run. That rests on four things, all of which are load-bearing: units act in ascending id
    /// order, never in list order; every tie anywhere falls back to unit id; all randomness comes
    /// from one <see cref="DeterministicRandom"/> drawn in a fixed sequence; and no Unity type,
    /// clock, hash code or floating-point transcendental is involved anywhere.
    /// </para>
    /// <para>
    /// <b>Presentation coupling: none.</b> The simulator produces a <see cref="BattleEvent"/> log
    /// and nothing else. The Gameplay layer replays that log afterwards to animate sprites. Never
    /// add a callback, a coroutine or a "current animation" concept here — a battle must remain
    /// something a unit test can run to completion in microseconds with the editor closed.
    /// </para>
    /// </remarks>
    public sealed partial class BattleSimulator
    {
        private readonly IBattleGrid grid;
        private readonly CombatUnit[] unitsInIdOrder;
        private readonly CombatConfig config;
        private readonly IDamageFormula damageFormula;
        private readonly ITargetingStrategy targeting;
        private readonly IMovementPolicy movement;
        private readonly TerrainModifierProvider terrain;
        private readonly KapatiranResolver kapatiran;
        private readonly DeterministicRandom rng;

        private readonly List<BattleEvent> allEvents = new List<BattleEvent>();
        private readonly List<CombatUnit> livingScratch = new List<CombatUnit>();
        private readonly List<CombatUnit> candidateScratch = new List<CombatUnit>();

        private readonly int maxTurns;
        private readonly bool allowDiagonals;
        private readonly bool logModifierEvents;
        private readonly bool spanishTerrainBonuses;
        private readonly BattleOutcome mutualAnnihilationOutcome;
        private readonly BattleObjective objective;
        private readonly float sortieSpeed;

        private int turnNumber;
        private BattleOutcome outcome = BattleOutcome.InProgress;

        /// <summary>
        /// Creates a simulator for one battle. The units' current positions are taken as the result
        /// of the Deployment phase; nothing here places anything.
        /// </summary>
        /// <param name="grid">The battle map.</param>
        /// <param name="units">
        /// Every unit taking part, from both sides. Ids must be unique. The list is copied and
        /// sorted by id, so the caller's ordering never affects the outcome.
        /// </param>
        /// <param name="config">Tunables. Snapshotted, so later edits cannot affect this battle.</param>
        /// <param name="formula">
        /// Damage resolution. Null uses a <see cref="StandardDamageFormula"/> built from
        /// <paramref name="config"/> — see that class's header for why it is a placeholder.
        /// </param>
        /// <param name="targeting">Target selection. Null uses <see cref="TargetingStrategies.Default"/>.</param>
        /// <param name="terrain">Terrain modifiers. Null grants no terrain effects.</param>
        /// <param name="kapatiran">Bond resolution. Null means no bond can trigger.</param>
        /// <param name="movement">
        /// Movement policy and the seam for a future A*. Null uses
        /// <see cref="GreedyStepMovementPolicy"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when the grid, the unit list or the config is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the unit list is empty, contains a null, contains duplicate ids, or places a
        /// unit outside the map.
        /// </exception>
        public BattleSimulator(
            IBattleGrid grid,
            IReadOnlyList<CombatUnit> units,
            CombatConfig config,
            IDamageFormula formula = null,
            ITargetingStrategy targeting = null,
            TerrainModifierProvider terrain = null,
            KapatiranResolver kapatiran = null,
            IMovementPolicy movement = null)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            if (units == null)
            {
                throw new ArgumentNullException(nameof(units));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (units.Count == 0)
            {
                throw new ArgumentException("A battle needs at least one unit.", nameof(units));
            }

            this.grid = grid;
            this.config = config.Clone();

            maxTurns = this.config.MaxTurns;
            allowDiagonals = this.config.AllowDiagonalMovement;
            logModifierEvents = this.config.LogModifierEvents;
            spanishTerrainBonuses = this.config.SpanishReceivesTerrainBonuses;
            mutualAnnihilationOutcome = this.config.MutualAnnihilationOutcome;
            objective = this.config.Objective;
            sortieSpeed = this.config.SortieSpeed;

            unitsInIdOrder = BuildUnitArray(units);

            damageFormula = formula ?? new StandardDamageFormula(this.config);
            this.targeting = targeting ?? TargetingStrategies.Default(allowDiagonals);
            this.terrain = terrain ?? TerrainModifierProvider.CreateEmpty();
            this.kapatiran = kapatiran ?? KapatiranResolver.CreateEmpty();
            this.movement = movement ?? new GreedyStepMovementPolicy();

            rng = new DeterministicRandom(this.config.RandomSeed);
            RecordStartCells();
        }

        /// <summary>Every unit in the battle, in ascending id order — the order they act in.</summary>
        public IReadOnlyList<CombatUnit> Units
        {
            get { return unitsInIdOrder; }
        }

        /// <summary>The battle map.</summary>
        public IBattleGrid Grid
        {
            get { return grid; }
        }

        /// <summary>The snapshot of the configuration this battle is running under.</summary>
        public CombatConfig Config
        {
            get { return config; }
        }

        /// <summary>Number of AI turns executed so far.</summary>
        public int TurnNumber
        {
            get { return turnNumber; }
        }

        /// <summary>
        /// Current outcome. <see cref="BattleOutcome.InProgress"/> until a terminal condition is met.
        /// </summary>
        public BattleOutcome Outcome
        {
            get { return outcome; }
        }

        /// <summary>True while the battle can still take another turn.</summary>
        public bool IsFinished
        {
            get { return outcome != BattleOutcome.InProgress; }
        }

        /// <summary>Every event produced so far, in order.</summary>
        public IReadOnlyList<BattleEvent> EventLog
        {
            get { return allEvents; }
        }

        /// <summary>The battle's generator. Exposed for diagnostics; do not draw from it externally.</summary>
        public DeterministicRandom Random
        {
            get { return rng; }
        }

        /// <summary>
        /// Executes exactly one AI turn: recompute modifiers, regenerate, activate every living unit
        /// in id order, then evaluate the outcome from remaining HP as the document requires.
        /// </summary>
        /// <returns>The events this turn produced and the outcome evaluated at its end.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the battle has already finished.</exception>
        public BattleTurnResult ExecuteTurn()
        {
            if (IsFinished)
            {
                throw new InvalidOperationException(
                    "The battle has already finished with outcome " + outcome + "; no further turns may be executed.");
            }

            turnNumber++;

            List<BattleEvent> turnEvents = new List<BattleEvent>();
            Emit(turnEvents, BattleEvent.TurnStarted(turnNumber));
            ApplyQueuedCommand(turnEvents);

            RefreshLiving();
            RecomputeModifiers(turnEvents);
            ApplyTerrainRegeneration(turnEvents);
            ActivateUnits(turnEvents);

            int katipunanAlive = CountAlive(Team.Katipunan);
            int spanishAlive = CountAlive(Team.Spanish);
            outcome = EvaluateOutcome(katipunanAlive, spanishAlive);

            Emit(turnEvents, BattleEvent.TurnEnded(turnNumber, outcome));

            if (outcome != BattleOutcome.InProgress)
            {
                Emit(turnEvents, BattleEvent.BattleEnded(turnNumber, outcome));
            }

            return new BattleTurnResult(turnNumber, turnEvents, outcome, katipunanAlive, spanishAlive);
        }

        /// <summary>
        /// Runs turns until the battle reaches Victory, Defeat or Draw.
        /// </summary>
        /// <remarks>
        /// Termination is guaranteed by <see cref="CombatConfig.MaxTurns"/>, not by the battle
        /// resolving on its own. Two units that can never reach each other would otherwise loop
        /// forever, which the document's "until stage clear or defeat" wording does not rule out.
        /// </remarks>
        /// <returns>The outcome, the turn count, and the complete event log.</returns>
        public BattleResult RunToCompletion()
        {
            while (!IsFinished)
            {
                ExecuteTurn();
            }

            return new BattleResult(
                outcome,
                turnNumber,
                allEvents,
                CountAlive(Team.Katipunan),
                CountAlive(Team.Spanish));
        }

        /// <summary>
        /// Snapshots the current state as a <see cref="BattleResult"/> without running any further
        /// turns. Useful when a caller drives <see cref="ExecuteTurn"/> itself.
        /// </summary>
        public BattleResult GetResultSoFar()
        {
            return new BattleResult(
                outcome,
                turnNumber,
                allEvents,
                CountAlive(Team.Katipunan),
                CountAlive(Team.Spanish));
        }

        /// <summary>Number of living units on a side.</summary>
        /// <param name="team">Side to count.</param>
        public int CountAlive(Team team)
        {
            int count = 0;
            for (int i = 0; i < unitsInIdOrder.Length; i++)
            {
                CombatUnit unit = unitsInIdOrder[i];
                if (unit.IsAlive && unit.Team == team)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Validates the unit list and returns it sorted by id, which fixes activation order for the
        /// whole battle.
        /// </summary>
        private static CombatUnit[] BuildUnitArray(IReadOnlyList<CombatUnit> units)
        {
            CombatUnit[] array = new CombatUnit[units.Count];
            HashSet<int> seenIds = new HashSet<int>();

            for (int i = 0; i < units.Count; i++)
            {
                CombatUnit unit = units[i];
                if (unit == null)
                {
                    throw new ArgumentException("The unit list contains a null entry.", nameof(units));
                }

                if (!seenIds.Add(unit.Id))
                {
                    throw new ArgumentException(
                        "Duplicate unit id " + unit.Id + ". Ids must be unique because every deterministic tie-break falls back to them.",
                        nameof(units));
                }

                array[i] = unit;
            }

            Array.Sort(array, CompareById);
            return array;
        }

        /// <summary>Ascending unit id. The battle's one global ordering.</summary>
        private static int CompareById(CombatUnit left, CombatUnit right)
        {
            return left.Id.CompareTo(right.Id);
        }

        /// <summary>Records an event both in the turn's list and in the cumulative log.</summary>
        private void Emit(List<BattleEvent> turnEvents, BattleEvent battleEvent)
        {
            turnEvents.Add(battleEvent);
            allEvents.Add(battleEvent);
        }

        /// <summary>Refills the reusable living-units list in id order.</summary>
        private void RefreshLiving()
        {
            livingScratch.Clear();
            for (int i = 0; i < unitsInIdOrder.Length; i++)
            {
                if (unitsInIdOrder[i].IsAlive)
                {
                    livingScratch.Add(unitsInIdOrder[i]);
                }
            }
        }

        /// <summary>
        /// Rebuilds every living unit's modifier stack from scratch: terrain first, then Kapatiran.
        /// </summary>
        /// <remarks>
        /// Rebuilding rather than incrementally patching is deliberate. Units move, so a modifier
        /// granted last turn may be invalid this turn, and an incremental scheme would eventually
        /// leak a stale buff — a bug that would surface as a desynced replay long after the turn
        /// that caused it. Clearing costs nothing at these unit counts.
        /// </remarks>
        private void RecomputeModifiers(List<BattleEvent> turnEvents)
        {
            for (int i = 0; i < livingScratch.Count; i++)
            {
                livingScratch[i].Modifiers.Clear();
            }

            for (int i = 0; i < livingScratch.Count; i++)
            {
                CombatUnit unit = livingScratch[i];
                if (!grid.InBounds(unit.Position))
                {
                    continue;
                }

                TerrainType terrainType = grid.GetTerrain(unit.Position);
                IReadOnlyList<StatModifier> modifiers = terrain.GetModifiers(terrainType);
                bool bonusesAllowed = ReceivesTerrainBonuses(unit);
                bool atHome = unit.Abilities.HasHomeTerrain && unit.Abilities.Terrain == terrainType;

                for (int m = 0; m < modifiers.Count; m++)
                {
                    // Every stat reads higher-is-better, so a positive delta is always a benefit.
                    bool benefit = modifiers[m].PercentDelta > 0f || modifiers[m].FlatDelta > 0f;
                    if (!bonusesAllowed && benefit)
                    {
                        continue;
                    }

                    // A unit on its home terrain shrugs off that terrain's penalties.
                    if (atHome && !benefit)
                    {
                        continue;
                    }

                    AddModifier(unit, modifiers[m], turnEvents);
                }

                // Its own advantage there is an ability, not a terrain bonus, so the switch that
                // withholds fortifications from the Spanish does not withhold it.
                if (atHome)
                {
                    IReadOnlyList<StatModifier> home = unit.Abilities.HomeTerrainModifiers;
                    for (int m = 0; m < home.Count; m++)
                    {
                        AddModifier(unit, home[m], turnEvents);
                    }
                }
            }

            IReadOnlyList<KapatiranActivation> activations = kapatiran.Resolve(livingScratch, grid);
            for (int a = 0; a < activations.Count; a++)
            {
                KapatiranActivation activation = activations[a];
                IReadOnlyList<StatModifier> modifiers = activation.Modifiers;

                for (int m = 0; m < modifiers.Count; m++)
                {
                    activation.UnitA.Modifiers.Add(modifiers[m]);
                    activation.UnitB.Modifiers.Add(modifiers[m]);

                    if (logModifierEvents)
                    {
                        Emit(turnEvents, BattleEvent.ModifierApplied(turnNumber, activation.UnitA.Id, modifiers[m]));
                        Emit(turnEvents, BattleEvent.ModifierApplied(turnNumber, activation.UnitB.Id, modifiers[m]));
                    }
                }
            }

            ApplyCommandModifiers(turnEvents);
            ApplyAuras(turnEvents);
            ApplySeekerSpeed(turnEvents);
        }

        /// <summary>Adds a modifier to a unit and logs it when modifier logging is on.</summary>
        private void AddModifier(CombatUnit unit, StatModifier modifier, List<BattleEvent> turnEvents)
        {
            unit.Modifiers.Add(modifier);
            if (logModifierEvents)
            {
                Emit(turnEvents, BattleEvent.ModifierApplied(turnNumber, unit.Id, modifier));
            }
        }

        /// <summary>
        /// Every living aura-bearer grants its modifiers to each living ally within its radius, not
        /// counting itself. Two officers over one regular both count: the modifiers stack under
        /// the battle's <see cref="CombatConfig.StackingPolicy"/> like any others.
        /// </summary>
        private void ApplyAuras(List<BattleEvent> turnEvents)
        {
            for (int i = 0; i < livingScratch.Count; i++)
            {
                CombatUnit source = livingScratch[i];
                if (!source.Abilities.HasAura)
                {
                    continue;
                }

                int radius = source.Abilities.AuraRadius;
                IReadOnlyList<StatModifier> aura = source.Abilities.AuraModifiers;
                for (int j = 0; j < livingScratch.Count; j++)
                {
                    CombatUnit ally = livingScratch[j];
                    if (ally == source || ally.Team != source.Team || DistanceBetween(source.Position, ally.Position) > radius)
                    {
                        continue;
                    }

                    for (int m = 0; m < aura.Count; m++)
                    {
                        AddModifier(ally, aura[m], turnEvents);
                    }
                }
            }
        }

        /// <summary>
        /// Under <see cref="ObjectiveKind.Sabotage"/>, brings every Katipunan unit slower than the
        /// objective's seeker speed up to it with a flat ability modifier.
        /// </summary>
        private void ApplySeekerSpeed(List<BattleEvent> turnEvents)
        {
            if (objective.Kind != ObjectiveKind.Sabotage || objective.SeekerSpeed <= 0f)
            {
                return;
            }

            for (int i = 0; i < livingScratch.Count; i++)
            {
                CombatUnit unit = livingScratch[i];
                if (unit.Team != Team.Katipunan || unit.Abilities.NonCombatant)
                {
                    continue;
                }

                float speed = unit.GetEffectiveStat(StatKind.MovementSpeed);
                if (speed < objective.SeekerSpeed)
                {
                    AddModifier(
                        unit,
                        StatModifier.Flat(StatKind.MovementSpeed, objective.SeekerSpeed - speed, ModifierSource.Ability, "Infiltration"),
                        turnEvents);
                }
            }
        }

        /// <summary>
        /// False for a Spanish unit when <see cref="CombatConfig.SpanishReceivesTerrainBonuses"/> is
        /// off. Penalties are filtered by the caller, not here, so they still land.
        /// </summary>
        private bool ReceivesTerrainBonuses(CombatUnit unit)
        {
            return spanishTerrainBonuses || unit.Team != Team.Spanish;
        }

        /// <summary>
        /// Applies the document's "+5% HP regeneration per AI turn" terrain effect.
        /// </summary>
        /// <remarks>
        /// The fraction is taken against the unit's <em>effective</em> Max HP, so a bond that raises
        /// Max HP also raises what a tent restores. The result is then scaled by the unit's
        /// Healing Received multiplier, which is how Capstone Table 3's Field Medic bonus reaches
        /// terrain regeneration. TODO(design): the document does not say whether Healing Received
        /// applies to terrain regeneration or only to active healing.
        /// </remarks>
        private void ApplyTerrainRegeneration(List<BattleEvent> turnEvents)
        {
            for (int i = 0; i < livingScratch.Count; i++)
            {
                CombatUnit unit = livingScratch[i];
                if (!unit.IsAlive || !grid.InBounds(unit.Position))
                {
                    continue;
                }

                if (!ReceivesTerrainBonuses(unit))
                {
                    continue;
                }

                TerrainType terrainType = grid.GetTerrain(unit.Position);
                float fraction = terrain.GetHpRegenFraction(terrainType);
                if (fraction <= 0f)
                {
                    continue;
                }

                float amount = unit.GetEffectiveStat(StatKind.MaxHP) * fraction;
                amount *= unit.GetEffectiveStat(StatKind.HealingReceived);

                float healed = unit.Heal(amount);
                if (healed > 0f)
                {
                    Emit(turnEvents, BattleEvent.HpRegenerated(turnNumber, unit.Id, healed, terrainType.ToString()));
                }
            }
        }

        /// <summary>
        /// Activates every unit that was alive at the start of the turn, in ascending id order.
        /// </summary>
        /// <remarks>
        /// A unit killed earlier in the same turn is skipped rather than removed from the list, so
        /// the activation order of the survivors does not shift mid-turn. This is why the loop
        /// re-checks <see cref="CombatUnit.IsAlive"/> instead of iterating a freshly filtered list.
        /// </remarks>
        private void ActivateUnits(List<BattleEvent> turnEvents)
        {
            for (int i = 0; i < livingScratch.Count; i++)
            {
                CombatUnit unit = livingScratch[i];
                if (!unit.IsAlive || unit.Abilities.NonCombatant)
                {
                    continue;
                }

                // A gun that fired is still being reloaded: it neither fires nor moves.
                if (unit.ReloadRemaining > 0)
                {
                    unit.ReloadRemaining--;
                    continue;
                }

                if (unit.HealPower > 0f && TryHealAlly(unit, turnEvents))
                {
                    continue;
                }

                CombatUnit target = SelectTargetFor(unit);
                float range = unit.GetEffectiveStat(StatKind.AttackRange);

                // Raiders: under Escort the Spanish want the cart. They hit it whenever it is in
                // reach, and otherwise walk for it, fighting only what stands in reach on the way.
                CombatUnit escorted = RaidTargetFor(unit);
                if (escorted != null && DistanceBetween(unit.Position, escorted.Position) <= range)
                {
                    ResolveAttack(unit, escorted, turnEvents);
                    continue;
                }

                bool inReach = target != null && DistanceBetween(unit.Position, target.Position) <= range;

                if (inReach)
                {
                    ResolveAttack(unit, target, turnEvents);
                    continue;
                }

                if (escorted != null)
                {
                    AdvanceTo(unit, escorted.Position, range, unit.GetEffectiveStat(StatKind.MovementSpeed), turnEvents);
                    continue;
                }

                // A saboteur fights only what is already in reach; otherwise it makes for the target.
                if (objective.Kind == ObjectiveKind.Sabotage && unit.Team == Team.Katipunan)
                {
                    AdvanceTo(unit, objective.TargetCell, 0f, unit.GetEffectiveStat(StatKind.MovementSpeed), turnEvents);
                    continue;
                }

                if (target == null)
                {
                    continue;
                }

                CombatUnit provoker = SortieTargetFor(unit);
                if (provoker != null)
                {
                    AdvanceTo(unit, provoker.Position, range, sortieSpeed, turnEvents);
                    continue;
                }

                AdvanceTo(unit, target.Position, range, unit.GetEffectiveStat(StatKind.MovementSpeed), turnEvents);
            }
        }

        /// <summary>
        /// Under <see cref="ObjectiveKind.Escort"/>, the living escorted unit when
        /// <paramref name="unit"/> is its enemy; otherwise null.
        /// </summary>
        private CombatUnit RaidTargetFor(CombatUnit unit)
        {
            if (objective.Kind != ObjectiveKind.Escort)
            {
                return null;
            }

            CombatUnit escorted = FindUnit(objective.EscortUnitId);
            return escorted != null && escorted.IsAlive && escorted.Team != unit.Team ? escorted : null;
        }

        /// <summary>
        /// The living enemy an entrenched unit should sortie against, or null. Only a unit that
        /// cannot move by itself sorties, only when sorties are on, and only against the enemy that
        /// last hit it from out of reach.
        /// </summary>
        private CombatUnit SortieTargetFor(CombatUnit unit)
        {
            if (sortieSpeed <= 0f || unit.ProvokedBy < 0 || unit.GetEffectiveStat(StatKind.MovementSpeed) > 0f)
            {
                return null;
            }

            for (int i = 0; i < unitsInIdOrder.Length; i++)
            {
                CombatUnit other = unitsInIdOrder[i];
                if (other.Id == unit.ProvokedBy)
                {
                    if (other.IsAlive)
                    {
                        return other;
                    }

                    break;
                }
            }

            unit.ProvokedBy = -1;
            return null;
        }

        /// <summary>
        /// A healer's turn: restore the most wounded ally in reach, if any is hurt enough to be
        /// worth it. Returns false when there is no such ally, and the healer fights instead.
        /// </summary>
        /// <remarks>
        /// Reach is the healer's attack range. The ally with the lowest share of its Max HP wins,
        /// the lower id breaking ties, so the choice uses no dice and a battle with a healer stays
        /// as reproducible as one without. The healer does not heal itself. The ally's Healing
        /// Received multiplier applies, which is where Table 3's Vanguard and Field Medic bond
        /// pays off.
        /// </remarks>
        private bool TryHealAlly(CombatUnit healer, List<BattleEvent> turnEvents)
        {
            float reach = healer.GetEffectiveStat(StatKind.AttackRange);
            CombatUnit patient = null;
            float lowest = config.HealBelowFraction;

            for (int i = 0; i < unitsInIdOrder.Length; i++)
            {
                CombatUnit ally = unitsInIdOrder[i];
                if (ally == healer || !ally.IsAlive || ally.Team != healer.Team)
                {
                    continue;
                }

                if (DistanceBetween(healer.Position, ally.Position) > reach)
                {
                    continue;
                }

                float max = ally.GetEffectiveStat(StatKind.MaxHP);
                float share = max > 0f ? ally.CurrentHP / max : 1f;
                if (share < lowest)
                {
                    lowest = share;
                    patient = ally;
                }
            }

            if (patient == null)
            {
                return false;
            }

            float healed = patient.Heal(healer.HealPower * patient.GetEffectiveStat(StatKind.HealingReceived));
            if (healed <= 0f)
            {
                return false;
            }

            Emit(turnEvents, BattleEvent.UnitHealed(turnNumber, healer.Id, patient.Id, healed, healer.Position, patient.Position));
            return true;
        }

        /// <summary>Builds the living-enemy candidate list in id order and delegates to the strategy.</summary>
        private CombatUnit SelectTargetFor(CombatUnit unit)
        {
            candidateScratch.Clear();
            for (int i = 0; i < unitsInIdOrder.Length; i++)
            {
                CombatUnit other = unitsInIdOrder[i];
                if (other.IsAlive && other.Team != unit.Team)
                {
                    candidateScratch.Add(other);
                }
            }

            if (candidateScratch.Count == 0)
            {
                return null;
            }

            return targeting.SelectTarget(unit, candidateScratch, grid);
        }

        /// <summary>
        /// Steps the unit toward a cell, spending <paramref name="allowance"/> as this turn's
        /// whole-cell movement and stopping early once the cell is within <paramref name="range"/>.
        /// </summary>
        /// <remarks>
        /// A unit that moves does not also attack this turn. TODO(design): not specified in capstone
        /// document — the state machine alternates pathfinding and damage calculation without saying
        /// whether one unit does both in a turn. Move-or-attack is the more conservative reading and
        /// keeps a turn's cost predictable.
        /// </remarks>
        private void AdvanceTo(CombatUnit unit, GridCoord destination, float range, float allowance, List<BattleEvent> turnEvents)
        {
            int steps = unit.TakeMovementSteps(allowance);
            if (steps <= 0)
            {
                return;
            }

            // A unit can never usefully cross more cells than the map is wide plus tall; capping here
            // stops a pathological Movement Speed from turning one turn into a very long loop.
            int stepCap = grid.Width + grid.Height;
            if (steps > stepCap)
            {
                steps = stepCap;
            }

            for (int step = 0; step < steps; step++)
            {
                if (DistanceBetween(unit.Position, destination) <= range)
                {
                    return;
                }

                GridCoord next;
                if (!movement.TryGetNextStep(unit, destination, grid, unitsInIdOrder, allowDiagonals, out next))
                {
                    return;
                }

                GridCoord from = unit.Position;
                unit.MoveTo(next);
                Emit(turnEvents, BattleEvent.UnitMoved(turnNumber, unit.Id, from, next));
            }
        }

        /// <summary>Resolves one attack through the injected damage formula and logs the result.</summary>
        private void ResolveAttack(CombatUnit attacker, CombatUnit target, List<BattleEvent> turnEvents)
        {
            Emit(turnEvents, BattleEvent.UnitAttacked(turnNumber, attacker.Id, target.Id, attacker.Position, target.Position));

            DamageResult result = damageFormula.Compute(attacker.GetEffectiveStats(), target.GetEffectiveStats(), rng);
            float applied = target.ApplyDamage(result.Amount);

            Emit(turnEvents, BattleEvent.DamageDealt(turnNumber, attacker.Id, target.Id, result, applied));

            // Fired on from beyond its own reach: an entrenched target remembers who did it.
            if (applied > 0f && target.IsAlive
                && DistanceBetween(attacker.Position, target.Position) > target.GetEffectiveStat(StatKind.AttackRange))
            {
                target.ProvokedBy = attacker.Id;
            }

            if (!target.IsAlive)
            {
                Emit(turnEvents, BattleEvent.UnitDied(turnNumber, target.Id, attacker.Id, target.Position));
            }

            if (result.Connected)
            {
                ApplySplash(attacker, target.Position, result.Amount, turnEvents);
            }

            attacker.ReloadRemaining = attacker.Abilities.ReloadTurns;
        }

        /// <summary>
        /// Lands the attacker's splash share of <paramref name="hit"/> on every living enemy of the
        /// attacker standing orthogonally next to <paramref name="center"/>, in the fixed neighbour
        /// order. No rolls: the shell has already landed. Allies of the gun are never caught.
        /// </summary>
        /// <remarks>
        /// TODO(design): not specified in capstone document — the story names Spanish artillery but
        /// no blast rule. The splash is a share of the hit's rolled damage, after the target's
        /// defense and before its health ran out, so a kill shot does not blunt the blast.
        /// DESIGN-DECISIONS #19.
        /// </remarks>
        private void ApplySplash(CombatUnit attacker, GridCoord center, float hit, List<BattleEvent> turnEvents)
        {
            float fraction = attacker.Abilities.SplashFraction;
            if (fraction <= 0f || hit <= 0f)
            {
                return;
            }

            float amount = hit * fraction;
            foreach (GridCoord cell in GridDistance.Neighbors(center, false))
            {
                for (int i = 0; i < unitsInIdOrder.Length; i++)
                {
                    CombatUnit caught = unitsInIdOrder[i];
                    if (!caught.IsAlive || caught.Team == attacker.Team || caught.Position != cell)
                    {
                        continue;
                    }

                    float applied = caught.ApplyDamage(amount);
                    Emit(turnEvents, BattleEvent.SplashDamage(turnNumber, attacker.Id, caught.Id, applied));
                    if (!caught.IsAlive)
                    {
                        Emit(turnEvents, BattleEvent.UnitDied(turnNumber, caught.Id, attacker.Id, caught.Position));
                    }
                }
            }
        }

        /// <summary>Distance under the metric implied by <see cref="CombatConfig.AllowDiagonalMovement"/>.</summary>
        private int DistanceBetween(GridCoord a, GridCoord b)
        {
            return allowDiagonals ? GridDistance.Chebyshev(a, b) : GridDistance.Manhattan(a, b);
        }

        /// <summary>
        /// The document's end-of-turn check: "the system calculates remaining unit HP at the end of
        /// every AI turn to trigger the Victory/Defeat screen".
        /// </summary>
        private BattleOutcome EvaluateOutcome(int katipunanAlive, int spanishAlive)
        {
            switch (objective.Kind)
            {
                case ObjectiveKind.Escort:
                    return EvaluateEscort(katipunanAlive, spanishAlive);
                case ObjectiveKind.Sabotage:
                    return EvaluateSabotage(katipunanAlive);
            }

            if (katipunanAlive == 0 && spanishAlive == 0)
            {
                return mutualAnnihilationOutcome;
            }

            if (spanishAlive == 0)
            {
                return BattleOutcome.Victory;
            }

            if (katipunanAlive == 0)
            {
                return BattleOutcome.Defeat;
            }

            if (turnNumber >= maxTurns)
            {
                return BattleOutcome.Draw;
            }

            return BattleOutcome.InProgress;
        }

        /// <summary>
        /// <see cref="ObjectiveKind.Escort"/>: the escorted unit falling loses at once, even on the
        /// turn the last enemy falls; routing the enemy or reaching the turn cap with it standing wins.
        /// </summary>
        private BattleOutcome EvaluateEscort(int katipunanAlive, int spanishAlive)
        {
            CombatUnit escorted = FindUnit(objective.EscortUnitId);
            if (escorted == null || !escorted.IsAlive || katipunanAlive == 0)
            {
                return BattleOutcome.Defeat;
            }

            if (spanishAlive == 0 || turnNumber >= maxTurns)
            {
                return BattleOutcome.Victory;
            }

            return BattleOutcome.InProgress;
        }

        /// <summary>
        /// <see cref="ObjectiveKind.Sabotage"/>: a living Katipunan unit on the target cell wins,
        /// checked first so the unit that reaches it with its last breath still counts. Losing the
        /// squad, or the turn cap, loses. Routing the guard does not by itself win: the squad still
        /// has to walk in and light the fuse.
        /// </summary>
        private BattleOutcome EvaluateSabotage(int katipunanAlive)
        {
            for (int i = 0; i < unitsInIdOrder.Length; i++)
            {
                CombatUnit unit = unitsInIdOrder[i];
                if (unit.IsAlive && unit.Team == Team.Katipunan && unit.Position == objective.TargetCell)
                {
                    return BattleOutcome.Victory;
                }
            }

            if (katipunanAlive == 0 || turnNumber >= maxTurns)
            {
                return BattleOutcome.Defeat;
            }

            return BattleOutcome.InProgress;
        }

        /// <summary>The unit with <paramref name="id"/>, dead or alive, or null.</summary>
        private CombatUnit FindUnit(int id)
        {
            for (int i = 0; i < unitsInIdOrder.Length; i++)
            {
                if (unitsInIdOrder[i].Id == id)
                {
                    return unitsInIdOrder[i];
                }
            }

            return null;
        }
    }
}
