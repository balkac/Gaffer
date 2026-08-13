using System;
using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Narrative;
using Gaffer.Application.Progression;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Application.Transfers;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Drama;
using Gaffer.Domain.Leagues;
using Gaffer.Domain.Players;
using Gaffer.Domain.Traits;

namespace Gaffer.Application.Serialization
{
    /// <summary>
    /// Maps between the live season and its serializable snapshot. Capture records the clubs (with their
    /// full squads, so development and renewal survive across seasons), the season number, the played
    /// results, the rounds done, and the season seed; Restore rebuilds the league and hands back that state
    /// as data (<see cref="RestoredSeason"/>) for the run to replay onto its own season, which continues
    /// deterministically because the remaining fixtures are seeded from
    /// <see cref="SeasonSaveData.MatchSeed"/> — reproducing an uninterrupted run exactly. A club with
    /// no roster (a strength-only harness fixture, or an older v2 save) round-trips as strength only.
    /// </summary>
    public sealed class SeasonSaveMapper
    {
        /// <summary>
        /// A league snapshot with no run around it — a harness fixture, or any caller that owns a league
        /// and a season but not a <c>RunSession</c>. The document still claims the current schema and still
        /// carries a run block, because a v6 document always has one; that block holds the seed and nothing
        /// else, which reads back as "this save does not know" for every group and resumes from the
        /// caller's setup.
        /// </summary>
        public SeasonSaveData Capture(League league, LeagueSeason season, ulong matchSeed, int seasonNumber)
        {
            return Capture(league, season, matchSeed, seasonNumber, null);
        }

        public SeasonSaveData Capture(League league, LeagueSeason season, ulong matchSeed, int seasonNumber, RunState run)
        {
            var data = new SeasonSaveData
            {
                SchemaVersion = SeasonSaveData.CurrentVersion,
                LeagueName = league.Name,
                SeasonNumber = seasonNumber,
                PlayedRounds = season.CurrentRound,
                MatchSeed = matchSeed,
                Run = CaptureRun(run, matchSeed),
            };

            foreach (Club club in league.Clubs)
            {
                data.Clubs.Add(new ClubSaveData
                {
                    Id = club.Id.Value,
                    Name = club.Name,
                    Attack = club.Strength.Attack,
                    Midfield = club.Strength.Midfield,
                    Defence = club.Strength.Defence,
                    Squad = CaptureSquad(club.Squad),
                });
            }

            foreach (MatchResult result in season.PlayedResults)
            {
                data.Results.Add(new MatchResultSaveData
                {
                    Home = result.Home.Value,
                    Away = result.Away.Value,
                    HomeGoals = result.HomeGoals,
                    AwayGoals = result.AwayGoals,
                });
            }

            return data;
        }

        /// <summary>
        /// Rebuilds the run state a save carries: the league with its rosters, the rounds played, and the
        /// result history. No balance arguments, because there is nothing here for them to configure — the
        /// mapper hands back data and the caller's own <see cref="LeagueSeason"/> resolves traits, tactics
        /// and morale through the run's catalogs when it replays this history (see
        /// <see cref="RestoredSeason"/> on why no season is returned). Player traits ride in the document
        /// as ids and rebind against whatever catalog is live, so a restore is catalog-independent by
        /// construction.
        /// </summary>
        public RestoredSeason Restore(SeasonSaveData data)
        {
            var clubs = new List<Club>(data.Clubs.Count);
            foreach (ClubSaveData club in data.Clubs)
            {
                var strength = new TeamStrength(club.Attack, club.Midfield, club.Defence);
                Squad squad = RestoreSquad(club.Squad);
                clubs.Add(squad == null
                    ? new Club(new ClubId(club.Id), club.Name, strength)
                    : new Club(new ClubId(club.Id), club.Name, squad, strength));
            }

            var league = new League(data.LeagueName, clubs);

            var results = new List<MatchResult>(data.Results.Count);
            foreach (MatchResultSaveData result in data.Results)
            {
                // Per-goal events are not persisted (score is enough to rebuild the table); the
                // narrative layer (Faz 5) will decide what match detail a save must keep.
                results.Add(new MatchResult(new ClubId(result.Home), new ClubId(result.Away), result.HomeGoals, result.AwayGoals, Array.Empty<MatchEvent>()));
            }

            return new RestoredSeason(league, data.SeasonNumber, data.PlayedRounds, results, RestoreRun(data));
        }

