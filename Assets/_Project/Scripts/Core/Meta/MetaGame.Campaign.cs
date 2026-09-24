using System.Collections.Generic;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;

namespace BinakayanRising.Core.Meta
{
    /// <summary>What clearing a sub-quest gave.</summary>
    public sealed class QuestReward
    {
        public Quest Quest;

        /// <summary>False on a replay of an already-cleared battle.</summary>
        public bool FirstClear;

        public int Reales;

        /// <summary>The weapon added to the armoury, or null.</summary>
        public string Weapon;

        /// <summary>The lesson unlocked, or null.</summary>
        public string Lesson;

        /// <summary>True when this clear finished a campaign level.</summary>
        public bool LevelCleared;

        /// <summary>Level-ups from battle XP.</summary>
        public List<LevelUp> LevelUps = new List<LevelUp>();
    }

    /// <summary>Where the navigation guide points: one line of text and one place.</summary>
    public sealed class Objective
    {
        public LocString Text;

        /// <summary>A <see cref="Places"/> id, or null when there is nowhere to point.</summary>
        public string Place;

        /// <summary>The sub-quest this objective belongs to, or null.</summary>
        public Quest Quest;
    }

    /// <summary>A graded level assessment.</summary>
    public sealed class AssessmentOutcome
    {
        public int Score;
        public int Total;
        public bool Passed;

        /// <summary>True the first time this level's assessment is passed.</summary>
        public bool FirstPass;

        public int Reales;
    }

    public sealed partial class MetaGame
    {
        // ------------------------------------------------------------------ progress

        public bool IsCleared(Quest quest)
        {
            return quest != null && Data.clearedQuests.Contains(quest.Id);
        }

        /// <summary>
        /// The campaign is linear (Appendix F): a sub-quest opens when the one before it is
        /// cleared. Cleared battles stay open for replays.
        /// </summary>
        public bool IsUnlocked(Quest quest)
        {
            if (quest == null)
            {
                return false;
            }

            IReadOnlyList<Quest> quests = Campaign.Quests;
            for (int i = 0; i < quests.Count; i++)
            {
                if (quests[i] == quest)
                {
                    return i == 0 || IsCleared(quests[i - 1]);
                }
            }

            return false;
        }

        /// <summary>The first sub-quest not yet cleared, or null once the campaign is won.</summary>
        public Quest CurrentQuest
        {
            get
            {
                IReadOnlyList<Quest> quests = Campaign.Quests;
                for (int i = 0; i < quests.Count; i++)
                {
                    if (!IsCleared(quests[i]))
                    {
                        return quests[i];
                    }
                }

                return null;
            }
        }

        public bool CampaignComplete
        {
            get { return CurrentQuest == null; }
        }

