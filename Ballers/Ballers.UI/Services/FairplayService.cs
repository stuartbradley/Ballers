using Ballers.Models;
using Ballers.UI.Infrastructure;
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

        public Task<FairplayFixtureDto?> GetRatings(int fixtureId) =>
            _http.GetOrDefault<FairplayFixtureDto>($"api/fairplay/{fixtureId}");

        public Task SubmitRatings(int fixtureId, int homeRating, int awayRating) =>
            _http.PostOk($"api/fairplay/{fixtureId}",
                new SubmitFairplayRequest { HomeRating = homeRating, AwayRating = awayRating }, "Failed to save fairplay ratings.");

        public Task<List<FairplayTableRowDto>> GetTable(int seasonId) =>
            _http.GetListOrEmpty<FairplayTableRowDto>($"api/fairplay/table/{seasonId}");
    }
}
