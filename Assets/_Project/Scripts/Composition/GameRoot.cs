using Gaffer.Application.Run;
using Gaffer.Common;
using Gaffer.Common.Localization;
using Gaffer.Infrastructure.Localization;
using Gaffer.Infrastructure.Configuration;
using Gaffer.Presentation;
using Gaffer.Presentation.Shell;
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

        [Header("Look")]
        [Tooltip("Gaffer.uss — the theme. REQUIRED: without it every var() resolves to nothing and the " +
                 "screen falls back to Unity's default runtime look (a blue panel and black text).")]
        [SerializeField] private StyleSheet _theme;

        [Tooltip("Which language the screens speak. Leave as the reference locale while iterating; " +
                 "switching it is how the Turkish copy gets reviewed without a code change.")]
        [SerializeField] private string _locale = Locales.Reference;

        [Tooltip("Authored string table. Empty falls back to the built-in copy, which is the floor rather " +
                 "than a sync source (ContentAssets).")]
        [SerializeField] private StringTableSO _strings;

        [Header("Balance (optional — empty boots on the calibrated defaults)")]
        [SerializeField] private SimulationBalanceSO _simulation;
        [SerializeField] private DevelopmentBalanceSO _development;
        [SerializeField] private EconomyBalanceSO _economy;
        [SerializeField] private ScoutingBalanceSO _scouting;

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

            // The sheet is attached HERE rather than inside the screen: which stylesheet is in force is a
            // wiring fact, and Presentation deciding it for itself would make the look impossible to swap
            // from the composition root (ARCHITECTURE §6).
            VisualElement root = document.rootVisualElement;
            if (_theme != null)
            {
                root.styleSheets.Add(_theme);
            }
            else
            {
                // Loud, because the failure is silent otherwise: everything still draws, just in Unity's
                // default theme, which reads as "the art is wrong" rather than "the sheet is missing".
                Debug.LogError("GameRoot: no theme stylesheet assigned. Drag Assets/_Project/UI/Theme/Gaffer.uss "
                    + "onto the Theme field, or the screen will draw in Unity's default runtime look.");
            }

            new GameShell(started.Value, root, BuildText()).Build();
        }

        // The words, bound to one locale. Composition's job precisely: Presentation may not see the
        // string table's assembly, and a screen that chose its own locale could not be switched from here.
        private LocalizedStrings BuildText()
        {
            StringTable table = _strings != null ? _strings.ToTable(UiTextKeys.All) : GameStrings.Default;
            return new LocalizedStrings(table, string.IsNullOrEmpty(_locale) ? Locales.Reference : _locale);
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

        // One asset, four bundles: SimulationBalanceSO authors the chance model, the tactical steps, the
        // scorer weights AND the out-of-position penalty, and only the first was being read here — so every
        // tactics or attribution number tuned in the Inspector reached the editor windows (which wire all
        // four) and silently did not reach the game. A config asset half-read is worse than none: it looks
        // authored and behaves default.
        private RunBalance BuildBalance()
        {
            return new RunBalance(
                simulation: _simulation != null ? _simulation.ToSettings() : null,
                tacticsBalance: _simulation != null ? _simulation.ToTacticsSettings() : null,
                positionalFit: _simulation != null ? _simulation.ToPositionalFitSettings() : null,
                scorer: _simulation != null ? _simulation.ToScorerWeights() : null,
                development: _development != null ? _development.ToSettings() : null,
                economy: _economy != null ? _economy.ToSettings() : null,
                scouting: _scouting != null ? _scouting.ToSettings() : null);
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
