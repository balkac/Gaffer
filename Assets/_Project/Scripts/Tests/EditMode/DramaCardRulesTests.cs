using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Run;
using Gaffer.Common;
using Gaffer.Common.Localization;
using Gaffer.Domain.Drama;
using Gaffer.Domain.Players;
using Gaffer.Domain.Traits;
using Gaffer.Infrastructure.Localization;
using Gaffer.Presentation;
using Gaffer.Presentation.Drama;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// The rules the drama card draws by, held apart from the card. Each fails quietly on a device: a
    /// choice drawn with one line fewer than it has effects, a key leaking through as its own name, a
    /// preview priced off a different purse than the resolver reads, a Turkish card wearing English.
    /// </summary>
    public sealed class DramaCardRulesTests
    {
        // ----- Preview: one line per effect, in words -------------------------------------------------

        [Test]
        public void Preview_EveryChoiceInTheCatalog_HasOneLinePerEffect_InEveryShippedLocale()
        {
            // The playtest's complaint was a label with nothing under it. This pins the fix at its widest:
            // every authored choice, in every language, draws exactly as many lines as it has effects
            // (one when it has none), and none of those lines is a key wearing its own name.
            RunSession session = StartRun();
            Player subject = session.Squad.Players[0];
            var problems = new List<string>();

            foreach (string locale in Locales.Shipped)
            {
                LocalizedStrings text = GameStrings.Default.For(locale);
                IReadOnlyList<DramaEvent> events = DramaCatalog.Default.Events;
                for (int e = 0; e < events.Count; e++)
                {
                    DramaEvent dramaEvent = events[e];
                    var pending = new PendingDrama(dramaEvent, dramaEvent.RequiresSubject ? subject : null, Context(session));
                    for (int c = 0; c < dramaEvent.Choices.Count; c++)
                    {
                        DramaChoice choice = dramaEvent.Choices[c];
                        List<DramaLine> lines = DramaLines.Preview(pending, choice, session.Finances.Cash, 10_000L, 500_000L, text);

                        int expected = choice.Effects == null || choice.Effects.Count == 0 ? 1 : choice.Effects.Count;
                        if (lines.Count != expected)
                        {
                            problems.Add($"{dramaEvent.Id.Value}/{choice.LabelKey} ({locale}): {lines.Count} lines for {expected} effects");
                        }

                        for (int l = 0; l < lines.Count; l++)
                        {
                            if (lines[l].Text.Contains("ui.drama.") || lines[l].Text.Contains("trait."))
                            {
                                problems.Add($"{dramaEvent.Id.Value}/{choice.LabelKey} ({locale}): key leaked — {lines[l].Text}");
                            }
                        }
                    }
                }
            }

            Assert.That(problems, Is.Empty, string.Join(" | ", problems));
        }

        [Test]
        public void Preview_ACashFraction_IsPricedOffTheCashHandedIn()
        {
            // The resolver multiplies the club's cash by the fraction; the preview must show the same
            // euros, off the same purse — a card that said "−20%" would leave the manager doing the sum.
            RunSession session = StartRun();
            DramaEvent cut = DramaCatalog.Default.Find(new DramaEventId("budget-cut"));
            var pending = new PendingDrama(cut, null, Context(session));

            List<DramaLine> lines = DramaLines.Preview(pending, cut.Choices[0], 5_000_000L, 0L, 0L, English());

            Assert.That(lines.Count, Is.EqualTo(1));
            Assert.That(lines[0].Text, Does.Contain("−€1.0M"), lines[0].Text);
            Assert.That(lines[0].Text, Does.Contain("20% of the bank"), lines[0].Text);
            Assert.That(lines[0].Tone, Is.EqualTo(DramaTone.Cost));
        }

        [Test]
        public void Preview_AWageFine_QuotesTheWageHandedIn_AndNamesTheMan()
        {
            RunSession session = StartRun();
            Player subject = session.Squad.Players[0];
            DramaEvent scandal = DramaCatalog.Default.Find(new DramaEventId("night-club-scandal"));
            var pending = new PendingDrama(scandal, subject, Context(session));

            // Choice 0 is the fine: one week of his wage in, and a morale wound for four weeks.
            List<DramaLine> lines = DramaLines.Preview(pending, scandal.Choices[0], session.Finances.Cash, 12_500L, 0L, English());

            Assert.That(lines.Count, Is.EqualTo(2));
            Assert.That(lines[0].Text, Does.Contain("+€12k"), lines[0].Text);
            Assert.That(lines[0].Text, Does.Contain(subject.Name + ": one week of wage"), lines[0].Text);
            Assert.That(lines[0].Tone, Is.EqualTo(DramaTone.Gain));
            Assert.That(lines[1].Text, Does.Contain(subject.Name + ": morale −2"), lines[1].Text);
            Assert.That(lines[1].Text, Does.Contain("4 weeks"), lines[1].Text);
            Assert.That(lines[1].Tone, Is.EqualTo(DramaTone.Cost));
        }

        [Test]
        public void Preview_AChoiceWithNoEffects_SaysSoInOneQuietLine()
        {
            RunSession session = StartRun();
            DramaEvent scandal = DramaCatalog.Default.Find(new DramaEventId("night-club-scandal"));
            var pending = new PendingDrama(scandal, session.Squad.Players[0], Context(session));

            // Choice 1, "closed doors", is authored with no effects: living with it is the decision.
            List<DramaLine> lines = DramaLines.Preview(pending, scandal.Choices[1], session.Finances.Cash, 0L, 0L, English());

            Assert.That(lines.Count, Is.EqualTo(1));
            Assert.That(lines[0].Text, Is.EqualTo("Nothing changes. You live with it."));
            Assert.That(lines[0].Tone, Is.EqualTo(DramaTone.Quiet));
        }

        [Test]
        public void Preview_InTurkish_WritesTurkish_WithTheTurkishDecimalMark()
        {
            RunSession session = StartRun();
            Player subject = session.Squad.Players[0];
            DramaEvent request = DramaCatalog.Default.Find(new DramaEventId("transfer-request"));
            var pending = new PendingDrama(request, subject, Context(session));

            // Choice 2, persuade: morale +2 for four weeks and €250k out.
            List<DramaLine> lines = DramaLines.Preview(pending, request.Choices[2], session.Finances.Cash, 0L, 0L, GameStrings.Default.For(Locales.Turkish));

            Assert.That(lines.Count, Is.EqualTo(2));
            Assert.That(lines[0].Text, Is.EqualTo(subject.Name + ": moral +2  ·  4 hafta"), lines[0].Text);
            Assert.That(lines[1].Text, Is.EqualTo("Kasa −€250k"), lines[1].Text);
        }

        [Test]
        public void Preview_ATraitGrant_NamesTheTraitInWords()
        {
            RunSession session = StartRun();
            Player captain = session.Squad.Players[0];
            DramaEvent succession = DramaCatalog.Default.Find(new DramaEventId("captain-succession"));
            var pending = new PendingDrama(succession, captain, Context(session));

            List<DramaLine> lines = DramaLines.Preview(pending, succession.Choices[0], session.Finances.Cash, 0L, 0L, English());

            Assert.That(lines.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(lines[0].Text, Does.Contain("A team-mate inherits Dressing-room leader."), lines[0].Text);
        }

        // ----- Aftermath: what the answer did ----------------------------------------------------------

        [Test]
        public void Aftermath_GroupsATeamWideEffect_IntoOneCountedLine_AndNamesTheOneMan()
        {
            // Twenty-two identical lines would bury the one that is about a person. Same points and weeks
            // fold into a count; a different entry stays a name.
            RunSession session = StartRun();
            IReadOnlyList<Player> squad = session.Squad.Players;
            var changes = new List<MoraleChange>
            {
                new MoraleChange(squad[0].Id, -4.0, 8),
                new MoraleChange(squad[1].Id, 1.0, 4),
                new MoraleChange(squad[2].Id, 1.0, 4),
                new MoraleChange(squad[3].Id, 1.0, 4),
            };
            var resolution = new DramaResolution(
                new DramaEventId("test"), 0, 0L, session.Finances, changes,
                soldPlayer: null, saleFee: 0L, traitGrantTarget: null, grantedTrait: default(TraitId), rebuiltPlayer: null,
                lineup: session.Lineup());

            List<DramaLine> lines = DramaLines.Aftermath(resolution, id => NameIn(squad, id), English());

            Assert.That(lines.Count, Is.EqualTo(2));
            Assert.That(lines[0].Text, Is.EqualTo(squad[0].Name + ": morale −4  ·  8 weeks"), lines[0].Text);
            Assert.That(lines[0].Tone, Is.EqualTo(DramaTone.Cost));
            Assert.That(lines[1].Text, Is.EqualTo("3 players: morale +1  ·  4 weeks"), lines[1].Text);
            Assert.That(lines[1].Tone, Is.EqualTo(DramaTone.Gain));
        }

        [Test]
        public void Aftermath_ThroughTheSession_NamesTheSoldMan_TheFee_AndTheCashLeft()
        {
            // The real boundary: a forced event answered through RunSession.ResolveDrama, and the lines
            // written from the resolution it returned — nothing read back from the run.
            RunSession session = StartRun(AlwaysFire(SellingEvent()));
            Assert.That(session.AdvanceWeek().IsSuccess, Is.True);
            PendingDrama pending = session.PendingDrama;
            Assert.That(pending, Is.Not.Null, "the forced catalog fires every week");
            Player subject = pending.Subject;

            Result<DramaResolution> answered = session.ResolveDrama(0);
            Assert.That(answered.IsSuccess, Is.True, answered.Error);
            DramaResolution resolution = answered.Value;

            List<DramaLine> lines = DramaLines.Aftermath(resolution, id => NameIn(session.Squad.Players, id), English());

            // Morale on the man (he is gone from the roster, so he is named from the resolution), the
            // cash with the balance, and the sale with its fee — three lines, in that order.
            Assert.That(lines.Count, Is.EqualTo(3), string.Join(" | ", Texts(lines)));
            Assert.That(lines[0].Text, Does.StartWith(subject.Name + ": morale −3"), lines[0].Text);
            Assert.That(lines[1].Text, Does.StartWith("Cash −€250k"), lines[1].Text);
            Assert.That(lines[1].Text, Does.Contain(UiMoney.Format(resolution.Finances.Cash, Locales.Reference) + " in the bank"), lines[1].Text);
            Assert.That(lines[2].Text, Does.StartWith(subject.Name + " is out of the squad, sold for " + UiMoney.Format(resolution.SaleFee, Locales.Reference)), lines[2].Text);
        }

        [Test]
        public void Aftermath_WithNothingApplied_SaysNothingMoved()
        {
            RunSession session = StartRun();
            var resolution = new DramaResolution(
                new DramaEventId("test"), 1, 0L, session.Finances, new List<MoraleChange>(),
                soldPlayer: null, saleFee: 0L, traitGrantTarget: null, grantedTrait: default(TraitId), rebuiltPlayer: null,
                lineup: session.Lineup());

            List<DramaLine> lines = DramaLines.Aftermath(resolution, _ => null, English());

            Assert.That(lines.Count, Is.EqualTo(1));
            Assert.That(lines[0].Text, Is.EqualTo("Nothing moved. You lived with it."));
        }

        // ----- The number ---------------------------------------------------------------------------------

        [Test]
        public void Points_AreSigned_WithTheTypographicMinusThePenaltyColumnUses()
        {
            Assert.That(DramaLines.Points(3.0), Is.EqualTo("+3"));
            Assert.That(DramaLines.Points(-4.0), Is.EqualTo("−4"));
            Assert.That(DramaLines.Points(-1.5), Is.EqualTo("−1.5"));
            Assert.That(DramaLines.Points(0.04), Is.EqualTo("0"));
        }

        // ----- Helpers ------------------------------------------------------------------------------------

        private static LocalizedStrings English()
        {
            return GameStrings.Default.For(Locales.Reference);
        }

        private static DramaWeekContext Context(RunSession session)
        {
            return new DramaWeekContext(session.Squad.Players, null, 10, 0, false);
        }

        private static string NameIn(IReadOnlyList<Player> players, PlayerId id)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Id.Equals(id))
                {
                    return players[i].Name;
                }
            }

            return null;
        }

        private static List<string> Texts(List<DramaLine> lines)
        {
            var texts = new List<string>(lines.Count);
            for (int i = 0; i < lines.Count; i++)
            {
                texts.Add(lines[i].Text);
            }

            return texts;
        }

        private static RunSession StartRun()
        {
            return StartRun(new RunBalance(drama: new DramaSettings(maxEventsPerSeason: 0)));
        }

        private static RunSession StartRun(RunBalance balance)
        {
            Result<RunSession> started = RunSessionFactory.Start(
                new RunSetup(
                    teamCount: 8,
                    seed: 7UL,
                    managedClubIndex: 0,
                    promotionPosition: 2,
                    survivalPosition: 6,
                    startingCash: 3_000_000L,
                    weeklyWageBudget: 400_000L,
                    marketSize: 60,
                    guaranteedGems: 2),
                balance);
            Assert.That(started.IsSuccess, Is.True, started.Error);
            return started.Value;
        }

        private static RunBalance AlwaysFire(DramaEvent forced)
        {
            return new RunBalance(
                dramaEvents: new DramaCatalog(new[] { forced }),
                drama: new DramaSettings(
                    maxEventsPerSeason: 99,
                    minWeeksBetweenEvents: 1,
                    weeklyChancePerWeight: 1.0,
                    maxWeeklyChance: 1.0));
        }

        // Choice 0 does three things at once — morale, cash and a forced sale — which is exactly what the
        // aftermath has to say in three lines. Choice 1 leaves a way out.
        private static DramaEvent SellingEvent()
        {
            return new DramaEvent(
                new DramaEventId("test-sell-demand"), DramaCategory.Personal,
                "drama.test.title", "drama.test.body",
                requiresSubject: true,
                new DramaTrigger(),
                baseWeight: 1.0, cooldownWeeks: 0,
                new[]
                {
                    new DramaChoice("drama.test.let_him_go", new[]
                    {
                        new DramaEffect(DramaEffectKind.SubjectMorale, -3.0, 4),
                        new DramaEffect(DramaEffectKind.Cash, -250_000.0),
                        new DramaEffect(DramaEffectKind.SellSubject),
                    }),
                    new DramaChoice("drama.test.keep_him", new[]
                    {
                        new DramaEffect(DramaEffectKind.TeamMorale, 1.0, 2),
                    }),
                });
        }
    }
}
