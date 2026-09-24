using System;
using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Content;
using NUnit.Framework;

namespace BinakayanRising.Tests.Combat
{
    /// <summary>
    /// Covers the bonds in <see cref="BondCatalog"/> end to end: the real bond, the real unit stats
    /// from <see cref="UnitCatalog"/>, one simulated turn, and the stats that turn leaves behind.
    /// </summary>
    [TestFixture]
    public class BondCatalogTests
    {
        private const float Tolerance = 0.0001f;

        /// <summary>A catalog unit that stays where it is put.</summary>
        private static CombatUnit FromCatalog(int id, string archetypeId, int x, int y)
        {
            UnitArchetype archetype = UnitCatalog.Find(archetypeId);
            CombatUnit unit = CombatTestFactory.Unit(id, Team.Katipunan,
                archetype.BaseStats.With(StatKind.MovementSpeed, 0f), x, y, archetypeId);
            unit.HealPower = archetype.HealPower;
            return unit;
        }

        /// <summary>An enemy far enough away that nothing reaches it, or it anything, on turn one.</summary>
        private static CombatUnit DistantEnemy()
        {
            return CombatTestFactory.Unit(9, Team.Spanish, CombatTestFactory.Stats(movementSpeed: 0f), 9, 9);
        }

        private static BattleTurnResult RunOneTurn(List<CombatUnit> units, KapatiranBond bond)
        {
            var simulator = new BattleSimulator(CombatTestFactory.FlatGrid(10, 10), units,
                CombatTestFactory.Config(0, 10), kapatiran: new KapatiranResolver(new List<KapatiranBond> { bond }));
            return simulator.ExecuteTurn();
        }

        private static float FirstHealAmount(BattleTurnResult result)
        {
            for (int i = 0; i < result.Events.Count; i++)
            {
                if (result.Events[i].Type == BattleEventType.UnitHealed)
                {
                    return result.Events[i].Amount;
                }
            }

            return -1f;
        }

        [Test]
        public void TheVanguardAndMedicBondBoostsTheHealAndTheVanguardsMaxHP()
        {
            CombatUnit medic = FromCatalog(1, UnitCatalog.FieldMedic, 0, 0);
            CombatUnit vanguard = FromCatalog(2, UnitCatalog.Vanguard, 1, 0);
            vanguard.ApplyDamage(100f);
            float baseMaxHP = UnitCatalog.Find(UnitCatalog.Vanguard).BaseStats.MaxHP;

            BattleTurnResult result = RunOneTurn(new List<CombatUnit> { medic, vanguard, DistantEnemy() },
                BondCatalog.VanguardAndMedic());

            Assert.AreEqual(medic.HealPower * 1.25f, FirstHealAmount(result), Tolerance, "+25% Healing Received.");
            Assert.AreEqual(baseMaxHP * 1.05f, vanguard.GetEffectiveStat(StatKind.MaxHP), Tolerance, "+5% Max HP.");
            Assert.AreEqual("A", BondCatalog.VanguardAndMedic().RankLabel);
        }

        [Test]
        public void TheVanguardAndMedicBondDoesNothingApart()
        {
            CombatUnit medic = FromCatalog(1, UnitCatalog.FieldMedic, 0, 0);
            CombatUnit vanguard = FromCatalog(2, UnitCatalog.Vanguard, 2, 0);
            vanguard.ApplyDamage(100f);

            BattleTurnResult result = RunOneTurn(new List<CombatUnit> { medic, vanguard, DistantEnemy() },
                BondCatalog.VanguardAndMedic());

            Assert.AreEqual(medic.HealPower, FirstHealAmount(result), Tolerance,
                "Two tiles apart the medic still heals, but the bond is not active.");
            Assert.AreEqual(1f, vanguard.GetEffectiveStat(StatKind.HealingReceived), Tolerance);
        }

        [TestCase("A", 0.15f, 0.10f)]
        [TestCase("B", 0.05f, 0f)]
        public void TheMagdaloAndMagdiwangBondRaisesCritAndEvasionByRank(string rank, float critBonus, float evasionBonus)
        {
            CombatUnit magdalo = FromCatalog(1, UnitCatalog.MagdaloInfantry, 0, 0);
            CombatUnit magdiwang = FromCatalog(2, UnitCatalog.MagdiwangInfantry, 0, 1);

            RunOneTurn(new List<CombatUnit> { magdalo, magdiwang, DistantEnemy() }, BondCatalog.MagdaloAndMagdiwang(rank));

            foreach (CombatUnit unit in new[] { magdalo, magdiwang })
            {
                UnitStats stats = UnitCatalog.Find(unit.ArchetypeId).BaseStats;
                Assert.AreEqual(stats.CriticalHitChance * (1f + critBonus),
                    unit.GetEffectiveStat(StatKind.CriticalHitChance), Tolerance, unit.ArchetypeId + " crit");
                Assert.AreEqual(stats.Evasion * (1f + evasionBonus),
                    unit.GetEffectiveStat(StatKind.Evasion), Tolerance, unit.ArchetypeId + " evasion");
            }
        }

        [Test]
        public void TheMagdaloAndMagdiwangBondHasOnlyRanksBAndA()
        {
            Assert.Throws<ArgumentException>(() => BondCatalog.MagdaloAndMagdiwang("C"));
            Assert.AreEqual("A", BondCatalog.MagdaloAndMagdiwang(BondCatalog.RankA).RankLabel);
        }
    }
}
