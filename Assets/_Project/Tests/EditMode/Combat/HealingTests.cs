using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Content;
using NUnit.Framework;

namespace BinakayanRising.Tests.Combat
{
    /// <summary>
    /// Covers the Field Medic's heal: who it picks, how far it reaches, when it fights instead,
    /// and that a battle with a healer is as reproducible as one without.
    /// </summary>
    [TestFixture]
    public class HealingTests
    {
        private const float Power = 20f;

        private static CombatUnit Medic(int x, int y)
        {
            CombatUnit medic = CombatTestFactory.Unit(1, Team.Katipunan,
                CombatTestFactory.Stats(attackDamage: 5f, attackRange: 2f, movementSpeed: 0f), x, y, "FieldMedic");
            medic.HealPower = Power;
            return medic;
        }

        private static CombatUnit Soldier(int id, int x, int y, float damageTaken, string archetype = null)
        {
            CombatUnit soldier = CombatTestFactory.Unit(id, Team.Katipunan,
                CombatTestFactory.Stats(maxHP: 100f, movementSpeed: 0f), x, y, archetype);
            soldier.ApplyDamage(damageTaken);
            return soldier;
        }

        /// <summary>An enemy far enough away that nothing reaches it, or it anything, on turn one.</summary>
        private static CombatUnit DistantEnemy()
        {
            return CombatTestFactory.Unit(9, Team.Spanish, CombatTestFactory.Stats(movementSpeed: 0f), 9, 9);
        }

        private static BattleTurnResult RunOneTurn(List<CombatUnit> units, CombatConfig config = null, KapatiranResolver bonds = null)
        {
            var simulator = new BattleSimulator(CombatTestFactory.FlatGrid(10, 10), units,
                config ?? CombatTestFactory.Config(0, 10), kapatiran: bonds);
            return simulator.ExecuteTurn();
        }

        private static List<BattleEvent> Heals(BattleTurnResult result)
        {
            var heals = new List<BattleEvent>();
            for (int i = 0; i < result.Events.Count; i++)
            {
                if (result.Events[i].Type == BattleEventType.UnitHealed)
                {
                    heals.Add(result.Events[i]);
                }
            }

            return heals;
        }

        [Test]
        public void TheMedicHealsTheMostWoundedAllyInReach()
        {
            CombatUnit lightly = Soldier(2, 1, 0, 60f);
            CombatUnit badly = Soldier(3, 0, 1, 70f);
            List<BattleEvent> heals = Heals(RunOneTurn(new List<CombatUnit> { Medic(0, 0), lightly, badly, DistantEnemy() }));

            Assert.AreEqual(1, heals.Count, "One heal per turn, in place of the attack.");
            Assert.AreEqual(1, heals[0].ActorId);
            Assert.AreEqual(3, heals[0].TargetId, "The ally with the lowest share of Max HP comes first.");
            Assert.AreEqual(Power, heals[0].Amount, 0.0001f);
            Assert.AreEqual(50f, badly.CurrentHP, 0.0001f);
            Assert.AreEqual(40f, lightly.CurrentHP, 0.0001f);
        }

        [Test]
        public void AnEqualWoundGoesToTheLowerId()
        {
            List<BattleEvent> heals = Heals(RunOneTurn(new List<CombatUnit>
            {
                Medic(0, 0), Soldier(4, 1, 0, 50f), Soldier(2, 0, 1, 50f), DistantEnemy()
            }));

            Assert.AreEqual(2, heals[0].TargetId);
        }

        [Test]
        public void TheMedicFightsWhenNoAllyIsHurtEnough()
        {
            // 80 of 100 is above the default 75% line: not worth a turn.
            BattleTurnResult result = RunOneTurn(new List<CombatUnit> { Medic(0, 0), Soldier(2, 1, 0, 20f), DistantEnemy() });

            Assert.AreEqual(0, Heals(result).Count);
        }

