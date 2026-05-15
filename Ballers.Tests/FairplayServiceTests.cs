using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Services;
using Ballers.Models;
using Ballers.Tests.Helpers;

namespace Ballers.Tests
{
    public class FairplayServiceTests
    {
        private static IFairplayService Create(string dbName) =>
            new FairplayService(DbContextFactory.Create(dbName));

        private static ApplicationDbContext Db(string dbName) =>
            DbContextFactory.Create(dbName);

        private static async Task<(ApplicationDbContext db, Team home, Team away, Fixture fixture)>
            Seed(string dbName)
        {
            var db = DbContextFactory.Create(dbName);
            var home = new Team { Id = 1, Name = "Home FC" };
            var away = new Team { Id = 2, Name = "Away FC" };
            var season = new Season
            {
                Id = 1, SeasonNumber = 1,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow.AddMonths(5)
            };
            var fixture = new Fixture { Id = 1, HomeTeamId = 1, AwayTeamId = 2, SeasonId = 1 };
            db.Teams.AddRange(home, away);
            db.Seasons.Add(season);
            db.Fixtures.Add(fixture);
            await db.SaveChangesAsync();
            return (db, home, away, fixture);
        }

        // ── GetRatingsAsync ─────────────────────────────────────────────

        [Fact]
        public async Task GetRatingsAsync_UnknownFixture_ReturnsNull()
        {
            var svc = Create(nameof(GetRatingsAsync_UnknownFixture_ReturnsNull));
            var result = await svc.GetRatingsAsync(99);
            Assert.Null(result);
        }

        [Fact]
        public async Task GetRatingsAsync_NoRatings_ReturnsBothNull()
        {
            var (db, _, _, _) = await Seed(nameof(GetRatingsAsync_NoRatings_ReturnsBothNull));
            var svc = new FairplayService(db);

            var result = await svc.GetRatingsAsync(1);
            Assert.NotNull(result);
            Assert.Null(result.HomeRating);
            Assert.Null(result.AwayRating);
        }

        [Fact]
        public async Task GetRatingsAsync_WithRatings_ReturnsCorrectValues()
        {
            var (db, home, away, fixture) = await Seed(nameof(GetRatingsAsync_WithRatings_ReturnsCorrectValues));
            db.FairplayRatings.AddRange(
                new FairplayRating { FixtureId = fixture.Id, TeamId = home.Id, Rating = 8 },
                new FairplayRating { FixtureId = fixture.Id, TeamId = away.Id, Rating = 6 });
            await db.SaveChangesAsync();

            var svc = new FairplayService(db);
            var result = await svc.GetRatingsAsync(1);

            Assert.Equal(8, result!.HomeRating);
            Assert.Equal(6, result.AwayRating);
        }

        // ── SubmitRatingsAsync ──────────────────────────────────────────

        [Theory]
        [InlineData(0, 5)]
        [InlineData(11, 5)]
        [InlineData(5, 0)]
        [InlineData(5, 11)]
        public async Task SubmitRatingsAsync_OutOfRange_ThrowsArgumentException(int home, int away)
        {
            var svc = Create($"SubmitRatings_OutOfRange_{home}_{away}");
            await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.SubmitRatingsAsync(1, home, away));
        }

