using Ballers.Shared;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class KnockoutService
    {
        private readonly HttpClient _http;

        public KnockoutService(HttpClient http) => _http = http;

        public async Task<KnockoutBracketDto?> GetBracketAsync(int seasonId)
        {
            var response = await _http.GetAsync($"api/knockout/{seasonId}");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<KnockoutBracketDto>();
        }

        public async Task GenerateKnockoutAsync(int seasonId)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"api/admin/seasons/{seasonId}/generate-knockout");
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new Exception(body.Trim('"'));
            }
        }

        public async Task SubmitResultAsync(int fixtureId, int homeScore, int awayScore)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"api/knockout/{fixtureId}/result");
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            request.Content = JsonContent.Create(new SubmitKnockoutResultRequest { HomeScore = homeScore, AwayScore = awayScore });
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new Exception(body.Trim('"'));
            }
        }
    }
}
