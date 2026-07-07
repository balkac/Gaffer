namespace Gaffer.Application.Season
{
    /// <summary>
    /// The board's objective for the season, as final-position thresholds: finish at or above
    /// <see cref="PromotionPosition"/> to rise, at or above <see cref="SurvivalPosition"/> to keep the
    /// job, otherwise sacked (GDD §2). Positions are 1-based.
    /// </summary>
    public readonly struct BoardTarget
    {
        public BoardTarget(int promotionPosition, int survivalPosition)
        {
            PromotionPosition = promotionPosition;
            SurvivalPosition = survivalPosition;
        }

        public int PromotionPosition { get; }

        public int SurvivalPosition { get; }
    }
}
