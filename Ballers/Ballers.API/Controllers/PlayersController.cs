using Ballers.API.Models;
using Ballers.API.Models.Requests;
using Ballers.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PlayersController : ControllerBase
    {
        private readonly IPlayerService _players;
        private readonly UserManager<ApplicationUser> _userManager;

        public PlayersController(IPlayerService players, UserManager<ApplicationUser> userManager)
        {
            _players = players;
            _userManager = userManager;
        }

        [HttpGet("my-team")]
        public async Task<IActionResult> GetMyTeamPlayers()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.TeamId == null) return Unauthorized();
            return Ok(await _players.GetTeamPlayerDetailsAsync(user.TeamId.Value));
        }

        [HttpPost]
        public async Task<IActionResult> AddPlayer(CreatePlayerRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.TeamId == null) return Unauthorized();
            await _players.AddPlayerAsync(user.TeamId.Value, request);
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> RemovePlayer(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                var found = await _players.DeactivatePlayerAsync(id, user.TeamId, user.IsAdmin);
                return found ? Ok() : NotFound();
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpPut("{id}/image")]
        public async Task<IActionResult> UploadImage(int id, [FromBody] UploadPlayerImageRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                await _players.UploadPlayerImageAsync(id, user.TeamId, user.IsAdmin, request.ImageBase64);
                return Ok();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }
    }
}
