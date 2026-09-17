using System;
using System.Collections.Generic;
using BinakayanRising.Core.Localization;
using BinakayanRising.Gameplay;
using BinakayanRising.UI.Kit;
using UnityEngine;

namespace BinakayanRising.UI.Screens.Tutorial
{
    /// <summary>
    /// Runs the guided tutorial: sets up the teaching battle, walks the player through
    /// <see cref="TutorialScript"/>, and hands the game back exactly as it found it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Shown once.</b> It starts on its own the first time a battle opens and records that in
    /// <see cref="UserPrefs.TutorialDone"/> whether it is finished or skipped. After that it only
    /// runs when the player asks for it from the How-to-Play deck.
    /// </para>
    /// <para>
    /// <b>Restores what it borrowed.</b> The teaching battle changes the seed, the Spanish column and
    /// the replay speed. All three are put back when the tutorial ends, however it ends, so the
    /// player's next battle is the normal one.
    /// </para>
    /// <para>
    /// <b>Never strands the player.</b> Esc leaves at any step. Leaving during deployment with an
    /// empty board deploys the roster, so the screen behind the tutorial is always playable.
    /// </para>
    /// </remarks>
    [AddComponentMenu("")]
    public sealed class TutorialDirector : MonoBehaviour
    {
        private List<TutorialStep> steps;
        private BattleHud hud;
        private BattlePlaytest battle;
        private SpotlightOverlay overlay;

        private int stepIndex = -1;
        private bool running;
        private bool waiting;

        private int savedSeed;
        private int savedSpanishCount;
        private float savedSpeed;

        /// <summary>True while the tutorial is in progress, including while it waits out a replay.</summary>
        public bool IsRunning => running;

        /// <summary>True while the overlay is hidden so the replay can be watched.</summary>
        public bool IsWaitingOffscreen => running && waiting;

        /// <summary>Zero-based index of the current step, or -1.</summary>
        public int StepIndex => running ? stepIndex : -1;

        /// <summary>How many steps the tutorial has.</summary>
        public int StepCount => steps != null ? steps.Count : 0;

        /// <summary>The overlay, for inspection.</summary>
        public SpotlightOverlay Overlay => overlay;

        /// <summary>Attaches to a HUD. Called once, by the HUD that owns this.</summary>
        public void Bind(BattleHud owner)
        {
            hud = owner;
            battle = owner.Battle;
            steps = TutorialScript.Build();
            overlay = SpotlightOverlay.Create(transform, "Tutorial", Theme.Layer.Tutorial);
        }

        /// <summary>Starts the tutorial if it has never been finished or skipped.</summary>
        public void BeginIfFirstRun()
        {
            if (!UserPrefs.TutorialDone)
            {
                Begin();
            }
        }

        /// <summary>Starts, or restarts, the tutorial from the first step.</summary>
        public void Begin()
        {
            if (hud == null)
            {
                return;
            }

            if (running)
            {
                End(markDone: false, restore: true);
            }

            savedSeed = battle.Seed;
            savedSpanishCount = battle.SpanishCount;
            savedSpeed = battle.Speed;

            if (battle.CurrentPhase != BattlePlaytest.Phase.Deployment)
            {
                battle.RequestRedeploy();
            }

            battle.RequestClearDeployment();
            battle.SetSeed(TutorialScript.Seed);
            battle.SetSpanishCount(TutorialScript.SpanishCount);
            battle.SetSpeed(1f);
            battle.FrameBoard(true);

            running = true;
            waiting = false;

            hud.ControlActivated += OnControlActivated;
            hud.HotkeyFilter = AllowHotkey;
            battle.UnitPlaced += OnUnitPlaced;
            battle.PhaseChanged += OnPhaseChanged;
            Loc.LanguageChanged += OnLanguageChanged;

            Canvas.ForceUpdateCanvases();
            EnterStep(0);
        }

        /// <summary>Leaves the tutorial and marks it seen.</summary>
        public void Skip()
        {
            if (running)
            {
                UiSfx.Play(UiSfx.Cue.Close);
                End(markDone: true, restore: true);
            }
        }

        /// <summary>Moves to the next step, as the Next button does.</summary>
        public void Next()
        {
            if (running && !waiting)
            {
                Advance();
            }
        }

        private void OnDestroy()
        {
            if (running)
            {
                Unsubscribe();
            }
        }

        // ------------------------------------------------------------------ steps

        private TutorialStep Current => stepIndex >= 0 && stepIndex < steps.Count ? steps[stepIndex] : null;

