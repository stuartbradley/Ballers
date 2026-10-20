using Ballers.API.Authorization;
using Ballers.API.Services;
using Ballers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GalleriesController : ControllerBase
    {
        private readonly IGalleryService _galleries;

        public GalleriesController(IGalleryService galleries) => _galleries = galleries;

        // An unpublished gallery is one still being put together, so it stays
        // hidden from everyone but an admin.
        private bool IncludeHidden => User.IsSiteAdmin();

        // ── Public ────────────────────────────────────────────────────────

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetGalleries()
            => Ok(await _galleries.GetAllAsync(IncludeHidden));

        [AllowAnonymous]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetGallery(int id)
            => await _galleries.GetAsync(id, IncludeHidden) is { } gallery ? Ok(gallery) : NotFound();

        [AllowAnonymous]
        [HttpGet("{id:int}/photos")]
        public async Task<IActionResult> GetPhotos(int id, [FromQuery] int skip = 0, [FromQuery] int take = GalleryService.DefaultPageSize)
            => await _galleries.GetPhotosAsync(id, IncludeHidden, skip, take) is { } page ? Ok(page) : NotFound();

        /// <summary>
        /// Lets the fixture page show a "View gallery" link only when there is
        /// something to view, without pulling the gallery itself.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("by-fixture/{fixtureId:int}")]
        public async Task<IActionResult> GetByFixture(int fixtureId)
            => await _galleries.GetByFixtureAsync(fixtureId, IncludeHidden) is { } gallery ? Ok(gallery) : NotFound();

        // ── Admin ─────────────────────────────────────────────────────────

        [Authorize(Policy = Policies.SiteAdmin)]
        [HttpPost]
        public async Task<IActionResult> CreateGallery(SaveGalleryRequest request)
            => this.ToActionResult(await _galleries.CreateAsync(request), id => new { Id = id });

        [Authorize(Policy = Policies.SiteAdmin)]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateGallery(int id, SaveGalleryRequest request)
            => this.ToActionResult(await _galleries.UpdateAsync(id, request));

        [Authorize(Policy = Policies.SiteAdmin)]
        [HttpPost("{id:int}/photos")]
        [RequestSizeLimit(200 * 1024 * 1024)]
        public async Task<IActionResult> UploadPhotos(int id, [FromForm] List<IFormFile> files)
        {
            var uploads = files
                .Select(f => new UploadedFile(f.FileName, f.ContentType, f.Length, f.OpenReadStream))
                .ToList();
            return this.ToActionResult(await _galleries.UploadAsync(id, uploads));
        }

        /// <summary>
        /// Puts the gallery back in filename order, which for a match shoot is the
        /// order the game was played in.
        /// </summary>
        [Authorize(Policy = Policies.SiteAdmin)]
        [HttpPost("{id:int}/resort")]
        public async Task<IActionResult> Resort(int id)
            => this.ToActionResult(await _galleries.ResortAsync(id), count => new { reordered = count });

        [Authorize(Policy = Policies.SiteAdmin)]
        [HttpDelete("{id:int}/photos/{photoId:int}")]
        public async Task<IActionResult> DeletePhoto(int id, int photoId)
            => this.ToActionResult(await _galleries.DeletePhotoAsync(id, photoId));

        [Authorize(Policy = Policies.SiteAdmin)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteGallery(int id)
            => this.ToActionResult(await _galleries.DeleteAsync(id));
    }
}