        [Test]
        public void TheMedicDoesNotReachPastItsRange()
        {
            BattleTurnResult result = RunOneTurn(new List<CombatUnit> { Medic(0, 0), Soldier(2, 3, 0, 80f), DistantEnemy() });

            Assert.AreEqual(0, Heals(result).Count, "Range 2 cannot reach an ally three cells away.");
        }

        [Test]
        public void TheMedicNeverHealsItself()
        {
            CombatUnit medic = Medic(0, 0);
            medic.ApplyDamage(90f);
            BattleTurnResult result = RunOneTurn(new List<CombatUnit> { medic, DistantEnemy() });

            Assert.AreEqual(0, Heals(result).Count);
        }

        [Test]
        public void AHealStopsAtMaxHealth()
        {
            CombatConfig config = CombatTestFactory.Config(0, 10);
            config.HealBelowFraction = 1f;
            CombatUnit ally = Soldier(2, 1, 0, 5f);
            List<BattleEvent> heals = Heals(RunOneTurn(new List<CombatUnit> { Medic(0, 0), ally, DistantEnemy() }, config));

            Assert.AreEqual(5f, heals[0].Amount, 0.0001f, "The event reports what was restored, not what was offered.");
            Assert.AreEqual(100f, ally.CurrentHP, 0.0001f);
        }

        [Test]
        public void HealingReceivedFromABondScalesTheHeal()
        {
            var bond = new KapatiranBond("Vanguard_FieldMedic", "Vanguard", "FieldMedic", new List<StatModifier>
            {
                StatModifier.Percent(StatKind.HealingReceived, 0.25f, ModifierSource.Kapatiran, "Vanguard_FieldMedic")
            });

            CombatUnit vanguard = Soldier(2, 1, 0, 60f, "Vanguard");
            List<BattleEvent> heals = Heals(RunOneTurn(new List<CombatUnit> { Medic(0, 0), vanguard, DistantEnemy() },
                bonds: new KapatiranResolver(new List<KapatiranBond> { bond })));

            Assert.AreEqual(Power * 1.25f, heals[0].Amount, 0.0001f);
        }

        [Test]
        public void ABattleWithAMedicReplaysIdentically()
        {
            BattleResult first = BuildMedicSkirmish().RunToCompletion();
            BattleResult second = BuildMedicSkirmish().RunToCompletion();

            Assert.AreEqual(first.ToLogText(), second.ToLogText());
            StringAssert.Contains("UnitHealed", first.ToLogText(), "The skirmish should actually exercise the heal.");
        }

        private static BattleSimulator BuildMedicSkirmish()
        {
            UnitStats stats = CombatTestFactory.Stats(maxHP: 120f, attackDamage: 18f, evasion: 0.2f,
                rangedAccuracy: 0.9f, criticalHitChance: 0.2f, movementSpeed: 1f);
            CombatUnit medic = CombatTestFactory.Unit(1, Team.Katipunan,
                CombatTestFactory.Stats(attackDamage: 5f, attackRange: 2f, movementSpeed: 1f), 0, 3, "FieldMedic");
            medic.HealPower = Power;

            var units = new List<CombatUnit>
            {
                medic,
                CombatTestFactory.Unit(2, Team.Katipunan, stats, 1, 2),
                CombatTestFactory.Unit(3, Team.Katipunan, stats, 1, 4),
                CombatTestFactory.Unit(4, Team.Spanish, stats, 6, 2),
                CombatTestFactory.Unit(5, Team.Spanish, stats, 6, 4),
                CombatTestFactory.Unit(6, Team.Spanish, stats, 7, 3)
            };

            return new BattleSimulator(CombatTestFactory.FlatGrid(8, 8), units, CombatTestFactory.Config(77, 80));
        }

        [Test]
        public void OnlyTheFieldMedicIsAHealer()
        {
            foreach (UnitArchetype archetype in UnitCatalog.All)
            {
                if (archetype.Id == UnitCatalog.FieldMedic)
                {
                    Assert.Greater(archetype.HealPower, 0f);
                }
                else
                {
                    Assert.AreEqual(0f, archetype.HealPower, archetype.Id + " should not heal.");
                }
            }
        }
    }
}
