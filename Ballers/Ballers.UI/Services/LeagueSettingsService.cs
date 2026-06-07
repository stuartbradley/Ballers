using System.Net.Http.Json;
using Ballers.Shared;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Ballers.Services
{
    public class LeagueSettingsService
    {
        private readonly HttpClient _http;

        public LeagueSettingsService(HttpClient http)
        {
            _http = http;
        }

        public async Task<LeagueSettingsDto> Get()
            => await _http.GetFromJsonAsync<LeagueSettingsDto>("api/league-settings") ?? new();

        public async Task SetPlayersLocked(bool locked)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, "api/league-settings/players-locked");
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            request.Content = JsonContent.Create(locked);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        public async Task SetFixturesLocked(bool locked)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, "api/league-settings/fixtures-locked");
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            request.Content = JsonContent.Create(locked);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
    }
}
