using System;
using System.Collections.Generic;
using BinakayanRising.Core.Combat;
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

        public int EnemyCount = 6;

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
