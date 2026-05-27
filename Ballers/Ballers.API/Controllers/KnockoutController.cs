using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Services;
using Ballers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Controllers
{
    [Route("api/knockout")]
    [ApiController]
    public class KnockoutController : ControllerBase
    {
        private readonly IKnockoutService _knockout;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;

        public KnockoutController(IKnockoutService knockout, UserManager<ApplicationUser> userManager, ApplicationDbContext db)
        {
            _knockout = knockout;
            _userManager = userManager;
            _db = db;
        }

        [AllowAnonymous]
        [HttpGet("{seasonId}")]
        public async Task<IActionResult> GetBracket(int seasonId)
            => Ok(await _knockout.GetBracketAsync(seasonId));

        [Authorize]
        [HttpPost("{id}/result")]
        public async Task<IActionResult> SubmitResult(int id, [FromBody] SubmitKnockoutResultRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (!user.IsAdmin)
            {
                var fixture = await _db.KnockoutFixtures.FindAsync(id);
                if (fixture == null) return NotFound();
                if (user.TeamId == null || (fixture.HomeTeamId != user.TeamId && fixture.AwayTeamId != user.TeamId))
                    return Forbid();
            }

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
