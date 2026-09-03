namespace BinakayanRising.Data
{
    /// <summary>
    /// The "Tactician's Command" gameplay effect granted alongside the Reales payout when the
    /// player answers a pop-up quiz question correctly.
    /// </summary>
    /// <remarks>
    /// The members below are exactly the effects that appear in the reward column of
    /// <b>Capstone Table 4: Sample Historical Trivia Database (Educational Module)</b>.
    /// Every sample row pays +50 Reales and then adds one of these effects.
    /// </remarks>
    public enum QuizRewardEffect
    {
        /// <summary>No gameplay effect; the question pays its Reales reward only.</summary>
        None = 0,

        /// <summary>
        /// Heals every surviving Katipunan unit on the map.
        /// Table 4 sample row 1: "+50 Reales, Map-wide +10% HP Heal".
        /// </summary>
        MapWideHeal = 1,

        /// <summary>
        /// Temporarily raises the attack of every surviving Katipunan unit.
        /// Table 4 sample row 2: "+50 Reales, +10% Attack Buff for 1 turn".
        /// </summary>
        AttackBuff = 2,

        /// <summary>
        /// Returns every enemy unit to its spawn cell.
        /// Table 4 sample row 3: "+50 Reales, Resets AI Enemy positions".
        /// </summary>
        ResetEnemyPositions = 3,

        /// <summary>
        /// Brings one defeated friendly unit back into the battle.
        /// Table 4 sample row 4: "+50 Reales, Instantly revives 1 fallen unit".
        /// </summary>
        ReviveFallenUnit = 4
    }
}
