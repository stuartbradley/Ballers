using Ballers.API.Models;
using Ballers.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StatsController : ControllerBase
    {
        private readonly IStatsService _stats;
        private readonly UserManager<ApplicationUser> _userManager;

        public StatsController(IStatsService stats, UserManager<ApplicationUser> userManager)
        {
            _stats = stats;
            _userManager = userManager;
        }

        [AllowAnonymous]
        [HttpGet("top-motm")]
        public async Task<IActionResult> GetTopMotm()
            => Ok(await _stats.GetTopMotmAsync());

        [AllowAnonymous]
        [HttpGet("top-scorers")]
        public async Task<IActionResult> GetTopScorers()
            => Ok(await _stats.GetTopScorersAsync());

        [AllowAnonymous]
        [HttpGet("top-assists")]
        public async Task<IActionResult> GetTopAssists()
            => Ok(await _stats.GetTopAssistsAsync());

        [Authorize]
        [HttpGet("winloss")]
        public async Task<IActionResult> GetWinLoss()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.TeamId == null)
                return Ok(new { wins = 0, losses = 0, draws = 0 });

            var result = await _stats.GetWinLossAsync(user.TeamId.Value);
            return Ok(new { wins = result.Wins, losses = result.Losses, draws = result.Draws });
        }
    }
}
