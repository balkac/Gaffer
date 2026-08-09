using System.Collections.Generic;
using Gaffer.Common;
using Gaffer.Domain.Traits;

namespace Gaffer.Domain.Drama
{
    /// <summary>
    /// The set of drama events a run plays with, looked up by id. <see cref="Default"/> is the
    /// built-in calibrated set so the pure core and headless tests run without assets; the
    /// Infrastructure authoring surface maps onto these types and overrides it (config-as-override,
    /// the BalanceSO pattern). Copy fields are localization keys.
    /// <para>
    /// STRICTNESS POSTURE (ARCHITECTURE §11) — the same split <see cref="TraitCatalog"/> documents at
    /// length, restated here because this is the type that owns the CROSS-catalog references. Lookup
    /// (<see cref="Find"/>) stays tolerant for the save/restore path; loading authored content goes
    /// through <see cref="ValidateAgainst"/>, which is STRICT because this content ships INSIDE the
    /// build — asset and reader are atomic, so the version is a document fact and there is no
    /// acceptance policy to tune, and a trait slug that resolves to nothing is a typo that must break
    /// CI rather than quietly reduce a mechanically-real trait to flavor text (NON-NEGOTIABLE #7).
    /// If this content ever ships remotely instead, that choice inverts — see TraitCatalog's note.
    /// </para>
    /// </summary>
    public sealed class DramaCatalog
    {
        private readonly Dictionary<DramaEventId, DramaEvent> _byId;
        private readonly List<DramaEvent> _events;

        public DramaCatalog(IReadOnlyList<DramaEvent> events)
        {
            _events = new List<DramaEvent>(events);
            _byId = new Dictionary<DramaEventId, DramaEvent>(events.Count);
            foreach (DramaEvent dramaEvent in events)
            {
                _byId[dramaEvent.Id] = dramaEvent;
            }
        }

        public IReadOnlyList<DramaEvent> Events => _events;

        /// <summary>The event for this id, or null when the catalog does not define it — tolerant, for
        /// the same save-outlives-the-build reason <see cref="TraitCatalog.Find"/> is.</summary>
        public DramaEvent Find(DramaEventId id)
        {
            return _byId.TryGetValue(id, out DramaEvent found) ? found : null;
        }

        /// <summary>
        /// Checks this catalog as authored content that ships in the build: every event carries a
        /// unique id, and EVERY trait slug an event points at is actually defined by <paramref name="traits"/>.
        /// There are four such cross-references and a typo in any of them is invisible at runtime —
        /// a bias silently stops biasing, a required trait gates an event that can then never fire,
        /// a grant hands out a trait with no mechanics. Reports every problem with the offending
        /// event and trait id named, as a <see cref="Result"/> the loader turns into a visible
        /// failure (CONVENTIONS §4) rather than the silent skip <see cref="Find"/> would give.
        /// </summary>
        public Result ValidateAgainst(TraitCatalog traits)
        {
            if (traits == null)
            {
                // Not a recoverable authoring problem — asking for a cross-catalog check with nothing to
                // check against is a caller bug, and answering "valid" would be the silent pass this whole
                // method exists to remove (CONVENTIONS §4: broken invariants fail fast).
                throw new System.ArgumentNullException(nameof(traits));
            }

            return Check(traits);
        }

        /// <summary>
        /// Every localization key this catalog hands to the UI — each event's title and body, then each
        /// choice's label, in catalog order. Keys, never words: this is the list the string table is
        /// checked AGAINST (<see cref="Gaffer.Common.Localization.StringTable.Validate"/>), so a new
        /// event that ships with no copy fails on the commit that adds it rather than on the card.
        /// Empty keys are included deliberately — a choice with no label key is a hole, and the check
        /// on the other side is the one that says so by name.
        /// </summary>
        public IReadOnlyList<string> CopyKeys()
        {
            var keys = new List<string>(_events.Count * 4);
            for (int i = 0; i < _events.Count; i++)
            {
                DramaEvent dramaEvent = _events[i];
                keys.Add(dramaEvent.TitleKey);
                keys.Add(dramaEvent.BodyKey);
                IReadOnlyList<DramaChoice> choices = dramaEvent.Choices;
                if (choices == null)
                {
                    continue;
                }

                // Indexed, not foreach: IReadOnlyList<T>'s enumerator boxes (PERFORMANCE §8).
                for (int c = 0; c < choices.Count; c++)
                {
                    if (choices[c] != null)
                    {
                        keys.Add(choices[c].LabelKey);
                    }
                }
            }

            return keys;
        }

