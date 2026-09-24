using System.Collections.Generic;
using BinakayanRising.Core.Localization;

namespace BinakayanRising.Core.Content
{
    /// <summary>How a sub-quest is played.</summary>
    public enum QuestKind
    {
        /// <summary>Done in the encampment by completing its <see cref="Quest.Tasks"/>.</summary>
        HubTask = 0,

        /// <summary>A deployment and an auto-battle.</summary>
        Battle = 1
    }

    /// <summary>What counts as winning a battle.</summary>
    public enum WinRule
    {
        /// <summary>Rout every enemy (the proposal's default win condition).</summary>
        Rout = 0,

        /// <summary>
        /// Still have a unit standing when the turn cap is reached. The battle's draw becomes a
        /// win. No quest uses it since q06 became an escort; kept for saves and the playtest.
        /// </summary>
        Hold = 1,

        /// <summary>
        /// Keep the supply cart alive until the turn cap. It never fights; losing it loses the
        /// battle. "Forging the Earthworks" (q06, #37).
        /// </summary>
        Escort = 2,

        /// <summary>
        /// End a turn with any squad member on the marked powder magazine behind the Spanish line,
        /// before the turn cap. "The Silent Sabotage" (q07, #38).
        /// </summary>
        Sabotage = 3
    }

    /// <summary>So many of one Spanish archetype in a battle's column.</summary>
    public struct EnemyGroup
    {
        /// <summary>A <see cref="UnitCatalog"/> id of the Spanish side.</summary>
        public readonly string ArchetypeId;

        /// <summary>How many of them.</summary>
        public readonly int Count;

        public EnemyGroup(int count, string archetypeId)
        {
            Count = count;
            ArchetypeId = archetypeId;
        }
    }

    /// <summary>The battle half of a <see cref="QuestKind.Battle"/> sub-quest.</summary>
    public sealed class QuestBattle
    {
        /// <summary>Every Spanish unit, in the order they are numbered and formed up.</summary>
        public readonly IReadOnlyList<string> Enemies;

        public readonly int EnemyCount;
        public readonly int SquadCap;
        public readonly WinRule WinRule;
        public readonly int TurnCap;
        public readonly int Seed;

        /// <summary>Runs the guided battle tutorial over this battle.</summary>
        public readonly bool Tutorial;

        /// <summary>Asks one historical question at this turn. 0 for none.</summary>
        public readonly int QuizTurn;

        /// <summary>A battle against <paramref name="enemyCount"/> Spanish regulars.</summary>
        public QuestBattle(int enemyCount, int squadCap, WinRule winRule, int turnCap, int seed, bool tutorial, int quizTurn)
            : this(new[] { new EnemyGroup(enemyCount, UnitCatalog.SpanishRegular) }, squadCap, winRule, turnCap, seed, tutorial, quizTurn)
        {
        }

        /// <summary>A battle against a mixed Spanish column, listed group by group.</summary>
        public QuestBattle(EnemyGroup[] enemies, int squadCap, WinRule winRule, int turnCap, int seed, bool tutorial, int quizTurn)
        {
            var list = new List<string>();
            for (int g = 0; g < enemies.Length; g++)
            {
                for (int i = 0; i < enemies[g].Count; i++)
                {
                    list.Add(enemies[g].ArchetypeId);
                }
            }

            Enemies = list;
            EnemyCount = list.Count;
            SquadCap = squadCap;
            WinRule = winRule;
            TurnCap = turnCap;
            Seed = seed;
            Tutorial = tutorial;
            QuizTurn = quizTurn;
        }
    }

    /// <summary>A step of a hub-task sub-quest, and where in the encampment it is done.</summary>
    public sealed class HubTask
    {
        public readonly string Id;
        public readonly LocString Text;

        /// <summary>The encampment place the navigation pointer marks. See <see cref="Places"/>.</summary>
        public readonly string Place;

        public HubTask(string id, LocString text, string place)
        {
            Id = id;
            Text = text;
            Place = place;
        }
    }

    /// <summary>Clickable places in the encampment. Also the ids the navigation guide points at.</summary>
    public static class Places
    {
        public const string Aide = "aide";
        public const string MissionTent = "mission";
        public const string Farm = "farm";
        public const string Mine = "mine";
        public const string Exchange = "exchange";
        public const string Training = "training";
        public const string Recruitment = "recruit";
        public const string Armory = "armory";
        public const string Library = "library";
    }

