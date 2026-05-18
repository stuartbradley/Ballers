using Ballers.API.Services;
using Ballers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [ApiController]
    [Route("api/fairplay")]
    [Authorize(Roles = "Admin")]
    public class FairplayController : ControllerBase
    {
        private readonly IFairplayService _fairplay;
        private readonly IFixtureService _fixtures;

        public FairplayController(IFairplayService fairplay, IFixtureService fixtures)
        {
            _fairplay = fairplay;
            _fixtures = fixtures;
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

            if (fixture.IsEditLocked)
                return Conflict("This fixture is locked — more than 2 weeks have passed since it was played.");

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
