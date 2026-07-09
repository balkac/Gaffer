using System.Collections.Generic;
using Gaffer.Common;

namespace Gaffer.Application.Generation
{
    /// <summary>
    /// Builds a fictional club or league name from combinatorial parts — a place stem (prefix + ending)
    /// with an optional club word — so a run's world is generated, never a recognisable real club (decision
    /// #6: names are data, and fictional to sidestep trademark risk). The pools multiply into thousands of
    /// distinct names, far more than a league needs, and <see cref="GenerateDistinct"/> guarantees no repeats
    /// within a league. Deterministic through the injected rng: the same seed reproduces the same world.
    /// </summary>
    public sealed class ClubNameGenerator
    {
        // Place names build as prefix + ending: "Ash"+"field", "Bracken"+"moor", "Iron"+"bridge".
        private static readonly string[] Prefixes =
        {
            "Ash", "Bracken", "Cold", "Dun", "Elm", "Fen", "Grave", "Harrow", "Iron", "Kes",
            "Lang", "Marrow", "North", "Oak", "Pem", "Quarry", "Red", "Stone", "Thorn", "West",
            "Black", "Green", "Raven", "Wolf", "Hart", "Bram", "Fair", "Hollow", "Brook", "Kirk",
            "Ember", "Frost", "Glen", "Wold", "Marsh", "Whit",
        };

        private static readonly string[] Endings =
        {
            "field", "moor", "harbour", "more", "wick", "send", "gate", "bridge", "ford", "wood",
            "haven", "bury", "marsh", "cliff", "dale", "ton", "ham", "mouth", "port", "stead",
            "worth", "den", "fell", "brook", "combe", "thorpe", "shaw", "ridge", "borne", "wold",
        };

        // The optional trailing club word; roughly half of clubs take one, the rest stay single-word places.
        private static readonly string[] ClubWords =
        {
            "United", "City", "Town", "Rovers", "Athletic", "Albion", "Wanderers", "County",
            "Vale", "Forest", "Borough", "End",
        };

        private static readonly string[] Regions =
        {
            "Northern", "Southern", "Eastern", "Western", "Central", "Coastal", "Highland",
            "Lowland", "Border", "Valley", "Moorland", "Riverside",
        };

        private static readonly string[] Competitions =
        {
            "League", "Division", "Championship", "Conference", "Premier League", "First Division",
        };

        public string GenerateClubName(IRandom rng)
        {
            string place = Prefixes[rng.NextInt(Prefixes.Length)] + Endings[rng.NextInt(Endings.Length)];

            // Half the clubs carry a club word ("Ashfield United"); the rest are the bare place ("Brackenmoor").
            if (rng.NextInt(2) == 0)
            {
                return place + " " + ClubWords[rng.NextInt(ClubWords.Length)];
            }

            return place;
        }

        public string GenerateLeagueName(IRandom rng)
        {
            return Regions[rng.NextInt(Regions.Length)] + " " + Competitions[rng.NextInt(Competitions.Length)];
        }

        /// <summary>
        /// Returns <paramref name="count"/> distinct club names. Redraws on a collision; on the rare chance the
        /// draws keep colliding, a numbered suffix guarantees the count is met rather than looping forever.
        /// </summary>
        public IReadOnlyList<string> GenerateDistinct(int count, IRandom rng)
        {
            var seen = new HashSet<string>();
            var names = new List<string>(count > 0 ? count : 0);

            int attempts = 0;
            int attemptCap = (count + 1) * 50;
            while (names.Count < count && attempts < attemptCap)
            {
                string name = GenerateClubName(rng);
                if (seen.Add(name))
                {
                    names.Add(name);
                }

                attempts++;
            }

            // Fallback (effectively never hit given the pool size): fill any shortfall with numbered names.
            int fallback = 2;
            while (names.Count < count)
            {
                string name = GenerateClubName(rng) + " " + fallback;
                if (seen.Add(name))
                {
                    names.Add(name);
                }

                fallback++;
            }

            return names;
        }
    }
}
