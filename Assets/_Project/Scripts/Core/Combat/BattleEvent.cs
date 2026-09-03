using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BinakayanRising.Core.Grid;

namespace BinakayanRising.Core.Combat
{
    /// <summary>
    /// Terminal state of a battle, evaluated from remaining unit HP at the end of every AI turn as
    /// the capstone document specifies.
    /// </summary>
    public enum BattleOutcome
    {
        /// <summary>Both sides still have living units and the turn cap has not been reached.</summary>
        InProgress = 0,

        /// <summary>All enemy units are routed. The document's win condition.</summary>
        Victory = 1,

        /// <summary>The player's deployed roster is completely defeated. The document's lose condition.</summary>
        Defeat = 2,

        /// <summary>
        /// Neither win nor lose condition can be reached: the turn cap expired, or both sides were
        /// wiped out at once. TODO(design): not specified in capstone document; see
        /// <see cref="CombatConfig.MaxTurns"/> and <see cref="CombatConfig.MutualAnnihilationOutcome"/>.
        /// </summary>
        Draw = 3
    }

    /// <summary>
    /// The kind of thing a <see cref="BattleEvent"/> records.
    /// </summary>
    public enum BattleEventType
    {
        /// <summary>An AI turn began.</summary>
        TurnStarted = 0,

        /// <summary>A modifier was applied to a unit during this turn's recomputation.</summary>
        ModifierApplied = 1,

        /// <summary>A unit regenerated health from the terrain it is standing on.</summary>
        HpRegenerated = 2,

        /// <summary>A unit stepped from one cell to an adjacent cell.</summary>
        UnitMoved = 3,

        /// <summary>A unit initiated an attack against a target.</summary>
        UnitAttacked = 4,

        /// <summary>An attack resolved. Carries the damage, and whether it crit, missed or was evaded.</summary>
        DamageDealt = 5,

        /// <summary>A unit's health reached zero.</summary>
        UnitDied = 6,

        /// <summary>An AI turn ended and the win/lose check ran.</summary>
        TurnEnded = 7,

        /// <summary>The battle reached a terminal outcome.</summary>
        BattleEnded = 8
    }

