namespace Gaffer.Domain.Players
{
    /// <summary>
    /// How well a player suits the slot he has been put in — the one classification both the auto-picker
    /// and the match strength read, so "out of position" means the same thing to the game as it does on
    /// the screen the manager is looking at.
    ///
    /// <para><b>Why tiers and not a distance.</b> Football Manager grades the same thing on a six-step
    /// scale (Natural, Accomplished, Competent, Unconvincing, Awkward, Ineffectual) because a manager
    /// reasons in kinds, not in percentages: a midfielder at full-back is a *compromise*, a centre-back
    /// on the wing is an *emergency*, and an outfielder in goal is not football. Five tiers is as fine as
    /// this game's role model can honestly distinguish — Gaffer knows a player's role and the slot's role,
    /// and nothing about his individual history in it.</para>
    /// </summary>
    /// <remarks>
    /// The numeric values are pinned as a PERSISTENCE CONTRACT (NON-NEGOTIABLE #9). Nothing writes a fit
    /// to a save today — it is derived from a role pair — but it is the same class of classification data
    /// as <see cref="Position"/>, and pinning after something serializes it is too late.
    /// </remarks>
    public enum PositionalFit
    {
        /// <summary>His own role. No penalty — this is what the rating already means.</summary>
        Natural = 0,

        /// <summary>Another role on the same line: a central midfielder at right midfield.</summary>
        SameLine = 1,

        /// <summary>The next line along: a defender in midfield, a midfielder up front.</summary>
        AdjacentLine = 2,

        /// <summary>Two lines away or more: a centre-back asked to play striker.</summary>
        DistantLine = 3,

        /// <summary>Goalkeeper and outfielder are not interchangeable, in either direction.</summary>
        Impossible = 4,
    }
}
