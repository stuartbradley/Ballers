using Ballers.Models;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class StatService
    {
        private readonly HttpClient _http;

        public StatService(HttpClient http)
        {
            _http = http;
        }

        public async Task<StatsSummaryDto> GetSummary()
        {
            return await _http.GetFromJsonAsync<StatsSummaryDto>("api/stats/summary") ?? new();
        }

        public async Task<List<PlayerGoalsDto>> GetTopGoalScorers()
        {
            return await _http.GetFromJsonAsync<List<PlayerGoalsDto>>("api/stats/top-scorers") ?? new();
        }

        public async Task<List<PlayerAssistDto>> GetTopAssists()
        {
            return await _http.GetFromJsonAsync<List<PlayerAssistDto>>("api/stats/top-assists") ?? new();
        }

        public async Task<List<PlayerMotmDto>> GetTopMotm()
        {
            return await _http.GetFromJsonAsync<List<PlayerMotmDto>>("api/stats/top-motm") ?? new();
        }

        public async Task<List<PlayerLeaderboardDto>> GetLeaderboard(int? seasonId = null)
        {
            var url = seasonId.HasValue
                ? $"api/stats/leaderboard?seasonId={seasonId}"
                : "api/stats/leaderboard";
            return await _http.GetFromJsonAsync<List<PlayerLeaderboardDto>>(url) ?? new();
        }

        // Empty on failure, so the page can hide the team tab and the totals
        // rather than show zeros.
        public async Task<List<TeamStatsDto>> GetTeamStats(int? seasonId = null)
        {
            var url = seasonId.HasValue
                ? $"api/stats/teams?seasonId={seasonId}"
                : "api/stats/teams";
            try
            {
                return await _http.GetFromJsonAsync<List<TeamStatsDto>>(url) ?? new();
            }
            catch (HttpRequestException)
            {
                return new();
            }
        }
    }
}
