using Gaffer.Domain.Clubs;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using UnityEngine;

namespace Gaffer.Composition
{
    /// <summary>
    /// A throwaway in-editor smoke test: on Play it simulates one match on the pure Application core
    /// and logs the result to the Console — proof the deterministic sim runs inside Unity's runtime,
    /// not just under dotnet. Not shipped; the real matchday flow arrives with Presentation (Faz 7).
    /// </summary>
    public sealed class MatchSmokeTest : MonoBehaviour
    {
        [SerializeField]
        private ulong _seed = 1234UL;

        private void Start()
        {
            var simulator = new MatchSimulator(
                new PoissonChanceGenerator(MatchSimulationSettings.Default),
                new QualityChanceResolver());

            var command = new MatchCommand(
                new TeamStrength(70.0, 68.0, 66.0),
                new TeamStrength(55.0, 58.0, 60.0),
                new MatchContext(MatchImportance.Derby, 40000, isTitleDecider: true, isRivalry: true));

            MatchOutcome outcome = simulator.Simulate(command, new SplitMix64RandomNumberGenerator(_seed));

            Debug.Log($"[Gaffer] Full time  {outcome.HomeGoals}-{outcome.AwayGoals}  " +
                      $"({outcome.Events.Count} goals, seed {_seed})");
            foreach (MatchEvent goal in outcome.Events)
            {
                Debug.Log($"[Gaffer]  {goal.Minute}'  {goal.Side} scores");
            }
        }
    }
}
