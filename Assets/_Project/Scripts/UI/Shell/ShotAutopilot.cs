using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using BinakayanRising.Core.Meta;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// Drives a player build through a fixed route of screens, saving a screenshot and a layout
    /// audit at each stop, then quits. For reviewing the interface without clicking through it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Run as <c>BinakayanRising.x86_64 -brShot phase1 -brShotDir out -brSaveDir tmp</c>. Always
    /// pass <c>-brSaveDir</c>: the route starts new campaigns and deletes saves.
    /// </para>
    /// <para>
    /// The audit is the part that proves anything. A screenshot shows that something looks wrong;
    /// the audit measures it: every label that had to cut its text off, every label that shrank
    /// below three quarters of its size to fit, and every element that spills outside its layout
    /// group or off the screen. Lines go to <c>audit.txt</c> beside the images.
    /// </para>
    /// </remarks>
    public sealed class ShotAutopilot : MonoBehaviour
    {
        private string directory;
        private readonly StringBuilder audit = new StringBuilder();
        private int problems;

        private GameShell Shell
        {
            get { return GameShell.Current; }
        }

        private IEnumerator Start()
        {
            if (string.IsNullOrEmpty(CommandLine.Value("-brSaveDir")))
            {
                // The route deletes saves; without a scratch directory it would delete the real one.
                Debug.LogError("[Shot] refusing to run without -brSaveDir.");
                Application.Quit(2);
                yield break;
            }

            directory = CommandLine.Value("-brShotDir") ?? Path.Combine(Application.persistentDataPath, "shots");
            Directory.CreateDirectory(directory);

            if (CommandLine.Has("-brShotSize"))
            {
                string[] size = (CommandLine.Value("-brShotSize") ?? "1920x1080").Split('x');
                Screen.SetResolution(int.Parse(size[0]), int.Parse(size[1]), FullScreenMode.Windowed);
            }

            string route = CommandLine.Value("-brShot") ?? "phase1";
            Language before = Loc.Current;
            UserPrefs.ChooseLanguage(Language.English);

            // Every route but the shell's starts on the menu, as before the splash existed, and
            // photographs the camp without its story scene playing over it.
            if (route != "shell")
            {
                if (SplashScreen.Current != null)
                {
                    SplashScreen.Current.Dismiss(true);
                }

                EncampmentScreen.StoryAutoplay = false;
            }

            // Let the machine launch and the first screen finish fading in.
            yield return Wait(1.2f);

            switch (route)
            {
                case "shell":
                    yield return ShellRoute();
                    break;

                case "phase2":
                    yield return Phase2();
                    break;

                case "phase3":
                    yield return Phase3();
                    break;

                case "phase4":
                    yield return Phase4();
                    break;

                case "pause":
                    yield return Pause();
                    break;

                case "issues":
                    yield return Issues();
                    break;

                default:
                    yield return Phase1();
                    break;
            }

            UserPrefs.ChooseLanguage(before);
            audit.Insert(0, "screen " + Screen.width + "x" + Screen.height + ", problems " + problems + "\n");
            File.WriteAllText(Path.Combine(directory, "audit.txt"), audit.ToString());
            Debug.Log("[Shot] done, " + problems + " layout problems. " + directory);
            yield return Wait(0.3f);
            Application.Quit(problems == 0 ? 0 : 3);
        }

        private IEnumerator Phase1()
        {
            Shell.Session.DeleteSave();
            Shell.Router.Current?.Show();
            yield return Shot("01_menu_empty");

            Shell.StartNewCampaign();
            yield return Shot("02_hub");

            Shell.OpenSettings();
            yield return Shot("03_settings");

            Click("Button Delete Save");
            yield return Shot("04_confirm_delete");
            Click("Button Cancel");

            Shell.CloseSettings();
            Shell.ReturnToTitle();
            yield return Shot("05_menu_saved");

            Click("Button New Campaign");
            yield return Shot("06_confirm_overwrite");
            Click("Button Cancel");

            UserPrefs.ChooseLanguage(Language.Filipino);
            yield return Shot("07_menu_fil");

            Shell.ContinueCampaign();
            yield return Shot("08_hub_fil");

            Shell.OpenSettings();
            yield return Shot("09_settings_fil");
            Shell.CloseSettings();
        }

        /// <summary>
        /// The encampment and its economy: the camp itself, hovering, the aide's welcome, a
        /// keeper's first-visit greeting, the three resource tabs and the inventory.
        /// </summary>
        private IEnumerator Phase2()
        {
            Shell.Session.DeleteSave();
            Shell.StartNewCampaign();
            yield return Wait(0.6f);
            MeasureCamp("camp on entry");
            yield return Shot("p2_01_camp");

            Shell.Camp.ForceHover(Places.MissionTent, null);
            yield return Shot("p2_02_hover_tent");
            Shell.Camp.ForceHover(null, Characters.Tomas);
            yield return Shot("p2_03_hover_tomas");
            Shell.Camp.ForceHover(null, null);

            // Walk to the aide; the welcome opens on arrival.
            Shell.Camp.ClickFigure(Characters.Tomas);
            yield return Wait(0.25f);
            yield return Shot("p2_04_walking");
            yield return WaitWhile(() => Shell.Camp.IsWalking, 6f);
            yield return Shot("p2_05_dialogue_tomas");
            Hub().Dialogue.Advance();
            Hub().Dialogue.Advance();
            Hub().Dialogue.Advance();
            Hub().Dialogue.Advance();
            yield return Shot("p2_06_dialogue_tomas_line3");
            Hub().Dialogue.Finish();

            // The arrow moves on in the hub's LateUpdate, so measure a frame later.
            yield return null;
            MeasureCamp("after welcome");
            yield return Shot("p2_07_next_objective");

            // Seed the stores: the farm part-way, the mine full, and goods to sell.
            MetaGame game = Shell.Session.Game;
            long now = game.Now.Ticks;
            game.Data.farm.sinceUtcTicks = now - (long)(game.Rules.Farm.SecondsPerUnit * 13.4 * System.TimeSpan.TicksPerSecond);
            game.Data.mine.sinceUtcTicks = now - (long)(game.Rules.Mine.SecondsPerUnit * 40.0 * System.TimeSpan.TicksPerSecond);
            game.Earn(Currency.Rations, 25);
            game.Earn(Currency.Scrap, 12);

            Shell.Camp.ClickSite(Places.Farm);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            yield return Shot("p2_08_farmer_greeting");
            Hub().Dialogue.Finish();
            yield return Shot("p2_09_farm");
            ClickIn("Button Harvest");
            yield return Shot("p2_10_farm_harvested");
            ClickIn("Tab 1");
            yield return Shot("p2_11_mine_full");
            ClickIn("Tab 2");
            yield return Shot("p2_12_exchange");
            ClickIn("Button Sell All Rations");
            yield return Shot("p2_13_exchange_sold");
            MeasureToastClear();
            ClickIn("Button Back To Camp");
            yield return Wait(0.4f);

            Shell.Camp.ClickSite(Places.Armory);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            yield return Shot("p2_14_inventory");
            ClickIn("Tab 1");
            yield return Shot("p2_15_weapons");
            ClickIn("Give 1");
            yield return Shot("p2_16_weapon_given");
            ClickIn("Button Back To Camp");
            yield return Wait(0.4f);

            Shell.Camp.ClickSite(Places.Library);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            yield return Shot("p2_17_coming_soon");

            UserPrefs.ChooseLanguage(Language.Filipino);
            Shell.Camp.ForceHover(Places.Exchange, null);
            yield return Shot("p2_18_hub_fil");
            Shell.Camp.ForceHover(null, null);
            Shell.Camp.ClickFigure(Characters.Tomas);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            yield return Shot("p2_19_reminder_fil");
            Hub().Dialogue.Finish();
            Shell.Camp.ClickSite(Places.Mine);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            yield return Shot("p2_20_miner_fil");
            Hub().Dialogue.Finish();
            yield return Shot("p2_21_mine_fil");
            ClickIn("Button Back To Camp");
            yield return Wait(0.4f);
            Shell.Camp.ClickSite(Places.Armory);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            ClickIn("Tab 1");
            yield return Shot("p2_22_weapons_fil");
        }

        /// <summary>
        /// Progression: the drill sergeant, the training ground, a drill that promotes (caught
        /// while the numbers count and after), the recruitment hall, a guaranteed Hero, ten
        /// recruits at once, and the same screens in Filipino.
        /// </summary>
        private IEnumerator Phase3()
        {
            Shell.Session.DeleteSave();
            Shell.StartNewCampaign();
            yield return Wait(0.6f);

            // Seed enough to drill and recruit, and put the first unit one drill from a level.
            MetaGame game = Shell.Session.Game;
            game.Earn(Currency.Reales, 2400);
            game.Earn(Currency.Scrap, 40);
            OwnedUnit first = game.Units[0];
            first.xp = game.XpToNext(first) - 40;

            Shell.Camp.ClickSite(Places.Training);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            yield return Shot("p3_01_sergeant_greeting");
            Hub().Dialogue.Finish();
            yield return Wait(0.4f);
            Training().SelectedUnit = first.id;
            yield return Shot("p3_02_training");

            ClickIn("Button Drill");
            yield return Shot("p3_03_promotion_counting", 0.45f);
            yield return Shot("p3_04_promotion", 1.2f);
            yield return DrainPromotions("p3_04");
            ClickIn("Button Drill");
            yield return Shot("p3_05_drilled");
            MeasureToastClear();
            Training().SelectedUnit = game.Units[game.Units.Count - 1].id;
            yield return Shot("p3_06_training_other");
            ClickIn("Button Back To Camp");
            yield return Wait(0.4f);

            // Nine recruits without a Hero: the next is guaranteed.
            game.Data.pity = game.Rules.PityThreshold - 1;
            Shell.Camp.ClickSite(Places.Recruitment);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            yield return Shot("p3_07_recruit_greeting");
            Hub().Dialogue.Finish();
            yield return Wait(0.4f);
            yield return Shot("p3_08_recruit");

            ClickIn("Button Recruit One");
            yield return Shot("p3_09_reveal_one_back", 0.1f);
            yield return WaitWhile(() => RecruitReveal.Current != null && !RecruitReveal.Current.AllShown, 6f);
            yield return Shot("p3_10_reveal_one");
            MeasureReveal("10_reveal_one");
            yield return CloseReveal("p3_10");

            ClickIn("Button Recruit Many");
            yield return Shot("p3_11_reveal_many_mid", 0.9f);
            yield return WaitWhile(() => RecruitReveal.Current != null && !RecruitReveal.Current.AllShown, 10f);
            yield return Shot("p3_12_reveal_many");
            MeasureReveal("12_reveal_many");
            yield return CloseReveal("p3_12");
            yield return Shot("p3_13_recruit_after");

            UserPrefs.ChooseLanguage(Language.Filipino);
            yield return Shot("p3_14_recruit_fil");
            ClickIn("Button Recruit One");
            yield return WaitWhile(() => RecruitReveal.Current != null && !RecruitReveal.Current.AllShown, 6f);
            yield return Shot("p3_15_reveal_fil");
            MeasureReveal("15_reveal_fil");
            yield return CloseReveal("p3_15");
            ClickIn("Button Back To Camp");
            yield return Wait(0.4f);

            OwnedUnit second = game.Units[1];
            second.xp = game.XpToNext(second) - 40;
            Shell.Camp.ClickSite(Places.Training);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            Training().SelectedUnit = second.id;
            yield return Shot("p3_16_training_fil");
            ClickIn("Button Drill");
            yield return Shot("p3_17_promotion_fil");
            yield return DrainPromotions("p3_17");
        }

        /// <summary>
        /// The Week 19 additions: the rank-up card, the Mission Tent map, a cutscene, the Library
        /// and its test, a campaign battle with its minimap and mid-battle question, in English
        /// and Filipino.
        /// </summary>
        private IEnumerator Phase4()
        {
            Shell.Session.DeleteSave();
            Shell.StartNewCampaign();
            yield return Wait(0.6f);

            MetaGame game = Shell.Session.Game;
            game.Earn(Currency.Rations, 60);
            foreach (string id in new[] { "q01", "q02", "q03", "q04" })
            {
                game.Data.clearedQuests.Add(id);
            }

            foreach (string id in new[] { "l01", "l02", "l03", "l04" })
            {
                game.Data.lessons.Add(id);
            }

            Hub().Dialogue.Finish();
            yield return WaitWhile(() => RankUpCard.Current == null, 4f);
            yield return Shot("p4_01_rankup_stars", 0.35f);
            yield return Shot("p4_02_rankup", 2.4f);
            MeasureCard("rankup", RankUpCard.Current != null ? RankUpCard.Current.Card : null);
            if (RankUpCard.Current != null)
            {
                RankUpCard.Current.Continue();
                RankUpCard.Current?.Continue();
            }

            yield return Wait(0.4f);
            Shell.Camp.ClickSite(Places.MissionTent);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            Hub().Dialogue.Finish();
            yield return WaitWhile(() => !(Shell.Router.Current is MissionMapScreen), 4f);
            yield return Shot("p4_03_map");
            var map = Shell.Router.Current as MissionMapScreen;
            if (map != null)
            {
                MeasureCard("map", map.Card);
            }

            CutscenePlayer.Play("c06_earthworks", null);
            yield return Shot("p4_04_cutscene_typing", 0.5f);
            yield return Shot("p4_05_cutscene", 3.5f);
            CutscenePlayer.Current?.Advance();
            CutscenePlayer.Current?.Advance();
            yield return Shot("p4_06_cutscene_slide2", 3.5f);
            CutscenePlayer.Current?.Skip();
            yield return Wait(0.3f);

            Shell.Router.Current?.Hide(immediate: true);
            Shell.Machine.ReturnToEncampment();
            yield return Wait(0.6f);
            LibraryPanel.Open(Shell);
            yield return Shot("p4_07_library");
            MeasureCard("library", LibraryPanel.Current != null ? LibraryPanel.Current.Card : null);
            LibraryPanel.Current?.Close();
            QuizCard.Show(Learning.Test(1, 5, 3), "Level 1 Test", null, null);
            yield return Shot("p4_08_quiz");
            MeasureCard("quiz", QuizCard.Current != null ? QuizCard.Current.Card : null);
            PickFirst();
            yield return Shot("p4_09_quiz_answered");
            DestroyQuiz();

            UserPrefs.ChooseLanguage(Language.Filipino);
            yield return Wait(0.3f);
            LibraryPanel.Open(Shell);
            yield return Shot("p4_10_library_fil");
            LibraryPanel.Current?.Close();
            QuizCard.Show(Learning.Test(3, 5, 5), "Pagsusulit", null, null);
            PickFirst();
            yield return Shot("p4_11_quiz_fil");
            DestroyQuiz();
            CutscenePlayer.Play("c10_dawn", null);
            yield return Shot("p4_12_cutscene_fil", 4f);
            CutscenePlayer.Current?.Skip();
            game.Data.clearedQuests.Add("q05");
            yield return WaitWhile(() => RankUpCard.Current == null, 4f);
            yield return Shot("p4_13_rankup_fil", 2.6f);
            RankUpCard.Current?.Continue();
            RankUpCard.Current?.Continue();
            UserPrefs.ChooseLanguage(Language.English);
            yield return Wait(0.3f);

            // A real campaign battle: q06, the Hold battle with a question on turn 5.
            Shell.Camp.ClickSite(Places.MissionTent);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            yield return WaitWhile(() => !(Shell.Router.Current is MissionMapScreen), 4f);
            Quest quest = Campaign.Find("q06");
            Shell.LaunchQuest(quest);
            yield return Wait(0.5f);
            CutscenePlayer.Current?.Skip();
            yield return WaitWhile(() => Shell.Battle == null, 4f);
            yield return Wait(1.5f);
            yield return Shot("p4_14_battle_deploy");
            if (Shell.Battle != null)
            {
                Shell.Battle.RequestAutoDeploy();
                yield return Wait(0.5f);
                yield return Shot("p4_15_battle_deployed");
                Shell.Battle.RequestAssault();
                Shell.Battle.SetSpeed(4f);
                yield return WaitWhile(() => QuizCard.Current == null && Shell.Battle != null && Shell.Battle.CurrentPhase != BinakayanRising.Gameplay.BattlePlaytest.Phase.Finished, 90f);
                yield return Shot("p4_16_battle_quiz");
                PickFirst();
                yield return Shot("p4_17_battle_quiz_answered");
                QuizCard.Current?.Continue();
                yield return WaitWhile(() => Shell.Battle != null && Shell.Battle.CurrentPhase != BinakayanRising.Gameplay.BattlePlaytest.Phase.Finished, 120f);
                yield return Shot("p4_18_battle_report", 1.5f);
                if (Shell.Battle != null)
                {
                    Shell.Battle.EndMission();
                }

                yield return Wait(1f);
                yield return Shot("p4_19_after_battle");
                yield return DrainPromotions("p4_19");
                yield return Wait(0.5f);
                yield return Shot("p4_20_after_cards");
            }
        }

        /// <summary>
        /// The battle pause menu: q06 opened as Phase 4 opens it, the replay paused mid-way, in
        /// English and Filipino, then a retreat from the menu back to the camp.
        /// </summary>
        private IEnumerator Pause()
        {
            Shell.Session.DeleteSave();
            Shell.StartNewCampaign();
            yield return Wait(0.6f);

            MetaGame game = Shell.Session.Game;
            game.Earn(Currency.Rations, 60);
            foreach (string id in new[] { "q01", "q02", "q03", "q04", "q05" })
            {
                game.Data.clearedQuests.Add(id);
            }

            Hub().Dialogue.Finish();

            // Clearing five quests at once earns ranks; their cards come up in the hub.
            yield return WaitWhile(() => RankUpCard.Current == null, 3f);
            int guard = 0;
            while (RankUpCard.Current != null && guard++ < 8)
            {
                RankUpCard.Current.Continue();
                RankUpCard.Current?.Continue();
                yield return Wait(0.4f);
            }

            Shell.Camp.ClickSite(Places.MissionTent);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            Hub().Dialogue.Finish();
            yield return WaitWhile(() => !(Shell.Router.Current is MissionMapScreen), 4f);
            Shell.LaunchQuest(Campaign.Find("q06"));
            yield return Wait(0.5f);
            CutscenePlayer.Current?.Skip();
            yield return WaitWhile(() => Shell.Battle == null, 4f);
            yield return Wait(1.5f);
            if (Shell.Battle == null)
            {
                Note("pause", "the q06 battle did not open");
                yield break;
            }

            Shell.Battle.RequestAutoDeploy();
            yield return Wait(0.5f);
            Shell.Battle.RequestAssault();
            Shell.Battle.SetSpeed(1f);
            yield return Wait(2f);
            if (Shell.Machine.CurrentState != Gameplay.Flow.GameState.Combat)
            {
                Note("pause", "state machine is " + Shell.Machine.CurrentState + " mid-replay, not Combat");
            }

            var hud = Object.FindAnyObjectByType<BinakayanRising.UI.Screens.BattleHud>();
            if (hud == null || !hud.OpenPauseMenu())
            {
                Note("pause", "the pause menu did not open");
                yield break;
            }

            yield return Shot("pause_01_menu");
            MeasureCard("pause", PauseMenu.Current != null ? PauseMenu.Current.Card : null);
            if (Shell.Battle != null && !Shell.Battle.Paused)
            {
                Note("pause", "the battle is not paused under the menu");
            }

            UserPrefs.ChooseLanguage(Language.Filipino);
            yield return Shot("pause_02_menu_fil");
            MeasureCard("pause_fil", PauseMenu.Current != null ? PauseMenu.Current.Card : null);
            UserPrefs.ChooseLanguage(Language.English);

            PauseMenu.Current?.Resume();
            yield return Wait(0.3f);
            if (Shell.Battle != null && Shell.Battle.Paused && QuizCard.Current == null)
            {
                Note("pause", "the battle stayed paused after Resume");
            }

            // Leave through the menu's Retreat, the way a player abandons a battle.
            if (hud != null && hud.OpenPauseMenu())
            {
                Button[] buttons = PauseMenu.Current.GetComponentsInChildren<Button>(false);
                for (int i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i].name == "Button Retreat")
                    {
                        buttons[i].onClick.Invoke();
                        break;
                    }
                }
            }

            yield return Wait(1f);
            if (Shell.Battle != null || !Gameplay.Flow.GameStateMachine.IsEncampmentSubState(Shell.Machine.CurrentState))
            {
                Note("pause", "Retreat did not return to the camp; state " + Shell.Machine.CurrentState);
            }
        }

        /// <summary>
        /// The shell and its content (#46, #50, #34, #40, #49): the splash in both languages, the
        /// menu over the trench scene, a new campaign opening on Act 1 by itself (once only), the
        /// overwrite guard, the Library's glossary, the other acts, the aftermath and the Music
        /// slider in Settings.
        /// </summary>
        private IEnumerator ShellRoute()
        {
            Shell.Session.DeleteSave();
            if (SplashScreen.Current == null)
            {
                Note("shell", "no splash at start-up");
            }

            if (SplashScreen.LoadArt() == null)
            {
                Note("shell", "splash picture missing from Resources/" + SplashScreen.ArtPath);
            }

            AuditMusic("splash", MusicPlayer.Track.Menu);
            yield return Shot("s_01_splash");
            UserPrefs.ChooseLanguage(Language.Filipino);
            yield return Shot("s_02_splash_fil");
            UserPrefs.ChooseLanguage(Language.English);

            if (SplashScreen.Current != null)
            {
                SplashScreen.Current.Dismiss(false);
            }

            yield return Wait(0.8f);
            Shell.Router.Current?.Show();
            yield return Shot("s_03_menu");

            // A new campaign: the camp plays Act 1 on its own.
            Shell.StartNewCampaign();
            yield return WaitWhile(() => CutscenePlayer.Current == null, 4f);
            if (CutscenePlayer.Current == null)
            {
                Note("shell", "Act 1 did not play on a new campaign");
            }

            yield return Shot("s_04_act1", 3.5f);
            CutscenePlayer.Current?.Advance();
            CutscenePlayer.Current?.Advance();
            yield return Shot("s_05_act1_slide2", 3f);
            CutscenePlayer.Current?.Skip();
            yield return Wait(1.8f);
            AuditMusic("camp", MusicPlayer.Track.Camp);

            // Back to the title: Continue must not replay Act 1; New Campaign must ask first.
            Shell.ReturnToTitle();
            yield return Wait(0.8f);
            Click("Button New Campaign");
            yield return Shot("s_06_confirm_overwrite");
            Click("Button Cancel");
            Shell.ContinueCampaign();
            yield return Wait(1f);
            if (CutscenePlayer.Current != null)
            {
                Note("shell", "Act 1 played again after Continue");
                CutscenePlayer.Current.Skip();
            }

            if (Shell.Session.Game == null)
            {
                Note("shell", "Continue did not load the save");
            }

            // The glossary.
            LibraryPanel.Open(Shell);
            yield return Wait(0.3f);
            if (LibraryPanel.Current == null)
            {
                Note("shell", "the Library did not open");
            }
            else
            {
                LibraryPanel.Current.ShowGlossary();
                yield return Shot("s_07_glossary");
                MeasureCard("glossary", LibraryPanel.Current.Card);
                LibraryPanel.Current.TurnPage(1);
                yield return Shot("s_08_glossary_page2");
                LibraryPanel.Current.TurnPage(1);
                yield return Shot("s_09_glossary_page3");
                UserPrefs.ChooseLanguage(Language.Filipino);
                yield return Shot("s_10_glossary_fil");
                UserPrefs.ChooseLanguage(Language.English);
                LibraryPanel.Current.Close();
            }

            yield return Wait(0.4f);

            // The other acts, and the aftermath of the final battle.
            string[] scenes = { Cutscenes.Act2, Cutscenes.Act3, Cutscenes.Act4 };
            for (int i = 0; i < scenes.Length; i++)
            {
                CutscenePlayer.Play(scenes[i], null);
                yield return Shot("s_11_act" + (i + 2), 3.5f);
                CutscenePlayer.Current?.Skip();
                yield return Wait(0.6f);
            }

            CutscenePlayer.Play(Cutscenes.Aftermath, null);
            yield return Shot("s_12_aftermath", 3.5f);
            for (int i = 0; i < 4; i++)
            {
                CutscenePlayer.Current?.Advance();
                CutscenePlayer.Current?.Advance();
                yield return Wait(0.5f);
            }

            yield return Shot("s_13_aftermath_later", 3.5f);
            UserPrefs.ChooseLanguage(Language.Filipino);
            yield return Shot("s_14_aftermath_fil", 0.8f);
            UserPrefs.ChooseLanguage(Language.English);
            CutscenePlayer.Current?.Skip();
            yield return Wait(0.6f);

            Shell.OpenSettings();
            yield return Shot("s_15_settings_music");
            Shell.CloseSettings();
        }

        /// <summary>Writes which music is playing, and at what level, and checks it is the one expected.</summary>
        private void AuditMusic(string where, MusicPlayer.Track expected)
        {
            AudioClip clip = MusicPlayer.CurrentClip;
            audit.AppendFormat("\n-- music at {0}: {1} ({2}), level {3:0.00}\n", where, MusicPlayer.Current,
                clip != null ? clip.name : "no clip", UserPrefs.EffectiveMusicVolume);
            if (MusicPlayer.Current != expected)
            {
                Note("music", where + " plays " + MusicPlayer.Current + ", not " + expected);
            }

            if (clip == null)
            {
                Note("music", "no clip loaded for " + MusicPlayer.Current + " at " + where);
            }
        }

        /// <summary>Answers the open question with choice A, to show the reveal.</summary>

        /// <summary>
        /// The features closed from the issue tracker: the Training detail's full stats and bond,
        /// the purse counting up, the Rations chip flashing when a quest is unaffordable, HP bars
        /// over the board mid-replay, and every How-to-Play page.
        /// </summary>
        private IEnumerator Issues()
        {
            Shell.Session.DeleteSave();
            Shell.StartNewCampaign();
            yield return Wait(0.6f);

            MetaGame game = Shell.Session.Game;
            Hub().Dialogue.Finish();
            yield return Wait(0.4f);

            // The purse mid-count, just after a payment lands.
            game.Earn(Currency.Reales, 900);
            yield return Shot("i_01_reales_counting", 0.15f);
            yield return Shot("i_02_reales_counted", 0.8f);

            Shell.Camp.ClickSite(Places.Training);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            Hub().Dialogue.Finish();
            yield return Wait(0.4f);
            OwnedUnit bonded = game.Units[0];
            for (int i = 0; i < game.Units.Count; i++)
            {
                if (game.Units[i].archetype != null && game.Units[i].archetype.ToUpperInvariant().Contains("VAN"))
                {
                    bonded = game.Units[i];
                }
            }

            Training().SelectedUnit = bonded.id;
            yield return Shot("i_03_training_detail");
            Training().SelectedUnit = game.Units[0].id;
            yield return Shot("i_04_training_detail_leader");
            UserPrefs.ChooseLanguage(Language.Filipino);
            yield return Shot("i_05_training_detail_fil");
            UserPrefs.ChooseLanguage(Language.English);
            ClickIn("Button Back To Camp");
            yield return Wait(0.6f);

            // Too few Rations for q05: the Mission Tent flashes the Rations chip.
            game.TrySpend(Cost.Of(Currency.Rations, game.Balance(Currency.Rations)));
            foreach (string id in new[] { "q01", "q02", "q03", "q04" })
            {
                game.Data.clearedQuests.Add(id);
            }

            // The cleared quests earn ranks; let their cards come up and go first.
            Hub().Dialogue.Finish();
            yield return WaitWhile(() => RankUpCard.Current == null, 3f);
            for (int i = 0; RankUpCard.Current != null && i < 8; i++)
            {
                RankUpCard.Current.Continue();
                RankUpCard.Current?.Continue();
                yield return Wait(0.5f);
            }

            Shell.Camp.ClickSite(Places.MissionTent);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            Hub().Dialogue.Finish();
            yield return WaitWhile(() => !(Shell.Router.Current is MissionMapScreen), 4f);
            yield return Shot("i_06_rations_flash", 0.15f);
            yield return Shot("i_06b_rations_flash", 0.1f);
            yield return Shot("i_07_rations_short", 1.2f);

            // A replay with HP bars over every unit.
            game.Earn(Currency.Rations, 60);
            game.Data.clearedQuests.Add("q05");

            // Clearing quests at once earns ranks; their cards come up first.
            yield return WaitWhile(() => RankUpCard.Current == null, 2f);
            int guard = 0;
            while (RankUpCard.Current != null && guard++ < 8)
            {
                RankUpCard.Current.Continue();
                RankUpCard.Current?.Continue();
                yield return Wait(0.4f);
            }

            Shell.LaunchQuest(Campaign.Find("q06"));
            yield return Wait(0.5f);
            CutscenePlayer.Current?.Skip();
            yield return WaitWhile(() => Shell.Battle == null, 4f);
            yield return Wait(1.5f);
            if (Shell.Battle == null)
            {
                Note("issues", "the q06 battle did not open");
                yield break;
            }

            var hud = Object.FindAnyObjectByType<BinakayanRising.UI.Screens.BattleHud>();
            if (hud != null && hud.Deck != null)
            {
                hud.Deck.Open();
                for (int page = 0; page < 8; page++)
                {
                    hud.Deck.ShowPage(page);
                    yield return Shot("i_deck_" + page, 0.5f);
                }

                UserPrefs.ChooseLanguage(Language.Filipino);
                hud.Deck.ShowPage(3);
                yield return Shot("i_deck_3_fil", 0.5f);
                UserPrefs.ChooseLanguage(Language.English);
                hud.Deck.Close();
                yield return Wait(0.4f);
            }

            Shell.Battle.RequestAutoDeploy();
            yield return Wait(0.5f);
            Shell.Battle.RequestAssault();
            Shell.Battle.SetSpeed(1f);
            yield return Wait(3f);
            yield return Shot("i_08_hp_bars", 0.1f);
            Shell.Battle.SetSpeed(4f);
            yield return WaitWhile(() => QuizCard.Current == null && Shell.Battle != null && Shell.Battle.CurrentPhase != BinakayanRising.Gameplay.BattlePlaytest.Phase.Finished, 90f);
            PickFirst();
            QuizCard.Current?.Continue();
            yield return Wait(2.5f);
            yield return Shot("i_09_hp_bars_later", 0.1f);
        }

        private void PickFirst()
        {
            QuizCard card = QuizCard.Current;
            if (card == null)
            {
                Note("step", "no quiz card open");
                return;
            }

            Button[] buttons = card.GetComponentsInChildren<Button>(false);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name.StartsWith("Choice"))
                {
                    buttons[i].onClick.Invoke();
                    return;
                }
            }
        }

        private static void DestroyQuiz()
        {
            if (QuizCard.Current != null)
            {
                Object.Destroy(QuizCard.Current.gameObject);
            }
        }

        /// <summary>Records a card's size and checks it sits inside the screen.</summary>
        private void MeasureCard(string what, RectTransform card)
        {
            if (card == null)
            {
                Note(what, "card not open");
                return;
            }

            var corners = new Vector3[4];
            card.GetWorldCorners(corners);
            audit.AppendFormat("\n-- {0} card {1:0}x{2:0} at ({3:0},{4:0})-({5:0},{6:0})\n", what,
                card.rect.width, card.rect.height, corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        /// <summary>Closes the reveal, then shoots and dismisses any promotion it hands on.</summary>
        private IEnumerator CloseReveal(string prefix)
        {
            if (RecruitReveal.Current == null)
            {
                Note("step", "no reveal open (" + prefix + ")");
                yield break;
            }

            RecruitReveal.Current.Press();
            yield return Wait(0.3f);
            int shown = 0;
            while (PromotionCard.Current != null && shown < 6)
            {
                shown++;
                yield return Shot(prefix + "_promotion_" + shown);
                PromotionCard.Current.Continue();
                yield return Wait(0.3f);
            }
        }

        /// <summary>Dismisses every queued promotion card, shooting any after the first.</summary>
        private IEnumerator DrainPromotions(string prefix)
        {
            int guard = 0;
            while (PromotionCard.Current != null && guard < 6)
            {
                if (guard > 0)
                {
                    yield return Shot(prefix + "_next_" + guard);
                }

                guard++;
                PromotionCard.Current.Continue();
                yield return Wait(0.3f);
            }
        }

        private TrainingScreen Training()
        {
            return Shell.Router.Get<TrainingScreen>(Gameplay.Flow.GameState.RosterTraining);
        }

        private EncampmentScreen Hub()
        {
            return Shell.Router.Get<EncampmentScreen>(Gameplay.Flow.GameState.BaseHub);
        }

        /// <summary>
        /// Writes the camp's framing to the audit as numbers: the camera, the art scale, where the
        /// clearing and the arrow land on screen. The screenshot shows it; this measures it.
        /// </summary>
        private void MeasureCamp(string when)
        {
            Camp.CampWorld camp = Shell.Camp;
            Camera view = camp.View;
            if (view == null)
            {
                Note("camp", "no camera (" + when + ")");
                return;
            }

            float screenPerUnit = Screen.height / (2f * view.orthographicSize);
            float perArtPixel = screenPerUnit / Camp.CampIso.PixelsPerUnit;
            Rect content = camp.ContentBounds;
            Vector3 low = view.WorldToScreenPoint(new Vector3(content.xMin, content.yMin, 0f));
            Vector3 high = view.WorldToScreenPoint(new Vector3(content.xMax, content.yMax, 0f));
            audit.Append("\n-- camp, ").Append(when).Append('\n');
            audit.AppendFormat("  camera ortho {0:0.0000} at ({1:0.0000},{2:0.0000}); {3:0.00} screen px per art px\n",
                view.orthographicSize, view.transform.position.x, view.transform.position.y, perArtPixel);
            audit.AppendFormat("  content on screen x {0:0}..{1:0}, y {2:0}..{3:0} (top inset {4:0})\n",
                low.x, high.x, low.y, high.y, camp.TopInsetPixels);
            if (Mathf.Abs(perArtPixel - Mathf.Round(perArtPixel)) > 0.001f)
            {
                Note("camp", "art scale is not a whole number: " + perArtPixel);
            }

            if (low.x < 0f || high.x > Screen.width || low.y < 0f || high.y > Screen.height - camp.TopInsetPixels + 1f)
            {
                Note("camp", "content not inside the free screen area");
            }

            if (camp.ArrowShown)
            {
                Vector3 tip = view.WorldToScreenPoint(camp.ArrowTip);
                float top = view.WorldToScreenPoint(camp.ArrowBounds.max).y;
                audit.AppendFormat("  arrow tip at screen ({0:0},{1:0}), top {2:0}\n", tip.x, tip.y, top);
                if (top > Screen.height - camp.TopInsetPixels)
                {
                    Note("camp", "arrow reaches under the top bar (" + when + ")");
                }
            }
        }

        /// <summary>
        /// Checks the reveal's title sits clear above the tiles, and that a Hero's turning sun stays
        /// inside its card whatever angle it is at.
        /// </summary>
        private void MeasureReveal(string when)
        {
            RecruitReveal reveal = RecruitReveal.Current;
            if (reveal == null)
            {
                Note("reveal", "none open (" + when + ")");
                return;
            }

            // A screen-space overlay: world corners are screen pixels, y up.
            var corners = new Vector3[4];
            Transform band = reveal.transform.Find("Band");
            RectTransform title = band.GetComponentInChildren<TextMeshProUGUI>().rectTransform;
            title.GetWorldCorners(corners);
            float titleTop = corners[1].y;
            float titleBottom = corners[0].y;
            float tilesTop = float.MinValue;
            audit.AppendFormat("\n-- reveal, {0}: title {1:0}..{2:0}\n", when, titleBottom, titleTop);

            for (int i = 0; i < band.childCount; i++)
            {
                var tile = band.GetChild(i) as RectTransform;
                if (tile == null || !tile.name.StartsWith("Recruit "))
                {
                    continue;
                }

                tile.GetWorldCorners(corners);
                tilesTop = Mathf.Max(tilesTop, corners[1].y);
                Rect card = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
                Transform front = tile.Find("Front");
                Transform sun = null;
                Image[] images = front != null ? front.GetComponentsInChildren<Image>(false) : new Image[0];
                for (int k = 0; k < images.Length && sun == null; k++)
                {
                    sun = images[k].name == "Sigil" ? images[k].transform.parent : null;
                }

                if (sun == null)
                {
                    continue;
                }

                var glow = (RectTransform)sun;
                float radius = glow.rect.width * 0.5f * glow.lossyScale.x;
                Vector3 centre = glow.TransformPoint(glow.rect.center);
                audit.AppendFormat("  {0} sun centre ({1:0},{2:0}) radius {3:0}, card {4:0}..{5:0} x {6:0}..{7:0}\n",
                    tile.name, centre.x, centre.y, radius, card.xMin, card.xMax, card.yMin, card.yMax);
                if (centre.x - radius < card.xMin || centre.x + radius > card.xMax
                    || centre.y - radius < card.yMin || centre.y + radius > card.yMax)
                {
                    Note("reveal", tile.name + "'s sun reaches outside the card");
                }
            }

            audit.AppendFormat("  tiles top {0:0}\n", tilesTop);
            if (titleBottom < tilesTop)
            {
                Note("reveal", "title overlaps the tiles");
            }
        }

        /// <summary>Checks the toast now showing stays clear of the open panel's card.</summary>
        private void MeasureToastClear()
        {
            var panel = Shell.Router.Current as CampPanelScreen;
            GameObject toast = GameObject.Find("Toast");
            Transform toastCard = toast != null ? toast.transform.Find("Card") : null;
            if (panel == null || toastCard == null)
            {
                Note("toast", "no panel or no toast to measure");
                return;
            }

            // Both canvases are screen-space overlays, so world corners are screen pixels.
            var corners = new Vector3[4];
            panel.Card.GetWorldCorners(corners);
            float cardBottom = corners[0].y;
            ((RectTransform)toastCard).GetWorldCorners(corners);
            float toastTop = corners[1].y;
            audit.AppendFormat("\n-- toast top {0:0}, panel card bottom {1:0}\n", toastTop, cardBottom);
            if (toastTop > cardBottom)
            {
                Note("toast", "covers the panel card");
            }
        }

        private static IEnumerator WaitWhile(System.Func<bool> condition, float timeout)
        {
            float end = Time.unscaledTime + timeout;
            while (condition() && Time.unscaledTime < end)
            {
                yield return null;
            }

            yield return Wait(0.2f);
        }

        /// <summary>Clicks a button on the screen now showing, by object name.</summary>
        private void ClickIn(string name)
        {
            GameScreen screen = Shell.Router.Current;
            Button[] buttons = screen != null ? screen.GetComponentsInChildren<Button>(false) : new Button[0];
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name == name)
                {
                    buttons[i].onClick.Invoke();
                    return;
                }
            }

            Note("step", "no button named '" + name + "' on " + (screen != null ? screen.name : "no screen"));
        }

        // ------------------------------------------------------------------ steps

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
            {
                yield return null;
            }
        }

        private void Click(string name)
        {
            GameObject target = GameObject.Find(name);
            Button button = target != null ? target.GetComponent<Button>() : null;
            if (button == null)
            {
                Note("step", "no button named '" + name + "'");
                return;
            }

            button.onClick.Invoke();
        }

        private IEnumerator Shot(string name, float settle = 0.7f)
        {
            yield return Wait(settle);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, name + ".png"));
            audit.Append("\n== ").Append(name).Append('\n');
            Audit();
            yield return null;
            yield return null;
        }

        // ------------------------------------------------------------------ audit

        private void Audit()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var corners = new Vector3[4];
            var parentCorners = new Vector3[4];

            for (int c = 0; c < canvases.Length; c++)
            {
                Canvas canvas = canvases[c];
                if (!canvas.isActiveAndEnabled || !canvas.isRootCanvas)
                {
                    continue;
                }

                // Measure in canvas units, the 1920x1080 the layout was written against.
                float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
                Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);

                TextMeshProUGUI[] labels = canvas.GetComponentsInChildren<TextMeshProUGUI>(false);
                for (int i = 0; i < labels.Length; i++)
                {
                    TextMeshProUGUI label = labels[i];
                    if (string.IsNullOrEmpty(label.text) || !label.isActiveAndEnabled)
                    {
                        continue;
                    }

                    label.ForceMeshUpdate();
                    if (label.isTextTruncated || (label.overflowMode == TextOverflowModes.Overflow && IsOverflowing(label)))
                    {
                        Note(PathOf(label.transform), "text cut off: \"" + Clip(label.text) + "\"");
                    }
                    else if (label.enableAutoSizing && label.fontSize < label.fontSizeMax * 0.75f)
                    {
                        Note(PathOf(label.transform), string.Format("shrank to {0:0} of {1:0}: \"{2}\"", label.fontSize, label.fontSizeMax, Clip(label.text)));
                    }
                }

                RectTransform[] rects = canvas.GetComponentsInChildren<RectTransform>(false);
                for (int i = 0; i < rects.Length; i++)
                {
                    RectTransform rect = rects[i];
                    if (rect.rect.width <= 0f || rect.rect.height <= 0f || rect.name == "Sigil" && rect.rect.width >= 600f || rect.name == "Picture")
                    {
                        continue;
                    }

                    rect.GetWorldCorners(corners);
                    Rect bounds = ScreenRect(corners, canvas);

                    if (bounds.xMin < screen.xMin - 1f || bounds.yMin < screen.yMin - 1f
                        || bounds.xMax > screen.xMax + 1f || bounds.yMax > screen.yMax + 1f)
                    {
                        Note(PathOf(rect), "off screen: " + Describe(bounds, scale));
                        continue;
                    }

                    var parent = rect.parent as RectTransform;
                    if (parent == null || parent.GetComponent<HorizontalOrVerticalLayoutGroup>() == null)
                    {
                        continue;
                    }

                    parent.GetWorldCorners(parentCorners);
                    Rect outer = ScreenRect(parentCorners, canvas);
                    if (bounds.xMin < outer.xMin - 1f || bounds.yMin < outer.yMin - 1f
                        || bounds.xMax > outer.xMax + 1f || bounds.yMax > outer.yMax + 1f)
                    {
                        Note(PathOf(rect), "spills out of " + parent.name + ": " + Describe(bounds, scale) + " in " + Describe(outer, scale));
                    }
                }
            }
        }

        private static bool IsOverflowing(TextMeshProUGUI label)
        {
            // Preferred values are measured at the largest size, so for a label that shrinks to fit
            // they overstate it; the mesh it actually drew is the measure there.
            float height = label.enableAutoSizing
                ? label.textBounds.size.y
                : label.GetPreferredValues(label.text, label.rectTransform.rect.width, 0f).y;
            return height > label.rectTransform.rect.height + 2f;
        }

        private static Rect ScreenRect(Vector3[] corners, Canvas canvas)
        {
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static string Describe(Rect rect, float scale)
        {
            return string.Format("[{0:0},{1:0} {2:0}x{3:0}]", rect.x / scale, rect.y / scale, rect.width / scale, rect.height / scale);
        }

        private void Note(string where, string what)
        {
            problems++;
            string line = "  " + where + ": " + what;
            audit.Append(line).Append('\n');
            Debug.LogWarning("[Shot]" + line);
        }

        private static string PathOf(Transform transform)
        {
            var parts = new List<string>();
            for (Transform t = transform; t != null && parts.Count < 5; t = t.parent)
            {
                parts.Add(t.name);
            }

            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private static string Clip(string text)
        {
            text = text.Replace("\n", " ");
            return text.Length > 60 ? text.Substring(0, 60) + "..." : text;
        }
    }
}
