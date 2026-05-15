using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Models.Requests;
using Ballers.API.Services;
using Ballers.Tests.Helpers;

namespace Ballers.Tests
{
    public class PlayerServiceTests
    {
        private static async Task<ApplicationDbContext> SeedBase(string dbName)
        {
            var db = DbContextFactory.Create(dbName);
            db.Teams.AddRange(
                new Team { Id = 1, Name = "Team A" },
                new Team { Id = 2, Name = "Team B" });
            db.Players.AddRange(
                new Player { Id = 1, Name = "Active1", Number = 5, TeamId = 1, IsActive = true },
                new Player { Id = 2, Name = "Active2", Number = 10, TeamId = 1, IsActive = true },
                new Player { Id = 3, Name = "Inactive", Number = 7, TeamId = 1, IsActive = false },
                new Player { Id = 4, Name = "OtherTeam", Number = 1, TeamId = 2, IsActive = true });
            await db.SaveChangesAsync();
            return db;
        }

        // ── GetTeamPlayersAsync ─────────────────────────────────────────

        [Fact]
        public async Task GetTeamPlayers_OnlyReturnsActivePlayers()
        {
            var db = await SeedBase(nameof(GetTeamPlayers_OnlyReturnsActivePlayers));
            var svc = new PlayerService(db);

            var result = await svc.GetTeamPlayersAsync(1);

            Assert.Equal(2, result.Count);
            Assert.DoesNotContain(result, p => p.Name == "Inactive");
        }

        [Fact]
        public async Task GetTeamPlayers_OnlyReturnsCorrectTeam()
        {
            var db = await SeedBase(nameof(GetTeamPlayers_OnlyReturnsCorrectTeam));
            var svc = new PlayerService(db);

            var result = await svc.GetTeamPlayersAsync(1);

            Assert.DoesNotContain(result, p => p.Name == "OtherTeam");
        }

        [Fact]
        public async Task GetTeamPlayers_OrderedByNumber()
        {
            var db = await SeedBase(nameof(GetTeamPlayers_OrderedByNumber));
            var svc = new PlayerService(db);

            var result = await svc.GetTeamPlayersAsync(1);

            Assert.Equal("Active1", result[0].Name); // number 5
            Assert.Equal("Active2", result[1].Name); // number 10
        }

        // ── AddPlayerAsync ──────────────────────────────────────────────

        [Fact]
        public async Task AddPlayer_CreatesPlayerWithCorrectData()
        {
            var db = await SeedBase(nameof(AddPlayer_CreatesPlayerWithCorrectData));
            var svc = new PlayerService(db);

            await svc.AddPlayerAsync(1, new CreatePlayerRequest
            {
                Name = "New Guy",
                Number = 99,
                Position = "GK"
            });

            var player = db.Players.Single(p => p.Name == "New Guy");
            Assert.Equal(1, player.TeamId);
            Assert.Equal(99, player.Number);
            Assert.Equal("GK", player.Position);
            Assert.True(player.IsActive);
        }

        // ── DeactivatePlayerAsync ───────────────────────────────────────

        [Fact]
        public async Task DeactivatePlayer_NotFound_ReturnsFalse()
        {
            var db = await SeedBase(nameof(DeactivatePlayer_NotFound_ReturnsFalse));
            var svc = new PlayerService(db);

            var result = await svc.DeactivatePlayerAsync(999, 1, false);
            Assert.False(result);
        }

        [Fact]
        public async Task DeactivatePlayer_WrongTeam_ThrowsUnauthorized()
        {
            var db = await SeedBase(nameof(DeactivatePlayer_WrongTeam_ThrowsUnauthorized));
            var svc = new PlayerService(db);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                svc.DeactivatePlayerAsync(4, 1, false)); // player 4 is team 2, requesting as team 1
        }

        [Fact]
        public async Task DeactivatePlayer_OwnPlayer_DeactivatesAndReturnsTrue()
        {
            var db = await SeedBase(nameof(DeactivatePlayer_OwnPlayer_DeactivatesAndReturnsTrue));
            var svc = new PlayerService(db);

            var result = await svc.DeactivatePlayerAsync(1, 1, false);

            Assert.True(result);
            Assert.False(db.Players.Find(1)!.IsActive);
        }

        [Fact]
        public async Task DeactivatePlayer_AdminCanRemoveAnyTeam()
        {
            var db = await SeedBase(nameof(DeactivatePlayer_AdminCanRemoveAnyTeam));
            var svc = new PlayerService(db);

            var result = await svc.DeactivatePlayerAsync(4, 1, isAdmin: true);

            Assert.True(result);
            Assert.False(db.Players.Find(4)!.IsActive);
        }
    }
}
