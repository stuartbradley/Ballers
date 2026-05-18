using Ballers.Models;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class FixtureService
    {
        private HttpClient _httpClient;

        public FixtureService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<FixtureDto>> GetFixtures()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "api/fixtures");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content
                .ReadFromJsonAsync<List<FixtureDto>>() ?? [];
        }

        public async Task<FixtureWeekDto?> GetCurrentWeek()
        {
            var response = await _httpClient.GetAsync("api/fixtures/current-week");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<FixtureWeekDto>();
        }

        public async Task<List<FixtureWeekDto>> GetFixtureWeeks()
        {
            var response = await _httpClient.GetAsync("api/fixtures/weeks");
            if (!response.IsSuccessStatusCode) return [];
            return await response.Content.ReadFromJsonAsync<List<FixtureWeekDto>>() ?? [];
        }

        public async Task<FixtureDetailsDto?> GetFixture(int id)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/fixtures/{id}");
           
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content
                .ReadFromJsonAsync<FixtureDetailsDto>();
        }
        public async Task SaveSquad(
                        int fixtureId,
                        List<int> playerIds)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"api/fixtures/{fixtureId}/squad");

            request.Content = JsonContent.Create(new
            {
                playerIds
            });

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                throw new Exception("Unable to save squad.");
        }

        public async Task<List<FixtureDto>> GetNextFixtures()
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                "api/fixtures/next-fixtures");

            request.SetBrowserRequestCredentials(
                BrowserRequestCredentials.Include);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return new List<FixtureDto>();

            return await response.Content
                .ReadFromJsonAsync<List<FixtureDto>>() ?? new();
        }

        public async Task UpdateFixtureSchedule(int fixtureId, string location, string? postcode, DateTime kickoffTime)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"api/fixtures/{fixtureId}/schedule");

            request.Content = JsonContent.Create(new
            {
                location,
                postcode,
                kickoffTime,
            });

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                throw new Exception("unable to update fixture");
        }

        public async Task SubmitStats(
                        int fixtureId,
                        List<FixturePlayerStatInput> stats)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"api/fixtures/{fixtureId}/stats");

            request.Content = JsonContent.Create(new
            {
                PlayerStats = stats
            });
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                throw new Exception("Unable to submit stats.");
        }


        public async Task<List<PlayerStatsDto>> GetFixtureStats(int fixtureId)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/fixtures/{fixtureId}/stats");

            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return new List<PlayerStatsDto>();

            return await response.Content.ReadFromJsonAsync<List<PlayerStatsDto>>() ?? new();
        }
        public async Task<List<PlayerDto>> GetFixturePlayers(int fixtureId)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/fixtures/{fixtureId}/players");

            request.SetBrowserRequestCredentials(
                BrowserRequestCredentials.Include);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return new List<PlayerDto>();

            return await response.Content.ReadFromJsonAsync<List<PlayerDto>>() ?? new();
        }
        public async Task<List<HeadToHeadResultDto>> GetHeadToHead(int fixtureId)
        {
            var response = await _httpClient.GetAsync($"api/fixtures/{fixtureId}/head-to-head");
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<HeadToHeadResultDto>>() ?? new();
        }

        public async Task SaveCaptaincy(int fixtureId, int? captainId, int? viceId)
        {
            await _httpClient.PutAsJsonAsync($"api/fixtures/{fixtureId}/captaincy", new
            {
                captainPlayerId = captainId,
                viceCaptainPlayerId = viceId
            });
        }

        public async Task<List<OpponentPlayerStatDto>> GetOpponentStats(int fixtureId, int? teamId = null)
        {
            var url = teamId.HasValue
                ? $"api/fixtures/{fixtureId}/opponent-stats?teamId={teamId}"
                : $"api/fixtures/{fixtureId}/opponent-stats";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<OpponentPlayerStatDto>>() ?? new();
        }

        public async Task<List<FixtureSquadPlayerDto>> GetFixtureSquad(int fixtureId)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/fixtures/{fixtureId}/squad");

            request.SetBrowserRequestCredentials(
                BrowserRequestCredentials.Include);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return new List<FixtureSquadPlayerDto>();

            return await response.Content.ReadFromJsonAsync<List<FixtureSquadPlayerDto>>() ?? new();
        }

    }
}
