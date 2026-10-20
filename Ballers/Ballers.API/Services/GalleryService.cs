using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Shared;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    /// <summary>An uploaded file, without tying the service to ASP.NET's IFormFile.</summary>
    public sealed record UploadedFile(string FileName, string ContentType, long Length, Func<Stream> OpenRead);

    public sealed record GalleryUploadResult(int Added, List<string> Rejected);

    public interface IGalleryService
    {
        // includeHidden lets the admin see galleries still being put together.
        Task<List<GallerySummaryDto>> GetAllAsync(bool includeHidden);
        Task<GalleryDetailDto?> GetAsync(int id, bool includeHidden);
        Task<GalleryPhotoPageDto?> GetPhotosAsync(int id, bool includeHidden, int skip, int take);
        Task<FixtureGalleryRefDto?> GetByFixtureAsync(int fixtureId, bool includeHidden);
        Task<Outcome<int>> CreateAsync(SaveGalleryRequest request);
        Task<Outcome> UpdateAsync(int id, SaveGalleryRequest request);
        Task<Outcome<GalleryUploadResult>> UploadAsync(int id, IReadOnlyList<UploadedFile> files);
        Task<Outcome<int>> ResortAsync(int id);
        Task<Outcome> DeletePhotoAsync(int id, int photoId);
        Task<Outcome> DeleteAsync(int id);
    }

    public class GalleryService : IGalleryService
    {
        // Galleries run to hundreds of photos, so the page loads them in chunks
        // rather than dropping the whole set into one response.
        public const int DefaultPageSize = 40;
        public const int MaxPageSize = 100;

        private const long MaxUploadBytes = 15 * 1024 * 1024;
        private static readonly string[] AllowedContentTypes =
            ["image/jpeg", "image/png", "image/webp"];

        private readonly ApplicationDbContext _db;
        private readonly IGalleryStorageService _storage;

        public GalleryService(ApplicationDbContext db, IGalleryStorageService storage)
        {
            _db = db;
            _storage = storage;
        }

        private IQueryable<Gallery> Visible(bool includeHidden)
            => _db.Galleries.AsNoTracking().Where(g => includeHidden || g.IsPublished);

        public async Task<List<GallerySummaryDto>> GetAllAsync(bool includeHidden)
            => await Visible(includeHidden)
                .OrderByDescending(g => g.Fixture!.Kickoff ?? g.Fixture.WindowStart)
                .ThenByDescending(g => g.Id)
                .Select(g => new GallerySummaryDto
                {
                    Id = g.Id,
                    FixtureId = g.FixtureId,
                    Title = g.Title,
                    HomeTeam = g.Fixture!.HomeTeam!.Name,
                    AwayTeam = g.Fixture.AwayTeam!.Name,
                    HomeScore = g.Fixture.HomeScore,
                    AwayScore = g.Fixture.AwayScore,
                    IsPlayed = g.Fixture.IsPlayed,
                    Kickoff = g.Fixture.Kickoff,
                    Week = g.Fixture.MatchNumber,
                    PhotographerName = g.Photographer != null ? g.Photographer.Name : null,
                    PhotoCount = g.Photos.Count,
                    IsPublished = g.IsPublished,
                    CoverThumbnailUrl = g.Photos
                        .OrderBy(p => p.SortOrder)
                        .Select(p => p.ThumbnailUrl)
                        .FirstOrDefault(),
                    CoverImageUrl = g.Photos
                        .OrderBy(p => p.SortOrder)
                        .Select(p => p.ImageUrl)
                        .FirstOrDefault()
                })
                .ToListAsync();

        public async Task<GalleryDetailDto?> GetAsync(int id, bool includeHidden)
            => await Visible(includeHidden)
                .Where(g => g.Id == id)
                .Select(g => new GalleryDetailDto
                {
                    Id = g.Id,
                    FixtureId = g.FixtureId,
                    Title = g.Title,
                    Description = g.Description,
                    HomeTeam = g.Fixture!.HomeTeam!.Name,
                    AwayTeam = g.Fixture.AwayTeam!.Name,
                    HomeScore = g.Fixture.HomeScore,
                    AwayScore = g.Fixture.AwayScore,
                    IsPlayed = g.Fixture.IsPlayed,
                    Kickoff = g.Fixture.Kickoff,
                    Week = g.Fixture.MatchNumber,
                    Location = g.Fixture.Location,
                    PhotoCount = g.Photos.Count,
                    IsPublished = g.IsPublished,
                    Photographer = g.Photographer == null ? null : new PhotographerDto
                    {
                        Id = g.Photographer.Id,
                        Name = g.Photographer.Name,
                        Website = g.Photographer.Website,
                        Instagram = g.Photographer.Instagram,
                        Facebook = g.Photographer.Facebook,
                        Email = g.Photographer.Email,
                        Bio = g.Photographer.Bio,
                        LogoUrl = g.Photographer.LogoUrl
                    }
                })
                .FirstOrDefaultAsync();

        public async Task<GalleryPhotoPageDto?> GetPhotosAsync(int id, bool includeHidden, int skip, int take)
        {
            if (!await Visible(includeHidden).AnyAsync(g => g.Id == id)) return null;

            take = Math.Clamp(take, 1, MaxPageSize);
            skip = Math.Max(0, skip);

            var query = _db.GalleryPhotos.AsNoTracking().Where(p => p.GalleryId == id);
            var total = await query.CountAsync();

            var photos = await query
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Id)
                .Skip(skip)
                .Take(take)
                .Select(p => new GalleryPhotoDto
                {
                    Id = p.Id,
                    ImageUrl = p.ImageUrl,
                    ThumbnailUrl = p.ThumbnailUrl,
                    Width = p.Width,
                    Height = p.Height,
                    SortOrder = p.SortOrder
                })
                .ToListAsync();

            return new GalleryPhotoPageDto
            {
                Photos = photos,
                Total = total,
                HasMore = skip + photos.Count < total
            };
        }

        // The fixture page shows its "View gallery" link whenever the fixture has
        // a gallery. An empty one still gets a link rather than silently
        // vanishing, which is far easier to make sense of than a button that
        // never appears.
        public async Task<FixtureGalleryRefDto?> GetByFixtureAsync(int fixtureId, bool includeHidden)
            => await Visible(includeHidden)
                .Where(g => g.FixtureId == fixtureId)
                .Select(g => new FixtureGalleryRefDto
                {
                    GalleryId = g.Id,
                    PhotoCount = g.Photos.Count
                })
                .FirstOrDefaultAsync();

        public async Task<Outcome<int>> CreateAsync(SaveGalleryRequest request)
        {
            if (!await _db.Fixtures.AnyAsync(f => f.Id == request.FixtureId))
                return Outcome.Invalid("That fixture does not exist.");

            if (await _db.Galleries.AnyAsync(g => g.FixtureId == request.FixtureId))
                return Outcome.Conflict("That fixture already has a gallery.");

            var gallery = new Gallery
            {
                FixtureId = request.FixtureId,
                PhotographerId = request.PhotographerId,
                Title = request.Title,
                Description = request.Description,
                IsPublished = request.IsPublished
            };

            _db.Galleries.Add(gallery);
            await _db.SaveChangesAsync();
            return gallery.Id;
        }

        public async Task<Outcome> UpdateAsync(int id, SaveGalleryRequest request)
        {
            var gallery = await _db.Galleries.FindAsync(id);
            if (gallery == null) return Outcome.NotFound();

            gallery.PhotographerId = request.PhotographerId;
            gallery.Title = request.Title;
            gallery.Description = request.Description;
            gallery.IsPublished = request.IsPublished;

            await _db.SaveChangesAsync();
            return Outcome.Ok;
        }

        public async Task<Outcome<GalleryUploadResult>> UploadAsync(int id, IReadOnlyList<UploadedFile> files)
        {
            if (await _db.Galleries.FindAsync(id) == null) return Outcome.NotFound();
            if (files.Count == 0) return Outcome.Invalid("No files provided.");

            var added = 0;
            var rejected = new List<string>();

            foreach (var file in files)
            {
                if (file.Length == 0 || file.Length > MaxUploadBytes)
                {
                    rejected.Add($"{file.FileName}: must be between 1 byte and 15 MB");
                    continue;
                }

                if (!AllowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
                {
                    rejected.Add($"{file.FileName}: only JPEG, PNG and WebP are accepted");
                    continue;
                }

                try
                {
                    await using var stream = file.OpenRead();
                    var stored = await _storage.SaveAsync(id, stream, file.FileName);

                    _db.GalleryPhotos.Add(new GalleryPhoto
                    {
                        GalleryId = id,
                        OriginalFileName = file.FileName,
                        ImageUrl = stored.ImageUrl,
                        ThumbnailUrl = stored.ThumbnailUrl,
                        Width = stored.Width,
                        Height = stored.Height
                    });

                    added++;
                }
                catch (Exception ex)
                {
                    rejected.Add($"{file.FileName}: {ex.Message}");
                }
            }

            await _db.SaveChangesAsync();

            // Photos arrive in whatever order the browser hands them over, and in
            // several batches, so ordering has to be settled across the whole
            // gallery after each upload rather than per request.
            await ResequenceAsync(id);

            return new GalleryUploadResult(added, rejected);
        }

        /// <summary>
        /// Puts the gallery back in filename order, which for a match shoot is the
        /// order the game was played in.
        /// </summary>
        public async Task<Outcome<int>> ResortAsync(int id)
        {
            if (!await _db.Galleries.AnyAsync(g => g.Id == id)) return Outcome.NotFound();
            return await ResequenceAsync(id);
        }

        private async Task<int> ResequenceAsync(int galleryId)
        {
            var photos = await _db.GalleryPhotos
                .Where(p => p.GalleryId == galleryId)
                .ToListAsync();

            var ordered = photos
                .OrderBy(SortKey, NaturalFileNameComparer.Instance)
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
                ordered[i].SortOrder = i;

            await _db.SaveChangesAsync();
            return ordered.Count;
        }

        /// <summary>
        /// The original filename where we have it. Photos uploaded before that was
        /// recorded fall back to the stored file, whose name still begins with the
        /// original stem followed by the unique suffix.
        /// </summary>
        private static string SortKey(GalleryPhoto photo)
        {
            if (!string.IsNullOrWhiteSpace(photo.OriginalFileName))
                return photo.OriginalFileName;

            var name = Path.GetFileNameWithoutExtension(photo.ImageUrl) ?? "";
            var dash = name.LastIndexOf('-');

            // The suffix is a 32 character hex guid, so anything else is part of
            // the photographer's own name for the file.
            return dash > 0 && name.Length - dash - 1 == 32 ? name[..dash] : name;
        }

        public async Task<Outcome> DeletePhotoAsync(int id, int photoId)
        {
            var photo = await _db.GalleryPhotos
                .FirstOrDefaultAsync(p => p.Id == photoId && p.GalleryId == id);
            if (photo == null) return Outcome.NotFound();

            _storage.DeletePhoto(photo.ImageUrl, photo.ThumbnailUrl);
            _db.GalleryPhotos.Remove(photo);
            await _db.SaveChangesAsync();
            return Outcome.Ok;
        }

        public async Task<Outcome> DeleteAsync(int id)
        {
            var gallery = await _db.Galleries.FindAsync(id);
            if (gallery == null) return Outcome.NotFound();

            // Files first: a failure here leaves orphaned files rather than rows
            // pointing at images that are already gone.
            _storage.DeleteGallery(id);

            _db.Galleries.Remove(gallery);
            await _db.SaveChangesAsync();
            return Outcome.Ok;
        }
    }
}
