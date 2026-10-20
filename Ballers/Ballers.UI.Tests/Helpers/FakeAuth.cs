using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Ballers.UI.Tests.Helpers
{
    /// <summary>A signed-in user with the given roles and, for a manager, team.</summary>
    public class FakeAuth : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal _user;

        public FakeAuth(int? teamId = null, params string[] roles)
        {
            var claims = roles.Select(r => new Claim(ClaimTypes.Role, r)).ToList();
            if (teamId is { } id) claims.Add(new Claim("TeamId", id.ToString()));
            _user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(new AuthenticationState(_user));
    }
}