        /// <summary>The id half of <see cref="ValidateAgainst"/> — unique, non-empty event ids and
        /// well-formed choices — for the one caller that has no trait catalog in hand (the compat
        /// <c>ToCatalog</c> shim). It is a WEAKER check by construction: it cannot see a dangling trait
        /// slug, so it is not a substitute for the cross-catalog one.</summary>
        public Result Validate()
        {
            return Check(null);
        }

        private Result Check(TraitCatalog traits)
        {
            var problems = new List<string>();
            var seen = new HashSet<string>();
            for (int i = 0; i < _events.Count; i++)
            {
                // A null entry cannot reach here — the constructor's id indexing would have raised on it
                // already, which is the right answer for a broken invariant (CONVENTIONS §4).
                DramaEvent dramaEvent = _events[i];
                string id = dramaEvent.Id.Value;
                if (string.IsNullOrEmpty(id))
                {
                    problems.Add($"Drama event at index {i} has no id slug.");
                    continue;
                }

                if (!seen.Add(id))
                {
                    problems.Add($"Drama event id '{id}' is defined more than once.");
                }

                CollectTraitProblems(dramaEvent, traits, problems);
            }

            return TraitCatalog.Describe(problems, "Drama catalog");
        }

        private static void CollectTraitProblems(DramaEvent dramaEvent, TraitCatalog traits, List<string> problems)
        {
            string id = dramaEvent.Id.Value;
            CollectBiasProblems(dramaEvent.SubjectTraitBiases, traits, id, "subject trait bias", problems);
            CollectBiasProblems(dramaEvent.SquadTraitBiases, traits, id, "squad trait bias", problems);

            // An unset required trait means "any player" — the neutral answer, not a missing one.
            TraitId required = dramaEvent.Trigger == null ? default : dramaEvent.Trigger.RequiredSubjectTrait;
            if (traits != null && !string.IsNullOrEmpty(required.Value) && !traits.Defines(required))
            {
                problems.Add($"Drama event '{id}' requires undefined trait '{required.Value}'.");
            }

            if (dramaEvent.Choices == null)
            {
                problems.Add($"Drama event '{id}' has no choices.");
                return;
            }

            for (int c = 0; c < dramaEvent.Choices.Count; c++)
            {
                DramaChoice choice = dramaEvent.Choices[c];
                if (choice?.Effects == null)
                {
                    problems.Add($"Drama event '{id}' has a missing choice at index {c}.");
                    continue;
                }

                // Indexed, not foreach: these lists are reached through IReadOnlyList<T>, whose enumerator
                // is boxed on every loop (PERFORMANCE §8's named trap).
                for (int e = 0; e < choice.Effects.Count; e++)
                {
                    DramaEffect effect = choice.Effects[e];
                    if (effect.Kind != DramaEffectKind.GrantTraitToSuccessor)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(effect.Trait.Value))
                    {
                        problems.Add($"Drama event '{id}' choice '{choice.LabelKey}' grants no trait.");
                    }
                    else if (traits != null && !traits.Defines(effect.Trait))
                    {
                        problems.Add(
                            $"Drama event '{id}' choice '{choice.LabelKey}' grants undefined trait '{effect.Trait.Value}'.");
                    }
                }
            }
        }

        private static void CollectBiasProblems(
            IReadOnlyList<DramaTraitBias> biases, TraitCatalog traits, string eventId, string what, List<string> problems)
        {
            if (biases == null)
            {
                return;
            }

            for (int i = 0; i < biases.Count; i++)
            {
                DramaTraitBias bias = biases[i];
                if (string.IsNullOrEmpty(bias.Trait.Value))
                {
                    problems.Add($"Drama event '{eventId}' has a {what} with no trait id.");
                }
                else if (traits != null && !traits.Defines(bias.Trait))
                {
                    problems.Add($"Drama event '{eventId}' has a {what} on undefined trait '{bias.Trait.Value}'.");
                }
            }
        }

