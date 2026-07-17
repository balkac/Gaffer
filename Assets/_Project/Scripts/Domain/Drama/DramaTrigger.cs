namespace Gaffer.Domain.Drama
{
    /// <summary>
    /// The pure condition data that gates a drama event — who can be its subject and what club state
    /// it needs. Zero means "not conditioned on this". The engine evaluates these against its weekly
    /// snapshot; the definition itself never reads live state. A settings-style mutable POCO so the
    /// authoring surface maps onto it field by field.
    /// </summary>
    public sealed class DramaTrigger
    {
        /// <summary>Subject must be at most this old (0 = any age).</summary>
        public int MaxSubjectAge { get; set; }

        /// <summary>Subject must be at least this old (0 = any age).</summary>
        public int MinSubjectAge { get; set; }

        /// <summary>Subject's role rating must be at least this (0 = any) — "your star", not anyone.</summary>
        public double MinSubjectRating { get; set; }

        /// <summary>Subject's hidden potential must exceed his current rating by at least this (0 = any)
        /// — the wonderkid filter.</summary>
        public double MinSubjectPotentialGap { get; set; }

        /// <summary>Subject must not be in the current starting eleven (the bench grievance filter).</summary>
        public bool SubjectBenched { get; set; }

        /// <summary>The club must have lost at least this many consecutive matches (0 = any form).</summary>
        public int MinLossStreak { get; set; }

        /// <summary>The club must sit at or below this 1-based table position (0 = anywhere) — pressure events.</summary>
        public int MinTablePosition { get; set; }

        /// <summary>The transfer window must be open (transfer-flavored events).</summary>
        public bool RequiresOpenWindow { get; set; }
    }
}
