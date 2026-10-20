using Ballers.API.Data;
using Ballers.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Controllers
{
    /// <summary>
    /// Serves photos stored in the database as real images, so pages can link
    /// to them (and browsers cache them) rather than receive them embedded.
    /// </summary>
    [Route("api/media")]
    [ApiController]
    [AllowAnonymous]
    public class MediaController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public MediaController(ApplicationDbContext db) => _db = db;

        /// <summary>
        /// A player's photo. The link's ?v= changes whenever the photo does
        /// (see <see cref="StoredImages.PlayerUrl(int, int?)"/>), so it can be cached
        /// indefinitely.
        /// </summary>
        [HttpGet("players/{id:int}")]
        [ResponseCache(Duration = 31536000, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> PlayerPhoto(int id)
        {
            var stored = await _db.Players.AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => p.ProfileImageBase64)
                .FirstOrDefaultAsync();

            return StoredImages.Decode(stored) is { } photo
                ? File(photo.Bytes, photo.ContentType)
                : NotFound();
        }

        /// <summary>A Match of the Day post's cover, linked from the posts list.</summary>
        [HttpGet("motd/{id:int}/cover")]
        [ResponseCache(Duration = 31536000, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> MotdCover(int id)
        {
            var stored = await _db.MatchOfTheDayPosts.AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => p.CoverImageBase64)
                .FirstOrDefaultAsync();

            return StoredImages.Decode(stored) is { } photo
                ? File(photo.Bytes, photo.ContentType)
                : NotFound();
        }
    }
}
