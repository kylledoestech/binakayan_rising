using System;
using System.Collections.Generic;
using BinakayanRising.Core.Grid;

namespace BinakayanRising.Core.Combat
{
    /// <summary>
    /// The Tactician's Command half of the simulator: a command queued between two turns takes
    /// effect at the start of the next one, right after its <see cref="BattleEventType.TurnStarted"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kept in its own file so the turn loop stays readable. The loop calls in at three points:
    /// the constructor records every unit's starting cell, the turn start applies a queued command,
    /// and the modifier rebuild re-adds any timed command buff still running.
    /// </para>
    /// <para>
    /// Nothing here draws from the random stream, so issuing a command never shifts the dice of
    /// what came before it, and a battle with a command stays exactly as reproducible as one
    /// without. See <see cref="TacticianCommands.RunWithCommand"/> for how a pre-simulated battle
    /// takes one.
    /// </para>
    /// </remarks>
    public sealed partial class BattleSimulator
    {
        /// <summary>A command's stat bonus with the last turn it applies on.</summary>
        private struct TimedCommandModifier
        {
            public Team Team;
            public StatModifier Modifier;
            public int LastTurn;
        }

        private readonly Dictionary<int, GridCoord> startCells = new Dictionary<int, GridCoord>();
        private readonly List<TimedCommandModifier> commandModifiers = new List<TimedCommandModifier>();

        private TacticianCommand queuedCommand = TacticianCommand.None;
        private float queuedMagnitude;
        private int queuedDuration;

        /// <summary>The command waiting for the next turn, or <see cref="TacticianCommand.None"/>.</summary>
        public TacticianCommand QueuedCommand
        {
            get { return queuedCommand; }
        }

        /// <summary>Where a unit stood when the battle began: its deployment or spawn cell.</summary>
        /// <param name="unitId">The unit.</param>
        /// <param name="cell">Its starting cell.</param>
        /// <returns>False for an unknown id.</returns>
        public bool TryGetStartCell(int unitId, out GridCoord cell)
        {
            return startCells.TryGetValue(unitId, out cell);
        }

