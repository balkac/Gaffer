using System.Collections.Generic;

namespace Gaffer.Application.Transfers
{
    /// <summary>
    /// A scout's read on one attribute — a low–high band that always contains the true value but narrows
    /// as the player is watched more (ART_STYLE §4.1 scout mask). At full accuracy low == high == truth.
    /// </summary>
    public readonly struct AttributeEstimate
    {
        public AttributeEstimate(string labelKey, int low, int high)
        {
            LabelKey = labelKey;
            Low = low;
            High = high;
        }

        /// <summary>
        /// The attribute's localization key ("attr.finishing.abbrev"), not the word. A scout report is
        /// core data that reaches the player, and raw display text in the core is forbidden
        /// (NON-NEGOTIABLE #8) — Presentation resolves this against the string table. It was named
        /// <c>Label</c> while already carrying a key, which is exactly how a key ends up rendered
        /// verbatim to a player.
        /// </summary>
        public string LabelKey { get; }

        public int Low { get; }

        public int High { get; }
    }

    /// <summary>
    /// What a manager actually knows about a target: banded estimates of the role's key attributes and of
    /// the hidden potential, plus how well he has been scouted (0 = a name on a list, 1 = fully known). The
    /// discovery fantasy lives here — at low accuracy a gem's potential band overlaps an ordinary player's,
    /// so you are betting on a hunch; scouting narrows the bands until the ceiling is clear (TDD §5).
    /// </summary>
    public sealed class ScoutReport
    {
        public ScoutReport(double accuracy, int potentialLow, int potentialHigh, IReadOnlyList<AttributeEstimate> keyAttributes)
        {
            Accuracy = accuracy;
            PotentialLow = potentialLow;
            PotentialHigh = potentialHigh;
            KeyAttributes = keyAttributes;
        }

        public double Accuracy { get; }

        public int PotentialLow { get; }

        public int PotentialHigh { get; }

        public IReadOnlyList<AttributeEstimate> KeyAttributes { get; }
    }
}
