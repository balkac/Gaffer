namespace Gaffer.Presentation.Market
{
    /// <summary>
    /// The three ages a manager shops by. Bands rather than a slider, because on a phone a slider is two
    /// fiddly thumbs and a band is one tap — and because the bands ARE the questions: a prospect to grow, a
    /// man at his peak, a veteran for a season. The edges are the scouting trade's own: 21 is where a
    /// "young player" stops being one in most competitions, 29 where a fee stops being an investment.
    /// </summary>
    public enum AgeBand
    {
        All = 0,
        Under22 = 1,
        Prime = 2,
        Veteran = 3,
    }
}
