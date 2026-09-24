using System;
using BinakayanRising.Core.Localization;
using BinakayanRising.Core.Meta;
using BinakayanRising.Gameplay;
using BinakayanRising.Gameplay.Flow;
using BinakayanRising.Gameplay.Meta;
using BinakayanRising.UI.Camp;
using BinakayanRising.UI.Kit;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// Runs the game outside a battle: boots at start-up, owns the state machine, the screen
    /// router, the open campaign and the settings panel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Start-up.</b> Before the first scene loads, the installer tells
    /// <see cref="BattlePlaytest"/> not to spawn its standalone battle; after the scene loads, the
    /// shell creates itself unless something else already drives the scene (the styleguide, or a
    /// deliberately placed playtest). <c>Tools &gt; Binakayan Rising &gt; Battle Playtest Only</c>,
    /// or <c>-brPlaytest</c> on a player's command line, restores the old straight-to-battle boot.
    /// </para>
    /// <para>
    /// <b>Saving.</b> The session writes a few seconds after each change, and at once when the app
    /// is paused or quit, so closing the window never loses more than that.
    /// </para>
    /// </remarks>
    public sealed partial class GameShell : MonoBehaviour
    {
        private const string PlaytestOnlyPref = "BinakayanRising.PlaytestOnly";

        private Canvas canvas;
        private SettingsPanel settings;
        private MetaGame watched;

        /// <summary>The running shell, or null when the game was started in playtest mode.</summary>
        public static GameShell Current { get; private set; }

        public GameSession Session { get; private set; }

        public GameStateMachine Machine { get; private set; }

        public ScreenRouter Router { get; private set; }

        /// <summary>The canvas every campaign screen is built on.</summary>
        public Canvas Canvas
        {
            get { return canvas; }
        }

        /// <summary>The walkable encampment, on screen in every encampment state.</summary>
        public CampWorld Camp { get; private set; }

        /// <summary>
        /// The building the player last walked into, so a screen that serves several (the
        /// resource screen's Farm, Mine and Exchange tabs) opens on the right one.
        /// </summary>
        public string VisitingPlace { get; set; }

        /// <summary>True while the settings panel is on screen.</summary>
        public bool SettingsOpen
        {
            get { return settings != null && settings.IsOpen; }
        }

        /// <summary>Raised when the settings panel closes, so screens can re-read preferences.</summary>
        public event Action SettingsClosed;

        // ------------------------------------------------------------------ boot

        /// <summary>True when the game should boot straight into the standalone battle.</summary>
        public static bool PlaytestOnly
        {
            get
            {
#if UNITY_EDITOR
                if (EditorPrefs.GetBool(PlaytestOnlyPref, false))
                {
                    return true;
                }
#endif
                return CommandLine.Has("-brPlaytest");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            BattlePlaytest.AutoBootstrap = PlaytestOnly;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (PlaytestOnly || Current != null || BattlePlaytest.SceneAlreadyDriven())
            {
                return;
            }

            new GameObject("Binakayan Rising").AddComponent<GameShell>();
        }

        private void Awake()
        {
            Current = this;

            string saveDir = CommandLine.Value("-brSaveDir");
            SaveStore store = string.IsNullOrEmpty(saveDir) ? new SaveStore() : new SaveStore(saveDir);
            Session = new GameSession(store, MetaRules.Default(), () => DateTime.UtcNow);

            UiKit.EnsureEventSystem();
            canvas = UiKit.Screen("Campaign", Theme.Layer.Shell);
            canvas.transform.SetParent(transform, worldPositionStays: false);

            Router = gameObject.AddComponent<ScreenRouter>();
            Machine = gameObject.AddComponent<GameStateMachine>();

            Camp = CampWorld.Create();
            Camp.transform.SetParent(transform, worldPositionStays: false);

            Register<MainMenuScreen>("Main Menu");
            Register<EncampmentScreen>("Encampment");
            Register<ResourceScreen>("Resources");
            Register<InventoryScreen>("Inventory");
            Register<TrainingScreen>("Training");
            Register<RecruitScreen>("Recruitment");
            Register<MissionMapScreen>("Mission Tent");

            Machine.StateChanged += OnStateChanged;
            Session.GameOpened += WatchGame;

            // The machine launches itself on Start, which the router, bound now, turns into the
            // main menu appearing.
            Router.Bind(Machine);

            if (CommandLine.Has("-brShot"))
            {
                gameObject.AddComponent<ShotAutopilot>();
            }
        }

        private T Register<T>(string name) where T : ShellScreen
        {
            RectTransform rect = UiKit.NewRect(canvas.transform, name);
            T screen = rect.gameObject.AddComponent<T>();
            screen.Shell = this;
            Router.Register(screen);
            return screen;
        }

        private void Update()
        {
            Session.Tick(Time.unscaledDeltaTime);
            TickCampaign();
            if (Camp.gameObject.activeSelf)
            {
                Camp.TopInsetPixels = CampaignBar.CoveredHeight * canvas.scaleFactor;
            }
        }

        /// <summary>The camp is on screen in the hub and under every panel opened from it.</summary>
        private void OnStateChanged(GameState from, GameState to)
        {
            bool inCamp = InCamp(to);
            if (!inCamp)
            {
                Camp.Hide();
                return;
            }

            if (!InCamp(from))
            {
                Camp.ResetAvatar();
            }

            if (!Camp.gameObject.activeSelf)
            {
                Camp.Show(CampaignBar.CoveredHeight * canvas.scaleFactor);
            }
        }

        /// <summary>
        /// The camp stays drawn under every encampment panel, and under the Mission Tent's map,
        /// which is opened from it.
        /// </summary>
        private static bool InCamp(GameState state)
        {
            return GameStateMachine.IsEncampmentSubState(state) || state == GameState.MissionPortal;
        }

        /// <summary>Follows the open campaign, to announce each finished sub-quest.</summary>
        private void WatchGame()
        {
            if (watched != null)
            {
                watched.QuestCompleted -= AnnounceQuest;
            }

            watched = Session.Game;
            if (watched != null)
            {
                watched.QuestCompleted += AnnounceQuest;
            }
        }

        private static void AnnounceQuest(QuestReward reward)
        {
            if (reward == null || reward.Quest == null)
            {
                return;
            }

            string message = Loc.Format(TextKey.HubQuestDone, reward.Quest.Title.Get());
            if (reward.Reales > 0)
            {
                message += "   " + Loc.Format(TextKey.HubRewardReales, reward.Reales);
            }

            UiSfx.Play(UiSfx.Cue.Victory);
            UiControls.Toast(message, 3.4f);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Session.SaveNow();
            }
        }

        private void OnApplicationQuit()
        {
            Session.SaveNow();
        }

        private void OnDestroy()
        {
            if (watched != null)
            {
                watched.QuestCompleted -= AnnounceQuest;
            }

            if (Current == this)
            {
                Current = null;
            }
        }

        // ------------------------------------------------------------------ campaign

        /// <summary>Opens the saved campaign and enters the encampment.</summary>
        public void ContinueCampaign()
        {
            if (!Session.Continue())
            {
                UiSfx.Play(UiSfx.Cue.Error);
                return;
            }

            Machine.LoadSaveOrNewGame();
            if (Session.RestoredFromBackup)
            {
                UiControls.Toast(Loc.Get(TextKey.MenuSaveRestored), 4f);
            }
        }

        /// <summary>Starts a fresh campaign and enters the encampment.</summary>
        public void StartNewCampaign()
        {
            Session.NewCampaign();
            Machine.LoadSaveOrNewGame();
        }

        /// <summary>Saves, closes the campaign and returns to the title screen.</summary>
        public void ReturnToTitle()
        {
            Session.Close();
            LeaveForTitle();
        }

        /// <summary>Deletes the save and, if a campaign was open, returns to the title screen.</summary>
        public void DeleteSave()
        {
            Session.DeleteSave();
            LeaveForTitle();
        }

        private void LeaveForTitle()
        {
            if (Machine.CurrentState == GameState.MainMenu)
            {
                // Already there: just let the menu re-read the save.
                Router.Current?.Show();
                return;
            }

            if (Machine.CurrentState != GameState.BaseHub && InCamp(Machine.CurrentState))
            {
                Machine.ReturnToBaseHub();
            }

            Machine.TryTransitionTo(GameState.MainMenu);
        }

        public void Quit()
        {
            Session.SaveNow();
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ settings

        public void OpenSettings()
        {
            if (settings == null)
            {
                settings = SettingsPanel.Create(this);
                settings.Closed += () =>
                {
                    Action handler = SettingsClosed;
                    if (handler != null)
                    {
                        handler();
                    }
                };
            }

            settings.Open();
        }

        public void CloseSettings()
        {
            if (settings != null)
            {
                settings.Close();
            }
        }

#if UNITY_EDITOR
        private const string PlaytestMenu = "Tools/Binakayan Rising/Battle Playtest Only";

        [MenuItem(PlaytestMenu, priority = 5)]
        private static void TogglePlaytestOnly()
        {
            bool next = !EditorPrefs.GetBool(PlaytestOnlyPref, false);
            EditorPrefs.SetBool(PlaytestOnlyPref, next);
            Menu.SetChecked(PlaytestMenu, next);
            Debug.Log(next
                ? "Play now boots straight into the standalone battle."
                : "Play now boots into the campaign: main menu, encampment, missions.");
        }

        [MenuItem(PlaytestMenu, isValidateFunction: true)]
        private static bool TogglePlaytestOnlyValidate()
        {
            Menu.SetChecked(PlaytestMenu, EditorPrefs.GetBool(PlaytestOnlyPref, false));
            return !EditorApplication.isPlaying;
        }
#endif
    }

    /// <summary>Reads the player's command-line switches.</summary>
    public static class CommandLine
    {
        public static bool Has(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The argument after <paramref name="flag"/>, or null.</summary>
        public static string Value(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
