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
    }
}
