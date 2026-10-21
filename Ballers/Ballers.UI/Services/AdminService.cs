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

        public async Task CreateAdmin(string email, string password)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "api/admin/create-admin");

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

        public async Task<Ballers.Shared.PointsSettingsDto?> GetPointsSettings()
        {
            var response = await _http.GetAsync("api/admin/points-settings");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<Ballers.Shared.PointsSettingsDto>();
        }

        public async Task UpdatePointsSettings(Ballers.Shared.PointsSettingsDto settings)
        {
            var response = await _http.PutAsJsonAsync("api/admin/points-settings", settings);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                var msg = body.Trim('"');
                throw new Exception(string.IsNullOrWhiteSpace(msg) ? $"Error {(int)response.StatusCode}" : msg);
            }
        }

        private class PointsRunResponse
        {
            public int FixturesCredited { get; set; }
            public int PlayersCredited { get; set; }
            public int PointsAwarded { get; set; }
        }

        /// <summary>
        /// Forces a points payout. Returns what it credited so the admin page can
        /// say something more useful than "done".
        /// </summary>
        public async Task<(int Fixtures, int Players, int Points)> RunPlayerPoints()
        {
            var response = await _http.PostAsync("api/admin/run-player-points", null);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                var msg = body.Trim('"');
                throw new Exception(string.IsNullOrWhiteSpace(msg) ? $"Error {(int)response.StatusCode}" : msg);
            }

            var result = await response.Content.ReadFromJsonAsync<PointsRunResponse>();
            return (result?.FixturesCredited ?? 0, result?.PlayersCredited ?? 0, result?.PointsAwarded ?? 0);
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

        public async Task<Ballers.Shared.AdminOverviewDto?> GetOverview()
        {
            try { return await _http.GetFromJsonAsync<Ballers.Shared.AdminOverviewDto>("api/admin/overview"); }
            catch (HttpRequestException) { return null; }
        }

        public async Task<List<Ballers.Shared.AdminAccountDto>?> GetAccounts()
        {
            try { return await _http.GetFromJsonAsync<List<Ballers.Shared.AdminAccountDto>>("api/admin/accounts"); }
            catch (HttpRequestException) { return null; }
        }

        public async Task ResetAccountPassword(string id, string newPassword)
        {
            var response = await _http.PostAsJsonAsync($"api/admin/accounts/{Uri.EscapeDataString(id)}/reset-password",
                new Ballers.Shared.AdminResetPasswordRequest { NewPassword = newPassword });
            if (!response.IsSuccessStatusCode)
                throw new Exception(await ReadError(response));
        }

        public async Task DeleteAccount(string id)
        {
            var response = await _http.DeleteAsync($"api/admin/accounts/{Uri.EscapeDataString(id)}");
            if (!response.IsSuccessStatusCode)
                throw new Exception(await ReadError(response));
        }

        // The API sends a plain message for these errors; fall back to the
        // status when it doesn't.
        private static async Task<string> ReadError(HttpResponseMessage response)
        {
            var text = (await response.Content.ReadAsStringAsync()).Trim().Trim('"');
            return string.IsNullOrWhiteSpace(text) || text.StartsWith("{") ? $"Request failed ({(int)response.StatusCode})." : text;
        }

        // ── Walkovers ─────────────────────────────────────────────────────

        public async Task<List<Ballers.Shared.WalkoverFixtureDto>> GetWalkoverFixtures()
        {
            try { return await _http.GetFromJsonAsync<List<Ballers.Shared.WalkoverFixtureDto>>("api/admin/walkovers") ?? new(); }
            catch (HttpRequestException) { return new(); }
        }

        public async Task AwardWalkover(int fixtureId, int winnerTeamId)
        {
            var response = await _http.PostAsJsonAsync($"api/admin/fixtures/{fixtureId}/walkover",
                new Ballers.Shared.AwardWalkoverRequest { WinnerTeamId = winnerTeamId });
            if (!response.IsSuccessStatusCode)
                throw new Exception(await ReadError(response));
        }

        public async Task UndoWalkover(int fixtureId)
        {
            var response = await _http.DeleteAsync($"api/admin/fixtures/{fixtureId}/walkover");
            if (!response.IsSuccessStatusCode)
                throw new Exception(await ReadError(response));
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
