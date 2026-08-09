using System;

namespace Gaffer.Infrastructure.Persistence
{
    /// <summary>
    /// Where a resumed run's future comes from — the one place in the game that reads ambient entropy.
    /// <para>
    /// It lives in Infrastructure because that is what it IS: a read of the machine's clock. The pure core
    /// may not do this (NON-NEGOTIABLE #2 — <c>Common</c>/<c>Domain</c>/<c>Application</c> take randomness
    /// through an injected <c>IRandom</c> and never reach for a clock, a GUID or a global RNG), and that
    /// constraint is the reason <c>RunSessionFactory.Resume</c> takes a continuation seed as an ARGUMENT
    /// instead of making one. The core stays a pure function of its seed; who chooses the seed moved out
    /// here, where a test never goes.
    /// </para>
    /// <para>
    /// WHAT IT BUYS: football's uncertainty across a reload. Before schema v6 the season seed came back out
    /// of the save, so replaying week 12 from the same file always produced the same scoreline; now the
    /// same tactics and the same eleven can lose the match they won an hour ago, which is the behaviour the
    /// owner asked for ("FM'de aynı taktik ve aynı kadroyla bile çıksan sonuç değişir"). The accepted cost
    /// is that save-scumming becomes possible — see the note on <c>RunSessionFactory.Resume</c>.
    /// </para>
    /// <para>
    /// A caller that wants the OLD behaviour does not come here: it passes the save's own
    /// <c>MatchSeed</c> and the run replays exactly. That is the shape a debug tool or a bug-report
    /// reproduction uses.
    /// </para>
    /// </summary>
    public static class ContinuationSeed
    {
        /// <summary>
        /// A fresh seed for this moment. <see cref="Guid.NewGuid"/> rather than the tick count alone: two
        /// loads inside the same clock resolution are an ordinary double-click, and they must not resume
        /// onto the same future. The two halves of the GUID are folded together and avalanche-mixed
        /// (SplitMix64's finalizer) so the seed is well-distributed rather than merely unique — the match
        /// seeds are derived from it by a mix that assumes nothing about its shape, but a run should not
        /// start life clustered next to the last one either.
        /// </summary>
        public static ulong Fresh()
        {
            byte[] bytes = Guid.NewGuid().ToByteArray();
            ulong low = BitConverter.ToUInt64(bytes, 0);
            ulong high = BitConverter.ToUInt64(bytes, 8);

            unchecked
            {
                ulong z = low ^ (high * 0x9E3779B97F4A7C15UL);
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
