namespace Gaffer.Domain.Players
{
    /// <summary>
    /// A generated player — layer one of four (TDD §4.2): the numbers. Name and nationality are
    /// generated data (proper nouns, not localization keys); attributes are the visible stats and
    /// <see cref="HiddenPotential"/> is the ceiling the scout only estimates (TDD §5). Personality,
    /// relationships, and the journey-with-you layers arrive in later phases.
    /// </summary>
    public sealed class Player
    {
        public Player(PlayerId id, string name, string nationality, PlayerRole role, int age, Attributes attributes, byte hiddenPotential)
        {
            Id = id;
            Name = name;
            Nationality = nationality;
            Role = role;
            Position = PlayerRoles.Line(role);
            Age = age;
            Attributes = attributes;
            HiddenPotential = hiddenPotential;
        }

        /// <summary>Builds a player from the broad line alone, taking a representative role for it.</summary>
        public Player(PlayerId id, string name, string nationality, Position position, int age, Attributes attributes, byte hiddenPotential)
            : this(id, name, nationality, PlayerRoles.Representative(position), age, attributes, hiddenPotential)
        {
        }

        public PlayerId Id { get; }

        public string Name { get; }

        public string Nationality { get; }

        /// <summary>The specific role (e.g. right-back); <see cref="Position"/> is the broad line it sits on.</summary>
        public PlayerRole Role { get; }

        public Position Position { get; }

        public int Age { get; }

        public Attributes Attributes { get; }

        public byte HiddenPotential { get; }
    }
}
