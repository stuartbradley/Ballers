using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Ballers.API.Authorization
{
    /// <summary>
    /// The named policies used on controllers, so who may call an endpoint is
    /// written once here rather than as role strings scattered across them.
    /// </summary>
    public static class Policies
    {
        public const string SiteAdmin = nameof(SiteAdmin);
        // Admins and referees: the people who run games rather than play in them.
        public const string Official = nameof(Official);
        public const string Manager = nameof(Manager);
        // A manager looking after their squad, or an admin doing it for them.
        public const string SquadStaff = nameof(SquadStaff);
        public const string Player = nameof(Player);

        public static IServiceCollection AddBallersAuthorization(this IServiceCollection services)
        {
            services.AddAuthorization(o =>
            {
                o.AddPolicy(SiteAdmin, p => p.RequireRole(AppRoles.Admin));
                o.AddPolicy(Official, p => p.RequireRole(AppRoles.Admin, AppRoles.Referee));
                o.AddPolicy(Manager, p => p.RequireRole(AppRoles.Manager));
                o.AddPolicy(SquadStaff, p => p.RequireRole(AppRoles.Manager, AppRoles.Admin));
                o.AddPolicy(Player, p => p.RequireRole(AppRoles.Player));
            });
            services.AddScoped<IAuthorizationHandler, FixtureAccessHandler>();
            return services;
        }

        public static bool IsSiteAdmin(this ClaimsPrincipal user) => user.IsInRole(AppRoles.Admin);

        // Admins and referees have no team of their own.
        public static bool IsOfficial(this ClaimsPrincipal user)
            => user.IsInRole(AppRoles.Admin) || user.IsInRole(AppRoles.Referee);
    }
}
