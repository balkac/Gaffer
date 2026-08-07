using Gaffer.Common;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class SplitMix64RandomNumberGeneratorTests
    {
        // KNOWN-ANSWER VECTORS. Every value below was produced by an INDEPENDENT implementation of the
        // published SplitMix64 definition (Steele/Lea/Flood 2014) — a throwaway Python script doing
        //     s += 0x9E3779B97F4A7C15;  z = s;
        //     z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
        //     z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
        //     return z ^ (z >> 31);
        // with every operation masked to 64 bits. They were NOT captured from
        // SplitMix64RandomNumberGenerator: recording our own output and asserting it back would only prove
        // the generator is self-consistent, not that it is SplitMix64. Twenty consecutive draws per seed
        // (rather than three) is what makes a transcription slip in a shift or a multiplier impossible to
        // survive — a wrong constant can coincide on one draw, never on twenty across three seeds.
        // Determinism is NON-NEGOTIABLE #2, and this is the only test that pins the stream to something
        // outside this repository.

        private static readonly ulong[] Seed0Stream =
        {
            0xE220A8397B1DCDAFUL, 0x6E789E6AA1B965F4UL, 0x06C45D188009454FUL, 0xF88BB8A8724C81ECUL,
            0x1B39896A51A8749BUL, 0x53CB9F0C747EA2EAUL, 0x2C829ABE1F4532E1UL, 0xC584133AC916AB3CUL,
            0x3EE5789041C98AC3UL, 0xF3B8488C368CB0A6UL, 0x657EECDD3CB13D09UL, 0xC2D326E0055BDEF6UL,
            0x8621A03FE0BBDB7BUL, 0x8E1F7555983AA92FUL, 0xB54E0F1600CC4D19UL, 0x84BB3F97971D80ABUL,
            0x7D29825C75521255UL, 0xC3CF17102B7F7F86UL, 0x3466E9A083914F64UL, 0xD81A8D2B5A4485ACUL,
        };

        private static readonly ulong[] Seed42Stream =
        {
            0xBDD732262FEB6E95UL, 0x28EFE333B266F103UL, 0x47526757130F9F52UL, 0x581CE1FF0E4AE394UL,
            0x09BC585A244823F2UL, 0xDE4431FA3C80DB06UL, 0x37E9671C45376D5DUL, 0xCCF635EE9E9E2FA4UL,
            0x5705B8770B3D7DD5UL, 0x9E54D738297F77AEUL, 0x3474724A775B19BFUL, 0x7E348A0E451650BEUL,
            0x836DED897F3E46E6UL, 0x851F977347ED6DB7UL, 0xAA47E31C02E78EDCUL, 0x341452C54D7C33F2UL,
            0x1A83D752F35EBA75UL, 0x7ED90003F67F9E1DUL, 0x17EADFF448A86A07UL, 0xB05ECA1A2972B860UL,
        };

        /// <summary>A third seed with bits set right across the word (0x0123456789ABCDEF), so the vectors
        /// are not all drawn from a state whose high half starts near zero.</summary>
        private const ulong ThirdSeed = 0x0123456789ABCDEFUL;

        private static readonly ulong[] ThirdSeedStream =
        {
            0x157A3807A48FAA9DUL, 0xD573529B34A1D093UL, 0x2F90B72E996DCCBEUL, 0xA2D419334C4667ECUL,
            0x01404CE914938008UL, 0x14BC574C2A2B4C72UL, 0xB8FC5B1060708C05UL, 0x8931545F4F9EA651UL,
            0xF984DB4EF14FDE1BUL, 0x2680D065CB73ECE7UL, 0xCDB8C9CD9A62DA0FUL, 0x6A6E60FD5089ADECUL,
            0x8EBA85B28DF77747UL, 0x97F6C69811CFB13BUL, 0x380E8B5C685039CFUL, 0xD7EBCCA19D49C3F5UL,
            0x2AB8C4E395CB5958UL, 0x0028BABE93685D04UL, 0x997F31F8A4CD9C80UL, 0xD21D99F3172D8BACUL,
        };

        private static void AssertMatchesReferenceStream(ulong seed, ulong[] expected)
        {
            var rng = new SplitMix64RandomNumberGenerator(seed);

            for (int draw = 0; draw < expected.Length; draw++)
            {
                Assert.That(rng.NextUInt64(), Is.EqualTo(expected[draw]),
                    $"seed {seed}: draw {draw} diverges from the SplitMix64 reference stream.");
            }
        }

        [Test]
        public void NextUInt64_Seed0_MatchesReferenceStream()
        {
            AssertMatchesReferenceStream(0UL, Seed0Stream);
        }

        [Test]
        public void NextUInt64_Seed42_MatchesReferenceStream()
        {
            AssertMatchesReferenceStream(42UL, Seed42Stream);
        }

        [Test]
        public void NextUInt64_ThirdSeed_MatchesReferenceStream()
        {
            AssertMatchesReferenceStream(ThirdSeed, ThirdSeedStream);
        }

        [Test]
        public void Reseed_RestartsTheReferenceStream()
        {
            // State/Reseed is how sub-streams are resumed after a save (PERFORMANCE §8), so it has to land
            // on the reference stream too — not merely on whatever the generator produced last time.
            var rng = new SplitMix64RandomNumberGenerator(ThirdSeed);
            for (int i = 0; i < 5; i++)
            {
                rng.NextUInt64();
            }

            rng.Reseed(42UL);

            for (int draw = 0; draw < Seed42Stream.Length; draw++)
            {
                Assert.That(rng.NextUInt64(), Is.EqualTo(Seed42Stream[draw]));
            }
        }

        [Test]
        public void NextUInt64_SameSeed_IsDeterministic()
        {
            var first = new SplitMix64RandomNumberGenerator(12345);
            var second = new SplitMix64RandomNumberGenerator(12345);

            for (int i = 0; i < 1000; i++)
            {
                Assert.That(first.NextUInt64(), Is.EqualTo(second.NextUInt64()));
            }
        }

        [Test]
        public void NextUInt64_DifferentSeeds_Diverge()
        {
            var first = new SplitMix64RandomNumberGenerator(1);
            var second = new SplitMix64RandomNumberGenerator(2);

            Assert.That(first.NextUInt64(), Is.Not.EqualTo(second.NextUInt64()));
        }

        [Test]
        public void NextInt_WithUpperBound_StaysInRange()
        {
            var rng = new SplitMix64RandomNumberGenerator(7);

            for (int i = 0; i < 10000; i++)
            {
                int value = rng.NextInt(6);
                Assert.That(value, Is.InRange(0, 5));
            }
        }

        [Test]
        public void NextInt_WithBounds_StaysInRange()
        {
            var rng = new SplitMix64RandomNumberGenerator(7);

            for (int i = 0; i < 10000; i++)
            {
                int value = rng.NextInt(-3, 4);
                Assert.That(value, Is.InRange(-3, 3));
            }
        }

        [Test]
        public void NextInt_MinNotBelowMax_Throws()
        {
            var rng = new SplitMix64RandomNumberGenerator(0);

            Assert.That(() => rng.NextInt(5, 5), Throws.ArgumentException);
        }

        [Test]
        public void NextDouble_OverManyDraws_StaysInUnitInterval()
        {
            var rng = new SplitMix64RandomNumberGenerator(99);

            for (int i = 0; i < 10000; i++)
            {
                double value = rng.NextDouble();
                Assert.That(value, Is.GreaterThanOrEqualTo(0.0));
                Assert.That(value, Is.LessThan(1.0));
            }
        }
    }
}
