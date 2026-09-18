namespace BinakayanRising.Gameplay.Flow
{
    /// <summary>
    /// Every state in the capstone document's <b>Figure 2: Game State Machine Diagram</b>, including
    /// the sub-states of the two composite states.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The diagram, transcribed:
    /// </para>
    /// <code>
    /// (start) --Launch Application--&gt; MainMenuState
    /// MainMenuState --Load Save / New Game--&gt; EncampmentState
    /// EncampmentState (composite):
    ///     (start) --&gt; BaseHub
    ///     BaseHub --Farm / Mine--&gt; ResourceManagement
    ///     BaseHub --Gacha System--&gt; HeroSummoning
    ///     BaseHub --Upgrade Units--&gt; RosterTraining
    /// EncampmentState --Select Stage--&gt; MissionPortal
    /// MissionPortal --Load Isometric Map--&gt; DeploymentState
    /// DeploymentState --Lock Formation &amp; Start--&gt; CombatState
    /// CombatState (composite):
    ///     (start) --&gt; AI_Pathfinding
    ///     AI_Pathfinding &lt;--&gt; DamageCalculation   [loop until stage clear or defeat]
    /// CombatState --Mid-Combat or End-Stage Trigger--&gt; QuizState
    /// QuizState --Reward Virtual Currency--&gt; EncampmentState
    /// CombatState/DeploymentState --Defeat (Restart)--&gt; EncampmentState
    /// </code>
    /// <para>
    /// <b>Victory and Defeat</b> appear in the document as screen overlays rather than boxes on the
    /// diagram — "the system calculates remaining unit HP at the end of every AI turn to trigger the
    /// Victory/Defeat screen". They are modelled as states here so the overlay has something to be
    /// driven by and so that the return to the Encampment is a legal, checked transition rather than
    /// an ad-hoc scene load.
    /// </para>
    /// <para>
    /// <b>The Combat sub-states are deliberately absent.</b> <c>AI_Pathfinding</c> and
    /// <c>DamageCalculation</c> alternate entirely inside one call to
    /// <see cref="BinakayanRising.Core.Combat.BattleSimulator.ExecuteTurn"/>; they are phases of a
    /// turn, not states the player or the UI can ever observe. Promoting them to enum members would
    /// invite code to poll for a state that is only ever true for microseconds inside a headless
    /// resolver.
    /// </para>
    /// </remarks>
    public enum GameState
    {
        /// <summary>Title screen. The state the application launches into.</summary>
        MainMenu = 0,

        /// <summary>
        /// The Encampment composite state. Transient: entering it immediately enters its initial
        /// sub-state, <see cref="BaseHub"/>.
        /// </summary>
        Encampment = 1,

        /// <summary>Encampment sub-state: the hub screen every facility is reached from.</summary>
        BaseHub = 2,

        /// <summary>Encampment sub-state: the Farm and the Mines. Reached by "Farm / Mine".</summary>
        ResourceManagement = 3,

        /// <summary>Encampment sub-state: the Reales gacha. Reached by "Gacha System".</summary>
        HeroSummoning = 4,

        /// <summary>Encampment sub-state: unit upgrades and synthesis. Reached by "Upgrade Units".</summary>
        RosterTraining = 5,

        /// <summary>Stage select. Reached from the Encampment by "Select Stage".</summary>
        MissionPortal = 6,

        /// <summary>
        /// The isometric deployment board. Reached by "Load Isometric Map". Player input is live
        /// here and nowhere in combat.
        /// </summary>
        Deployment = 7,

        /// <summary>
        /// The autonomous auto-battler. Reached by "Lock Formation &amp; Start". All player input is
        /// locked for this state's entire duration.
        /// </summary>
        Combat = 8,

        /// <summary>
        /// The historical trivia pop-up. Freezes combat physics and timers while it is open.
        /// Reached by the "Mid-Combat or End-Stage Trigger".
        /// </summary>
        Quiz = 9,

        /// <summary>Victory overlay, raised when the end-of-turn HP check routs every enemy.</summary>
        Victory = 10,

        /// <summary>Defeat overlay, raised when the end-of-turn HP check wipes out the player's roster.</summary>
        Defeat = 11,

        /// <summary>
        /// Not in Figure 2: the Armory, where the player sees what they own and equips and
        /// reforges weapons. A leaf of the Encampment, like the Farm/Mine screen.
        /// </summary>
        Inventory = 12
    }
}
