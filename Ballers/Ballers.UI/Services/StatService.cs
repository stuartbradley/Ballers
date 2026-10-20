using Ballers.Models;
using Ballers.Shared;
using Ballers.UI.Infrastructure;

namespace Ballers.Services
{
    public class StatService
    {
        private readonly HttpClient _http;

        public StatService(HttpClient http)
        {
            _http = http;
        }

        public async Task<StatsSummaryDto> GetSummary() =>
            await _http.GetOrDefault<StatsSummaryDto>("api/stats/summary") ?? new();

        public Task<List<PlayerGoalsDto>> GetTopGoalScorers() =>
            _http.GetListOrEmpty<PlayerGoalsDto>("api/stats/top-scorers");

        public Task<List<PlayerAssistDto>> GetTopAssists() =>
            _http.GetListOrEmpty<PlayerAssistDto>("api/stats/top-assists");

        public Task<List<PlayerMotmDto>> GetTopMotm() =>
            _http.GetListOrEmpty<PlayerMotmDto>("api/stats/top-motm");

        public Task<List<PlayerLeaderboardDto>> GetLeaderboard(int? seasonId = null) =>
            _http.GetListOrEmpty<PlayerLeaderboardDto>(seasonId.HasValue
                ? $"api/stats/leaderboard?seasonId={seasonId}"
                : "api/stats/leaderboard");

        // Empty on failure, so the page can hide the team tab and the totals
        // rather than show zeros.
        public Task<List<TeamStatsDto>> GetTeamStats(int? seasonId = null) =>
            _http.GetListOrEmpty<TeamStatsDto>(seasonId.HasValue
                ? $"api/stats/teams?seasonId={seasonId}"
                : "api/stats/teams");
    }
}
