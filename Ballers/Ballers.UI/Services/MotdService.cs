using Ballers.Models;
using Ballers.UI.Infrastructure;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class MotdService
    {
        private readonly HttpClient _http;

        public MotdService(HttpClient http) => _http = http;

        public Task<List<MotdListItemDto>> GetList() =>
            _http.GetListOrEmpty<MotdListItemDto>("api/motd");

        public Task<MotdDetailDto?> GetById(int id) =>
            _http.GetOrDefault<MotdDetailDto>($"api/motd/{id}");

        public Task<List<MotdFixtureOptionDto>> GetFixtureOptions() =>
            _http.GetListOrEmpty<MotdFixtureOptionDto>("api/motd/fixture-options");

        public Task Create(SaveMotdRequest request) =>
            _http.PostOk("api/motd", request, "Save failed. Please try again.");

        public Task Update(int id, SaveMotdRequest request) =>
            _http.PutOk($"api/motd/{id}", request, "Save failed. Please try again.");

        public Task Delete(int id) =>
            _http.DeleteOk($"api/motd/{id}", "Couldn't delete that post.");
    }
}
