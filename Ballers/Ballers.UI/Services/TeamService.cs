using Ballers.Models;
using Ballers.UI.Infrastructure;

namespace Ballers.Services
{
    public class TeamService
    {
        private readonly HttpClient _http;

        public TeamService(HttpClient http) => _http = http;

        public Task<List<TeamProfileDto>> GetAllTeams() => _http.GetListOrEmpty<TeamProfileDto>("api/teams");

        /// <summary>
        /// Every club with its record, form and squad rating. Empty if it could
        /// not be fetched, so the pages can carry on without it.
        /// </summary>
        public Task<List<Ballers.Shared.TeamOverviewDto>> GetOverview() =>
            _http.GetListOrEmpty<Ballers.Shared.TeamOverviewDto>("api/teams/overview");

        public Task<TeamProfileDto?> GetProfile(int teamId) =>
            _http.GetOrDefault<TeamProfileDto>($"api/teams/{teamId}/profile");

        public Task<List<PlayerCardDto>> GetSquad(int teamId) =>
            _http.GetListOrEmpty<PlayerCardDto>($"api/teams/{teamId}/squad");

        public async Task<TeamProfileDto> UpdateProfile(int teamId, UpdateTeamProfileRequest request) =>
            await _http.PutOk<TeamProfileDto>($"api/teams/{teamId}/profile", request, "Failed to save. Please try again.")
            ?? throw new ApiException("The profile was saved but could not be read back.", 200);

        // Returns the new badge URL. Throws with the API's reason, which says
        // when an image was turned down by the content check.
        public async Task<string> UploadImage(int teamId, Stream imageStream, string fileName, string contentType)
        {
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(imageStream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            content.Add(fileContent, "image", fileName);

            var result = await _http.PostOk<UploadImageResult>($"api/teams/{teamId}/profile/image", content, "Upload failed.");
            return result?.Url ?? throw new ApiException("Upload failed.", 200);
        }

        // Manager photo is stored as a base64 data URL in the DB (same as player photos).
        public Task UploadManagerImage(int teamId, string imageBase64) =>
            _http.PutOk($"api/teams/{teamId}/manager/image",
                new UploadManagerImageRequest { ImageBase64 = imageBase64 }, "Couldn't save the manager photo.");

        private class UploadImageResult
        {
            public string? Url { get; set; }
        }
    }
}
