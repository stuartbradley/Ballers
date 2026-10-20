using Ballers.API.Authorization;
using Ballers.API.Models;
using Ballers.Models;
using Microsoft.AspNetCore.Identity;
using Ballers.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = Policies.SiteAdmin)]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _admin;
        private readonly IFixtureService _fixtures;
        private readonly ISeasonService _seasons;
        private readonly IKnockoutService _knockout;
        private readonly IPlayerPointsService _playerPoints;
        private readonly ILeagueSettingsService _leagueSettings;
        private readonly IAdminOverviewService _overview;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(
            IAdminService admin,
            IFixtureService fixtures,
            ISeasonService seasons,
            IKnockoutService knockout,
            IPlayerPointsService playerPoints,
            ILeagueSettingsService leagueSettings,
            IAdminOverviewService overview,
            UserManager<ApplicationUser> userManager)
        {
            _admin = admin;
            _fixtures = fixtures;
            _seasons = seasons;
            _knockout = knockout;
            _playerPoints = playerPoints;
            _leagueSettings = leagueSettings;
            _overview = overview;
            _userManager = userManager;
        }

        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview()
            => Ok(await _overview.GetAsync(DateTime.UtcNow));

        [HttpGet("accounts")]
        public async Task<IActionResult> GetAccounts()
            => Ok(await _admin.GetAccountsAsync(_userManager.GetUserId(User) ?? ""));

        [HttpPost("accounts/{id}/reset-password")]
        public async Task<IActionResult> ResetPassword(string id, Ballers.Shared.AdminResetPasswordRequest request)
        {
            try
            {
                await _admin.ResetPasswordAsync(id, request.NewPassword);
                return Ok();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        [HttpDelete("accounts/{id}")]
        public async Task<IActionResult> DeleteAccount(string id)
        {
            try
            {
                await _admin.DeleteAccountAsync(id, _userManager.GetUserId(User) ?? "");
                return Ok();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("create-team")]
        public async Task<IActionResult> CreateTeam(CreateTeamRequest request)
        {
            try
            {
                await _admin.CreateTeamAsync(request.TeamName, request.Email, request.Password);
                return Ok();
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("create-referee")]
        public async Task<IActionResult> CreateReferee(CreateRefereeRequest request)
        {
            try
            {
                await _admin.CreateRefereeAsync(request.Email, request.Password);
                return Ok();
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("create-admin")]
        public async Task<IActionResult> CreateAdmin(CreateAdminRequest request)
        {
            try
            {
                await _admin.CreateAdminAsync(request.Email, request.Password);
                return Ok();
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        /// <summary>
        /// Credits any played fixture that has not been paid out yet. The weekly
        /// job calls the same code; this is here so a run can be forced by hand,
        /// or driven by a scheduled task when the app pool cannot be kept alive.
        /// Safe to call repeatedly — an already-paid fixture is skipped.
        /// </summary>
        [HttpPost("run-player-points")]
        public async Task<IActionResult> RunPlayerPoints()
        {
            var result = await _playerPoints.RunCatchUpAsync();
            return Ok(result);
        }

        [HttpGet("points-settings")]
        public async Task<IActionResult> GetPointsSettings()
            => Ok(await _leagueSettings.GetPointsSettingsAsync());

        [HttpPut("points-settings")]
        public async Task<IActionResult> UpdatePointsSettings(Ballers.Shared.PointsSettingsDto request)
        {
            try
            {
                await _leagueSettings.UpdatePointsSettingsAsync(request);
                return Ok();
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("generate-fixtures")]
        public async Task<IActionResult> GenerateFixtures(GenerateFixturesRequest request)
        {
            try
            {
                await _fixtures.GenerateFixturesAsync(request.TeamIds, request.StartDate);
                return Ok();
            }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("import-fixtures")]
        public async Task<IActionResult> ImportFixtures(
            IFormFile file, [FromForm] int seasonNumber, [FromForm] DateTime startDate, [FromForm] bool makeActive = false)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No CSV file provided.");
            if (seasonNumber < 1)
                return BadRequest("A valid season number is required.");

            using var stream = file.OpenReadStream();
            var result = await _fixtures.ImportFixturesAsync(stream, seasonNumber, startDate, makeActive);

            if (result.Errors.Count > 0)
                return BadRequest(new { errors = result.Errors });

            return Ok(new { created = result.Created });
        }

        [HttpPost("seasons/{id}/generate-knockout")]
        public async Task<IActionResult> GenerateKnockout(int id)
        {
            try
            {
                await _knockout.GenerateAsync(id);
                return Ok();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
        }

        [HttpDelete("seasons/{id}")]
        public async Task<IActionResult> DeleteSeason(int id)
        {
            try
            {
                await _admin.DeleteSeasonAsync(id);
                return Ok();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        }

        [HttpPost("seasons/{id}/activate")]
        public async Task<IActionResult> ActivateSeason(int id)
        {
            try
            {
                await _seasons.ActivateSeasonAsync(id);
                return Ok();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        }
    }
}
