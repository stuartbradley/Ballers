using Ballers.API.Models;
using Ballers.API.Models.Requests;
using Ballers.API.Services;
using Ballers.Models;
using Ballers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FixturesController : ControllerBase
    {
        private readonly IFixtureService _fixtures;
        private readonly UserManager<ApplicationUser> _userManager;

        public FixturesController(IFixtureService fixtures, UserManager<ApplicationUser> userManager)
        {
            _fixtures = fixtures;
            _userManager = userManager;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetFixture(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var fixture = await _fixtures.GetByIdAsync(id);
            if (fixture == null) return NotFound();

            if (!user.IsAdmin && user.TeamId != fixture.HomeTeamId && user.TeamId != fixture.AwayTeamId)
                return Forbid();

            return Ok(fixture);
        }

        [HttpGet]
        public async Task<IActionResult> GetFixtures()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            return Ok(await _fixtures.GetForUserAsync(user.IsAdmin, user.TeamId));
        }

        [HttpPost("{fixtureId}/stats")]
        public async Task<IActionResult> SubmitStats(int fixtureId, SubmitFixtureStatsRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var fixture = await _fixtures.GetByIdAsync(fixtureId);
            if (fixture == null) return NotFound();

            bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (!isAdmin && user.TeamId != fixture.HomeTeamId && user.TeamId != fixture.AwayTeamId)
                return Forbid();

            if (fixture.IsEditLocked)
                return Conflict("This fixture is locked — more than 2 weeks have passed since it was played.");

            try
            {
                await _fixtures.SubmitStatsAsync(fixtureId, request.PlayerStats, isAdmin ? null : user.TeamId);
                return Ok();
            }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [AllowAnonymous]
        [HttpGet("table/{seasonId}")]
        public async Task<IActionResult> GetTable(int seasonId)
            => Ok(await _fixtures.GetTableAsync(seasonId));

        [Authorize(Roles = "Admin")]
        [HttpPut("{fixtureId}/referee")]
        public async Task<IActionResult> AssignReferee(int fixtureId, [FromBody] AssignRefereeRequest request)
        {
            var fixture = await _fixtures.GetByIdAsync(fixtureId);
            if (fixture == null) return NotFound();

            if (fixture.IsEditLocked)
                return Conflict("This fixture is locked — more than 2 weeks have passed since it was played.");

            await _fixtures.AssignRefereeAsync(fixtureId, request.RefereeId);
            return Ok();
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{fixtureId}/schedule")]
        public async Task<IActionResult> UpdateSchedule(int fixtureId, UpdateFixtureScheduleRequest request)
        {
            var fixture = await _fixtures.GetByIdAsync(fixtureId);
            if (fixture == null) return NotFound();

            if (fixture.IsEditLocked)
                return Conflict("This fixture is locked — more than 2 weeks have passed since it was played.");

            await _fixtures.UpdateScheduleAsync(fixtureId, request.Location, request.Postcode, request.KickOffTime);
            return Ok();
        }

        [HttpGet("{fixtureId}/players")]
        public async Task<IActionResult> GetFixturePlayers(int fixtureId, [FromQuery] int? teamId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            bool isHome = user.TeamId != null;
            bool isAway = user.TeamId != null;

            var fixture = await _fixtures.GetByIdAsync(fixtureId);
            if (fixture == null) return NotFound();

            if (!isAdmin && user.TeamId != fixture.HomeTeamId && user.TeamId != fixture.AwayTeamId)
                return Forbid();

            var players = await _fixtures.GetPlayersAsync(fixtureId, isAdmin, user.TeamId, teamId);
            return players == null ? NotFound() : Ok(players);
        }

        [HttpGet("{fixtureId}/squad")]
        public async Task<IActionResult> GetFixtureSquad(int fixtureId)
            => Ok(await _fixtures.GetSquadAsync(fixtureId));

        [HttpPost("{fixtureId}/squad")]
        public async Task<IActionResult> UpdateSquad(int fixtureId, UpdateFixtureSquadRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var fixture = await _fixtures.GetByIdAsync(fixtureId);
            if (fixture == null) return NotFound();

            bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (!isAdmin && user.TeamId != fixture.HomeTeamId && user.TeamId != fixture.AwayTeamId)
                return Forbid();

            if (fixture.IsEditLocked)
                return Conflict("This fixture is locked — more than 2 weeks have passed since it was played.");

            await _fixtures.UpdateSquadAsync(fixtureId, request.PlayerIds, isAdmin ? null : user.TeamId);
            return Ok();
        }

        [AllowAnonymous]
        [HttpGet("current-week")]
        public async Task<IActionResult> GetCurrentWeek()
        {
            var week = await _fixtures.GetCurrentWeekAsync();
            return week == null ? NotFound() : Ok(week);
        }

        [AllowAnonymous]
        [HttpGet("weeks")]
        public async Task<IActionResult> GetFixtureWeeks()
            => Ok(await _fixtures.GetAllWeeksAsync());

        [HttpGet("{fixtureId}/stats")]
        public async Task<IActionResult> GetFixtureStats(int fixtureId)
            => Ok(await _fixtures.GetStatsAsync(fixtureId));

        [HttpGet("{fixtureId}/opponent-stats")]
        public async Task<IActionResult> GetOpponentStats(int fixtureId, [FromQuery] int? teamId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var fixture = await _fixtures.GetByIdAsync(fixtureId);
            if (fixture == null) return NotFound();

            bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (!isAdmin && user.TeamId != fixture.HomeTeamId && user.TeamId != fixture.AwayTeamId)
                return Forbid();

            int opponentTeamId = (isAdmin && teamId.HasValue)
                ? teamId.Value
                : (user.TeamId == fixture.HomeTeamId ? fixture.AwayTeamId : fixture.HomeTeamId);

            return Ok(await _fixtures.GetOpponentStatsAsync(fixtureId, opponentTeamId));
        }

        [HttpGet("{fixtureId}/head-to-head")]
        public async Task<IActionResult> GetHeadToHead(int fixtureId)
        {
            var fixture = await _fixtures.GetByIdAsync(fixtureId);
            if (fixture == null) return NotFound();
            return Ok(await _fixtures.GetHeadToHeadAsync(fixture.HomeTeamId, fixture.AwayTeamId, fixtureId));
        }

        [HttpPut("{fixtureId}/captaincy")]
        public async Task<IActionResult> SaveCaptaincy(int fixtureId, [FromBody] SaveCaptaincyRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var fixture = await _fixtures.GetByIdAsync(fixtureId);
            if (fixture == null) return NotFound();

            bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (!isAdmin && user.TeamId != fixture.HomeTeamId && user.TeamId != fixture.AwayTeamId)
                return Forbid();

            int teamId = (isAdmin || user.TeamId == null) ? fixture.HomeTeamId : user.TeamId.Value;
            await _fixtures.SaveCaptaincyAsync(fixtureId, teamId, request.CaptainPlayerId, request.ViceCaptainPlayerId);
            return Ok();
        }

        [AllowAnonymous]
        [HttpGet("next-fixtures")]
        public async Task<IActionResult> GetNextFixtures()
        {
            var week = await _fixtures.GetCurrentWeekAsync();
            return Ok(week?.Matches ?? new List<FixtureMatchDto>());
        }
    }
}
