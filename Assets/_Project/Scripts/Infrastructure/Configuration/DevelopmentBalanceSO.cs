using System;
using Gaffer.Application.Progression;
using UnityEngine;

namespace Gaffer.Infrastructure.Configuration
{
    /// <summary>
    /// The Unity authoring surface for player-development balance (NON-NEGOTIABLE #3): tune the numbers in the
    /// Inspector, and <see cref="ToSettings"/> maps them to the pure <see cref="DevelopmentSettings"/> the core
    /// reads. A config-as-override (ARCHITECTURE §7): assign an asset to shift the curves; assign none and the
    /// core falls back to <see cref="DevelopmentSettings.Default"/>. The defaults here mirror that calibrated
    /// baseline, so a fresh asset behaves exactly like shipping until you edit it.
    /// </summary>
    [CreateAssetMenu(menuName = "Gaffer/Balance/Development", fileName = "DevelopmentBalance")]
    public sealed class DevelopmentBalanceSO : ScriptableObject
    {
        [Header("Growth — fraction of the remaining ability gap closed per season, by age band")]
        [SerializeField] private double growthRateTo20 = 0.14;
        [SerializeField] private double growthRateTo22 = 0.10;
        [SerializeField] private double growthRateTo24 = 0.07;
        [SerializeField] private double growthRateTo26 = 0.045;
        [SerializeField] private double growthRateTo29 = 0.02;

        [Header("Season variance — per-season multiplier on growth/decline (centred on 1)")]
        [SerializeField] private double minSeasonVariance = 0.6;
        [SerializeField] private double maxSeasonVariance = 1.4;

        [Header("Peak age by role — when decline sets in (keepers latest, forwards first)")]
        [Range(16, 50)] [SerializeField] private int keeperPeakAge = 34;
        [Range(16, 50)] [SerializeField] private int centralPeakAge = 32;
        [Range(16, 50)] [SerializeField] private int widePeakAge = 31;
        [Range(16, 50)] [SerializeField] private int forwardPeakAge = 30;
        [Range(16, 50)] [SerializeField] private int minDeclineAge = 30;

        [Header("Decline — ability lost per season past the peak")]
        [SerializeField] private double declinePerYear = 0.9;
        [Range(0, 40)] [SerializeField] private int maxDeclineYears = 6;
        [SerializeField] private double generalDeclineFactor = 0.6;

        [Header("Floors")]
        [Range(1, 99)] [SerializeField] private byte attributeFloor = 25;
        [Range(1, 99)] [SerializeField] private byte physicalFloor = 15;

        private void OnValidate()
        {
            ClampToValidRanges();
        }

        public DevelopmentSettings ToSettings()
        {
            ClampToValidRanges();
            return new DevelopmentSettings(
                growthRateTo20: growthRateTo20,
                growthRateTo22: growthRateTo22,
                growthRateTo24: growthRateTo24,
                growthRateTo26: growthRateTo26,
                growthRateTo29: growthRateTo29,
                minSeasonVariance: minSeasonVariance,
                maxSeasonVariance: maxSeasonVariance,
                keeperPeakAge: keeperPeakAge,
                centralPeakAge: centralPeakAge,
                widePeakAge: widePeakAge,
                forwardPeakAge: forwardPeakAge,
                minDeclineAge: minDeclineAge,
                declinePerYear: declinePerYear,
                maxDeclineYears: maxDeclineYears,
                generalDeclineFactor: generalDeclineFactor,
                attributeFloor: attributeFloor,
                physicalFloor: physicalFloor);
        }

        /// <summary>
        /// Holds the serialized numbers inside the bands the development curves can make sense of, and
        /// asserts the one invariant no per-field bound can (UNITY.md §8). Unity's <c>[Range]</c> only
        /// decorates float and int, so the <c>double</c> fields are bounded here instead — and the
        /// mappers call it too, because the attribute constrains the Inspector, not code that builds
        /// the settings object from a stale asset. Every band is wider than the calibrated value it
        /// holds, so a shipping asset passes through untouched.
        /// </summary>
        private void ClampToValidRanges()
        {
            growthRateTo20 = Math.Clamp(growthRateTo20, 0.0, 1.0);
            growthRateTo22 = Math.Clamp(growthRateTo22, 0.0, 1.0);
            growthRateTo24 = Math.Clamp(growthRateTo24, 0.0, 1.0);
            growthRateTo26 = Math.Clamp(growthRateTo26, 0.0, 1.0);
            growthRateTo29 = Math.Clamp(growthRateTo29, 0.0, 1.0);

            minSeasonVariance = Math.Clamp(minSeasonVariance, 0.0, 5.0);
            maxSeasonVariance = Math.Clamp(maxSeasonVariance, 0.0, 5.0);

            // The pair is drawn from as a range: an inverted one would hand the growth roll a negative
            // width and quietly invert every season's variance, with nothing raised.
            if (maxSeasonVariance < minSeasonVariance)
            {
                maxSeasonVariance = minSeasonVariance;
            }

            declinePerYear = Math.Clamp(declinePerYear, 0.0, 10.0);
            generalDeclineFactor = Math.Clamp(generalDeclineFactor, 0.0, 5.0);
        }
    }
}
