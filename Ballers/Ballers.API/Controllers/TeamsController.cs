using Ballers.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [ApiController]
    [Route("api/teams")]
    public class TeamsController : ControllerBase
    {
        private readonly ITeamService _teams;

        public TeamsController(ITeamService teams) => _teams = teams;

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetTeams()
            => Ok(await _teams.GetTeamsAsync());

        /// <summary>Every club with its record, form and squad rating.</summary>
        [HttpGet("overview")]
        [AllowAnonymous]
        public async Task<ActionResult<List<Ballers.Shared.TeamOverviewDto>>> GetOverview()
            => await _teams.GetOverviewAsync();
    }
}
