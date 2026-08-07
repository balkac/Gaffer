using System;
using Gaffer.Application.Season;
using UnityEngine;

namespace Gaffer.Infrastructure.Configuration
{
    /// <summary>
    /// The Unity authoring surface for squad-renewal balance (NON-NEGOTIABLE #3): retirement ages, how a high
    /// rating stays a player, the academy-gem cadence and its hidden band, the youth intake age, and the
    /// ordinary youth ability/potential band drawn around the squad's own level. Tune it
    /// in the Inspector and <see cref="ToSettings"/> maps to the pure <see cref="RenewalSettings"/>.
    /// Config-as-override — no asset means <see cref="RenewalSettings.Default"/>. Defaults mirror it.
    /// </summary>
    [CreateAssetMenu(menuName = "Gaffer/Balance/Renewal", fileName = "RenewalBalance")]
    public sealed class RenewalBalanceSO : ScriptableObject
    {
        [Header("Retirement age thresholds (no one plays past Hard; odds climb through the twilight years)")]
        [Range(16, 60)] [SerializeField] private int keeperTwilightAge = 36;
        [Range(16, 60)] [SerializeField] private int keeperHardAge = 43;
        [Range(16, 60)] [SerializeField] private int outfielderTwilightAge = 33;
        [Range(16, 60)] [SerializeField] private int outfielderHardAge = 40;

        [Tooltip("How much a high rating eases retirement in the twilight years (0 = age only).")]
        [SerializeField] private double retirementRatingEase = 0.4;

        [Header("Academy gem — a rare guaranteed cadence, never a per-player chance")]
        [Tooltip("How often, in seasons, a club's academy yields a hidden gem.")]
        [Range(1, 50)] [SerializeField] private int gemCadenceSeasons = 5;
        [Range(1, 99)] [SerializeField] private byte gemMinAbility = 28;
        [Range(1, 99)] [SerializeField] private byte gemMaxAbility = 46;
        [Range(1, 99)] [SerializeField] private byte gemMinPotential = 86;
        [Range(1, 99)] [SerializeField] private byte gemMaxPotential = 96;

        [Header("Youth intake age range")]
        [Range(14, 25)] [SerializeField] private int youthMinAge = 16;
        [Range(14, 25)] [SerializeField] private int youthMaxAge = 18;

        [Header("Academy intake — youth that join each season beyond replacing retirees")]
        [Tooltip("How many academy youths join each season on top of replacements (0 disables it).")]
        [Range(0, 10)] [SerializeField] private int youthIntakePerSeason = 1;
        [Tooltip("The size the academy intake grows the squad toward and never pushes it past.")]
        [Range(1, 60)] [SerializeField] private int maxSquadSize = 25;

        [Header("Ordinary youth band — squad average + offset, held inside an absolute floor/ceiling")]
        [Tooltip("Lowest visible ability: squad average + this offset, then clamped to the floor/ceiling below.")]
        [Range(-60, 60)] [SerializeField] private int youthMinAbilityOffset = -25;
        [Range(1, 99)] [SerializeField] private byte youthMinAbilityFloor = 25;
        [Range(1, 99)] [SerializeField] private byte youthMinAbilityCeiling = 60;

        [Tooltip("Highest visible ability: squad average + this offset, then clamped to the floor/ceiling below.")]
        [Range(-60, 60)] [SerializeField] private int youthMaxAbilityOffset = -8;
        [Range(1, 99)] [SerializeField] private byte youthMaxAbilityFloor = 35;
        [Range(1, 99)] [SerializeField] private byte youthMaxAbilityCeiling = 72;

        [Tooltip("Lowest hidden potential: squad average + this offset, then clamped to the floor/ceiling below.")]
        [Range(-60, 60)] [SerializeField] private int youthMinPotentialOffset = -3;
        [Range(1, 99)] [SerializeField] private byte youthMinPotentialFloor = 45;
        [Range(1, 99)] [SerializeField] private byte youthMinPotentialCeiling = 85;

        [Tooltip("Highest hidden potential: squad average + this offset (the one positive offset — a prospect can outgrow today's first team).")]
        [Range(-60, 60)] [SerializeField] private int youthMaxPotentialOffset = 18;
        [Range(1, 99)] [SerializeField] private byte youthMaxPotentialFloor = 60;
        [Range(1, 99)] [SerializeField] private byte youthMaxPotentialCeiling = 95;

        [Tooltip("The average to assume when a squad has no players to average.")]
        [Range(1, 99)] [SerializeField] private int emptySquadAverageRating = 50;

        private void OnValidate()
        {
            ClampToValidRanges();
        }

