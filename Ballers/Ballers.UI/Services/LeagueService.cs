using Ballers.Models;
using Ballers.UI.Infrastructure;

namespace Ballers.Services
{
    public class LeagueService
    {
        private readonly HttpClient _httpClient;

        public LeagueService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public Task<List<LeagueTableRowDto>> GetTable(int seasonId) =>
            _httpClient.GetListOrEmpty<LeagueTableRowDto>($"api/fixtures/table/{seasonId}");
    }
}
