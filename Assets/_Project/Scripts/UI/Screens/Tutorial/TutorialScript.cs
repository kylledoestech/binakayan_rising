using System.Collections.Generic;
using BinakayanRising.Core.Localization;

namespace BinakayanRising.UI.Screens.Tutorial
{
    /// <summary>
    /// The guided first battle: nine cards from the goal to the result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The battle is rigged to be won, and that was checked, not assumed.</b> The tutorial clears
    /// the board, fields three Spanish regulars instead of six, and fixes the seed. Seed 1896 was
    /// chosen by running the real simulator offline against every tile the Marksman can be placed on
    /// in step 3, with auto-deploy filling the rest in step 6: all eleven placements end in victory.
    /// A first battle lost after doing exactly what the tutorial said teaches the wrong lesson.
    /// </para>
    /// <para>
    /// If the scenario, the roster or the combat rules change, re-run that check before trusting
    /// these two numbers again.
    /// </para>
    /// </remarks>
    public static class TutorialScript
    {
        /// <summary>Seed the tutorial battle is resolved from.</summary>
        public const int Seed = 1896;

        /// <summary>Spanish regulars in the tutorial battle.</summary>
        public const int SpanishCount = 3;

        /// <summary>Builds the steps in order.</summary>
        public static List<TutorialStep> Build()
        {
            return new List<TutorialStep>
            {
                new TutorialStep
                {
                    Title = TextKey.TutWelcomeTitle,
                    Body = TextKey.TutWelcomeBody,
                    Centred = true,
                    ShowLanguages = true,
                },
                new TutorialStep
                {
                    Title = TextKey.TutPickTitle,
                    Body = TextKey.TutPickBody,
                    // The side panel's row or the strip's portrait: both pick the Marksman, and
                    // pressing the portrait to drag it counts as picking it.
                    Targets = new[] { new TutorialTarget("roster.0", true), new TutorialTarget("strip.0", true) },
                    Advance = TutorialAdvance.Control,
                    ControlPrefix = "roster.",
                },
                new TutorialStep
                {
                    Title = TextKey.TutPlaceTitle,
                    Body = TextKey.TutPlaceBody,
                    // The strip stays reachable so the Marksman can still be dragged over.
                    Targets = new[]
                    {
                        new TutorialTarget(TutorialTarget.DeployZone, true),
                        new TutorialTarget("strip.0", true),
                    },
                    Advance = TutorialAdvance.UnitPlaced,
                    BoardInput = true,
                },
                new TutorialStep
                {
                    Title = TextKey.TutTerrainTitle,
                    Body = TextKey.TutTerrainBody,

                    // Lit and pass-through so the wheel reaches the camera; board clicks are locked
                    // so trying the zoom cannot also lift the unit just placed.
                    Targets = new[] { new TutorialTarget(TutorialTarget.Board, true) },
                    CameraInput = true,
                    ReframeOnExit = true,
                },
                new TutorialStep
                {
                    Title = TextKey.TutBondsTitle,
                    Body = TextKey.TutBondsBody,
                    Targets = new[]
                    {
                        new TutorialTarget("roster", false),
                        new TutorialTarget(TutorialTarget.DeployZone, false),
                    },
                },
                new TutorialStep
                {
                    Title = TextKey.TutAutoTitle,
                    Body = TextKey.TutAutoBody,
                    Targets = new[] { new TutorialTarget("deploy.auto", true) },
                    Advance = TutorialAdvance.Control,
                    ControlPrefix = "deploy.auto",
                },
                new TutorialStep
                {
                    Title = TextKey.TutAssaultTitle,
                    Body = TextKey.TutAssaultBody,
                    Targets = new[] { new TutorialTarget("deploy.assault", true) },
                    Advance = TutorialAdvance.CombatStarted,
                    Hotkeys = new[] { HudHotkey.Assault },
                },
                new TutorialStep
                {
                    Title = TextKey.TutWatchTitle,
                    Body = TextKey.TutWatchBody,
                    Targets = new[]
                    {
                        new TutorialTarget("top.speed", true),
                        new TutorialTarget("log", false),
                    },
                    PauseReplay = true,
                    Hotkeys = new[] { HudHotkey.SpeedSlow, HudHotkey.SpeedNormal, HudHotkey.SpeedFast },
                },
                new TutorialStep
                {
                    Title = TextKey.TutOutcomeTitle,
                    Body = TextKey.TutOutcomeBody,
                    Targets = new[]
                    {
                        new TutorialTarget("outcome.card", false),
                        new TutorialTarget("top.help", false),
                    },
                    WaitForResult = true,
                },
            };
        }
    }
}
