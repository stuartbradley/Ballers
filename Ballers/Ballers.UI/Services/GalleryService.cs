using System.Net.Http.Json;
using Ballers.Shared;

namespace Ballers.Services
{
    public class GalleryService
    {
        private readonly HttpClient _http;

        public GalleryService(HttpClient http) => _http = http;

        public async Task<List<GallerySummaryDto>> GetGalleries()
        {
            var response = await _http.GetAsync("api/galleries");
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<GallerySummaryDto>>() ?? new();
        }

        public async Task<GalleryDetailDto?> GetGallery(int id)
        {
            var response = await _http.GetAsync($"api/galleries/{id}");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<GalleryDetailDto>();
        }

        /// <summary>Photos arrive a page at a time — a full gallery is hundreds of images.</summary>
        public async Task<GalleryPhotoPageDto> GetPhotos(int galleryId, int skip, int take)
        {
            var response = await _http.GetAsync($"api/galleries/{galleryId}/photos?skip={skip}&take={take}");
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<GalleryPhotoPageDto>() ?? new();
        }

        /// <summary>Returns the gallery id for a fixture, or null when it has none.</summary>
        public async Task<int?> GetGalleryIdForFixture(int fixtureId)
        {
            var response = await _http.GetAsync($"api/galleries/by-fixture/{fixtureId}");
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<FixtureGalleryRefDto>();
            return result?.GalleryId;
        }

        public async Task<List<PhotographerDto>> GetPhotographers()
        {
            var response = await _http.GetAsync("api/photographers");
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<PhotographerDto>>() ?? new();
        }

        // ── Admin ─────────────────────────────────────────────────────────

        public async Task<int?> CreateGallery(SaveGalleryRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/galleries", request);
            if (!response.IsSuccessStatusCode) return null;

            var created = await response.Content.ReadFromJsonAsync<CreatedRef>();
            return created?.Id;
        }

        public async Task<bool> UpdateGallery(int id, SaveGalleryRequest request)
        {
            var response = await _http.PutAsJsonAsync($"api/galleries/{id}", request);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteGallery(int id)
        {
            var response = await _http.DeleteAsync($"api/galleries/{id}");
            return response.IsSuccessStatusCode;
        }

        /// <summary>Puts a gallery back into filename order.</summary>
        public async Task<bool> ResortGallery(int galleryId)
        {
            var response = await _http.PostAsync($"api/galleries/{galleryId}/resort", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeletePhoto(int galleryId, int photoId)
        {
            var response = await _http.DeleteAsync($"api/galleries/{galleryId}/photos/{photoId}");
            return response.IsSuccessStatusCode;
        }

        public async Task<UploadResult> UploadPhotos(int galleryId, MultipartFormDataContent content)
        {
            var response = await _http.PostAsync($"api/galleries/{galleryId}/photos", content);
            if (!response.IsSuccessStatusCode)
                return new UploadResult(0, [$"Upload failed: {response.StatusCode}"]);

            return await response.Content.ReadFromJsonAsync<UploadResult>()
                   ?? new UploadResult(0, ["Upload returned no result."]);
        }

        public async Task<int?> CreatePhotographer(PhotographerDto photographer)
        {
            var response = await _http.PostAsJsonAsync("api/photographers", photographer);
            if (!response.IsSuccessStatusCode) return null;

            var created = await response.Content.ReadFromJsonAsync<CreatedRef>();
            return created?.Id;
        }

        public async Task<bool> UpdatePhotographer(int id, PhotographerDto photographer)
        {
            var response = await _http.PutAsJsonAsync($"api/photographers/{id}", photographer);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeletePhotographer(int id)
        {
            var response = await _http.DeleteAsync($"api/photographers/{id}");
            return response.IsSuccessStatusCode;
        }

        private sealed record CreatedRef(int Id);
    }

    public record UploadResult(int Added, List<string> Rejected);
}
