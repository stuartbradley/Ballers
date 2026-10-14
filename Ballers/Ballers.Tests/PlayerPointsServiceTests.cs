using Ballers.API.BackgroundServices;
using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Services;
using Ballers.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ballers.Tests
{
    public class PlayerPointsServiceTests
    {
        private static LeagueSetting Settings() => new()
        {
            Id = 1,
            AttributeCap = 99,
            SignupPointsGrant = 80,
            PointsPerAppearance = 1,
            PointsPerWin = 1,
            PointsPerGoal = 3,
            PointsPerAssist = 2,
            PointsPerCleanSheet = 3,
            PointsPerMotm = 5,
            SilverOverallThreshold = 20,
            GoldOverallThreshold = 35
        };

        /// <summary>
        /// Two teams, one played fixture that team 1 won 2-0, and a single player
        /// on team 1 who has a card. Callers add the stat row they want to test.
        /// </summary>
        private static async Task<ApplicationDbContext> SeedBase(
            string dbName,
            string position = "FWD",
            int homeScore = 2,
            int awayScore = 0,
            bool isPlayed = true)
        {
            var db = DbContextFactory.Create(dbName);

            db.Teams.AddRange(
                new Team { Id = 1, Name = "Team A" },
                new Team { Id = 2, Name = "Team B" });
            db.Seasons.Add(new Season
            {
                Id = 1,
                SeasonNumber = 1,
                IsActive = true,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow.AddMonths(1)
            });
            db.Players.Add(new Player { Id = 1, Name = "Alex", Number = 9, TeamId = 1, Position = position });
            db.PlayerCards.Add(new PlayerCardStats
            {
                Id = 1, PlayerId = 1, UnspentPoints = 80, TotalPointsEarned = 80
            });
            db.Fixtures.Add(new Fixture
            {
                Id = 1,
                SeasonId = 1,
                HomeTeamId = 1,
                AwayTeamId = 2,
                HomeScore = homeScore,
                AwayScore = awayScore,
                IsPlayed = isPlayed,
                MatchNumber = 1,
                Kickoff = DateTime.UtcNow.AddDays(-1)
            });
            db.LeagueSettings.Add(Settings());

            await db.SaveChangesAsync();
            return db;
        }

        private static PlayerPointsService Service(ApplicationDbContext db)
            => new(db, new LeagueSettingsService(db),
                NullLogger<PlayerPointsService>.Instance);

        private static async Task AddStat(
            ApplicationDbContext db,
            int goals = 0,
            int assists = 0,
            bool motm = false,
            int ownGoals = 0)
        {
            db.FixturePlayerStats.Add(new FixturePlayerStat
            {
                Id = 1,
                FixtureId = 1,
                PlayerId = 1,
                Goals = goals,
                Assists = assists,
                OwnGoals = ownGoals,
                ManOfTheMatch = motm
            });
            await db.SaveChangesAsync();
        }

        // ── Each kind of point ──────────────────────────────────────────

        [Fact]
        public async Task Appearance_AndWin_AreCredited()
        {
            var db = await SeedBase(nameof(Appearance_AndWin_AreCredited));
            await AddStat(db);

            var result = await Service(db).RunCatchUpAsync();

            // 1 appearance + 1 win, and nothing else.
            Assert.Equal(2, result.PointsAwarded);
            Assert.Equal(82, (await db.PlayerCards.FirstAsync()).UnspentPoints);
        }

        [Fact]
        public async Task Goals_AndAssists_AreCreditedPerEvent()
        {
            var db = await SeedBase(nameof(Goals_AndAssists_AreCreditedPerEvent));
            await AddStat(db, goals: 2, assists: 1);

            var result = await Service(db).RunCatchUpAsync();

            // 1 appearance + 1 win + (2 x 3 goals) + (1 x 2 assists) = 10
            Assert.Equal(10, result.PointsAwarded);
        }

        [Fact]
        public async Task Motm_IsCredited()
        {
            var db = await SeedBase(nameof(Motm_IsCredited));
            await AddStat(db, motm: true);

            var result = await Service(db).RunCatchUpAsync();

            // 1 appearance + 1 win + 5 man of the match
            Assert.Equal(7, result.PointsAwarded);
        }

        [Fact]
        public async Task CleanSheet_IsCreditedToAKeeper()
        {
            var db = await SeedBase(nameof(CleanSheet_IsCreditedToAKeeper), position: "GK");
            await AddStat(db);

            var award = await RunAndGetAward(db);

            Assert.Equal(3, award.CleanSheetPoints);
        }

        [Fact]
        public async Task CleanSheet_IsCreditedToADefender()
        {
            var db = await SeedBase(nameof(CleanSheet_IsCreditedToADefender), position: "DEF");
            await AddStat(db);

            var award = await RunAndGetAward(db);

            Assert.Equal(3, award.CleanSheetPoints);
        }

        [Fact]
        public async Task CleanSheet_IsNotCreditedToAnOutfieldAttacker()
        {
            var db = await SeedBase(nameof(CleanSheet_IsNotCreditedToAnOutfieldAttacker), position: "FWD");
            await AddStat(db);

            var award = await RunAndGetAward(db);

            Assert.Equal(0, award.CleanSheetPoints);
        }

        [Fact]
        public async Task CleanSheet_IsNotCreditedWhenTheTeamConceded()
        {
            var db = await SeedBase(
                nameof(CleanSheet_IsNotCreditedWhenTheTeamConceded),
                position: "GK", homeScore: 2, awayScore: 1);
            await AddStat(db);

            var award = await RunAndGetAward(db);

            Assert.Equal(0, award.CleanSheetPoints);
        }

        [Fact]
        public async Task Win_IsNotCreditedForALoss()
        {
            var db = await SeedBase(nameof(Win_IsNotCreditedForALoss), homeScore: 0, awayScore: 3);
            await AddStat(db);

            var award = await RunAndGetAward(db);

            Assert.Equal(0, award.WinPoints);
            Assert.Equal(1, award.Points); // the appearance only
        }

        [Fact]
        public async Task Win_IsNotCreditedForADraw()
        {
            var db = await SeedBase(nameof(Win_IsNotCreditedForADraw), homeScore: 1, awayScore: 1);
            await AddStat(db);

            var award = await RunAndGetAward(db);

            Assert.Equal(0, award.WinPoints);
        }

        [Fact]
        public async Task OwnGoals_CostNothing()
        {
            var db = await SeedBase(nameof(OwnGoals_CostNothing));
            await AddStat(db, ownGoals: 2);

            var result = await Service(db).RunCatchUpAsync();

            // Still just the appearance and the win — a card never goes backwards.
            Assert.Equal(2, result.PointsAwarded);
        }

        // ── Idempotency ─────────────────────────────────────────────────

        [Fact]
        public async Task RunningTwice_CreditsOnce()
        {
            var db = await SeedBase(nameof(RunningTwice_CreditsOnce));
            await AddStat(db, goals: 1);
            var svc = Service(db);

            var first = await svc.RunCatchUpAsync();
            var second = await svc.RunCatchUpAsync();

            Assert.Equal(5, first.PointsAwarded); // appearance + win + goal
            Assert.Equal(0, second.PointsAwarded);
            Assert.Equal(0, second.FixturesCredited);
            Assert.Equal(85, (await db.PlayerCards.FirstAsync()).UnspentPoints);
            Assert.Equal(1, await db.PlayerPointsAwards.CountAsync());
        }

        [Fact]
        public async Task ALateEnteredResult_IsPickedUpByTheNextRun()
        {
            var db = await SeedBase(nameof(ALateEnteredResult_IsPickedUpByTheNextRun));
            await AddStat(db, goals: 1);
            var svc = Service(db);

            await svc.RunCatchUpAsync();

            // A second fixture's result lands after the first run has been and gone.
            db.Fixtures.Add(new Fixture
            {
                Id = 2, SeasonId = 1, HomeTeamId = 1, AwayTeamId = 2,
                HomeScore = 1, AwayScore = 0, IsPlayed = true, MatchNumber = 2
            });
            db.FixturePlayerStats.Add(new FixturePlayerStat
            {
                Id = 2, FixtureId = 2, PlayerId = 1, Goals = 1
            });
            await db.SaveChangesAsync();

            var result = await svc.RunCatchUpAsync();

            Assert.Equal(1, result.FixturesCredited);
            Assert.Equal(5, result.PointsAwarded);
        }

        // ── What is left alone ──────────────────────────────────────────

        [Fact]
        public async Task UnplayedFixtures_AreNotCredited()
        {
            var db = await SeedBase(nameof(UnplayedFixtures_AreNotCredited), isPlayed: false);
            await AddStat(db, goals: 3);

            var result = await Service(db).RunCatchUpAsync();

            Assert.Equal(0, result.PointsAwarded);
            Assert.Equal(0, await db.PlayerPointsAwards.CountAsync());
        }

        [Fact]
        public async Task PlayersWithNoCard_AreSkippedNotCrashedOn()
        {
            var db = await SeedBase(nameof(PlayersWithNoCard_AreSkippedNotCrashedOn));
            db.Players.Add(new Player { Id = 2, Name = "No Login", Number = 4, TeamId = 1, Position = "MID" });
            db.FixturePlayerStats.Add(new FixturePlayerStat
            {
                Id = 2, FixtureId = 1, PlayerId = 2, Goals = 5
            });
            await AddStat(db);

            var result = await Service(db).RunCatchUpAsync();

            // Only the player who has a card is paid; the other is left for
            // whenever they sign up, with no backdated windfall waiting.
            Assert.Equal(1, result.PlayersCredited);
            Assert.Equal(2, result.PointsAwarded);
            Assert.Equal(1, await db.PlayerPointsAwards.CountAsync());
        }

        [Fact]
        public async Task ATransferredPlayer_KeepsTheirOwnStatsButNotTheTeamPoints()
        {
            var db = await SeedBase(nameof(ATransferredPlayer_KeepsTheirOwnStatsButNotTheTeamPoints));
            await AddStat(db, goals: 1);

            // They have since moved to a team that was not in this fixture.
            var player = await db.Players.FirstAsync(p => p.Id == 1);
            player.TeamId = 2;
            db.Fixtures.First().AwayTeamId = 3;
            db.Teams.Add(new Team { Id = 3, Name = "Team C" });
            await db.SaveChangesAsync();

            var award = await RunAndGetAward(db);

            Assert.Equal(3, award.GoalPoints);
            Assert.Equal(1, award.AppearancePoints);
            Assert.Equal(0, award.WinPoints);
            Assert.Equal(0, award.CleanSheetPoints);
        }

        [Fact]
        public async Task NothingToDo_ReportsAnEmptyRun()
        {
            var db = await SeedBase(nameof(NothingToDo_ReportsAnEmptyRun));

            var result = await Service(db).RunCatchUpAsync();

            Assert.Equal(0, result.FixturesCredited);
            Assert.Equal(0, result.PlayersCredited);
            Assert.Equal(0, result.PointsAwarded);
        }

        [Fact]
        public async Task ARun_RecordsWhenItHappened()
        {
            var db = await SeedBase(nameof(ARun_RecordsWhenItHappened));
            await AddStat(db);

            await Service(db).RunCatchUpAsync();

            Assert.NotNull((await db.LeagueSettings.FirstAsync()).LastPointsRunAt);
        }

        // ── The weekly schedule ─────────────────────────────────────────

        [Theory]
        // Monday through Saturday: the days remaining until Sunday 00:00.
        [InlineData("2026-10-12T00:00:00", 6)] // Monday
        [InlineData("2026-10-14T00:00:00", 4)] // Wednesday
        [InlineData("2026-10-17T00:00:00", 1)] // Saturday
        public void NextRun_IsTheComingSundayMidnight(string nowText, int expectedDays)
        {
            var now = DateTime.Parse(nowText);

            var delay = PlayerPointsBackgroundService.TimeUntilNextSundayMidnight(now);

            Assert.Equal(expectedDays, delay.TotalDays);
            Assert.Equal(DayOfWeek.Sunday, now.Add(delay).DayOfWeek);
        }

        [Fact]
        public void NextRun_FromSundayMidnight_IsAFullWeekAway()
        {
            // Exactly on the hour it fires, the next one must be a week out rather
            // than zero, or the run would immediately repeat in a tight loop.
            var delay = PlayerPointsBackgroundService.TimeUntilNextSundayMidnight(
                new DateTime(2026, 10, 18, 0, 0, 0));

            Assert.Equal(7, delay.TotalDays);
        }

        [Fact]
        public void NextRun_LaterOnSunday_IsTheFollowingSunday()
        {
            var delay = PlayerPointsBackgroundService.TimeUntilNextSundayMidnight(
                new DateTime(2026, 10, 18, 9, 30, 0));

            Assert.Equal(DayOfWeek.Sunday, new DateTime(2026, 10, 18, 9, 30, 0).Add(delay).DayOfWeek);
            Assert.True(delay.TotalDays is > 6 and < 7);
        }

        private static async Task<PlayerPointsAward> RunAndGetAward(ApplicationDbContext db)
        {
            await Service(db).RunCatchUpAsync();
            return await db.PlayerPointsAwards.FirstAsync(a => a.PlayerId == 1 && a.FixtureId == 1);
        }
    }
}
