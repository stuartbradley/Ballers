using Ballers.Models;
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

        public async Task<PenaltyShootoutDto?> GetShootout(int fixtureId)
        {
            var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, $"api/penalty/{fixtureId}"));

            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<PenaltyShootoutDto>();
        }

        public async Task SubmitKicks(int fixtureId, List<PenaltyKickInput> kicks)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"api/penalty/{fixtureId}/kicks");
            request.Content = JsonContent.Create(new { kicks });
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new Exception("Failed to submit penalty kicks.");
        }

        public async Task<List<PenaltyTableRowDto>> GetTable(int seasonId)
        {
            return await _httpClient.GetFromJsonAsync<List<PenaltyTableRowDto>>(
                $"api/penalty/table/{seasonId}") ?? new();
        }
    }
}
