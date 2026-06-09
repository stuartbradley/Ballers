namespace Ballers.Services
{
    using System.Net.Http.Json;
    using Ballers.Models;
    using Microsoft.AspNetCore.Components.Forms;
    using Microsoft.AspNetCore.Components.WebAssembly.Http;

    public class AdminService
    {
        private class ImportOk { public int created { get; set; } }
        private class ImportErr { public List<string> errors { get; set; } = new(); }

        public async Task<(int Created, List<string> Errors)> ImportFixtures(
            IBrowserFile file, int seasonNumber, DateTime startDate, bool makeActive)
        {
            var content = new MultipartFormDataContent
            {
                { new StreamContent(file.OpenReadStream(5 * 1024 * 1024)), "file", file.Name },
                { new StringContent(seasonNumber.ToString()), "seasonNumber" },
                { new StringContent(startDate.ToString("yyyy-MM-dd")), "startDate" },
                { new StringContent(makeActive.ToString()), "makeActive" },
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "api/admin/import-fixtures") { Content = content };
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

            var response = await _http.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var ok = await response.Content.ReadFromJsonAsync<ImportOk>();
                return (ok?.created ?? 0, new());
            }

            try
            {
                var err = await response.Content.ReadFromJsonAsync<ImportErr>();
                if (err?.errors is { Count: > 0 }) return (0, err.errors);
            }
            catch { /* fall through */ }

            var body = await response.Content.ReadAsStringAsync();
            return (0, new() { string.IsNullOrWhiteSpace(body) ? "Import failed." : body.Trim('"') });
        }

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

        public async Task CreateReferee(string email, string password)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "api/admin/create-referee");

            request.SetBrowserRequestCredentials(
                BrowserRequestCredentials.Include);

            request.Content = JsonContent.Create(new
            {
                email,
                password
            });

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                var msg = body.Trim('"');
                throw new Exception(string.IsNullOrWhiteSpace(msg) ? $"Error {(int)response.StatusCode}" : msg);
            }
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

        public async Task DeleteSeason(int id)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, $"api/admin/seasons/{id}");
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
