using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Services;
using Ballers.Tests.Helpers;

namespace Ballers.Tests
{
    public class StatsServiceTests
    {
        private static async Task<ApplicationDbContext> SeedBase(string dbName)
        {
            var db = DbContextFactory.Create(dbName);
            db.Teams.Add(new Team { Id = 1, Name = "FC Test" });
            db.Seasons.Add(new Season
            {
                Id = 1, SeasonNumber = 1,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow.AddMonths(5)
            });
            db.Fixtures.Add(new Fixture
            {
                Id = 1, HomeTeamId = 1, AwayTeamId = 1,
                SeasonId = 1, IsPlayed = true
            });
            await db.SaveChangesAsync();
            return db;
        }

        // ── GetTopScorersAsync ──────────────────────────────────────────

        [Fact]
        public async Task GetTopScorers_OrderedByGoalsDescending()
        {
            var db = await SeedBase(nameof(GetTopScorers_OrderedByGoalsDescending));
            db.Players.AddRange(
                new Player { Id = 1, Name = "Alice", TeamId = 1 },
                new Player { Id = 2, Name = "Bob", TeamId = 1 });
            db.FixturePlayerStats.AddRange(
                new FixturePlayerStat { PlayerId = 1, FixtureId = 1, Goals = 3 },
                new FixturePlayerStat { PlayerId = 2, FixtureId = 1, Goals = 7 });
            await db.SaveChangesAsync();

            var svc = new StatsService(db);
            var result = await svc.GetTopScorersAsync();

            Assert.Equal("Bob", result[0].Name);
            Assert.Equal(7, result[0].Goals);
        }

