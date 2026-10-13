using System.Collections;
using BinakayanRising.Core.Content;
using BinakayanRising.UI.Camp;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// The "campview" route: Esc and the scroll wheel in the camp (GDD p. 38), driven through the
    /// input system the way a keyboard and mouse drive them, and measured off the live camera.
    /// </summary>
    /// <remarks>
    /// Esc: the hub opens the pause menu, Quit Game asks first, and Esc never does two things at
    /// once (the Training screen's lore closing and the screen backing out on one press, or a
    /// panel backing out and the menu opening). Zoom: whole pixel scales, the point under the
    /// cursor staying put, the view never leaving the framing, and a walking figure followed.
    /// </remarks>
    public sealed partial class ShotAutopilot
    {
        private IEnumerator CampViewRoute()
        {
            Shell.Session.DeleteSave();
            Shell.StartNewCampaign();
            yield return Wait(0.6f);
            yield return ClearCards("start");
            yield return Wait(0.5f);

            yield return EscapeChecks();
            yield return ZoomChecks();
        }

        // ------------------------------------------------------------------ Esc

        private IEnumerator EscapeChecks()
        {
            yield return PressEscape();
            if (PauseMenu.Current == null)
            {
                Note("esc", "Esc in the hub opened no pause menu");
                yield break;
            }

            string[] buttons = { "Button Resume", "Button Settings", "Button Main Menu", "Button Quit Game" };
            foreach (string name in buttons)
            {
                if (FindButton(PauseMenu.Current, name) == null)
                {
                    Note("esc", "the camp pause menu has no '" + name + "'");
                }
            }

            yield return Shot("cv_01_pause", 0.5f);

            PressIn(PauseMenu.Current, "Button Quit Game", "esc quit");
            yield return Wait(0.5f);
            GameObject confirm = GameObject.Find("Confirm QuitConfirmTitle");
            if (confirm == null)
            {
                Note("esc", "Quit Game did not ask first");
            }
            else
            {
                yield return Shot("cv_02_quit_confirm", 0.3f);
                yield return PressEscape();
                if (PauseMenu.Current == null)
                {
                    Note("esc", "Esc under the quit prompt closed the menu beneath it");
                }

                PressIn(confirm.transform, "Button Cancel", "esc quit cancel");
                yield return Wait(0.3f);
            }

            yield return PressEscape();
            if (PauseMenu.Current != null)
            {
                Note("esc", "Esc did not close the pause menu");
                PauseMenu.Current.Close();
            }

            yield return Wait(0.3f);
            if (PauseMenu.Current != null)
            {
                Note("esc", "the Esc that closed the menu opened it again");
            }

            // The Training screen, its lore list, then Esc twice: one thing per press.
            Shell.Camp.ClickSite(Places.Training);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            yield return ClearCards("training");
            yield return WaitWhile(() => !(Shell.Router.Current is TrainingScreen), 4f);
            if (!(Shell.Router.Current is TrainingScreen training))
            {
                Note("esc", "the Training screen did not open");
                yield break;
            }

            yield return Wait(0.4f);
            PressIn(training, "Button Lore", "esc lore");
            yield return WaitWhile(() => BondLorePanel.Current == null, 2f);
            yield return Wait(0.4f);
            yield return PressEscape();
            if (BondLorePanel.Current != null)
            {
                Note("esc", "Esc did not close the lore list");
            }

            if (!(Shell.Router.Current is TrainingScreen))
            {
                Note("esc", "the Esc that closed the lore list also closed the Training screen");
            }

            yield return Wait(0.3f);
            yield return PressEscape();
            yield return Wait(0.4f);
            if (Shell.Router.Current is TrainingScreen)
            {
                Note("esc", "Esc did not back out of the Training screen");
            }

            if (PauseMenu.Current != null)
            {
                Note("esc", "the Esc that backed out of Training also opened the pause menu");
                PauseMenu.Current.Close();
            }

            Line("esc: hub menu, quit prompt, lore and panel each took one press");
            yield return Wait(0.5f);
        }

        /// <summary>One press and release of Esc, through the input system.</summary>
        private static IEnumerator PressEscape()
        {
            Keyboard keys = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keys, new KeyboardState(Key.Escape));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keys, new KeyboardState());
            yield return null;
            yield return null;
        }

        // ------------------------------------------------------------------ zoom

        private IEnumerator ZoomChecks()
        {
            CampWorld camp = Shell.Camp;
            Camera eye = camp.View;
            Vector3 framedPosition = eye.transform.position;
            float framedSize = eye.orthographicSize;
            float framedPpu = Screen.height / (2f * framedSize);
            Rect framed = OnScreenWorld(eye);
            Line(string.Format("zoom 0: {0:0} px per unit, view {1}", framedPpu, framed));
            yield return Shot("cv_03_zoom0", 0.3f);

            // Anchor on the Mission Tent's door, well away from the view's centre.
            Vector2 anchor = eye.WorldToScreenPoint(CampIso.ToWorld(Encampment.Site(Places.MissionTent).Door));
            for (int step = 1; step <= CampWorld.MaxZoomSteps + 1; step++)
            {
                Vector3 under = eye.ScreenToWorldPoint(new Vector3(anchor.x, anchor.y, 0f));
                yield return Scroll(anchor, 1f);
                int expected = Mathf.Min(step, CampWorld.MaxZoomSteps);
                if (camp.ZoomSteps != expected)
                {
                    Note("zoom", "after " + step + " notches in, zoom step " + camp.ZoomSteps + ", expected " + expected);
                }

                float ppu = Screen.height / (2f * eye.orthographicSize);
                float wantPpu = CampIso.PixelsPerUnit * (Mathf.Floor(framedPpu / CampIso.PixelsPerUnit) + expected);
                Vector2 drift = (Vector2)eye.WorldToScreenPoint(under) - anchor;
                Rect seen = OnScreenWorld(eye);
                Vector3 at = eye.transform.position;
                float offX = Mathf.Abs((at.x * ppu) - Mathf.Round(at.x * ppu));
                float offY = Mathf.Abs((at.y * ppu) - Mathf.Round(at.y * ppu));
                Line(string.Format("zoom {0}: {1:0.##} px per unit (want {2:0}), anchor drift {3:0.#} px, view {4}, pixel offset {5:0.###}/{6:0.###}",
                    camp.ZoomSteps, ppu, wantPpu, drift.magnitude, seen, offX, offY));

                if (Mathf.Abs(ppu - wantPpu) > 0.01f)
                {
                    Note("zoom", string.Format("step {0} draws {1:0.##} px per unit, not a whole scale ({2:0})", camp.ZoomSteps, ppu, wantPpu));
                }

                if (!Inside(seen, framed, 1f / ppu))
                {
                    Note("zoom", "step " + camp.ZoomSteps + " shows " + seen + ", outside the framing " + framed);
                }

                if (offX > 0.01f || offY > 0.01f)
                {
                    Note("zoom", "step " + camp.ZoomSteps + " camera is off the pixel grid");
                }

                // The anchor can only drift when the view had to be pulled back inside the framing.
                if (drift.magnitude > 1.5f && !TouchesEdge(seen, framed, 2f / ppu))
                {
                    Note("zoom", string.Format("step {0}: the point under the cursor moved {1:0.#} px", camp.ZoomSteps, drift.magnitude));
                }

                yield return Shot("cv_04_zoom" + step, 0.3f);
            }

            // Zoomed all the way in, walk to the far side of the camp: the figure stays in view.
            Vector2 farthest = Vector2.zero;
            string far = null;
            foreach (string place in new[] { Places.Farm, Places.Mine, Places.Armory, Places.Library, Places.Recruitment })
            {
                Vector2 door = CampIso.ToWorld(Encampment.Site(place).Door);
                if (far == null || Vector2.Distance(door, camp.AvatarPosition) > Vector2.Distance(farthest, camp.AvatarPosition))
                {
                    farthest = door;
                    far = place;
                }
            }

            Vector3 startFocus = eye.transform.position;
            camp.ClickSite(far);
            float worst = float.MaxValue;
            float waited = 0f;
            bool shotMid = false;
            while (camp.IsWalking && waited < 10f)
            {
                Vector2 on = eye.WorldToScreenPoint(camp.AvatarPosition);
                float room = Mathf.Min(on.x, Screen.width - on.x, on.y, Screen.height - Shell.Camp.TopInsetPixels - on.y);
                worst = Mathf.Min(worst, room);
                if (!shotMid && waited > 0.8f)
                {
                    shotMid = true;
                    yield return Shot("cv_05_follow", 0f);
                }

                yield return null;
                waited += Time.unscaledDeltaTime;
            }

            float moved = Vector2.Distance(eye.transform.position, startFocus);
            Line(string.Format("follow: walked to {0}, camera moved {1:0.##} units, figure kept {2:0} px from the view's edge at worst", far, moved, worst));
            if (worst < 0f)
            {
                Note("zoom", "the walking figure left the view (" + worst.ToString("0") + " px)");
            }

            yield return ClearCards("follow");
            if (Shell.Router.Current != null && !(Shell.Router.Current is EncampmentScreen))
            {
                yield return PressEscape();
                yield return Wait(0.5f);
            }

            // All the way out: back to the framing exactly.
            for (int i = 0; i <= CampWorld.MaxZoomSteps; i++)
            {
                yield return Scroll(new Vector2(Screen.width * 0.5f, Screen.height * 0.4f), -1f);
            }

            if (camp.ZoomSteps != 0 || eye.transform.position != framedPosition || !Mathf.Approximately(eye.orthographicSize, framedSize))
            {
                Note("zoom", string.Format("zoomed out: step {0}, camera {1} size {2}, framing was {3} size {4}",
                    camp.ZoomSteps, eye.transform.position, eye.orthographicSize, framedPosition, framedSize));
            }

            yield return Shot("cv_06_zoom_out", 0.3f);
        }

        /// <summary>One wheel notch at <paramref name="screen"/>, through the input system.</summary>
        private static IEnumerator Scroll(Vector2 screen, float direction)
        {
            Mouse mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen, scroll = new Vector2(0f, 120f * direction) });
            yield return null;
            yield return null;
            yield return Wait(0.2f);
        }

        /// <summary>The world area the camera draws under the interface, from the camera itself.</summary>
        private Rect OnScreenWorld(Camera eye)
        {
            Vector3 low = eye.ScreenToWorldPoint(new Vector3(0f, 0f, 0f));
            Vector3 high = eye.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height - Shell.Camp.TopInsetPixels, 0f));
            return Rect.MinMaxRect(low.x, low.y, high.x, high.y);
        }

        private static bool Inside(Rect inner, Rect outer, float slack)
        {
            return inner.xMin >= outer.xMin - slack && inner.yMin >= outer.yMin - slack
                && inner.xMax <= outer.xMax + slack && inner.yMax <= outer.yMax + slack;
        }

        private static bool TouchesEdge(Rect inner, Rect outer, float slack)
        {
            return inner.xMin <= outer.xMin + slack || inner.yMin <= outer.yMin + slack
                || inner.xMax >= outer.xMax - slack || inner.yMax >= outer.yMax - slack;
        }
    }
}
