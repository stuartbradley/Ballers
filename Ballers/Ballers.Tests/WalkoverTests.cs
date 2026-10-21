using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Services;
using Ballers.API.Models.Requests;
using Ballers.Tests.Helpers;
using Moq;

namespace Ballers.Tests
{
    public class WalkoverTests
    {
        private static async Task<(ApplicationDbContext Db, FixtureService Svc)> Seed(string dbName, Action<Fixture>? setup = null)
        {
            var db = DbContextFactory.Create(dbName);
            db.Teams.AddRange(
                new Team { Id = 1, Name = "Home FC" },
                new Team { Id = 2, Name = "Away FC" });
            db.Seasons.Add(new Season
            {
                Id = 1, SeasonNumber = 1, IsActive = true,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow.AddMonths(5)
            });
            var fixture = new Fixture
            {
                Id = 1, HomeTeamId = 1, AwayTeamId = 2, SeasonId = 1, MatchNumber = 1,
                WindowStart = DateTime.UtcNow.AddDays(-3),
                WindowEnd = DateTime.UtcNow.AddDays(3)
            };
            setup?.Invoke(fixture);
            db.Fixtures.Add(fixture);
            await db.SaveChangesAsync();
            return (db, new FixtureService(db, Mock.Of<IKnockoutService>()));
        }

        [Fact]
        public async Task Award_AwaySideWins3Nil_AndCountsAsPlayed()
        {
            var (db, svc) = await Seed(nameof(Award_AwaySideWins3Nil_AndCountsAsPlayed));

            var problem = await svc.AwardWalkoverAsync(1, winnerTeamId: 2);

            Assert.Null(problem);
            var f = db.Fixtures.Single();
            Assert.True(f.IsWalkover);
            Assert.True(f.IsPlayed);
            Assert.True(f.HomeStatsSubmitted && f.AwayStatsSubmitted);
            Assert.Equal((0, 3), (f.HomeScore, f.AwayScore));
        }

        [Fact]
        public async Task Award_CountsInTheTableAsAWin()
        {
            var (_, svc) = await Seed(nameof(Award_CountsInTheTableAsAWin));
            await svc.AwardWalkoverAsync(1, winnerTeamId: 1);

            var table = await svc.GetTableAsync(1);

            var home = table.Single(r => r.Team == "Home FC");
            Assert.Equal((1, 1, 3, 0, 3), (home.Played, home.Won, home.GoalsFor, home.GoalsAgainst, home.Points));
            Assert.Equal(1, table.Single(r => r.Team == "Away FC").Lost);
        }

        [Fact]
        public async Task Award_NoOneIsCreditedWithTheGoals()
        {
            var (db, svc) = await Seed(nameof(Award_NoOneIsCreditedWithTheGoals));
            await svc.AwardWalkoverAsync(1, winnerTeamId: 1);

            Assert.Empty(db.FixturePlayerStats);
            var match = (await svc.GetAllWeeksAsync(1)).Single().Matches.Single();
            Assert.True(match.IsWalkover);
            Assert.Empty(match.HomeScorers);
        }

        [Fact]
        public async Task Award_RefusedOnceStatsAreIn()
        {
            var (_, svc) = await Seed(nameof(Award_RefusedOnceStatsAreIn), f => f.HomeStatsSubmitted = true);

            Assert.NotNull(await svc.AwardWalkoverAsync(1, winnerTeamId: 1));
        }

        [Fact]
        public async Task Award_RefusedForKnockoutGames()
        {
            var (_, svc) = await Seed(nameof(Award_RefusedForKnockoutGames), f => f.IsKnockout = true);

            Assert.NotNull(await svc.AwardWalkoverAsync(1, winnerTeamId: 1));
        }

        [Fact]
        public async Task Award_RefusedForATeamNotInTheFixture()
        {
            var (_, svc) = await Seed(nameof(Award_RefusedForATeamNotInTheFixture));

            Assert.NotNull(await svc.AwardWalkoverAsync(1, winnerTeamId: 99));
        }

        [Fact]
        public async Task SubmitStats_RefusedOnAWalkover()
        {
            var (db, svc) = await Seed(nameof(SubmitStats_RefusedOnAWalkover));
            await svc.AwardWalkoverAsync(1, winnerTeamId: 1);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.SubmitStatsAsync(1, new List<PlayerStatDto>(), teamId: 1));
            Assert.Equal((3, 0), (db.Fixtures.Single().HomeScore, db.Fixtures.Single().AwayScore));
        }

        [Fact]
        public async Task Undo_PutsTheFixtureBackToAwaitingStats()
        {
            var (db, svc) = await Seed(nameof(Undo_PutsTheFixtureBackToAwaitingStats));
            await svc.AwardWalkoverAsync(1, winnerTeamId: 1);

            Assert.Null(await svc.UndoWalkoverAsync(1));

            var f = db.Fixtures.Single();
            Assert.False(f.IsWalkover || f.IsPlayed || f.HomeStatsSubmitted || f.AwayStatsSubmitted);
            Assert.Equal((0, 0), (f.HomeScore, f.AwayScore));
        }

        [Fact]
        public async Task Undo_RefusedWhenNotAWalkover()
        {
            var (_, svc) = await Seed(nameof(Undo_RefusedWhenNotAWalkover));

            Assert.NotNull(await svc.UndoWalkoverAsync(1));
        }

        [Fact]
        public async Task List_ShowsOpenFixturesAndWalkovers_NotPlayedOnes()
        {
            var (db, svc) = await Seed(nameof(List_ShowsOpenFixturesAndWalkovers_NotPlayedOnes));
            db.Fixtures.AddRange(
                new Fixture { Id = 2, HomeTeamId = 2, AwayTeamId = 1, SeasonId = 1, MatchNumber = 2, IsPlayed = true, HomeStatsSubmitted = true, AwayStatsSubmitted = true },
                new Fixture { Id = 3, HomeTeamId = 2, AwayTeamId = 1, SeasonId = 1, MatchNumber = 3 });
            await db.SaveChangesAsync();
            await svc.AwardWalkoverAsync(3, winnerTeamId: 2);

            var list = await svc.GetWalkoverFixturesAsync();

            Assert.Equal(new[] { 1, 3 }, list.Select(w => w.Id));
            Assert.True(list.Single(w => w.Id == 3).IsWalkover);
        }
    }
}
