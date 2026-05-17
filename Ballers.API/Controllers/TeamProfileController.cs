using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Services;
using Ballers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Controllers
{
    [ApiController]
    [Route("api/teams")]
    public class TeamProfileController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ImageModerationService _moderation;
        private readonly IWebHostEnvironment _env;

        public TeamProfileController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ImageModerationService moderation,
            IWebHostEnvironment env)
        {
            _db = db;
            _userManager = userManager;
            _moderation = moderation;
            _env = env;
        }

        [HttpGet("{id}/profile")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProfile(int id)
        {
            var team = await _db.Teams.FindAsync(id);
            if (team == null) return NotFound();

            return Ok(new TeamProfileDto
            {
                Id = team.Id,
                Name = team.Name,
                PhoneNumber = team.PhoneNumber,
                ManagerName = team.ManagerName,
                YearFormed = team.YearFormed,
                Bio = team.Bio,
                ProfileImageUrl = team.ProfileImageUrl
            });
        }

        [HttpPut("{id}/profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile(int id, [FromBody] UpdateTeamProfileRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            if (user.TeamId != id) return Forbid();

            var team = await _db.Teams.FindAsync(id);
            if (team == null) return NotFound();

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Team name is required.");

            team.Name = request.Name.Trim();
            team.PhoneNumber = request.PhoneNumber?.Trim();
            team.ManagerName = request.ManagerName?.Trim();
            team.YearFormed = request.YearFormed;
            team.Bio = request.Bio?.Trim();

            await _db.SaveChangesAsync();

            return Ok(new TeamProfileDto
            {
                Id = team.Id,
                Name = team.Name,
                PhoneNumber = team.PhoneNumber,
                ManagerName = team.ManagerName,
                YearFormed = team.YearFormed,
                Bio = team.Bio,
                ProfileImageUrl = team.ProfileImageUrl
            });
        }

        [HttpPost("{id}/profile/image")]
        [Authorize]
        public async Task<IActionResult> UploadImage(int id, IFormFile image)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            if (user.TeamId != id) return Forbid();

            var team = await _db.Teams.FindAsync(id);
            if (team == null) return NotFound();

            if (image == null || image.Length == 0)
                return BadRequest("No image provided.");

            if (image.Length > 5 * 1024 * 1024)
                return BadRequest("Image must be under 5 MB.");

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(image.ContentType.ToLower()))
                return BadRequest("Only JPEG, PNG, and WebP images are accepted.");

            byte[] imageBytes;
            using (var ms = new MemoryStream())
            {
                await image.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }

            if (await _moderation.IsExplicitAsync(imageBytes, image.ContentType))
                return BadRequest("Image was rejected: explicit or inappropriate content detected.");

            var ext = image.ContentType switch
            {
                "image/png"  => ".png",
                "image/webp" => ".webp",
                _            => ".jpg"
            };

            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var uploadsPath = Path.Combine(webRoot, "uploads", "teams");
            Directory.CreateDirectory(uploadsPath);

            var fileName = $"{id}{ext}";
            var filePath = Path.Combine(uploadsPath, fileName);

            await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

            team.ProfileImageUrl = $"/uploads/teams/{fileName}";
            await _db.SaveChangesAsync();

            return Ok(new { url = team.ProfileImageUrl });
        }
    }
}
