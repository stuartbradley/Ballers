namespace Ballers.Services
{
    using System.Net.Http.Json;
    using Ballers.Models;
    using Ballers.Shared;
    using Ballers.UI.Infrastructure;
    using Microsoft.AspNetCore.Components.Forms;

    public class AdminService
    {
        private readonly HttpClient _http;

        public AdminService(HttpClient http)
        {
            _http = http;
        }

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

            var response = await _http.PostAsync("api/admin/import-fixtures", content);
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

        public Task CreateTeam(string name, string email, string password) =>
            _http.PostOk("api/admin/create-team",
                new CreateTeamRequest { TeamName = name, Email = email, Password = password }, "Couldn't create the team.");

        public Task CreateReferee(string email, string password) =>
            _http.PostOk("api/admin/create-referee",
                new CreateRefereeRequest { Email = email, Password = password }, "Couldn't create the referee account.");

        public Task CreateAdmin(string email, string password) =>
            _http.PostOk("api/admin/create-admin",
                new CreateAdminRequest { Email = email, Password = password }, "Couldn't create the admin account.");

        public Task<PointsSettingsDto?> GetPointsSettings() =>
            _http.GetOrDefault<PointsSettingsDto>("api/admin/points-settings");

        public Task UpdatePointsSettings(PointsSettingsDto settings) =>
            _http.PutOk("api/admin/points-settings", settings, "Couldn't save the points settings.");

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
            var result = await _http.PostOk<PointsRunResponse>("api/admin/run-player-points", null, "Couldn't run the points payout.");
            return (result?.FixturesCredited ?? 0, result?.PlayersCredited ?? 0, result?.PointsAwarded ?? 0);
        }

        public Task GenerateFixtures(List<int> teamIds, DateTime startDate) =>
            _http.PostOk("api/admin/generate-fixtures",
                new GenerateFixturesRequest { TeamIds = teamIds, StartDate = startDate }, "Couldn't generate the fixtures.");

        public Task<AdminOverviewDto?> GetOverview() =>
            _http.GetOrDefault<AdminOverviewDto>("api/admin/overview");

        public Task<List<AdminAccountDto>?> GetAccounts() =>
            _http.GetOrDefault<List<AdminAccountDto>>("api/admin/accounts");

        public Task ResetAccountPassword(string id, string newPassword) =>
            _http.PostOk($"api/admin/accounts/{Uri.EscapeDataString(id)}/reset-password",
                new AdminResetPasswordRequest { NewPassword = newPassword }, "Couldn't reset the password.");

        public Task DeleteAccount(string id) =>
            _http.DeleteOk($"api/admin/accounts/{Uri.EscapeDataString(id)}", "Couldn't delete the account.");

        public Task<List<TeamDto>> GetTeams() => _http.GetListOrEmpty<TeamDto>("api/teams");

        public Task<List<SeasonDto>> GetSeasons() => _http.GetListOrEmpty<SeasonDto>("api/seasons");

        public Task ActivateSeason(int id) =>
            _http.PostOk($"api/admin/seasons/{id}/activate", null, "Couldn't activate the season.");

        public Task DeleteSeason(int id) =>
            _http.DeleteOk($"api/admin/seasons/{id}", "Couldn't delete the season.");
    }
}