    /// <summary>One of the ten sub-quests of Appendix F.</summary>
    public sealed class Quest
    {
        public string Id;
        public int Level;
        public int Number;
        public QuestKind Kind;
        public LocString Title;

        /// <summary>The proposal's bracketed tag, e.g. [Combat Tutorial].</summary>
        public LocString Tag;

        public LocString Place;
        public LocString Briefing;

        /// <summary>Cutscene played before the quest starts, or null.</summary>
        public string PreCutscene;

        /// <summary>Cutscene played after the quest is cleared, or null.</summary>
        public string PostCutscene;

        public QuestBattle Battle;

        /// <summary>How many of one Spanish archetype the quest's battle fields.</summary>
        public int EnemiesOf(string archetypeId)
        {
            int count = 0;
            if (Battle != null)
            {
                for (int i = 0; i < Battle.Enemies.Count; i++)
                {
                    if (Battle.Enemies[i] == archetypeId)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>Hub-task step ids, all required. See <see cref="Campaign.Task"/>.</summary>
        public string[] Tasks = new string[0];

        public int RationsCost;
        public int RewardReales;

        /// <summary>Weapon added to the armoury on first clear, or null.</summary>
        public string RewardWeapon;

        /// <summary>Lesson unlocked in the Learning panel on first clear.</summary>
        public string RewardLesson;

        /// <summary>Node position on the campaign map, 0..1 from the bottom-left.</summary>
        public float MapX;
        public float MapY;
    }

    /// <summary>One of the three campaign levels.</summary>
    public sealed class CampaignLevel
    {
        public readonly int Number;
        public readonly LocString Title;

        public CampaignLevel(int number, LocString title)
        {
            Number = number;
            Title = title;
        }
    }

    /// <summary>
    /// The linear campaign of Appendix F: three levels, ten sub-quests, in order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sub-quests tagged as tutorials, base management or resource gathering are played in the
    /// encampment; the rest are battles. The Level 2 escort and stealth tags are battles with their
    /// own win rule on the same auto-battler loop (DESIGN-DECISIONS #21): q06 guards a supply cart
    /// until turn 20 (<see cref="WinRule.Escort"/>), q07 sends a squad of three onto a powder
    /// magazine behind the Spanish line (<see cref="WinRule.Sabotage"/>).
    /// </para>
    /// <para>
    /// The Spanish column grows with the campaign (DESIGN-DECISIONS #19): regulars only in the
    /// q02 tutorial, marines for the landings (q05, q09), artillery once the offensive opens (q06,
    /// q08, q10), and an officer wherever a column needs holding together.
    /// </para>
    /// </remarks>
    public static class Campaign
    {
        public const int LevelCount = 3;

        public const string TaskTalkAide = "talk.aide";
        public const string TaskRecruit = "recruit";
        public const string TaskTrain = "train";
        public const string TaskHarvestFarm = "harvest.farm";
        public const string TaskHarvestMine = "harvest.mine";
        public const string TaskExchange = "exchange";
        public const string TaskEquip = "equip";

        private static readonly List<CampaignLevel> levels = new List<CampaignLevel>
        {
            new CampaignLevel(1, new LocString("The Architect's Awakening", "Ang Paggising ng Arkitekto")),
            new CampaignLevel(2, new LocString("Preparations for War", "Paghahanda sa Digmaan")),
            new CampaignLevel(3, new LocString("Battle of Binakayan-Dalahican", "Labanan sa Binakayan-Dalahican"))
        };

        private static readonly List<HubTask> tasks = new List<HubTask>
        {
            new HubTask(TaskTalkAide, new LocString("Speak with your aide, Tomas", "Kausapin ang iyong katuwang na si Tomas"), Places.Aide),
            new HubTask(TaskRecruit, new LocString("Recruit a unit at the Recruitment Hall", "Mangalap ng yunit sa Bulwagan ng Pangangalap"), Places.Recruitment),
            new HubTask(TaskTrain, new LocString("Drill a unit at the Training Grounds", "Sanayin ang isang yunit sa Sanayan"), Places.Training),
            new HubTask(TaskEquip, new LocString("Equip a weapon at the Armory", "Magbigay ng sandata sa Taguan ng Armas"), Places.Armory),
            new HubTask(TaskHarvestFarm, new LocString("Harvest Rations at the Farm", "Umani ng Rasyon sa Bukid"), Places.Farm),
            new HubTask(TaskHarvestMine, new LocString("Collect Scrap at the Mine", "Kumuha ng Bakal sa Minahan"), Places.Mine),
            new HubTask(TaskExchange, new LocString("Sell goods for Reales at the Exchange", "Ipagbili ang ani kapalit ng Reales sa Palitan"), Places.Exchange)
        };

        private static readonly List<Quest> quests = new List<Quest>
        {
            // ---------------------------------------------------------------- Level 1
            new Quest
            {
                Id = "q01", Level = 1, Number = 1, Kind = QuestKind.HubTask,
                Title = new LocString("The Scholar of Ghent", "Ang Iskolar ng Ghent"),
                Tag = new LocString("UI Tutorial", "Gabay sa Interface"),
                Place = new LocString("Ghent, Belgium", "Ghent, Belhika"),
                Briefing = new LocString(
                    "A student engineer answers the call from home. Meet your aide and learn the encampment.",
                    "Isang estudyanteng inhinyero ang tumugon sa tawag ng bayan. Kilalanin ang iyong katuwang at ang kampo."),
                PreCutscene = Cutscenes.Act1,
                Tasks = new[] { TaskTalkAide },
                RewardReales = 50, RewardLesson = "l01",
                MapX = 0.14f, MapY = 0.84f
            },
            new Quest
            {
                Id = "q02", Level = 1, Number = 2, Kind = QuestKind.Battle,
                Title = new LocString("The Reality Check", "Ang Pagkamulat"),
                Tag = new LocString("Combat Tutorial", "Gabay sa Labanan"),
                Place = new LocString("Kawit, Cavite", "Kawit, Kabite"),
                Briefing = new LocString(
                    "A Spanish patrol probes the trench line. Deploy your troops and hold.",
                    "Isang patrolyang Kastila ang sumusubok sa hanay ng trinsera. Ipuwesto ang hukbo at manindigan."),
                PreCutscene = Cutscenes.Act2,
                Battle = new QuestBattle(3, 6, WinRule.Rout, 120, 1896, true, 0),
                RationsCost = 0, RewardReales = 100, RewardLesson = "l02",
                MapX = 0.46f, MapY = 0.55f
            },
            new Quest
            {
                Id = "q03", Level = 1, Number = 3, Kind = QuestKind.HubTask,
                Title = new LocString("The Vanguard's Rally", "Ang Pagtitipon ng Taliba"),
                Tag = new LocString("Base Management", "Pamamahala ng Kampo"),
                Place = new LocString("Kawit encampment", "Kampo sa Kawit"),
                Briefing = new LocString(
                    "Farmers, teachers and medics come to the camp. Recruit them, drill them and arm them.",
                    "Dumarating sa kampo ang mga magsasaka, guro at mediko. Kalapin, sanayin at armasan sila."),
                PreCutscene = Cutscenes.Act3,
                Tasks = new[] { TaskRecruit, TaskTrain, TaskEquip },
                RewardReales = 150, RewardWeapon = WeaponCatalog.Bolo, RewardLesson = "l03",
                MapX = 0.50f, MapY = 0.48f
            },
            new Quest
            {
                Id = "q04", Level = 1, Number = 4, Kind = QuestKind.HubTask,
                Title = new LocString("Scavenging for the Cause", "Pangangalap para sa Layunin"),
                Tag = new LocString("Resource Gathering", "Pag-iipon ng Yaman"),
                Place = new LocString("Noveleta", "Noveleta"),
                Briefing = new LocString(
                    "An army marches on its stomach. Harvest the Farm and Mine, then sell the surplus for Reales.",
                    "Ang hukbo ay lumalakad sa sikmura. Umani sa Bukid at Minahan, at ipagbili ang sobra kapalit ng Reales."),
                PreCutscene = "c04_scavenge",
                Tasks = new[] { TaskHarvestFarm, TaskHarvestMine, TaskExchange },
                RewardReales = 150, RewardLesson = "l04",
                MapX = 0.33f, MapY = 0.44f
            },

            // ---------------------------------------------------------------- Level 2
            new Quest
            {
                Id = "q05", Level = 2, Number = 5, Kind = QuestKind.Battle,
                Title = new LocString("The Intercepted Armada", "Ang Naharang na Armada"),
                Tag = new LocString("Synergy Drill", "Sanayang Buklod"),
                Place = new LocString("Bacoor Bay shore", "Baybayin ng Bacoor"),
                Briefing = new LocString(
                    "A landing party comes ashore. Place bonded pairs side by side - Kapatiran makes them stronger together.",
                    "May pangkat na dumaong sa pampang. Ipuwesto nang magkatabi ang magkabuklod - mas malakas sila nang magkasama."),
                PreCutscene = "c05_armada",
                // A landing party: marines wade in along the shallows beside the regulars.
                Battle = new QuestBattle(new[] { new EnemyGroup(3, UnitCatalog.SpanishRegular), new EnemyGroup(2, UnitCatalog.SpanishMarine) }, 6, WinRule.Rout, 120, 1901, false, 4),
                RationsCost = 5, RewardReales = 150, RewardWeapon = WeaponCatalog.Paltik, RewardLesson = "l05",
                MapX = 0.72f, MapY = 0.56f
            },
            new Quest
            {
                Id = "q06", Level = 2, Number = 6, Kind = QuestKind.Battle,
                Title = new LocString("Forging the Earthworks", "Ang Pagbuo ng mga Muog"),
                Tag = new LocString("Defense", "Pagtatanggol"),
                Place = new LocString("Binakayan", "Binakayan"),
                Briefing = new LocString(
                    "The trenches are half dug. Keep the supply cart behind the line alive until turn 20 so the diggers can finish.",
                    "Kalahati pa lang ang trinsera. Iligtas ang kariton ng panustos sa likod ng hanay hanggang yugto 20 upang matapos ng mga manghuhukay."),
                PreCutscene = "c06_earthworks",
                // Escort: the cart must still stand at the end of turn 20. One gun shells the line.
                Battle = new QuestBattle(new[] { new EnemyGroup(5, UnitCatalog.SpanishRegular), new EnemyGroup(3, UnitCatalog.SpanishCazador), new EnemyGroup(1, UnitCatalog.SpanishOfficer), new EnemyGroup(1, UnitCatalog.SpanishArtillery) }, 6, WinRule.Escort, 20, 1902, false, 5),
                RationsCost = 6, RewardReales = 200, RewardLesson = "l06",
                MapX = 0.58f, MapY = 0.40f
            },
            new Quest
            {
                Id = "q07", Level = 2, Number = 7, Kind = QuestKind.Battle,
                Title = new LocString("The Silent Sabotage", "Ang Tahimik na Pananabotahe"),
                Tag = new LocString("Stealth / Intelligence", "Paniktik"),
                Place = new LocString("Cavite Nuevo", "Cavite Nuevo"),
                Briefing = new LocString(
                    "Only three may slip behind enemy lines. Reach the marked powder magazine within 30 turns - they fight only what blocks the way.",
                    "Tatlo lamang ang maaaring pumuslit sa likod ng kaaway. Marating ang markadong imbakan ng pulbura sa loob ng 30 yugto - lalabanan lamang nila ang humaharang."),
                PreCutscene = "c07_sabotage",
                // Sabotage: a squad of three makes for the magazine; the guard post stands in the way.
                Battle = new QuestBattle(new[] { new EnemyGroup(2, UnitCatalog.SpanishRegular), new EnemyGroup(1, UnitCatalog.SpanishCazador), new EnemyGroup(1, UnitCatalog.SpanishOfficer) }, 3, WinRule.Sabotage, 30, 1903, false, 3),
                RationsCost = 4, RewardReales = 200, RewardWeapon = WeaponCatalog.Remington, RewardLesson = "l07",
                MapX = 0.80f, MapY = 0.30f
            },

            // ---------------------------------------------------------------- Level 3
            new Quest
            {
                Id = "q08", Level = 3, Number = 8, Kind = QuestKind.Battle,
                Title = new LocString("The First Wave", "Ang Unang Daluyong"),
                Tag = new LocString("November 9, 1896", "Nobyembre 9, 1896"),
                Place = new LocString("Binakayan", "Binakayan"),
                Briefing = new LocString(
                    "Blanco's offensive begins. Spanish columns advance on Binakayan under covering fire.",
                    "Nagsimula ang opensiba ni Blanco. Sumusulong ang mga hanay ng Kastila sa Binakayan."),
                PreCutscene = Cutscenes.Act4,
                // "Under covering fire": two guns behind a column led by an officer.
                Battle = new QuestBattle(new[] { new EnemyGroup(4, UnitCatalog.SpanishRegular), new EnemyGroup(1, UnitCatalog.SpanishCazador), new EnemyGroup(1, UnitCatalog.SpanishOfficer), new EnemyGroup(2, UnitCatalog.SpanishArtillery) }, 6, WinRule.Rout, 120, 1909, false, 4),
                RationsCost = 8, RewardReales = 250, RewardLesson = "l08",
                MapX = 0.56f, MapY = 0.28f
            },
            new Quest
            {
                Id = "q09", Level = 3, Number = 9, Kind = QuestKind.Battle,
                Title = new LocString("The War of Attrition", "Ang Digmaan ng Pagtitiis"),
                Tag = new LocString("November 10, 1896", "Nobyembre 10, 1896"),
                Place = new LocString("Dalahican", "Dalahican"),
                Briefing = new LocString(
                    "The fighting shifts to the Dalahican shore. The enemy wades through the shallows.",
                    "Lumipat ang labanan sa baybayin ng Dalahican. Lumulusong ang kaaway sa mababaw na tubig."),
                PreCutscene = "c09_attrition",
                // The shore battle: marines in the shallows, Cazadores screening them.
                Battle = new QuestBattle(new[] { new EnemyGroup(3, UnitCatalog.SpanishRegular), new EnemyGroup(4, UnitCatalog.SpanishMarine), new EnemyGroup(2, UnitCatalog.SpanishCazador), new EnemyGroup(1, UnitCatalog.SpanishOfficer) }, 6, WinRule.Rout, 120, 1910, false, 5),
                RationsCost = 10, RewardReales = 300, RewardWeapon = WeaponCatalog.Mauser, RewardLesson = "l09",
                MapX = 0.34f, MapY = 0.22f
            },
            new Quest
            {
                Id = "q10", Level = 3, Number = 10, Kind = QuestKind.Battle,
                Title = new LocString("The Decisive Dawn", "Ang Mapagpasyang Bukang-liwayway"),
                Tag = new LocString("November 11, 1896", "Nobyembre 11, 1896"),
                Place = new LocString("Binakayan-Dalahican", "Binakayan-Dalahican"),
                Briefing = new LocString(
                    "The last and largest assault. Everything Evangelista built is put to the test.",
                    "Ang huli at pinakamalaking salakay. Susubukin ang lahat ng itinayo ni Evangelista."),
                PreCutscene = "c10_dawn",
                PostCutscene = Cutscenes.Aftermath,
                // Everything at once.
                Battle = new QuestBattle(new[] { new EnemyGroup(6, UnitCatalog.SpanishRegular), new EnemyGroup(2, UnitCatalog.SpanishMarine), new EnemyGroup(3, UnitCatalog.SpanishCazador), new EnemyGroup(2, UnitCatalog.SpanishOfficer), new EnemyGroup(1, UnitCatalog.SpanishArtillery) }, 6, WinRule.Rout, 120, 1911, false, 6),
                RationsCost = 12, RewardReales = 500, RewardLesson = "l10",
                MapX = 0.46f, MapY = 0.14f
            }
        };

        public static IReadOnlyList<CampaignLevel> Levels
        {
            get { return levels; }
        }

        public static IReadOnlyList<Quest> Quests
        {
            get { return quests; }
        }

        public static IReadOnlyList<HubTask> Tasks
        {
            get { return tasks; }
        }

        public static Quest Find(string id)
        {
            for (int i = 0; i < quests.Count; i++)
            {
                if (quests[i].Id == id)
                {
                    return quests[i];
                }
            }

            return null;
        }

        public static HubTask Task(string id)
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                if (tasks[i].Id == id)
                {
                    return tasks[i];
                }
            }

            return null;
        }

        public static CampaignLevel Level(int number)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].Number == number)
                {
                    return levels[i];
                }
            }

            return null;
        }

        /// <summary>The sub-quests of <paramref name="level"/>, in order.</summary>
        public static List<Quest> QuestsIn(int level)
        {
            var list = new List<Quest>();
            for (int i = 0; i < quests.Count; i++)
            {
                if (quests[i].Level == level)
                {
                    list.Add(quests[i]);
                }
            }

            return list;
        }
    }
}