        // ----- The run block (v6) ------------------------------------------------------------------------

        // A null snapshot is a capture with no run in hand, not an error: the block is still written, with
        // the seed in it and every group absent. See Capture's own note.
        private static RunSaveData CaptureRun(RunState run, ulong matchSeed)
        {
            if (run == null)
            {
                return new RunSaveData { OriginalSeed = matchSeed };
            }

            return new RunSaveData
            {
                OriginalSeed = run.OriginalSeed,
                Setup = CaptureSetup(run.Setup),
                Finances = CaptureFinances(run.Finances),
                Tactics = CaptureTactics(run.Formation, run.Tactics),
                Eleven = CaptureEleven(run.Eleven),
                Market = CaptureMarket(run.Market),
                Morale = CaptureMorale(run.Morale),
                Drama = CaptureDrama(run),
                Journeys = CaptureJourneys(run.Journeys),
                Development = CaptureDevelopment(run.Development),
            };
        }

        /// <summary>
        /// The run block turned back into domain types, or null when the document has none (a pre-v6 save
        /// that skipped the migrator). Tolerant throughout, per the posture on
        /// <see cref="SeasonSaveData"/>: an absent group becomes an absent member and the run falls back to
        /// its setup, an unreadable tactical axis takes the neutral value, and a formation whose slots do
        /// not read comes back absent rather than half-built. Nothing here refuses a load.
        /// </summary>
        private static RunState RestoreRun(SeasonSaveData data)
        {
            RunSaveData run = data.Run;
            if (run == null)
            {
                return null;
            }

            RestoreTactics(run.Tactics, out Formation? formation, out Tactics? tactics);

            return new RunState(
                originalSeed: run.OriginalSeed,
                setup: RestoreSetup(run.Setup),
                finances: RestoreFinances(run.Finances),
                formation: formation,
                tactics: tactics,
                eleven: run.Eleven,
                market: RestorePlayers(run.Market),
                morale: RestoreMorale(run.Morale),
                drama: RestoreDrama(run.Drama),
                pendingEvent: RestorePendingEvent(run.Drama),
                pendingSubjectPlayerId: run.Drama != null ? run.Drama.PendingSubjectPlayerId : RunSaveData.NoPlayer,
                journeys: RestoreJourneys(run.Journeys),
                development: RestoreDevelopment(run.Development));
        }

        private static RunSetupSaveData CaptureSetup(RunSetupState setup)
        {
            if (setup == null)
            {
                return null;
            }

            return new RunSetupSaveData
            {
                ManagedClubIndex = setup.ManagedClubIndex,
                TeamCount = setup.TeamCount,
                PromotionPosition = setup.PromotionPosition,
                SurvivalPosition = setup.SurvivalPosition,
                MarketSize = setup.MarketSize,
                GuaranteedGems = setup.GuaranteedGems,
            };
        }

        private static RunSetupState RestoreSetup(RunSetupSaveData setup)
        {
            if (setup == null)
            {
                return null;
            }

            return new RunSetupState(
                setup.ManagedClubIndex,
                setup.TeamCount,
                setup.PromotionPosition,
                setup.SurvivalPosition,
                setup.MarketSize,
                setup.GuaranteedGems);
        }

        private static FinancesSaveData CaptureFinances(Finances? finances)
        {
            if (finances == null)
            {
                return null;
            }

            Finances money = finances.Value;
            return new FinancesSaveData
            {
                Cash = money.Cash,
                WeeklyWageBudget = money.WeeklyWageBudget,
                WeeklyWageBill = money.WeeklyWageBill,
            };
        }

        private static Finances? RestoreFinances(FinancesSaveData finances)
        {
            if (finances == null)
            {
                return null;
            }

            return new Finances(finances.Cash, finances.WeeklyWageBudget, finances.WeeklyWageBill);
        }

