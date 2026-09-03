using BinakayanRising.Core.Combat;
using NUnit.Framework;

namespace BinakayanRising.Tests.Combat
{
    /// <summary>
    /// Covers <see cref="ModifierStack"/>: the two stacking policies, and the flat-versus-percentage
    /// distinction the capstone document forces by mixing "+20% Defense" with "+1 Attack Range".
    /// </summary>
    [TestFixture]
    public class ModifierStackTests
    {
        [Test]
        public void EmptyStackReturnsBaseValue()
        {
            ModifierStack stack = new ModifierStack();
            Assert.AreEqual(100f, stack.Resolve(StatKind.Defense, 100f), 0.0001f);
        }

        [Test]
        public void AdditivePercentSumsPercentagesBeforeApplyingThem()
        {
            ModifierStack stack = new ModifierStack(ModifierStackingPolicy.AdditivePercent);
            stack.Add(StatModifier.Percent(StatKind.Defense, 0.20f, ModifierSource.Terrain));
            stack.Add(StatModifier.Percent(StatKind.Defense, 0.10f, ModifierSource.Kapatiran));

            // 100 * (1 + 0.20 + 0.10) == 130
            Assert.AreEqual(130f, stack.Resolve(StatKind.Defense, 100f), 0.0001f);
        }

        [Test]
        public void MultiplicativePercentCompoundsPercentages()
        {
            ModifierStack stack = new ModifierStack(ModifierStackingPolicy.MultiplicativePercent);
            stack.Add(StatModifier.Percent(StatKind.Defense, 0.20f, ModifierSource.Terrain));
            stack.Add(StatModifier.Percent(StatKind.Defense, 0.10f, ModifierSource.Kapatiran));

            // 100 * 1.20 * 1.10 == 132
            Assert.AreEqual(132f, stack.Resolve(StatKind.Defense, 100f), 0.0001f);
        }

        [Test]
        public void TheTwoPoliciesDisagreeOnceMoreThanOneSourceApplies()
        {
            ModifierStack additive = new ModifierStack(ModifierStackingPolicy.AdditivePercent);
            ModifierStack multiplicative = new ModifierStack(ModifierStackingPolicy.MultiplicativePercent);

            additive.Add(StatModifier.Percent(StatKind.Defense, 0.20f, ModifierSource.Terrain));
            additive.Add(StatModifier.Percent(StatKind.Defense, 0.10f, ModifierSource.Kapatiran));
            multiplicative.Add(StatModifier.Percent(StatKind.Defense, 0.20f, ModifierSource.Terrain));
            multiplicative.Add(StatModifier.Percent(StatKind.Defense, 0.10f, ModifierSource.Kapatiran));

            Assert.AreNotEqual(
                additive.Resolve(StatKind.Defense, 100f),
                multiplicative.Resolve(StatKind.Defense, 100f),
                "The stacking policy must be a real choice, not a cosmetic one.");
        }

        [Test]
        public void ASinglePercentageResolvesIdenticallyUnderBothPolicies()
        {
            ModifierStack additive = new ModifierStack(ModifierStackingPolicy.AdditivePercent);
            ModifierStack multiplicative = new ModifierStack(ModifierStackingPolicy.MultiplicativePercent);
            additive.Add(StatModifier.Percent(StatKind.Defense, 0.20f, ModifierSource.Terrain));
            multiplicative.Add(StatModifier.Percent(StatKind.Defense, 0.20f, ModifierSource.Terrain));

            Assert.AreEqual(
                additive.Resolve(StatKind.Defense, 100f),
                multiplicative.Resolve(StatKind.Defense, 100f),
                0.0001f);
        }

        [Test]
        public void FlatAttackRangeBonusAddsExactlyOneCell()
        {
            ModifierStack stack = new ModifierStack();
            stack.Add(StatModifier.Flat(StatKind.AttackRange, 1f, ModifierSource.Kapatiran, "RankA"));

            // Capstone Table 3 rank A grants "+1 Attack Range", not "+100% Attack Range".
            Assert.AreEqual(3f, stack.Resolve(StatKind.AttackRange, 2f), 0.0001f);
        }

        [Test]
        public void APercentageOfOneWouldDoubleTheRangeWhichIsWhyFlatExists()
        {
            ModifierStack stack = new ModifierStack();
            stack.Add(StatModifier.Percent(StatKind.AttackRange, 1f, ModifierSource.Kapatiran));

            Assert.AreEqual(4f, stack.Resolve(StatKind.AttackRange, 2f), 0.0001f);
        }

        [Test]
        public void FlatIsAppliedAfterPercentagesRatherThanBeingScaledByThem()
        {
            ModifierStack stack = new ModifierStack(ModifierStackingPolicy.AdditivePercent);
            stack.Add(StatModifier.Percent(StatKind.AttackRange, 0.50f, ModifierSource.Terrain));
            stack.Add(StatModifier.Flat(StatKind.AttackRange, 1f, ModifierSource.Kapatiran));

            // (2 * 1.5) + 1 == 4, not (2 + 1) * 1.5 == 4.5
            Assert.AreEqual(4f, stack.Resolve(StatKind.AttackRange, 2f), 0.0001f);
        }

        [Test]
        public void ModifiersForOtherStatsAreIgnored()
        {
            ModifierStack stack = new ModifierStack();
            stack.Add(StatModifier.Percent(StatKind.Evasion, 0.15f, ModifierSource.Terrain));

            Assert.AreEqual(100f, stack.Resolve(StatKind.Defense, 100f), 0.0001f);
        }

        [Test]
        public void ClearSourceRemovesOnlyThatSource()
        {
            ModifierStack stack = new ModifierStack();
            stack.Add(StatModifier.Percent(StatKind.Defense, 0.20f, ModifierSource.Terrain));
            stack.Add(StatModifier.Percent(StatKind.Defense, 0.10f, ModifierSource.Kapatiran));

            stack.ClearSource(ModifierSource.Terrain);

            Assert.AreEqual(1, stack.Count);
            Assert.AreEqual(110f, stack.Resolve(StatKind.Defense, 100f), 0.0001f);
        }

        [Test]
        public void NegativePercentagesReduceTheStat()
        {
            ModifierStack stack = new ModifierStack();
            stack.Add(StatModifier.Percent(StatKind.MovementSpeed, -0.15f, ModifierSource.Terrain, "CoastalShallows"));

            Assert.AreEqual(0.85f, stack.Resolve(StatKind.MovementSpeed, 1f), 0.0001f);
        }

        [Test]
        public void WithModifiersResolvesEveryStatAtOnce()
        {
            UnitStats baseStats = CombatTestFactory.Stats(maxHP: 100f, defense: 10f, evasion: 0.2f);
            ModifierStack stack = new ModifierStack();
            stack.Add(StatModifier.Percent(StatKind.Defense, 0.20f, ModifierSource.Terrain, "Trench"));
            stack.Add(StatModifier.Percent(StatKind.Evasion, 0.15f, ModifierSource.Terrain, "Trench"));

            UnitStats effective = baseStats.WithModifiers(stack);

            Assert.AreEqual(12f, effective.Defense, 0.0001f);
            Assert.AreEqual(0.23f, effective.Evasion, 0.0001f);
            Assert.AreEqual(100f, effective.MaxHP, 0.0001f, "Unmodified stats must pass through untouched.");
        }
    }
}
