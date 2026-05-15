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

        public async Task<WinLossDto?> GetWinLoss()
        {
            var response = await _http.GetAsync("api/stats/winloss");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<WinLossDto>();
        }
    }
}
