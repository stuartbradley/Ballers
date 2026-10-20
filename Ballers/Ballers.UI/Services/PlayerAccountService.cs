using Ballers.Shared;
using Ballers.UI.Infrastructure;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class PlayerAccountService
    {
        private readonly HttpClient _http;

        public PlayerAccountService(HttpClient http) => _http = http;

        public Task<List<ClaimablePlayerDto>> GetClaimablePlayers(int teamId) =>
            _http.GetListOrEmpty<ClaimablePlayerDto>($"api/playeraccount/teams/{teamId}/claimable");

        /// <summary>
        /// Signs a player up. Throws with the server's reason on failure — the
        /// whole point of this form is telling someone why it did not work.
        /// </summary>
        public async Task Register(PlayerSignupRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/playeraccount/register", request);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                throw new ApiException("Too many sign-up attempts. Please try again later.", 429);

            await response.EnsureOk("Unable to sign up.");
        }

        public Task<PlayerAccountStatusDto?> GetMyStatus() =>
            _http.GetOrDefault<PlayerAccountStatusDto>("api/playeraccount/me");

        public Task<List<PlayerAccountRequestDto>> GetRequests() =>
            _http.GetListOrEmpty<PlayerAccountRequestDto>("api/playeraccount/requests");

        public Task Approve(int requestId) => Resolve(requestId, "approve");
        public Task Reject(int requestId) => Resolve(requestId, "reject");

        private Task Resolve(int requestId, string action) =>
            _http.PostOk($"api/playeraccount/requests/{requestId}/{action}", null, $"Unable to {action} that request.");
    }
}
