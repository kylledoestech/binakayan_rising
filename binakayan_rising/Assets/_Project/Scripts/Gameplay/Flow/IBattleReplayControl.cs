using System;
using BinakayanRising.Core.Combat;

namespace BinakayanRising.Gameplay.Flow
{
    /// <summary>
    /// The seam between the flow layer and the presentation component that animates a finished
    /// <see cref="BattleResult"/> event log.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Implemented by <c>BinakayanRising.Gameplay.Presentation.BattleReplayer</c></b>, which owns
    /// the sprites, the tweens, the pacing and the camera. The flow layer needs four verbs — start,
    /// pause, resume, skip — and two notifications, and nothing else.
    /// </para>
    /// <para>
    /// <b>Pause is the quiz freeze.</b> The capstone document requires the Quiz state to freeze
    /// combat physics and timers. <see cref="Pause"/> is that freeze on the replay side: after it
    /// returns, no further <see cref="TurnEnded"/> may be raised until <see cref="Resume"/> is
    /// called. It must therefore be safe to call from inside a <see cref="TurnEnded"/> handler.
    /// </para>
    /// </remarks>
    public interface IBattleReplayControl
    {
        /// <summary>True between <see cref="StartReplay"/> and <see cref="BattleFinished"/>.</summary>
        bool IsPlaying { get; }

        /// <summary>True while playback is frozen by <see cref="Pause"/>.</summary>
        bool IsPaused { get; }

        /// <summary>
        /// Raised once per replayed AI turn, after the last event of that turn has been shown.
        /// </summary>
        /// <remarks>
        /// The first argument is the 1-based turn number; the second is the outcome the simulator
        /// evaluated at the end of that turn, which is
        /// <see cref="BattleOutcome.InProgress"/> for every turn but the last. This is the hook the
        /// document's "the system calculates remaining unit HP at the end of every AI turn" check
        /// and the mid-combat quiz trigger both hang off.
        /// </remarks>
        event Action<int, BattleOutcome> TurnEnded;

        /// <summary>
        /// Raised once when playback reaches the end of the log, or immediately after
        /// <see cref="SkipToEnd"/>. Carries the same <see cref="BattleResult"/> that was passed to
        /// <see cref="StartReplay"/>.
        /// </summary>
        event Action<BattleResult> BattleFinished;

        /// <summary>
        /// Begins animating a finished battle from turn one.
        /// </summary>
        /// <remarks>
        /// The implementation is expected to already hold the unit roster it built while
        /// constructing the simulator, so the log's unit ids resolve to scene objects. Calling this
        /// while a replay is running restarts playback.
        /// </remarks>
        /// <param name="result">The finished battle to animate. Never null.</param>
        void StartReplay(BattleResult result);

        /// <summary>
        /// Freezes playback where it stands: no further events are shown and no timer advances.
        /// Calling it while already paused does nothing.
        /// </summary>
        void Pause();

        /// <summary>
        /// Continues playback from wherever <see cref="Pause"/> stopped it. Calling it while not
        /// paused does nothing.
        /// </summary>
        void Resume();

        /// <summary>
        /// Abandons the animation, snaps every unit to its final state, and raises
        /// <see cref="BattleFinished"/> immediately. Used by a "skip battle" button and by the flow
        /// layer when the player leaves combat early.
        /// </summary>
        void SkipToEnd();
    }
}
