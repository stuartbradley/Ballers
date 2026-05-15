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

        public FairplayController(IFairplayService fairplay) => _fairplay = fairplay;

        [HttpGet("{fixtureId:int}")]
        public async Task<IActionResult> GetRatings(int fixtureId)
        {
            var result = await _fairplay.GetRatingsAsync(fixtureId);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost("{fixtureId:int}")]
        public async Task<IActionResult> SubmitRatings(int fixtureId, [FromBody] SubmitFairplayRequest request)
        {
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
