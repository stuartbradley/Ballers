using Ballers.API.Controllers;
using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Services;
using Ballers.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Ballers.Tests
{
    public class DebugControllerTests
    {
        private static IWebHostEnvironment MakeEnv(bool isDev)
        {
            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.EnvironmentName).Returns(isDev ? "Development" : "Production");
            return env.Object;
        }

        private static IConfiguration MakeConfig(bool isUat, string adminPass = "Test123!") =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["IsUAT"] = isUat.ToString().ToLower(),
                    ["AdminSeedPassword"] = adminPass
                })
                .Build();

        private static DebugController MakeController(
            bool isDev, bool isUat,
            ApplicationDbContext db,
            IServiceProvider? sp = null,
            IKnockoutService? knockout = null)
        {
            sp ??= Mock.Of<IServiceProvider>();
            knockout ??= Mock.Of<IKnockoutService>();
            return new DebugController(MakeEnv(isDev), MakeConfig(isUat), db, sp, knockout);
        }

        private static IServiceProvider BuildServiceProvider(ApplicationDbContext db)
        {
            var services = new ServiceCollection();
            services.AddSingleton(db);
            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(MakeConfig(true));
            return services.BuildServiceProvider();
        }

        // ── Access control — all 6 endpoints blocked in Production without IsUAT ──

        [Fact]
        public async Task Clear_ProductionNoUAT_Returns404()
        {
            var db = DbContextFactory.Create("debug_blocked_clear");
            Assert.IsType<NotFoundResult>(await MakeController(false, false, db).Clear());
        }

        [Fact]
        public async Task SetupTeams_ProductionNoUAT_Returns404()
        {
            var db = DbContextFactory.Create("debug_blocked_teams");
            Assert.IsType<NotFoundResult>(await MakeController(false, false, db).SetupTeams());
        }

        [Fact]
        public async Task SetupTeamsAndFixtures_ProductionNoUAT_Returns404()
        {
            var db = DbContextFactory.Create("debug_blocked_teams_fixtures");
            Assert.IsType<NotFoundResult>(await MakeController(false, false, db).SetupTeamsAndFixtures());
        }

        [Fact]
        public async Task SetupFullSeason_ProductionNoUAT_Returns404()
        {
            var db = DbContextFactory.Create("debug_blocked_full");
            Assert.IsType<NotFoundResult>(await MakeController(false, false, db).SetupFullSeason());
        }

        [Fact]
        public async Task SetupAlmostComplete_ProductionNoUAT_Returns404()
        {
            var db = DbContextFactory.Create("debug_blocked_almost");
            Assert.IsType<NotFoundResult>(await MakeController(false, false, db).SetupAlmostComplete());
        }

        [Fact]
        public async Task SetupNextSeason_ProductionNoUAT_Returns404()
        {
            var db = DbContextFactory.Create("debug_blocked_next");
            Assert.IsType<NotFoundResult>(await MakeController(false, false, db).SetupNextSeason());
        }

        // ── Clear in UAT wipes all data ────────────────────────────────────────

        [Fact]
        public async Task Clear_UAT_WipesDataAndReturnsOk()
        {
            var db = DbContextFactory.Create("debug_wipe_uat");

            db.Teams.Add(new Team { Id = 1, Name = "Test FC" });
            db.Seasons.Add(new Season { Id = 1, SeasonNumber = 1, IsActive = true, StartDate = DateTime.UtcNow.AddMonths(-1), EndDate = DateTime.UtcNow.AddMonths(5) });
            db.Players.Add(new Player { Id = 1, Name = "Player One", TeamId = 1, Position = "FW" });
            db.Fixtures.Add(new Fixture { Id = 1, HomeTeamId = 1, AwayTeamId = 1, SeasonId = 1, MatchNumber = 1, WindowStart = DateTime.UtcNow, WindowEnd = DateTime.UtcNow.AddDays(7) });
            await db.SaveChangesAsync();

            var sp = BuildServiceProvider(db);
            var result = await MakeController(false, true, db, sp).Clear();

            Assert.IsType<OkObjectResult>(result);
            Assert.Empty(db.Teams);
            Assert.Empty(db.Seasons);
            Assert.Empty(db.Players);
            Assert.Empty(db.Fixtures);
        }

        // ── SetupNextSeason early exit ─────────────────────────────────────────

        [Fact]
        public async Task SetupNextSeason_UAT_NoTeams_ReturnsBadRequest()
        {
            var db = DbContextFactory.Create("debug_next_no_teams");
            var result = await MakeController(false, true, db).SetupNextSeason();
            Assert.IsType<BadRequestObjectResult>(result);
        }

        // ── Development flag also allows access ────────────────────────────────

        [Fact]
        public async Task SetupNextSeason_Development_NoTeams_ReturnsBadRequest_NotNotFound()
        {
            var db = DbContextFactory.Create("debug_dev_next_no_teams");
            var result = await MakeController(true, false, db).SetupNextSeason();
            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
