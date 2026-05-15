using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Models.Requests;
using Ballers.API.Services;
using Ballers.Tests.Helpers;

namespace Ballers.Tests
{
    public class PenaltyServiceTests
    {
        private static async Task<ApplicationDbContext> SeedBase(string dbName)
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
            db.Fixtures.Add(new Fixture
            {
                Id = 1, HomeTeamId = 1, AwayTeamId = 2,
                SeasonId = 1, IsPlayed = true
            });
            await db.SaveChangesAsync();
            return db;
        }

        private static async Task AddShootout(ApplicationDbContext db,
            int fixtureId, int[] homeScored, int[] awayScored)
        {
            var shootout = new PenaltyShootout { FixtureId = fixtureId };
            db.PenaltyShootouts.Add(shootout);
            await db.SaveChangesAsync();

            for (int i = 0; i < homeScored.Length; i++)
            {
                var player = new Player { Id = 100 + i, Name = $"HP{i}", TeamId = 1 };
                db.Players.Add(player);
                db.PenaltyKicks.Add(new PenaltyKick
                {
                    ShootoutId = shootout.Id, PlayerId = player.Id,
                    TeamId = 1, Order = i + 1, Scored = homeScored[i] == 1
                });
            }
            for (int i = 0; i < awayScored.Length; i++)
            {
                var player = new Player { Id = 200 + i, Name = $"AP{i}", TeamId = 2 };
                db.Players.Add(player);
                db.PenaltyKicks.Add(new PenaltyKick
                {
                    ShootoutId = shootout.Id, PlayerId = player.Id,
                    TeamId = 2, Order = i + 1, Scored = awayScored[i] == 1
                });
            }
            await db.SaveChangesAsync();
        }

        // ── GetTableAsync ───────────────────────────────────────────────

