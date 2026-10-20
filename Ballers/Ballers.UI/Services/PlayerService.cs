using Ballers.Models;
using Ballers.UI.Infrastructure;

namespace Ballers.Services
{
    public class PlayerService
    {
        private readonly HttpClient _httpClient;

        public PlayerService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // Throws rather than coming back empty: an empty squad would invite the
        // manager to add everyone again.
        public async Task<List<PlayerDto>> GetMyPlayers()
        {
            var response = await _httpClient.GetAsync("api/players/my-team");
            return await response.ReadOk<List<PlayerDto>>("Couldn't load your squad.") ?? new();
        }

        public Task AddPlayer(string name, int number, string position) =>
            _httpClient.PostOk("api/players",
                new CreatePlayerRequest { Name = name, Number = number, Position = position }, "Couldn't add the player.");

        public Task AddPlayerForTeam(int teamId, string name, int number, string position) =>
            _httpClient.PostOk($"api/players/team/{teamId}",
                new CreatePlayerRequest { Name = name, Number = number, Position = position }, "Couldn't add the player.");

        public Task RemovePlayer(int id) =>
            _httpClient.DeleteOk($"api/players/{id}", "Couldn't remove the player.");

        public Task UpdatePlayer(int id, string name, int number, string position) =>
            _httpClient.PutOk($"api/players/{id}",
                new UpdatePlayerRequest { Name = name, Number = number, Position = position }, "Couldn't save the player.");

        public Task UploadPlayerImage(int id, string imageBase64) =>
            _httpClient.PutOk($"api/players/{id}/image",
                new UploadPlayerImageRequest { ImageBase64 = imageBase64 }, "Couldn't save the photo.");
    }
}