        [Fact]
        public async Task GetTopScorers_ExcludesOtherSeasons()
        {
            var db = await SeedBase(nameof(GetTopScorers_ExcludesOtherSeasons));
            db.Seasons.Add(new Season
            {
                Id = 2, SeasonNumber = 0,
                StartDate = DateTime.UtcNow.AddYears(-2),
                EndDate = DateTime.UtcNow.AddYears(-1)
            });
            db.Fixtures.Add(new Fixture { Id = 99, HomeTeamId = 1, AwayTeamId = 1, SeasonId = 2 });
            db.Players.Add(new Player { Id = 1, Name = "OldStar", TeamId = 1 });
            db.FixturePlayerStats.Add(
                new FixturePlayerStat { PlayerId = 1, FixtureId = 99, Goals = 100 });
            await db.SaveChangesAsync();

            var svc = new StatsService(db);
            var result = await svc.GetTopScorersAsync();
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetTopScorers_ReturnsAtMostFive()
        {
            var db = await SeedBase(nameof(GetTopScorers_ReturnsAtMostFive));
            for (int i = 1; i <= 8; i++)
            {
                db.Players.Add(new Player { Id = i, Name = $"P{i}", TeamId = 1 });
                db.FixturePlayerStats.Add(
                    new FixturePlayerStat { PlayerId = i, FixtureId = 1, Goals = i });
            }
            await db.SaveChangesAsync();

            var svc = new StatsService(db);
            var result = await svc.GetTopScorersAsync();
            Assert.Equal(5, result.Count);
        }

        // ── GetTopAssistsAsync ──────────────────────────────────────────

        [Fact]
        public async Task GetTopAssists_OrderedByAssistsDescending()
        {
            var db = await SeedBase(nameof(GetTopAssists_OrderedByAssistsDescending));
            db.Players.AddRange(
                new Player { Id = 1, Name = "Alice", TeamId = 1 },
                new Player { Id = 2, Name = "Bob", TeamId = 1 });
            db.FixturePlayerStats.AddRange(
                new FixturePlayerStat { PlayerId = 1, FixtureId = 1, Assists = 1 },
                new FixturePlayerStat { PlayerId = 2, FixtureId = 1, Assists = 5 });
            await db.SaveChangesAsync();

            var svc = new StatsService(db);
            var result = await svc.GetTopAssistsAsync();

            Assert.Equal("Bob", result[0].Name);
            Assert.Equal(5, result[0].Assists);
        }

        // ── GetTopMotmAsync ─────────────────────────────────────────────

        [Fact]
        public async Task GetTopMotm_OrderedByMotmCountDescending()
        {
            var db = await SeedBase(nameof(GetTopMotm_OrderedByMotmCountDescending));
            var f2 = new Fixture { Id = 2, HomeTeamId = 1, AwayTeamId = 1, SeasonId = 1, IsPlayed = true };
            db.Fixtures.Add(f2);
            db.Players.AddRange(
                new Player { Id = 1, Name = "Consistent", TeamId = 1 },
                new Player { Id = 2, Name = "OneHit", TeamId = 1 });
            db.FixturePlayerStats.AddRange(
                new FixturePlayerStat { PlayerId = 1, FixtureId = 1, ManOfTheMatch = true },
                new FixturePlayerStat { PlayerId = 1, FixtureId = 2, ManOfTheMatch = true },
                new FixturePlayerStat { PlayerId = 2, FixtureId = 1, ManOfTheMatch = true });
            await db.SaveChangesAsync();

            var svc = new StatsService(db);
            var result = await svc.GetTopMotmAsync();

            Assert.Equal("Consistent", result[0].Name);
            Assert.Equal(2, result[0].Motm);
        }

        // ── GetWinLossAsync ─────────────────────────────────────────────

        private static async Task<ApplicationDbContext> SeedWinLossBase(string dbName)
        {
            var db = DbContextFactory.Create(dbName);
            db.Teams.AddRange(
                new Team { Id = 1, Name = "Team A" },
                new Team { Id = 2, Name = "Team B" });
            db.Seasons.Add(new Season
            {
                Id = 1, SeasonNumber = 1,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow.AddMonths(5)
            });
            await db.SaveChangesAsync();
            return db;
        }

        [Fact]
        public async Task GetWinLoss_NoFixtures_AllZero()
        {
            var db = await SeedWinLossBase(nameof(GetWinLoss_NoFixtures_AllZero));
            var svc = new StatsService(db);
            var result = await svc.GetWinLossAsync(1);

            Assert.Equal(0, result.Wins);
            Assert.Equal(0, result.Losses);
            Assert.Equal(0, result.Draws);
        }

        [Fact]
        public async Task GetWinLoss_CorrectCounts()
        {
            var db = await SeedWinLossBase(nameof(GetWinLoss_CorrectCounts));

            // Win (home, 3-1)
            db.Fixtures.Add(new Fixture { Id = 1, HomeTeamId = 1, AwayTeamId = 2, SeasonId = 1, IsPlayed = true, HomeScore = 3, AwayScore = 1, WindowStart = DateTime.UtcNow, WindowEnd = DateTime.UtcNow });
            // Loss (away, 2-1 for opponent = loss for team 1)
            db.Fixtures.Add(new Fixture { Id = 2, HomeTeamId = 2, AwayTeamId = 1, SeasonId = 1, IsPlayed = true, HomeScore = 2, AwayScore = 1, WindowStart = DateTime.UtcNow, WindowEnd = DateTime.UtcNow });
            // Draw
            db.Fixtures.Add(new Fixture { Id = 3, HomeTeamId = 1, AwayTeamId = 2, SeasonId = 1, IsPlayed = true, HomeScore = 2, AwayScore = 2, WindowStart = DateTime.UtcNow, WindowEnd = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var svc = new StatsService(db);
            var result = await svc.GetWinLossAsync(1);

            Assert.Equal(1, result.Wins);
            Assert.Equal(1, result.Losses);
            Assert.Equal(1, result.Draws);
        }

        // ── GetLeaderboardAsync ─────────────────────────────────────────

        private static async Task<ApplicationDbContext> SeedLeaderboardBase(string dbName)
        {
            var db = DbContextFactory.Create(dbName);
            db.Teams.AddRange(
                new Team { Id = 1, Name = "Team A" },
                new Team { Id = 2, Name = "Team B" });
            db.Seasons.AddRange(
                new Season { Id = 1, SeasonNumber = 1, StartDate = DateTime.UtcNow.AddMonths(-2), EndDate = DateTime.UtcNow.AddMonths(4) },
                new Season { Id = 2, SeasonNumber = 2, StartDate = DateTime.UtcNow.AddYears(-2), EndDate = DateTime.UtcNow.AddYears(-1) });
            db.Fixtures.AddRange(
                new Fixture { Id = 1, HomeTeamId = 1, AwayTeamId = 2, SeasonId = 1, IsPlayed = true, HomeScore = 2, AwayScore = 1 },
                new Fixture { Id = 2, HomeTeamId = 1, AwayTeamId = 2, SeasonId = 1, IsPlayed = true, HomeScore = 0, AwayScore = 0 },
                new Fixture { Id = 3, HomeTeamId = 1, AwayTeamId = 2, SeasonId = 2, IsPlayed = true, HomeScore = 1, AwayScore = 0 });
            await db.SaveChangesAsync();
            return db;
        }

        [Fact]
        public async Task GetLeaderboard_NoSeasonFilter_ReturnsAllPlayedStats()
        {
            var db = await SeedLeaderboardBase(nameof(GetLeaderboard_NoSeasonFilter_ReturnsAllPlayedStats));
            db.Players.Add(new Player { Id = 1, Name = "AllStar", TeamId = 1, Position = "MID" });
            db.FixturePlayerStats.AddRange(
                new FixturePlayerStat { PlayerId = 1, FixtureId = 1, Goals = 1 },
                new FixturePlayerStat { PlayerId = 1, FixtureId = 3, Goals = 2 });
            await db.SaveChangesAsync();

            var svc = new StatsService(db);
            var result = await svc.GetLeaderboardAsync(null);

            Assert.Single(result);
            Assert.Equal(3, result[0].Goals);
            Assert.Equal(2, result[0].Appearances);
        }

        [Fact]
        public async Task GetLeaderboard_SeasonFilter_OnlyReturnsThatSeason()
        {
            var db = await SeedLeaderboardBase(nameof(GetLeaderboard_SeasonFilter_OnlyReturnsThatSeason));
            db.Players.Add(new Player { Id = 1, Name = "P1", TeamId = 1, Position = "FWD" });
            db.FixturePlayerStats.AddRange(
                new FixturePlayerStat { PlayerId = 1, FixtureId = 1, Goals = 2 },  // season 1
                new FixturePlayerStat { PlayerId = 1, FixtureId = 3, Goals = 5 }); // season 2
            await db.SaveChangesAsync();

            var svc = new StatsService(db);
            var result = await svc.GetLeaderboardAsync(seasonId: 1);

            Assert.Single(result);
            Assert.Equal(2, result[0].Goals);
        }

        [Fact]
        public async Task GetLeaderboard_OrderedByGoalsThenAssists()
        {
            var db = await SeedLeaderboardBase(nameof(GetLeaderboard_OrderedByGoalsThenAssists));
            db.Players.AddRange(
                new Player { Id = 1, Name = "Scorer", TeamId = 1, Position = "FWD" },
                new Player { Id = 2, Name = "Creator", TeamId = 1, Position = "MID" });
            db.FixturePlayerStats.AddRange(
                new FixturePlayerStat { PlayerId = 1, FixtureId = 1, Goals = 3, Assists = 0 },
                new FixturePlayerStat { PlayerId = 2, FixtureId = 1, Goals = 1, Assists = 4 });
            await db.SaveChangesAsync();

            var svc = new StatsService(db);
            var result = await svc.GetLeaderboardAsync(null);

            Assert.Equal("Scorer", result[0].Name);
            Assert.Equal("Creator", result[1].Name);
        }

        [Fact]
        public async Task GetLeaderboard_EqualGoals_OrderedByAssistsDescending()
        {
            var db = await SeedLeaderboardBase(nameof(GetLeaderboard_EqualGoals_OrderedByAssistsDescending));
            db.Players.AddRange(
                new Player { Id = 1, Name = "LowAssist", TeamId = 1, Position = "FWD" },
                new Player { Id = 2, Name = "HighAssist", TeamId = 1, Position = "MID" });
            db.FixturePlayerStats.AddRange(
                new FixturePlayerStat { PlayerId = 1, FixtureId = 1, Goals = 2, Assists = 1 },
                new FixturePlayerStat { PlayerId = 2, FixtureId = 1, Goals = 2, Assists = 3 });
            await db.SaveChangesAsync();

            var svc = new StatsService(db);
            var result = await svc.GetLeaderboardAsync(null);

            Assert.Equal("HighAssist", result[0].Name);
        }

        [Fact]
        public async Task GetLeaderboard_GkGetsCleanSheet_WhenOpponentScoresZero()
        {
            var db = await SeedLeaderboardBase(nameof(GetLeaderboard_GkGetsCleanSheet_WhenOpponentScoresZero));
            // fixture 2 is 0-0 — GK for home team should get clean sheet
            db.Players.Add(new Player { Id = 1, Name = "Keeper", TeamId = 1, Position = "GK" });
            db.FixturePlayerStats.Add(new FixturePlayerStat { PlayerId = 1, FixtureId = 2 });
            await db.SaveChangesAsync();

            var svc = new StatsService(db);
            var result = await svc.GetLeaderboardAsync(null);

            Assert.Equal(1, result[0].CleanSheets);
        }

        [Fact]
        public async Task GetLeaderboard_GkNoCleanSheet_WhenOpponentScores()
        {
            var db = await SeedLeaderboardBase(nameof(GetLeaderboard_GkNoCleanSheet_WhenOpponentScores));
            // fixture 1 is 2-1 — away GK (team 2) conceded 2, no clean sheet
            db.Players.Add(new Player { Id = 1, Name = "AwayKeeper", TeamId = 2, Position = "GK" });
            db.FixturePlayerStats.Add(new FixturePlayerStat { PlayerId = 1, FixtureId = 1 });
            await db.SaveChangesAsync();

            var svc = new StatsService(db);
            var result = await svc.GetLeaderboardAsync(null);

            Assert.Equal(0, result[0].CleanSheets);
        }

        [Fact]
        public async Task GetLeaderboard_NonGk_AlwaysZeroCleanSheets()
        {
            var db = await SeedLeaderboardBase(nameof(GetLeaderboard_NonGk_AlwaysZeroCleanSheets));
            // fixture 2 is 0-0 but player is a striker
            db.Players.Add(new Player { Id = 1, Name = "Striker", TeamId = 1, Position = "FWD" });
            db.FixturePlayerStats.Add(new FixturePlayerStat { PlayerId = 1, FixtureId = 2 });
            await db.SaveChangesAsync();

            var svc = new StatsService(db);
            var result = await svc.GetLeaderboardAsync(null);

            Assert.Equal(0, result[0].CleanSheets);
        }

        [Fact]
        public async Task GetLeaderboard_OnlyIncludesPlayedFixtures()
        {
            var db = DbContextFactory.Create(nameof(GetLeaderboard_OnlyIncludesPlayedFixtures));
            db.Teams.Add(new Team { Id = 1, Name = "T1" });
            db.Fixtures.Add(new Fixture { Id = 1, HomeTeamId = 1, AwayTeamId = 1, SeasonId = 1, IsPlayed = false });
            db.Players.Add(new Player { Id = 1, Name = "Ghost", TeamId = 1, Position = "MID" });
            db.FixturePlayerStats.Add(new FixturePlayerStat { PlayerId = 1, FixtureId = 1, Goals = 5 });
            await db.SaveChangesAsync();

            var svc = new StatsService(db);
            var result = await svc.GetLeaderboardAsync(null);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetLeaderboard_AggregatesMultipleAppearances()
        {
            var db = await SeedLeaderboardBase(nameof(GetLeaderboard_AggregatesMultipleAppearances));
            db.Players.Add(new Player { Id = 1, Name = "Regular", TeamId = 1, Position = "MID" });
            db.FixturePlayerStats.AddRange(
                new FixturePlayerStat { PlayerId = 1, FixtureId = 1, Goals = 1, Assists = 2, YellowCards = true },
                new FixturePlayerStat { PlayerId = 1, FixtureId = 2, Goals = 2, Assists = 1, YellowCards = false });
            await db.SaveChangesAsync();

            var svc = new StatsService(db);
            var result = await svc.GetLeaderboardAsync(null);

            Assert.Equal(3, result[0].Goals);
            Assert.Equal(3, result[0].Assists);
            Assert.Equal(2, result[0].Appearances);
            Assert.Equal(1, result[0].YellowCards);
        }

        [Fact]
        public async Task GetLeaderboard_RedCard_Counted()
        {
            var db = await SeedLeaderboardBase(nameof(GetLeaderboard_RedCard_Counted));
            db.Players.Add(new Player { Id = 1, Name = "Hothead", TeamId = 1, Position = "DEF" });
            db.FixturePlayerStats.AddRange(
                new FixturePlayerStat { PlayerId = 1, FixtureId = 1, RedCard = true },
                new FixturePlayerStat { PlayerId = 1, FixtureId = 2, RedCard = false });
            await db.SaveChangesAsync();

            var svc = new StatsService(db);
            var result = await svc.GetLeaderboardAsync(null);

            Assert.Equal(1, result[0].RedCards);
        }

        [Fact]
        public async Task GetLeaderboard_TeamName_PopulatedFromPlayer()
        {
            var db = await SeedLeaderboardBase(nameof(GetLeaderboard_TeamName_PopulatedFromPlayer));
            db.Players.Add(new Player { Id = 1, Name = "P1", TeamId = 2, Position = "FWD" });
            db.FixturePlayerStats.Add(new FixturePlayerStat { PlayerId = 1, FixtureId = 1, Goals = 1 });
            await db.SaveChangesAsync();

            var svc = new StatsService(db);
            var result = await svc.GetLeaderboardAsync(null);

            Assert.Equal("Team B", result[0].Team);
        }
    }
}
