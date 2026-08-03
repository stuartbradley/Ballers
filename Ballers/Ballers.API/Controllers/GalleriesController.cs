using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Services;
using Ballers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GalleriesController : ControllerBase
    {
        // Galleries run to hundreds of photos, so the page loads them in chunks
        // rather than dropping the whole set into one response.
        private const int DefaultPageSize = 40;
        private const int MaxPageSize = 100;

        private const long MaxUploadBytes = 15 * 1024 * 1024;
        private static readonly string[] AllowedContentTypes =
            ["image/jpeg", "image/png", "image/webp"];

        private readonly ApplicationDbContext _db;
        private readonly IGalleryStorageService _storage;
        private readonly UserManager<ApplicationUser> _userManager;

        public GalleriesController(
            ApplicationDbContext db,
            IGalleryStorageService storage,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _storage = storage;
            _userManager = userManager;
        }

        private async Task<bool> IsAdminAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.IsAdmin ?? false;
        }

        // ── Public ────────────────────────────────────────────────────────

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetGalleries()
        {
            // An unpublished gallery is one still being put together, so it stays
            // out of the list for everyone but an admin.
            var includeHidden = await IsAdminAsync();

            var galleries = await _db.Galleries
                .AsNoTracking()
                .Where(g => includeHidden || g.IsPublished)
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

            return Ok(galleries);
        }

        [AllowAnonymous]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetGallery(int id)
        {
            var includeHidden = await IsAdminAsync();

            var gallery = await _db.Galleries
                .AsNoTracking()
                .Where(g => g.Id == id && (includeHidden || g.IsPublished))
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

            return gallery == null ? NotFound() : Ok(gallery);
        }

        [AllowAnonymous]
        [HttpGet("{id:int}/photos")]
        public async Task<IActionResult> GetPhotos(int id, [FromQuery] int skip = 0, [FromQuery] int take = DefaultPageSize)
        {
            var includeHidden = await IsAdminAsync();

            var exists = await _db.Galleries
                .AnyAsync(g => g.Id == id && (includeHidden || g.IsPublished));
            if (!exists) return NotFound();

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

            return Ok(new GalleryPhotoPageDto
            {
                Photos = photos,
                Total = total,
                HasMore = skip + photos.Count < total
            });
        }

        /// <summary>
        /// Lets the fixture page show a "View gallery" link only when there is
        /// something to view, without pulling the gallery itself.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("by-fixture/{fixtureId:int}")]
        public async Task<IActionResult> GetByFixture(int fixtureId)
        {
            var includeHidden = await IsAdminAsync();

            // The link appears whenever the fixture has a gallery. An empty one
            // still gets a link rather than silently vanishing, which is far
            // easier to make sense of than a button that never appears.
            var gallery = await _db.Galleries
                .AsNoTracking()
                .Where(g => g.FixtureId == fixtureId && (includeHidden || g.IsPublished))
                .Select(g => new FixtureGalleryRefDto
                {
                    GalleryId = g.Id,
                    PhotoCount = g.Photos.Count
                })
                .FirstOrDefaultAsync();

            return gallery == null ? NotFound() : Ok(gallery);
        }

        // ── Admin ─────────────────────────────────────────────────────────

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateGallery(SaveGalleryRequest request)
        {
            if (!await IsAdminAsync()) return Forbid();

            var fixtureExists = await _db.Fixtures.AnyAsync(f => f.Id == request.FixtureId);
            if (!fixtureExists) return BadRequest("That fixture does not exist.");

            if (await _db.Galleries.AnyAsync(g => g.FixtureId == request.FixtureId))
                return Conflict("That fixture already has a gallery.");

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

            return Ok(new { gallery.Id });
        }

        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateGallery(int id, SaveGalleryRequest request)
        {
            if (!await IsAdminAsync()) return Forbid();

            var gallery = await _db.Galleries.FindAsync(id);
            if (gallery == null) return NotFound();

            gallery.PhotographerId = request.PhotographerId;
            gallery.Title = request.Title;
            gallery.Description = request.Description;
            gallery.IsPublished = request.IsPublished;

            await _db.SaveChangesAsync();
            return Ok();
        }

        [Authorize]
        [HttpPost("{id:int}/photos")]
        [RequestSizeLimit(200 * 1024 * 1024)]
        public async Task<IActionResult> UploadPhotos(int id, [FromForm] List<IFormFile> files)
        {
            if (!await IsAdminAsync()) return Forbid();

            var gallery = await _db.Galleries.FindAsync(id);
            if (gallery == null) return NotFound();

            if (files.Count == 0) return BadRequest("No files provided.");

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
                    await using var stream = file.OpenReadStream();
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

            return Ok(new { added, rejected });
        }

        /// <summary>
        /// Puts the gallery back in filename order, which for a match shoot is the
        /// order the game was played in.
        /// </summary>
        [Authorize]
        [HttpPost("{id:int}/resort")]
        public async Task<IActionResult> Resort(int id)
        {
            if (!await IsAdminAsync()) return Forbid();

            if (!await _db.Galleries.AnyAsync(g => g.Id == id)) return NotFound();

            var count = await ResequenceAsync(id);
            return Ok(new { reordered = count });
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

        [Authorize]
        [HttpDelete("{id:int}/photos/{photoId:int}")]
        public async Task<IActionResult> DeletePhoto(int id, int photoId)
        {
            if (!await IsAdminAsync()) return Forbid();

            var photo = await _db.GalleryPhotos
                .FirstOrDefaultAsync(p => p.Id == photoId && p.GalleryId == id);
            if (photo == null) return NotFound();

            _storage.DeletePhoto(photo.ImageUrl, photo.ThumbnailUrl);
            _db.GalleryPhotos.Remove(photo);
            await _db.SaveChangesAsync();

            return Ok();
        }

        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteGallery(int id)
        {
            if (!await IsAdminAsync()) return Forbid();

            var gallery = await _db.Galleries.FindAsync(id);
            if (gallery == null) return NotFound();

            // Files first: a failure here leaves orphaned files rather than rows
            // pointing at images that are already gone.
            _storage.DeleteGallery(id);

            _db.Galleries.Remove(gallery);
            await _db.SaveChangesAsync();

            return Ok();
        }
    }
}
