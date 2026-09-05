namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// What each tactical setting is CALLED, as localization keys.
    ///
    /// <para>Keys and never words, the same division <c>PlayerRoles.GetShortLabelKey</c> keeps: this
    /// assembly may carry a key and may not carry a sentence a player reads (NON-NEGOTIABLE #8). The
    /// words live in the string table, in every shipped locale, and <c>UiTextKeys</c> walks these enums to
    /// make the guard demand them.</para>
    ///
    /// <para>They sit beside the enums rather than in Presentation because the axes are the simulation's
    /// vocabulary — the match screen reads them to say what was played, the tactics screen will read them
    /// to offer the choice, and neither should own the naming.</para>
    /// </summary>
    public static class TacticsTextKeys
    {
        public static string For(Mentality mentality)
        {
            switch (mentality)
            {
                case Mentality.VeryDefensive:
                    return "tactics.mentality.very_defensive";
                case Mentality.Defensive:
                    return "tactics.mentality.defensive";
                case Mentality.Balanced:
                    return "tactics.mentality.balanced";
                case Mentality.Attacking:
                    return "tactics.mentality.attacking";
                default:
                    return "tactics.mentality.very_attacking";
            }
        }

        public static string For(Tempo tempo)
        {
            switch (tempo)
            {
                case Tempo.Patient:
                    return "tactics.tempo.patient";
                case Tempo.Standard:
                    return "tactics.tempo.standard";
                default:
                    return "tactics.tempo.intense";
            }
        }

        public static string For(Pressing pressing)
        {
            switch (pressing)
            {
                case Pressing.Contain:
                    return "tactics.pressing.contain";
                case Pressing.Standard:
                    return "tactics.pressing.standard";
                default:
                    return "tactics.pressing.press";
            }
        }

        public static string For(Approach approach)
        {
            switch (approach)
            {
                case Approach.Possession:
                    return "tactics.approach.possession";
                case Approach.Balanced:
                    return "tactics.approach.balanced";
                default:
                    return "tactics.approach.counter";
            }
        }
    }
}
