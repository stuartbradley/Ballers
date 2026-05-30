using Ballers.Shared;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class TotwService
    {
        private readonly HttpClient _http;
        public TotwService(HttpClient http) => _http = http;

        public async Task<TeamOfTheWeekDto?> GetCurrent()
        {
            var resp = await _http.GetAsync("api/totw/current");
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<TeamOfTheWeekDto>();
        }

        public async Task<List<TeamOfTheWeekDto>> GetHistory()
            => await _http.GetFromJsonAsync<List<TeamOfTheWeekDto>>("api/totw/history") ?? new();
    }
}
