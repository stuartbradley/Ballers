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
        private readonly INotificationService _notifications;
        private readonly ILeagueSettingsService _leagueSettings;

        public FixturesController(IFixtureService fixtures, UserManager<ApplicationUser> userManager, INotificationService notifications, ILeagueSettingsService leagueSettings)
        {
            _fixtures = fixtures;
            _userManager = userManager;
            _notifications = notifications;
            _leagueSettings = leagueSettings;
        }

        // Global admin "shut down editing" switch — only a true admin bypasses it.
        private async Task<bool> FixturesLockedFor(ApplicationUser user)
            => !user.IsAdmin && await _leagueSettings.AreFixturesLockedAsync();

        [HttpGet("{id}")]
        public async Task<IActionResult> GetFixture(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var fixture = await _fixtures.GetByIdAsync(id);
            if (fixture == null) return NotFound();

            if (!user.IsAdmin && !user.IsReferee && user.TeamId != fixture.HomeTeamId && user.TeamId != fixture.AwayTeamId)
                return Forbid();

            return Ok(fixture);
        }

        [HttpGet]
        public async Task<IActionResult> GetFixtures()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            return Ok(await _fixtures.GetForUserAsync(user.IsAdmin || user.IsReferee, user.TeamId));
        }

        [HttpPost("{fixtureId}/stats")]
        public async Task<IActionResult> SubmitStats(int fixtureId, SubmitFixtureStatsRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var fixture = await _fixtures.GetByIdAsync(fixtureId);
            if (fixture == null) return NotFound();

            bool isAdmin = user.IsAdmin || user.IsReferee;
            if (!isAdmin && user.TeamId != fixture.HomeTeamId && user.TeamId != fixture.AwayTeamId)
                return Forbid();

            if (await FixturesLockedFor(user))
                return Conflict("Fixtures are locked by the admin — editing is disabled.");

            if (fixture.IsEditLocked)
                return Conflict("This fixture is locked — more than 2 weeks have passed since it was played.");

            try
            {
                await _fixtures.SubmitStatsAsync(fixtureId, request.PlayerStats, isAdmin ? null : user.TeamId);

                var msg = $"Stats submitted for {fixture.HomeTeam} vs {fixture.AwayTeam}.";
                var link = $"/fixture/{fixtureId}";
                await _notifications.CreateForTeamAsync(fixture.HomeTeamId, NotificationType.ResultSubmitted, msg, link, fixtureId);
                await _notifications.CreateForTeamAsync(fixture.AwayTeamId, NotificationType.ResultSubmitted, msg, link, fixtureId);

                return Ok();
            }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [AllowAnonymous]
        [HttpGet("table/{seasonId}")]
        public async Task<IActionResult> GetTable(int seasonId)
            => Ok(await _fixtures.GetTableAsync(seasonId));

        [Authorize]
        [HttpPut("{fixtureId}/referee")]
        public async Task<IActionResult> AssignReferee(int fixtureId, [FromBody] AssignRefereeRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var fixture = await _fixtures.GetByIdAsync(fixtureId);
            if (fixture == null) return NotFound();

            // Admins/referees, or a manager of either team in this fixture, may edit.
            if (!user.IsAdmin && !user.IsReferee && user.TeamId != fixture.HomeTeamId && user.TeamId != fixture.AwayTeamId)
                return Forbid();

            if (await FixturesLockedFor(user))
                return Conflict("Fixtures are locked by the admin — editing is disabled.");

            if (fixture.IsEditLocked)
                return Conflict("This fixture is locked — more than 2 weeks have passed since it was played.");

            await _fixtures.AssignRefereeAsync(fixtureId, request.RefereeId);
            return Ok();
        }

        [Authorize]
        [HttpPut("{fixtureId}/schedule")]
        public async Task<IActionResult> UpdateSchedule(int fixtureId, UpdateFixtureScheduleRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var fixture = await _fixtures.GetByIdAsync(fixtureId);
            if (fixture == null) return NotFound();

            // Admins/referees, or a manager of either team in this fixture, may edit.
            if (!user.IsAdmin && !user.IsReferee && user.TeamId != fixture.HomeTeamId && user.TeamId != fixture.AwayTeamId)
                return Forbid();

            if (await FixturesLockedFor(user))
                return Conflict("Fixtures are locked by the admin — editing is disabled.");

            if (fixture.IsEditLocked)
                return Conflict("This fixture is locked — more than 2 weeks have passed since it was played.");

            await _fixtures.UpdateScheduleAsync(fixtureId, request.Location, request.Postcode, request.KickOffTime);

            var kickoffDisplay = request.KickOffTime.ToString("dd MMM HH:mm");
            await _notifications.CreateForTeamAsync(
                fixture.AwayTeamId,
                NotificationType.FixtureUpdated,
                $"Fixture vs {fixture.HomeTeam} has been updated — {kickoffDisplay} at {request.Location ?? "TBD"}.",
                $"/fixture/{fixtureId}");

            return Ok();
        }

        [HttpGet("{fixtureId}/players")]
        public async Task<IActionResult> GetFixturePlayers(int fixtureId, [FromQuery] int? teamId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            bool isAdmin = user.IsAdmin || user.IsReferee;

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

            bool isAdmin = user.IsAdmin || user.IsReferee;
            if (!isAdmin && user.TeamId != fixture.HomeTeamId && user.TeamId != fixture.AwayTeamId)
                return Forbid();

            if (await FixturesLockedFor(user))
                return Conflict("Fixtures are locked by the admin — editing is disabled.");

            if (fixture.IsEditLocked)
                return Conflict("This fixture is locked — more than 2 weeks have passed since it was played.");

            await _fixtures.UpdateSquadAsync(fixtureId, request.PlayerIds, isAdmin ? null : user.TeamId);
            return Ok();
        }

        [AllowAnonymous]
        [HttpGet("current-week")]
        public async Task<IActionResult> GetCurrentWeek()
        {
            if (await PublicFixturesHidden()) return NotFound();
            var week = await _fixtures.GetCurrentWeekAsync();
            return week == null ? NotFound() : Ok(week);
        }

        [AllowAnonymous]
        [HttpGet("weeks")]
        public async Task<IActionResult> GetFixtureWeeks([FromQuery] int? seasonId)
        {
            if (await PublicFixturesHidden()) return Ok(new List<FixtureWeekDto>());
            return Ok(await _fixtures.GetAllWeeksAsync(seasonId));
        }

        // Fixtures are hidden from the public schedule unless the viewer is an
        // admin/referee (so the admin can prepare a season before revealing it).
        private async Task<bool> PublicFixturesHidden()
        {
            if (!await _leagueSettings.AreFixturesHiddenAsync()) return false;
            var user = await _userManager.GetUserAsync(User);
            return !(user?.IsAdmin ?? false) && !(user?.IsReferee ?? false);
        }

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

            bool isAdmin = user.IsAdmin || user.IsReferee;
            if (!isAdmin && user.TeamId != fixture.HomeTeamId && user.TeamId != fixture.AwayTeamId)
                return Forbid();

            if (!isAdmin && user.TeamId == null) return Forbid();

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

            bool isAdmin = user.IsAdmin || user.IsReferee;
            if (!isAdmin && user.TeamId != fixture.HomeTeamId && user.TeamId != fixture.AwayTeamId)
                return Forbid();

            if (!isAdmin && user.TeamId == null) return Forbid();

            if (await FixturesLockedFor(user))
                return Conflict("Fixtures are locked by the admin — editing is disabled.");

            int teamId = isAdmin ? fixture.HomeTeamId : user.TeamId!.Value;
            await _fixtures.SaveCaptaincyAsync(fixtureId, teamId, request.CaptainPlayerId, request.ViceCaptainPlayerId);
            return Ok();
        }

        [AllowAnonymous]
        [HttpGet("next-fixtures")]
        public async Task<IActionResult> GetNextFixtures()
        {
            if (await PublicFixturesHidden()) return Ok(new List<FixtureMatchDto>());
            var week = await _fixtures.GetCurrentWeekAsync();
            return Ok(week?.Matches ?? new List<FixtureMatchDto>());
        }
    }
}