        private void EnterStep(int index)
        {
            stepIndex = index;
            TutorialStep step = steps[index];

            if (step.WaitForResult && battle.CurrentPhase != BattlePlaytest.Phase.Finished)
            {
                // The replay is the lesson here, so the overlay steps aside until it is over.
                waiting = true;
                overlay.SetVisible(false);
                battle.SetPaused(false);
                battle.SetBoardInputLocked(true);
                battle.SetCameraInputLocked(false);
                return;
            }

            waiting = false;
            battle.SetPaused(step.PauseReplay);
            battle.SetBoardInputLocked(!step.BoardInput);
            battle.SetCameraInputLocked(!step.CameraInput);

            overlay.ClearTargets();
            for (int i = 0; i < step.Targets.Length; i++)
            {
                overlay.AddTarget(Resolve(step.Targets[i].Id), step.Targets[i].PassThrough);
            }

            RenderCard();
            overlay.SetVisible(true);
            Canvas.ForceUpdateCanvases();
            overlay.Refresh();
            UiSfx.Play(UiSfx.Cue.Open);
        }

        private void RenderCard()
        {
            TutorialStep step = Current;
            if (step == null)
            {
                return;
            }

            bool last = stepIndex == steps.Count - 1;
            string primary = step.Advance == TutorialAdvance.Next
                ? Loc.Get(last ? TextKey.TutDone : TextKey.TutNext)
                : null;

            overlay.SetCard(
                Loc.Format(TextKey.TutStep, stepIndex + 1, steps.Count),
                Loc.Get(step.Title),
                Loc.Get(step.Body),
                primary,
                Next,
                last ? null : Loc.Get(TextKey.TutSkip),
                Skip,
                step.Centred,
                step.ShowLanguages);
        }

        private void Advance()
        {
            TutorialStep step = Current;
            if (step != null && step.ReframeOnExit)
            {
                battle.FrameBoard(false);
            }

            int next = stepIndex + 1;
            if (next >= steps.Count)
            {
                UiSfx.Play(UiSfx.Cue.Confirm);
                End(markDone: true, restore: true);
                return;
            }

            EnterStep(next);
        }

        private Func<Rect> Resolve(string id)
        {
            switch (id)
            {
                case TutorialTarget.DeployZone:
                    return () => hud.WorldScreenRect(battle.DeployZoneWorldRect);
                case TutorialTarget.Board:
                    return () => hud.WorldScreenRect(battle.BoardWorldRect);
                default:
                    return () => hud.AnchorScreenRect(id);
            }
        }

        // ------------------------------------------------------------------ events

        private void OnControlActivated(string anchorId)
        {
            TutorialStep step = Current;
            if (waiting || step == null || step.Advance != TutorialAdvance.Control || string.IsNullOrEmpty(step.ControlPrefix))
            {
                return;
            }

            if (anchorId != null && anchorId.StartsWith(step.ControlPrefix, StringComparison.Ordinal))
            {
                Advance();
            }
        }

        private void OnUnitPlaced()
        {
            TutorialStep step = Current;
            if (!waiting && step != null && step.Advance == TutorialAdvance.UnitPlaced)
            {
                Advance();
            }
        }

        private void OnPhaseChanged(BattlePlaytest.Phase phase)
        {
            TutorialStep step = Current;
            if (step == null)
            {
                return;
            }

            if (waiting)
            {
                if (phase == BattlePlaytest.Phase.Finished)
                {
                    EnterStep(stepIndex);
                }
                else if (phase == BattlePlaytest.Phase.Deployment)
                {
                    // Redeployed mid-replay: the player has taken over, which is the point.
                    End(markDone: true, restore: true);
                }

                return;
            }

            if (step.Advance == TutorialAdvance.CombatStarted && phase == BattlePlaytest.Phase.Combat)
            {
                Advance();
            }
        }

        private void OnLanguageChanged()
        {
            if (running && !waiting)
            {
                RenderCard();
            }
        }

        private bool AllowHotkey(HudHotkey key)
        {
            if (!running)
            {
                return true;
            }

            if (waiting)
            {
                return key != HudHotkey.Assault;
            }

            TutorialStep step = Current;
            return step != null && Array.IndexOf(step.Hotkeys, key) >= 0;
        }

        // ------------------------------------------------------------------ end

        private void End(bool markDone, bool restore)
        {
            running = false;
            waiting = false;
            stepIndex = -1;

            Unsubscribe();
            overlay.SetVisible(false);
            overlay.ClearTargets();

            battle.SetPaused(false);
            battle.SetBoardInputLocked(false);
            battle.SetCameraInputLocked(false);

            if (restore)
            {
                battle.SetSeed(savedSeed);
                battle.SetSpanishCount(savedSpanishCount);
                battle.SetSpeed(savedSpeed);

                if (battle.CurrentPhase == BattlePlaytest.Phase.Deployment && battle.PlacementCount == 0)
                {
                    battle.RequestAutoDeploy();
                }
            }

            if (markDone)
            {
                UserPrefs.TutorialDone = true;
            }
        }

        private void Unsubscribe()
        {
            if (hud != null)
            {
                hud.ControlActivated -= OnControlActivated;
                if (hud.HotkeyFilter == (Func<HudHotkey, bool>)AllowHotkey)
                {
                    hud.HotkeyFilter = null;
                }
            }

            if (battle != null)
            {
                battle.UnitPlaced -= OnUnitPlaced;
                battle.PhaseChanged -= OnPhaseChanged;
            }

            Loc.LanguageChanged -= OnLanguageChanged;
        }
    }
}
