using Ballers.Models;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class LeagueService
    {
        private readonly HttpClient _httpClient;

        public LeagueService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<LeagueTableRowDto>> GetTable(int seasonId)
        {
            return await _httpClient.GetFromJsonAsync<List<LeagueTableRowDto>>($"api/fixtures/table/{seasonId}") ?? new();
        }
    }
}