    /// <summary>
    /// One record in the battle's event log.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The event log is the interface between simulation and presentation.</b> The resolver runs
    /// headless and produces this log; the Gameplay layer replays it afterwards to animate sprites,
    /// pop damage numbers and drive the victory screen. Nothing in Core knows that any of that
    /// exists, which is what lets an entire battle be simulated in a unit test with the editor
    /// closed.
    /// </para>
    /// <para>
    /// Because the log is also the determinism oracle — two runs of the same seed must produce
    /// byte-identical logs — <see cref="ToString"/> is culture-invariant and every field it prints
    /// is fully determined by the simulation. Never add a timestamp, an object hash or anything else
    /// that varies between runs.
    /// </para>
    /// <para>
    /// One flat struct covers every event type rather than a class hierarchy: the log is produced in
    /// bulk, compared field by field, and serialised into replays, and a flat record keeps all three
    /// cheap. Unused fields carry documented sentinels (<see cref="NoUnit"/>, <see cref="GridCoord.Zero"/>).
    /// </para>
    /// </remarks>
    public readonly struct BattleEvent
    {
        /// <summary>Sentinel used by <see cref="ActorId"/> and <see cref="TargetId"/> when no unit is involved.</summary>
        public const int NoUnit = -1;

        private readonly int turn;
        private readonly BattleEventType type;
        private readonly int actorId;
        private readonly int targetId;
        private readonly GridCoord from;
        private readonly GridCoord to;
        private readonly float amount;
        private readonly bool wasCrit;
        private readonly bool wasEvaded;
        private readonly bool wasMissed;
        private readonly string detail;

        /// <summary>
        /// Creates an event. Prefer the named factory methods, which fill in the correct sentinels.
        /// </summary>
        /// <param name="turn">AI turn number, starting at 1. Zero for pre-battle events.</param>
        /// <param name="type">What happened.</param>
        /// <param name="actorId">Unit that acted, or <see cref="NoUnit"/>.</param>
        /// <param name="targetId">Unit acted upon, or <see cref="NoUnit"/>.</param>
        /// <param name="from">Origin cell for movement events.</param>
        /// <param name="to">Destination cell for movement events.</param>
        /// <param name="amount">Damage, healing, or a numeric payload.</param>
        /// <param name="wasCrit">Attack resolution flag.</param>
        /// <param name="wasEvaded">Attack resolution flag.</param>
        /// <param name="wasMissed">Attack resolution flag.</param>
        /// <param name="detail">Free-form deterministic text, e.g. a modifier description or an outcome name.</param>
        public BattleEvent(
            int turn,
            BattleEventType type,
            int actorId,
            int targetId,
            GridCoord from,
            GridCoord to,
            float amount,
            bool wasCrit,
            bool wasEvaded,
            bool wasMissed,
            string detail)
        {
            this.turn = turn;
            this.type = type;
            this.actorId = actorId;
            this.targetId = targetId;
            this.from = from;
            this.to = to;
            this.amount = amount;
            this.wasCrit = wasCrit;
            this.wasEvaded = wasEvaded;
            this.wasMissed = wasMissed;
            this.detail = detail ?? string.Empty;
        }

        /// <summary>AI turn number, starting at 1.</summary>
        public int Turn
        {
            get { return turn; }
        }

        /// <summary>What happened.</summary>
        public BattleEventType Type
        {
            get { return type; }
        }

        /// <summary>Id of the unit that acted, or <see cref="NoUnit"/>.</summary>
        public int ActorId
        {
            get { return actorId; }
        }

        /// <summary>Id of the unit acted upon, or <see cref="NoUnit"/>.</summary>
        public int TargetId
        {
            get { return targetId; }
        }

        /// <summary>Origin cell for movement events.</summary>
        public GridCoord From
        {
            get { return from; }
        }

        /// <summary>Destination cell for movement events.</summary>
        public GridCoord To
        {
            get { return to; }
        }

        /// <summary>Damage dealt, health restored, or a numeric payload. Zero when not applicable.</summary>
        public float Amount
        {
            get { return amount; }
        }

        /// <summary>True when the recorded attack was a critical hit.</summary>
        public bool WasCrit
        {
            get { return wasCrit; }
        }

        /// <summary>True when the recorded attack was dodged.</summary>
        public bool WasEvaded
        {
            get { return wasEvaded; }
        }

        /// <summary>True when the recorded attack failed its accuracy roll.</summary>
        public bool WasMissed
        {
            get { return wasMissed; }
        }

        /// <summary>Free-form deterministic text. Never null.</summary>
        public string Detail
        {
            get { return detail ?? string.Empty; }
        }

        /// <summary>An AI turn began.</summary>
        /// <param name="turn">Turn number.</param>
        public static BattleEvent TurnStarted(int turn)
        {
            return new BattleEvent(turn, BattleEventType.TurnStarted, NoUnit, NoUnit, GridCoord.Zero, GridCoord.Zero, 0f, false, false, false, null);
        }

        /// <summary>An AI turn ended; <paramref name="outcome"/> is the post-turn evaluation.</summary>
        /// <param name="turn">Turn number.</param>
        /// <param name="outcome">Outcome evaluated at the end of this turn.</param>
        public static BattleEvent TurnEnded(int turn, BattleOutcome outcome)
        {
            return new BattleEvent(turn, BattleEventType.TurnEnded, NoUnit, NoUnit, GridCoord.Zero, GridCoord.Zero, 0f, false, false, false, outcome.ToString());
        }

        /// <summary>The battle reached a terminal outcome.</summary>
        /// <param name="turn">Turn number the battle ended on.</param>
        /// <param name="outcome">Terminal outcome.</param>
        public static BattleEvent BattleEnded(int turn, BattleOutcome outcome)
        {
            return new BattleEvent(turn, BattleEventType.BattleEnded, NoUnit, NoUnit, GridCoord.Zero, GridCoord.Zero, 0f, false, false, false, outcome.ToString());
        }

        /// <summary>A modifier was applied to a unit.</summary>
        /// <param name="turn">Turn number.</param>
        /// <param name="unitId">Unit receiving the modifier.</param>
        /// <param name="modifier">The modifier applied.</param>
        public static BattleEvent ModifierApplied(int turn, int unitId, StatModifier modifier)
        {
            return new BattleEvent(turn, BattleEventType.ModifierApplied, unitId, NoUnit, GridCoord.Zero, GridCoord.Zero, 0f, false, false, false, modifier.ToString());
        }

        /// <summary>A unit regenerated health from terrain.</summary>
        /// <param name="turn">Turn number.</param>
        /// <param name="unitId">Unit healed.</param>
        /// <param name="healed">Health actually restored.</param>
        /// <param name="terrainName">Terrain that granted the regeneration.</param>
        public static BattleEvent HpRegenerated(int turn, int unitId, float healed, string terrainName)
        {
            return new BattleEvent(turn, BattleEventType.HpRegenerated, unitId, NoUnit, GridCoord.Zero, GridCoord.Zero, healed, false, false, false, terrainName);
        }

        /// <summary>A unit stepped one cell.</summary>
        /// <param name="turn">Turn number.</param>
        /// <param name="unitId">Unit that moved.</param>
        /// <param name="from">Cell left.</param>
        /// <param name="to">Cell entered.</param>
        public static BattleEvent UnitMoved(int turn, int unitId, GridCoord from, GridCoord to)
        {
            return new BattleEvent(turn, BattleEventType.UnitMoved, unitId, NoUnit, from, to, 0f, false, false, false, null);
        }

        /// <summary>A unit initiated an attack.</summary>
        /// <param name="turn">Turn number.</param>
        /// <param name="attackerId">Attacking unit.</param>
        /// <param name="targetId">Target unit.</param>
        /// <param name="attackerCell">Attacker's cell.</param>
        /// <param name="targetCell">Target's cell.</param>
        public static BattleEvent UnitAttacked(int turn, int attackerId, int targetId, GridCoord attackerCell, GridCoord targetCell)
        {
            return new BattleEvent(turn, BattleEventType.UnitAttacked, attackerId, targetId, attackerCell, targetCell, 0f, false, false, false, null);
        }

        /// <summary>An attack resolved.</summary>
        /// <param name="turn">Turn number.</param>
        /// <param name="attackerId">Attacking unit.</param>
        /// <param name="targetId">Target unit.</param>
        /// <param name="result">How the attack resolved.</param>
        /// <param name="applied">Health actually removed, which may be less than the rolled damage.</param>
        public static BattleEvent DamageDealt(int turn, int attackerId, int targetId, DamageResult result, float applied)
        {
            return new BattleEvent(turn, BattleEventType.DamageDealt, attackerId, targetId, GridCoord.Zero, GridCoord.Zero, applied, result.WasCrit, result.WasEvaded, result.WasMissed, null);
        }

        /// <summary>A unit died.</summary>
        /// <param name="turn">Turn number.</param>
        /// <param name="unitId">Unit that died.</param>
        /// <param name="killerId">Unit that landed the killing blow, or <see cref="NoUnit"/>.</param>
        /// <param name="cell">Cell the unit died on.</param>
        public static BattleEvent UnitDied(int turn, int unitId, int killerId, GridCoord cell)
        {
            return new BattleEvent(turn, BattleEventType.UnitDied, unitId, killerId, cell, cell, 0f, false, false, false, null);
        }

        /// <summary>
        /// A stable, culture-invariant one-line rendering. This is the canonical form the
        /// determinism tests compare, so any change to it changes what "identical log" means.
        /// </summary>
        public override string ToString()
        {
            StringBuilder builder = new StringBuilder(96);
            builder.Append('T').Append(turn.ToString(CultureInfo.InvariantCulture));
            builder.Append(' ').Append(type.ToString());
            builder.Append(" a=").Append(actorId.ToString(CultureInfo.InvariantCulture));
            builder.Append(" t=").Append(targetId.ToString(CultureInfo.InvariantCulture));
            builder.Append(" from=").Append(from.ToString());
            builder.Append(" to=").Append(to.ToString());
            builder.Append(" amt=").Append(amount.ToString("0.####", CultureInfo.InvariantCulture));
            builder.Append(" crit=").Append(wasCrit ? '1' : '0');
            builder.Append(" eva=").Append(wasEvaded ? '1' : '0');
            builder.Append(" miss=").Append(wasMissed ? '1' : '0');

            string text = Detail;
            if (text.Length > 0)
            {
                builder.Append(" [").Append(text).Append(']');
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// What one AI turn produced: its events and the outcome evaluated at its end.
    /// </summary>
    public sealed class BattleTurnResult
    {
        private readonly int turnNumber;
        private readonly BattleEvent[] events;
        private readonly BattleOutcome outcome;
        private readonly int katipunanAlive;
        private readonly int spanishAlive;

        /// <summary>Creates a turn result.</summary>
        /// <param name="turnNumber">Turn number, starting at 1.</param>
        /// <param name="events">Events produced this turn, in order. Copied.</param>
        /// <param name="outcome">Outcome evaluated at the end of the turn.</param>
        /// <param name="katipunanAlive">Living Katipunan units after the turn.</param>
        /// <param name="spanishAlive">Living Spanish units after the turn.</param>
        public BattleTurnResult(int turnNumber, IReadOnlyList<BattleEvent> events, BattleOutcome outcome, int katipunanAlive, int spanishAlive)
        {
            this.turnNumber = turnNumber;
            this.outcome = outcome;
            this.katipunanAlive = katipunanAlive;
            this.spanishAlive = spanishAlive;

            if (events == null)
            {
                this.events = new BattleEvent[0];
            }
            else
            {
                this.events = new BattleEvent[events.Count];
                for (int i = 0; i < events.Count; i++)
                {
                    this.events[i] = events[i];
                }
            }
        }

        /// <summary>Turn number, starting at 1.</summary>
        public int TurnNumber
        {
            get { return turnNumber; }
        }

        /// <summary>Events produced this turn, in order.</summary>
        public IReadOnlyList<BattleEvent> Events
        {
            get { return events; }
        }

        /// <summary>Outcome evaluated at the end of this turn.</summary>
        public BattleOutcome Outcome
        {
            get { return outcome; }
        }

        /// <summary>Living Katipunan units after this turn.</summary>
        public int KatipunanAlive
        {
            get { return katipunanAlive; }
        }

        /// <summary>Living Spanish units after this turn.</summary>
        public int SpanishAlive
        {
            get { return spanishAlive; }
        }
    }

    /// <summary>
    /// The result of running a battle to completion: the outcome, how long it took, and the full
    /// event log the presentation layer replays.
    /// </summary>
    public sealed class BattleResult
    {
        private readonly BattleOutcome outcome;
        private readonly int turnsElapsed;
        private readonly BattleEvent[] events;
        private readonly int katipunanAlive;
        private readonly int spanishAlive;

        /// <summary>Creates a battle result.</summary>
        /// <param name="outcome">Terminal outcome.</param>
        /// <param name="turnsElapsed">Number of AI turns executed.</param>
        /// <param name="events">Every event, in order. Copied.</param>
        /// <param name="katipunanAlive">Surviving Katipunan units.</param>
        /// <param name="spanishAlive">Surviving Spanish units.</param>
        public BattleResult(BattleOutcome outcome, int turnsElapsed, IReadOnlyList<BattleEvent> events, int katipunanAlive, int spanishAlive)
        {
            this.outcome = outcome;
            this.turnsElapsed = turnsElapsed;
            this.katipunanAlive = katipunanAlive;
            this.spanishAlive = spanishAlive;

            if (events == null)
            {
                this.events = new BattleEvent[0];
            }
            else
            {
                this.events = new BattleEvent[events.Count];
                for (int i = 0; i < events.Count; i++)
                {
                    this.events[i] = events[i];
                }
            }
        }

        /// <summary>Terminal outcome of the battle.</summary>
        public BattleOutcome Outcome
        {
            get { return outcome; }
        }

        /// <summary>Number of AI turns executed.</summary>
        public int TurnsElapsed
        {
            get { return turnsElapsed; }
        }

        /// <summary>Every event, in order.</summary>
        public IReadOnlyList<BattleEvent> Events
        {
            get { return events; }
        }

        /// <summary>Surviving Katipunan units.</summary>
        public int KatipunanAlive
        {
            get { return katipunanAlive; }
        }

        /// <summary>Surviving Spanish units.</summary>
        public int SpanishAlive
        {
            get { return spanishAlive; }
        }

        /// <summary>
        /// Renders the whole log as newline-separated lines. This is the canonical text form used to
        /// compare two runs for determinism, and is the thing to diff when a replay desyncs.
        /// </summary>
        public string ToLogText()
        {
            StringBuilder builder = new StringBuilder(events.Length * 64);
            for (int i = 0; i < events.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(events[i].ToString());
            }

            return builder.ToString();
        }

        /// <summary>Short summary line, culture-invariant.</summary>
        public override string ToString()
        {
            return outcome
                + " after " + turnsElapsed.ToString(CultureInfo.InvariantCulture) + " turns"
                + " (Katipunan " + katipunanAlive.ToString(CultureInfo.InvariantCulture)
                + " / Spanish " + spanishAlive.ToString(CultureInfo.InvariantCulture) + " alive)";
        }
    }
}
