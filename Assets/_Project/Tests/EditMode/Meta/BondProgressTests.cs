using System;
using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Grid;
using BinakayanRising.Core.Meta;
using NUnit.Framework;

namespace BinakayanRising.Tests.Meta
{
    /// <summary>
    /// Kapatiran support ranks (#19): side-by-side battles earn C, B and A, the rank is kept in the
    /// save, and a battle is fought with the bonus of the rank each pair has reached.
    /// </summary>
    [TestFixture]
    public class BondProgressTests
    {
        // The opening roster, by save id: 1 Evangelista, 2 Aguinaldo, 3 Marksman, 4 Engineer, 5 Vanguard.
        private const int Evangelista = 1;
        private const int Aguinaldo = 2;
        private const int Marksman = 3;
        private const int Engineer = 4;

        private MetaRules rules;
        private MetaGame game;

        [SetUp]
        public void SetUp()
        {
            DateTime now = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
            rules = MetaRules.Default();
            game = new MetaGame(MetaGame.NewGame(rules, now, 42), rules, () => now);
        }

        private static KeyValuePair<int, GridCoord> At(int unitId, int x, int y)
        {
            return new KeyValuePair<int, GridCoord>(unitId, new GridCoord(x, y));
        }

        /// <summary>Evangelista and Aguinaldo side by side; the Marksman and Engineer apart.</summary>
        private List<BondRankUp> FightOnce()
        {
            return game.RecordBondSupport(new[]
            {
                At(Evangelista, 0, 0), At(Aguinaldo, 1, 0), At(Marksman, 0, 3), At(Engineer, 3, 3)
            });
        }

        [Test]
        public void TheRosterStartsWithNoRankAndNoLore()
        {
            foreach (BondPair pair in BondCatalog.Pairs)
            {
                Assert.AreEqual(BondRank.None, game.BondRankOf(pair.Id), pair.Id);
                Assert.IsFalse(game.IsLoreUnlocked(pair.Id), pair.Id);
            }

            Assert.IsEmpty(game.BattleBonds(), "no pair has a bonus before fighting together");
        }

        [Test]
        public void FightingSideBySideClimbsCThenBThenA()
        {
            var ranks = new List<BondRank>();
            var ups = new List<int>();
            for (int battle = 1; battle <= 6; battle++)
            {
                ups.Add(FightOnce().Count);
                ranks.Add(game.BondRankOf(BondCatalog.EvangelistaAguinaldo));
            }

            CollectionAssert.AreEqual(
                new[] { BondRank.C, BondRank.C, BondRank.B, BondRank.B, BondRank.A, BondRank.A }, ranks);
            CollectionAssert.AreEqual(new[] { 1, 0, 1, 0, 1, 0 }, ups, "one card per new rank, none at the cap");
            Assert.AreEqual(0, game.BondSupportForNext(BondCatalog.EvangelistaAguinaldo), "A is the top");
        }

        [Test]
        public void APairThatStoodApartEarnsNothing()
        {
            FightOnce();
            Assert.AreEqual(0, game.BondSupport(BondCatalog.MarksmanEngineer));
            Assert.AreEqual(0, game.BondSupport(BondCatalog.VanguardFieldMedic), "the Field Medic was not deployed");
        }

        [Test]
        public void DiagonalNeighboursAreNotSideBySide()
        {
            game.RecordBondSupport(new[] { At(Marksman, 0, 0), At(Engineer, 1, 1) });
            Assert.AreEqual(0, game.BondSupport(BondCatalog.MarksmanEngineer), "the battle's bond adjacency is four-way");
        }

        [Test]
        public void TheFirstRankUpUnlocksTheLore()
        {
            BondRankUp up = FightOnce()[0];
            Assert.AreEqual(BondCatalog.EvangelistaAguinaldo, up.Pair.Id);
            Assert.AreEqual(BondRank.None, up.From);
            Assert.AreEqual(BondRank.C, up.To);
            Assert.IsTrue(up.UnlockedLore);
            Assert.IsTrue(game.IsLoreUnlocked(BondCatalog.EvangelistaAguinaldo));

            Assert.IsFalse(game.IsLoreHeard(BondCatalog.EvangelistaAguinaldo));
            game.MarkLoreHeard(BondCatalog.EvangelistaAguinaldo);
            Assert.IsTrue(game.IsLoreHeard(BondCatalog.EvangelistaAguinaldo));
        }

        [Test]
        public void BattleBondsCarryEachPairsEarnedRankAndRankCGivesNone()
        {
            FightOnce();
            Assert.IsEmpty(game.BattleBonds(), "rank C is lore only");

            FightOnce();
            FightOnce();
            List<KapatiranBond> bonds = game.BattleBonds();
            Assert.AreEqual(1, bonds.Count);
            Assert.AreEqual(BondCatalog.EvangelistaAguinaldo, bonds[0].BondId);
            Assert.AreEqual(BondCatalog.RankB, bonds[0].RankLabel);
        }

        [Test]
        public void AVersionTwoSaveGainsAnEmptyBondListAndKeepsEverythingElse()
        {
            SaveData data = MetaGame.NewGame(rules, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 1);
            data.version = 2;
            data.bonds = null;
            int units = data.units.Count;

            Assert.IsTrue(data.Repair(rules));
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.IsNotNull(data.bonds);
            Assert.IsEmpty(data.bonds);
            Assert.AreEqual(units, data.units.Count);
        }

        [Test]
        public void RepairDropsUnknownAndDuplicateBondsAndNegativeSupport()
        {
            game.Data.bonds.Add(new BondRecord { bond = BondCatalog.MarksmanEngineer, support = -4 });
            game.Data.bonds.Add(new BondRecord { bond = BondCatalog.MarksmanEngineer, support = 9 });
            game.Data.bonds.Add(new BondRecord { bond = "Bonifacio_Rizal", support = 3 });

            Assert.IsTrue(game.Data.Repair(rules));
            Assert.AreEqual(1, game.Data.bonds.Count);
            Assert.AreEqual(0, game.BondSupport(BondCatalog.MarksmanEngineer), "the first record wins, clamped");
        }
    }
}
