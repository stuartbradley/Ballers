using Ballers.API.Authorization;
using Ballers.API.Models;
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
        internal const string FixturesLockedMessage = "Fixtures are locked by the admin — editing is disabled.";

        private readonly IFixtureService _fixtures;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuthorizationService _authorization;
        private readonly ILeagueSettingsService _leagueSettings;

        public FixturesController(IFixtureService fixtures, UserManager<ApplicationUser> userManager, IAuthorizationService authorization, ILeagueSettingsService leagueSettings)
        {
            _fixtures = fixtures;
            _userManager = userManager;
            _authorization = authorization;
            _leagueSettings = leagueSettings;
        }

        // The caller and the fixture they're acting on, once they've been let in.
        private sealed record FixtureCall(ApplicationUser User, FixtureDetailsDto Fixture, bool IsOfficial);

        /// <summary>
        /// Loads the caller and the fixture and checks the caller may act on it
        /// (see FixtureAccessRequirement). An edit is also refused while the
        /// admin's global lock is on, which only a true admin bypasses. Returns
        /// the response to send when refused.
        /// </summary>
        private async Task<(FixtureCall? Call, IActionResult? Refusal)> AuthorizeAsync(
            int fixtureId, FixtureAccessRequirement allowed, bool edit = false)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return (null, Unauthorized());

            var fixture = await _fixtures.GetByIdAsync(fixtureId);
            if (fixture == null) return (null, NotFound());

            var sides = new FixtureSides(fixture.HomeTeamId, fixture.AwayTeamId);
            if (!(await _authorization.AuthorizeAsync(User, sides, allowed)).Succeeded)
                return (null, Forbid());

            if (edit && !User.IsSiteAdmin() && await _leagueSettings.AreFixturesLockedAsync())
                return (null, Conflict(FixturesLockedMessage));

            return (new FixtureCall(user, fixture, User.IsOfficial()), null);
        }

        /// <summary>
        /// Which team a write applies to. Admins and referees have no team of
        /// their own, so they name the side explicitly and default to the home
        /// team; a manager always acts for their own team, whatever they send.
        /// Returns null when an admin names a team that is not in this fixture.
        /// </summary>
        private static int? ResolveActingTeamId(FixtureCall call, int? requestedTeamId)
        {
            if (!call.IsOfficial) return call.User.TeamId;

            if (requestedTeamId is null) return call.Fixture.HomeTeamId;

            return requestedTeamId == call.Fixture.HomeTeamId || requestedTeamId == call.Fixture.AwayTeamId
                ? requestedTeamId
                : null;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetFixture(int id)
        {
            var (call, refusal) = await AuthorizeAsync(id, FixtureAccessRequirement.OfficialsOrEitherTeam);
            return refusal ?? Ok(call!.Fixture);
        }

        [HttpGet]
        public async Task<IActionResult> GetFixtures([FromQuery] int? seasonId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            return Ok(await _fixtures.GetForUserAsync(User.IsOfficial(), user.TeamId, seasonId));
        }

        [HttpPost("{fixtureId}/stats")]
        public async Task<IActionResult> SubmitStats(int fixtureId, SubmitFixtureStatsRequest request)
        {
            var (call, refusal) = await AuthorizeAsync(fixtureId, FixtureAccessRequirement.OfficialsOrEitherTeam, edit: true);
            if (refusal != null) return refusal;

            var actingTeamId = ResolveActingTeamId(call!, request.TeamId);
            if (actingTeamId is null) return BadRequest("That team is not in this fixture.");

            // Both teams are told through FixtureStatsSubmitted.
            try
            {
                await _fixtures.SubmitStatsAsync(fixtureId, request.PlayerStats, actingTeamId);
                return Ok();
            }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [AllowAnonymous]
        [HttpGet("table/{seasonId}")]
        public async Task<IActionResult> GetTable(int seasonId)
            => Ok(await _fixtures.GetTableAsync(seasonId));

        [HttpPut("{fixtureId}/referee")]
        public async Task<IActionResult> AssignReferee(int fixtureId, [FromBody] AssignRefereeRequest request)
        {
            var (_, refusal) = await AuthorizeAsync(fixtureId, FixtureAccessRequirement.OfficialsOrHomeTeam, edit: true);
            if (refusal != null) return refusal;

            await _fixtures.AssignRefereeAsync(fixtureId, request.RefereeId);
            return Ok();
        }

        [HttpPut("{fixtureId}/schedule")]
        public async Task<IActionResult> UpdateSchedule(int fixtureId, UpdateFixtureScheduleRequest request)
        {
            var (_, refusal) = await AuthorizeAsync(fixtureId, FixtureAccessRequirement.OfficialsOrHomeTeam, edit: true);
            if (refusal != null) return refusal;

            // The away side is told through FixtureRescheduled.
            await _fixtures.UpdateScheduleAsync(fixtureId, request.Location, request.Postcode, request.KickOffTime);
            return Ok();
        }

        /// <summary>
        /// Hands hosting to the other side: the away team becomes home and takes
        /// over the setup. For the home manager (say their pitch is unavailable)
        /// or an admin, and only before the game, with no stats in yet.
        /// </summary>
        [HttpPost("{fixtureId}/swap-home-away")]
        public async Task<IActionResult> SwapHomeAway(int fixtureId)
        {
            var (_, refusal) = await AuthorizeAsync(fixtureId, FixtureAccessRequirement.AdminOrHomeTeam, edit: true);
            if (refusal != null) return refusal;

            // Both sides are told through FixtureSidesSwapped.
            var problem = await _fixtures.SwapHomeAwayAsync(fixtureId, DateTime.UtcNow, byAdmin: User.IsSiteAdmin());
            return problem == null ? Ok() : Conflict(problem);
        }

        [HttpGet("{fixtureId}/players")]
        public async Task<IActionResult> GetFixturePlayers(int fixtureId, [FromQuery] int? teamId = null)
        {
            var (call, refusal) = await AuthorizeAsync(fixtureId, FixtureAccessRequirement.OfficialsOrEitherTeam);
            if (refusal != null) return refusal;

            var players = await _fixtures.GetPlayersAsync(fixtureId, call!.IsOfficial, call.User.TeamId, teamId);
            return players == null ? NotFound() : Ok(players);
        }

        [HttpGet("{fixtureId}/squad")]
        public async Task<IActionResult> GetFixtureSquad(int fixtureId)
            => Ok(await _fixtures.GetSquadAsync(fixtureId));

        [HttpPost("{fixtureId}/squad")]
        public async Task<IActionResult> UpdateSquad(int fixtureId, UpdateFixtureSquadRequest request)
        {
            var (call, refusal) = await AuthorizeAsync(fixtureId, FixtureAccessRequirement.OfficialsOrEitherTeam, edit: true);
            if (refusal != null) return refusal;

            // Never null for an admin: UpdateSquadAsync treats a null team as
            // "replace every player in this fixture", which would wipe the other
            // team's squad when an admin saved one side.
            var actingTeamId = ResolveActingTeamId(call!, request.TeamId);
            if (actingTeamId is null) return BadRequest("That team is not in this fixture.");

            await _fixtures.UpdateSquadAsync(fixtureId, request.PlayerIds, actingTeamId);
            return Ok();
        }

        // ── True man of the match ──────────────────────────────────────────
        // The referee names a man of the match per team in the stats, then picks
        // one of the two as the match's real man of the match. That pick is for
        // the site admin only: referees and managers are deliberately excluded,
        // and it is never returned on the ordinary fixture endpoints, so it
        // cannot leak to players through the fixture page.

        [Authorize(Policy = Policies.SiteAdmin)]
        [HttpGet("{fixtureId}/true-motm")]
        public async Task<IActionResult> GetTrueMotm(int fixtureId)
        {
            var detail = await _fixtures.GetTrueMotmAsync(fixtureId);
            return detail == null ? NotFound() : Ok(detail);
        }

        [Authorize(Policy = Policies.SiteAdmin)]
        [HttpPut("{fixtureId}/true-motm")]
        public async Task<IActionResult> SetTrueMotm(int fixtureId, SetTrueMotmRequest request)
        {
            var saved = await _fixtures.SetTrueMotmAsync(fixtureId, request.PlayerId);
            return saved
                ? Ok()
                : BadRequest("That player is not a man of the match for this fixture.");
        }

        [Authorize(Policy = Policies.SiteAdmin)]
        [HttpGet("true-motm")]
        public async Task<IActionResult> GetTrueMotmLog()
        {
            return Ok(await _fixtures.GetTrueMotmLogAsync());
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
            return !User.IsOfficial();
        }

        [HttpGet("{fixtureId}/stats")]
        public async Task<IActionResult> GetFixtureStats(int fixtureId)
            => Ok(await _fixtures.GetStatsAsync(fixtureId));

        [HttpGet("{fixtureId}/opponent-stats")]
        public async Task<IActionResult> GetOpponentStats(int fixtureId, [FromQuery] int? teamId = null)
        {
            var (call, refusal) = await AuthorizeAsync(fixtureId, FixtureAccessRequirement.OfficialsOrEitherTeam);
            if (refusal != null) return refusal;
            var fixture = call!.Fixture;

            int opponentTeamId = (call.IsOfficial && teamId.HasValue)
                ? teamId.Value
                : (call.User.TeamId == fixture.HomeTeamId ? fixture.AwayTeamId : fixture.HomeTeamId);

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
            var (call, refusal) = await AuthorizeAsync(fixtureId, FixtureAccessRequirement.OfficialsOrEitherTeam, edit: true);
            if (refusal != null) return refusal;

            var actingTeamId = ResolveActingTeamId(call!, request.TeamId);
            if (actingTeamId is null) return BadRequest("That team is not in this fixture.");

            await _fixtures.SaveCaptaincyAsync(
                fixtureId, actingTeamId.Value, request.CaptainPlayerId, request.ViceCaptainPlayerId);
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
