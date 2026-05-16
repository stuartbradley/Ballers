using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Models.Requests;
using Ballers.API.Services;
using Ballers.Tests.Helpers;

namespace Ballers.Tests
{
    public class FixtureServiceTests
    {
        private static async Task<ApplicationDbContext> SeedBase(string dbName)
        {
            var db = DbContextFactory.Create(dbName);
            db.Teams.AddRange(
                new Team { Id = 1, Name = "Home FC" },
                new Team { Id = 2, Name = "Away FC" });
            db.Seasons.Add(new Season
            {
                Id = 1, SeasonNumber = 1,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow.AddMonths(5)
            });
            await db.SaveChangesAsync();
            return db;
        }

        private static Fixture MakeFixture(int id, int homeId = 1, int awayId = 2, int seasonId = 1) =>
            new Fixture
            {
                Id = id,
                HomeTeamId = homeId,
                AwayTeamId = awayId,
                SeasonId = seasonId,
                MatchNumber = id,
                WindowStart = DateTime.UtcNow.AddDays(-3),
                WindowEnd = DateTime.UtcNow.AddDays(3)
            };

        // ── GetByIdAsync ────────────────────────────────────────────────

        [Fact]
        public async Task GetById_NotFound_ReturnsNull()
        {
            var db = await SeedBase(nameof(GetById_NotFound_ReturnsNull));
            var svc = new FixtureService(db);
            var result = await svc.GetByIdAsync(99);
            Assert.Null(result);
        }

        [Fact]
        public async Task GetById_ReturnsCorrectDetail()
        {
            var db = await SeedBase(nameof(GetById_ReturnsCorrectDetail));
            var kickoff = DateTime.UtcNow.AddDays(1);
            db.Fixtures.Add(new Fixture
            {
                Id = 1, HomeTeamId = 1, AwayTeamId = 2, SeasonId = 1,
                MatchNumber = 1, Location = "Test Ground",
                Kickoff = kickoff, IsPlayed = false,
                WindowStart = DateTime.UtcNow.AddDays(-1),
                WindowEnd = DateTime.UtcNow.AddDays(5)
            });
            await db.SaveChangesAsync();

            var svc = new FixtureService(db);
            var result = await svc.GetByIdAsync(1);

            Assert.NotNull(result);
            Assert.Equal("Home FC", result.HomeTeam);
            Assert.Equal("Away FC", result.AwayTeam);
            Assert.Equal(1, result.HomeTeamId);
            Assert.Equal(2, result.AwayTeamId);
            Assert.Equal("Test Ground", result.Location);
            Assert.Equal(kickoff, result.Kickoff);
            Assert.False(result.IsPlayed);
        }

        // ── GetForUserAsync ─────────────────────────────────────────────