        // Shape and tactics travel together because they are answered together: a team sheet is meaningless
        // without the shape whose slots it fills.
        private static TacticsSaveData CaptureTactics(Formation? formation, Tactics? tactics)
        {
            if (formation == null && tactics == null)
            {
                return null;
            }

            var data = new TacticsSaveData();
            if (formation != null)
            {
                Formation shape = formation.Value;
                data.FormationName = shape.Name;
                data.FormationSlots = CaptureSlots(shape.Slots);
            }

            if (tactics != null)
            {
                Tactics setup = tactics.Value;
                data.Mentality = PersistedEnum.ToName(setup.Mentality);
                data.Tempo = PersistedEnum.ToName(setup.Tempo);
                data.Pressing = PersistedEnum.ToName(setup.Pressing);
                data.Approach = PersistedEnum.ToName(setup.Approach);
            }

            return data;
        }

        private static List<string> CaptureSlots(IReadOnlyList<PlayerRole> slots)
        {
            if (slots == null)
            {
                return null;
            }

            var names = new List<string>(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                names.Add(PersistedPlayerRole.ToName(slots[i]));
            }

            return names;
        }

        // A slot role this build cannot read makes the whole SHAPE unreadable, not just that slot: an
        // eleven-slot formation quietly restored with ten is a worse answer than falling back to the run's
        // setup, because the missing slot would silently bench somebody every week.
        private static void RestoreTactics(TacticsSaveData data, out Formation? formation, out Tactics? tactics)
        {
            formation = null;
            tactics = null;
            if (data == null)
            {
                return;
            }

            if (data.FormationSlots != null && data.FormationSlots.Count > 0)
            {
                var slots = new List<PlayerRole>(data.FormationSlots.Count);
                bool readable = true;
                for (int i = 0; i < data.FormationSlots.Count; i++)
                {
                    if (!PersistedPlayerRole.TryParse(data.FormationSlots[i], out PlayerRole role))
                    {
                        readable = false;
                        break;
                    }

                    slots.Add(role);
                }

                if (readable)
                {
                    formation = new Formation(data.FormationName, slots);
                }
            }

            if (data.Mentality != null || data.Tempo != null || data.Pressing != null || data.Approach != null)
            {
                Tactics neutral = Simulation.Tactics.Balanced;
                tactics = new Tactics(
                    PersistedEnum.ParseOr(data.Mentality, neutral.Mentality),
                    PersistedEnum.ParseOr(data.Tempo, neutral.Tempo),
                    PersistedEnum.ParseOr(data.Pressing, neutral.Pressing),
                    PersistedEnum.ParseOr(data.Approach, neutral.Approach));
            }
        }

        private static List<int> CaptureEleven(IReadOnlyList<int> eleven)
        {
            if (eleven == null)
            {
                return null;
            }

            var ids = new List<int>(eleven.Count);
            for (int i = 0; i < eleven.Count; i++)
            {
                ids.Add(eleven[i]);
            }

            return ids;
        }

        private static List<PlayerSaveData> CaptureMarket(IReadOnlyList<Player> market)
        {
            if (market == null)
            {
                return null;
            }

            var players = new List<PlayerSaveData>(market.Count);
            for (int i = 0; i < market.Count; i++)
            {
                players.Add(CapturePlayer(market[i]));
            }

            return players;
        }

        private static List<MoraleSaveData> CaptureMorale(IReadOnlyList<MoraleEntry> morale)
        {
            if (morale == null)
            {
                return null;
            }

            var entries = new List<MoraleSaveData>(morale.Count);
            for (int i = 0; i < morale.Count; i++)
            {
                MoraleEntry entry = morale[i];
                entries.Add(new MoraleSaveData
                {
                    PlayerId = entry.Player.Value,
                    Points = entry.Points,
                    WeeksLeft = entry.WeeksLeft,
                });
            }

            return entries;
        }

        private static IReadOnlyList<MoraleEntry> RestoreMorale(List<MoraleSaveData> morale)
        {
            if (morale == null)
            {
                return null;
            }

            var entries = new List<MoraleEntry>(morale.Count);
            for (int i = 0; i < morale.Count; i++)
            {
                MoraleSaveData entry = morale[i];
                entries.Add(new MoraleEntry(new PlayerId(entry.PlayerId), entry.Points, entry.WeeksLeft));
            }

            return entries;
        }

