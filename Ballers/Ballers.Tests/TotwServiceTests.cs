using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Services;
using Ballers.Tests.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace Ballers.Tests
{
    public class TotwServiceTests
    {
        private static readonly DateTime Week = new(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);

        private static async Task<ApplicationDbContext> SeedBase(string dbName)
        {
            var db = DbContextFactory.Create(dbName);
            db.Teams.AddRange(
                new Team { Id = 1, Name = "Blackpool United" },
                new Team { Id = 2, Name = "Unity Football Club" });
            db.Seasons.Add(new Season
            {
                Id = 1, SeasonNumber = 1, IsActive = true,
                StartDate = Week.AddMonths(-1),
                EndDate = Week.AddMonths(5)
            });
            db.Fixtures.Add(new Fixture
            {
                Id = 1, HomeTeamId = 1, AwayTeamId = 2,
                SeasonId = 1, IsPlayed = true, MatchNumber = 3,
                WindowStart = Week, WindowEnd = Week.AddDays(7)
            });
            await db.SaveChangesAsync();
            return db;
        }

        private static TotwService Build(ApplicationDbContext db)
            => new(db, new MemoryCache(new MemoryCacheOptions()));

        [Fact]
        public async Task GetCurrent_IncludesGoalkeeperManOfTheMatch()
        {
            var db = await SeedBase(nameof(GetCurrent_IncludesGoalkeeperManOfTheMatch));
            db.Players.AddRange(
                new Player { Id = 1, Name = "Keeper", TeamId = 1, Position = "GK" },
                new Player { Id = 2, Name = "Striker", TeamId = 2, Position = "FWD" });
            db.FixturePlayerStats.AddRange(
                new FixturePlayerStat { PlayerId = 1, FixtureId = 1, ManOfTheMatch = true },
                new FixturePlayerStat { PlayerId = 2, FixtureId = 1, ManOfTheMatch = true, Goals = 2 });
            await db.SaveChangesAsync();

            var totw = await Build(db).GetCurrentAsync();

            Assert.NotNull(totw);
            Assert.Equal(2, totw!.Players.Count);
            var keeper = Assert.Single(totw.Players, p => p.PlayerId == 1);
            Assert.True(keeper.IsGoalkeeper);
        }

        [Fact]
        public async Task GetCurrent_ExcludesPlayersWithoutManOfTheMatch()
        {
            var db = await SeedBase(nameof(GetCurrent_ExcludesPlayersWithoutManOfTheMatch));
            db.Players.AddRange(
                new Player { Id = 1, Name = "Winner", TeamId = 1, Position = "MID" },
                new Player { Id = 2, Name = "Bystander", TeamId = 2, Position = "DEF" });
            db.FixturePlayerStats.AddRange(
                new FixturePlayerStat { PlayerId = 1, FixtureId = 1, ManOfTheMatch = true },
                new FixturePlayerStat { PlayerId = 2, FixtureId = 1, ManOfTheMatch = false });
            await db.SaveChangesAsync();

            var totw = await Build(db).GetCurrentAsync();

            Assert.NotNull(totw);
            var player = Assert.Single(totw!.Players);
            Assert.Equal(1, player.PlayerId);
            Assert.False(player.IsGoalkeeper);
        }

        [Fact]
        public async Task GetCurrent_ReturnsEveryWinnerWhenWeekHasMoreThanElevenFixtures()
        {
            var db = await SeedBase(nameof(GetCurrent_ReturnsEveryWinnerWhenWeekHasMoreThanElevenFixtures));
            // 12 fixtures in the one week, one winner each — comfortably past
            // the old ten-slot cap.
            for (var i = 2; i <= 12; i++)
            {
                db.Fixtures.Add(new Fixture
                {
                    Id = i, HomeTeamId = 1, AwayTeamId = 2,
                    SeasonId = 1, IsPlayed = true, MatchNumber = 3,
                    WindowStart = Week, WindowEnd = Week.AddDays(7)
                });
            }
            for (var i = 1; i <= 12; i++)
            {
                db.Players.Add(new Player { Id = i, Name = $"Winner {i}", TeamId = 1, Position = "MID" });
                db.FixturePlayerStats.Add(
                    new FixturePlayerStat { PlayerId = i, FixtureId = i, ManOfTheMatch = true });
            }
            await db.SaveChangesAsync();

            var totw = await Build(db).GetCurrentAsync();

            Assert.NotNull(totw);
            Assert.Equal(12, totw!.Players.Count);
        }

        [Fact]
        public async Task GetCurrent_ReturnsNullWhenNoManOfTheMatchAwarded()
        {
            var db = await SeedBase(nameof(GetCurrent_ReturnsNullWhenNoManOfTheMatchAwarded));
            db.Players.Add(new Player { Id = 1, Name = "Winner", TeamId = 1, Position = "MID" });
            db.FixturePlayerStats.Add(
                new FixturePlayerStat { PlayerId = 1, FixtureId = 1, ManOfTheMatch = false });
            await db.SaveChangesAsync();

            Assert.Null(await Build(db).GetCurrentAsync());
        }
    }
}
