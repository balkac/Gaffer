namespace Gaffer.Domain.Players
{
    /// <summary>
    /// One row of a role's makeup: an attribute and how much of the role's rating it carries. A
    /// <c>readonly struct</c> so a whole table lives inline in one array and reading it on the sim hot
    /// path allocates nothing (PERFORMANCE §4, §8).
    /// </summary>
    public readonly struct RoleAttributeWeight
    {
        public RoleAttributeWeight(PlayerAttribute attribute, double weight)
        {
            Attribute = attribute;
            Weight = weight;
        }

        public PlayerAttribute Attribute { get; }

        /// <summary>This attribute's share of the role rating; a role's weights sum to 1.</summary>
        public double Weight { get; }
    }
}
