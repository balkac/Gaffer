using System.Collections.Generic;

namespace Gaffer.Domain.Drama
{
    /// <summary>
    /// One of the decisions a drama event puts in front of the manager. The label is a localization
    /// key (NON-NEGOTIABLE #8); the effects are what choosing it actually does — a choice with no
    /// effects is legal (living with it IS a decision), but an event needs at least two choices to
    /// be drama at all (GDD §4.7 rule 2).
    /// </summary>
    public sealed class DramaChoice
    {
        public DramaChoice(string labelKey, IReadOnlyList<DramaEffect> effects)
        {
            LabelKey = labelKey;
            Effects = effects;
        }

        public string LabelKey { get; }

        public IReadOnlyList<DramaEffect> Effects { get; }
    }
}
