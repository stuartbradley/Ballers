using Ballers.Models;
using Ballers.UI.Infrastructure;

namespace Ballers.Services
{
    public class SeasonService
    {
        private readonly HttpClient _httpClient;

        public SeasonService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public Task<List<SeasonDto>> GetSeasons() =>
            _httpClient.GetListOrEmpty<SeasonDto>("api/seasons");

        public Task<SeasonDto?> GetCurrentSeason() =>
            _httpClient.GetOrDefault<SeasonDto>("api/seasons/current");
    }
}
