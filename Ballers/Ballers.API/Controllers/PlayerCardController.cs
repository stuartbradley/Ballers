using Ballers.API.Models;
using Ballers.API.Services;
using Ballers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [Route("api/playercard")]
    [ApiController]
    [Authorize]
    public class PlayerCardController : ControllerBase
    {
        private readonly IPlayerCardService _cards;
        private readonly ILeagueSettingsService _settings;
        private readonly UserManager<ApplicationUser> _userManager;

        public PlayerCardController(IPlayerCardService cards, ILeagueSettingsService settings,
            UserManager<ApplicationUser> userManager)
        {
            _cards = cards;
            _settings = settings;
            _userManager = userManager;
        }

        /// <summary>
        /// The ratings each tier starts at, for the home page. Only these two
        /// values: the rest of the points economy stays behind the admin role.
        /// </summary>
        [HttpGet("tiers")]
        [AllowAnonymous]
        public async Task<ActionResult<CardTierThresholdsDto>> GetTiers()
        {
            var s = await _settings.GetPointsSettingsAsync();
            return new CardTierThresholdsDto
            {
                SilverOverallThreshold = s.SilverOverallThreshold,
                GoldOverallThreshold = s.GoldOverallThreshold,
                AttributeCap = s.AttributeCap
            };
        }

        /// <summary>
        /// The card of the week for the home page. 204 when there is nobody to
        /// show yet, before the first points run has paid anyone.
        /// </summary>
        [HttpGet("featured")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFeatured()
        {
            var card = await _cards.GetFeaturedCardAsync();
            return card == null ? NoContent() : Ok(card);
        }

        /// <summary>A player's public card. Carries no points balance.</summary>
        [HttpGet("{playerId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> Get(int playerId)
        {
            var card = await _cards.GetPublicCardAsync(playerId);
            if (card == null) return NotFound();

            return Ok(new { attributes = card.Value.Attributes, tier = card.Value.Tier });
        }

        [HttpGet("me")]
        [Authorize(Roles = "Player")]
        public async Task<IActionResult> GetMine()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                return Ok(await _cards.GetMyCardAsync(user.Id));
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        [HttpGet("me/awards")]
        [Authorize(Roles = "Player")]
        public async Task<IActionResult> GetMyAwards()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                return Ok(await _cards.GetMyAwardsAsync(user.Id));
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("me/allocate")]
        [Authorize(Roles = "Player")]
        public async Task<IActionResult> Allocate([FromBody] AllocatePointsRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                return Ok(await _cards.AllocateAsync(user.Id, request));
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        }
    }
}
