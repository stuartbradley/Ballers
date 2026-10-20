using Ballers.API.Authorization;
using Ballers.API.Models;
using Ballers.API.Services;
using Ballers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ballers.API.Controllers
{
    [Route("api/playeraccount")]
    [ApiController]
    [Authorize]
    public class PlayerAccountController : ControllerBase
    {
        private readonly IPlayerAccountService _accounts;
        private readonly UserManager<ApplicationUser> _userManager;

        public PlayerAccountController(IPlayerAccountService accounts, UserManager<ApplicationUser> userManager)
        {
            _accounts = accounts;
            _userManager = userManager;
        }

        /// <summary>
        /// The squad places a signing-up player can claim. Anonymous because it is
        /// needed before the account exists; it exposes no more than the public
        /// team profile page already does.
        /// </summary>
        [HttpGet("teams/{teamId:int}/claimable")]
        [AllowAnonymous]
        public async Task<IActionResult> GetClaimable(int teamId)
            => Ok(await _accounts.GetClaimablePlayersAsync(teamId));

        /// <summary>
        /// Creates a player account with no role and no permissions, plus a request
        /// for their manager to approve. Rate limited: this is the only anonymous
        /// write endpoint on the site.
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        [EnableRateLimiting("player-signup")]
        public async Task<IActionResult> Register([FromBody] PlayerSignupRequest request)
        {
            try
            {
                await _accounts.RegisterAsync(request);
                return Ok();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        /// <summary>
        /// Where the caller's own account stands. Any logged-in account may ask,
        /// including one still waiting for approval — that is the whole point.
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMyStatus()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                return Ok(await _accounts.GetMyStatusAsync(user.Id));
            }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [HttpGet("requests")]
        [Authorize(Policy = Policies.SquadStaff)]
        public async Task<IActionResult> GetRequests()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                return Ok(await _accounts.GetRequestsAsync(user.Id));
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [HttpPost("requests/{id:int}/approve")]
        [Authorize(Policy = Policies.SquadStaff)]
        public async Task<IActionResult> Approve(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                await _accounts.ApproveAsync(id, user.Id);
                return Ok();
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
        }

        [HttpPost("requests/{id:int}/reject")]
        [Authorize(Policy = Policies.SquadStaff)]
        public async Task<IActionResult> Reject(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                await _accounts.RejectAsync(id, user.Id);
                return Ok();
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
        }
    }
}
