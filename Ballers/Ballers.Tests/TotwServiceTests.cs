using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Services;
using Ballers.Tests.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Moq;

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

        // No player has a card in these tests.
        private static TotwService Build(ApplicationDbContext db)
        {
            var cards = new Mock<IPlayerCardService>();
            cards.Setup(c => c.GetPublicCardsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, (Ballers.Shared.PlayerAttributesDto Attributes, string Tier)>());
            return new(db, new MemoryCache(new MemoryCacheOptions()), cards.Object);
        }

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

        [Fact]
        public async Task GetCurrent_GivesEachWinnerTheirResult()
        {
            var db = await SeedBase(nameof(GetCurrent_GivesEachWinnerTheirResult));
            var fixture = await db.Fixtures.FindAsync(1);
            fixture!.HomeScore = 3;
            fixture.AwayScore = 1;
            db.Players.AddRange(
                new Player { Id = 1, Name = "HomeHero", TeamId = 1, Position = "FWD" },
                new Player { Id = 2, Name = "AwayHero", TeamId = 2, Position = "GK" });
            db.FixturePlayerStats.AddRange(
                new FixturePlayerStat { PlayerId = 1, FixtureId = 1, ManOfTheMatch = true, Goals = 3 },
                new FixturePlayerStat { PlayerId = 2, FixtureId = 1, ManOfTheMatch = true });
            await db.SaveChangesAsync();

            var totw = await Build(db).GetCurrentAsync();

            var home = totw!.Players.Single(p => p.PlayerId == 1);
            Assert.Equal(("Unity Football Club", 3, 1), (home.OpponentName, home.TeamScore, home.OpponentScore));
            var away = totw.Players.Single(p => p.PlayerId == 2);
            Assert.Equal(("Blackpool United", 1, 3), (away.OpponentName, away.TeamScore, away.OpponentScore));
        }

        [Fact]
        public async Task GetCurrent_SummarisesTheWeek()
        {
            var db = await SeedBase(nameof(GetCurrent_SummarisesTheWeek));
            var fixture = await db.Fixtures.FindAsync(1);
            fixture!.HomeScore = 2;
            fixture.AwayScore = 2;
            db.Players.AddRange(
                new Player { Id = 1, Name = "Winner", TeamId = 1, Position = "MID" },
                new Player { Id = 2, Name = "Scorer", TeamId = 2, Position = "FWD" });
            db.FixturePlayerStats.AddRange(
                new FixturePlayerStat { PlayerId = 1, FixtureId = 1, ManOfTheMatch = true },
                new FixturePlayerStat { PlayerId = 2, FixtureId = 1, Goals = 2 });
            await db.SaveChangesAsync();

            var summary = (await Build(db).GetCurrentAsync())!.Summary!;

            Assert.Equal(1, summary.Games);
            Assert.Equal(4, summary.Goals);
            // The week's top scorer need not be a Man of the Match.
            Assert.Equal(("Scorer", "Unity Football Club", 2),
                (summary.TopScorerName, summary.TopScorerTeam, summary.TopScorerGoals));
        }

        [Fact]
        public async Task Appearances_CountsWeeksNotGames()
        {
            var db = await SeedBase(nameof(Appearances_CountsWeeksNotGames));
            db.Fixtures.AddRange(
                // A second game in the same week, and a game the week after.
                new Fixture { Id = 2, HomeTeamId = 2, AwayTeamId = 1, SeasonId = 1, IsPlayed = true, MatchNumber = 4,
                              WindowStart = Week, WindowEnd = Week.AddDays(7) },
                new Fixture { Id = 3, HomeTeamId = 1, AwayTeamId = 2, SeasonId = 1, IsPlayed = true, MatchNumber = 5,
                              WindowStart = Week.AddDays(7), WindowEnd = Week.AddDays(14) });
            db.Players.AddRange(
                new Player { Id = 1, Name = "Regular", TeamId = 1, Position = "MID" },
                new Player { Id = 2, Name = "OneOff", TeamId = 2, Position = "DEF" });
            db.FixturePlayerStats.AddRange(
                new FixturePlayerStat { PlayerId = 1, FixtureId = 1, ManOfTheMatch = true },
                new FixturePlayerStat { PlayerId = 1, FixtureId = 2, ManOfTheMatch = true },
                new FixturePlayerStat { PlayerId = 1, FixtureId = 3, ManOfTheMatch = true },
                new FixturePlayerStat { PlayerId = 2, FixtureId = 3, ManOfTheMatch = true });
            await db.SaveChangesAsync();
            var svc = Build(db);

            var table = await svc.GetAppearancesAsync();

            Assert.Equal(("Regular", 2), (table[0].Name, table[0].Count));
            Assert.Equal(("OneOff", 1), (table[1].Name, table[1].Count));

            // On a card, the count runs up to that week only.
            var firstWeek = await svc.GetWeekAsync(1);
            Assert.Equal(1, firstWeek!.Players.First(p => p.PlayerId == 1).TotwAppearances);
            var secondWeek = await svc.GetWeekAsync(2);
            Assert.Equal(2, secondWeek!.Players.Single(p => p.PlayerId == 1).TotwAppearances);
        }
    }
}
