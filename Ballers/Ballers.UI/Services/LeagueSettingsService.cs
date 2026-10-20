using System.Net.Http.Json;
using Ballers.Shared;
using Ballers.UI.Infrastructure;

namespace Ballers.Services
{
    public class LeagueSettingsService
    {
        private readonly HttpClient _http;

        public LeagueSettingsService(HttpClient http)
        {
            _http = http;
        }

        // Throws rather than falling back to defaults: the admin switches would
        // otherwise show everything unlocked when the settings didn't load.
        public async Task<LeagueSettingsDto> Get()
        {
            var response = await _http.GetAsync("api/league-settings");
            return await response.ReadOk<LeagueSettingsDto>("Couldn't load the league settings.") ?? new();
        }

        public Task SetPlayersLocked(bool locked) => Put("api/league-settings/players-locked", locked);

        public Task SetFixturesLocked(bool locked) => Put("api/league-settings/fixtures-locked", locked);

        public Task SetFixturesHidden(bool hidden) => Put("api/league-settings/fixtures-hidden", hidden);

        private Task Put(string url, bool value) =>
            _http.PutOk(url, value, "Couldn't save the setting.");
    }
}
