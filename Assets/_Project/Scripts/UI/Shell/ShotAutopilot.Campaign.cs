using System.Collections;
using System.Collections.Generic;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Meta;
using BinakayanRising.Gameplay;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// The "campaign" route: the whole campaign as a player meets it. A new game, every sub-quest
    /// in order, each battle auto-deployed and fought to the end (a lost one retried), every
    /// question answered, every card after it closed, and each level's Library test once the
    /// level is cleared.
    /// </summary>
    /// <remarks>
    /// Buttons are pressed the way a mouse presses them: a raycast at the button's centre, then
    /// down, up and click on whatever the raycast hit first. The other routes invoke
    /// <c>onClick</c> directly, which is how a Library scrim that swallowed the test's answers
    /// (#58) went unseen; here a button something else covers fails the run. The camp's hub
    /// tasks go through the same <see cref="MetaGame"/> calls their screens make, since phases 2
    /// and 3 already shoot those screens.
    /// </remarks>
    public sealed partial class ShotAutopilot
    {
        private const int BattleAttempts = 3;

        private bool mapChecked;

        /// <summary>
        /// The run skips the first-battle tutorial, which is remembered in PlayerPrefs rather than
        /// the throwaway save folder; put the flag back so this machine's player still sees it.
        /// </summary>
        private IEnumerator CampaignRoute()
        {
            bool tutorialSeen = UserPrefs.TutorialDone;
            yield return CampaignRun();
            if (!tutorialSeen)
            {
                UserPrefs.ResetTutorial();
            }
        }

        private IEnumerator CampaignRun()
        {
            Shell.Session.DeleteSave();
            Shell.StartNewCampaign();
            yield return Wait(0.6f);
            yield return ClearCards("start");

            MetaGame game = Shell.Session.Game;
            IReadOnlyList<Quest> quests = Campaign.Quests;
            for (int i = 0; i < quests.Count; i++)
            {
                Quest quest = quests[i];
                if (game.CurrentQuest != quest)
                {
                    Note("campaign", quest.Id + " is not the current quest (" + (game.CurrentQuest != null ? game.CurrentQuest.Id : "none") + ")");
                    yield break;
                }

                if (quest.Kind == QuestKind.HubTask)
                {
                    yield return DoHubTasks(game, quest);
                }
                else
                {
                    yield return FightQuest(game, quest);
                }

                yield return ClearCards(quest.Id);
                if (!game.IsCleared(quest))
                {
                    Note("campaign", quest.Id + " did not clear; the run stops here");
                    yield return Shot("campaign_stuck_" + quest.Id, 0.5f);
                    yield break;
                }

                if (i + 1 == quests.Count || quests[i + 1].Level != quest.Level)
                {
                    yield return TakeLibraryTest(game, quest.Level);
                }
            }

            if (game.CurrentQuest != null)
            {
                Note("campaign", "every quest played but " + game.CurrentQuest.Id + " is still open");
            }

            Line("campaign won: " + quests.Count + " quests cleared, rank " + game.Rank.Title
                + ", " + game.Data.reales + " Reales, " + game.Units.Count + " units");
            yield return Shot("campaign_end", 1f);
        }

        // ------------------------------------------------------------------ hub tasks

        private IEnumerator DoHubTasks(MetaGame game, Quest quest)
        {
            for (int i = 0; i < quest.Tasks.Length; i++)
            {
                string task = quest.Tasks[i];
                string where = quest.Id + " " + task;
                if (!DoTask(game, task))
                {
                    // Out of what the task spends: a player would wait on the farm and mine, so
                    // the run tops up and says so rather than stopping.
                    game.Earn(Currency.Reales, 500);
                    game.Earn(Currency.Rations, 40);
                    game.Earn(Currency.Scrap, 40);
                    Line(where + ": short of resources, topped up");
                    if (!DoTask(game, task))
                    {
                        yield return WaitForHarvest(game, task);
                    }
                }

                if (!game.IsTaskDone(quest, task) && !game.IsCleared(quest))
                {
                    Note(where, "task not marked done");
                }

                yield return ClearCards(where);
            }
        }

        private static bool DoTask(MetaGame game, string task)
        {
            switch (task)
            {
                case Campaign.TaskTalkAide:
                    // The aide's welcome ends with this call; the dialogue itself is shot by phase 2.
                    game.MarkTask(Campaign.TaskTalkAide);
                    return true;

                case Campaign.TaskRecruit:
                    return game.TryPull(1) != null;

                case Campaign.TaskTrain:
                    for (int i = 0; i < game.Units.Count; i++)
                    {
                        LevelUp levelUp;
                        if (game.TryDrill(game.Units[i].id, out levelUp))
                        {
                            return true;
                        }
                    }

                    return false;

                case Campaign.TaskEquip:
                    for (int w = 0; w < game.Weapons.Count; w++)
                    {
                        for (int u = 0; u < game.Units.Count; u++)
                        {
                            if (game.CanWield(game.Units[u], game.Weapons[w]) && game.TryEquip(game.Units[u].id, game.Weapons[w].id))
                            {
                                return true;
                            }
                        }
                    }

                    return false;

                case Campaign.TaskHarvestFarm:
                    return game.Harvest(Facility.Farm) > 0;

                case Campaign.TaskHarvestMine:
                    return game.Harvest(Facility.Mine) > 0;

                case Campaign.TaskExchange:
                    return game.Exchange(Currency.Rations, 1) > 0 || game.Exchange(Currency.Scrap, 1) > 0;

                default:
                    return false;
            }
        }

        /// <summary>A harvest needs something grown; waits out one unit of production, then harvests.</summary>
        private IEnumerator WaitForHarvest(MetaGame game, string task)
        {
            Facility facility;
            if (task == Campaign.TaskHarvestFarm)
            {
                facility = Facility.Farm;
            }
            else if (task == Campaign.TaskHarvestMine)
            {
                facility = Facility.Mine;
            }
            else
            {
                Note(task, "could not be done even after a top-up");
                yield break;
            }

            yield return WaitWhile(() => game.Stored(facility) == 0, 60f);
            if (game.Harvest(facility) == 0)
            {
                Note(task, "nothing to harvest after a minute");
            }
        }

        // ------------------------------------------------------------------ battles

        private IEnumerator FightQuest(MetaGame game, Quest quest)
        {
            for (int attempt = 1; attempt <= BattleAttempts && !game.IsCleared(quest); attempt++)
            {
                if (!game.CanLaunch(quest))
                {
                    game.Earn(Currency.Rations, quest.RationsCost);
                    Line(quest.Id + ": short of Rations to launch, topped up");
                }

                // The way a player gets there: walk to the Mission Tent, pick the node, Deploy.
                if (!(Shell.Router.Current is MissionMapScreen))
                {
                    Shell.Camp.ClickSite(Places.MissionTent);
                    yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
                    yield return WaitWhile(() => !(Shell.Router.Current is MissionMapScreen), 4f);
                }

                var map = Shell.Router.Current as MissionMapScreen;
                if (map == null)
                {
                    Note(quest.Id, "the Mission Tent did not open the map");
                    yield return Shot("campaign_" + quest.Id + "_no_map", 0.2f);
                    yield break;
                }

                yield return Wait(0.4f);
                if (!mapChecked)
                {
                    mapChecked = true;
                    CheckMapNodes(map, game.CurrentQuest);
                    yield return Shot("campaign_map", 0.2f);
                }

                PressIn(map, "Node " + quest.Id, quest.Id + " map");
                yield return Wait(0.3f);
                PressIn(map, "Button Deploy", quest.Id + " map");
                yield return Wait(0.5f);

                // The quest's opening scene, then the board.
                float waited = 0f;
                while (Shell.Battle == null && waited < 20f)
                {
                    if (CutscenePlayer.Current != null)
                    {
                        PressIn(CutscenePlayer.Current, "Button Cutscene Skip", quest.Id + " opening");
                    }

                    yield return Wait(0.5f);
                    waited += 0.5f;
                }

                BattlePlaytest battle = Shell.Battle;
                if (battle == null)
                {
                    Note(quest.Id, "the battle did not open");
                    yield break;
                }

                yield return Wait(1.5f);

                // The first battle opens the guided tutorial; a returning player skips it the same way.
                Transform tutorial = battle.transform.Find("Tutorial");
                if (tutorial != null && FindButton(tutorial, "Button Skip") != null)
                {
                    Line(quest.Id + ": the tutorial opened; skipped it");
                    PressIn(tutorial, "Button Skip", quest.Id + " tutorial");
                    yield return Wait(0.5f);
                }

                var hud = Object.FindAnyObjectByType<BinakayanRising.UI.Screens.BattleHud>();
                PressIn(hud, "Button Auto Deploy", quest.Id + " deploy");
                yield return Wait(0.5f);
                PressIn(hud, "Button Begin Assault", quest.Id + " deploy");
                yield return Wait(1f);
                battle.SetSpeed(8f);

                waited = 0f;
                while (Shell.Battle == battle && battle.CurrentPhase != BattlePlaytest.Phase.Finished && waited < 300f)
                {
                    yield return AnswerByClick(quest.Id + " battle question");
                    yield return Wait(0.5f);
                    waited += 0.5f;
                }

                if (battle.CurrentPhase != BattlePlaytest.Phase.Finished || battle.Result == null)
                {
                    Note(quest.Id, "the battle did not finish in " + waited + " s");
                    yield return Shot("campaign_" + quest.Id + "_hung", 0.2f);
                    yield break;
                }

                audit.AppendFormat("\n-- {0} attempt {1}: {2} after {3} turns, won {4}, katipunan {5}, spanish {6}\n",
                    quest.Id, attempt, battle.Result.Outcome, battle.Result.TurnsElapsed, battle.MissionWon,
                    battle.Result.KatipunanAlive, battle.Result.SpanishAlive);
                yield return Shot("campaign_" + quest.Id + "_report_" + attempt, 1.5f);

                yield return WaitWhile(() => FindButton(hud, "Button Return To Camp") == null, 5f);
                PressIn(hud, "Button Return To Camp", quest.Id + " report");
                yield return WaitWhile(() => Shell.Battle != null, 8f);
                if (Shell.Battle != null)
                {
                    Note(quest.Id, "Return To Camp did not leave the battle");
                    Shell.Battle.EndMission();
                    yield return Wait(1f);
                }

                yield return ClearCards(quest.Id);
            }

            if (!game.IsCleared(quest))
            {
                Note(quest.Id, "lost " + BattleAttempts + " times with auto-deploy");
            }
        }

        /// <summary>Answers an open question with its right choice, then takes a command if one is offered.</summary>
        private IEnumerator AnswerByClick(string where)
        {
            QuizCard quiz = QuizCard.Current;
            if (quiz != null)
            {
                yield return Wait(0.2f);
                PressIn(quiz, "Choice " + "ABCD"[RightChoiceOnScreen(quiz)], where);
                yield return Wait(0.3f);
                PressIn(quiz, "Button Quiz Continue", where);
                yield return Wait(0.4f);
            }

            TacticianCommandCard card = TacticianCommandCard.Current;
            if (card != null)
            {
                for (int i = 0; i < TacticianCommandCard.Commands.Length; i++)
                {
                    if (card.IsAllowed(i))
                    {
                        PressIn(card, "Command " + (i + 1), where + " command");
                        break;
                    }
                }

                yield return WaitWhile(() => TacticianCommandCard.Current != null, 3f);
            }
        }

        /// <summary>The right choice for the question on <paramref name="quiz"/>, found by its prompt; 0 if unknown.</summary>
        private int RightChoiceOnScreen(QuizCard quiz)
        {
            TextMeshProUGUI[] texts = quiz.GetComponentsInChildren<TextMeshProUGUI>(false);
            for (int level = 1; level <= Campaign.LevelCount; level++)
            {
                List<Question> questions = Learning.QuestionsIn(level);
                for (int q = 0; q < questions.Count; q++)
                {
                    string prompt = questions[q].Text.Get();
                    for (int t = 0; t < texts.Length; t++)
                    {
                        if (texts[t].text == prompt)
                        {
                            return questions[q].Answer;
                        }
                    }
                }
            }

            Note("quiz", "question on screen not found in the bank");
            return 0;
        }

        // ------------------------------------------------------------------ Library

        private IEnumerator TakeLibraryTest(MetaGame game, int level)
        {
            string where = "level " + level + " test";
            if (!game.IsAssessmentOpen(level))
            {
                Note(where, "not open after the level was cleared");
                yield break;
            }

            LibraryPanel library = LibraryPanel.Open(Shell);
            yield return Wait(0.6f);
            if (library == null)
            {
                Note(where, "the Library did not open");
                yield break;
            }

            PressIn(library, "Button Test " + level, where);
            yield return Wait(0.6f);
            for (int guard = 0; QuizCard.Current != null && guard < 20; guard++)
            {
                QuizCard quiz = QuizCard.Current;
                PressIn(quiz, "Choice " + "ABCD"[RightChoiceOnScreen(quiz)], where);
                yield return Wait(0.3f);
                if (guard == 0)
                {
                    yield return Shot("campaign_test_" + level, 0.2f);
                }

                PressIn(quiz, "Button Quiz Continue", where);
                yield return Wait(0.4f);
            }

            AssessmentRecord record = game.Assessment(level);
            if (record == null || record.attempts != 1 || !record.passed)
            {
                Note(where, "not recorded as one passed attempt ("
                    + (record == null ? "no record" : record.attempts + " attempts, " + record.best + "/" + record.total) + ")");
            }
            else
            {
                Line(where + ": " + record.best + "/" + record.total + ", passed");
            }

            yield return Wait(0.5f);
            PressIn(library, "Button Close X", where);
            yield return Wait(0.5f);
            if (LibraryPanel.Current != null)
            {
                Note(where, "the Library did not close");
                LibraryPanel.Current.Close();
            }
        }

        // ------------------------------------------------------------------ cards

        /// <summary>Closes every card, scene and dialogue that queues up after a step, by their buttons.</summary>
        private IEnumerator ClearCards(string where)
        {
            yield return Wait(0.5f);
            for (int guard = 0; guard < 40; guard++)
            {
                if (CutscenePlayer.Current != null)
                {
                    PressIn(CutscenePlayer.Current, "Button Cutscene Skip", where + " scene");
                }
                else if (PromotionCard.Current != null)
                {
                    PressIn(PromotionCard.Current, "Button Continue", where + " promotion");
                }
                else if (BondRankCard.Current != null)
                {
                    PressIn(BondRankCard.Current, "Button Continue", where + " bond rank");
                }
                else if (RankUpCard.Current != null)
                {
                    PressIn(RankUpCard.Current, "Button Continue", where + " rank");
                }
                else if (Hub() != null && Hub().Dialogue != null && Hub().Dialogue.IsOpen)
                {
                    Hub().Dialogue.Finish();
                }
                else
                {
                    yield break;
                }

                yield return Wait(0.5f);
            }

            Note(where, "cards kept coming after 40 presses");
        }

        // ------------------------------------------------------------------ pressing

        private static Button FindButton(Component root, string name)
        {
            if (root == null)
            {
                return null;
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(false);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name == name)
                {
                    return buttons[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Presses the button named <paramref name="name"/> under <paramref name="root"/> through the
        /// event system, as a mouse would. Notes it when the button is missing, disabled, or covered.
        /// </summary>
        private bool PressIn(Component root, string name, string where)
        {
            Button button = FindButton(root, name);
            if (button == null || !button.IsInteractable())
            {
                Note(where, "'" + name + "' " + (button == null ? "not found" : "not interactable"));
                return false;
            }

            PointerEventData pointer = PointerAt(button, out RaycastResult hit);
            GameObject top = hit.gameObject;
            if (top == null || !top.transform.IsChildOf(button.transform))
            {
                Note(where, "'" + name + "' is covered by " + (top == null ? "nothing" : PathOf(top.transform)));
                return false;
            }

            pointer.pointerPressRaycast = hit;
            pointer.pointerCurrentRaycast = hit;
            ExecuteEvents.ExecuteHierarchy(top, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(top, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(top, pointer, ExecuteEvents.pointerClickHandler);
            return true;
        }

        /// <summary>A left-button pointer at the centre of <paramref name="button"/>, and the first thing a raycast there hits.</summary>
        private static PointerEventData PointerAt(Button button, out RaycastResult hit)
        {
            var rect = (RectTransform)button.transform;
            Canvas canvas = button.GetComponentInParent<Canvas>().rootCanvas;
            Camera eye = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(eye, rect.TransformPoint(rect.rect.center));

            EventSystem events = EventSystem.current;
            var pointer = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            events.RaycastAll(pointer, hits);
            hit = hits.Count > 0 ? hits[0] : default(RaycastResult);
            return pointer;
        }

        /// <summary>
        /// Reports each map node another covers at its centre, with the distance between the two
        /// centres in screen pixels. The current sub-quest's node must take its click; any other
        /// covered is only logged, since Kawit's two nodes overlap by design.
        /// </summary>
        private void CheckMapNodes(MissionMapScreen map, Quest current)
        {
            Button[] buttons = map.GetComponentsInChildren<Button>(false);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (!buttons[i].name.StartsWith("Node "))
                {
                    continue;
                }

                PointerAt(buttons[i], out RaycastResult hit);
                if (hit.gameObject != null && hit.gameObject.transform.IsChildOf(buttons[i].transform))
                {
                    continue;
                }

                Button cover = hit.gameObject != null ? hit.gameObject.GetComponentInParent<Button>() : null;
                string by = hit.gameObject == null ? "nothing" : PathOf(hit.gameObject.transform);
                if (cover != null)
                {
                    var own = (RectTransform)buttons[i].transform;
                    var other = (RectTransform)cover.transform;
                    Vector2 a = RectTransformUtility.WorldToScreenPoint(null, own.TransformPoint(own.rect.center));
                    Vector2 b = RectTransformUtility.WorldToScreenPoint(null, other.TransformPoint(other.rect.center));
                    by += string.Format(" (centres {0:0} px apart, hit area {1:0} px wide)", Vector2.Distance(a, b), own.rect.width * own.lossyScale.x);
                }

                string what = "'" + buttons[i].name + "' is covered at its centre by " + by;
                if (current != null && buttons[i].name == "Node " + current.Id)
                {
                    Note("map", what);
                }
                else
                {
                    Line("map: " + what);
                }
            }
        }
    }
}
