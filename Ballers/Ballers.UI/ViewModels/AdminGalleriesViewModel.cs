using Ballers.Services;
using Ballers.Shared;
using Ballers.UI.Infrastructure;
using Microsoft.AspNetCore.Components.Forms;

namespace Ballers.ViewModels
{
    /// <summary>
    /// Admin for match galleries and the photographers credited on them:
    /// create a gallery for a fixture, upload its photos, publish it.
    /// </summary>
    public class AdminGalleriesViewModel : ViewModelBase
    {
        private readonly GalleryService _galleries;
        private readonly FixtureService _fixtures;

        public AdminGalleriesViewModel(GalleryService galleries, FixtureService fixtures)
        {
            _galleries = galleries;
            _fixtures = fixtures;
        }

        public bool IsLoading { get; private set; } = true;
        public string? Message { get; private set; }
        public bool MessageIsError { get; private set; }

        public List<GallerySummaryDto> Galleries { get; private set; } = new();
        public List<PhotographerDto> Photographers { get; private set; } = new();
        private List<FixtureListItemDto> _allFixtures = new();

        public PhotographerDto PhotographerForm { get; private set; } = new();
        public int? EditingPhotographerId { get; private set; }

        public int NewGalleryFixtureId { get; set; }
        public int NewGalleryPhotographerId { get; set; }
        public string? NewGalleryTitle { get; set; }
        public string? NewGalleryDescription { get; set; }

        public int? UploadingGalleryId { get; private set; }
        public int UploadedCount { get; private set; }
        public int UploadTotal { get; private set; }

        // Photos go up in small batches. Sending a whole 200-photo gallery in one
        // request would blow past the server's request size limit and give no
        // feedback until it finished.
        public const int UploadBatchSize = 5;
        public const long MaxFileBytes = 15 * 1024 * 1024;

        // Fixtures that don't have a gallery yet.
        public List<FixtureListItemDto> AvailableFixtures =>
            _allFixtures.Where(f => Galleries.All(g => g.FixtureId != f.Id)).ToList();

        public async Task InitializeAsync()
        {
            await ReloadAsync();
            _allFixtures = await _fixtures.GetFixtures();
            IsLoading = false;
        }

        private async Task ReloadAsync()
        {
            Galleries = await _galleries.GetGalleries();
            Photographers = await _galleries.GetPhotographers();
        }

        public static string FixtureLabel(FixtureListItemDto f)
            => $"Week {f.Week} — {f.HomeTeam} v {f.AwayTeam}" +
               (f.Kickoff.HasValue ? $" ({f.Kickoff.Value:d MMM yyyy})" : "");

        // Runs an admin write, showing the API's reason when it fails.
        private async Task<bool> Try(Func<Task> write)
        {
            try
            {
                await write();
                return true;
            }
            catch (ApiException ex)
            {
                Show(ex.Message, error: true);
                return false;
            }
        }

        // ── Photographers ──

        public async Task SavePhotographer()
        {
            if (string.IsNullOrWhiteSpace(PhotographerForm.Name))
            {
                Show("A photographer needs a name.", error: true);
                return;
            }

            var saved = await Try(() => EditingPhotographerId is { } id
                ? _galleries.UpdatePhotographer(id, PhotographerForm)
                : _galleries.CreatePhotographer(PhotographerForm));
            if (!saved) return;

            PhotographerForm = new PhotographerDto();
            EditingPhotographerId = null;
            await ReloadAsync();
            Show("Photographer saved.");
        }

        public void EditPhotographer(PhotographerDto p)
        {
            EditingPhotographerId = p.Id;
            PhotographerForm = new PhotographerDto
            {
                Name = p.Name,
                Website = p.Website,
                Instagram = p.Instagram,
                Facebook = p.Facebook,
                Email = p.Email,
                Bio = p.Bio,
                LogoUrl = p.LogoUrl
            };
        }

        public void CancelPhotographerEdit()
        {
            EditingPhotographerId = null;
            PhotographerForm = new PhotographerDto();
        }

        public async Task DeletePhotographer(PhotographerDto p)
        {
            if (!await Try(() => _galleries.DeletePhotographer(p.Id))) return;

            await ReloadAsync();
            Show($"{p.Name} removed. Their galleries are still there, just uncredited.");
        }

        // ── Galleries ──

        public async Task CreateGallery()
        {
            if (NewGalleryFixtureId == 0)
            {
                Show("Choose a fixture first.", error: true);
                return;
            }

            var request = new SaveGalleryRequest
            {
                FixtureId = NewGalleryFixtureId,
                PhotographerId = NewGalleryPhotographerId == 0 ? null : NewGalleryPhotographerId,
                Title = string.IsNullOrWhiteSpace(NewGalleryTitle) ? null : NewGalleryTitle,
                Description = string.IsNullOrWhiteSpace(NewGalleryDescription) ? null : NewGalleryDescription,
                IsPublished = true
            };

            if (!await Try(() => _galleries.CreateGallery(request))) return;

            NewGalleryFixtureId = 0;
            NewGalleryPhotographerId = 0;
            NewGalleryTitle = null;
            NewGalleryDescription = null;

            await ReloadAsync();
            Show("Gallery created — add photos to it below.");
        }

        public async Task TogglePublished(GallerySummaryDto g)
        {
            var request = new SaveGalleryRequest
            {
                FixtureId = g.FixtureId,
                Title = g.Title,
                IsPublished = !g.IsPublished
            };

            if (!await Try(() => _galleries.UpdateGallery(g.Id, request))) return;

            await ReloadAsync();
        }

        public async Task Resort(GallerySummaryDto g)
        {
            if (!await Try(() => _galleries.ResortGallery(g.Id))) return;

            await ReloadAsync();
            Show($"{g.HomeTeam} v {g.AwayTeam} is back in filename order.");
        }

        public async Task DeleteGallery(GallerySummaryDto g)
        {
            if (!await Try(() => _galleries.DeleteGallery(g.Id))) return;

            await ReloadAsync();
            Show("Gallery deleted.");
        }

        public async Task UploadFiles(int galleryId, IReadOnlyList<IBrowserFile> files)
        {
            if (files.Count == 0) return;

            UploadingGalleryId = galleryId;
            UploadedCount = 0;
            UploadTotal = files.Count;
            var problems = new List<string>();

            foreach (var batch in files.Chunk(UploadBatchSize))
            {
                using var content = new MultipartFormDataContent();

                foreach (var file in batch)
                {
                    if (file.Size > MaxFileBytes)
                    {
                        problems.Add($"{file.Name} is over 15 MB");
                        continue;
                    }

                    var stream = file.OpenReadStream(MaxFileBytes);
                    var streamContent = new StreamContent(stream);
                    streamContent.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                    content.Add(streamContent, "files", file.Name);
                }

                var result = await _galleries.UploadPhotos(galleryId, content);
                problems.AddRange(result.Rejected);

                UploadedCount += batch.Length;
                NotifyChanged();
            }

            UploadingGalleryId = null;
            await ReloadAsync();

            Show(problems.Count == 0
                ? $"Uploaded {UploadTotal} photos."
                : $"Uploaded with {problems.Count} problem(s): {string.Join("; ", problems.Take(3))}",
                error: problems.Count > 0);
        }

        private void Show(string text, bool error = false)
        {
            Message = text;
            MessageIsError = error;
        }
    }
}
