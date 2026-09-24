using System.Text;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using BinakayanRising.Gameplay;

namespace BinakayanRising.UI.Screens
{
    /// <summary>
    /// Turns the battle's language-free records — archetypes, outcomes, field report entries —
    /// into sentences in the current language.
    /// </summary>
    /// <remarks>
    /// Everything here allocates, so callers format on change, never per frame: the field report
    /// when its version or the language moves, a unit name when a row is bound.
    /// </remarks>
    public static class BattleText
    {
        /// <summary>A unit's full name in the current language.</summary>
        /// <param name="archetypeId">Archetype, e.g. "Marksman".</param>
        /// <param name="ordinal">Numbering among same-archetype units; used by Spanish regulars.</param>
        /// <param name="fallback">Returned when the archetype has no localized name.</param>
        public static string UnitName(string archetypeId, int ordinal, string fallback = null)
        {
            switch (archetypeId)
            {
                case "Marksman":
                    return Loc.Get(TextKey.UnitMarksman);
                case "Engineer":
                    return Loc.Get(TextKey.UnitEngineer);
                case "Evangelista":
                    return Loc.Get(TextKey.UnitEvangelista);
                case "Aguinaldo":
                    return Loc.Get(TextKey.UnitAguinaldo);
                case "Vanguard":
                    return Loc.Get(TextKey.UnitVanguard);
                case "SpanishRegular":
                    return Loc.Format(TextKey.UnitSpanishRegular, ordinal);
                case "SpanishArtillery":
                    return Loc.Format(TextKey.UnitSpanishArtillery, ordinal);
                case "SpanishCazador":
                    return Loc.Format(TextKey.UnitSpanishCazador, ordinal);
                case "SpanishOfficer":
                    return Loc.Format(TextKey.UnitSpanishOfficer, ordinal);
                case "SpanishMarine":
                    return Loc.Format(TextKey.UnitSpanishMarine, ordinal);
                case PlaytestScenario.SupplyCartArchetype:
                    return Loc.Get(TextKey.UnitSupplyCart);
                default:
                    return fallback ?? archetypeId ?? string.Empty;
            }
        }

        /// <summary>
        /// The standing order for a battle fought under an objective (#37, #38), or null under Rout
        /// and Hold, whose goal the phase readout already implies.
        /// </summary>
        public static string Objective(WinRule rule, int turnCap)
        {
            switch (rule)
            {
                case WinRule.Escort:
                    return Loc.Format(TextKey.HudObjectiveEscort, turnCap);
                case WinRule.Sabotage:
                    return Loc.Format(TextKey.HudObjectiveSabotage, turnCap);
                default:
                    return null;
            }
        }

        /// <summary>The outcome's name: Victory, Defeat or Draw.</summary>
        public static string Outcome(BattleOutcome outcome)
        {
            switch (outcome)
            {
                case BattleOutcome.Victory:
                    return Loc.Get(TextKey.OutcomeVictory);
                case BattleOutcome.Defeat:
                    return Loc.Get(TextKey.OutcomeDefeat);
                default:
                    return Loc.Get(TextKey.OutcomeDraw);
            }
        }

        /// <summary>The phase readout for the top bar.</summary>
        public static string Phase(BattlePlaytest.Phase phase, int turn)
        {
            switch (phase)
            {
                case BattlePlaytest.Phase.Deployment:
                    return Loc.Get(TextKey.PhaseDeployment);
                case BattlePlaytest.Phase.Combat:
                    return Loc.Format(TextKey.PhaseCombat, turn);
                default:
                    return Loc.Get(TextKey.PhaseResolved);
            }
        }

        /// <summary>Appends one field report line, prefixed with its turn.</summary>
        public static void AppendReportLine(StringBuilder into, BattlePlaytest.FieldReportEntry entry)
        {
            into.Append('T').Append(entry.Turn).Append("  ");

            switch (entry.Kind)
            {
                case BattlePlaytest.FieldReportKind.AssaultBegan:
                    into.Append(Loc.Get(TextKey.LogAssaultBegan));
                    break;

                case BattlePlaytest.FieldReportKind.UnitRouted:
                    into.Append(Loc.Format(TextKey.LogRouted, UnitName(entry.ActorArchetypeId, entry.ActorOrdinal)));
                    break;

                case BattlePlaytest.FieldReportKind.CriticalHit:
                    into.Append(Loc.Format(
                        TextKey.LogCriticalHit,
                        UnitName(entry.ActorArchetypeId, entry.ActorOrdinal),
                        UnitName(entry.TargetArchetypeId, entry.TargetOrdinal)));
                    break;

                case BattlePlaytest.FieldReportKind.BattleResolved:
                    into.Append(Loc.Format(TextKey.LogResolved, Outcome(entry.Outcome), entry.TurnsElapsed));
                    break;
            }
        }
    }
}
