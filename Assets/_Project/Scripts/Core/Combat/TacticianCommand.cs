using System;

namespace BinakayanRising.Core.Combat
{
    /// <summary>
    /// A "Tactician's Command": the battlefield half of a correct mid-battle quiz answer
    /// (Capstone Table 4). The player picks one after the Reales are paid.
    /// </summary>
    /// <remarks>
    /// The values match <c>BinakayanRising.Data.QuizRewardEffect</c> one for one, so the two convert
    /// with a cast. Core cannot reference the Data assembly, which is why the enum is repeated here.
    /// </remarks>
    public enum TacticianCommand
    {
        /// <summary>No command.</summary>
        None = 0,

        /// <summary>Table 4 row 1: "Map-wide +10% HP Heal" for every living Katipunan unit.</summary>
        MapWideHeal = 1,

        /// <summary>Table 4 row 2: "+10% Attack Buff for 1 turn" for every living Katipunan unit.</summary>
        AttackBuff = 2,

        /// <summary>Table 4 row 3: "Resets AI Enemy positions" — every living enemy back to its starting cell.</summary>
        ResetEnemyPositions = 3,

        /// <summary>Table 4 row 4: "Instantly revives 1 fallen unit".</summary>
        ReviveFallenUnit = 4
    }

    /// <summary>
    /// The numbers behind each <see cref="TacticianCommand"/>, and the one way a command reaches a
    /// battle that has already been simulated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a re-simulation.</b> The battle is resolved in full before the replay starts, so a
    /// command picked at the quiz turn cannot be "applied" to anything live. Instead the battle is
    /// run again from its deployment with the same seed: every turn before the quiz reproduces the
    /// original log exactly (the simulator is deterministic), the command lands at the start of the
    /// quiz turn, and everything after it is resolved afresh. The replay keeps its place, because
    /// the log it has already shown is byte-identical, and simply continues on the new tail.
    /// </para>
    /// <para>
    /// TODO(design): Table 4 gives "+10%" for the heal and the attack buff and nothing else. The
    /// revive's health share and the buff's length ("1 turn", read as the quiz turn itself) are
    /// this build's choices.
    /// </para>
    /// </remarks>
    public static class TacticianCommands
    {
        /// <summary>Share of each unit's Max HP the map-wide heal restores. Table 4: +10%.</summary>
        public const float HealFraction = 0.10f;

        /// <summary>Attack Damage bonus of the buff. Table 4: +10%.</summary>
        public const float AttackFraction = 0.10f;

        /// <summary>AI turns the attack buff lasts. Table 4: "for 1 turn".</summary>
        public const int AttackTurns = 1;

        /// <summary>Share of its Max HP a revived unit comes back with.</summary>
        public const float ReviveFraction = 0.50f;

        /// <summary>Source tag on the attack buff's modifier, for logs.</summary>
        public const string SourceTag = "Tactician's Command";

        /// <summary>The magnitude a command is issued at by default.</summary>
        public static float DefaultMagnitude(TacticianCommand command)
        {
            switch (command)
            {
                case TacticianCommand.MapWideHeal:
                    return HealFraction;
                case TacticianCommand.AttackBuff:
                    return AttackFraction;
                case TacticianCommand.ReviveFallenUnit:
                    return ReviveFraction;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Runs a battle again from its deployment with <paramref name="command"/> issued at the
        /// start of <paramref name="turn"/>, and returns the new result.
        /// </summary>
        /// <param name="build">
        /// Builds a fresh simulator for the same battle: the same grid, units in their deployment
        /// cells, config and seed. Called exactly once.
        /// </param>
        /// <param name="turn">The turn the command opens; 1 or more.</param>
        /// <param name="command">The command.</param>
        /// <param name="magnitude">Its magnitude, e.g. <see cref="HealFraction"/>.</param>
        /// <param name="durationTurns">Turns a timed command lasts.</param>
        /// <returns>
        /// The whole battle. Its log up to and including <c>TurnStarted(turn)</c> is identical to
        /// the log of the battle without the command. When the battle ended before
        /// <paramref name="turn"/>, it is returned unchanged.
        /// </returns>
        public static BattleResult RunWithCommand(
            Func<BattleSimulator> build, int turn, TacticianCommand command, float magnitude, int durationTurns)
        {
            if (build == null)
            {
                throw new ArgumentNullException(nameof(build));
            }

            BattleSimulator simulator = build();
            while (!simulator.IsFinished && simulator.TurnNumber < turn - 1)
            {
                simulator.ExecuteTurn();
            }

            if (!simulator.IsFinished)
            {
                simulator.QueueCommand(command, magnitude, durationTurns);
            }

            return simulator.RunToCompletion();
        }
    }
}