        public RenewalSettings ToSettings()
        {
            ClampToValidRanges();
            return new RenewalSettings
            {
                KeeperTwilightAge = keeperTwilightAge,
                KeeperHardAge = keeperHardAge,
                OutfielderTwilightAge = outfielderTwilightAge,
                OutfielderHardAge = outfielderHardAge,
                RetirementRatingEase = retirementRatingEase,
                GemCadenceSeasons = gemCadenceSeasons,
                GemMinAbility = gemMinAbility,
                GemMaxAbility = gemMaxAbility,
                GemMinPotential = gemMinPotential,
                GemMaxPotential = gemMaxPotential,
                YouthMinAge = youthMinAge,
                YouthMaxAge = youthMaxAge,
                YouthIntakePerSeason = youthIntakePerSeason,
                MaxSquadSize = maxSquadSize,
                YouthMinAbilityOffset = youthMinAbilityOffset,
                YouthMinAbilityFloor = youthMinAbilityFloor,
                YouthMinAbilityCeiling = youthMinAbilityCeiling,
                YouthMaxAbilityOffset = youthMaxAbilityOffset,
                YouthMaxAbilityFloor = youthMaxAbilityFloor,
                YouthMaxAbilityCeiling = youthMaxAbilityCeiling,
                YouthMinPotentialOffset = youthMinPotentialOffset,
                YouthMinPotentialFloor = youthMinPotentialFloor,
                YouthMinPotentialCeiling = youthMinPotentialCeiling,
                YouthMaxPotentialOffset = youthMaxPotentialOffset,
                YouthMaxPotentialFloor = youthMaxPotentialFloor,
                YouthMaxPotentialCeiling = youthMaxPotentialCeiling,
                EmptySquadAverageRating = emptySquadAverageRating,
            };
        }

        /// <summary>
        /// Bounds the fields <c>[Range]</c> cannot reach (Unity's attribute only decorates float and
        /// int) and asserts the cross-field invariants no per-field bound can express (UNITY.md §8).
        /// Called from the mapper too, because the attribute constrains the Inspector, not code that
        /// builds the settings object from a stale asset. Every band is wider than the calibrated
        /// value it holds, so a shipping asset passes through untouched.
        /// </summary>
        private void ClampToValidRanges()
        {
            retirementRatingEase = Math.Clamp(retirementRatingEase, 0.0, 1.0);

            // Hard must sit past Twilight: the gap between them is the divisor of the retirement odds,
            // and an empty band is 0/0 — NaN, which reads as "never retires" and ages a squad forever
            // with nothing raised (CONVENTIONS §6).
            if (keeperHardAge <= keeperTwilightAge)
            {
                keeperHardAge = keeperTwilightAge + 1;
            }

            if (outfielderHardAge <= outfielderTwilightAge)
            {
                outfielderHardAge = outfielderTwilightAge + 1;
            }

            // The gem and youth pairs are drawn from as ranges; an inverted one is not a tuning choice.
            if (gemMaxAbility < gemMinAbility)
            {
                gemMaxAbility = gemMinAbility;
            }

            if (gemMaxPotential < gemMinPotential)
            {
                gemMaxPotential = gemMinPotential;
            }

            if (youthMaxAge < youthMinAge)
            {
                youthMaxAge = youthMinAge;
            }

            // Each youth-band edge is held between its own floor and ceiling. An inverted pair does not
            // throw anywhere — the clamp simply pins the edge to the floor and every academy prospect
            // in the game comes out on the same number, with nothing raised. Straighten it here.
            if (youthMinAbilityCeiling < youthMinAbilityFloor)
            {
                youthMinAbilityCeiling = youthMinAbilityFloor;
            }

            if (youthMaxAbilityCeiling < youthMaxAbilityFloor)
            {
                youthMaxAbilityCeiling = youthMaxAbilityFloor;
            }

            if (youthMinPotentialCeiling < youthMinPotentialFloor)
            {
                youthMinPotentialCeiling = youthMinPotentialFloor;
            }

            if (youthMaxPotentialCeiling < youthMaxPotentialFloor)
            {
                youthMaxPotentialCeiling = youthMaxPotentialFloor;
            }

            // The generator draws ability and potential as ranges too, so the *resulting* band must not
            // be inverted either: the max edge's bounds have to reach at least the min edge's.
            if (youthMaxAbilityCeiling < youthMinAbilityFloor)
            {
                youthMaxAbilityCeiling = youthMinAbilityFloor;
            }

            if (youthMaxPotentialCeiling < youthMinPotentialFloor)
            {
                youthMaxPotentialCeiling = youthMinPotentialFloor;
            }

            emptySquadAverageRating = Math.Clamp(emptySquadAverageRating, 1, 99);
        }
    }
}