        private static DramaSaveData CaptureDrama(RunState run)
        {
            DramaEngineState drama = run.Drama;
            bool hasPending = !string.IsNullOrEmpty(run.PendingEvent.Value);
            if (drama == null && !hasPending)
            {
                return null;
            }

            var data = new DramaSaveData
            {
                PendingEventId = hasPending ? run.PendingEvent.Value : null,
                PendingSubjectPlayerId = run.PendingSubjectPlayerId,
            };

            if (drama != null)
            {
                data.Week = drama.Week;
                data.LastFiredWeek = drama.LastFiredWeek;
                data.FiredThisSeason = drama.FiredThisSeason;
                data.Events = CaptureDramaEvents(drama.Events);
            }

            return data;
        }

        private static List<DramaEventSaveData> CaptureDramaEvents(IReadOnlyList<DramaEventMark> marks)
        {
            if (marks == null)
            {
                return null;
            }

            var events = new List<DramaEventSaveData>(marks.Count);
            for (int i = 0; i < marks.Count; i++)
            {
                events.Add(new DramaEventSaveData { Id = marks[i].Event.Value, LastFiredWeek = marks[i].LastFiredWeek });
            }

            return events;
        }

        private static DramaEngineState RestoreDrama(DramaSaveData data)
        {
            if (data == null)
            {
                return null;
            }

            var marks = new List<DramaEventMark>(data.Events != null ? data.Events.Count : 0);
            if (data.Events != null)
            {
                for (int i = 0; i < data.Events.Count; i++)
                {
                    DramaEventSaveData fired = data.Events[i];
                    marks.Add(new DramaEventMark(new DramaEventId(fired.Id), fired.LastFiredWeek));
                }
            }

            return new DramaEngineState(data.Week, data.LastFiredWeek, data.FiredThisSeason, marks);
        }

        private static DramaEventId RestorePendingEvent(DramaSaveData data)
        {
            return data == null || string.IsNullOrEmpty(data.PendingEventId)
                ? default
                : new DramaEventId(data.PendingEventId);
        }

        private static List<PlayerSaveData> CaptureSquad(Squad squad)
        {
            if (squad == null)
            {
                return null;
            }

            var players = new List<PlayerSaveData>(squad.Players.Count);
            foreach (Player player in squad.Players)
            {
                players.Add(CapturePlayer(player));
            }

            return players;
        }

        // One player record, so a squad and the v6 market are written by the same code — a prospect and a
        // squad member are the same thing to the format, and two copies of this mapping would be two places
        // to forget a field.
        private static PlayerSaveData CapturePlayer(Player player)
        {
            return new PlayerSaveData
            {
                Id = player.Id.Value,
                Name = player.Name,
                Nationality = player.Nationality,
                RoleName = PersistedPlayerRole.ToName(player.Role),
                Age = player.Age,
                HiddenPotential = player.HiddenPotential,
                Attributes = ToData(player.Attributes),
                Traits = CaptureTraits(player),
            };
        }

        private static Squad RestoreSquad(List<PlayerSaveData> saved)
        {
            IReadOnlyList<Player> players = RestorePlayers(saved);
            return players == null ? null : new Squad(players);
        }

        private static IReadOnlyList<Player> RestorePlayers(List<PlayerSaveData> saved)
        {
            if (saved == null)
            {
                return null;
            }

            var players = new List<Player>(saved.Count);
            foreach (PlayerSaveData p in saved)
            {
                players.Add(new Player(
                    new PlayerId(p.Id), p.Name, p.Nationality, ReadRole(p), p.Age,
                    FromData(p.Attributes), (byte)p.HiddenPotential, RestoreTraits(p.Traits)));
            }

            return players;
        }

        /// <summary>
        /// Resolves a persisted role name. Restore's whole job is mapping a document the caller has already
        /// had accepted, and <see cref="SaveMigrator.Migrate"/> is the gate that turns an unreadable role
        /// into an expected <c>Result</c> failure on the load path — so by the time a document reaches here
        /// an unparseable name means a caller skipped the gate, i.e. a bug in this codebase rather than a bad
        /// file. That is the fail-fast half of CONVENTIONS §4, and it is deliberately not a silent default:
        /// defaulting would hand the player a squad quietly re-roled to goalkeeper.
        /// </summary>
        private static PlayerRole ReadRole(PlayerSaveData player)
        {
            if (!PersistedPlayerRole.TryParse(player.RoleName, out PlayerRole role))
            {
                throw new InvalidOperationException(
                    $"Player {player.Id} ('{player.Name}') has the unreadable role '{player.RoleName}'. " +
                    "Run SaveMigrator.Migrate before Restore — it converts a pre-v5 ordinal and reports a bad role as a Result failure.");
            }

            return role;
        }

