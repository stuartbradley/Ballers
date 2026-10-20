using Ballers.API.Models;
using Ballers.API.Services;
using Ballers.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [ApiController]
    [Route("api/seasons")]
    public class SeasonsController : ControllerBase
    {
        private readonly ISeasonService _seasons;

        public SeasonsController(ISeasonService seasons) => _seasons = seasons;

        [HttpGet]
        public async Task<IActionResult> GetSeasons()
        {
            var seasons = await _seasons.GetAllAsync();
            return Ok(seasons.Select(ToDto));
        }

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentSeason()
        {
            var season = await _seasons.GetCurrentAsync();
            if (season == null) return NotFound();

            return Ok(ToDto(season));
        }

        private static SeasonDto ToDto(Season s) => new()
        {
            Id = s.Id,
            SeasonNumber = s.SeasonNumber,
            StartDate = s.StartDate,
            EndDate = s.EndDate,
            IsActive = s.IsActive,
            IsKnockoutGenerated = s.IsKnockoutGenerated
        };
    }
}
