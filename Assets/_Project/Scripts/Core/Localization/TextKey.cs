namespace BinakayanRising.Core.Localization
{
    /// <summary>
    /// Every player-facing string in the battle slice. The text itself lives in
    /// <see cref="StringTable"/>.
    /// </summary>
    /// <remarks>
    /// Not persisted anywhere, so members may be reordered and inserted freely.
    /// <see cref="Count"/> must stay last: the table sizes its arrays from it.
    /// </remarks>
    public enum TextKey
    {
        None,

        // HUD — top bar
        HudSeed,
        HudSpeed,
        HudSkip,
        HudRedeploy,

        // HUD — phase readout
        PhaseDeployment,
        PhaseCombat,
        PhaseResolved,

        // HUD — deployment panel
        DeployTitle,
        DeployHint,
        RosterStats,
        RosterDeployed,
        SpanishColumn,
        AutoDeploy,
        BeginAssault,
        ShowTips,
        HideTips,
        TipsBody,

        // HUD — combat panel
        OrderOfBattle,
        TeamKatipunan,
        TeamSpanish,
        FieldReport,

        // HUD — outcome card
        OutcomeVictory,
        OutcomeDefeat,
        OutcomeDraw,
        OutcomeTurns,
        OutcomeSurvivors,
        OutcomeSeed,
        NewSeed,

        // Unit names
        UnitMarksman,
        UnitEngineer,
        UnitEvangelista,
        UnitAguinaldo,
        UnitVanguard,
        UnitSpanishRegular,

        // Field report and floating numbers
        LogAssaultBegan,
        LogRouted,
        LogCriticalHit,
        LogResolved,
        PopupDodge,
        PopupMiss,

        // Guided tutorial
        TutNext,
        TutSkip,
        TutDone,
        TutStep,
        TutWelcomeTitle,
        TutWelcomeBody,
        TutPickTitle,
        TutPickBody,
        TutPlaceTitle,
        TutPlaceBody,
        TutTerrainTitle,
        TutTerrainBody,
        TutBondsTitle,
        TutBondsBody,
        TutAutoTitle,
        TutAutoBody,
        TutAssaultTitle,
        TutAssaultBody,
        TutWatchTitle,
        TutWatchBody,
        TutOutcomeTitle,
        TutOutcomeBody,

        // How-to-Play deck
        DeckTitle,
        DeckBack,
        DeckNext,
        DeckClose,
        DeckReplay,
        DeckBattleTitle,
        DeckBattleBody,
        DeckDeployTitle,
        DeckDeployBody,
        DeckUnitsTitle,
        DeckUnitsBody,
        DeckTerrainTitle,
        DeckTerrainBody,
        DeckBondsTitle,
        DeckBondsBody,
        DeckCombatTitle,
        DeckCombatBody,
        DeckControlsTitle,
        DeckControlsBody,
        TerrainTrench,
        TerrainTent,
        TerrainShallows,
        TerrainBamboo,

        Count
    }
}
