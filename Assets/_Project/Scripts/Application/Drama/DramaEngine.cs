using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Application.Transfers;
using Gaffer.Common;
using Gaffer.Domain.Drama;
using Gaffer.Domain.Players;
using Gaffer.Domain.Traits;

namespace Gaffer.Application.Drama
{
    /// <summary>
    /// The weekly drama loop (TDD §8): filter the catalog against this week's state, weight the
    /// candidates by base weight and trait bias, enforce scarcity (season budget, minimum gap, per
    /// -event cooldown, once-per-run), and let the injected rng pick — then put a decision in front
    /// of the manager and, on the answer, apply the effects. The three GDD §4.7 rules are enforced
    /// here structurally: consequential (effects change morale/cash/squad), decided (a choice index
    /// is required), rare (the envelope above). Deterministic: same state, same rng stream, same drama.
    /// Stateful per run — cooldowns and the season budget live here; call <see cref="StartSeason"/>
    /// at each rollover.
    /// </summary>
    public sealed class DramaEngine
    {
        private readonly DramaCatalog _catalog;
        private readonly DramaSettings _settings;

        private readonly Dictionary<DramaEventId, int> _lastFiredWeek = new Dictionary<DramaEventId, int>();
        private readonly HashSet<DramaEventId> _firedEver = new HashSet<DramaEventId>();

        // Scratch buffer for the weekly candidate collection, reused across ticks (cleared per call)
        // so a quiet week allocates nothing (PERFORMANCE §8). Valid until the next TickWeek call.
        private readonly List<Candidate> _candidateScratch = new List<Candidate>(16);
        private int _week;
        // Far enough back that the first week is never gap-blocked, small enough that the subtraction
        // in the gap check can never overflow.
        private int _lastFiredAnyWeek = -1_000_000;
        private int _firedThisSeason;

        public DramaEngine()
            : this(DramaCatalog.Default, DramaSettings.Default)
        {
        }

        public DramaEngine(DramaCatalog catalog, DramaSettings settings)
        {
            _catalog = catalog;
            _settings = settings;
        }

        /// <summary>Resets the season budget (cooldowns and once-per-run marks persist across seasons).</summary>
        public void StartSeason()
        {
            _firedThisSeason = 0;
        }

        /// <summary>
        /// Advances the engine one week and maybe raises an event. Null means a quiet week — by
        /// design the common case. The rng draw order is fixed (fire roll, then pick roll), so a
        /// seeded stream reproduces the same drama.
        /// </summary>
        public PendingDrama TickWeek(DramaWeekContext context, IRandom rng)
        {
            _week++;

            if (_firedThisSeason >= _settings.MaxEventsPerSeason)
            {
                return null;
            }

            if (_week - _lastFiredAnyWeek < _settings.MinWeeksBetweenEvents)
            {
                return null;
            }

            List<Candidate> candidates = CollectCandidates(context);
            if (candidates.Count == 0)
            {
                return null;
            }

            // The firing chance scales with the total candidate weight, so a trait bias changes how
            // often its event happens, not merely which candidate wins the pick.
            double totalWeight = 0.0;
            foreach (Candidate candidate in candidates)
            {
                totalWeight += candidate.Weight;
            }

            double fireChance = _settings.WeeklyChancePerWeight * totalWeight;
            if (fireChance > _settings.MaxWeeklyChance)
            {
                fireChance = _settings.MaxWeeklyChance;
            }

            if (rng.NextDouble() >= fireChance)
            {
                return null;
            }

            Candidate picked = PickWeighted(candidates, rng.NextDouble());
            _lastFiredWeek[picked.Event.Id] = _week;
            _firedEver.Add(picked.Event.Id);
            _lastFiredAnyWeek = _week;
            _firedThisSeason++;

            return new PendingDrama(picked.Event, picked.Subject, context);
        }

