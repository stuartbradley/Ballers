using Ballers.Models;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class FairplayService
    {
        private readonly HttpClient _http;

        public FairplayService(HttpClient http)
        {
            _http = http;
        }

        public async Task<FairplayFixtureDto?> GetRatings(int fixtureId)
        {
            var response = await _http.GetAsync($"api/fairplay/{fixtureId}");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<FairplayFixtureDto>();
        }

        public async Task SubmitRatings(int fixtureId, int homeRating, int awayRating)
        {
            var response = await _http.PostAsJsonAsync($"api/fairplay/{fixtureId}",
                new SubmitFairplayRequest { HomeRating = homeRating, AwayRating = awayRating });
            if (!response.IsSuccessStatusCode)
                throw new Exception("Failed to save fairplay ratings.");
        }

        public async Task<List<FairplayTableRowDto>> GetTable(int seasonId)
        {
            var response = await _http.GetAsync($"api/fairplay/table/{seasonId}");
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<FairplayTableRowDto>>() ?? new();
        }
    }
}
