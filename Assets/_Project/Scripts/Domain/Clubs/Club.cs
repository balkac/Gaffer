namespace Gaffer.Domain.Clubs
{
    /// <summary>
    /// A club in a league: its identity, a (fictional, generated) display name, and its strength.
    /// The name is a proper noun carried as data — not a localization key — and the generator that
    /// produces fictional names lands in Application/Generation (Faz 3). Strength stands in for a
    /// squad until players feed it.
    /// </summary>
    public sealed class Club
    {
        public Club(ClubId id, string name, TeamStrength strength)
        {
            Id = id;
            Name = name;
            Strength = strength;
        }

        public ClubId Id { get; }

        public string Name { get; }

        public TeamStrength Strength { get; }
    }
}
