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
        /// Esc means the nearest thing to "get me out": close the deck if it is open, else leave the
        /// tutorial, else skip the replay. Spreading that across three components had each of them
        /// act on the same press.
        /// </remarks>
        private void ReadHotkeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
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
                    if (battle.CurrentPhase != BattlePlaytest.Phase.Combat)
                    {
                        return false;
                    }

                    SkipReplay();
                    return true;
            }

            return false;
        }
    }
}
