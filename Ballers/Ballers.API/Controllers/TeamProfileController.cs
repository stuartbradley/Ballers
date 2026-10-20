using Ballers.API.Models;
using Ballers.API.Services;
using Ballers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [ApiController]
    [Route("api/teams")]
    public class TeamProfileController : ControllerBase
    {
        private readonly ITeamProfileService _profiles;
        private readonly UserManager<ApplicationUser> _userManager;

        public TeamProfileController(ITeamProfileService profiles, UserManager<ApplicationUser> userManager)
        {
            _profiles = profiles;
            _userManager = userManager;
        }

        // Only a team's own manager edits its profile.
        private async Task<IActionResult?> RefuseUnlessManagerOfAsync(int teamId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            return user.TeamId == teamId ? null : Forbid();
        }

        [HttpGet("{id}/profile")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProfile(int id)
            => await _profiles.GetProfileAsync(id) is { } profile ? Ok(profile) : NotFound();

        [HttpPut("{id}/profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile(int id, [FromBody] UpdateTeamProfileRequest request)
        {
            if (await RefuseUnlessManagerOfAsync(id) is { } refusal) return refusal;
            return this.ToActionResult(await _profiles.UpdateProfileAsync(id, request));
        }

        [HttpGet("{id}/squad")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSquad(int id)
            => await _profiles.GetSquadAsync(id) is { } squad ? Ok(squad) : NotFound();

        [HttpPost("{id}/profile/image")]
        [Authorize]
        public async Task<IActionResult> UploadImage(int id, IFormFile? image)
        {
            if (await RefuseUnlessManagerOfAsync(id) is { } refusal) return refusal;

            var upload = image == null
                ? null
                : new UploadedFile(image.FileName, image.ContentType, image.Length, image.OpenReadStream);
            return this.ToActionResult(await _profiles.SetBadgeAsync(id, upload), url => new { url });
        }

        [HttpPut("{id}/manager/image")]
        [Authorize]
        public async Task<IActionResult> UploadManagerImage(int id, [FromBody] UploadManagerImageRequest request)
        {
            if (await RefuseUnlessManagerOfAsync(id) is { } refusal) return refusal;
            return this.ToActionResult(await _profiles.SetManagerImageAsync(id, request.ImageBase64));
        }
    }
}
