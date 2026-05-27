using Ballers.API.Data;
using Ballers.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Controllers
{
    [ApiController]
    [Route("api/test")]
    [AllowAnonymous]
    public class TestController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _db;
        private readonly IServiceProvider _sp;
        private readonly INotificationChecker _checker;
        private readonly IKnockoutService _knockout;

        public TestController(IWebHostEnvironment env, ApplicationDbContext db, IServiceProvider sp, INotificationChecker checker, IKnockoutService knockout)
        {
            _env = env;
            _db = db;
            _sp = sp;
            _checker = checker;
            _knockout = knockout;
        }

        [HttpPost("reset")]
        public async Task<IActionResult> Reset()
        {
            if (!_env.IsEnvironment("Testing"))
                return NotFound();

            await _db.Database.EnsureDeletedAsync();
            await _db.Database.MigrateAsync();
            await DbSeeder.Seed(_sp);
            await DevSeeder.SeedAsync(_sp);

            return Ok(new { message = "BallersAutoTest reset and seeded." });
        }

        [HttpPost("reset-knockout")]
        public async Task<IActionResult> ResetKnockout()
        {
            if (!_env.IsEnvironment("Testing")) return NotFound();

            var season = await _db.Seasons.FirstOrDefaultAsync(s => s.IsActive);
            if (season == null) return BadRequest(new { error = "No active season." });

            await ClearKnockoutDataAsync(season.Id);
            season.IsKnockoutGenerated = false;
            await _db.SaveChangesAsync();

            return Ok(new { seasonId = season.Id });
        }

        [HttpPost("setup-knockout")]
        public async Task<IActionResult> SetupKnockout()
        {
            if (!_env.IsEnvironment("Testing")) return NotFound();

            var season = await _db.Seasons.FirstOrDefaultAsync(s => s.IsActive);
            if (season == null) return BadRequest(new { error = "No active season." });

            await ClearKnockoutDataAsync(season.Id);
            season.IsKnockoutGenerated = false;
            await _db.SaveChangesAsync();

            await _knockout.GenerateAsync(season.Id);

            // Return first linked fixture ID and its home manager's email so tests can log in as the right user.
            var firstKo = await _db.KnockoutFixtures
                .Where(k => k.SeasonId == season.Id && k.LinkedFixtureId.HasValue)
                .OrderBy(k => k.Id)
                .FirstOrDefaultAsync();

            string? managerEmail = null;
            if (firstKo?.HomeTeamId != null)
            {
                var homeManager = await _db.Users.FirstOrDefaultAsync(u => u.TeamId == firstKo.HomeTeamId);
                managerEmail = homeManager?.Email;
            }

            return Ok(new { seasonId = season.Id, fixtureId = firstKo?.LinkedFixtureId, managerEmail });
        }

        private async Task ClearKnockoutDataAsync(int seasonId)
        {
            // Remove linked Fixture records before removing KnockoutFixtures (NoAction FK)
            var linkedIds = await _db.KnockoutFixtures
                .Where(k => k.SeasonId == seasonId && k.LinkedFixtureId.HasValue)
                .Select(k => k.LinkedFixtureId!.Value)
                .ToListAsync();

            var koFixtures = await _db.KnockoutFixtures.Where(k => k.SeasonId == seasonId).ToListAsync();
            _db.KnockoutFixtures.RemoveRange(koFixtures);
            await _db.SaveChangesAsync();

            if (linkedIds.Count > 0)
            {
                var linkedFixtures = await _db.Fixtures.Where(f => linkedIds.Contains(f.Id)).ToListAsync();
                _db.Fixtures.RemoveRange(linkedFixtures);
                await _db.SaveChangesAsync();
            }
        }

        [HttpPost("run-notification-checks")]
        public async Task<IActionResult> RunNotificationChecks()
        {
            if (!_env.IsEnvironment("Testing")) return NotFound();
            await _checker.RunChecksAsync();
            return Ok();
        }

        [HttpPost("prepare-notification-scenario")]
        public async Task<IActionResult> PrepareNotificationScenario([FromBody] PrepareScenarioRequest request)
        {
            if (!_env.IsEnvironment("Testing")) return NotFound();

            var fixture = await _db.Fixtures
                .Include(f => f.HomeTeam)
                .Include(f => f.AwayTeam)
                .Where(f => !f.IsPlayed && f.Kickoff > DateTime.UtcNow)
                .OrderBy(f => f.Kickoff)
                .FirstOrDefaultAsync();

            if (fixture == null)
                return BadRequest(new { error = "No upcoming unplayed fixture found." });

            switch (request.Scenario)
            {
                case "NoLocation72h":
                    fixture.Kickoff = DateTime.UtcNow.AddHours(50);
                    fixture.Location = null;
                    fixture.Postcode = null;
                    break;

                case "NoReferee48h":
                    fixture.Kickoff = DateTime.UtcNow.AddHours(36);
                    fixture.RefereeId = null;
                    break;

                case "NoSquad24h":
                    fixture.Kickoff = DateTime.UtcNow.AddHours(18);
                    var squad = await _db.FixturePlayers
                        .Where(fp => fp.FixtureId == fixture.Id).ToListAsync();
                    _db.FixturePlayers.RemoveRange(squad);
                    break;

                case "NoStats48h":
                    fixture.Kickoff = DateTime.UtcNow.AddHours(-50);
                    fixture.IsPlayed = false;
                    break;

                default:
                    return BadRequest(new { error = $"Unknown scenario: {request.Scenario}" });
            }

            await _db.SaveChangesAsync();

            var homeManager = await _db.Users.FirstOrDefaultAsync(u => u.TeamId == fixture.HomeTeamId);
            var awayManager = await _db.Users.FirstOrDefaultAsync(u => u.TeamId == fixture.AwayTeamId);

            return Ok(new
            {
                fixtureId = fixture.Id,
                homeManagerEmail = homeManager?.Email,
                awayManagerEmail = awayManager?.Email,
                homeTeamName = fixture.HomeTeam?.Name,
                awayTeamName = fixture.AwayTeam?.Name
            });
        }
    }
}

public record PrepareScenarioRequest(string Scenario);