        [Fact]
        public async Task SubmitRatingsAsync_UnknownFixture_ThrowsKeyNotFoundException()
        {
            var svc = Create(nameof(SubmitRatingsAsync_UnknownFixture_ThrowsKeyNotFoundException));
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                svc.SubmitRatingsAsync(99, 7, 8));
        }

        [Fact]
        public async Task SubmitRatingsAsync_NewRatings_CreatesRecords()
        {
            var (db, _, _, _) = await Seed(nameof(SubmitRatingsAsync_NewRatings_CreatesRecords));
            var svc = new FairplayService(db);

            await svc.SubmitRatingsAsync(1, 9, 7);

            Assert.Equal(2, db.FairplayRatings.Count());
            Assert.Equal(9, db.FairplayRatings.First(r => r.TeamId == 1).Rating);
            Assert.Equal(7, db.FairplayRatings.First(r => r.TeamId == 2).Rating);
        }

        [Fact]
        public async Task SubmitRatingsAsync_ExistingRatings_UpdatesInPlace()
        {
            var (db, home, away, fixture) = await Seed(nameof(SubmitRatingsAsync_ExistingRatings_UpdatesInPlace));
            db.FairplayRatings.AddRange(
                new FairplayRating { FixtureId = fixture.Id, TeamId = home.Id, Rating = 5 },
                new FairplayRating { FixtureId = fixture.Id, TeamId = away.Id, Rating = 5 });
            await db.SaveChangesAsync();

            var svc = new FairplayService(db);
            await svc.SubmitRatingsAsync(1, 10, 3);

            Assert.Equal(2, db.FairplayRatings.Count());
            Assert.Equal(10, db.FairplayRatings.First(r => r.TeamId == home.Id).Rating);
            Assert.Equal(3, db.FairplayRatings.First(r => r.TeamId == away.Id).Rating);
        }

        // ── GetTableAsync ───────────────────────────────────────────────

        [Fact]
        public async Task GetTableAsync_NoRatings_ReturnsEmptyList()
        {
            var svc = Create(nameof(GetTableAsync_NoRatings_ReturnsEmptyList));
            var rows = await svc.GetTableAsync(1);
            Assert.Empty(rows);
        }

        [Fact]
        public async Task GetTableAsync_ReturnsCorrectAverageAndTotal()
        {
            var (db, home, _, _) = await Seed(nameof(GetTableAsync_ReturnsCorrectAverageAndTotal));
            var f2 = new Fixture { Id = 2, HomeTeamId = home.Id, AwayTeamId = 2, SeasonId = 1 };
            db.Fixtures.Add(f2);
            await db.SaveChangesAsync();

            db.FairplayRatings.AddRange(
                new FairplayRating { FixtureId = 1, TeamId = home.Id, Rating = 8 },
                new FairplayRating { FixtureId = 2, TeamId = home.Id, Rating = 6 });
            await db.SaveChangesAsync();

            var svc = new FairplayService(db);
            var rows = await svc.GetTableAsync(1);

            var row = rows.First(r => r.Team == "Home FC");
            Assert.Equal(2, row.Rated);
            Assert.Equal(14, row.TotalRating);
            Assert.Equal(7.0, row.AverageRating);
        }

        [Fact]
        public async Task GetTableAsync_OrderedByAverageDescending()
        {
            var (db, _, _, fixture) = await Seed(nameof(GetTableAsync_OrderedByAverageDescending));
            db.FairplayRatings.AddRange(
                new FairplayRating { FixtureId = fixture.Id, TeamId = 1, Rating = 4 },
                new FairplayRating { FixtureId = fixture.Id, TeamId = 2, Rating = 9 });
            await db.SaveChangesAsync();

            var svc = new FairplayService(db);
            var rows = await svc.GetTableAsync(1);

            Assert.Equal("Away FC", rows[0].Team);
            Assert.Equal(1, rows[0].Position);
            Assert.Equal("Home FC", rows[1].Team);
            Assert.Equal(2, rows[1].Position);
        }

        [Fact]
        public async Task GetTableAsync_IgnoresOtherSeasons()
        {
            var (db, _, _, _) = await Seed(nameof(GetTableAsync_IgnoresOtherSeasons));
            db.Seasons.Add(new Season { Id = 2, SeasonNumber = 2, StartDate = DateTime.UtcNow.AddYears(-2), EndDate = DateTime.UtcNow.AddYears(-1) });
            db.Fixtures.Add(new Fixture { Id = 99, HomeTeamId = 1, AwayTeamId = 2, SeasonId = 2 });
            await db.SaveChangesAsync();

            db.FairplayRatings.Add(new FairplayRating { FixtureId = 99, TeamId = 1, Rating = 10 });
            await db.SaveChangesAsync();

            var svc = new FairplayService(db);
            var rows = await svc.GetTableAsync(1);
            Assert.Empty(rows);
        }
    }
}
