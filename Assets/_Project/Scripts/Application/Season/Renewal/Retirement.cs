using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Season
{
    /// <summary>
    /// When a career ends. One rule, one owner (ARCHITECTURE §8a): a club's roster turns its veterans over
    /// through <see cref="SquadRenewal"/> and the transfer market turns its own over through
    /// <see cref="MarketRenewal"/>, and both ask here — so an unattached veteran and a squad veteran hang up
    /// their boots on the same terms. It used to be private to <see cref="SquadRenewal"/>, which is exactly
    /// how a second, drifting copy gets written the moment a second caller appears.
    /// </summary>
    public static class Retirement
    {
        /// <summary>
        /// Whether this player retires now. Nobody plays past the hard age (later for keepers, who last
        /// longest); before the twilight age nobody goes at all. Between them the odds climb with age and
        /// ease for a higher rating, so a star lingers while a fading squad player calls it a day.
        ///
        /// <para>Draws from <paramref name="rng"/> only inside the twilight band, which is what lets a
        /// caller seed one stream per player and still get the same answer it always did
        /// (NON-NEGOTIABLE #2).</para>
        /// </summary>
        public static bool Retires(Player player, RenewalSettings settings, IRandom rng)
        {
            bool keeper = player.Role == PlayerRole.Goalkeeper;
            int twilight = keeper ? settings.KeeperTwilightAge : settings.OutfielderTwilightAge;
            int hard = keeper ? settings.KeeperHardAge : settings.OutfielderHardAge;

            if (player.Age >= hard)
            {
                return true;
            }

            if (player.Age < twilight)
            {
                return false;
            }

            // The twilight band is the divisor. A config where Hard is not past Twilight would make it
            // zero, and 0/0 is NaN — `rng.NextDouble() < NaN` is false, so nobody would ever retire and
            // squads would age forever with nothing thrown anywhere (CONVENTIONS §6). Clamping the band
            // keeps the answer sensible instead of resting on the two returns above happening to cover
            // that config today. A no-op for every Hard > Twilight, which is every shipped value.
            int twilightBand = hard > twilight ? hard - twilight : 1;
            double progress = (double)(player.Age - twilight) / twilightBand;
            double rating = PlayerRatings.ForRole(player);
            double chance = progress * (1.0 - (settings.RetirementRatingEase * (rating / 100.0)));
            return rng.NextDouble() < chance;
        }
    }
}
