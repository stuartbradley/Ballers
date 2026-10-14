using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Services;
using Ballers.Shared;
using Ballers.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Ballers.Tests
{
    public class AdminOverviewServiceTests
    {
        private static readonly DateTime Now = new(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Week1 = new(2026, 5, 4, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Week2 = Week1.AddDays(7);
        private static readonly DateTime Week3 = Week1.AddDays(14);
        private static readonly DateTime Week4 = Week1.AddDays(21);

        private static async Task<ApplicationDbContext> Seed(string name)
        {
            var db = DbContextFactory.Create(name);
            db.Teams.AddRange(
                new Team { Id = 1, Name = "Rovers" },
                new Team { Id = 2, Name = "Unity" },
                new Team { Id = 3, Name = "Athletic" });
            db.Seasons.Add(new Season { Id = 1, SeasonNumber = 2, IsActive = true, StartDate = Week1, EndDate = Week4.AddDays(7) });
            db.Referees.Add(new Referee { Id = 1, Name = "Ref" });
            Fixture F(int id, int home, int away, DateTime week) => new()
            {
                Id = id, HomeTeamId = home, AwayTeamId = away, SeasonId = 1,
                WindowStart = week, WindowEnd = week.AddDays(7), Kickoff = week.AddDays(2), RefereeId = 1
            };

            db.Fixtures.AddRange(
                // Week 1: played.
                F(1, 1, 2, Week1).With(f => { f.IsPlayed = true; f.HomeStatsSubmitted = f.AwayStatsSubmitted = true; }),
                // Week 2: due, only the home side has sent stats.
                F(2, 2, 3, Week2).With(f => f.HomeStatsSubmitted = true),
                // Week 2: due, nothing in.
                F(3, 3, 1, Week2),
                // Week 3 (open now): no referee yet.
                F(4, 1, 3, Week3).With(f => { f.RefereeId = null; f.Kickoff = Now.AddDays(1); }),
                // Week 4: no kickoff time yet.
                F(5, 2, 1, Week4).With(f => f.Kickoff = null));
            await db.SaveChangesAsync();
            return db;
        }

        [Fact]
        public async Task Progress_CountsWeeksAndPlayedFixtures()
        {
            var db = await Seed(nameof(Progress_CountsWeeksAndPlayedFixtures));

            var dto = await new AdminOverviewService(db).GetAsync(Now);

            Assert.Equal(2, dto.SeasonNumber);
            Assert.Equal((3, 4), (dto.CurrentWeek, dto.TotalWeeks));
            Assert.Equal((1, 5), (dto.FixturesPlayed, dto.FixturesTotal));
            Assert.Equal(3, dto.Teams);
        }

        [Fact]
        public async Task Attention_FlagsEachKindOfProblem()
        {
            var db = await Seed(nameof(Attention_FlagsEachKindOfProblem));

            var dto = await new AdminOverviewService(db).GetAsync(Now);

            var stats = Assert.Single(dto.Attention, a => a.Kind == "stats");
            Assert.Equal("Waiting for Athletic's stats", stats.Detail);
            Assert.Equal("/fixture/2", stats.Link);

            Assert.Equal("/fixture/3", Assert.Single(dto.Attention, a => a.Kind == "result").Link);
            Assert.Equal("/fixture/4", Assert.Single(dto.Attention, a => a.Kind == "referee").Link);
            Assert.Equal("/fixture/5", Assert.Single(dto.Attention, a => a.Kind == "kickoff").Link);
        }

        [Fact]
        public async Task Attention_IncludesPendingSignups()
        {
            var db = await Seed(nameof(Attention_IncludesPendingSignups));
            db.Users.Add(new ApplicationUser { Id = "u1", UserName = "p@x", Email = "p@x" });
            db.PlayerAccountRequests.Add(new PlayerAccountRequest
            {
                UserId = "u1", RequestedTeamId = 1, RequestedName = "New Lad", CreatedAt = Now.AddDays(-1)
            });
            await db.SaveChangesAsync();

            var dto = await new AdminOverviewService(db).GetAsync(Now);

            Assert.Equal(1, dto.PendingSignups);
            Assert.Contains(dto.Attention, a => a.Kind == "signup");
            Assert.Contains(dto.Activity, a => a.Kind == "signup" && a.Text == "New Lad signed up for Rovers");
        }

        [Fact]
        public async Task Activity_ListsEachResultOnceNewestFirst()
        {
            var db = await Seed(nameof(Activity_ListsEachResultOnceNewestFirst));
            db.Users.AddRange(
                new ApplicationUser { Id = "a", UserName = "a@x" },
                new ApplicationUser { Id = "b", UserName = "b@x" });
            // The same result notified to two people, and an older one.
            db.Notifications.AddRange(
                new Notification { UserId = "a", Type = NotificationType.ResultSubmitted, Message = "Rovers 2-1 Unity", ReferenceId = 1, CreatedAt = Now.AddDays(-2) },
                new Notification { UserId = "b", Type = NotificationType.ResultSubmitted, Message = "Rovers 2-1 Unity", ReferenceId = 1, CreatedAt = Now.AddDays(-2) },
                new Notification { UserId = "a", Type = NotificationType.ResultSubmitted, Message = "Older", ReferenceId = 9, CreatedAt = Now.AddDays(-5) },
                new Notification { UserId = "a", Type = NotificationType.ResultSubmitted, Message = "Too old", ReferenceId = 8, CreatedAt = Now.AddDays(-60) });
            await db.SaveChangesAsync();

            var dto = await new AdminOverviewService(db).GetAsync(Now);

            var results = dto.Activity.Where(a => a.Kind == "result").Select(a => a.Text).ToList();
            Assert.Equal(new[] { "Rovers 2-1 Unity", "Older" }, results);
        }

        [Fact]
        public async Task NoActiveSeason_StillReturnsCounts()
        {
            var db = DbContextFactory.Create(nameof(NoActiveSeason_StillReturnsCounts));
            db.Teams.Add(new Team { Id = 1, Name = "Solo" });
            await db.SaveChangesAsync();

            var dto = await new AdminOverviewService(db).GetAsync(Now);

            Assert.Null(dto.SeasonNumber);
            Assert.Equal(1, dto.Teams);
            Assert.Empty(dto.Attention);
        }
    }

    public class AdminAccountTests
    {
        private static (AdminService Svc, UserManager<ApplicationUser> Users, ApplicationDbContext Db) Build(string name)
        {
            var db = DbContextFactory.Create(name);
            var services = new ServiceCollection();
            services.AddSingleton(db);
            services.AddLogging();
            services.AddDataProtection();
            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();
            var sp = services.BuildServiceProvider();
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            return (new AdminService(db, users), users, db);
        }

        private static async Task<ApplicationUser> AddUser(UserManager<ApplicationUser> users, string email, string? role, int? teamId = null)
        {
            var user = new ApplicationUser { UserName = email, Email = email, TeamId = teamId };
            Assert.True((await users.CreateAsync(user, "Passw0rd!")).Succeeded);
            if (role != null) await users.AddToRoleAsync(user, role);
            return user;
        }

        private static async Task SeedRoles(ApplicationDbContext db)
        {
            foreach (var r in new[] { "Admin", "Manager", "Referee", "Player" })
                db.Roles.Add(new IdentityRole(r) { NormalizedName = r.ToUpperInvariant() });
            await db.SaveChangesAsync();
        }

        [Fact]
        public async Task GetAccounts_GivesEachLoginItsRoleAndTeam()
        {
            var (svc, users, db) = Build(nameof(GetAccounts_GivesEachLoginItsRoleAndTeam));
            await SeedRoles(db);
            db.Teams.Add(new Team { Id = 1, Name = "Rovers" });
            await db.SaveChangesAsync();
            var me = await AddUser(users, "admin@x", "Admin");
            await AddUser(users, "boss@x", "Manager", teamId: 1);
            var pending = await AddUser(users, "new@x", null);
            db.PlayerAccountRequests.Add(new PlayerAccountRequest
            {
                UserId = pending.Id, RequestedTeamId = 1, RequestedName = "New Lad", CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var list = await svc.GetAccountsAsync(me.Id);

            Assert.Equal(new[] { "Admin", "Manager", "Pending" }, list.Select(a => a.Role));
            Assert.True(list[0].IsYou);
            Assert.Equal("Rovers", list[1].TeamName);
            Assert.Equal(("New Lad", "Rovers"), (list[2].PlayerName, list[2].TeamName));
        }

        [Fact]
        public async Task Delete_RefusesYourOwnAccountAndTheLastAdmin()
        {
            var (svc, users, db) = Build(nameof(Delete_RefusesYourOwnAccountAndTheLastAdmin));
            await SeedRoles(db);
            var me = await AddUser(users, "me@x", "Manager");
            var onlyAdmin = await AddUser(users, "admin@x", "Admin");

            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteAccountAsync(me.Id, me.Id));
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteAccountAsync(onlyAdmin.Id, me.Id));
        }

        [Fact]
        public async Task Delete_RemovesTheLoginButKeepsTheTeam()
        {
            var (svc, users, db) = Build(nameof(Delete_RemovesTheLoginButKeepsTheTeam));
            await SeedRoles(db);
            db.Teams.Add(new Team { Id = 1, Name = "Rovers" });
            await db.SaveChangesAsync();
            var me = await AddUser(users, "admin@x", "Admin");
            var boss = await AddUser(users, "boss@x", "Manager", teamId: 1);

            await svc.DeleteAccountAsync(boss.Id, me.Id);

            Assert.Null(await users.FindByIdAsync(boss.Id));
            Assert.Single(db.Teams);
        }

        [Fact]
        public async Task ResetPassword_SetsTheNewPassword()
        {
            var (svc, users, db) = Build(nameof(ResetPassword_SetsTheNewPassword));
            await SeedRoles(db);
            var boss = await AddUser(users, "boss@x", "Manager");

            await svc.ResetPasswordAsync(boss.Id, "NewPassw0rd!");

            Assert.True(await users.CheckPasswordAsync((await users.FindByIdAsync(boss.Id))!, "NewPassw0rd!"));
        }
    }

    internal static class TestExtensions
    {
        public static T With<T>(this T item, Action<T> change)
        {
            change(item);
            return item;
        }
    }
}
