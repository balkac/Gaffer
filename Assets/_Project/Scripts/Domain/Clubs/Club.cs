namespace Gaffer.Domain.Clubs
{
    /// <summary>
    /// A club in a league: its identity, a (fictional, generated) display name, its <see cref="Squad"/>,
    /// and the match <see cref="Strength"/> derived from that squad (BuildEffectiveStrength). The name is
    /// a proper noun carried as data — not a localization key. <see cref="Squad"/> is optional: a restored
    /// club (save/load) or a harness fixture carries only its strength, so it is <c>null</c> there.
    /// </summary>
    public sealed class Club
    {
        public Club(ClubId id, string name, TeamStrength strength)
            : this(id, name, null, strength)
        {
        }

        public Club(ClubId id, string name, Squad squad, TeamStrength strength)
        {
            Id = id;
            Name = name;
            Squad = squad;
            Strength = strength;
        }

        public ClubId Id { get; }

        public string Name { get; }

        /// <summary>The club's roster, or <c>null</c> when the club was built from strength alone.</summary>
        public Squad Squad { get; }

        public TeamStrength Strength { get; }
    }
}
