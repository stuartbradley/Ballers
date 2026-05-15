namespace Ballers.Services
{
    using System.Net.Http.Json;
    using Ballers.Models;
    using Microsoft.AspNetCore.Components.WebAssembly.Http;

    public class AdminService
    {
        private readonly HttpClient _http;

        public AdminService(HttpClient http)
        {
            _http = http;
        }

        public async Task CreateTeam(string name, string email, string password)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "api/admin/create-team");

            request.SetBrowserRequestCredentials(
                BrowserRequestCredentials.Include);

            request.Content = JsonContent.Create(new
            {
                teamName = name,
                email,
                password
            });

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        public async Task GenerateFixtures(List<int> teamIds, DateTime startDate)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "api/admin/generate-fixtures");

            request.SetBrowserRequestCredentials(
                BrowserRequestCredentials.Include);

            request.Content = JsonContent.Create(new
            {
                teamIds,
                startDate
            });

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                var msg = body.Trim('"');
                throw new Exception(string.IsNullOrWhiteSpace(msg) ? $"Error {(int)response.StatusCode}" : msg);
            }
        }

        public async Task<List<TeamDto>> GetTeams()
            => await _http.GetFromJsonAsync<List<TeamDto>>("api/teams") ?? new();

        public async Task<List<SeasonDto>> GetSeasons()
            => await _http.GetFromJsonAsync<List<SeasonDto>>("api/seasons") ?? new();

        public async Task ActivateSeason(int id)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"api/admin/seasons/{id}/activate");
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new Exception(body.Trim('"'));
            }
        }
    }
}
