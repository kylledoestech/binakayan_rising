using BinakayanRising.Core.Combat;
using NUnit.Framework;

namespace BinakayanRising.Tests.Combat
{
    /// <summary>
    /// Covers <see cref="DeterministicRandom"/>. Everything about battle reproducibility rests on
    /// this class, so the tests check the properties the simulation actually relies on: identical
    /// streams for identical seeds, different streams for different seeds, bounded output, and no
    /// dice consumed for degenerate probabilities.
    /// </summary>
    [TestFixture]
    public class DeterministicRandomTests
    {
        [Test]
        public void SameSeedProducesTheSameStream()
        {
            DeterministicRandom a = new DeterministicRandom(12345);
            DeterministicRandom b = new DeterministicRandom(12345);

            for (int i = 0; i < 200; i++)
            {
                Assert.AreEqual(a.NextUInt64(), b.NextUInt64(), "Draw " + i + " diverged.");
            }
        }

        [Test]
        public void DifferentSeedsProduceDifferentStreams()
        {
            DeterministicRandom a = new DeterministicRandom(1);
            DeterministicRandom b = new DeterministicRandom(2);

            bool anyDifference = false;
            for (int i = 0; i < 50; i++)
            {
                if (a.NextUInt64() != b.NextUInt64())
                {
                    anyDifference = true;
                }
            }

            Assert.IsTrue(anyDifference, "Adjacent seeds must not produce identical streams.");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void EverySeedIncludingZeroAndNegativesYieldsANonDegenerateStream(int seed)
        {
            DeterministicRandom rng = new DeterministicRandom(seed);
            ulong first = rng.NextUInt64();

            bool sawSomethingElse = false;
            for (int i = 0; i < 20; i++)
            {
                if (rng.NextUInt64() != first)
                {
                    sawSomethingElse = true;
                }
            }

            Assert.IsTrue(sawSomethingElse, "Seed " + seed + " produced a constant stream.");
        }

        [Test]
        public void NextFloat01StaysInRange()
        {
            DeterministicRandom rng = new DeterministicRandom(7);
            for (int i = 0; i < 5000; i++)
            {
                float value = rng.NextFloat01();
                Assert.IsTrue(value >= 0f && value < 1f, "Out of range: " + value);
            }
        }

        [Test]
        public void NextIntStaysWithinBoundsAndCoversThem()
        {
            DeterministicRandom rng = new DeterministicRandom(99);
            bool sawMin = false;
            bool sawMax = false;

            for (int i = 0; i < 5000; i++)
            {
                int value = rng.NextInt(3, 8);
                Assert.IsTrue(value >= 3 && value < 8, "Out of range: " + value);
                if (value == 3)
                {
                    sawMin = true;
                }

                if (value == 7)
                {
                    sawMax = true;
                }
            }

            Assert.IsTrue(sawMin && sawMax, "Range endpoints were never produced.");
        }

        [Test]
        public void NextIntWithAnEmptyRangeReturnsTheBound()
        {
            DeterministicRandom rng = new DeterministicRandom(5);
            Assert.AreEqual(4, rng.NextInt(4, 4));
        }

        [Test]
        public void DegenerateProbabilitiesConsumeNoDice()
        {
            DeterministicRandom rng = new DeterministicRandom(42);
            ulong before = rng.State;

            Assert.IsFalse(rng.Chance(0f));
            Assert.IsFalse(rng.Chance(-1f));
            Assert.IsTrue(rng.Chance(1f));
            Assert.IsTrue(rng.Chance(2f));

            Assert.AreEqual(before, rng.State, "A certain or impossible chance must not advance the stream.");
        }

        [Test]
        public void ChanceIsRoughlyCalibrated()
        {
            DeterministicRandom rng = new DeterministicRandom(2024);
            int hits = 0;
            for (int i = 0; i < 10000; i++)
            {
                if (rng.Chance(0.25f))
                {
                    hits++;
                }
            }

            Assert.IsTrue(hits > 2200 && hits < 2800, "Expected roughly 2500 hits in 10000, got " + hits);
        }

        [Test]
        public void CloneContinuesTheSameStreamWithoutDisturbingTheOriginal()
        {
            DeterministicRandom rng = new DeterministicRandom(31337);
            rng.NextUInt64();

            DeterministicRandom clone = rng.Clone();

            Assert.AreEqual(rng.NextUInt64(), clone.NextUInt64());
        }
    }
}
