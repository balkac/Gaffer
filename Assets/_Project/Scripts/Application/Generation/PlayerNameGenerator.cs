using Gaffer.Common;

namespace Gaffer.Application.Generation
{
    /// <summary>
    /// Builds a player name by combining a first and last name from component pools — generic human
    /// names, so no specific real player is reproduced (unlike club names, these are ordinary names,
    /// not trademarks). Deterministic through the injected rng. Nationality-flavoured pools are a
    /// later refinement.
    /// </summary>
    public sealed class PlayerNameGenerator
    {
        private static readonly string[] FirstNames =
        {
            "James", "William", "Thomas", "George", "Harry", "Jack", "Charlie", "Oscar",
            "Leo", "Arthur", "Henry", "Alfie", "Freddie", "Theo", "Ethan", "Noah",
            "Daniel", "Samuel", "Joseph", "Adam", "Owen", "Callum", "Connor", "Kyle",
            "Nathan", "Aaron", "Reece", "Liam",
        };

        private static readonly string[] LastNames =
        {
            "Walker", "Wright", "Turner", "Hughes", "Baker", "Palmer", "Foster", "Barnes",
            "Reid", "Chapman", "Marsh", "Doyle", "Pierce", "Hale", "Ward", "Shaw",
            "Bell", "Cross", "Frost", "Nash", "Vance", "Quinn", "Rowe", "Sharpe",
            "Lowe", "Kerr", "Pratt", "Grant",
        };

        public string GenerateName(IRandom rng)
        {
            string first = FirstNames[rng.NextInt(FirstNames.Length)];
            string last = LastNames[rng.NextInt(LastNames.Length)];
            return first + " " + last;
        }
    }
}
