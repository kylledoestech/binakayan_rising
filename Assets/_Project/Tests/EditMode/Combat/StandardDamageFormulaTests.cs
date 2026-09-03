using BinakayanRising.Core.Combat;
using NUnit.Framework;

namespace BinakayanRising.Tests.Combat
{
    /// <summary>
    /// Covers <see cref="StandardDamageFormula"/>. These tests pin the placeholder formula's
    /// behaviour so that when the design team ratifies a real one, the change is visible as a test
    /// diff rather than a silent balance shift.
    /// </summary>
    [TestFixture]
    public class StandardDamageFormulaTests
    {
        private DeterministicRandom rng;

        [SetUp]
        public void SetUp()
        {
            rng = new DeterministicRandom(1);
        }

        [Test]
        public void SubtractiveMitigationRemovesDefenseFromAttack()
        {
            CombatConfig config = CombatTestFactory.Config();
            config.MitigationMode = DamageMitigationMode.Subtractive;
            StandardDamageFormula formula = new StandardDamageFormula(config);

            DamageResult result = formula.Compute(
                CombatTestFactory.Stats(attackDamage: 20f),
                CombatTestFactory.Stats(defense: 8f),
                rng);

            Assert.IsTrue(result.Connected);
            Assert.AreEqual(12f, result.Amount, 0.0001f);
        }

        [Test]
        public void MultiplicativeMitigationTreatsDefenseAsAFraction()
        {
            CombatConfig config = CombatTestFactory.Config();
            config.MitigationMode = DamageMitigationMode.Multiplicative;
            StandardDamageFormula formula = new StandardDamageFormula(config);

            DamageResult result = formula.Compute(
                CombatTestFactory.Stats(attackDamage: 20f),
                CombatTestFactory.Stats(defense: 0.25f),
                rng);

            Assert.AreEqual(15f, result.Amount, 0.0001f);
        }

        [Test]
        public void AVeryHighDefenseStillTakesTheConfiguredMinimumDamage()
        {
            CombatConfig config = CombatTestFactory.Config();
            config.MinimumDamage = 1f;
            StandardDamageFormula formula = new StandardDamageFormula(config);

            DamageResult result = formula.Compute(
                CombatTestFactory.Stats(attackDamage: 5f),
                CombatTestFactory.Stats(defense: 1000f),
                rng);

            Assert.AreEqual(1f, result.Amount, 0.0001f);
        }

        [Test]
        public void AnAttackNeverHealsEvenWithNoDamageFloor()
        {
            CombatConfig config = CombatTestFactory.Config();
            config.MinimumDamage = 0f;
            StandardDamageFormula formula = new StandardDamageFormula(config);

            DamageResult result = formula.Compute(
                CombatTestFactory.Stats(attackDamage: 5f),
                CombatTestFactory.Stats(defense: 1000f),
                rng);

            Assert.AreEqual(0f, result.Amount, 0.0001f);
            Assert.IsTrue(result.Amount >= 0f, "Mitigation must never produce negative damage.");
        }

        [Test]
        public void NegativeDamageIsClampedByTheResultTypeItself()
        {
            DamageResult result = new DamageResult(-50f, false, false, false);
            Assert.AreEqual(0f, result.Amount, 0.0001f);
        }

        [Test]
        public void ACertainCriticalHitMultipliesMitigatedDamage()
        {
            CombatConfig config = CombatTestFactory.Config();
            config.CriticalHitMultiplier = 3f;
            StandardDamageFormula formula = new StandardDamageFormula(config);

            DamageResult result = formula.Compute(
                CombatTestFactory.Stats(attackDamage: 20f, criticalHitChance: 1f),
                CombatTestFactory.Stats(defense: 5f),
                rng);

            Assert.IsTrue(result.WasCrit);

            // Crit multiplies AFTER mitigation: (20 - 5) * 3 == 45, not (20 * 3) - 5 == 55.
            Assert.AreEqual(45f, result.Amount, 0.0001f);
        }

        [Test]
        public void ACertainEvasionAvoidsTheAttackEntirely()
        {
            StandardDamageFormula formula = new StandardDamageFormula(CombatTestFactory.Config());

            DamageResult result = formula.Compute(
                CombatTestFactory.Stats(attackDamage: 20f),
                CombatTestFactory.Stats(evasion: 1f),
                rng);

            Assert.IsTrue(result.WasEvaded);
            Assert.IsFalse(result.Connected);
            Assert.AreEqual(0f, result.Amount, 0.0001f);
        }

        [Test]
        public void ZeroAccuracyAlwaysMisses()
        {
            StandardDamageFormula formula = new StandardDamageFormula(CombatTestFactory.Config());

            DamageResult result = formula.Compute(
                CombatTestFactory.Stats(attackDamage: 20f, rangedAccuracy: 0f),
                CombatTestFactory.Stats(),
                rng);

            Assert.IsTrue(result.WasMissed);
            Assert.AreEqual(0f, result.Amount, 0.0001f);
        }

        [Test]
        public void DisablingTheAccuracyRollMakesZeroAccuracyHarmless()
        {
            CombatConfig config = CombatTestFactory.Config();
            config.ApplyAccuracyRoll = false;
            StandardDamageFormula formula = new StandardDamageFormula(config);

            DamageResult result = formula.Compute(
                CombatTestFactory.Stats(attackDamage: 20f, rangedAccuracy: 0f),
                CombatTestFactory.Stats(),
                rng);

            Assert.IsTrue(result.Connected);
            Assert.AreEqual(20f, result.Amount, 0.0001f);
        }

        [Test]
        public void EvasionIsRolledBeforeAccuracySoADodgeCostsExactlyOneDraw()
        {
            StandardDamageFormula formula = new StandardDamageFormula(CombatTestFactory.Config());
            DeterministicRandom local = new DeterministicRandom(4);
            ulong before = local.State;

            DamageResult result = formula.Compute(
                CombatTestFactory.Stats(attackDamage: 20f),
                CombatTestFactory.Stats(evasion: 1f),
                local);

            Assert.IsTrue(result.WasEvaded);
            Assert.AreEqual(before, local.State, "A certain dodge draws no dice, so the stream must be untouched.");
        }

        [Test]
        public void TheSameSeedAndInputsProduceTheSameResult()
        {
            StandardDamageFormula formula = new StandardDamageFormula(CombatTestFactory.Config());
            UnitStats attacker = CombatTestFactory.Stats(attackDamage: 20f, criticalHitChance: 0.5f);
            UnitStats defender = CombatTestFactory.Stats(defense: 2f, evasion: 0.3f);

            DeterministicRandom first = new DeterministicRandom(555);
            DeterministicRandom second = new DeterministicRandom(555);

            for (int i = 0; i < 100; i++)
            {
                DamageResult a = formula.Compute(attacker, defender, first);
                DamageResult b = formula.Compute(attacker, defender, second);
                Assert.AreEqual(a.ToString(), b.ToString(), "Attack " + i + " diverged.");
            }
        }
    }
}
