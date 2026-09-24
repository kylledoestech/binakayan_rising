using System;
using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Grid;

namespace BinakayanRising.Gameplay
{
    /// <summary>
    /// A campaign battle handed to <see cref="BattlePlaytest"/>: the player's own roster and the
    /// quest's rules in place of the prototype's fixed scenario.
    /// </summary>
    /// <remarks>
    /// The shell fills one in and sets <see cref="BattlePlaytest.PendingMission"/> before creating
    /// the battle; the battle takes it in its <c>Awake</c>. Without one the battle is the standalone
    /// playtest, unchanged.
    /// </remarks>
    public sealed class MissionSetup
    {
        /// <summary>The quest id, for logs and screenshots.</summary>
        public string QuestId;

        /// <summary>The quest title in the player's language, for the top bar.</summary>
        public string Title;

        /// <summary>The player's rank title, shown under the quest title.</summary>
        public string RankTitle;

        /// <summary>The player's units, in roster order. Ids are the save's unit ids.</summary>
        public List<RosterEntry> Roster = new List<RosterEntry>();

        /// <summary>How many Spanish regulars advance when <see cref="Enemies"/> is null.</summary>
        public int EnemyCount = 6;

        /// <summary>The Spanish column, one archetype id per unit (#16), or null for <see cref="EnemyCount"/> regulars.</summary>
        public IReadOnlyList<string> Enemies;

        /// <summary>The quest's win rule (#37, #38). Decides the simulator's objective.</summary>
        public WinRule WinRule = WinRule.Rout;

        /// <summary>Most units the player may deploy.</summary>
        public int SquadCap = 6;

        public int Seed = 1896;

        /// <summary>Turns before the battle is called.</summary>
        public int TurnCap = 120;

        /// <summary>True when surviving to the turn cap wins (the Hold rule): a draw counts.</summary>
        public bool HoldWins;

        /// <summary>Runs the guided battle tutorial.</summary>
        public bool Tutorial;

        /// <summary>The turn one historical question is asked at, or 0 for none.</summary>
        public int QuizTurn;

        /// <summary>The campaign level, so the quiz asks about the right part of the story.</summary>
        public int Level = 1;

        /// <summary>
        /// The Kapatiran bonds at the ranks the player's pairs have earned (#19), or null for the
        /// standalone playtest's rank-A set.
        /// </summary>
        public List<KapatiranBond> Bonds;

        /// <summary>Called once when the player leaves the finished battle.</summary>
        public Action<MissionReport> Finished;

        /// <summary>Whether <paramref name="outcome"/> wins under this mission's rule.</summary>
        public bool IsWin(BattleOutcome outcome)
        {
            return outcome == BattleOutcome.Victory || (HoldWins && outcome == BattleOutcome.Draw);
        }

        /// <summary>The Spanish column as archetype ids: <see cref="Enemies"/>, or that many regulars.</summary>
        public IReadOnlyList<string> EnemyList()
        {
            if (Enemies != null && Enemies.Count > 0)
            {
                return Enemies;
            }

            var regulars = new List<string>();
            int count = EnemyCount < 1 ? 1 : (EnemyCount > 14 ? 14 : EnemyCount);
            for (int i = 0; i < count; i++)
            {
                regulars.Add(UnitCatalog.SpanishRegular);
            }

            return regulars;
        }
    }

    /// <summary>How a campaign battle ended, handed back to the shell.</summary>
    public sealed class MissionReport
    {
        public bool Won;
        public BattleOutcome Outcome;

        /// <summary>The save ids of every unit the player deployed.</summary>
        public List<int> Deployed = new List<int>();

        /// <summary>Where each deployed unit stood, by save id: what Kapatiran support is earned from.</summary>
        public List<KeyValuePair<int, GridCoord>> Placements = new List<KeyValuePair<int, GridCoord>>();

        /// <summary>True when the player left before fighting: nothing is settled.</summary>
        public bool Retreated;
    }
}
