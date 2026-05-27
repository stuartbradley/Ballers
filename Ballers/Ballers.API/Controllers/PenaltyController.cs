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
    public class PenaltyController : ControllerBase
    {
        private readonly IPenaltyService _penalty;
        private readonly IFixtureService _fixtures;
        private readonly UserManager<ApplicationUser> _userManager;

        public PenaltyController(
            IPenaltyService penalty,
            IFixtureService fixtures,
            UserManager<ApplicationUser> userManager)
        {
            _penalty = penalty;
            _fixtures = fixtures;
            _userManager = userManager;
        }

        [AllowAnonymous]
        [HttpGet("table/{seasonId:int}")]
        public async Task<IActionResult> GetTable(int seasonId)
            => Ok(await _penalty.GetTableAsync(seasonId));

        [HttpGet("{fixtureId:int}")]
        public async Task<IActionResult> GetShootout(int fixtureId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var fixture = await _fixtures.GetByIdAsync(fixtureId);
            if (fixture == null) return NotFound();

            if (!user.IsAdmin && user.TeamId != fixture.HomeTeamId && user.TeamId != fixture.AwayTeamId)
                return Forbid();

            var result = await _penalty.GetShootoutAsync(fixtureId);
            if (result == null) return NotFound();

            return Ok(new { homeKicks = result.Value.HomeKicks, awayKicks = result.Value.AwayKicks });
        }

        [HttpPost("{fixtureId:int}/kicks")]
        public async Task<IActionResult> SubmitKicks(
            int fixtureId,
            SubmitPenaltyKicksRequest request,
            [FromQuery] int? teamId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var fixture = await _fixtures.GetByIdAsync(fixtureId);
            if (fixture == null) return NotFound();

            bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            bool isHome = user.TeamId == fixture.HomeTeamId;
            bool isAway = user.TeamId == fixture.AwayTeamId;

            if (!isAdmin && !isHome && !isAway) return Forbid();

            int resolvedTeamId = isAdmin
                ? (teamId ?? fixture.HomeTeamId)
                : (isHome ? fixture.HomeTeamId : fixture.AwayTeamId);

            try
            {
                await _penalty.SubmitKicksAsync(fixtureId, resolvedTeamId, request.Kicks);
                return Ok();
            }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (KeyNotFoundException) { return NotFound(); }
        }
    }
}
