using System.Collections.Generic;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Narrative
{
    /// <summary>
    /// A whole spell at the club in one shape: how he arrived, what he did, how he left, and what the club
    /// made on him. The read model the sale moment and the season recap are written from (Faz 5.5).
    ///
    /// <para><b>It stores nothing.</b> Every number here is read back out of the journey the run already
    /// keeps — a projection, built on demand, so there is no second copy of a career to keep in step with
    /// the first (ARCHITECTURE §8a). That is also why it is cheap enough to build at the moment a player
    /// is sold rather than maintained all season for the one time it is read.</para>
    ///
    /// <para><b><see cref="Profit"/> is the point of the whole game loop.</b> GDD §4.4's discover-grow-sell
    /// flip only pays off if the payoff is visible: found for nothing, grown for three seasons, sold for
    /// twelve million. The moments carry the two fees already, so the flip can finally be stated as one
    /// number instead of being something the manager is expected to have kept track of himself.</para>
    /// </summary>
    public readonly struct CareerSummary
    {
        private CareerSummary(
            PlayerId player,
            string name,
            int firstSeason,
            int lastSeason,
            int appearances,
            int goals,
            long arrivalFee,
            bool cameThroughTheAcademy,
            long departureFee,
            bool hasLeft)
        {
            Player = player;
            Name = name;
            FirstSeason = firstSeason;
            LastSeason = lastSeason;
            Appearances = appearances;
            Goals = goals;
            ArrivalFee = arrivalFee;
            CameThroughTheAcademy = cameThroughTheAcademy;
            DepartureFee = departureFee;
            HasLeft = hasLeft;
        }

        public PlayerId Player { get; }

        /// <summary>His name as the journey recorded it — the squad may no longer know him.</summary>
        public string Name { get; }

        public int FirstSeason { get; }

        public int LastSeason { get; }

        public int SeasonsAtTheClub => (LastSeason - FirstSeason) + 1;

        public int Appearances { get; }

        public int Goals { get; }

        /// <summary>What he cost. Zero for a free transfer and for an academy player.</summary>
        public long ArrivalFee { get; }

        /// <summary>True when the club made him rather than bought him — a different story from a free transfer.</summary>
        public bool CameThroughTheAcademy { get; }

        /// <summary>What he went for. Zero while he is still at the club.</summary>
        public long DepartureFee { get; }

        public bool HasLeft { get; }

        /// <summary>What the club made on him. Negative is a real answer, and an honest one.</summary>
        public long Profit => DepartureFee - ArrivalFee;

        /// <summary>
        /// Reads a journey into a summary. Null journey gives a default summary rather than throwing:
        /// asking about a player the manager never had is a question with an answer ("nothing"), not a
        /// broken invariant (CONVENTIONS §4).
        /// </summary>
        public static CareerSummary Create(PlayerJourney journey)
        {
            if (journey == null)
            {
                return default;
            }

            IReadOnlyList<CareerMoment> moments = journey.Moments;
            int first = int.MaxValue;
            int last = int.MinValue;
            long arrivalFee = 0L;
            long departureFee = 0L;
            bool academy = false;
            bool left = false;

            for (int i = 0; i < moments.Count; i++)
            {
                CareerMoment moment = moments[i];
                if (moment.Season < first)
                {
                    first = moment.Season;
                }

                if (moment.Season > last)
                {
                    last = moment.Season;
                }

                switch (moment.Kind)
                {
                    case CareerMomentKind.Signing:
                        arrivalFee = moment.Count;
                        break;
                    case CareerMomentKind.AcademyArrival:
                        academy = true;
                        break;
                    case CareerMomentKind.Sale:
                        departureFee = moment.Count;
                        left = true;
                        break;
                    case CareerMomentKind.Retirement:
                        left = true;
                        break;
                    default:
                        // Every other kind is part of the story without changing its shape.
                        break;
                }
            }

            return new CareerSummary(
                journey.Player,
                journey.Name,
                first == int.MaxValue ? 0 : first,
                last == int.MinValue ? 0 : last,
                journey.Appearances,
                journey.Goals,
                arrivalFee,
                academy,
                departureFee,
                left);
        }
    }
}
