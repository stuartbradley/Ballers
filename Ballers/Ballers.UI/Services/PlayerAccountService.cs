using Ballers.Shared;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class PlayerAccountService
    {
        private readonly HttpClient _http;

        public PlayerAccountService(HttpClient http) => _http = http;

        public async Task<List<ClaimablePlayerDto>> GetClaimablePlayers(int teamId)
        {
            var response = await _http.GetAsync($"api/playeraccount/teams/{teamId}/claimable");
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<ClaimablePlayerDto>>() ?? new();
        }

        /// <summary>
        /// Signs a player up. Throws with the server's reason on failure — the
        /// whole point of this form is telling someone why it did not work.
        /// </summary>
        public async Task Register(PlayerSignupRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/playeraccount/register", request);
            if (response.IsSuccessStatusCode) return;

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                throw new Exception("Too many sign-up attempts. Please try again later.");

            var message = await response.Content.ReadAsStringAsync();
            throw new Exception(string.IsNullOrWhiteSpace(message)
                ? "Unable to sign up."
                : message);
        }

        public async Task<PlayerAccountStatusDto?> GetMyStatus()
        {
            var response = await _http.GetAsync("api/playeraccount/me");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<PlayerAccountStatusDto>();
        }

        public async Task<List<PlayerAccountRequestDto>> GetRequests()
        {
            var response = await _http.GetAsync("api/playeraccount/requests");
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<PlayerAccountRequestDto>>() ?? new();
        }

        public Task Approve(int requestId) => Resolve(requestId, "approve");
        public Task Reject(int requestId) => Resolve(requestId, "reject");

        private async Task Resolve(int requestId, string action)
        {
            var response = await _http.PostAsync($"api/playeraccount/requests/{requestId}/{action}", null);
            if (response.IsSuccessStatusCode) return;

            var message = await response.Content.ReadAsStringAsync();
            throw new Exception(string.IsNullOrWhiteSpace(message)
                ? $"Unable to {action} that request."
                : message);
        }
    }
}
