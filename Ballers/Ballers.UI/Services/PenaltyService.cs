using Ballers.Models;
using Ballers.UI.Infrastructure;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class PenaltyService
    {
        private readonly HttpClient _httpClient;

        public PenaltyService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public Task<PenaltyShootoutDto?> GetShootout(int fixtureId) =>
            _httpClient.GetOrDefault<PenaltyShootoutDto>($"api/penalty/{fixtureId}");

        public Task SubmitKicks(int fixtureId, List<PenaltyKickEntry> kicks) =>
            _httpClient.PostOk($"api/penalty/{fixtureId}/kicks",
                new SubmitPenaltyKicksRequest { Kicks = kicks }, "Failed to submit penalty kicks.");

        public Task<List<PenaltyTableRowDto>> GetTable(int seasonId) =>
            _httpClient.GetListOrEmpty<PenaltyTableRowDto>($"api/penalty/table/{seasonId}");
    }
}
