using Ballers.Models;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class RefereeService
    {
        private readonly HttpClient _http;

        public RefereeService(HttpClient http) => _http = http;

        public async Task<List<RefereeDto>> GetAll()
        {
            var response = await _http.GetAsync("api/referees");
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<RefereeDto>>() ?? new();
        }

        public async Task<RefereeDto?> Create(SaveRefereeRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/referees", request);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<RefereeDto>();
        }

        public async Task<RefereeDto?> Update(int id, SaveRefereeRequest request)
        {
            var response = await _http.PutAsJsonAsync($"api/referees/{id}", request);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<RefereeDto>();
        }

        public async Task<bool> Delete(int id)
        {
            var response = await _http.DeleteAsync($"api/referees/{id}");
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> AssignToFixture(int fixtureId, int? refereeId)
        {
            var response = await _http.PutAsJsonAsync($"api/fixtures/{fixtureId}/referee", new { refereeId });
            return response.IsSuccessStatusCode;
        }
    }
}
