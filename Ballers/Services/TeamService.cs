using Ballers.Models;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class TeamService
    {
        private readonly HttpClient _http;

        public TeamService(HttpClient http) => _http = http;

        public async Task<List<TeamProfileDto>> GetAllTeams()
        {
            var response = await _http.GetAsync("api/teams");
            if (!response.IsSuccessStatusCode) return new List<TeamProfileDto>();
            return await response.Content.ReadFromJsonAsync<List<TeamProfileDto>>() ?? new List<TeamProfileDto>();
        }

        public async Task<TeamProfileDto?> GetProfile(int teamId)
        {
            var response = await _http.GetAsync($"api/teams/{teamId}/profile");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<TeamProfileDto>();
        }

        public async Task<TeamProfileDto?> UpdateProfile(int teamId, UpdateTeamProfileRequest request)
        {
            var response = await _http.PutAsJsonAsync($"api/teams/{teamId}/profile", request);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<TeamProfileDto>();
        }

        public async Task<string?> UploadImage(int teamId, Stream imageStream, string fileName, string contentType)
        {
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(imageStream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            content.Add(fileContent, "image", fileName);

            var response = await _http.PostAsync($"api/teams/{teamId}/profile/image", content);
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<UploadImageResult>();
            return result?.Url;
        }

        private class UploadImageResult
        {
            public string? Url { get; set; }
        }
    }
}
