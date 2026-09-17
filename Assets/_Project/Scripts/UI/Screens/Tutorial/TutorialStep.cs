using BinakayanRising.Core.Localization;

namespace BinakayanRising.UI.Screens.Tutorial
{
    /// <summary>What moves the tutorial on from a step.</summary>
    public enum TutorialAdvance
    {
        /// <summary>The Next button on the card.</summary>
        Next,

        /// <summary>A HUD control whose anchor id starts with <see cref="TutorialStep.ControlPrefix"/>.</summary>
        Control,

        /// <summary>A unit being set down on the board.</summary>
        UnitPlaced,

        /// <summary>The assault beginning, by button or by Space.</summary>
        CombatStarted
    }

    /// <summary>Something the spotlight lights during a step.</summary>
    public struct TutorialTarget
    {
        /// <summary>The whole deployment zone on the board.</summary>
        public const string DeployZone = "board.deploy";

        /// <summary>The whole board.</summary>
        public const string Board = "board.all";

        /// <summary>A HUD anchor id, or one of the board ids above.</summary>
        public string Id;

        /// <summary>Whether clicks inside the lit area reach what is underneath.</summary>
        public bool PassThrough;

        public TutorialTarget(string id, bool passThrough)
        {
            Id = id;
            PassThrough = passThrough;
        }
    }

    /// <summary>One card of the guided tutorial, as data.</summary>
    /// <remarks>
    /// Steps describe what to light, what to say and what the player may touch; the director turns
    /// that into overlay calls and input locks. Keeping them as data is what lets the whole script
    /// be read top to bottom in <see cref="TutorialScript"/>.
    /// </remarks>
    public sealed class TutorialStep
    {
        public TextKey Title;
        public TextKey Body;
        public TutorialTarget[] Targets = new TutorialTarget[0];
        public TutorialAdvance Advance = TutorialAdvance.Next;

        /// <summary>For <see cref="TutorialAdvance.Control"/>: the anchor id, or its prefix.</summary>
        public string ControlPrefix;

        /// <summary>Centre the card instead of placing it beside the first target.</summary>
        public bool Centred;

        /// <summary>Offer English and Filipino buttons on the card.</summary>
        public bool ShowLanguages;

        /// <summary>Freeze the replay while this card is up.</summary>
        public bool PauseReplay;

        /// <summary>Allow placing and lifting units on the board.</summary>
        public bool BoardInput;

        /// <summary>Allow zooming and panning the camera.</summary>
        public bool CameraInput;

        /// <summary>Hotkeys that work during this step. Esc always skips.</summary>
        public HudHotkey[] Hotkeys = new HudHotkey[0];

        /// <summary>Reframe the board when leaving the step, undoing any zoom the step invited.</summary>
        public bool ReframeOnExit;

        /// <summary>
        /// Wait for the battle to resolve before showing this card, with the overlay out of the way
        /// so the replay can be watched.
        /// </summary>
        public bool WaitForResult;
    }
}
