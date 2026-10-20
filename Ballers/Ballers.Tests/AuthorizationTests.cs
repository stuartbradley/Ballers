using System.Security.Claims;
using Ballers.API.Authorization;
using Ballers.API.Models;
using Ballers.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Ballers.Tests
{
    public class AuthorizationTests
    {
        private static readonly FixtureSides Game = new(HomeTeamId: 10, AwayTeamId: 20);

        private static ClaimsPrincipal Signed(params string[] roles)
            => new(new ClaimsIdentity(
                roles.Select(r => new Claim(ClaimTypes.Role, r)).Append(new Claim(ClaimTypes.NameIdentifier, "u1")),
                "test"));

        private static IAuthorizationService Authorization(int? teamId = null)
        {
            var users = DbContextFactory.MockUserManager<ApplicationUser>();
            users.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(new ApplicationUser { Id = "u1", TeamId = teamId });

            return new ServiceCollection()
                .AddLogging()
                .AddScoped<UserManager<ApplicationUser>>(_ => users.Object)
                .AddBallersAuthorization()
                .BuildServiceProvider()
                .GetRequiredService<IAuthorizationService>();
        }

        private static async Task<bool> Allowed(ClaimsPrincipal user, int? teamId, FixtureAccessRequirement requirement)
            => (await Authorization(teamId).AuthorizeAsync(user, Game, requirement)).Succeeded;

        [Fact]
        public async Task SiteAdmin_IsLetIntoEveryGame()
        {
            Assert.True(await Allowed(Signed(AppRoles.Admin), null, FixtureAccessRequirement.AdminOrHomeTeam));
            Assert.True(await Allowed(Signed(AppRoles.Admin), null, FixtureAccessRequirement.AdminOrEitherTeam));
        }

        [Fact]
        public async Task Referee_OnlyWhereRefereesAreAllowed()
        {
            Assert.True(await Allowed(Signed(AppRoles.Referee), null, FixtureAccessRequirement.OfficialsOrEitherTeam));
            Assert.False(await Allowed(Signed(AppRoles.Referee), null, FixtureAccessRequirement.AdminOrEitherTeam));
        }

        [Fact]
        public async Task AwayManager_EitherTeamButNotHomeOnly()
        {
            var manager = Signed(AppRoles.Manager);
            Assert.True(await Allowed(manager, 20, FixtureAccessRequirement.OfficialsOrEitherTeam));
            Assert.False(await Allowed(manager, 20, FixtureAccessRequirement.OfficialsOrHomeTeam));
            Assert.True(await Allowed(manager, 10, FixtureAccessRequirement.AdminOrHomeTeam));
        }

        [Fact]
        public async Task ManagerOfAnotherTeam_IsKeptOut()
            => Assert.False(await Allowed(Signed(AppRoles.Manager), 99, FixtureAccessRequirement.OfficialsOrEitherTeam));

        [Fact]
        public async Task SiteAdminPolicy_ReadsTheRole()
        {
            var auth = Authorization();
            Assert.True((await auth.AuthorizeAsync(Signed(AppRoles.Admin), Policies.SiteAdmin)).Succeeded);
            Assert.False((await auth.AuthorizeAsync(Signed(AppRoles.Manager), Policies.SiteAdmin)).Succeeded);
            Assert.True((await auth.AuthorizeAsync(Signed(AppRoles.Referee), Policies.Official)).Succeeded);
        }
    }
}