        /// <summary>
        /// Whether <paramref name="command"/> would do anything right now: the heal and the buff
        /// need a living Katipunan unit, the reset a living enemy, the revive a fallen Katipunan unit.
        /// </summary>
        public bool CanIssue(TacticianCommand command)
        {
            switch (command)
            {
                case TacticianCommand.MapWideHeal:
                    return CountAlive(Team.Katipunan) > 0;
                case TacticianCommand.AttackBuff:
                    return HasLivingFighter(Team.Katipunan);
                case TacticianCommand.ResetEnemyPositions:
                    return CountAlive(Team.Spanish) > 0;
                case TacticianCommand.ReviveFallenUnit:
                    return LastFallen(Team.Katipunan) != null;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Queues a command for the start of the next turn, replacing any command already queued.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="magnitude">Fraction, e.g. 0.10 for Table 4's "+10%".</param>
        /// <param name="durationTurns">Turns a timed command lasts; at least 1.</param>
        /// <exception cref="InvalidOperationException">Thrown when the battle has already finished.</exception>
        public void QueueCommand(TacticianCommand command, float magnitude, int durationTurns)
        {
            if (IsFinished)
            {
                throw new InvalidOperationException("A finished battle takes no commands.");
            }

            queuedCommand = command;
            queuedMagnitude = magnitude;
            queuedDuration = durationTurns < 1 ? 1 : durationTurns;
        }

        private void RecordStartCells()
        {
            for (int i = 0; i < unitsInIdOrder.Length; i++)
            {
                startCells[unitsInIdOrder[i].Id] = unitsInIdOrder[i].Position;
            }
        }

        /// <summary>Carries out the queued command, if any, at the top of the turn.</summary>
        private void ApplyQueuedCommand(List<BattleEvent> turnEvents)
        {
            TacticianCommand command = queuedCommand;
            queuedCommand = TacticianCommand.None;
            if (command == TacticianCommand.None)
            {
                return;
            }

            Emit(turnEvents, BattleEvent.CommandIssued(turnNumber, command, queuedMagnitude));

            switch (command)
            {
                case TacticianCommand.MapWideHeal:
                    CommandHeal(queuedMagnitude, turnEvents);
                    break;
                case TacticianCommand.AttackBuff:
                    commandModifiers.Add(new TimedCommandModifier
                    {
                        Team = Team.Katipunan,
                        Modifier = StatModifier.Percent(StatKind.AttackDamage, queuedMagnitude, ModifierSource.Quiz, TacticianCommands.SourceTag),
                        LastTurn = turnNumber + queuedDuration - 1
                    });
                    break;
                case TacticianCommand.ResetEnemyPositions:
                    CommandResetEnemies(turnEvents);
                    break;
                case TacticianCommand.ReviveFallenUnit:
                    CommandRevive(queuedMagnitude, turnEvents);
                    break;
            }
        }

        /// <summary>Re-adds every command buff still running; called after terrain and Kapatiran.</summary>
        private void ApplyCommandModifiers(List<BattleEvent> turnEvents)
        {
            for (int c = 0; c < commandModifiers.Count; c++)
            {
                TimedCommandModifier timed = commandModifiers[c];
                if (turnNumber > timed.LastTurn)
                {
                    continue;
                }

                for (int i = 0; i < livingScratch.Count; i++)
                {
                    CombatUnit unit = livingScratch[i];
                    if (!unit.IsAlive || unit.Team != timed.Team || unit.Abilities.NonCombatant)
                    {
                        continue;
                    }

                    unit.Modifiers.Add(timed.Modifier);
                    if (logModifierEvents)
                    {
                        Emit(turnEvents, BattleEvent.ModifierApplied(turnNumber, unit.Id, timed.Modifier));
                    }
                }
            }
        }

        /// <summary>Every living Katipunan unit regains a share of its Max HP.</summary>
        private void CommandHeal(float fraction, List<BattleEvent> turnEvents)
        {
            for (int i = 0; i < unitsInIdOrder.Length; i++)
            {
                CombatUnit unit = unitsInIdOrder[i];
                if (!unit.IsAlive || unit.Team != Team.Katipunan)
                {
                    continue;
                }

                float healed = unit.Heal(unit.GetEffectiveStat(StatKind.MaxHP) * fraction);
                if (healed > 0f)
                {
                    Emit(turnEvents, BattleEvent.HpRegenerated(turnNumber, unit.Id, healed, TacticianCommands.SourceTag));
                }
            }
        }

        /// <summary>
        /// Every living enemy goes back to the cell it started on, in id order. One whose cell is
        /// taken goes to the nearest free walkable cell instead.
        /// </summary>
        private void CommandResetEnemies(List<BattleEvent> turnEvents)
        {
            HashSet<GridCoord> taken = new HashSet<GridCoord>();
            for (int i = 0; i < unitsInIdOrder.Length; i++)
            {
                CombatUnit unit = unitsInIdOrder[i];
                if (unit.IsAlive && unit.Team != Team.Spanish)
                {
                    taken.Add(unit.Position);
                }
            }

            for (int i = 0; i < unitsInIdOrder.Length; i++)
            {
                CombatUnit enemy = unitsInIdOrder[i];
                if (!enemy.IsAlive || enemy.Team != Team.Spanish)
                {
                    continue;
                }

                GridCoord home;
                if (!startCells.TryGetValue(enemy.Id, out home))
                {
                    home = enemy.Position;
                }

                GridCoord destination;
                if (!TryNearestFree(home, taken, false, out destination))
                {
                    destination = enemy.Position;
                }

                taken.Add(destination);
                if (destination != enemy.Position)
                {
                    GridCoord from = enemy.Position;
                    enemy.MoveTo(destination);
                    Emit(turnEvents, BattleEvent.UnitMoved(turnNumber, enemy.Id, from, destination));
                }
            }
        }

        /// <summary>
        /// The Katipunan unit that fell most recently comes back on its deployment cell, or the
        /// nearest free deployable cell, or failing that the nearest free cell of any kind.
        /// </summary>
        private void CommandRevive(float fraction, List<BattleEvent> turnEvents)
        {
            CombatUnit fallen = LastFallen(Team.Katipunan);
            if (fallen == null)
            {
                return;
            }

            HashSet<GridCoord> taken = new HashSet<GridCoord>();
            for (int i = 0; i < unitsInIdOrder.Length; i++)
            {
                if (unitsInIdOrder[i].IsAlive)
                {
                    taken.Add(unitsInIdOrder[i].Position);
                }
            }

            // A revive is never a free win: under Sabotage the target cell is off limits.
            if (objective.Kind == ObjectiveKind.Sabotage)
            {
                taken.Add(objective.TargetCell);
            }

            GridCoord home;
            if (!startCells.TryGetValue(fallen.Id, out home))
            {
                home = fallen.Position;
            }

            GridCoord cell;
            if (!TryNearestFree(home, taken, true, out cell))
            {
                return;
            }

            float health = fallen.BaseStats.MaxHP * fraction;
            if (fallen.Revive(health, cell))
            {
                Emit(turnEvents, BattleEvent.UnitRevived(turnNumber, fallen.Id, cell, fallen.CurrentHP));
            }
        }

        /// <summary>True when <paramref name="team"/> has a living unit that fights (not a supply cart).</summary>
        private bool HasLivingFighter(Team team)
        {
            for (int i = 0; i < unitsInIdOrder.Length; i++)
            {
                CombatUnit unit = unitsInIdOrder[i];
                if (unit.IsAlive && unit.Team == team && !unit.Abilities.NonCombatant)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The fighting unit of <paramref name="team"/> whose death is latest in the log and who is
        /// still dead. A non-combatant — the escorted cart — is never brought back: its loss has
        /// already ended the battle.
        /// </summary>
        private CombatUnit LastFallen(Team team)
        {
            for (int e = allEvents.Count - 1; e >= 0; e--)
            {
                BattleEvent battleEvent = allEvents[e];
                if (battleEvent.Type != BattleEventType.UnitDied)
                {
                    continue;
                }

                CombatUnit unit = FindUnit(battleEvent.ActorId);
                if (unit != null && unit.Team == team && !unit.IsAlive && !unit.Abilities.NonCombatant)
                {
                    return unit;
                }
            }

            return null;
        }

        /// <summary>
        /// The free walkable cell closest to <paramref name="preferred"/> (itself when free).
        /// Ties go to the lower row, then the lower column, so the choice is deterministic. With
        /// <paramref name="deployableFirst"/>, any free deployable cell beats any other cell.
        /// </summary>
        private bool TryNearestFree(GridCoord preferred, HashSet<GridCoord> taken, bool deployableFirst, out GridCoord cell)
        {
            cell = preferred;
            bool found = false;
            int bestTier = int.MaxValue;
            int bestDistance = int.MaxValue;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    GridCoord candidate = new GridCoord(x, y);
                    if (!grid.IsWalkable(candidate) || taken.Contains(candidate))
                    {
                        continue;
                    }

                    int tier = candidate == preferred ? 0 : (deployableFirst && !grid.IsDeployable(candidate) ? 2 : 1);
                    int distance = GridDistance.Manhattan(candidate, preferred);
                    if (tier < bestTier || (tier == bestTier && distance < bestDistance))
                    {
                        bestTier = tier;
                        bestDistance = distance;
                        cell = candidate;
                        found = true;
                    }
                }
            }

            return found;
        }
    }
}
