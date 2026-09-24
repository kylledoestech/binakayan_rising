using BinakayanRising.Core.Localization;
using BinakayanRising.Gameplay;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// The battle's pause menu: Resume, Settings, Retreat and Main Menu (Quit outside a campaign),
    /// over a frozen board.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Pausing.</b> Opening freezes the replay and locks the board and camera; closing restores
    /// exactly what it found. A battle something else had already paused (the mid-battle question)
    /// stays paused, and a board already locked stays locked.
    /// </para>
    /// <para>
    /// <b>Keys.</b> Esc or P closes the menu, except in the frame it opened and while Settings is on
    /// top: Settings closes itself on Esc, and that same press must not also close this menu.
    /// Which Esc press opens the menu is decided by the HUD (BattleHud.Hotkeys), which ranks it
    /// below every older Esc meaning.
    /// </para>
    /// <para>
    /// <b>Leaving.</b> Retreat is the battle's own retreat, <see cref="BattlePlaytest.EndMission"/>,
    /// which reports a retreat to the campaign before the battle is resolved: nothing is settled
    /// and no Rations are spent. Main Menu retreats the same way, then returns to the title.
    /// Both are unavailable once the battle is resolved (the outcome panel's Return settles it)
    /// and while the tutorial runs.
    /// </para>
    /// </remarks>
    public sealed class PauseMenu : MonoBehaviour
    {
        private const float CardWidth = 560f;
        private const float CardHeight = 500f;
        private const float ButtonWidth = 360f;
        private const float ButtonHeight = 64f;

        private BattlePlaytest battle;
        private bool wasPaused;
        private bool wasBoardLocked;
        private bool wasCameraLocked;
        private bool canLeave;
        private int openedFrame;
        private bool settingsOpenLastFrame;

        private RectTransform card;
        private CanvasGroup cardGroup;
        private Button retreat;
        private Button leave;

        /// <summary>The menu showing now, or null.</summary>
        public static PauseMenu Current { get; private set; }

        /// <summary>
        /// The frame the last menu closed in, so the HUD reading the same Esc press later in that
        /// frame does not reopen it.
        /// </summary>
        public static int ClosedFrame { get; private set; } = -1;

        /// <summary>The card's frame, for layout checks.</summary>
        public RectTransform Card
        {
            get { return card; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
            ClosedFrame = -1;
        }

        /// <summary>
        /// Opens the menu over <paramref name="battle"/> and pauses it. Returns the open menu when
        /// one is already up, and null when there is no battle or a question or cutscene is up.
        /// </summary>
        /// <param name="battle">The battle to pause.</param>
        /// <param name="canLeave">False while retreating is not allowed (the tutorial is running).</param>
        public static PauseMenu Show(BattlePlaytest battle, bool canLeave)
        {
            if (Current != null)
            {
                return Current;
            }

            // A question or a cutscene owns the screen; the menu waits for it to finish.
            if (battle == null || QuizCard.Current != null || TacticianCommandCard.Current != null || CutscenePlayer.Current != null)
            {
                return null;
            }

            Canvas canvas = UiKit.Screen("Pause Menu", Theme.Layer.Pause);
            UiKit.EnsureEventSystem();
            var view = canvas.gameObject.AddComponent<PauseMenu>();
            view.battle = battle;
            view.canLeave = canLeave;
            view.openedFrame = Time.frameCount;

            view.wasPaused = battle.Paused;
            view.wasBoardLocked = battle.BoardInputLocked;
            view.wasCameraLocked = battle.CameraInputLocked;
            battle.SetPaused(true);
            battle.SetBoardInputLocked(true);
            battle.SetCameraInputLocked(true);

            view.Build(canvas.transform);
            Current = view;
            view.RefreshButtons();
            view.StartCoroutine(UiTween.Enter(view.card, view.cardGroup, new Vector2(0f, -40f), 0.25f));
            UiSfx.Play(UiSfx.Cue.Open);
            return view;
        }

        private void Build(Transform root)
        {
            UiKit.Scrim(root);

            card = UiKit.Panel(root, "Card");
            UiKit.Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
            cardGroup = UiKit.Group(card.gameObject);

            RectTransform column = UiKit.Column(card, "Column", Theme.Space.Snug, Theme.Space.FramePadding + 8f, TextAnchor.UpperCenter);
            UiKit.Stretch(column);
            UiLayout.FillWidth(column);

            RectTransform header = UiKit.Row(column, "Header", Theme.Space.Base, 0f, TextAnchor.MiddleCenter);
            UiLayout.Fix(header, 0f, 56f);
            Image sigil = UiKit.Sigil(header, 40f, Theme.Gold);
            UiLayout.Fix((RectTransform)sigil.transform.parent, 40f, 40f);
            TextMeshProUGUI title = UiKit.Display(header, Loc.Get(TextKey.PauseTitle), Theme.Type.Title, TextAlignmentOptions.Center);
            title.color = Theme.Revolution;
            UiKit.Localize(title, TextKey.PauseTitle);
            UiLayout.OneLine(title, Theme.Type.Title);
            UiLayout.Fix(title.rectTransform, 280f, 56f);

            // A blank the sigil's size on the right, so the title sits on the card's centre line.
            UiLayout.Fix(UiKit.NewRect(header, "Balance"), 40f, 40f);

            Image rule = UiKit.Divider(column, 400f);
            UiLayout.Fix(rule.rectTransform, 400f, 16f);

            UiKit.SealButton(column, TextKey.PauseResume, Resume, ButtonWidth, ButtonHeight, 0f, "Button Resume");

            if (GameShell.Current != null)
            {
                UiKit.SealButton(column, TextKey.MenuSettings, OpenSettings, ButtonWidth, ButtonHeight, 0f, "Button Settings");
            }

            if (battle.IsMission)
            {
                retreat = UiKit.SealButton(column, TextKey.MissionRetreat, Retreat, ButtonWidth, ButtonHeight, 0f, "Button Retreat");
            }

            leave = GameShell.Current != null
                ? UiKit.SealButton(column, TextKey.HubToTitle, MainMenu, ButtonWidth, ButtonHeight, 0f, "Button Main Menu")
                : UiKit.SealButton(column, TextKey.MenuQuit, Quit, ButtonWidth, ButtonHeight, 0f, "Button Quit");

            TextMeshProUGUI hint = UiKit.Caption(column, Loc.Get(TextKey.PauseHint), TextAlignmentOptions.Center);
            UiKit.Localize(hint, TextKey.PauseHint);
            hint.fontStyle = FontStyles.Italic;
            UiLayout.OneLine(hint, Theme.Type.Small);
            UiLayout.Fix(hint.rectTransform, 0f, 28f);
        }

        /// <summary>Retreat and Main Menu leave the battle unsettled; not once it is resolved.</summary>
        private bool LeaveAllowed
        {
            get { return canLeave && battle != null && battle.CurrentPhase != BattlePlaytest.Phase.Finished; }
        }

        private void RefreshButtons()
        {
            bool allowed = LeaveAllowed;
            if (retreat != null)
            {
                retreat.interactable = allowed;
            }

            // Outside a campaign the second button is Quit, which is always allowed.
            if (leave != null && GameShell.Current != null)
            {
                leave.interactable = allowed;
            }
        }

        /// <summary>Closes the menu and resumes the battle, unless something else had paused it.</summary>
        public void Resume()
        {
            UiSfx.Play(UiSfx.Cue.Close);
            Close();
        }

        private void OpenSettings()
        {
            if (GameShell.Current != null)
            {
                GameShell.Current.OpenSettings();
            }
        }

        private void Retreat()
        {
            if (!LeaveAllowed)
            {
                UiSfx.Play(UiSfx.Cue.Error);
                return;
            }

            BattlePlaytest leaving = battle;
            Close();
            leaving.EndMission();
        }

        private void MainMenu()
        {
            if (!LeaveAllowed)
            {
                UiSfx.Play(UiSfx.Cue.Error);
                return;
            }

            BattlePlaytest leaving = battle;
            GameShell shell = GameShell.Current;
            Close();
            leaving.EndMission();
            if (shell != null)
            {
                shell.ReturnToTitle();
            }
        }

        private void Quit()
        {
            if (GameShell.Current != null)
            {
                GameShell.Current.Quit();
                return;
            }

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Removes the menu and restores the pause and input locks it found.</summary>
        public void Close()
        {
            if (battle != null)
            {
                battle.SetPaused(wasPaused);
                battle.SetBoardInputLocked(wasBoardLocked);
                battle.SetCameraInputLocked(wasCameraLocked);
            }

            battle = null;
            if (Current == this)
            {
                Current = null;
            }

            ClosedFrame = Time.frameCount;
            Destroy(gameObject);
        }

        private void Update()
        {
            // The board was removed under the menu (a retreat, or the battle ended otherwise).
            if (battle == null)
            {
                if (Current == this)
                {
                    Current = null;
                    ClosedFrame = Time.frameCount;
                }

                Destroy(gameObject);
                return;
            }

            RefreshButtons();

            bool settingsOpen = GameShell.Current != null && GameShell.Current.SettingsOpen;
            bool settingsJustClosed = settingsOpenLastFrame;
            settingsOpenLastFrame = settingsOpen;
            if (settingsOpen || settingsJustClosed || Time.frameCount <= openedFrame)
            {
                return;
            }

            Keyboard keys = Keyboard.current;
            if (keys != null && (keys.escapeKey.wasPressedThisFrame || keys.pKey.wasPressedThisFrame))
            {
                Resume();
            }
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
                ClosedFrame = Time.frameCount;
            }
        }
    }
}
