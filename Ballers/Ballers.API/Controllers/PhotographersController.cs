using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PhotographersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public PhotographersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        private async Task<bool> IsAdminAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.IsAdmin ?? false;
        }

        // Public: the gallery page credits the photographer, so anyone viewing a
        // gallery needs to be able to read these.
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var photographers = await _db.Photographers
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .Select(p => new PhotographerDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Website = p.Website,
                    Instagram = p.Instagram,
                    Facebook = p.Facebook,
                    Email = p.Email,
                    Bio = p.Bio,
                    LogoUrl = p.LogoUrl
                })
                .ToListAsync();

            return Ok(photographers);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create(PhotographerDto request)
        {
            if (!await IsAdminAsync()) return Forbid();

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("A name is required.");

            var photographer = new Photographer
            {
                Name = request.Name.Trim(),
                Website = Clean(request.Website),
                Instagram = Clean(request.Instagram),
                Facebook = Clean(request.Facebook),
                Email = Clean(request.Email),
                Bio = Clean(request.Bio),
                LogoUrl = Clean(request.LogoUrl)
            };

            _db.Photographers.Add(photographer);
            await _db.SaveChangesAsync();

            return Ok(new { photographer.Id });
        }

        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, PhotographerDto request)
        {
            if (!await IsAdminAsync()) return Forbid();

            var photographer = await _db.Photographers.FindAsync(id);
            if (photographer == null) return NotFound();

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("A name is required.");

            photographer.Name = request.Name.Trim();
            photographer.Website = Clean(request.Website);
            photographer.Instagram = Clean(request.Instagram);
            photographer.Facebook = Clean(request.Facebook);
            photographer.Email = Clean(request.Email);
            photographer.Bio = Clean(request.Bio);
            photographer.LogoUrl = Clean(request.LogoUrl);

            await _db.SaveChangesAsync();
            return Ok();
        }

        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await IsAdminAsync()) return Forbid();

            var photographer = await _db.Photographers.FindAsync(id);
            if (photographer == null) return NotFound();

            // Galleries keep their photos and simply lose the credit — see the
            // SetNull relationship in ApplicationDbContext.
            _db.Photographers.Remove(photographer);
            await _db.SaveChangesAsync();

            return Ok();
        }

        private static string? Clean(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
