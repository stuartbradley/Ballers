using Ballers.API.Models;
using Ballers.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [Route("api/manager")]
    [ApiController]
    [Authorize(Roles = "Manager")]
    public class ManagerController : ControllerBase
    {
        private readonly IManagerDashboardService _dashboard;
        private readonly UserManager<ApplicationUser> _userManager;

        public ManagerController(IManagerDashboardService dashboard, UserManager<ApplicationUser> userManager)
        {
            _dashboard = dashboard;
            _userManager = userManager;
        }

        /// <summary>The signed-in manager's own team; no team can be asked for.</summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.TeamId is not int teamId) return NotFound("This account has no team.");

            var dto = await _dashboard.GetAsync(teamId, DateTime.UtcNow);
            return dto == null ? NotFound("Team not found.") : Ok(dto);
        }
    }
}
