using System;
using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Meta;
using NUnit.Framework;

namespace BinakayanRising.Tests.Meta
{
    /// <summary>
    /// The encampment economy, progression, recruiting and campaign rules, against a clock the
    /// test controls.
    /// </summary>
    [TestFixture]
    public class MetaGameTests
    {
        private static readonly DateTime Start = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

        private DateTime now;
        private MetaRules rules;
        private MetaGame game;

        [SetUp]
        public void SetUp()
        {
            now = Start;
            rules = MetaRules.Default();
            game = new MetaGame(MetaGame.NewGame(rules, now, 42), rules, () => now);
        }

        private void Advance(double seconds)
        {
            now = now.AddSeconds(seconds);
        }

        private void ClearThrough(string questId)
        {
            foreach (Quest quest in Campaign.Quests)
            {
                if (!game.IsCleared(quest))
                {
                    game.Data.clearedQuests.Add(quest.Id);
                }

                if (quest.Id == questId)
                {
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ new game

        [Test]
        public void NewGameStartsWithTheOpeningRosterPurseAndKit()
        {
            Assert.AreEqual(5, game.Units.Count);
            Assert.AreEqual(6, game.Weapons.Count);
            Assert.AreEqual(rules.StartingReales, game.Balance(Currency.Reales));
            Assert.AreEqual(rules.StartingRations, game.Balance(Currency.Rations));
            Assert.AreEqual(rules.StartingScrap, game.Balance(Currency.Scrap));
            Assert.AreEqual("q01", game.CurrentQuest.Id);
            Assert.AreEqual("Kawal", game.Rank.Title);
        }

        [Test]
        public void EachStarterHoldsItsOwnWeaponAndABalarawWaitsOnTheRack()
        {
            var expected = new Dictionary<string, string>
            {
                { UnitCatalog.Evangelista, WeaponCatalog.Bolo },
                { UnitCatalog.Aguinaldo, WeaponCatalog.Sibat },
                { UnitCatalog.Marksman, WeaponCatalog.Paltik },
                { UnitCatalog.Engineer, WeaponCatalog.Gulok },
                { UnitCatalog.Vanguard, WeaponCatalog.Talibong }
            };

            foreach (OwnedUnit unit in game.Units)
            {
                Assert.AreEqual(expected[unit.archetype], game.WeaponOf(unit).Id, unit.archetype);
            }

            var rack = new List<OwnedWeapon>();
            foreach (OwnedWeapon weapon in game.Weapons)
            {
                if (game.HolderOf(weapon.id) == null)
                {
                    rack.Add(weapon);
                }
            }

            Assert.AreEqual(1, rack.Count);
            Assert.AreEqual(WeaponCatalog.Balaraw, rack[0].weapon);
        }

        // ------------------------------------------------------------------ farm and mine

        [Test]
        public void FarmAccruesOneRationPerIntervalAndHarvestMovesItIntoThePurse()
        {
            Advance(rules.Farm.SecondsPerUnit * 5);
            Assert.AreEqual(5, game.Stored(Facility.Farm));

            int before = game.Balance(Currency.Rations);
            Assert.AreEqual(5, game.Harvest(Facility.Farm));
            Assert.AreEqual(before + 5, game.Balance(Currency.Rations));
            Assert.AreEqual(0, game.Stored(Facility.Farm));
        }

        [Test]
        public void HarvestKeepsPartialProgressTowardTheNextUnit()
        {
            Advance(rules.Farm.SecondsPerUnit * 2.5);
            Assert.AreEqual(2, game.Harvest(Facility.Farm));

            Advance(rules.Farm.SecondsPerUnit * 0.5);
            Assert.AreEqual(1, game.Stored(Facility.Farm), "the half unit banked before the harvest must survive it");
        }

        [Test]
        public void StorageStopsAtTheCapAndTimePastItIsNotOwed()
        {
            Advance(rules.Mine.SecondsPerUnit * (rules.Mine.StorageCap + 50));
            Assert.AreEqual(rules.Mine.StorageCap, game.Stored(Facility.Mine));
            Assert.IsTrue(game.IsFull(Facility.Mine));
            Assert.AreEqual(1f, game.Progress(Facility.Mine));

            game.Harvest(Facility.Mine);
            Assert.AreEqual(0, game.Stored(Facility.Mine), "overflow past the cap is not banked for later");
        }

        [Test]
        public void AClockThatRunsBackwardsProducesNothingAndRestartsTheCount()
        {
            Advance(-3600);
            Assert.AreEqual(0, game.Stored(Facility.Farm));
            Assert.AreEqual(0, game.Harvest(Facility.Farm));

            Advance(rules.Farm.SecondsPerUnit);
            Assert.AreEqual(1, game.Stored(Facility.Farm), "counting restarts from the corrected time");
        }

        // ------------------------------------------------------------------ exchange

        [Test]
        public void ExchangeSellsWholeLotsForReales()
        {
            ExchangeRate rate = rules.RateFor(Currency.Scrap);
            game.Data.scrap = rate.LotSize * 2 + 3;
            int reales = game.Balance(Currency.Reales);

            Assert.AreEqual(2, game.SellableLots(Currency.Scrap));
            Assert.AreEqual(rate.RealesPerLot * 2, game.Exchange(Currency.Scrap, 2));
            Assert.AreEqual(3, game.Balance(Currency.Scrap));
            Assert.AreEqual(reales + rate.RealesPerLot * 2, game.Balance(Currency.Reales));
        }

        [Test]
        public void ExchangeRefusesMoreLotsThanThePurseHolds()
        {
            game.Data.rations = 5;
            Assert.AreEqual(0, game.Exchange(Currency.Rations, 1));
            Assert.AreEqual(5, game.Balance(Currency.Rations));
            Assert.AreEqual(0, game.Exchange(Currency.Reales, 1), "Reales cannot be sold for Reales");
        }

        // ------------------------------------------------------------------ training

        [Test]
        public void DrillCostsTheListedPriceAndLevelsUpWithOverflowCarried()
        {
            OwnedUnit unit = game.Units[0];
            int reales = game.Balance(Currency.Reales);
            int scrap = game.Balance(Currency.Scrap);
            unit.xp = 30;

            LevelUp up;
            Assert.IsTrue(game.TryDrill(unit.id, out up));
            Assert.IsNotNull(up);
            Assert.AreEqual(1, up.FromLevel);
            Assert.AreEqual(2, up.ToLevel);
            Assert.AreEqual(30, unit.xp, "130 XP at a 100 XP threshold leaves 30");
            Assert.Greater(up.After.MaxHP, up.Before.MaxHP);
            Assert.AreEqual(reales - rules.DrillCost.Reales, game.Balance(Currency.Reales));
            Assert.AreEqual(scrap - rules.DrillCost.Scrap, game.Balance(Currency.Scrap));
        }

        [Test]
        public void DrillIsRefusedAtTheLevelCapOrWhenBroke()
        {
            OwnedUnit unit = game.Units[0];
            LevelUp up;

            unit.level = rules.LevelCap;
            Assert.IsFalse(game.TryDrill(unit.id, out up));

            unit.level = 1;
            game.Data.reales = 0;
            Assert.IsFalse(game.TryDrill(unit.id, out up));
            Assert.AreEqual(1, unit.level);
        }

        [Test]
        public void XpPastTheLevelCapIsDiscarded()
        {
            OwnedUnit unit = game.Units[0];
            unit.level = rules.LevelCap - 1;
            game.AwardXp(new List<int> { unit.id }, 100000);

            Assert.AreEqual(rules.LevelCap, unit.level);
            Assert.AreEqual(0, unit.xp);
        }

        // ------------------------------------------------------------------ armoury

        private OwnedUnit UnitOf(string archetype)
        {
            foreach (OwnedUnit unit in game.Units)
            {
                if (unit.archetype == archetype)
                {
                    return unit;
                }
            }

            return null;
        }

        private OwnedWeapon Owned(string weapon)
        {
            foreach (OwnedWeapon owned in game.Weapons)
            {
                if (owned.weapon == weapon)
                {
                    return owned;
                }
            }

            return null;
        }

        [Test]
        public void EquippingMovesAWeaponOutOfItsPreviousHandsAndRaisesAttack()
        {
            OwnedUnit vanguard = UnitOf(UnitCatalog.Vanguard);
            OwnedUnit evangelista = UnitOf(UnitCatalog.Evangelista);
            int talibong = vanguard.weaponId;
            float bareAttack = game.StatsAt(evangelista.archetype, evangelista.level).AttackDamage;

            Assert.IsTrue(game.TryEquip(evangelista.id, talibong));
            Assert.AreEqual(talibong, evangelista.weaponId);
            Assert.AreEqual(0, vanguard.weaponId, "one talibong, one pair of hands");
            Assert.AreEqual(bareAttack + WeaponCatalog.Find(WeaponCatalog.Talibong).AttackBonus, game.StatsOf(evangelista).AttackDamage, 0.001f);
            Assert.IsNull(game.HolderOf(Owned(WeaponCatalog.Bolo).id), "Evangelista's bolo went back to the rack");
        }

        [Test]
        public void EquippingTheRackBalarawOrSwappingCountsForTheEquipTask()
        {
            ClearThrough("q02");
            Quest q03 = Campaign.Find("q03");
            Assert.AreEqual(q03, game.CurrentQuest);

            Assert.IsTrue(game.TryEquip(UnitOf(UnitCatalog.Aguinaldo).id, Owned(WeaponCatalog.Balaraw).id));
            Assert.IsTrue(game.IsTaskDone(q03, Campaign.TaskEquip));

            var swap = new MetaGame(MetaGame.NewGame(rules, now, 7), rules, () => now);
            foreach (Quest quest in Campaign.Quests)
            {
                swap.Data.clearedQuests.Add(quest.Id);
                if (quest.Id == "q02")
                {
                    break;
                }
            }

            OwnedUnit engineer = swap.Units[3];
            Assert.IsTrue(swap.TryEquip(engineer.id, swap.Units[0].weaponId), "a swap between two soldiers");
            Assert.IsTrue(swap.IsTaskDone(q03, Campaign.TaskEquip));
        }

        [TestCase(WeaponCatalog.Bolo, 2f, 0f, 0f, 0f)]
        [TestCase(WeaponCatalog.Talibong, 2f, 0f, 0.03f, 0f)]
        [TestCase(WeaponCatalog.Gulok, 3f, -0.03f, 0f, 0f)]
        [TestCase(WeaponCatalog.Sibat, 1f, 0.05f, 0f, 0f)]
        [TestCase(WeaponCatalog.Balaraw, 1f, 0f, 0.06f, 0f)]
        [TestCase(WeaponCatalog.Lantaka, 7f, -0.05f, 0f, 1f)]
        public void EachWeaponAppliesItsTwistToTheHoldersStats(string weapon, float attack, float accuracy, float crit, float range)
        {
            OwnedUnit engineer = UnitOf(UnitCatalog.Engineer);
            OwnedWeapon owned = Owned(weapon) ?? game.FindWeapon(AddWeapon(weapon));
            Assert.IsTrue(game.TryEquip(engineer.id, owned.id));

            UnitStats bare = game.StatsAt(engineer.archetype, engineer.level);
            UnitStats armed = game.StatsOf(engineer);
            Assert.AreEqual(bare.AttackDamage + attack, armed.AttackDamage, 0.001f);
            Assert.AreEqual(bare.RangedAccuracy + accuracy, armed.RangedAccuracy, 0.001f);
            Assert.AreEqual(bare.CriticalHitChance + crit, armed.CriticalHitChance, 0.001f);
            Assert.AreEqual(bare.AttackRange + range, armed.AttackRange, 0.001f);
            Assert.AreEqual(bare.MaxHP, armed.MaxHP, 0.001f, "weapons never touch health");
        }

        private int AddWeapon(string weapon)
        {
            var owned = new OwnedWeapon { id = game.Data.nextWeaponId++, weapon = weapon };
            game.Data.weapons.Add(owned);
            return owned.id;
        }

        [Test]
        public void OnlyTheTrenchEngineerCanHoldTheLantaka()
        {
            int lantaka = AddWeapon(WeaponCatalog.Lantaka);
            OwnedUnit engineer = UnitOf(UnitCatalog.Engineer);

            foreach (OwnedUnit unit in game.Units)
            {
                if (unit == engineer)
                {
                    continue;
                }

                int before = unit.weaponId;
                Assert.IsFalse(game.CanWield(unit, game.FindWeapon(lantaka)), unit.archetype);
                Assert.IsFalse(game.TryEquip(unit.id, lantaka), unit.archetype);
                Assert.AreEqual(before, unit.weaponId, "a refused equip leaves the old weapon in hand");
            }

            Assert.IsTrue(game.TryEquip(engineer.id, lantaka));
            Assert.AreEqual(lantaka, engineer.weaponId);
            Assert.IsNull(WeaponCatalog.Find(WeaponCatalog.Lantaka).UpgradesTo, "the Lantaka cannot be reforged");
            Assert.IsFalse(game.CanSynthesize(game.FindWeapon(lantaka)));

            // An edited save that hands it to someone else is put right on load.
            UnitOf(UnitCatalog.Vanguard).weaponId = lantaka;
            engineer.weaponId = 0;
            Assert.IsTrue(game.Data.Repair(rules));
            Assert.AreEqual(0, UnitOf(UnitCatalog.Vanguard).weaponId);
        }

        [Test]
        public void ForgingTheEarthworksRewardsTheLantaka()
        {
            Quest q06 = Campaign.Find("q06");
            Assert.AreEqual(WeaponCatalog.Lantaka, q06.RewardWeapon);

            ClearThrough("q05");
            int weapons = game.Weapons.Count;
            QuestReward reward = game.CompleteBattle(q06, true, new List<int> { game.Units[0].id });

            Assert.AreEqual(WeaponCatalog.Lantaka, reward.Weapon);
            Assert.AreEqual(weapons + 1, game.Weapons.Count);
            Assert.AreEqual(WeaponCatalog.Lantaka, game.Weapons[game.Weapons.Count - 1].weapon);
        }

        [TestCase(WeaponCatalog.Bolo)]
        [TestCase(WeaponCatalog.Talibong)]
        [TestCase(WeaponCatalog.Gulok)]
        [TestCase(WeaponCatalog.Sibat)]
        [TestCase(WeaponCatalog.Balaraw)]
        public void EveryTierOneWeaponReforgesIntoAPaltikAtTheBoloPrice(string weapon)
        {
            WeaponDef def = WeaponCatalog.Find(weapon);
            WeaponDef bolo = WeaponCatalog.Find(WeaponCatalog.Bolo);
            Assert.AreEqual(1, def.Tier);
            Assert.AreEqual(WeaponCatalog.Paltik, def.UpgradesTo);
            Assert.AreEqual(bolo.SynthesisCost.Reales, def.SynthesisCost.Reales);
            Assert.AreEqual(bolo.SynthesisCost.Rations, def.SynthesisCost.Rations);
            Assert.AreEqual(bolo.SynthesisCost.Scrap, def.SynthesisCost.Scrap);
        }

        [Test]
        public void SynthesisUpgradesTheWeaponInPlaceAndCharges()
        {
            OwnedUnit vanguard = UnitOf(UnitCatalog.Vanguard);
            OwnedWeapon talibong = game.FindWeapon(vanguard.weaponId);
            WeaponDef def = WeaponCatalog.Find(WeaponCatalog.Talibong);
            game.Data.reales = def.SynthesisCost.Reales;
            game.Data.scrap = def.SynthesisCost.Scrap;

            Assert.IsTrue(game.TrySynthesize(talibong.id));
            Assert.AreEqual(WeaponCatalog.Paltik, talibong.weapon);
            Assert.AreEqual(talibong.id, vanguard.weaponId, "the holder keeps the upgraded weapon");
            Assert.AreEqual(0, game.Balance(Currency.Scrap));
            Assert.AreEqual(0.0f, game.StatsOf(vanguard).CriticalHitChance - game.StatsAt(vanguard.archetype, 1).CriticalHitChance, 0.001f,
                "the talibong's edge goes with it");
            Assert.IsFalse(game.TrySynthesize(talibong.id), "no scrap left for the next tier");
        }

        // ------------------------------------------------------------------ recruiting

        [Test]
        public void PullsAreDeterminedBySeedAndPullNumberSoReloadingCannotReroll()
        {
            SaveData copy = MetaGame.NewGame(rules, Start, 42);
            var other = new MetaGame(copy, rules, () => now);

            List<PullResult> a = game.TryPull(3);
            List<PullResult> b = other.TryPull(3);

            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(a[i].Archetype, b[i].Archetype);
            }
        }

        [Test]
        public void PityGuaranteesAHeroOnTheThresholdPull()
        {
            rules.RarityWeights = new[] { 1, 0, 0 };
            game.Data.reales = 100000;

            List<PullResult> results = game.TryPull(rules.PityThreshold);
            for (int i = 0; i < results.Count - 1; i++)
            {
                Assert.AreEqual(UnitRarity.Common, results[i].Rarity);
            }

            PullResult last = results[results.Count - 1];
            Assert.AreEqual(UnitRarity.Hero, last.Rarity);
            Assert.IsTrue(last.FromPity);
            Assert.AreEqual(0, game.Data.pity);
        }

        [Test]
        public void ADuplicateHeroTrainsTheOneYouHaveInsteadOfAddingASecond()
        {
            rules.RarityWeights = new[] { 0, 0, 1 };
            int units = game.Units.Count;

            PullResult result = game.TryPull(1)[0];
            Assert.IsFalse(result.NewUnit);
            Assert.AreEqual(rules.DuplicateHeroXp, result.DuplicateXp);
            Assert.AreEqual(units, game.Units.Count);
        }

        [Test]
        public void PullIsRefusedWhenBrokeAndChargesNothing()
        {
            game.Data.reales = rules.SinglePullCost - 1;
            Assert.IsNull(game.TryPull(1));
            Assert.AreEqual(rules.SinglePullCost - 1, game.Balance(Currency.Reales));
        }

        // ------------------------------------------------------------------ campaign

        [Test]
        public void TheCampaignIsLinear()
        {
            Quest q01 = Campaign.Find("q01");
            Quest q02 = Campaign.Find("q02");
            Assert.IsTrue(game.IsUnlocked(q01));
            Assert.IsFalse(game.IsUnlocked(q02));

            game.MarkTask(Campaign.TaskTalkAide);
            Assert.IsTrue(game.IsCleared(q01), "a hub task clears itself when its last step is done");
            Assert.IsTrue(game.IsUnlocked(q02));
        }

        [Test]
        public void AHubTaskStepOnlyCountsWhileItsSubQuestIsCurrent()
        {
            game.Data.rations = 0;
            game.MarkTask(Campaign.TaskExchange);
            ClearThrough("q03");

            Assert.AreEqual("q04", game.CurrentQuest.Id);
            Assert.IsFalse(game.IsTaskDone(game.CurrentQuest, Campaign.TaskExchange));
            Assert.AreEqual(Places.Farm, game.CurrentObjective.Place);
        }

        [Test]
        public void HubTaskCompletionPaysAndReportsTheReward()
        {
            QuestReward seen = null;
            game.QuestCompleted += reward => seen = reward;
            int reales = game.Balance(Currency.Reales);

            game.MarkTask(Campaign.TaskTalkAide);

            Assert.IsNotNull(seen);
            Assert.AreEqual("q01", seen.Quest.Id);
            Assert.AreEqual(reales + seen.Quest.RewardReales, game.Balance(Currency.Reales));
            Assert.IsTrue(game.IsLessonUnlocked(seen.Quest.RewardLesson));
        }

        [Test]
        public void LaunchingABattleCostsRations()
        {
            ClearThrough("q04");
            Quest q05 = Campaign.Find("q05");
            game.Data.rations = q05.RationsCost - 1;
            Assert.IsFalse(game.TryLaunch(q05));
            Assert.AreEqual(Places.Farm, game.CurrentObjective.Place, "the guide sends a hungry army to the farm");

            game.Data.rations = q05.RationsCost;
            Assert.IsTrue(game.TryLaunch(q05));
            Assert.AreEqual(0, game.Balance(Currency.Rations));
        }

        [Test]
        public void FirstClearPaysInFullAndAReplayPaysHalf()
        {
            ClearThrough("q01");
            Quest q02 = Campaign.Find("q02");
            var deployed = new List<int> { game.Units[0].id };

            QuestReward first = game.CompleteBattle(q02, true, deployed);
            Assert.IsTrue(first.FirstClear);
            Assert.AreEqual(q02.RewardReales, first.Reales);

            QuestReward replay = game.CompleteBattle(q02, true, deployed);
            Assert.IsFalse(replay.FirstClear);
            Assert.AreEqual(q02.RewardReales / 2, replay.Reales);
        }

        [Test]
        public void ADefeatGivesXpButClearsNothing()
        {
            ClearThrough("q01");
            Quest q02 = Campaign.Find("q02");
            OwnedUnit unit = game.Units[0];

            QuestReward result = game.CompleteBattle(q02, false, new List<int> { unit.id });
            Assert.AreEqual(0, result.Reales);
            Assert.IsFalse(game.IsCleared(q02));
            Assert.AreEqual(rules.DefeatXp, unit.xp);
        }

        [Test]
        public void ClearingAMilestoneEarnsTheNextRankAndOwesAPromotionCard()
        {
            ClearThrough("q03");
            Quest q04 = Campaign.Find("q04");
            Assert.AreEqual("Kabo", game.Rank.Title);
            game.AcknowledgeRank();

            game.MarkTask(Campaign.TaskHarvestFarm);
            game.MarkTask(Campaign.TaskHarvestMine);
            game.MarkTask(Campaign.TaskExchange);

            Assert.IsTrue(game.IsCleared(q04));
            Assert.AreEqual("Sarhento", game.Rank.Title);
            Assert.IsTrue(game.PromotionOwed);

            game.AcknowledgeRank();
            Assert.IsFalse(game.PromotionOwed);
        }

        [Test]
        public void EveryRankHasItsOwnMilestoneQuest()
        {
            Assert.AreEqual(8, PlayerRanks.Count);
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 1; i < PlayerRanks.Count; i++)
            {
                string quest = PlayerRanks.MilestoneOf(i);
                Assert.IsNotNull(Campaign.Find(quest), "rank " + i);
                Assert.IsTrue(seen.Add(quest), "rank " + i + " shares its milestone");
            }
        }

        [Test]
        public void EachMilestoneRaisesTheRankByOne()
        {
            var cleared = new System.Collections.Generic.List<string>();
            for (int i = 1; i < PlayerRanks.Count; i++)
            {
                Assert.AreEqual(i - 1, PlayerRanks.ForCleared(cleared).Index);
                cleared.Add(PlayerRanks.MilestoneOf(i));
                Assert.AreEqual(i, PlayerRanks.ForCleared(cleared).Index);
            }
        }

        [Test]
        public void AnOldSavesShownRankMovesOntoTheEightRankLadder()
        {
            var data = new SaveData { version = 1, rankShown = 1 };
            data.Repair(MetaRules.Default());
            Assert.AreEqual(2, data.rankShown);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
        }

        [Test]
        public void TheFinishedLevelsAssessmentIsSuggestedUntilTheNextLevelBegins()
        {
            ClearThrough("q04");
            game.Data.rations = 100;
            Assert.AreEqual(Places.Library, game.CurrentObjective.Place);

            game.TryLaunch(Campaign.Find("q05"));
            Assert.AreEqual(Places.MissionTent, game.CurrentObjective.Place);
        }

        [Test]
        public void TheRankLadderEndsAtHeneral()
        {
            ClearThrough("q10");
            Assert.AreEqual("Heneral", game.Rank.Title);
            Assert.IsTrue(game.CampaignComplete);
        }

        // ------------------------------------------------------------------ learning

        [Test]
        public void AssessmentPaysItsBonusOnlyOnTheFirstPass()
        {
            AssessmentOutcome fail = game.RecordAssessment(1, 2, 5);
            Assert.IsFalse(fail.Passed);
            Assert.AreEqual(0, fail.Reales);

            AssessmentOutcome pass = game.RecordAssessment(1, 4, 5);
            Assert.IsTrue(pass.FirstPass);
            Assert.AreEqual(rules.AssessmentFirstPassReales, pass.Reales);

            AssessmentOutcome again = game.RecordAssessment(1, 5, 5);
            Assert.IsFalse(again.FirstPass);
            Assert.AreEqual(0, again.Reales);
            Assert.AreEqual(5, game.Assessment(1).best);
            Assert.AreEqual(3, game.Assessment(1).attempts);
        }

        [Test]
        public void ACorrectQuizAnswerPaysTable4sFiftyReales()
        {
            int reales = game.Balance(Currency.Reales);
            Assert.AreEqual(50, game.RecordQuizAnswer("quiz.1", true));
            Assert.AreEqual(0, game.RecordQuizAnswer("quiz.2", false));
            Assert.AreEqual(reales + 50, game.Balance(Currency.Reales));
            Assert.AreEqual(2, game.Data.quizAnswered);
            Assert.AreEqual(1, game.Data.quizCorrect);
        }

        // ------------------------------------------------------------------ repair

        [Test]
        public void RepairFixesAnImpossibleSave()
        {
            SaveData data = MetaGame.NewGame(rules, Start, 1);
            data.reales = -50;
            data.units.Add(new OwnedUnit { id = data.units[0].id, archetype = UnitCatalog.Vanguard });
            data.units[1].weaponId = 999;
            data.units[2].level = 99;
            data.lessons = null;
            data.nextWeaponId = 1;

            Assert.IsTrue(data.Repair(rules));
            Assert.AreEqual(0, data.reales);
            Assert.AreEqual(5, data.units.Count, "the duplicate id is dropped");
            Assert.AreEqual(0, data.units[1].weaponId, "a weapon that does not exist is not held");
            Assert.AreEqual(rules.LevelCap, data.units[2].level);
            Assert.IsNotNull(data.lessons);
            Assert.Greater(data.nextWeaponId, 3, "new weapons must never reuse an existing id");
            Assert.IsFalse(data.Repair(rules), "a repaired save needs no further repair");
        }

        [Test]
        public void ASaveFromBeforeTheNamedWeaponsStillLoadsWithItsBolos()
        {
            // The old opening kit: three bolos, one in the Vanguard's hands.
            SaveData data = MetaGame.NewGame(rules, Start, 3);
            data.weapons.Clear();
            data.nextWeaponId = 1;
            foreach (OwnedUnit unit in data.units)
            {
                unit.weaponId = 0;
            }

            for (int i = 0; i < 3; i++)
            {
                data.weapons.Add(new OwnedWeapon { id = data.nextWeaponId++, weapon = "bolo" });
            }

            data.units[4].weaponId = data.weapons[0].id;

            Assert.IsFalse(data.Repair(rules), "nothing in an old save needs migrating");
            var old = new MetaGame(data, rules, () => now);
            OwnedUnit vanguard = old.Units[4];
            Assert.AreEqual(WeaponCatalog.Bolo, old.WeaponOf(vanguard).Id);
            Assert.AreEqual(old.StatsAt(vanguard.archetype, 1).AttackDamage + 2f, old.StatsOf(vanguard).AttackDamage, 0.001f);
            Assert.IsTrue(old.TryEquip(old.Units[0].id, data.weapons[1].id));

            old.Data.reales = 1000;
            old.Data.scrap = 1000;
            Assert.IsTrue(old.TrySynthesize(data.weapons[2].id));
            Assert.AreEqual(WeaponCatalog.Paltik, data.weapons[2].weapon);
        }
    }
}
