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
        [SerializeField] private int keeperPeakAge = 34;
        [SerializeField] private int centralPeakAge = 32;
        [SerializeField] private int widePeakAge = 31;
        [SerializeField] private int forwardPeakAge = 30;
        [SerializeField] private int minDeclineAge = 30;

        [Header("Decline — ability lost per season past the peak")]
        [SerializeField] private double declinePerYear = 0.9;
        [SerializeField] private int maxDeclineYears = 6;
        [SerializeField] private double generalDeclineFactor = 0.6;

        [Header("Floors")]
        [SerializeField] private byte attributeFloor = 25;
        [SerializeField] private byte physicalFloor = 15;

        public DevelopmentSettings ToSettings()
        {
            return new DevelopmentSettings
            {
                GrowthRateTo20 = growthRateTo20,
                GrowthRateTo22 = growthRateTo22,
                GrowthRateTo24 = growthRateTo24,
                GrowthRateTo26 = growthRateTo26,
                GrowthRateTo29 = growthRateTo29,
                MinSeasonVariance = minSeasonVariance,
                MaxSeasonVariance = maxSeasonVariance,
                KeeperPeakAge = keeperPeakAge,
                CentralPeakAge = centralPeakAge,
                WidePeakAge = widePeakAge,
                ForwardPeakAge = forwardPeakAge,
                MinDeclineAge = minDeclineAge,
                DeclinePerYear = declinePerYear,
                MaxDeclineYears = maxDeclineYears,
                GeneralDeclineFactor = generalDeclineFactor,
                AttributeFloor = attributeFloor,
                PhysicalFloor = physicalFloor,
            };
        }
    }
}