        /// <summary>
        /// Applies the chosen answer: morale effects land on the given ledger now; cash and any
        /// forced sale come back in the outcome for their owners. <paramref name="currentCash"/>
        /// feeds fraction-of-cash effects (a budget cut scales to the club).
        /// </summary>
        public Result<DramaOutcome> Resolve(PendingDrama pending, int choiceIndex, MoraleLedger morale, long currentCash = 0)
        {
            if (pending == null)
            {
                return Result<DramaOutcome>.Failure("There is no pending drama to resolve.");
            }

            if (choiceIndex < 0 || choiceIndex >= pending.Event.Choices.Count)
            {
                return Result<DramaOutcome>.Failure(
                    $"Choice {choiceIndex} is out of range for event '{pending.Event.Id.Value}' with {pending.Event.Choices.Count} choices.");
            }

            DramaChoice choice = pending.Event.Choices[choiceIndex];
            double cashDelta = 0.0;
            Player playerToSell = null;
            Player traitGrantTarget = null;
            TraitId grantedTrait = default;

            foreach (DramaEffect effect in choice.Effects)
            {
                switch (effect.Kind)
                {
                    case DramaEffectKind.SubjectMorale:
                        morale.Apply(pending.Subject.Id, effect.Magnitude, effect.DurationWeeks);
                        break;
                    case DramaEffectKind.TeamMorale:
                        foreach (Player player in pending.Context.Squad)
                        {
                            morale.Apply(player.Id, effect.Magnitude, effect.DurationWeeks);
                        }

                        break;
                    case DramaEffectKind.Cash:
                        cashDelta += effect.Magnitude;
                        break;
                    case DramaEffectKind.CashFraction:
                        cashDelta += currentCash * effect.Magnitude;
                        break;
                    case DramaEffectKind.SubjectWageFine:
                        cashDelta += PlayerWage.Weekly(pending.Subject);
                        break;
                    case DramaEffectKind.SellSubject:
                        playerToSell = pending.Subject;
                        break;
                    case DramaEffectKind.GrantTraitToSuccessor:
                        traitGrantTarget = Successor(pending);
                        grantedTrait = traitGrantTarget != null ? effect.Trait : default;
                        break;
                }
            }

            return Result<DramaOutcome>.Success(
                new DramaOutcome(pending.Event.Id, choiceIndex, (long)cashDelta, playerToSell, traitGrantTarget, grantedTrait));
        }

        // The heir apparent when a captain anoints a successor: the strongest under-24 teammate, or —
        // in an old squad — the strongest teammate of any age. Deterministic (ties break to the lower
        // id), never the subject himself; null in a one-man room.
        private static Player Successor(PendingDrama pending)
        {
            Player best = null;
            double bestRating = double.MinValue;
            bool bestIsYoung = false;
            foreach (Player player in pending.Context.Squad)
            {
                if (pending.Subject != null && player.Id == pending.Subject.Id)
                {
                    continue;
                }

                bool young = player.Age < 24;
                double rating = PlayerRatings.ForRole(player);

                // Youth outranks rating; within the same bracket the higher rating wins, then the lower id.
                bool better = best == null
                    || (young && !bestIsYoung)
                    || (young == bestIsYoung
                        && (rating > bestRating || (rating == bestRating && player.Id.Value < best.Id.Value)));
                if (better)
                {
                    best = player;
                    bestRating = rating;
                    bestIsYoung = young;
                }
            }

            return best;
        }

        private readonly struct Candidate
        {
            public Candidate(DramaEvent dramaEvent, Player subject, double weight)
            {
                Event = dramaEvent;
                Subject = subject;
                Weight = weight;
            }

            public DramaEvent Event { get; }

            public Player Subject { get; }

            public double Weight { get; }
        }

        private List<Candidate> CollectCandidates(DramaWeekContext context)
        {
            List<Candidate> candidates = _candidateScratch;
            candidates.Clear();
            IReadOnlyList<DramaEvent> events = _catalog.Events;
            IReadOnlyList<Player> squad = context.Squad;
            for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
            {
                DramaEvent dramaEvent = events[eventIndex];
                if (dramaEvent.OncePerRun && _firedEver.Contains(dramaEvent.Id))
                {
                    continue;
                }

                if (_lastFiredWeek.TryGetValue(dramaEvent.Id, out int lastWeek)
                    && _week - lastWeek < dramaEvent.CooldownWeeks)
                {
                    continue;
                }

                if (!ClubConditionsMet(dramaEvent.Trigger, context))
                {
                    continue;
                }

                double squadBias = SquadBias(dramaEvent, squad);
                if (!dramaEvent.RequiresSubject)
                {
                    candidates.Add(new Candidate(dramaEvent, null, dramaEvent.BaseWeight * squadBias));
                    continue;
                }

                for (int playerIndex = 0; playerIndex < squad.Count; playerIndex++)
                {
                    Player player = squad[playerIndex];
                    if (!SubjectMatches(dramaEvent.Trigger, player, context))
                    {
                        continue;
                    }

                    double weight = dramaEvent.BaseWeight * squadBias * SubjectBias(dramaEvent, player);
                    candidates.Add(new Candidate(dramaEvent, player, weight));
                }
            }

            return candidates;
        }

