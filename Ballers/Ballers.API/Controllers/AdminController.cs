using Ballers.API.Models.Requests;
using Ballers.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _admin;
        private readonly IFixtureService _fixtures;
        private readonly ISeasonService _seasons;
        private readonly IKnockoutService _knockout;

        public AdminController(IAdminService admin, IFixtureService fixtures, ISeasonService seasons, IKnockoutService knockout)
        {
            _admin = admin;
            _fixtures = fixtures;
            _seasons = seasons;
            _knockout = knockout;
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
