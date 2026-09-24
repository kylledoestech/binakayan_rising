using BinakayanRising.Gameplay;
using UnityEngine.InputSystem;

namespace BinakayanRising.UI.Screens
{
    public sealed partial class BattleHud
    {
        /// <summary>
        /// Reads every keyboard shortcut in one place, so a single key press is handled once.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Esc means the nearest thing to "get me out". Spreading that across several components
        /// had each of them act on the same press, so the order is fixed here, first match wins:
        /// </para>
        /// <list type="number">
        /// <item>A question card or cutscene is up: the HUD reads nothing; they own the keyboard.</item>
        /// <item>The pause menu is up (or closed this very frame): the HUD reads nothing. The menu
        /// closes itself on Esc or P, and Settings opened from it closes itself on Esc first.</item>
        /// <item>The How-to-Play deck is open: Esc closes it.</item>
        /// <item>The tutorial is in the foreground: Esc skips it.</item>
        /// <item>The replay is running: Esc skips it to the result.</item>
        /// <item>Otherwise (deploying, or the battle resolved): Esc opens the pause menu.</item>
        /// </list>
        /// <para>
        /// P opens the pause menu at any of the last three steps, so the replay itself can be paused
        /// without giving up Esc's skip.
        /// </para>
        /// </remarks>
        private void ReadHotkeys()
        {
            Keyboard keyboard = Keyboard.current;
            // A question card owns the keyboard: its 1 to 4 answer, not change the speed.
            if (keyboard == null || BinakayanRising.UI.Shell.QuizCard.Current != null
                || BinakayanRising.UI.Shell.CutscenePlayer.Current != null
                || BinakayanRising.UI.Shell.PauseMenu.Current != null
                || BinakayanRising.UI.Shell.PauseMenu.ClosedFrame == UnityEngine.Time.frameCount)
            {
                return;
            }

            // Right-click closes the deck, as Esc does, unless something drawn above it is
            // under the pointer (the settings panel or a modal card).
            if (deck != null && deck.IsOpen && UiPointer.TryClaimRightClick(BinakayanRising.UI.Kit.Theme.Layer.Deck))
            {
                deck.Close();
                return;
            }

            if (keyboard.mKey.wasPressedThisFrame && (deck == null || !deck.IsOpen))
            {
                ToggleMinimap();
                return;
            }

            if (deck != null && deck.IsOpen)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    HandleHotkey(HudHotkey.Escape);
                }
                else if (keyboard.leftArrowKey.wasPressedThisFrame)
                {
                    deck.Turn(-1);
                }
                else if (keyboard.rightArrowKey.wasPressedThisFrame)
                {
                    deck.Turn(1);
                }

                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                HandleHotkey(HudHotkey.Escape);
            }
            else if (keyboard.pKey.wasPressedThisFrame)
            {
                HandleHotkey(HudHotkey.Pause);
            }
            else if (keyboard.spaceKey.wasPressedThisFrame)
            {
                HandleHotkey(HudHotkey.Assault);
            }
            else if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            {
                HandleHotkey(HudHotkey.SpeedSlow);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            {
                HandleHotkey(HudHotkey.SpeedNormal);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            {
                HandleHotkey(HudHotkey.SpeedFast);
            }
        }

        /// <summary>Performs a hotkey's action, if the current filter allows it.</summary>
        /// <returns>True when the key did something.</returns>
        public bool HandleHotkey(HudHotkey key)
        {
            // The open deck owns the keyboard: Esc closes it and nothing reaches the battle behind.
            if (deck != null && deck.IsOpen)
            {
                if (key != HudHotkey.Escape)
                {
                    return false;
                }

                deck.Close();
                return true;
            }

            // While the tutorial has stepped aside to let the replay play, Esc keeps its usual
            // meaning and skips the replay, which brings the last card up.
            if (key == HudHotkey.Escape && tutorial != null && tutorial.IsRunning && !tutorial.IsWaitingOffscreen)
            {
                tutorial.Skip();
                return true;
            }

            if (HotkeyFilter != null && !HotkeyFilter(key))
            {
                return false;
            }

            switch (key)
            {
                case HudHotkey.Assault:
                    if (battle.CurrentPhase != BattlePlaytest.Phase.Deployment || battle.PlacementCount == 0)
                    {
                        return false;
                    }

                    BeginAssault();
                    return true;

                case HudHotkey.SpeedSlow:
                    SetSpeedStep(0);
                    return true;

                case HudHotkey.SpeedNormal:
                    SetSpeedStep(1);
                    return true;

                case HudHotkey.SpeedFast:
                    SetSpeedStep(2);
                    return true;

                case HudHotkey.Escape:
                    if (battle.CurrentPhase == BattlePlaytest.Phase.Combat)
                    {
                        SkipReplay();
                        return true;
                    }

                    return OpenPauseMenu();

                case HudHotkey.Pause:
                    return OpenPauseMenu();
            }

            return false;
        }

        /// <summary>
        /// Opens the pause menu over the battle. Leaving from it is refused while the tutorial
        /// runs, as the top bar's Retreat is.
        /// </summary>
        /// <returns>True when the menu opened.</returns>
        public bool OpenPauseMenu()
        {
            bool canLeave = tutorial == null || !tutorial.IsRunning;
            return BinakayanRising.UI.Shell.PauseMenu.Show(battle, canLeave) != null;
        }
    }
}