        private static List<string> CaptureTraits(Player player)
        {
            if (player.Traits.Count == 0)
            {
                return null;
            }

            var slugs = new List<string>(player.Traits.Count);
            foreach (TraitId id in player.Traits)
            {
                slugs.Add(id.Value);
            }

            return slugs;
        }

        // Null (a v3 save, or a trait-less player) restores as no traits; unknown slugs are kept — the
        // catalog resolves ids on use and ignores what it does not define.
        private static IReadOnlyList<TraitId> RestoreTraits(List<string> slugs)
        {
            if (slugs == null || slugs.Count == 0)
            {
                return null;
            }

            var ids = new List<TraitId>(slugs.Count);
            foreach (string slug in slugs)
            {
                ids.Add(new TraitId(slug));
            }

            return ids;
        }

        private static AttributesSaveData ToData(Attributes a)
        {
            return new AttributesSaveData
            {
                Finishing = a.Finishing,
                Technique = a.Technique,
                FirstTouch = a.FirstTouch,
                Dribbling = a.Dribbling,
                Passing = a.Passing,
                Crossing = a.Crossing,
                Heading = a.Heading,
                LongShots = a.LongShots,
                Marking = a.Marking,
                Tackling = a.Tackling,
                Penalties = a.Penalties,
                FreeKicks = a.FreeKicks,
                Corners = a.Corners,
                LongThrows = a.LongThrows,
                Pace = a.Pace,
                Acceleration = a.Acceleration,
                Stamina = a.Stamina,
                Strength = a.Strength,
                Agility = a.Agility,
                Jumping = a.Jumping,
                Balance = a.Balance,
                Positioning = a.Positioning,
                Reflexes = a.Reflexes,
                Handling = a.Handling,
                AerialReach = a.AerialReach,
                CommandOfArea = a.CommandOfArea,
                OneOnOnes = a.OneOnOnes,
                Kicking = a.Kicking,
                GkPositioning = a.GkPositioning,
            };
        }

        private static Attributes FromData(AttributesSaveData d)
        {
            return new Attributes
            {
                Finishing = d.Finishing,
                Technique = d.Technique,
                FirstTouch = d.FirstTouch,
                Dribbling = d.Dribbling,
                Passing = d.Passing,
                Crossing = d.Crossing,
                Heading = d.Heading,
                LongShots = d.LongShots,
                Marking = d.Marking,
                Tackling = d.Tackling,
                Penalties = d.Penalties,
                FreeKicks = d.FreeKicks,
                Corners = d.Corners,
                LongThrows = d.LongThrows,
                Pace = d.Pace,
                Acceleration = d.Acceleration,
                Stamina = d.Stamina,
                Strength = d.Strength,
                Agility = d.Agility,
                Jumping = d.Jumping,
                Balance = d.Balance,
                Positioning = d.Positioning,
                Reflexes = d.Reflexes,
                Handling = d.Handling,
                AerialReach = d.AerialReach,
                CommandOfArea = d.CommandOfArea,
                OneOnOnes = d.OneOnOnes,
                Kicking = d.Kicking,
                GkPositioning = d.GkPositioning,
            };
        }

        // ----- The run's memory (v7) ----------------------------------------------------------------------

