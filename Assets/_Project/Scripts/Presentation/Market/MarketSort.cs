namespace Gaffer.Presentation.Market
{
    /// <summary>The orders a manager reads the market in. Each is a question: who is best, who is
    /// youngest, who is cheapest — and ties always fall back to best, because that is the question
    /// underneath the other two.</summary>
    public enum MarketSort
    {
        Rating = 0,
        Age = 1,
        Fee = 2,
    }
}
