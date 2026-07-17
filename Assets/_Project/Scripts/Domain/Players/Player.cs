using System;
using System.Collections.Generic;
using Gaffer.Domain.Traits;

namespace Gaffer.Domain.Players
{
    /// <summary>
    /// A generated player — layers one and two of four (TDD §4.2): the numbers, and the traits that
    /// make him a character. Name and nationality are generated data (proper nouns, not localization
    /// keys); attributes are the visible stats and <see cref="HiddenPotential"/> is the ceiling the
    /// scout only estimates (TDD §5). Traits are referenced by id — the definitions live in the
    /// catalog (data), never here. Relationships and the journey-with-you layers arrive later.
    /// </summary>
    public sealed class Player
    {
        public Player(PlayerId id, string name, string nationality, PlayerRole role, int age, Attributes attributes, byte hiddenPotential, IReadOnlyList<TraitId> traits = null)
        {
            Id = id;
            Name = name;
            Nationality = nationality;
            Role = role;
            Position = PlayerRoles.Line(role);
            Age = age;
            Attributes = attributes;
            HiddenPotential = hiddenPotential;
            Traits = traits ?? Array.Empty<TraitId>();
        }

        /// <summary>Builds a player from the broad line alone, taking a representative role for it.</summary>
        public Player(PlayerId id, string name, string nationality, Position position, int age, Attributes attributes, byte hiddenPotential, IReadOnlyList<TraitId> traits = null)
            : this(id, name, nationality, PlayerRoles.Representative(position), age, attributes, hiddenPotential, traits)
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

        /// <summary>The traits this player carries, by id; definitions resolve through the catalog.</summary>
        public IReadOnlyList<TraitId> Traits { get; }
    }
}
