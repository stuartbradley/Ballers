using System.Security.Claims;
using Ballers.Client.Auth;
using Microsoft.AspNetCore.Components.Authorization;
namespace Ballers.Auth
{
    public class AuthStateProvider : AuthenticationStateProvider
    {
        private readonly AuthService _authService;
        private Task<AuthenticationState>? _cachedState;

        public AuthStateProvider(AuthService authService)
        {
            _authService = authService;
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return _cachedState ??= BuildStateAsync();
        }

        private async Task<AuthenticationState> BuildStateAsync()
        {
            var userInfo = await _authService.GetCurrentUser();

            if (userInfo == null)
                return new AuthenticationState(
                    new ClaimsPrincipal(new ClaimsIdentity()));


            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, userInfo.Email)
            };

            if (userInfo.TeamId != null)
            {
                claims.Add(new Claim("TeamId",
                    userInfo.TeamId.ToString()!));
            }

            // ⭐ THIS IS THE MISSING PART
            if (!string.IsNullOrWhiteSpace(userInfo.TeamName))
            {
                claims.Add(new Claim("TeamName", userInfo.TeamName));
            }

            if (userInfo.PlayerId != null)
            {
                claims.Add(new Claim("PlayerId", userInfo.PlayerId.Value.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(userInfo.PlayerName))
            {
                claims.Add(new Claim("PlayerName", userInfo.PlayerName));
            }

            // Explicit precedence, and no catch-all. A player account must not fall
            // through to Manager: that would hand it the squad, team and stat
            // submission pages. An account that is none of these — a player still
            // waiting on their manager — gets no role at all, which is correct.
            if (userInfo.IsAdmin)
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            else if (userInfo.IsReferee)
                claims.Add(new Claim(ClaimTypes.Role, "Referee"));
            else if (userInfo.PlayerId != null)
                claims.Add(new Claim(ClaimTypes.Role, "Player"));
            else if (userInfo.TeamId != null)
                claims.Add(new Claim(ClaimTypes.Role, "Manager"));

            if (userInfo.IsPendingPlayer)
            {
                claims.Add(new Claim("PendingPlayer", "true"));
            }

            var identity = new ClaimsIdentity(claims, "serverAuth");


            return new AuthenticationState(
                new ClaimsPrincipal(identity));
        }
        public void NotifyUserLogout()
        {
            _cachedState = null;
            var anonymous = Task.FromResult(
                new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
            _cachedState = anonymous;
            NotifyAuthenticationStateChanged(anonymous);
        }

        public void NotifyAuthChanged()
        {
            _cachedState = null;
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }
}