        [Fact]
        public async Task GetTableAsync_NoShootouts_AllTeamsZeroStats()
        {
            var db = await SeedBase(nameof(GetTableAsync_NoShootouts_AllTeamsZeroStats));
            var svc = new PenaltyService(db);
            var rows = await svc.GetTableAsync(1);

            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.Equal(0, r.Played));
            Assert.All(rows, r => Assert.Equal(0, r.Points));
        }

        [Fact]
        public async Task GetTableAsync_HomeTeamWin_CorrectWinLoss()
        {
            var db = await SeedBase(nameof(GetTableAsync_HomeTeamWin_CorrectWinLoss));
            await AddShootout(db, 1, new[] { 1, 1, 1, 1, 0 }, new[] { 1, 1, 0, 0, 0 });

            var svc = new PenaltyService(db);
            var rows = await svc.GetTableAsync(1);

            var a = rows.First(r => r.Team == "Team A");
            var b = rows.First(r => r.Team == "Team B");

            Assert.Equal(1, a.Won);
            Assert.Equal(0, a.Lost);
            Assert.Equal(3, a.Points);
            Assert.Equal(0, b.Won);
            Assert.Equal(1, b.Lost);
            Assert.Equal(0, b.Points);
        }

        [Fact]
        public async Task GetTableAsync_AwayTeamWin_CorrectWinLoss()
        {
            var db = await SeedBase(nameof(GetTableAsync_AwayTeamWin_CorrectWinLoss));
            await AddShootout(db, 1, new[] { 1, 0, 0, 0, 0 }, new[] { 1, 1, 1, 1, 0 });

            var svc = new PenaltyService(db);
            var rows = await svc.GetTableAsync(1);

            var a = rows.First(r => r.Team == "Team A");
            var b = rows.First(r => r.Team == "Team B");

            Assert.Equal(0, a.Won);
            Assert.Equal(1, a.Lost);
            Assert.Equal(1, b.Won);
            Assert.Equal(0, b.Lost);
        }

        [Fact]
        public async Task GetTableAsync_Draw_BothTeamsGetOnePoint()
        {
            var db = await SeedBase(nameof(GetTableAsync_Draw_BothTeamsGetOnePoint));
            await AddShootout(db, 1, new[] { 1, 1, 1, 0, 0 }, new[] { 1, 1, 1, 0, 0 });

            var svc = new PenaltyService(db);
            var rows = await svc.GetTableAsync(1);

            Assert.All(rows.Where(r => r.Played > 0), r =>
            {
                Assert.Equal(1, r.Drawn);
                Assert.Equal(1, r.Points);
            });
        }

        [Fact]
        public async Task GetTableAsync_PenaltiesForAndAgainst_AreCorrect()
        {
            var db = await SeedBase(nameof(GetTableAsync_PenaltiesForAndAgainst_AreCorrect));
            await AddShootout(db, 1, new[] { 1, 1, 1, 1, 0 }, new[] { 1, 1, 0, 0, 0 });

            var svc = new PenaltyService(db);
            var rows = await svc.GetTableAsync(1);

            var a = rows.First(r => r.Team == "Team A");
            Assert.Equal(4, a.PenaltiesFor);
            Assert.Equal(2, a.PenaltiesAgainst);
            Assert.Equal(2, a.PenaltyDifference);
        }

        [Fact]
        public async Task GetTableAsync_OrderedByPointsThenPenaltyDifference()
        {
            var db = await SeedBase(nameof(GetTableAsync_OrderedByPointsThenPenaltyDifference));
            await AddShootout(db, 1, new[] { 1, 1, 1, 1, 0 }, new[] { 1, 1, 0, 0, 0 });

            var svc = new PenaltyService(db);
            var rows = await svc.GetTableAsync(1);

            Assert.Equal("Team A", rows[0].Team);
            Assert.Equal(1, rows[0].Position);
            Assert.Equal("Team B", rows[1].Team);
            Assert.Equal(2, rows[1].Position);
        }

        [Fact]
        public async Task GetTableAsync_IncompleteShootout_IsIgnored()
        {
            var db = await SeedBase(nameof(GetTableAsync_IncompleteShootout_IsIgnored));
            var shootout = new PenaltyShootout { FixtureId = 1 };
            db.PenaltyShootouts.Add(shootout);
            await db.SaveChangesAsync();

            for (int i = 0; i < 5; i++)
            {
                var p = new Player { Id = 10 + i, Name = $"P{i}", TeamId = 1 };
                db.Players.Add(p);
                db.PenaltyKicks.Add(new PenaltyKick
                {
                    ShootoutId = shootout.Id, PlayerId = p.Id,
                    TeamId = 1, Order = i + 1, Scored = true
                });
            }
            await db.SaveChangesAsync();

            var svc = new PenaltyService(db);
            var rows = await svc.GetTableAsync(1);
            Assert.All(rows, r => Assert.Equal(0, r.Played));
        }

        // ── GetShootoutAsync ────────────────────────────────────────────

        [Fact]
        public async Task GetShootoutAsync_UnknownFixture_ReturnsNull()
        {
            var db = DbContextFactory.Create(nameof(GetShootoutAsync_UnknownFixture_ReturnsNull));
            var svc = new PenaltyService(db);
            var result = await svc.GetShootoutAsync(99);
            Assert.Null(result);
        }

        [Fact]
        public async Task GetShootoutAsync_NoShootout_ReturnsEmptyLists()
        {
            var db = await SeedBase(nameof(GetShootoutAsync_NoShootout_ReturnsEmptyLists));
            var svc = new PenaltyService(db);
            var result = await svc.GetShootoutAsync(1);

            Assert.NotNull(result);
            Assert.Empty(result.Value.HomeKicks);
            Assert.Empty(result.Value.AwayKicks);
        }

        [Fact]
        public async Task GetShootoutAsync_ReturnsKicksInOrder()
        {
            var db = await SeedBase(nameof(GetShootoutAsync_ReturnsKicksInOrder));
            await AddShootout(db, 1, new[] { 0, 1, 1, 0, 1 }, new[] { 1, 0, 1, 1, 0 });

            var svc = new PenaltyService(db);
            var result = await svc.GetShootoutAsync(1);

            Assert.NotNull(result);
            var home = result.Value.HomeKicks;
            var away = result.Value.AwayKicks;

            Assert.Equal(5, home.Count);
            Assert.Equal(5, away.Count);
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, home.Select(k => k.Order).ToArray());
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, away.Select(k => k.Order).ToArray());

            Assert.False(home[0].Scored);
            Assert.True(home[1].Scored);
            Assert.True(away[0].Scored);
            Assert.False(away[1].Scored);
        }

        // ── SubmitKicksAsync ────────────────────────────────────────────

        [Fact]
        public async Task SubmitKicksAsync_FixtureNotFound_ThrowsKeyNotFoundException()
        {
            var db = DbContextFactory.Create(nameof(SubmitKicksAsync_FixtureNotFound_ThrowsKeyNotFoundException));
            var svc = new PenaltyService(db);
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                svc.SubmitKicksAsync(99, 1, MakeKicks(5)));
        }

        [Fact]
        public async Task SubmitKicksAsync_MatchNotPlayed_ThrowsInvalidOperation()
        {
            var db = await SeedBase(nameof(SubmitKicksAsync_MatchNotPlayed_ThrowsInvalidOperation));
            db.Fixtures.Add(new Fixture { Id = 2, HomeTeamId = 1, AwayTeamId = 2, SeasonId = 1, IsPlayed = false });
            await db.SaveChangesAsync();

            var svc = new PenaltyService(db);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.SubmitKicksAsync(2, 1, MakeKicks(5)));
        }

        [Fact]
        public async Task SubmitKicksAsync_Not5Kicks_ThrowsArgumentException()
        {
            var db = await SeedBase(nameof(SubmitKicksAsync_Not5Kicks_ThrowsArgumentException));
            var svc = new PenaltyService(db);
            await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.SubmitKicksAsync(1, 1, MakeKicks(4)));
        }

        [Fact]
        public async Task SubmitKicksAsync_SavesKicksAndReplacesExisting()
        {
            var db = await SeedBase(nameof(SubmitKicksAsync_SavesKicksAndReplacesExisting));
            for (int i = 1; i <= 5; i++)
                db.Players.Add(new Player { Id = i, Name = $"P{i}", TeamId = 1 });
            await db.SaveChangesAsync();

            var kicks = Enumerable.Range(1, 5)
                .Select(i => new PenaltyKickEntry { PlayerId = i, Order = i, Scored = i % 2 == 0 })
                .ToList();

            var svc = new PenaltyService(db);
            await svc.SubmitKicksAsync(1, 1, kicks);

            Assert.Equal(5, db.PenaltyKicks.Count());

            // Submit again — should replace, not duplicate
            await svc.SubmitKicksAsync(1, 1, kicks);
            Assert.Equal(5, db.PenaltyKicks.Count());
        }

        private static List<PenaltyKickEntry> MakeKicks(int count) =>
            Enumerable.Range(1, count)
                .Select(i => new PenaltyKickEntry { PlayerId = i, Order = i, Scored = true })
                .ToList();
    }
}
