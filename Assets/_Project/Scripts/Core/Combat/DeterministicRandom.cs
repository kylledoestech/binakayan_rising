using System;

namespace BinakayanRising.Core.Combat
{
    /// <summary>
    /// A seeded pseudo-random number generator whose output depends on nothing but its seed and the
    /// number of draws taken from it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is deliberately NOT <see cref="System.Random"/> and NOT <c>UnityEngine.Random</c>.
    /// <see cref="System.Random"/>'s algorithm is an implementation detail that has already changed
    /// once between .NET runtimes, and <c>UnityEngine.Random</c> is process-global mutable state
    /// that the editor, the physics system and any third-party package can perturb. Either would
    /// mean the same seed produced a different battle on a different machine, which would make the
    /// auto-battler unreproducible and every determinism test worthless.
    /// </para>
    /// <para>
    /// The algorithm is xorshift64* : a 64-bit xorshift core followed by a multiplication, seeded
    /// through a SplitMix64 mixing step so that adjacent seeds (0, 1, 2 ...) produce well-separated
    /// streams. It uses only integer arithmetic defined exactly by the C# specification, so results
    /// are bit-identical on every platform the game ships to.
    /// </para>
    /// <para>
    /// Not thread-safe by design: a battle is simulated on one thread, and adding a lock would
    /// invite callers to draw from it concurrently, which would destroy determinism.
    /// </para>
    /// </remarks>
    public sealed class DeterministicRandom
    {
        private const ulong SplitMixIncrement = 0x9E3779B97F4A7C15UL;
        private const ulong Xorshift64StarMultiplier = 0x2545F4914F6CDD1DUL;

        /// <summary>
        /// Scale used to turn a 24-bit integer into a float in <c>[0, 1)</c>. 24 bits is exactly the
        /// mantissa width of a <see cref="float"/>, so every value produced is exactly representable
        /// and the conversion never rounds up to 1.0f.
        /// </summary>
        private const float Float01Scale = 1f / 16777216f;

        private readonly int seed;
        private ulong state;

        /// <summary>
        /// Creates a generator for the given seed. Every seed, including zero and negative values,
        /// produces a valid non-degenerate stream.
        /// </summary>
        /// <param name="seed">The battle seed. The same seed always replays the same battle.</param>
        public DeterministicRandom(int seed)
        {
            this.seed = seed;
            state = Mix((ulong)(uint)seed + SplitMixIncrement);

            if (state == 0UL)
            {
                // xorshift is degenerate at zero; any non-zero constant restores a full-period stream.
                state = SplitMixIncrement;
            }
        }

        /// <summary>The seed this generator was created with.</summary>
        public int Seed
        {
            get { return seed; }
        }

        /// <summary>
        /// The current internal state. Exposed so a battle can be snapshotted and resumed, and so
        /// tests can assert that two runs consumed the same number of draws.
        /// </summary>
        public ulong State
        {
            get { return state; }
        }

        /// <summary>
        /// Creates an independent generator positioned at this one's current state, so a caller can
        /// speculatively draw without disturbing the battle's stream.
        /// </summary>
        public DeterministicRandom Clone()
        {
            DeterministicRandom copy = new DeterministicRandom(seed);
            copy.state = state;
            return copy;
        }

        /// <summary>Draws the next raw 64-bit value and advances the stream.</summary>
        public ulong NextUInt64()
        {
            unchecked
            {
                ulong x = state;
                x ^= x >> 12;
                x ^= x << 25;
                x ^= x >> 27;
                state = x;
                return x * Xorshift64StarMultiplier;
            }
        }

        /// <summary>Draws the next raw 32-bit value, taken from the high half of the 64-bit output.</summary>
        public uint NextUInt32()
        {
            return (uint)(NextUInt64() >> 32);
        }

        /// <summary>
        /// Draws a float uniformly distributed in <c>[0, 1)</c>. The upper bound is exclusive, so
        /// <c>Chance(1f)</c> is handled as a special case rather than relying on a draw.
        /// </summary>
        public float NextFloat01()
        {
            return (NextUInt32() >> 8) * Float01Scale;
        }

        /// <summary>
        /// Draws an integer uniformly distributed in <c>[minInclusive, maxExclusive)</c>.
        /// </summary>
        /// <param name="minInclusive">Lower bound, included in the range.</param>
        /// <param name="maxExclusive">Upper bound, excluded from the range.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="maxExclusive"/> is less than <paramref name="minInclusive"/>.
        /// </exception>
        /// <remarks>
        /// Uses rejection sampling rather than a plain modulo so the distribution stays uniform even
        /// when the range does not divide 2^32. Rejection consumes extra draws, but it does so
        /// deterministically, which is all the simulation requires.
        /// </remarks>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive < minInclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive), maxExclusive, "maxExclusive must be greater than or equal to minInclusive.");
            }

            if (maxExclusive == minInclusive)
            {
                return minInclusive;
            }

            uint range = (uint)((long)maxExclusive - minInclusive);
            uint limit = (uint)(0x100000000UL % range);

            uint draw;
            do
            {
                draw = NextUInt32();
            }
            while (draw < limit);

            return (int)(minInclusive + (long)(draw % range));
        }

        /// <summary>
        /// Returns <c>true</c> with the given probability.
        /// </summary>
        /// <param name="probability">
        /// Chance of success as a 0..1 fraction. Values at or below zero always fail and values at
        /// or above one always succeed, and neither case consumes a draw — so a battle configured
        /// with no randomness (all chances 0 or 1) leaves the stream untouched and stays trivially
        /// reproducible.
        /// </param>
        public bool Chance(float probability)
        {
            if (probability <= 0f)
            {
                return false;
            }

            if (probability >= 1f)
            {
                return true;
            }

            return NextFloat01() < probability;
        }

        /// <summary>SplitMix64 finaliser, used only to spread the seed across the 64-bit state.</summary>
        private static ulong Mix(ulong value)
        {
            unchecked
            {
                ulong z = value;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
