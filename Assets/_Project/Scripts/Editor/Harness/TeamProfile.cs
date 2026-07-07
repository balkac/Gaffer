using Gaffer.Application.Simulation;

namespace Gaffer.Editor.Harness
{
    /// <summary>A league team: its pre-season quality rank (0 = strongest) and its match strength.</summary>
    public sealed class TeamProfile
    {
        public TeamProfile(int rank, string name, double baseQuality, TeamStrength strength)
        {
            Rank = rank;
            Name = name;
            BaseQuality = baseQuality;
            Strength = strength;
        }

        public int Rank { get; }

        public string Name { get; }

        public double BaseQuality { get; }

        public TeamStrength Strength { get; }
    }
}
