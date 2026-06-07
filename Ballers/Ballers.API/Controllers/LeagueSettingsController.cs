using Ballers.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [Route("api/league-settings")]
    [ApiController]
    [Authorize]
    public class LeagueSettingsController : ControllerBase
    {
        private readonly ILeagueSettingsService _settings;

        public LeagueSettingsController(ILeagueSettingsService settings)
        {
            _settings = settings;
        }

        // Any authenticated user reads the flags (managers need PlayersLocked to gate their UI).
        [HttpGet]
        public async Task<IActionResult> Get() => Ok(await _settings.GetAsync());

        [Authorize(Roles = "Admin")]
        [HttpPut("players-locked")]
        public async Task<IActionResult> SetPlayersLocked([FromBody] bool locked)
        {
            await _settings.SetPlayersLockedAsync(locked);
            return Ok();
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("fixtures-locked")]
        public async Task<IActionResult> SetFixturesLocked([FromBody] bool locked)
        {
            await _settings.SetFixturesLockedAsync(locked);
            return Ok();
        }
    }
}
