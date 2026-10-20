using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Services;
using Ballers.Shared;
using Ballers.Tests.Helpers;
using Moq;

namespace Ballers.Tests
{
    public class ManagerDashboardServiceTests
    {
        private static readonly DateTime Now = new(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);

        // Rovers (1) are the manager's team.
        private static async Task<ApplicationDbContext> Seed(string name, int squadSize = 12, bool profile = true)
        {
            var db = DbContextFactory.Create(name);
            db.Teams.AddRange(
                new Team { Id = 1, Name = "Rovers", ProfileImageUrl = profile ? "/crest.png" : null, HomeKitColour = profile ? "#c00" : null },
                new Team { Id = 2, Name = "Unity" },
                new Team { Id = 3, Name = "Athletic" });
            db.Seasons.Add(new Season { Id = 1, SeasonNumber = 3, IsActive = true, StartDate = Now.AddMonths(-2), EndDate = Now.AddMonths(3) });
            for (var i = 1; i <= squadSize; i++)
                db.Players.Add(new Player { Id = i, Name = $"Rover {i}", TeamId = 1, Position = "MID" });
            db.Players.Add(new Player { Id = 100, Name = "Unity Lad", TeamId = 2, Position = "MID" });
            await db.SaveChangesAsync();
            return db;
        }

        private static Fixture F(int id, int home, int away, DateTime kickoff) => new()
        {
            Id = id, HomeTeamId = home, AwayTeamId = away, SeasonId = 1, Kickoff = kickoff,
            WindowStart = kickoff.Date.AddDays(-2), WindowEnd = kickoff.Date.AddDays(5)
        };

        private static ManagerDashboardService Build(ApplicationDbContext db)
        {
            var teams = new Mock<ITeamService>();
            teams.Setup(t => t.GetOverviewAsync()).ReturnsAsync(new List<TeamOverviewDto>
            {
                new() { Id = 1, Name = "Rovers", Position = 2, Played = 5, Won = 3, Form = new() { "W", "L", "W" } },
                new() { Id = 2, Name = "Unity", Position = 1 },
                new() { Id = 3, Name = "Athletic", Position = 3 },
            });
            return new ManagerDashboardService(db, teams.Object);
        }

        [Fact]
        public async Task NextMatch_IsTheNextUnplayedGame_WithReadiness()
        {
            var db = await Seed(nameof(NextMatch_IsTheNextUnplayedGame_WithReadiness));
            db.Fixtures.AddRange(
                F(1, 1, 2, Now.AddDays(-7)).With(f => { f.IsPlayed = true; f.HomeStatsSubmitted = f.AwayStatsSubmitted = true; }),
                F(2, 3, 1, Now.AddDays(2)).With(f => f.AwayCaptainId = 4),
                F(3, 1, 2, Now.AddDays(16)));
            for (var p = 1; p <= 11; p++) db.FixturePlayers.Add(new FixturePlayer { FixtureId = 2, PlayerId = p });
            await db.SaveChangesAsync();

            var dto = (await Build(db).GetAsync(1, Now))!;

            var next = dto.NextMatch!;
            Assert.Equal((2, "Athletic", false), (next.FixtureId, next.Opponent, next.IsHome));
            Assert.True(next.SquadPicked);
            Assert.Equal("Rover 4", next.CaptainName);
            Assert.Equal(2, dto.Standing!.Position);
            Assert.Equal(3, dto.TeamsInLeague);
        }

        [Fact]
        public async Task Todos_FlagMissingStatsAndUnreadySquad()
        {
            var db = await Seed(nameof(Todos_FlagMissingStatsAndUnreadySquad));
            db.Fixtures.AddRange(
                // Last week: Unity sent theirs, we haven't.
                F(1, 1, 2, Now.AddDays(-5)).With(f => f.AwayStatsSubmitted = true),
                // In two days, at home: no squad, no captain, no venue.
                F(2, 1, 3, Now.AddDays(2)));
            for (var p = 1; p <= 5; p++) db.FixturePlayers.Add(new FixturePlayer { FixtureId = 2, PlayerId = p });
            await db.SaveChangesAsync();

            var dto = (await Build(db).GetAsync(1, Now))!;

            var stats = Assert.Single(dto.Todos, t => t.Kind == "stats");
            Assert.Equal("/fixture/1?tab=stats", stats.Link);
            Assert.True(stats.Urgent);

            var squad = Assert.Single(dto.Todos, t => t.Kind == "squad");
            Assert.False(squad.Done);
            Assert.True(squad.Urgent);
            Assert.Contains("5 of at least 11", squad.Detail);

            Assert.False(Assert.Single(dto.Todos, t => t.Kind == "captain").Done);
            Assert.False(Assert.Single(dto.Todos, t => t.Kind == "details").Done);

            var recent = Assert.Single(dto.RecentResults);
            Assert.Equal((false, true), (recent.OurStatsIn, recent.TheirStatsIn));
        }

