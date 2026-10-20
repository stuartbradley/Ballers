using Ballers.Shared;
using Ballers.UI.Infrastructure;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class KnockoutService
    {
        private readonly HttpClient _http;

        public KnockoutService(HttpClient http) => _http = http;

        public Task<KnockoutBracketDto?> GetBracketAsync(int seasonId) =>
            _http.GetOrDefault<KnockoutBracketDto>($"api/knockout/{seasonId}");

        public Task GenerateKnockoutAsync(int seasonId) =>
            _http.PostOk($"api/admin/seasons/{seasonId}/generate-knockout", null, "Couldn't generate the knockout draw.");

        public Task SubmitResultAsync(int fixtureId, int homeScore, int awayScore) =>
            _http.PostOk($"api/knockout/{fixtureId}/result",
                new SubmitKnockoutResultRequest { HomeScore = homeScore, AwayScore = awayScore }, "Couldn't save the result.");
    }
}
