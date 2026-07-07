using System.Collections.Generic;
using Gaffer.Domain.Clubs;

namespace Gaffer.Application.Season
{
    /// <summary>
    /// Builds a full double round-robin schedule with the circle method: every club plays every other
    /// once at home and once away, and each round is a match week where every club plays exactly once
    /// (one club rests on a bye when the count is odd). Deterministic — a given club order yields the
    /// same schedule.
    /// </summary>
    public sealed class FixtureScheduler
    {
        public IReadOnlyList<Fixture> CreateDoubleRoundRobin(IReadOnlyList<ClubId> clubs)
        {
            var fixtures = new List<Fixture>();
            if (clubs.Count < 2)
            {
                return fixtures;
            }

            var rotation = new List<int>(clubs.Count + 1);
            for (int i = 0; i < clubs.Count; i++)
            {
                rotation.Add(i);
            }

            bool hasBye = rotation.Count % 2 != 0;
            if (hasBye)
            {
                rotation.Add(-1); // bye slot
            }

            int slots = rotation.Count;
            int firstLegRounds = slots - 1;

            for (int round = 0; round < firstLegRounds; round++)
            {
                for (int pair = 0; pair < slots / 2; pair++)
                {
                    int a = rotation[pair];
                    int b = rotation[slots - 1 - pair];
                    if (a < 0 || b < 0)
                    {
                        continue; // a bye
                    }

                    // Alternate home/away so a club is not always home in the first leg.
                    bool firstIsHome = (round + pair) % 2 == 0;
                    ClubId home = firstIsHome ? clubs[a] : clubs[b];
                    ClubId away = firstIsHome ? clubs[b] : clubs[a];
                    fixtures.Add(new Fixture(round, home, away));
                }

                RotateKeepingFirst(rotation);
            }

            // Second leg mirrors the first with home and away swapped, on later rounds.
            int firstLegCount = fixtures.Count;
            for (int i = 0; i < firstLegCount; i++)
            {
                Fixture first = fixtures[i];
                fixtures.Add(new Fixture(first.Round + firstLegRounds, first.Away, first.Home));
            }

            return fixtures;
        }

        private static void RotateKeepingFirst(List<int> rotation)
        {
            int last = rotation[rotation.Count - 1];
            rotation.RemoveAt(rotation.Count - 1);
            rotation.Insert(1, last);
        }
    }
}
