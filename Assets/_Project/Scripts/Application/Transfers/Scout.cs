using System;
using System.Collections.Generic;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Transfers
{
    /// <summary>
    /// Turns a player's true numbers into what a manager sees at a given scouting accuracy — the mask
    /// (TDD §5 / ART_STYLE §4.1). Each estimate is a band that always contains the truth but whose width
    /// shrinks with accuracy; the band is not centred on the truth (a deterministic per-player offset keeps
    /// the midpoint from giving the value away), yet still brackets it. Deterministic: the same player and
    /// accuracy always produce the same report, so a report is stable frame to frame.
    /// </summary>
    public sealed class Scout
    {
        private const int PotentialMaxWidth = 22;
        private const int AttributeMaxWidth = 12;

        public ScoutReport Observe(Player player, double accuracy)
        {
            double clamped = Clamp01(accuracy);

            int potentialHalf = HalfWidth(PotentialMaxWidth, clamped);
            Band(player.HiddenPotential, potentialHalf, Salt(player.Id.Value, 0), out int potLow, out int potHigh);

            IReadOnlyList<AttributeKey> keys = RoleKeyAttributes.For(player.Position);
            var estimates = new List<AttributeEstimate>(keys.Count);
            int attributeHalf = HalfWidth(AttributeMaxWidth, clamped);
            for (int i = 0; i < keys.Count; i++)
            {
                AttributeKey key = keys[i];
                Band(key.Read(player.Attributes), attributeHalf, Salt(player.Id.Value, i + 1), out int low, out int high);
                estimates.Add(new AttributeEstimate(key.Label, low, high));
            }

            return new ScoutReport(clamped, potLow, potHigh, estimates);
        }

        // Places a band of half-width `half` around the truth, offset by up to ±half/2 (a deterministic
        // per-player nudge so the midpoint is not the truth), then clamps to [1, 99]. Because the offset is
        // at most half the width, the truth is always inside the band.
        private static void Band(int truth, int half, double noise01, out int low, out int high)
        {
            double offset = (noise01 - 0.5) * half;
            double centre = truth + offset;
            low = Clamp((int)Math.Round(centre - half));
            high = Clamp((int)Math.Round(centre + half));
            if (low > truth)
            {
                low = Clamp(truth);
            }

            if (high < truth)
            {
                high = Clamp(truth);
            }
        }

        private static int HalfWidth(int maxWidth, double accuracy)
        {
            return (int)Math.Round(maxWidth * (1.0 - accuracy));
        }

        private static double Salt(int playerId, int channel)
        {
            unchecked
            {
                uint h = (uint)(playerId * 2654435761u) ^ (uint)(channel * 40503u);
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFFu) / 65536.0;
            }
        }

        private static int Clamp(int value)
        {
            if (value < 1)
            {
                return 1;
            }

            return value > 99 ? 99 : value;
        }

        private static double Clamp01(double value)
        {
            if (value < 0.0)
            {
                return 0.0;
            }

            return value > 1.0 ? 1.0 : value;
        }
    }
}
