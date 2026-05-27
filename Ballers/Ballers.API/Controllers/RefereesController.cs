using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Controllers
{
    [ApiController]
    [Route("api/referees")]
    [Authorize]
    public class RefereesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public RefereesController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var referees = await _db.Referees
                .Include(r => r.Fixtures).ThenInclude(f => f.HomeTeam)
                .Include(r => r.Fixtures).ThenInclude(f => f.AwayTeam)
                .OrderBy(r => r.Name)
                .ToListAsync();

            return Ok(referees.Select(ToDto));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SaveRefereeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Name is required.");

            var referee = new Referee
            {
                Name        = request.Name.Trim(),
                PhoneNumber = request.PhoneNumber?.Trim(),
                Email       = request.Email?.Trim(),
                Notes       = request.Notes?.Trim()
            };

            _db.Referees.Add(referee);
            await _db.SaveChangesAsync();
            return Ok(ToDto(referee));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] SaveRefereeRequest request)
        {
            var referee = await _db.Referees
                .Include(r => r.Fixtures).ThenInclude(f => f.HomeTeam)
                .Include(r => r.Fixtures).ThenInclude(f => f.AwayTeam)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (referee == null) return NotFound();

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Name is required.");

            referee.Name        = request.Name.Trim();
            referee.PhoneNumber = request.PhoneNumber?.Trim();
            referee.Email       = request.Email?.Trim();
            referee.Notes       = request.Notes?.Trim();

            await _db.SaveChangesAsync();
            return Ok(ToDto(referee));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var referee = await _db.Referees.FindAsync(id);
            if (referee == null) return NotFound();

            _db.Referees.Remove(referee);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        private static RefereeDto ToDto(Referee r) => new()
        {
            Id          = r.Id,
            Name        = r.Name,
            PhoneNumber = r.PhoneNumber,
            Email       = r.Email,
            Notes       = r.Notes,
            UpcomingFixtures = r.Fixtures
                .Where(f => !f.IsPlayed)
                .OrderBy(f => f.WindowStart)
                .Take(10)
                .Select(f => new RefereeFixtureDto
                {
                    FixtureId   = f.Id,
                    HomeTeam    = f.HomeTeam?.Name ?? "",
                    AwayTeam    = f.AwayTeam?.Name ?? "",
                    WindowStart = f.WindowStart,
                    WindowEnd   = f.WindowEnd,
                    Kickoff     = f.Kickoff
                }).ToList()
        };
    }
}