        [Fact]
        public async Task GetForUser_AdminGetsAllFixtures()
        {
            var db = await SeedBase(nameof(GetForUser_AdminGetsAllFixtures));
            db.Teams.Add(new Team { Id = 3, Name = "Third FC" });
            db.Fixtures.AddRange(
                MakeFixture(1, 1, 2),
                MakeFixture(2, 2, 3));
            await db.SaveChangesAsync();

            var svc = new FixtureService(db);
            var result = await svc.GetForUserAsync(isAdmin: true, teamId: null);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetForUser_TeamGetsOnlyTheirFixtures()
        {
            var db = await SeedBase(nameof(GetForUser_TeamGetsOnlyTheirFixtures));
            db.Teams.Add(new Team { Id = 3, Name = "Third FC" });
            db.Fixtures.AddRange(
                MakeFixture(1, 1, 2),   // involves team 1
                MakeFixture(2, 2, 3));  // does not involve team 1
            await db.SaveChangesAsync();

            var svc = new FixtureService(db);
            var result = await svc.GetForUserAsync(isAdmin: false, teamId: 1);

            Assert.Single(result);
            Assert.Equal("Home FC", result[0].HomeTeam);
        }

        // ── GetTableAsync ───────────────────────────────────────────────

        [Fact]
        public async Task GetTable_CorrectPointsAndOrdering()
        {
            var db = await SeedBase(nameof(GetTable_CorrectPointsAndOrdering));
            db.Fixtures.AddRange(
                new Fixture { Id = 1, HomeTeamId = 1, AwayTeamId = 2, SeasonId = 1, IsPlayed = true, HomeScore = 2, AwayScore = 1, WindowStart = DateTime.UtcNow, WindowEnd = DateTime.UtcNow },
                new Fixture { Id = 2, HomeTeamId = 1, AwayTeamId = 2, SeasonId = 1, IsPlayed = true, HomeScore = 1, AwayScore = 1, WindowStart = DateTime.UtcNow, WindowEnd = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var svc = new FixtureService(db);
            var table = await svc.GetTableAsync(1);

            var home = table.First(r => r.Team == "Home FC");
            var away = table.First(r => r.Team == "Away FC");

            Assert.Equal(2, home.Played);
            Assert.Equal(1, home.Won);
            Assert.Equal(1, home.Drawn);
            Assert.Equal(0, home.Lost);
            Assert.Equal(4, home.Points);   // 1 win (3pts) + 1 draw (1pt)
            Assert.Equal(2, away.GoalsFor); // away scored 1 in game 1 and 1 in game 2

            Assert.Equal(1, table[0].Position); // home has more points
            Assert.Equal("Home FC", table[0].Team);
        }

        [Fact]
        public async Task GetTable_OnlyIncludesPlayedFixtures()
        {
            var db = await SeedBase(nameof(GetTable_OnlyIncludesPlayedFixtures));
            db.Fixtures.Add(new Fixture
            {
                Id = 1, HomeTeamId = 1, AwayTeamId = 2, SeasonId = 1,
                IsPlayed = false, WindowStart = DateTime.UtcNow, WindowEnd = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var svc = new FixtureService(db);
            var table = await svc.GetTableAsync(1);
            Assert.All(table, r => Assert.Equal(0, r.Played));
        }

        // ── GetCurrentWeekAsync ─────────────────────────────────────────

        [Fact]
        public async Task GetCurrentWeek_NoFixturesInWindow_ReturnsNull()
        {
            var db = await SeedBase(nameof(GetCurrentWeek_NoFixturesInWindow_ReturnsNull));
            db.Fixtures.Add(new Fixture
            {
                Id = 1, HomeTeamId = 1, AwayTeamId = 2, SeasonId = 1, MatchNumber = 1,
                WindowStart = DateTime.UtcNow.AddDays(10),
                WindowEnd = DateTime.UtcNow.AddDays(17)
            });
            await db.SaveChangesAsync();

            var svc = new FixtureService(db);
            var result = await svc.GetCurrentWeekAsync();
            Assert.Null(result);
        }

        [Fact]
        public async Task GetCurrentWeek_WithinWindow_ReturnsWeek()
        {
            var db = await SeedBase(nameof(GetCurrentWeek_WithinWindow_ReturnsWeek));
            db.Fixtures.Add(new Fixture
            {
                Id = 1, HomeTeamId = 1, AwayTeamId = 2, SeasonId = 1, MatchNumber = 3,
                WindowStart = DateTime.UtcNow.AddDays(-2),
                WindowEnd = DateTime.UtcNow.AddDays(5)
            });
            await db.SaveChangesAsync();

            var svc = new FixtureService(db);
            var result = await svc.GetCurrentWeekAsync();

            Assert.NotNull(result);
            Assert.Equal(3, result.WeekNumber);
            Assert.Single(result.Matches);
        }

        // ── GetSquadAsync / UpdateSquadAsync ────────────────────────────

        [Fact]
        public async Task GetSquad_ReturnsSquadForFixture()
        {
            var db = await SeedBase(nameof(GetSquad_ReturnsSquadForFixture));
            db.Fixtures.Add(MakeFixture(1));
            db.Players.AddRange(
                new Player { Id = 1, Name = "Player1", TeamId = 1 },
                new Player { Id = 2, Name = "Player2", TeamId = 1 });
            db.FixturePlayers.AddRange(
                new FixturePlayer { FixtureId = 1, PlayerId = 1 },
                new FixturePlayer { FixtureId = 1, PlayerId = 2 });
            await db.SaveChangesAsync();

            var svc = new FixtureService(db);
            var result = await svc.GetSquadAsync(1);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, e => e.PlayerId == 1);
            Assert.Contains(result, e => e.PlayerId == 2);
        }

        [Fact]
        public async Task UpdateSquad_ReplacesExistingEntries()
        {
            var db = await SeedBase(nameof(UpdateSquad_ReplacesExistingEntries));
            db.Fixtures.Add(MakeFixture(1));
            db.Players.AddRange(
                new Player { Id = 1, Name = "P1", TeamId = 1 },
                new Player { Id = 2, Name = "P2", TeamId = 1 },
                new Player { Id = 3, Name = "P3", TeamId = 1 });
            db.FixturePlayers.Add(new FixturePlayer { FixtureId = 1, PlayerId = 1 });
            await db.SaveChangesAsync();

            var svc = new FixtureService(db);
            await svc.UpdateSquadAsync(1, new List<int> { 2, 3 }, teamId: null);

            var squad = db.FixturePlayers.Where(fp => fp.FixtureId == 1).ToList();
            Assert.Equal(2, squad.Count);
            Assert.All(squad, fp => Assert.NotEqual(1, fp.PlayerId));
        }

        // ── SubmitStatsAsync ────────────────────────────────────────────

        [Fact]
        public async Task SubmitStats_CreatesNewStats_AndSetsScore()
        {
            var db = await SeedBase(nameof(SubmitStats_CreatesNewStats_AndSetsScore));
            db.Fixtures.Add(MakeFixture(1));
            db.Players.AddRange(
                new Player { Id = 1, Name = "HomeStriker", TeamId = 1 },
                new Player { Id = 2, Name = "AwayStriker", TeamId = 2 });
            await db.SaveChangesAsync();

            var svc = new FixtureService(db);
            await svc.SubmitStatsAsync(1, new List<PlayerStatDto>
            {
                new() { PlayerId = 1, Goals = 2, Assists = 1, IsManOfTheMatch = true },
                new() { PlayerId = 2, Goals = 1, Assists = 0 }
            }, teamId: null);

            var fixture = db.Fixtures.Find(1)!;
            Assert.True(fixture.IsPlayed);
            Assert.Equal(2, fixture.HomeScore);
            Assert.Equal(1, fixture.AwayScore);

            Assert.Equal(2, db.FixturePlayerStats.Count());
            var homeStat = db.FixturePlayerStats.Single(s => s.PlayerId == 1);
            Assert.Equal(2, homeStat.Goals);
            Assert.True(homeStat.ManOfTheMatch);
        }

        [Fact]
        public async Task SubmitStats_UpdatesExistingStats()
        {
            var db = await SeedBase(nameof(SubmitStats_UpdatesExistingStats));
            db.Fixtures.Add(MakeFixture(1));
            db.Players.Add(new Player { Id = 1, Name = "P1", TeamId = 1 });
            db.FixturePlayerStats.Add(new FixturePlayerStat { FixtureId = 1, PlayerId = 1, Goals = 1 });
            await db.SaveChangesAsync();

            var svc = new FixtureService(db);
            await svc.SubmitStatsAsync(1, new List<PlayerStatDto>
            {
                new() { PlayerId = 1, Goals = 3, Assists = 2 }
            }, teamId: null);

            Assert.Equal(1, db.FixturePlayerStats.Count());
            var stat = db.FixturePlayerStats.Single(s => s.PlayerId == 1);
            Assert.Equal(3, stat.Goals);
            Assert.Equal(2, stat.Assists);
        }

        [Fact]
        public async Task SubmitStats_UnknownFixture_ThrowsKeyNotFoundException()
        {
            var db = await SeedBase(nameof(SubmitStats_UnknownFixture_ThrowsKeyNotFoundException));
            var svc = new FixtureService(db);
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                svc.SubmitStatsAsync(99, new List<PlayerStatDto>(), teamId: null));
        }

        // ── UpdateScheduleAsync ─────────────────────────────────────────

        [Fact]
        public async Task UpdateSchedule_NotFound_ReturnsFalse()
        {
            var db = await SeedBase(nameof(UpdateSchedule_NotFound_ReturnsFalse));
            var svc = new FixtureService(db);
            var result = await svc.UpdateScheduleAsync(99, "Somewhere", DateTime.UtcNow);
            Assert.False(result);
        }

        [Fact]
        public async Task UpdateSchedule_UpdatesLocationAndKickoff()
        {
            var db = await SeedBase(nameof(UpdateSchedule_UpdatesLocationAndKickoff));
            db.Fixtures.Add(MakeFixture(1));
            await db.SaveChangesAsync();

            var kickoff = DateTime.UtcNow.AddDays(5);
            var svc = new FixtureService(db);
            var result = await svc.UpdateScheduleAsync(1, "New Ground", kickoff);

            Assert.True(result);
            var fixture = db.Fixtures.Find(1)!;
            Assert.Equal("New Ground", fixture.Location);
            Assert.Equal(kickoff, fixture.Kickoff);
        }
    }
}