        [Fact]
        public async Task Todos_AwayGameHasNoVenueJob_AndDistantGameHasNoPrep()
        {
            var db = await Seed(nameof(Todos_AwayGameHasNoVenueJob_AndDistantGameHasNoPrep));
            db.Fixtures.Add(F(1, 2, 1, Now.AddDays(30)));
            await db.SaveChangesAsync();

            var dto = (await Build(db).GetAsync(1, Now))!;

            Assert.NotNull(dto.NextMatch);
            Assert.DoesNotContain(dto.Todos, t => t.Kind is "squad" or "captain" or "details");
        }

        [Fact]
        public async Task Todos_IncludeSignupsSmallSquadAndMissingProfile()
        {
            var db = await Seed(nameof(Todos_IncludeSignupsSmallSquadAndMissingProfile), squadSize: 6, profile: false);
            db.Users.Add(new ApplicationUser { Id = "u", UserName = "u@x" });
            db.PlayerAccountRequests.AddRange(
                new PlayerAccountRequest { UserId = "u", RequestedTeamId = 1, RequestedName = "New", CreatedAt = Now },
                new PlayerAccountRequest { UserId = "u", RequestedTeamId = 2, RequestedName = "Other team", CreatedAt = Now });
            await db.SaveChangesAsync();

            var dto = (await Build(db).GetAsync(1, Now))!;

            Assert.Equal(1, dto.PendingSignups);
            Assert.Contains(dto.Todos, t => t.Kind == "signups" && t.Title == "1 player wants to join");
            Assert.Contains(dto.Todos, t => t.Kind == "players" && t.Detail.StartsWith("6 players"));
            Assert.Contains(dto.Todos, t => t.Kind == "profile");
            Assert.Null(dto.NextMatch);
        }

        [Fact]
        public async Task Todos_FlagStatsSentWithoutOurManOfTheMatch()
        {
            var db = await Seed(nameof(Todos_FlagStatsSentWithoutOurManOfTheMatch));
            db.Fixtures.AddRange(
                // Both in; only Unity named a man of the match.
                F(1, 1, 2, Now.AddDays(-7)).With(f => { f.IsPlayed = true; f.HomeStatsSubmitted = f.AwayStatsSubmitted = true; }),
                // Ours in with a man of the match.
                F(2, 3, 1, Now.AddDays(-3)).With(f => f.AwayStatsSubmitted = true));
            db.FixturePlayerStats.AddRange(
                new FixturePlayerStat { FixtureId = 1, PlayerId = 100, ManOfTheMatch = true },
                new FixturePlayerStat { FixtureId = 2, PlayerId = 2, ManOfTheMatch = true });
            await db.SaveChangesAsync();

            var dto = (await Build(db).GetAsync(1, Now))!;

            var motm = Assert.Single(dto.Todos, t => t.Kind == "motm");
            Assert.Equal("/fixture/1?tab=stats", motm.Link);
            Assert.Equal("Pick your man of the match v Unity", motm.Title);
        }

        [Fact]
        public async Task TopPlayers_CountOnlyThisTeamsPlayedGames()
        {
            var db = await Seed(nameof(TopPlayers_CountOnlyThisTeamsPlayedGames));
            db.Fixtures.AddRange(
                F(1, 1, 2, Now.AddDays(-7)).With(f => f.IsPlayed = true),
                F(2, 1, 2, Now.AddDays(-1)));
            db.FixturePlayerStats.AddRange(
                new FixturePlayerStat { FixtureId = 1, PlayerId = 3, Goals = 2, Assists = 1 },
                new FixturePlayerStat { FixtureId = 1, PlayerId = 5, Assists = 3 },
                new FixturePlayerStat { FixtureId = 1, PlayerId = 100, Goals = 9 },
                new FixturePlayerStat { FixtureId = 2, PlayerId = 5, Goals = 7 });
            await db.SaveChangesAsync();

            var dto = (await Build(db).GetAsync(1, Now))!;

            Assert.Equal(("Rover 3", 2), (dto.TopScorerName, dto.TopScorerGoals));
            Assert.Equal(("Rover 5", 3), (dto.TopAssisterName, dto.TopAssisterAssists));
        }

        [Fact]
        public async Task UnknownTeam_ReturnsNull()
        {
            var db = await Seed(nameof(UnknownTeam_ReturnsNull));
            Assert.Null(await Build(db).GetAsync(99, Now));
        }
    }
}
