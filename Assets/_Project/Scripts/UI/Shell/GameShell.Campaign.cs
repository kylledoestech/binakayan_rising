using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using BinakayanRising.Core.Meta;
using BinakayanRising.Gameplay;
using BinakayanRising.Gameplay.Flow;
using BinakayanRising.UI.Kit;
using UnityEngine;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// The campaign's way into a battle and back: the Mission Tent's Deploy starts the quest's
    /// battle with the player's own roster, and the battle's end settles XP, rewards and rank.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Rations are paid when the assault is fought, not when the board opens.</b> A player who
    /// looks at the board and retreats has spent nothing.
    /// </para>
    /// <para>
    /// <b>After a battle</b> the cards play in order: level-ups, then a rank earned, then the
    /// quest's closing cutscene. Each waits for the one before it.
    /// </para>
    /// </remarks>
    public sealed partial class GameShell
    {
        /// <summary>Most units offered on a battle's roster; the squad cap limits how many deploy.</summary>
        private const int RosterOffer = 8;

        private BattlePlaytest battle;
        private Quest battleQuest;

        /// <summary>The campaign battle on screen, or null.</summary>
        public BattlePlaytest Battle
        {
            get { return battle; }
        }

        /// <summary>True while a campaign card or panel is up (rank-up, cutscene, quiz, Library, battle pause menu).</summary>
        public bool ModalOpen
        {
            get
            {
                return PromotionCard.Current != null || RankUpCard.Current != null || RecruitReveal.Current != null
                    || CutscenePlayer.Current != null || QuizCard.Current != null || LibraryPanel.Current != null
                    || PauseMenu.Current != null || TacticianCommandCard.Current != null || BondRankCard.Current != null
                    || BondLorePanel.Current != null || LorePlayer.Current != null;
            }
        }

        /// <summary>
        /// Plays the quest's opening scene, then opens its battle. False, with an error sound,
        /// when the battle cannot be entered now.
        /// </summary>
        public bool LaunchQuest(Quest quest)
        {
            MetaGame game = Session.Game;
            if (game == null || battle != null || !game.CanLaunch(quest) || Machine.CurrentState != GameState.MissionPortal)
            {
                UiSfx.Play(UiSfx.Cue.Error);
                return false;
            }

            UiSfx.Play(UiSfx.Cue.Confirm);
            CutscenePlayer.Play(quest.PreCutscene, () => OpenBattle(quest));
            return true;
        }

        private void OpenBattle(Quest quest)
        {
            MetaGame game = Session.Game;
            if (game == null || !game.CanLaunch(quest) || !Machine.LoadIsometricMap())
            {
                UiSfx.Play(UiSfx.Cue.Error);
                return;
            }

            // The board draws itself; every campaign screen steps aside until it is done.
            if (Router.Current != null)
            {
                Router.Current.Hide(immediate: true);
            }

            canvas.enabled = false;

            battleQuest = quest;
            MissionSetup mission = BuildMission(game, quest);
            mission.Finished = FinishBattle;
            BattlePlaytest.PendingMission = mission;
            BattlePlaytest opened = new GameObject("Battle " + quest.Id).AddComponent<BattlePlaytest>();
            battle = opened;
            battle.QuizDue += AskBattleQuiz;
            battle.PhaseChanged += phase => OnBattlePhaseChanged(opened, phase);
            battle.HitLanded += critical => UiSfx.Play(UiSfx.Cue.Hit);
        }

        /// <summary>
        /// Keeps the state machine live with the board: the replay starting is Figure 2's
        /// "Lock Formation &amp; Start". A redeploy back to Deployment has no edge in the diagram,
        /// so the machine simply stays in Combat, and the next assault is a no-op for it.
        /// </summary>
        private void OnBattlePhaseChanged(BattlePlaytest from, BattlePlaytest.Phase phase)
        {
            if (from != battle)
            {
                return;
            }

            // The replay has resolved: the win or loss sting, over the battle music (#49).
            if (phase == BattlePlaytest.Phase.Finished)
            {
                MusicPlayer.Sting(from.MissionWon);
                return;
            }

            if (phase != BattlePlaytest.Phase.Combat)
            {
                return;
            }

            Machine.EnterCombat();
        }

        /// <summary>
        /// The battle's quiz turn: one question of the quest's level, the first the player has not
        /// met yet. A right answer pays Table 4's Reales. The replay resumes on Continue.
        /// </summary>
        private void AskBattleQuiz()
        {
            MetaGame game = Session.Game;
            BattlePlaytest asking = battle;
            if (game == null || asking == null || battleQuest == null)
            {
                if (asking != null)
                {
                    asking.SetPaused(false);
                }

                return;
            }

            Question question = Learning.NextQuiz(battleQuest.Level, game.Data.askedQuestions, battleQuest.Battle.Seed + game.Data.quizAnswered);
            var list = new List<Question>();
            if (question != null)
            {
                list.Add(question);

                // Figure 2: Combat --Mid-Combat Trigger--> Quiz, and back on Continue.
                if (Machine.EnterCombat())
                {
                    Machine.TriggerQuiz();
                }
            }

            // #43: a right answer pays Table 4's Reales (credited by RecordQuizAnswer, announced by
            // the receiver) and then the Tactician's Command the player picks, re-fought into the
            // rest of the battle before the replay resumes. A wrong answer gives nothing.
            var rewards = new CampaignQuizRewards(asking);
            bool earnedCommand = false;
            QuizCard.Show(list, Loc.Get(TextKey.QuizTitle),
                (q, correct) =>
                {
                    int reales = game.RecordQuizAnswer(q.Id, correct);
                    rewards.AwardReales(reales);
                    earnedCommand = correct;
                },
                score =>
                {
                    if (!earnedCommand || asking == null)
                    {
                        ResumeAfterQuiz(asking);
                        return;
                    }

                    TacticianCommandCard.Show(asking.CanIssueCommand, command =>
                    {
                        if (command != TacticianCommand.None)
                        {
                            rewards.ApplyRewardEffect(CampaignQuizRewards.ToEffect(command),
                                TacticianCommands.DefaultMagnitude(command), TacticianCommands.AttackTurns);
                        }

                        ResumeAfterQuiz(asking);
                    });
                });
        }

        /// <summary>Figure 2's Quiz --Continue--> Combat, and the replay runs on.</summary>
        private void ResumeAfterQuiz(BattlePlaytest asking)
        {
            if (Machine.CurrentState == GameState.Quiz)
            {
                Machine.ReturnToCombat();
            }

            if (asking != null)
            {
                asking.SetPaused(false);
            }
        }

        /// <summary>The quest's rules and the roster the player may deploy from.</summary>
        private static MissionSetup BuildMission(MetaGame game, Quest quest)
        {
            QuestBattle rules = quest.Battle;
            var mission = new MissionSetup
            {
                QuestId = quest.Id,
                Title = quest.Title.Get(),
                RankTitle = game.Rank.Title,
                EnemyCount = rules.EnemyCount,
                Enemies = rules.Enemies,
                WinRule = rules.WinRule,
                SquadCap = rules.SquadCap,
                Seed = rules.Seed,
                TurnCap = rules.TurnCap,
                HoldWins = rules.WinRule == WinRule.Hold,
                Tutorial = rules.Tutorial,
                QuizTurn = rules.QuizTurn,
                Level = quest.Level,

                // #19: the pairs fight at the ranks they have earned. The teaching battle keeps
                // every bond at rank A: it teaches what a bond does, and its win was checked so.
                Bonds = rules.Tutorial ? BondCatalog.AllAtRankA() : game.BattleBonds()
            };

            // The teaching battle is fought with exactly the five soldiers, and the stat blocks,
            // its win was checked against (TutorialScript); only the ids are the player's own.
            if (rules.Tutorial)
            {
                foreach (RosterEntry scripted in PlaytestScenario.KatipunanRoster())
                {
                    OwnedUnit owned = FirstOf(game, scripted.ArchetypeId);
                    if (owned != null)
                    {
                        mission.Roster.Add(new RosterEntry(
                            owned.id, scripted.DisplayName, scripted.ShortName, scripted.ArchetypeId, scripted.Stats));
                    }
                }

                if (mission.Roster.Count == PlaytestScenario.KatipunanRoster().Count)
                {
                    return mission;
                }

                mission.Roster.Clear();
            }

            // Otherwise the strongest soldiers first, so the best squad is the top of the list.
            var units = new List<OwnedUnit>(game.Units);
            units.Sort((a, b) => a.level != b.level ? b.level.CompareTo(a.level) : a.id.CompareTo(b.id));
            for (int i = 0; i < units.Count && mission.Roster.Count < RosterOffer; i++)
            {
                UnitArchetype archetype = UnitCatalog.Find(units[i].archetype);
                if (archetype == null)
                {
                    continue;
                }

                mission.Roster.Add(new RosterEntry(
                    units[i].id, archetype.Name.Get(), archetype.ShortName, archetype.Id, game.StatsOf(units[i])));
            }

            return mission;
        }

        private static OwnedUnit FirstOf(MetaGame game, string archetype)
        {
            IReadOnlyList<OwnedUnit> units = game.Units;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].archetype == archetype)
                {
                    return units[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Per frame: notices a battle that vanished without reporting, and shows a rank earned in
        /// the camp (a finished hub task can earn one) once nothing else is on screen.
        /// </summary>
        private void TickCampaign()
        {
            if (battleQuest != null && battle == null)
            {
                FinishBattle(new MissionReport { Retreated = true });
                return;
            }

            MetaGame game = Session.Game;
            if (game != null && game.PromotionOwed && battle == null
                && GameStateMachine.IsEncampmentSubState(Machine.CurrentState)
                && !SettingsOpen && !ModalOpen)
            {
                ShowRankUp(null);
            }
        }

        /// <summary>Settles a finished battle and returns to the camp.</summary>
        internal void FinishBattle(MissionReport report)
        {
            Quest quest = battleQuest;
            battleQuest = null;
            battle = null;
            canvas.enabled = true;

            // The machine may be in Deployment (a retreat before the assault), Combat, or Quiz (the
            // board vanished under a question); SettleBattle and LeaveBattle take legal edges from each.
            MetaGame game = Session.Game;
            if (quest == null || game == null)
            {
                Machine.LeaveBattle();
                return;
            }

            if (report == null || report.Retreated)
            {
                Machine.LeaveBattle();
                UiControls.Toast(Loc.Get(TextKey.MissionRetreated));
                return;
            }

            // Figure 2's path out of a battle: Combat, the outcome overlay, then the camp.
            Machine.SettleBattle(report.Won);

            game.TryLaunch(quest);
            QuestReward reward = game.CompleteBattle(quest, report.Won, report.Deployed);
            Machine.LeaveBattle();

            if (report.Won)
            {
                AnnounceQuest(reward);
            }
            else
            {
                UiSfx.Play(UiSfx.Cue.Error);
                UiControls.Toast(Loc.Get(TextKey.MissionLostToast), 3.4f);
            }

            // #19: every pair that fought side by side earns support; new ranks get their card
            // after the soldiers' promotions and before the player's own rank.
            List<BondRankUp> bondUps = game.RecordBondSupport(report.Placements);

            string closing = report.Won && reward.FirstClear ? quest.PostCutscene : null;
            PromotionCard.Show(reward.LevelUps,
                () => BondRankCard.Show(game, bondUps,
                    () => ShowRankUp(() => CutscenePlayer.Play(closing, null))));
        }

        /// <summary>Shows the rank-up card if a rank was earned and not yet shown, then continues.</summary>
        public void ShowRankUp(System.Action then)
        {
            MetaGame game = Session.Game;
            if (game == null || !game.PromotionOwed)
            {
                if (then != null)
                {
                    then();
                }

                return;
            }

            RankUpCard.Show(game, then);
        }
    }
}