        // Journeys are written whole. They are small — a few dozen careers of a few moments each, against a
        // world of 50,000 players — and they are the one thing in the document that cannot be re-derived
        // from anything else, because a moment is a reading of a match that has already been thrown away.
        private static List<JourneySaveData> CaptureJourneys(IReadOnlyList<PlayerJourney> journeys)
        {
            if (journeys == null || journeys.Count == 0)
            {
                return null;
            }

            var captured = new List<JourneySaveData>(journeys.Count);
            for (int i = 0; i < journeys.Count; i++)
            {
                PlayerJourney journey = journeys[i];
                IReadOnlyList<CareerMoment> moments = journey.Moments;
                var written = new List<MomentSaveData>(moments.Count);
                for (int m = 0; m < moments.Count; m++)
                {
                    CareerMoment moment = moments[m];
                    written.Add(new MomentSaveData
                    {
                        Kind = PersistedCareerMomentKind.ToName(moment.Kind),
                        ClubIndex = moment.Club.Value,
                        Season = moment.Season,
                        Round = moment.Round,
                        Minute = moment.Minute,
                        Count = moment.Count,
                    });
                }

                IReadOnlyList<PlayerSeason> seasons = journey.Seasons;
                var numbers = new List<int>(seasons.Count);
                var appearances = new List<int>(seasons.Count);
                var goals = new List<int>(seasons.Count);
                for (int y = 0; y < seasons.Count; y++)
                {
                    numbers.Add(seasons[y].Season);
                    appearances.Add(seasons[y].Appearances);
                    goals.Add(seasons[y].Goals);
                }

                captured.Add(new JourneySaveData
                {
                    PlayerId = journey.Player.Value,
                    Name = journey.Name,
                    Appearances = journey.Appearances,
                    Goals = journey.Goals,
                    Moments = written,
                    SeasonNumbers = numbers,
                    SeasonAppearances = appearances,
                    SeasonGoals = goals,
                });
            }

            return captured;
        }

        // Tolerant on the way back (ARCHITECTURE §11): a moment whose kind this build does not know is
        // DROPPED rather than failing the load. The vocabulary is expected to grow, so a save written by a
        // newer build is a real thing a player can have, and losing one line of a story is a better outcome
        // than a run that cannot be opened.
        private static List<PlayerJourney> RestoreJourneys(List<JourneySaveData> journeys)
        {
            if (journeys == null || journeys.Count == 0)
            {
                return null;
            }

            var restored = new List<PlayerJourney>(journeys.Count);
            for (int i = 0; i < journeys.Count; i++)
            {
                JourneySaveData saved = journeys[i];
                var moments = new List<CareerMoment>(saved.Moments != null ? saved.Moments.Count : 0);
                if (saved.Moments != null)
                {
                    for (int m = 0; m < saved.Moments.Count; m++)
                    {
                        MomentSaveData moment = saved.Moments[m];
                        if (!PersistedCareerMomentKind.TryParse(moment.Kind, out CareerMomentKind kind))
                        {
                            continue;
                        }

                        moments.Add(new CareerMoment(
                            kind,
                            new PlayerId(saved.PlayerId),
                            new ClubId(moment.ClubIndex),
                            moment.Season,
                            moment.Round,
                            moment.Minute,
                            moment.Count));
                    }
                }

                restored.Add(PlayerJourney.Restore(
                    new PlayerId(saved.PlayerId), saved.Name, saved.Appearances, saved.Goals, moments,
                    RestoreSeasons(saved)));
            }

            return restored;
        }

        // Tolerant of a shorter row than its siblings: the three lists are written together, so a mismatch
        // means an edited file rather than a version this build should try to interpret (ARCHITECTURE §11).
        private static List<PlayerSeason> RestoreSeasons(JourneySaveData saved)
        {
            List<int> numbers = saved.SeasonNumbers;
            if (numbers == null || saved.SeasonAppearances == null || saved.SeasonGoals == null)
            {
                return null;
            }

            var seasons = new List<PlayerSeason>(numbers.Count);
            for (int i = 0; i < numbers.Count && i < saved.SeasonAppearances.Count && i < saved.SeasonGoals.Count; i++)
            {
                seasons.Add(new PlayerSeason(numbers[i], saved.SeasonAppearances[i], saved.SeasonGoals[i]));
            }

            return seasons;
        }

        private static DevelopmentPeriodSaveData CaptureDevelopment(DevelopmentPeriod period)
        {
            if (period == null || period.IsEmpty)
            {
                return null;
            }

            return new DevelopmentPeriodSaveData
            {
                RoundsSinceTick = period.RoundsPlayed,
                AppearanceIds = new List<int>(period.PlayerIds),
                AppearanceCounts = new List<int>(period.Appearances),
            };
        }

        private static DevelopmentPeriod RestoreDevelopment(DevelopmentPeriodSaveData period)
        {
            if (period == null || period.RoundsSinceTick <= 0)
            {
                return null;
            }

            return new DevelopmentPeriod(period.RoundsSinceTick, period.AppearanceIds, period.AppearanceCounts);
        }
    }
}
