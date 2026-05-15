using Ballers.Models;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class SeasonService
    {
        private readonly HttpClient _httpClient;

        public SeasonService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<SeasonDto>> GetSeasons()
        {
            return await _httpClient.GetFromJsonAsync<List<SeasonDto>>("api/seasons") ?? new List<SeasonDto>();
        }

        public async Task<SeasonDto?> GetCurrentSeason()
        {
            var response = await _httpClient.GetAsync("api/seasons/current");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<SeasonDto>();
        }
    }
}
