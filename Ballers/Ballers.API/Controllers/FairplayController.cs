using Ballers.API.Authorization;
using Ballers.API.Services;
using Ballers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [ApiController]
    [Route("api/fairplay")]
    [Authorize(Policy = Policies.Official)]
    public class FairplayController : ControllerBase
    {
        private readonly IFairplayService _fairplay;
        private readonly IFixtureService _fixtures;
        private readonly ILeagueSettingsService _leagueSettings;

        public FairplayController(IFairplayService fairplay, IFixtureService fixtures, ILeagueSettingsService leagueSettings)
        {
            _fairplay = fairplay;
            _fixtures = fixtures;
            _leagueSettings = leagueSettings;
        }

        [HttpGet("{fixtureId:int}")]
        public async Task<IActionResult> GetRatings(int fixtureId)
        {
            var result = await _fairplay.GetRatingsAsync(fixtureId);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost("{fixtureId:int}")]
        public async Task<IActionResult> SubmitRatings(int fixtureId, [FromBody] SubmitFairplayRequest request)
        {
            var fixture = await _fixtures.GetByIdAsync(fixtureId);
            if (fixture == null) return NotFound();

            if (!User.IsSiteAdmin() && await _leagueSettings.AreFixturesLockedAsync())
                return Conflict(FixturesController.FixturesLockedMessage);

            try
            {
                await _fairplay.SubmitRatingsAsync(fixtureId, request.HomeRating, request.AwayRating);
                return Ok();
            }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [HttpGet("table/{seasonId:int}")]
        public async Task<IActionResult> GetTable(int seasonId)
            => Ok(await _fairplay.GetTableAsync(seasonId));
    }
}
