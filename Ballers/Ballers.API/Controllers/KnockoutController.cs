using Ballers.API.Authorization;
using Ballers.API.Services;
using Ballers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [Route("api/knockout")]
    [ApiController]
    public class KnockoutController : ControllerBase
    {
        private readonly IKnockoutService _knockout;
        private readonly IAuthorizationService _authorization;

        public KnockoutController(IKnockoutService knockout, IAuthorizationService authorization)
        {
            _knockout = knockout;
            _authorization = authorization;
        }

        [AllowAnonymous]
        [HttpGet("{seasonId}")]
        public async Task<IActionResult> GetBracket(int seasonId)
            => Ok(await _knockout.GetBracketAsync(seasonId));

        [Authorize]
        [HttpPost("{id}/result")]
        public async Task<IActionResult> SubmitResult(int id, [FromBody] SubmitKnockoutResultRequest request)
        {
            var sides = await _knockout.GetSidesAsync(id);
            if (sides == null) return NotFound();

            if (!(await _authorization.AuthorizeAsync(User, sides, FixtureAccessRequirement.AdminOrEitherTeam)).Succeeded)
                return Forbid();

            try
            {
                await _knockout.SubmitResultAsync(id, request.HomeScore, request.AwayScore);
                return Ok();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
        }
    }
}
