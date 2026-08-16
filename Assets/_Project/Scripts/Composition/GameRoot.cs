using Gaffer.Application.Run;
using Gaffer.Common;
using Gaffer.Infrastructure.Configuration;
using Gaffer.Presentation.Squad;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaffer.Composition
{
    /// <summary>
    /// Starts a run and hands it to the screen. The composition root for PLAY, as opposed to
    /// <see cref="GameBootstrapper"/>, which owns process-wide startup facts and nothing else.
    ///
    /// <para><b>It wires and then gets out of the way</b> (ARCHITECTURE §6). It builds the run's balance
    /// out of the config assets, opens a <see cref="RunSession"/>, constructs the screen against the
    /// document's root, and stops. It handles no input and holds no game state — a composition root that
    /// grew an event handler would have become a controller with a misleading name.</para>
    ///
    /// <para><b>Why the wiring lives here and not in Presentation.</b> The screen needs balance from
    /// <c>Infrastructure</c> (ScriptableObjects) and words from its string table, and Presentation's
    /// assembly deliberately references neither — it may not, or the layer arrows would stop pointing
    /// inwards. Composition is the only assembly that can see both sides, which is exactly its job.</para>
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class GameRoot : MonoBehaviour
    {
        [Header("The world this run is generated from")]
        [Tooltip("Same seed, same world, same season — every time. Change it for a different run.")]
        [SerializeField] private string _seed = "20260816";

        [Range(4, 24)] [SerializeField] private int _clubs = 20;
        [Range(0, 23)] [SerializeField] private int _managedClubIndex = 7;

        [Tooltip("How many unattached players the world holds. 50,000 is the scale target; start smaller " +
                 "while iterating on the screen and raise it to check the lists still fly.")]
        [SerializeField] private int _marketSize = 2000;

        [Header("Money to start on")]
        [SerializeField] private long _startingCash = 20_000_000L;
        [SerializeField] private long _weeklyWageBudget = 900_000L;

        [Header("Balance (optional — empty boots on the calibrated defaults)")]
        [SerializeField] private SimulationBalanceSO _simulation;
        [SerializeField] private DevelopmentBalanceSO _development;
        [SerializeField] private EconomyBalanceSO _economy;

        private void Start()
        {
            var document = GetComponent<UIDocument>();
            if (document.rootVisualElement == null)
            {
                Debug.LogError("GameRoot: the UIDocument has no root. Assign a PanelSettings asset to it.");
                return;
            }

            Result<RunSession> started = RunSessionFactory.Start(BuildSetup(), BuildBalance());
            if (started.IsFailure)
            {
                // A run that will not start is a wiring or a settings problem, and the message says which.
                // Reported rather than thrown: the player sees a screen that explains itself, not a stack.
                Debug.LogError("GameRoot: could not start a run — " + started.Error);
                return;
            }

            new SquadScreen(started.Value, document.rootVisualElement).Build();
        }

        private RunSetup BuildSetup()
        {
            return new RunSetup(
                teamCount: _clubs,
                seed: SeedValue(),
                managedClubIndex: _managedClubIndex,
                promotionPosition: 2,
                survivalPosition: _clubs - 3,
                startingCash: _startingCash,
                weeklyWageBudget: _weeklyWageBudget,
                marketSize: _marketSize,
                guaranteedGems: 3);
        }

        private RunBalance BuildBalance()
        {
            return new RunBalance(
                simulation: _simulation != null ? _simulation.ToSettings() : null,
                development: _development != null ? _development.ToSettings() : null,
                economy: _economy != null ? _economy.ToSettings() : null);
        }

        /// <summary>
        /// The seed, as a number. Typed as text in the Inspector on purpose: a run seed is a
        /// <c>ulong</c> and Unity has no field for one, so an int field would silently halve the space a
        /// player can type. Anything unparseable falls back to a fixed seed rather than to a clock —
        /// nothing in this game may read a clock, or a run would stop being reproducible
        /// (NON-NEGOTIABLE #2).
        /// </summary>
        private ulong SeedValue()
        {
            return ulong.TryParse(_seed, out ulong parsed) ? parsed : 20260816UL;
        }
    }
}
