using Ballers.API.Authorization;
using Ballers.API.Models;
using Ballers.Models;
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
        private readonly IAuthorizationService _authorization;

        public PenaltyController(
            IPenaltyService penalty,
            IFixtureService fixtures,
            UserManager<ApplicationUser> userManager,
            IAuthorizationService authorization)
        {
            _penalty = penalty;
            _fixtures = fixtures;
            _userManager = userManager;
            _authorization = authorization;
        }

        // Shootouts are for the site admin and the two teams; referees are left out.
        private async Task<bool> MayActOnAsync(FixtureDetailsDto fixture)
            => (await _authorization.AuthorizeAsync(
                User, new FixtureSides(fixture.HomeTeamId, fixture.AwayTeamId),
                FixtureAccessRequirement.AdminOrEitherTeam)).Succeeded;

        [AllowAnonymous]
        [HttpGet("table/{seasonId:int}")]
        public async Task<IActionResult> GetTable(int seasonId)
            => Ok(await _penalty.GetTableAsync(seasonId));

        [HttpGet("{fixtureId:int}")]
        public async Task<IActionResult> GetShootout(int fixtureId)
        {
            var fixture = await _fixtures.GetByIdAsync(fixtureId);
            if (fixture == null) return NotFound();
            if (!await MayActOnAsync(fixture)) return Forbid();

            var result = await _penalty.GetShootoutAsync(fixtureId);
            if (result == null) return NotFound();

            return Ok(new PenaltyShootoutDto { HomeKicks = result.Value.HomeKicks, AwayKicks = result.Value.AwayKicks });
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
            if (!await MayActOnAsync(fixture)) return Forbid();

            // An admin names the side; a manager always kicks for their own.
            int resolvedTeamId = User.IsSiteAdmin()
                ? (teamId ?? fixture.HomeTeamId)
                : (user.TeamId == fixture.HomeTeamId ? fixture.HomeTeamId : fixture.AwayTeamId);

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
