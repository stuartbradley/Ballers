using Ballers.Models;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class MotdService
    {
        private readonly HttpClient _http;

        public MotdService(HttpClient http) => _http = http;

        public async Task<List<MotdListItemDto>> GetList()
            => await _http.GetFromJsonAsync<List<MotdListItemDto>>("api/motd") ?? new();

        public async Task<MotdDetailDto?> GetById(int id)
        {
            var response = await _http.GetAsync($"api/motd/{id}");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<MotdDetailDto>();
        }

        public async Task<List<MotdFixtureOptionDto>> GetFixtureOptions()
        {
            var response = await _http.GetAsync("api/motd/fixture-options");
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<MotdFixtureOptionDto>>() ?? new();
        }

        public async Task<bool> Create(SaveMotdRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/motd", request);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> Update(int id, SaveMotdRequest request)
        {
            var response = await _http.PutAsJsonAsync($"api/motd/{id}", request);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> Delete(int id)
        {
            var response = await _http.DeleteAsync($"api/motd/{id}");
            return response.IsSuccessStatusCode;
        }
    }
}