        public bool IsLevelCleared(int level)
        {
            List<Quest> quests = Campaign.QuestsIn(level);
            if (quests.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < quests.Count; i++)
            {
                if (!IsCleared(quests[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public int LevelsCleared
        {
            get
            {
                int count = 0;
                for (int level = 1; level <= Campaign.LevelCount; level++)
                {
                    if (!IsLevelCleared(level))
                    {
                        break;
                    }

                    count++;
                }

                return count;
            }
        }

        /// <summary>The level the player is currently playing: the current quest's, or the last.</summary>
        public int CurrentLevel
        {
            get
            {
                Quest quest = CurrentQuest;
                return quest == null ? Campaign.LevelCount : quest.Level;
            }
        }

        /// <summary>Whether <paramref name="task"/> has been done for <paramref name="quest"/>.</summary>
        public bool IsTaskDone(Quest quest, string task)
        {
            return quest != null && Data.HasFlag(TaskFlag(quest, task));
        }

        // ------------------------------------------------------------------ rank

        /// <summary>The rank the player has earned: the highest whose milestone sub-quest is cleared.</summary>
        public PlayerRank Rank
        {
            get { return PlayerRanks.ForCleared(Data.clearedQuests); }
        }

        /// <summary>True when a rank was earned that the player has not yet been shown.</summary>
        public bool PromotionOwed
        {
            get { return Rank.Index > Data.rankShown; }
        }

        /// <summary>Records that the promotion card for the current rank has been shown.</summary>
        public void AcknowledgeRank()
        {
            if (Data.rankShown != Rank.Index)
            {
                Data.rankShown = Rank.Index;
                RaiseChanged();
            }
        }

        // ------------------------------------------------------------------ battles

        /// <summary>Whether <paramref name="quest"/>'s battle can be entered now.</summary>
        public bool CanLaunch(Quest quest)
        {
            return quest != null && quest.Kind == QuestKind.Battle && IsUnlocked(quest)
                && Data.rations >= quest.RationsCost && Data.units.Count > 0;
        }

        /// <summary>Pays the Rations to enter a battle (GDD: "depletes upon entering a stage").</summary>
        public bool TryLaunch(Quest quest)
        {
            if (!CanLaunch(quest))
            {
                return false;
            }

            Data.rations -= quest.RationsCost;
            string launched = "launched.level." + quest.Level;
            if (!Data.flags.Contains(launched))
            {
                Data.flags.Add(launched);
            }

            RaiseChanged();
            return true;
        }

        /// <summary>
        /// Settles a finished battle: XP for every deployed unit, and on a win the quest's reward
        /// — in full the first time, half the Reales on a replay.
        /// </summary>
        public QuestReward CompleteBattle(Quest quest, bool won, IList<int> deployedUnitIds)
        {
            var reward = new QuestReward { Quest = quest };
            reward.LevelUps = AwardXpSilently(deployedUnitIds, won ? Rules.VictoryXp : Rules.DefeatXp);

            if (won)
            {
                if (IsCleared(quest))
                {
                    reward.Reales = quest.RewardReales / 2;
                    AddSilently(Currency.Reales, reward.Reales);
                }
                else
                {
                    GrantFirstClear(quest, reward);
                }
            }

            RaiseChanged();
            return reward;
        }

        private List<LevelUp> AwardXpSilently(IList<int> unitIds, int xp)
        {
            var ups = new List<LevelUp>();
            if (unitIds == null)
            {
                return ups;
            }

            for (int i = 0; i < unitIds.Count; i++)
            {
                OwnedUnit unit = FindUnit(unitIds[i]);
                LevelUp up = unit == null ? null : GrantXp(unit, xp);
                if (up != null)
                {
                    ups.Add(up);
                }
            }

            return ups;
        }

        private void GrantFirstClear(Quest quest, QuestReward reward)
        {
            int levelsBefore = LevelsCleared;
            Data.clearedQuests.Add(quest.Id);

            reward.FirstClear = true;
            reward.Reales = quest.RewardReales;
            AddSilently(Currency.Reales, quest.RewardReales);

            if (!string.IsNullOrEmpty(quest.RewardWeapon) && WeaponCatalog.Find(quest.RewardWeapon) != null)
            {
                AddWeaponSilently(quest.RewardWeapon);
                reward.Weapon = quest.RewardWeapon;
            }

            if (!string.IsNullOrEmpty(quest.RewardLesson) && !Data.lessons.Contains(quest.RewardLesson))
            {
                Data.lessons.Add(quest.RewardLesson);
                reward.Lesson = quest.RewardLesson;
            }

            reward.LevelCleared = LevelsCleared > levelsBefore;
        }

        /// <summary>
        /// A hub-task sub-quest clears itself the moment its last step is done — the player is
        /// never asked to go back and "turn it in".
        /// </summary>
        private void CompleteHubTaskIfDone()
        {
            Quest quest = CurrentQuest;
            if (quest == null || quest.Kind != QuestKind.HubTask || quest.Tasks.Length == 0)
            {
                return;
            }

            for (int i = 0; i < quest.Tasks.Length; i++)
            {
                if (!IsTaskDone(quest, quest.Tasks[i]))
                {
                    return;
                }
            }

            var reward = new QuestReward { Quest = quest };
            GrantFirstClear(quest, reward);

            System.Action<QuestReward> handler = QuestCompleted;
            if (handler != null)
            {
                handler(reward);
            }

        }

        // ------------------------------------------------------------------ navigation guide

        private static readonly LocString OpenMissionTent = new LocString(
            "Open the Mission Tent and choose your next sub-quest",
            "Buksan ang Tolda ng Misyon at piliin ang susunod na misyon");

        private static readonly LocString CampaignDone = new LocString(
            "The campaign is won. Replay battles or review your lessons",
            "Tagumpay ang kampanya. Ulitin ang labanan o balikan ang mga aralin");

        private static readonly LocString NeedRations = new LocString(
            "Not enough Rations for the next battle - harvest the Farm",
            "Kulang ang Rasyon para sa susunod na labanan - umani sa Bukid");

        private static readonly LocString TakeAssessment = new LocString(
            "Take the Level {0} assessment at the Library",
            "Sagutan ang pagsusulit ng Antas {0} sa Aklatan");

        /// <summary>
        /// The one thing the player should do next, for the navigation guide. The campaign comes
        /// first; an untaken assessment for a finished level is suggested once the next battle is
        /// not what is blocking.
        /// </summary>
        public Objective CurrentObjective
        {
            get
            {
                Quest quest = CurrentQuest;
                if (quest == null)
                {
                    int pending = UntakenAssessment();
                    if (pending > 0)
                    {
                        return AssessmentObjective(pending);
                    }

                    return new Objective { Text = CampaignDone, Place = Places.MissionTent };
                }

                if (quest.Kind == QuestKind.HubTask)
                {
                    for (int i = 0; i < quest.Tasks.Length; i++)
                    {
                        if (!IsTaskDone(quest, quest.Tasks[i]))
                        {
                            HubTask task = Campaign.Task(quest.Tasks[i]);
                            if (task != null)
                            {
                                return new Objective { Text = task.Text, Place = task.Place, Quest = quest };
                            }
                        }
                    }
                }

                if (quest.Kind == QuestKind.Battle && Data.rations < quest.RationsCost)
                {
                    return new Objective { Text = NeedRations, Place = Places.Farm, Quest = quest };
                }

                int untaken = UntakenAssessment();
                // A finished level's assessment is suggested until it is taken or the player
                // moves on and launches a battle of the next level.
                if (untaken > 0 && quest.Level > untaken && !Data.HasFlag("launched.level." + quest.Level))
                {
                    return AssessmentObjective(untaken);
                }

                return new Objective { Text = OpenMissionTent, Place = Places.MissionTent, Quest = quest };
            }
        }

        private static Objective AssessmentObjective(int level)
        {
            var text = new LocString(
                string.Format(TakeAssessment.English, level),
                string.Format(TakeAssessment.Filipino, level));
            return new Objective { Text = text, Place = Places.Library };
        }

        /// <summary>The lowest cleared level whose assessment has never been attempted, or 0.</summary>
        private int UntakenAssessment()
        {
            for (int level = 1; level <= Campaign.LevelCount; level++)
            {
                if (IsLevelCleared(level) && Assessment(level) == null)
                {
                    return level;
                }
            }

            return 0;
        }

        // ------------------------------------------------------------------ learning

        public bool IsLessonUnlocked(string lessonId)
        {
            return Data.lessons.Contains(lessonId);
        }

        public AssessmentRecord Assessment(int level)
        {
            for (int i = 0; i < Data.assessments.Count; i++)
            {
                if (Data.assessments[i].level == level)
                {
                    return Data.assessments[i];
                }
            }

            return null;
        }

        /// <summary>An assessment opens when its level is cleared.</summary>
        public bool IsAssessmentOpen(int level)
        {
            return IsLevelCleared(level);
        }

        /// <summary>Records an attempt. The first pass pays a bonus; retakes are always free.</summary>
        public AssessmentOutcome RecordAssessment(int level, int score, int total)
        {
            AssessmentRecord record = Assessment(level);
            if (record == null)
            {
                record = new AssessmentRecord { level = level };
                Data.assessments.Add(record);
            }

            if (total < 1) total = 1;
            if (score < 0) score = 0;
            if (score > total) score = total;

            var outcome = new AssessmentOutcome
            {
                Score = score,
                Total = total,
                Passed = score * 100 >= Rules.AssessmentPassPercent * total
            };

            record.attempts++;
            record.total = total;
            if (score > record.best)
            {
                record.best = score;
            }

            if (outcome.Passed && !record.passed)
            {
                record.passed = true;
                outcome.FirstPass = true;
                outcome.Reales = Rules.AssessmentFirstPassReales;
                AddSilently(Currency.Reales, outcome.Reales);
            }

            RaiseChanged();
            return outcome;
        }

        /// <summary>Records a mid-battle quiz answer. Table 4: +50 Reales when correct.</summary>
        /// <returns>Reales earned.</returns>
        public int RecordQuizAnswer(string questionId, bool correct)
        {
            Data.quizAnswered++;
            if (!string.IsNullOrEmpty(questionId) && !Data.askedQuestions.Contains(questionId))
            {
                Data.askedQuestions.Add(questionId);
            }

            int reales = 0;
            if (correct)
            {
                Data.quizCorrect++;
                reales = Rules.QuizCorrectReales;
                AddSilently(Currency.Reales, reales);
            }

            RaiseChanged();
            return reales;
        }
    }
}
