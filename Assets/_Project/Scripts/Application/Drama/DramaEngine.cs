using System;
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
    /// The weekly drama loop (TDD §8): filter the catalog against this week's state, weight ONE
    /// candidate per surviving event by base weight and trait bias, enforce scarcity (a chance that
    /// answers to the run, minimum gap, per-event cooldown, once-per-run, season backstop), let the
    /// injected rng pick the event and then its subject — then put a decision in front
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
        private readonly EconomySettings _economy;

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
            : this(catalog, settings, null)
        {
        }

        /// <summary>Also takes economy balance (from a config asset) — the wage-fine effect prices a
        /// week's wage through it. Null falls back to the calibrated defaults.</summary>
        public DramaEngine(DramaCatalog catalog, DramaSettings settings, EconomySettings economy)
        {
            _catalog = catalog;
            _settings = settings;
            _economy = economy ?? EconomySettings.Default;
        }

        /// <summary>Resets the season budget (cooldowns and once-per-run marks persist across seasons).</summary>
        public void StartSeason()
        {
            _firedThisSeason = 0;
        }

        /// <summary>
        /// The engine's memory as data, for a save (schema v6). Everything that makes drama rare is in
        /// here; nothing that is balance or catalog is, because config is not serialized and definitions
        /// rebind by id on load (TDD §10).
        /// </summary>
        public DramaEngineState CaptureState()
        {
            var marks = new List<DramaEventMark>(_lastFiredWeek.Count);
            foreach (KeyValuePair<DramaEventId, int> fired in _lastFiredWeek)
            {
                marks.Add(new DramaEventMark(fired.Key, fired.Value));
            }

            return new DramaEngineState(_week, _lastFiredAnyWeek, _firedThisSeason, marks);
        }

        /// <summary>
        /// Puts a saved memory back, replacing whatever this engine had. An id the current catalog no
        /// longer defines is kept rather than dropped — the mark costs one dictionary entry, and dropping
        /// it would silently clear the cooldown of an event a later build (or a re-enabled asset) brings
        /// back. That is the tolerant posture save data takes (ARCHITECTURE §11); it is the opposite of
        /// what <see cref="DramaCatalog.ValidateAgainst"/> does to authored content, and both are correct.
        /// </summary>
        public void RestoreState(DramaEngineState state)
        {
            if (state == null)
            {
                return;
            }

            _lastFiredWeek.Clear();
            _firedEver.Clear();

            IReadOnlyList<DramaEventMark> marks = state.Events;
            if (marks != null)
            {
                for (int i = 0; i < marks.Count; i++)
                {
                    DramaEventMark mark = marks[i];
                    _lastFiredWeek[mark.Event] = mark.LastFiredWeek;
                    _firedEver.Add(mark.Event);
                }
            }

            _week = state.Week;
            _lastFiredAnyWeek = state.LastFiredWeek;
            _firedThisSeason = state.FiredThisSeason;
        }

        /// <summary>
        /// Advances the engine one week and maybe raises an event. Null means a quiet week — by
        /// design the common case. The rng draw order is fixed (fire roll, then the event pick, then
        /// the subject pick for an event that needs one), so a seeded stream reproduces the same drama.
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
            for (int i = 0; i < candidates.Count; i++)
            {
                totalWeight += candidates[i].Weight;
            }

            double fireChance = _settings.WeeklyChancePerWeight * totalWeight * CrisisMultiplier(context);
            if (fireChance > _settings.MaxWeeklyChance)
            {
                fireChance = _settings.MaxWeeklyChance;
            }

            if (rng.NextDouble() >= fireChance)
            {
                return null;
            }

            DramaEvent picked = PickWeighted(candidates, rng.NextDouble(), totalWeight);

            // The second draw (defect fix, PROGRESS "Gece kulübü skandalı"): the event won on its own
            // weight, and only now does the engine ask WHO it happens to, weighted by each eligible
            // player's subject bias. That split is what stops a wide filter from buying frequency.
            Player subject = null;
            if (picked.RequiresSubject)
            {
                subject = PickSubject(picked, context, rng.NextDouble());
                if (subject == null)
                {
                    // Unreachable: an event that needs a subject only becomes a candidate when at least
                    // one player fits it, and nothing between that check and here changes the squad. Kept
                    // as a silent quiet week rather than a null-subject PendingDrama the resolver would
                    // dereference — and deliberately without consuming budget, gap or cooldown.
                    return null;
                }
            }

            _lastFiredWeek[picked.Id] = _week;
            _firedEver.Add(picked.Id);
            _lastFiredAnyWeek = _week;
            _firedThisSeason++;

            return new PendingDrama(picked, subject, context);
        }

        /// <summary>
        /// How much this week's state multiplies the calm firing chance — the dial that makes drama
        /// answer to the run rather than arrive on a metronome. Pressure ramps linearly with the losing
        /// streak, and sitting in the drop zone weighs as much as one more defeat, so a comfortable
        /// mid-table season stays quiet while a collapse gets loud. Pure arithmetic on the snapshot: no
        /// allocation, and the same state always gives the same multiplier.
        /// </summary>
        private double CrisisMultiplier(in DramaWeekContext context)
        {
            if (_settings.CrisisLossStreak <= 0)
            {
                return 1.0;
            }

            double defeats = context.LossStreak;
            if (_settings.CrisisTablePosition > 0
                && context.TablePosition >= _settings.CrisisTablePosition)
            {
                defeats += 1.0;
            }

            double pressure = defeats / _settings.CrisisLossStreak;
            if (pressure <= 0.0)
            {
                return 1.0;
            }

            if (pressure > 1.0)
            {
                pressure = 1.0;
            }

            return 1.0 + ((_settings.CrisisChanceMultiplier - 1.0) * pressure);
        }

        /// <summary>
        /// Reads the chosen answer and returns everything it does as data — morale entries, cash, a
        /// forced sale, a granted trait — applying none of it. Nothing here mutates a ledger, a squad or
        /// a purse, so a caller can still refuse the whole resolution after seeing it (ARCHITECTURE §8);
        /// <see cref="Gaffer.Application.Run.RunSession.ResolveDrama"/> is the single owner that applies
        /// it in order (§8a). <paramref name="currentCash"/> feeds fraction-of-cash effects (a budget cut
        /// scales to the club).
        /// </summary>
        public Result<DramaOutcome> Resolve(PendingDrama pending, int choiceIndex, long currentCash = 0)
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
            List<MoraleChange> moraleChanges = null;

            foreach (DramaEffect effect in choice.Effects)
            {
                switch (effect.Kind)
                {
                    case DramaEffectKind.SubjectMorale:
                        moraleChanges = moraleChanges ?? new List<MoraleChange>(4);
                        moraleChanges.Add(new MoraleChange(pending.Subject.Id, effect.Magnitude, effect.DurationWeeks));
                        break;
                    case DramaEffectKind.TeamMorale:
                        moraleChanges = moraleChanges ?? new List<MoraleChange>(pending.Context.Squad.Count);
                        foreach (Player player in pending.Context.Squad)
                        {
                            moraleChanges.Add(new MoraleChange(player.Id, effect.Magnitude, effect.DurationWeeks));
                        }

                        break;
                    case DramaEffectKind.Cash:
                        cashDelta += effect.Magnitude;
                        break;
                    case DramaEffectKind.CashFraction:
                        cashDelta += currentCash * effect.Magnitude;
                        break;
                    case DramaEffectKind.SubjectWageFine:
                        cashDelta += PlayerWage.Weekly(pending.Subject, _economy);
                        break;
                    case DramaEffectKind.SellSubject:
                        playerToSell = pending.Subject;
                        break;
                    case DramaEffectKind.GrantTraitToSuccessor:
                        traitGrantTarget = Successor(pending);
                        grantedTrait = traitGrantTarget != null ? effect.Trait : default;
                        break;
                    default:
                        // "Every kind changes real state" (DramaEffect) is the rule this switch enforces.
                        // A kind with no arm here is a silent no-op — a broken invariant, not the kind of
                        // expected failure a Result carries (CONVENTIONS §1/§4).
                        throw new ArgumentOutOfRangeException(
                            nameof(pending),
                            effect.Kind,
                            $"Drama effect kind '{effect.Kind}' has no handler, so choosing it would change nothing.");
                }
            }

            return Result<DramaOutcome>.Success(new DramaOutcome(
                pending.Event.Id, choiceIndex, (long)cashDelta, playerToSell,
                (IReadOnlyList<MoraleChange>)moraleChanges, traitGrantTarget, grantedTrait));
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

        // One entry per EVENT in play this week — never one per eligible player. Which player it
        // happens to is a separate draw once the event has won (see PickSubject).
        private readonly struct Candidate
        {
            public Candidate(DramaEvent dramaEvent, double weight)
            {
                Event = dramaEvent;
                Weight = weight;
            }

            public DramaEvent Event { get; }

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
                    candidates.Add(new Candidate(dramaEvent, dramaEvent.BaseWeight * squadBias));
                    continue;
                }

                // NORMALISED CANDIDACY. Every eligible player used to be his own candidate, which
                // multiplied an event's weight by how many players it fitted: the night-club scandal
                // (MaxSubjectAge 30) matched most of a squad and so carried ~12 weight against a
                // transfer request's 0-2, and since the firing chance scales with total weight it
                // dominated BOTH whether drama fired and which drama it was — 60.8% of everything the
                // engine raised, measured. An event now contributes exactly one candidate whose weight
                // is its own, so how broadly it is written no longer buys it frequency.
                double subjectBias = BestSubjectBias(dramaEvent, context, out bool anyEligible);
                if (!anyEligible)
                {
                    continue;
                }

                candidates.Add(new Candidate(dramaEvent, dramaEvent.BaseWeight * squadBias * subjectBias));
            }

            return candidates;
        }

        /// <summary>
        /// The subject half of a normalised event weight: the STRONGEST pull any eligible player has on
        /// this story, and 1.0 when none of them is biased either way.
        /// <para>
        /// Max, not mean — PROGRESS left the choice open ("ortalama/maks") and the trait data settles it.
        /// Biases run in both directions and are large (press-magnet ×3 on the scandal, loyal ×0.2 on a
        /// transfer request), and the pool they are averaged over is whatever the event's filter happens
        /// to be wide enough to admit. The mean of one magnet among fifteen eligible squad members is
        /// ×1.13 — a trait sold as tripling a man's headlines would move the run by a barely-measurable
        /// 13%, which is NON-NEGOTIABLE #7's definition of flavor text, and it would move by LESS the
        /// bigger the squad, re-importing through the back door the squad-size sensitivity this whole
        /// change removes. Max keeps a carrier's pull at full strength and independent of squad size.
        /// The price is that a DAMPING bias (loyal ×0.2) only lowers the event's frequency while it
        /// covers the whole eligible pool — one loyal star does not stop a disgruntled team-mate asking
        /// for a move, which is the honest reading of the trait — but it always cuts his own share of
        /// the second draw, and squad-wide biases (a leader calming the room) are untouched by any of this.
        /// </para>
        /// </summary>
        private static double BestSubjectBias(DramaEvent dramaEvent, in DramaWeekContext context, out bool anyEligible)
        {
            IReadOnlyList<Player> squad = context.Squad;
            double best = 0.0;
            anyEligible = false;
            for (int i = 0; i < squad.Count; i++)
            {
                Player player = squad[i];
                if (!SubjectMatches(dramaEvent.Trigger, player, context))
                {
                    continue;
                }

                anyEligible = true;
                double bias = SubjectBias(dramaEvent, player);
                if (bias > best)
                {
                    best = bias;
                }
            }

            return best;
        }

        /// <summary>
        /// The second draw: which eligible player this event happens to, weighted by his own subject
        /// bias — so a press magnet is still three times likelier than a team-mate to be the one in the
        /// tabloid, and a loyal star a fifth as likely to be the one asking to leave. Null only when
        /// nobody is eligible. Two passes over the squad and no allocation, so the weekly tick stays
        /// allocation-free (PERFORMANCE §8).
        /// </summary>
        private static Player PickSubject(DramaEvent dramaEvent, in DramaWeekContext context, double roll)
        {
            IReadOnlyList<Player> squad = context.Squad;
            double total = 0.0;
            for (int i = 0; i < squad.Count; i++)
            {
                if (SubjectMatches(dramaEvent.Trigger, squad[i], context))
                {
                    total += SubjectBias(dramaEvent, squad[i]);
                }
            }

            double target = roll * total;
            double cumulative = 0.0;
            Player last = null;
            for (int i = 0; i < squad.Count; i++)
            {
                Player player = squad[i];
                if (!SubjectMatches(dramaEvent.Trigger, player, context))
                {
                    continue;
                }

                // Remembered as we go: the fallback for a roll that lands on the far edge of the
                // cumulative sum, and for the degenerate case of every eligible bias being zero.
                last = player;
                cumulative += SubjectBias(dramaEvent, player);
                if (target < cumulative)
                {
                    return player;
                }
            }

            return last;
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

        private static DramaEvent PickWeighted(List<Candidate> candidates, double roll, double total)
        {
            double target = roll * total;
            double cumulative = 0.0;
            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += candidates[i].Weight;
                if (target < cumulative)
                {
                    return candidates[i].Event;
                }
            }

            return candidates[candidates.Count - 1].Event;
        }
    }
}
