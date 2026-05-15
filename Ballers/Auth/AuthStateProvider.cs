using System.Security.Claims;
using Ballers.Client.Auth;
using Microsoft.AspNetCore.Components.Authorization;
namespace Ballers.Auth
{
    public class AuthStateProvider : AuthenticationStateProvider
    {
        private readonly AuthService _authService;

        public AuthStateProvider(AuthService authService)
        {
            _authService = authService;
        }

        public override async Task<AuthenticationState>
            GetAuthenticationStateAsync()
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

            if (userInfo.IsAdmin)
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            else
                claims.Add(new Claim(ClaimTypes.Role, "Manager"));

            var identity = new ClaimsIdentity(claims, "serverAuth");


            return new AuthenticationState(
                new ClaimsPrincipal(identity));
        }
        public void NotifyUserAuthentication()
        {
            NotifyAuthenticationStateChanged(
                GetAuthenticationStateAsync());
        }

        public void NotifyUserLogout()
        {
            NotifyAuthenticationStateChanged(
                Task.FromResult(
                    new AuthenticationState(
                        new ClaimsPrincipal(
                            new ClaimsIdentity()))));
        }
        public void NotifyAuthChanged()
        {
            NotifyAuthenticationStateChanged(
                GetAuthenticationStateAsync());
        }
    }
}
