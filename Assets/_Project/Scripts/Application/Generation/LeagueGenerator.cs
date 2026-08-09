using System;
using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;
using Gaffer.Domain.Traits;

namespace Gaffer.Application.Generation
{
    /// <summary>
    /// Generates a whole league: a fictional name, and <c>clubCount</c> clubs each with a distinct generated
    /// name, a squad drawn from an ability band set by its rank (top clubs stronger, bottom clubs weaker), and
    /// a strength derived from that squad. This is the "world is generated" step (decision #6) — no hand-authored
    /// club list. Deterministic through the injected rng: the same seed reproduces the same league, clubs, and
    /// rosters. Player ids are offset by rank so they never collide across clubs.
    /// <para>
    /// THE STRENGTH BUILDER IS INJECTED, and that is load-bearing rather than tidiness. The strength written
    /// here is persisted into the save and is the only strength a squad-less (restored, strength-only) club
    /// ever has, while <c>LeagueSeason</c> re-derives strength through the catalog it was CONFIGURED with. A
    /// builder wired in a field initializer would bind <see cref="TraitCatalog.Default"/>, so an authored
    /// catalog that retunes a teammate aura would produce a league generated under one set of rules and played
    /// under another. Wiring belongs to the composition root, not to a collaborator's field (ARCHITECTURE §6).
    /// </para>
    /// </summary>
    public sealed class LeagueGenerator
    {
        private const int TopCentre = 72;
        private const int BottomCentre = 46;
        private const int BandHalfWidth = 10;

        private readonly ClubNameGenerator _names;
        private readonly SquadGenerator _squads;
        private readonly EffectiveStrengthBuilder _strength;

        /// <summary>
        /// The built-in-catalog convenience: generates under <see cref="TraitCatalog.Default"/>. Correct only
        /// for a caller that also PLAYS on the default catalog (headless tests, the editor harnesses). A caller
        /// holding a configured catalog must pass it — see the overloads — or generation and season diverge.
        /// </summary>
        public LeagueGenerator(SquadGenerator squads)
            : this(squads, TraitCatalog.Default)
        {
        }

        /// <summary>Generates under a specific trait catalog: the strength each club is born with is derived
        /// through the same catalog the season will re-derive it through. Null falls back to the built-in set
        /// (ARCHITECTURE §7) — the fallback lives here, at the wiring seam, not inside the collaborator.</summary>
        public LeagueGenerator(SquadGenerator squads, TraitCatalog traits)
            : this(squads, new EffectiveStrengthBuilder(traits ?? TraitCatalog.Default), null)
        {
        }

        /// <summary>The fully-injected form the composition root uses: it hands over the very builder (and
        /// name generator) the rest of the graph shares, so no collaborator can quietly construct a second
        /// one on different balance.</summary>
        public LeagueGenerator(SquadGenerator squads, EffectiveStrengthBuilder strength, ClubNameGenerator names)
        {
            _squads = squads;
            _strength = strength ?? new EffectiveStrengthBuilder();
            _names = names ?? new ClubNameGenerator();
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
