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
        public Player(PlayerId id, string name, string nationality, Position position, int age, Attributes attributes, byte hiddenPotential)
        {
            Id = id;
            Name = name;
            Nationality = nationality;
            Position = position;
            Age = age;
            Attributes = attributes;
            HiddenPotential = hiddenPotential;
        }

        public PlayerId Id { get; }

        public string Name { get; }

        public string Nationality { get; }

        public Position Position { get; }

        public int Age { get; }

        public Attributes Attributes { get; }

        public byte HiddenPotential { get; }
    }
}
