using Ballers.Models;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class PlayerService
    {
        private readonly HttpClient _httpClient;

        public PlayerService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<PlayerDto>> GetMyPlayers()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "api/players/my-team");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<List<PlayerDto>>() ?? new();
        }

        public async Task AddPlayer(string name, int number, string position)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "api/players");

            request.Content = JsonContent.Create(new
            {
                name,
                number, 
                position
            });

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        public async Task RemovePlayer(int id)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"api/players/{id}");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
    }
}
