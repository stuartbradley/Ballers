using Ballers.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Ballers.API.Authorization
{
    /// <summary>The two sides of a game, which is all a fixture check needs.</summary>
    public sealed record FixtureSides(int HomeTeamId, int AwayTeamId);

    /// <summary>
    /// Who, besides the site admin, may act on a game. The admin is always let
    /// in; referees and the away side only where the requirement says so.
    /// </summary>
    public sealed class FixtureAccessRequirement : IAuthorizationRequirement
    {
        public bool Referees { get; }
        public bool AwayTeam { get; }

        private FixtureAccessRequirement(bool referees, bool awayTeam)
        {
            Referees = referees;
            AwayTeam = awayTeam;
        }

        // Admins, referees and either team's manager.
        public static readonly FixtureAccessRequirement OfficialsOrEitherTeam = new(referees: true, awayTeam: true);
        // Admins, referees and the home manager: the schedule and referee.
        public static readonly FixtureAccessRequirement OfficialsOrHomeTeam = new(referees: true, awayTeam: false);
        // The site admin and either team's manager; referees are left out.
        public static readonly FixtureAccessRequirement AdminOrEitherTeam = new(referees: false, awayTeam: true);
        // The site admin and the home manager.
        public static readonly FixtureAccessRequirement AdminOrHomeTeam = new(referees: false, awayTeam: false);
    }

    /// <summary>
    /// Roles come from the sign-in cookie; the manager's team is read from the
    /// account, since a team can be reassigned while the cookie lives on.
    /// </summary>
    public sealed class FixtureAccessHandler : AuthorizationHandler<FixtureAccessRequirement, FixtureSides>
    {
        private readonly UserManager<ApplicationUser> _users;

        public FixtureAccessHandler(UserManager<ApplicationUser> users) => _users = users;

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context, FixtureAccessRequirement requirement, FixtureSides sides)
        {
            if (context.User.IsSiteAdmin()
                || (requirement.Referees && context.User.IsInRole(AppRoles.Referee)))
            {
                context.Succeed(requirement);
                return;
            }

            var teamId = (await _users.GetUserAsync(context.User))?.TeamId;
            if (teamId is null) return;

            if (teamId == sides.HomeTeamId || (requirement.AwayTeam && teamId == sides.AwayTeamId))
                context.Succeed(requirement);
        }
    }
}
