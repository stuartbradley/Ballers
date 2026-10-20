using Ballers.Shared;
using Ballers.UI.Infrastructure;

namespace Ballers.Services
{
    public class GalleryService
    {
        private readonly HttpClient _http;

        public GalleryService(HttpClient http) => _http = http;

        public Task<List<GallerySummaryDto>> GetGalleries() =>
            _http.GetListOrEmpty<GallerySummaryDto>("api/galleries");

        public Task<GalleryDetailDto?> GetGallery(int id) =>
            _http.GetOrDefault<GalleryDetailDto>($"api/galleries/{id}");

        /// <summary>Photos arrive a page at a time — a full gallery is hundreds of images.</summary>
        public async Task<GalleryPhotoPageDto> GetPhotos(int galleryId, int skip, int take) =>
            await _http.GetOrDefault<GalleryPhotoPageDto>($"api/galleries/{galleryId}/photos?skip={skip}&take={take}")
            ?? new();

        /// <summary>Returns the gallery id for a fixture, or null when it has none.</summary>
        public async Task<int?> GetGalleryIdForFixture(int fixtureId) =>
            (await _http.GetOrDefault<FixtureGalleryRefDto>($"api/galleries/by-fixture/{fixtureId}"))?.GalleryId;

        public Task<List<PhotographerDto>> GetPhotographers() =>
            _http.GetListOrEmpty<PhotographerDto>("api/photographers");

        // ── Admin ─────────────────────────────────────────────────────────
        // Each throws an ApiException with the API's reason when it fails.

        public async Task<int> CreateGallery(SaveGalleryRequest request) =>
            (await _http.PostOk<CreatedRef>("api/galleries", request, "Couldn't create the gallery."))?.Id ?? 0;

        public Task UpdateGallery(int id, SaveGalleryRequest request) =>
            _http.PutOk($"api/galleries/{id}", request, "Couldn't save the gallery.");

        public Task DeleteGallery(int id) =>
            _http.DeleteOk($"api/galleries/{id}", "Couldn't delete the gallery.");

        /// <summary>Puts a gallery back into filename order.</summary>
        public Task ResortGallery(int galleryId) =>
            _http.PostOk($"api/galleries/{galleryId}/resort", null, "Couldn't re-sort the gallery.");

        public Task DeletePhoto(int galleryId, int photoId) =>
            _http.DeleteOk($"api/galleries/{galleryId}/photos/{photoId}", "Couldn't delete the photo.");

        /// <summary>
        /// Never throws: a batch that fails outright comes back as one rejection,
        /// so the upload loop can carry on with the next batch.
        /// </summary>
        public async Task<UploadResult> UploadPhotos(int galleryId, MultipartFormDataContent content)
        {
            try
            {
                return await _http.PostOk<UploadResult>($"api/galleries/{galleryId}/photos", content, "Upload failed.")
                       ?? new UploadResult(0, ["Upload returned no result."]);
            }
            catch (ApiException ex)
            {
                return new UploadResult(0, [ex.Message]);
            }
        }

        public async Task<int> CreatePhotographer(PhotographerDto photographer) =>
            (await _http.PostOk<CreatedRef>("api/photographers", photographer, "Couldn't add the photographer."))?.Id ?? 0;

        public Task UpdatePhotographer(int id, PhotographerDto photographer) =>
            _http.PutOk($"api/photographers/{id}", photographer, "Couldn't save the photographer.");

        public Task DeletePhotographer(int id) =>
            _http.DeleteOk($"api/photographers/{id}", "Couldn't delete the photographer.");

        private sealed record CreatedRef(int Id);
    }

    public record UploadResult(int Added, List<string> Rejected);
}
