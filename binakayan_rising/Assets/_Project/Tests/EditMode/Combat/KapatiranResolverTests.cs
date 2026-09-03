using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using NUnit.Framework;

namespace BinakayanRising.Tests.Combat
{
    /// <summary>
    /// Covers <see cref="KapatiranResolver"/>: which bonded pairs count as "in proximity" under each
    /// injected rule, and that a bond grants its modifiers to both partners.
    /// </summary>
    [TestFixture]
    public class KapatiranResolverTests
    {
        private BattleGrid grid;

        [SetUp]
        public void SetUp()
        {
            grid = CombatTestFactory.FlatGrid(10, 10);
        }

        private static CombatUnit Bonded(int id, string archetype, int x, int y)
        {
            return CombatTestFactory.Unit(id, Team.Katipunan, CombatTestFactory.Stats(), x, y, archetype);
        }

        [Test]
        public void TwoBondedUnitsPlacedAdjacentActivateTheBond()
        {
            KapatiranResolver resolver = new KapatiranResolver(new List<KapatiranBond> { CombatTestFactory.AttackBond() });
            List<CombatUnit> units = new List<CombatUnit>
            {
                Bonded(1, "Evangelista", 3, 3),
                Bonded(2, "Aguinaldo", 4, 3)
            };

            IReadOnlyList<KapatiranActivation> activations = resolver.Resolve(units, grid);

            Assert.AreEqual(1, activations.Count);
            Assert.AreEqual(1, activations[0].UnitA.Id);
            Assert.AreEqual(2, activations[0].UnitB.Id);
        }

        [Test]
        public void TheSameTwoUnitsPlacedFarApartDoNotActivateTheBond()
        {
            KapatiranResolver resolver = new KapatiranResolver(new List<KapatiranBond> { CombatTestFactory.AttackBond() });
            List<CombatUnit> units = new List<CombatUnit>
            {
                Bonded(1, "Evangelista", 0, 0),
                Bonded(2, "Aguinaldo", 9, 9)
            };

            Assert.AreEqual(0, resolver.Resolve(units, grid).Count);
        }

        [Test]
        public void OrthogonalAdjacencyExcludesDiagonalNeighbours()
        {
            KapatiranResolver resolver = new KapatiranResolver(
                new List<KapatiranBond> { CombatTestFactory.AttackBond() },
                KapatiranProximityRule.Orthogonal);

            List<CombatUnit> units = new List<CombatUnit>
            {
                Bonded(1, "Evangelista", 3, 3),
                Bonded(2, "Aguinaldo", 4, 4)
            };

            Assert.AreEqual(0, resolver.Resolve(units, grid).Count);
        }

        [Test]
        public void DiagonalAdjacencyIncludesDiagonalNeighbours()
        {
            KapatiranResolver resolver = new KapatiranResolver(
                new List<KapatiranBond> { CombatTestFactory.AttackBond() },
                KapatiranProximityRule.Diagonal);

            List<CombatUnit> units = new List<CombatUnit>
            {
                Bonded(1, "Evangelista", 3, 3),
                Bonded(2, "Aguinaldo", 4, 4)
            };

            Assert.AreEqual(1, resolver.Resolve(units, grid).Count);
        }

        [TestCase(2, 2, true)]
        [TestCase(2, 3, false)]
        [TestCase(3, 3, true)]
        public void RadiusRuleHonoursTheInjectedRadius(int radius, int separation, bool expectActive)
        {
            KapatiranResolver resolver = new KapatiranResolver(
                new List<KapatiranBond> { CombatTestFactory.AttackBond() },
                KapatiranProximityRule.Radius,
                radius);

            List<CombatUnit> units = new List<CombatUnit>
            {
                Bonded(1, "Evangelista", 0, 0),
                Bonded(2, "Aguinaldo", separation, 0)
            };

            Assert.AreEqual(expectActive ? 1 : 0, resolver.Resolve(units, grid).Count);
        }

        [Test]
        public void ABondWithAFallenBrotherGrantsNothing()
        {
            KapatiranResolver resolver = new KapatiranResolver(new List<KapatiranBond> { CombatTestFactory.AttackBond() });
            CombatUnit first = Bonded(1, "Evangelista", 3, 3);
            CombatUnit second = Bonded(2, "Aguinaldo", 4, 3);
            second.Kill();

            Assert.AreEqual(0, resolver.Resolve(new List<CombatUnit> { first, second }, grid).Count);
        }

        [Test]
        public void UnrelatedArchetypesStandingAdjacentActivateNothing()
        {
            KapatiranResolver resolver = new KapatiranResolver(new List<KapatiranBond> { CombatTestFactory.AttackBond() });
            List<CombatUnit> units = new List<CombatUnit>
            {
                Bonded(1, "Evangelista", 3, 3),
                Bonded(2, "SpanishRegular", 4, 3)
            };

            Assert.AreEqual(0, resolver.Resolve(units, grid).Count);
        }

        [Test]
        public void BondsDoNotCrossTeamsByDefault()
        {
            KapatiranResolver resolver = new KapatiranResolver(new List<KapatiranBond> { CombatTestFactory.AttackBond() });
            List<CombatUnit> units = new List<CombatUnit>
            {
                CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(), 3, 3, "Evangelista"),
                CombatTestFactory.Unit(2, Team.Spanish, CombatTestFactory.Stats(), 4, 3, "Aguinaldo")
            };

            Assert.AreEqual(0, resolver.Resolve(units, grid).Count);
        }

        [Test]
        public void ActivationsAreOrderedByUnitIdRegardlessOfInputOrder()
        {
            KapatiranResolver resolver = new KapatiranResolver(new List<KapatiranBond> { CombatTestFactory.AttackBond() });
            CombatUnit high = Bonded(9, "Evangelista", 3, 3);
            CombatUnit low = Bonded(4, "Aguinaldo", 4, 3);

            IReadOnlyList<KapatiranActivation> forward = resolver.Resolve(new List<CombatUnit> { high, low }, grid);
            IReadOnlyList<KapatiranActivation> reverse = resolver.Resolve(new List<CombatUnit> { low, high }, grid);

            Assert.AreEqual(4, forward[0].UnitA.Id);
            Assert.AreEqual(9, forward[0].UnitB.Id);
            Assert.AreEqual(forward[0].UnitA.Id, reverse[0].UnitA.Id);
            Assert.AreEqual(forward[0].UnitB.Id, reverse[0].UnitB.Id);
        }

        [Test]
        public void AResolverWithNoBondsNeverActivatesAnything()
        {
            KapatiranResolver resolver = KapatiranResolver.CreateEmpty();
            List<CombatUnit> units = new List<CombatUnit>
            {
                Bonded(1, "Evangelista", 3, 3),
                Bonded(2, "Aguinaldo", 4, 3)
            };

            Assert.AreEqual(0, resolver.Resolve(units, grid).Count);
        }

        [Test]
        public void AnActivationCarriesTheBondsModifiers()
        {
            KapatiranResolver resolver = new KapatiranResolver(new List<KapatiranBond> { CombatTestFactory.MarksmanEngineerRankA() });
            List<CombatUnit> units = new List<CombatUnit>
            {
                Bonded(1, "Marksman", 3, 3),
                Bonded(2, "Engineer", 3, 4)
            };

            IReadOnlyList<KapatiranActivation> activations = resolver.Resolve(units, grid);

            Assert.AreEqual(1, activations.Count);
            Assert.AreEqual(2, activations[0].Modifiers.Count);
            Assert.AreEqual("A", activations[0].Bond.RankLabel);
        }
    }
}
