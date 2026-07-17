using System.Collections.Generic;
using Gaffer.Domain.Traits;

namespace Gaffer.Domain.Drama
{
    /// <summary>
    /// The set of drama events a run plays with, looked up by id. <see cref="Default"/> is the
    /// built-in calibrated set so the pure core and headless tests run without assets; the
    /// Infrastructure authoring surface maps onto these types and overrides it (config-as-override,
    /// the BalanceSO pattern). Copy fields are localization keys.
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

        public DramaEvent Find(DramaEventId id)
        {
            return _byId.TryGetValue(id, out DramaEvent found) ? found : null;
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
                baseWeight: 0.8, cooldownWeeks: 10,
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
