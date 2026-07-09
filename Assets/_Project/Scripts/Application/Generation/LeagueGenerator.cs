using System;
using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;

namespace Gaffer.Application.Generation
{
    /// <summary>
    /// Generates a whole league: a fictional name, and <c>clubCount</c> clubs each with a distinct generated
    /// name, a squad drawn from an ability band set by its rank (top clubs stronger, bottom clubs weaker), and
    /// a strength derived from that squad. This is the "world is generated" step (decision #6) — no hand-authored
    /// club list. Deterministic through the injected rng: the same seed reproduces the same league, clubs, and
    /// rosters. Player ids are offset by rank so they never collide across clubs.
    /// </summary>
    public sealed class LeagueGenerator
    {
        private const int TopCentre = 72;
        private const int BottomCentre = 46;
        private const int BandHalfWidth = 10;

        private readonly ClubNameGenerator _names = new ClubNameGenerator();
        private readonly SquadGenerator _squads;
        private readonly EffectiveStrengthBuilder _strength = new EffectiveStrengthBuilder();

        public LeagueGenerator(SquadGenerator squads)
        {
            _squads = squads;
        }

        public League Generate(int clubCount, IRandom rng)
        {
            string leagueName = _names.GenerateLeagueName(rng);
            IReadOnlyList<string> clubNames = _names.GenerateDistinct(clubCount, rng);

            var clubs = new List<Club>(clubCount);
            for (int rank = 0; rank < clubCount; rank++)
            {
                GenerationContext context = ContextForRank(rank, clubCount);
                Squad squad = _squads.Generate(rank * SquadGenerator.SquadSize, context, rng);
                clubs.Add(new Club(new ClubId(rank), clubNames[rank], squad, _strength.Build(squad)));
            }

            return new League(leagueName, clubs);
        }

        // Top clubs draw from a higher ability band, bottom clubs a lower one — a believable spread, so the
        // table's shape emerges from the rosters rather than a scalar. (Formerly inline in the editor.)
        private static GenerationContext ContextForRank(int rank, int count)
        {
            double t = count <= 1 ? 0.0 : (double)rank / (count - 1);
            int centre = (int)Math.Round(TopCentre - (t * (TopCentre - BottomCentre)));
            return new GenerationContext
            {
                MinAbility = Clamp(centre - BandHalfWidth),
                MaxAbility = Clamp(centre + BandHalfWidth),
            };
        }

        private static byte Clamp(int value)
        {
            if (value < 1)
            {
                return 1;
            }

            return value > 99 ? (byte)99 : (byte)value;
        }
    }
}
