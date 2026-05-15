using Ballers.API.Services;
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
            return Ok(seasons.Select(s => new
            {
                s.Id,
                s.SeasonNumber,
                s.StartDate,
                s.EndDate,
                s.IsActive
            }));
        }

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentSeason()
        {
            var season = await _seasons.GetCurrentAsync();
            if (season == null) return NotFound();

            return Ok(new
            {
                season.Id,
                season.SeasonNumber,
                season.StartDate,
                season.EndDate
            });
        }
    }
}
