namespace Gaffer.Application.Narrative
{
    /// <summary>
    /// One reason a match might have mattered to one player. Adding a kind of moment means adding a rule,
    /// not editing the recogniser — the open/closed half of "many kinds of moment" (the owner's ask,
    /// 2026-08-13), and the reason each kind can be pinned by a test that knows about nothing else.
    /// </summary>
    public interface IMomentRule
    {
        /// <summary>
        /// True when this rule recognises a moment in <paramref name="occasion"/>, with
        /// <paramref name="moment"/> set to it. Must be pure: same occasion, same answer, no side
        /// effects, no randomness — a memory that changed when you looked at it twice would not be one.
        /// </summary>
        bool Recognise(in MatchOccasion occasion, out CareerMoment moment);
    }
}
