using Gaffer.Application.Season;
using UnityEngine;

namespace Gaffer.Infrastructure.Configuration
{
    /// <summary>
    /// The Unity authoring surface for squad-renewal balance (NON-NEGOTIABLE #3): retirement ages, how a high
    /// rating stays a player, the academy-gem cadence and its hidden band, and the youth intake age. Tune it
    /// in the Inspector and <see cref="ToSettings"/> maps to the pure <see cref="RenewalSettings"/>.
    /// Config-as-override — no asset means <see cref="RenewalSettings.Default"/>. Defaults mirror it.
    /// </summary>
    [CreateAssetMenu(menuName = "Gaffer/Balance/Renewal", fileName = "RenewalBalance")]
    public sealed class RenewalBalanceSO : ScriptableObject
    {
        [Header("Retirement age thresholds (no one plays past Hard; odds climb through the twilight years)")]
        [SerializeField] private int keeperTwilightAge = 36;
        [SerializeField] private int keeperHardAge = 43;
        [SerializeField] private int outfielderTwilightAge = 33;
        [SerializeField] private int outfielderHardAge = 40;

        [Tooltip("How much a high rating eases retirement in the twilight years (0 = age only).")]
        [SerializeField] private double retirementRatingEase = 0.4;

        [Header("Academy gem — a rare guaranteed cadence, never a per-player chance")]
        [Tooltip("How often, in seasons, a club's academy yields a hidden gem.")]
        [SerializeField] private int gemCadenceSeasons = 5;
        [SerializeField] private byte gemMinAbility = 28;
        [SerializeField] private byte gemMaxAbility = 46;
        [SerializeField] private byte gemMinPotential = 86;
        [SerializeField] private byte gemMaxPotential = 96;

        [Header("Youth intake age range")]
        [SerializeField] private int youthMinAge = 16;
        [SerializeField] private int youthMaxAge = 18;

        public RenewalSettings ToSettings()
        {
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
            };
        }
    }
}