        /// <summary>
        /// The built-in core set (GDD §4.7's menu, MVP-sized): personal, institutional, fan, and
        /// relationship events — each consequential (effects change morale, cash, or the squad),
        /// each a decision, each rare by cooldown. Numbers are starting calibration.
        /// </summary>
        public static DramaCatalog Default { get; } = new DramaCatalog(new[]
        {
            new DramaEvent(
                new DramaEventId("transfer-request"), DramaCategory.Personal,
                "drama.transfer_request.title", "drama.transfer_request.body",
                requiresSubject: true,
                new DramaTrigger { MinSubjectRating = 66.0, RequiresOpenWindow = true },
                baseWeight: 1.0, cooldownWeeks: 16,
                new[]
                {
                    new DramaChoice("drama.transfer_request.refuse", new[]
                    {
                        new DramaEffect(DramaEffectKind.SubjectMorale, -4.0, 8),
                    }),
                    new DramaChoice("drama.transfer_request.sell", new[]
                    {
                        new DramaEffect(DramaEffectKind.SellSubject),
                    }),
                    new DramaChoice("drama.transfer_request.persuade", new[]
                    {
                        new DramaEffect(DramaEffectKind.SubjectMorale, 2.0, 4),
                        new DramaEffect(DramaEffectKind.Cash, -250000.0),
                    }),
                },
                subjectTraitBiases: new[] { new DramaTraitBias(new TraitId("loyal"), 0.2) }),

            new DramaEvent(
                new DramaEventId("night-club-scandal"), DramaCategory.Personal,
                "drama.night_club_scandal.title", "drama.night_club_scandal.body",
                requiresSubject: true,
                new DramaTrigger { MaxSubjectAge = 30 },
                // 0.5, down from 0.8 (2026-08-07). This is the ONLY event in the set whose trigger
                // every ordinary week satisfies — no window, no streak, no rare trait, no veteran —
                // so it is in play roughly three times as often as anything else and its base weight
                // has to sit below theirs to come out level. It was also the event that the old
                // per-eligible-player candidacy inflated most (60.8% of all drama raised, measured);
                // normalising candidacy took it to 42%, and this brings it to 33% — still the most
                // common story in the game, which suits a squad full of twenty-somethings.
                baseWeight: 0.5, cooldownWeeks: 10,
                new[]
                {
                    new DramaChoice("drama.night_club_scandal.fine", new[]
                    {
                        new DramaEffect(DramaEffectKind.SubjectWageFine),
                        new DramaEffect(DramaEffectKind.SubjectMorale, -2.0, 4),
                    }),
                    new DramaChoice("drama.night_club_scandal.closed_doors", System.Array.Empty<DramaEffect>()),
                    new DramaChoice("drama.night_club_scandal.back_him", new[]
                    {
                        new DramaEffect(DramaEffectKind.SubjectMorale, 2.0, 4),
                        new DramaEffect(DramaEffectKind.TeamMorale, -1.0, 4),
                    }),
                },
                subjectTraitBiases: new[] { new DramaTraitBias(new TraitId("press-magnet"), 3.0) }),

            new DramaEvent(
                new DramaEventId("dressing-room-rift"), DramaCategory.Relationship,
                "drama.dressing_room_rift.title", "drama.dressing_room_rift.body",
                requiresSubject: false,
                new DramaTrigger { MinLossStreak = 3 },
                baseWeight: 1.0, cooldownWeeks: 12,
                new[]
                {
                    new DramaChoice("drama.dressing_room_rift.meeting", new[]
                    {
                        new DramaEffect(DramaEffectKind.TeamMorale, 2.0, 4),
                    }),
                    new DramaChoice("drama.dressing_room_rift.let_it_burn", new[]
                    {
                        new DramaEffect(DramaEffectKind.TeamMorale, -2.0, 6),
                    }),
                },
                squadTraitBiases: new[] { new DramaTraitBias(new TraitId("dressing-room-leader"), 0.5) }),

            new DramaEvent(
                new DramaEventId("fan-protest"), DramaCategory.FansMedia,
                "drama.fan_protest.title", "drama.fan_protest.body",
                requiresSubject: false,
                new DramaTrigger { MinLossStreak = 4 },
                baseWeight: 0.8, cooldownWeeks: 12,
                new[]
                {
                    new DramaChoice("drama.fan_protest.face_them", new[]
                    {
                        new DramaEffect(DramaEffectKind.TeamMorale, 1.0, 6),
                    }),
                    new DramaChoice("drama.fan_protest.ignore", new[]
                    {
                        new DramaEffect(DramaEffectKind.TeamMorale, -2.0, 6),
                    }),
                }),

            new DramaEvent(
                new DramaEventId("wonderkid-wants-minutes"), DramaCategory.Personal,
                "drama.wonderkid_wants_minutes.title", "drama.wonderkid_wants_minutes.body",
                requiresSubject: true,
                new DramaTrigger { MaxSubjectAge = 19, MinSubjectPotentialGap = 15.0, SubjectBenched = true },
                baseWeight: 0.8, cooldownWeeks: 16,
                new[]
                {
                    new DramaChoice("drama.wonderkid_wants_minutes.promise_starts", new[]
                    {
                        new DramaEffect(DramaEffectKind.SubjectMorale, 3.0, 8),
                    }),
                    new DramaChoice("drama.wonderkid_wants_minutes.wait_your_turn", new[]
                    {
                        new DramaEffect(DramaEffectKind.SubjectMorale, -3.0, 8),
                    }),
                }),

            new DramaEvent(
                new DramaEventId("budget-cut"), DramaCategory.Institutional,
                "drama.budget_cut.title", "drama.budget_cut.body",
                requiresSubject: false,
                new DramaTrigger(),
                baseWeight: 0.3, cooldownWeeks: 38,
                new[]
                {
                    new DramaChoice("drama.budget_cut.accept", new[]
                    {
                        new DramaEffect(DramaEffectKind.CashFraction, -0.2),
                    }),
                    new DramaChoice("drama.budget_cut.fight_it", new[]
                    {
                        new DramaEffect(DramaEffectKind.CashFraction, -0.05),
                        new DramaEffect(DramaEffectKind.TeamMorale, -1.0, 4),
                    }),
                }),

            new DramaEvent(
                new DramaEventId("captain-succession"), DramaCategory.Relationship,
                "drama.captain_succession.title", "drama.captain_succession.body",
                requiresSubject: true,
                new DramaTrigger { MinSubjectAge = 33, RequiredSubjectTrait = new TraitId("dressing-room-leader") },
                baseWeight: 0.6, cooldownWeeks: 38,
                new[]
                {
                    new DramaChoice("drama.captain_succession.anoint", new[]
                    {
                        new DramaEffect(DramaEffectKind.GrantTraitToSuccessor, new TraitId("dressing-room-leader")),
                        new DramaEffect(DramaEffectKind.TeamMorale, 1.0, 4),
                    }),
                    new DramaChoice("drama.captain_succession.your_call", new[]
                    {
                        new DramaEffect(DramaEffectKind.SubjectMorale, -2.0, 4),
                    }),
                }),

            new DramaEvent(
                new DramaEventId("club-takeover"), DramaCategory.Institutional,
                "drama.club_takeover.title", "drama.club_takeover.body",
                requiresSubject: false,
                new DramaTrigger(),
                baseWeight: 0.2, cooldownWeeks: 76,
                new[]
                {
                    new DramaChoice("drama.club_takeover.back_the_owners", new[]
                    {
                        new DramaEffect(DramaEffectKind.Cash, 2_000_000.0),
                    }),
                    new DramaChoice("drama.club_takeover.keep_your_distance", new[]
                    {
                        new DramaEffect(DramaEffectKind.TeamMorale, 1.0, 6),
                    }),
                },
                oncePerRun: true),

            new DramaEvent(
                new DramaEventId("press-war"), DramaCategory.FansMedia,
                "drama.press_war.title", "drama.press_war.body",
                requiresSubject: true,
                new DramaTrigger { RequiredSubjectTrait = new TraitId("press-magnet") },
                baseWeight: 0.7, cooldownWeeks: 14,
                new[]
                {
                    new DramaChoice("drama.press_war.muzzle_him", new[]
                    {
                        new DramaEffect(DramaEffectKind.SubjectMorale, -2.0, 4),
                    }),
                    new DramaChoice("drama.press_war.let_him_talk", new[]
                    {
                        new DramaEffect(DramaEffectKind.SubjectMorale, 2.0, 4),
                        new DramaEffect(DramaEffectKind.TeamMorale, -1.0, 2),
                    }),
                }),

            new DramaEvent(
                new DramaEventId("contract-standoff"), DramaCategory.Personal,
                "drama.contract_standoff.title", "drama.contract_standoff.body",
                requiresSubject: true,
                new DramaTrigger { MinSubjectAge = 30, MinSubjectRating = 60.0 },
                baseWeight: 0.7, cooldownWeeks: 20,
                new[]
                {
                    new DramaChoice("drama.contract_standoff.give_him_the_year", new[]
                    {
                        new DramaEffect(DramaEffectKind.Cash, -500_000.0),
                        new DramaEffect(DramaEffectKind.SubjectMorale, 2.0, 6),
                    }),
                    new DramaChoice("drama.contract_standoff.refuse", new[]
                    {
                        new DramaEffect(DramaEffectKind.SubjectMorale, -3.0, 6),
                    }),
                }),
        });
    }
}