        private static bool ClubConditionsMet(DramaTrigger trigger, in DramaWeekContext context)
        {
            if (trigger.MinLossStreak > 0 && context.LossStreak < trigger.MinLossStreak)
            {
                return false;
            }

            if (trigger.MinTablePosition > 0 && context.TablePosition < trigger.MinTablePosition)
            {
                return false;
            }

            if (trigger.RequiresOpenWindow && !context.IsWindowOpen)
            {
                return false;
            }

            return true;
        }

        private static bool SubjectMatches(DramaTrigger trigger, Player player, in DramaWeekContext context)
        {
            if (trigger.MaxSubjectAge > 0 && player.Age > trigger.MaxSubjectAge)
            {
                return false;
            }

            if (trigger.MinSubjectAge > 0 && player.Age < trigger.MinSubjectAge)
            {
                return false;
            }

            double rating = PlayerRatings.ForRole(player);
            if (trigger.MinSubjectRating > 0.0 && rating < trigger.MinSubjectRating)
            {
                return false;
            }

            if (trigger.MinSubjectPotentialGap > 0.0 && player.HiddenPotential - rating < trigger.MinSubjectPotentialGap)
            {
                return false;
            }

            if (trigger.SubjectBenched && (context.Starters == null || IsStarter(player, context.Starters)))
            {
                return false;
            }

            if (trigger.RequiredSubjectTrait.Value != null && !Carries(player, trigger.RequiredSubjectTrait))
            {
                return false;
            }

            return true;
        }

        private static bool IsStarter(Player player, IReadOnlyList<Player> starters)
        {
            for (int i = 0; i < starters.Count; i++)
            {
                if (starters[i].Id == player.Id)
                {
                    return true;
                }
            }

            return false;
        }

        private static double SubjectBias(DramaEvent dramaEvent, Player player)
        {
            double bias = 1.0;
            IReadOnlyList<DramaTraitBias> biases = dramaEvent.SubjectTraitBiases;
            for (int i = 0; i < biases.Count; i++)
            {
                if (Carries(player, biases[i].Trait))
                {
                    bias *= biases[i].WeightMultiplier;
                }
            }

            return bias;
        }

        private static double SquadBias(DramaEvent dramaEvent, IReadOnlyList<Player> squad)
        {
            double bias = 1.0;
            IReadOnlyList<DramaTraitBias> biases = dramaEvent.SquadTraitBiases;
            for (int i = 0; i < biases.Count; i++)
            {
                for (int playerIndex = 0; playerIndex < squad.Count; playerIndex++)
                {
                    if (Carries(squad[playerIndex], biases[i].Trait))
                    {
                        bias *= biases[i].WeightMultiplier;
                        break;
                    }
                }
            }

            return bias;
        }

        private static bool Carries(Player player, TraitId trait)
        {
            IReadOnlyList<TraitId> traits = player.Traits;
            for (int i = 0; i < traits.Count; i++)
            {
                if (traits[i] == trait)
                {
                    return true;
                }
            }

            return false;
        }

        private static Candidate PickWeighted(List<Candidate> candidates, double roll)
        {
            double total = 0.0;
            foreach (Candidate candidate in candidates)
            {
                total += candidate.Weight;
            }

            double target = roll * total;
            double cumulative = 0.0;
            foreach (Candidate candidate in candidates)
            {
                cumulative += candidate.Weight;
                if (target < cumulative)
                {
                    return candidate;
                }
            }

            return candidates[candidates.Count - 1];
        }
    }
}
