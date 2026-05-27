using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Controllers
{
    [ApiController]
    [Route("api/debug")]
    [AllowAnonymous]
    public class DebugController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _db;
        private readonly IServiceProvider _sp;
        private readonly IKnockoutService _knockout;

        public DebugController(IWebHostEnvironment env, ApplicationDbContext db, IServiceProvider sp, IKnockoutService knockout)
        {
            _env = env;
            _db = db;
            _sp = sp;
            _knockout = knockout;
        }

        [HttpPost("clear")]
        public async Task<IActionResult> Clear()
        {
            if (!_env.IsDevelopment()) return NotFound();
            try
            {
                await ResetDatabaseAsync();
                return Ok(new { message = "Database cleared. Admin user recreated." });
            }
            catch (Exception ex) { return StatusCode(500, new { message = $"Failed: {ex.Message}" }); }
        }

        [HttpPost("setup-teams")]
        public async Task<IActionResult> SetupTeams()
        {
            if (!_env.IsDevelopment()) return NotFound();
            try
            {
                await ResetDatabaseAsync();
                await DevSeeder.SeedAsync(_sp);
                await RemoveSeasonDataAsync();
                return Ok(new { message = "10 teams and players seeded. No fixtures." });
            }
            catch (Exception ex) { return StatusCode(500, new { message = $"Failed: {ex.Message}" }); }
        }

        [HttpPost("setup-teams-fixtures")]
        public async Task<IActionResult> SetupTeamsAndFixtures()
        {
            if (!_env.IsDevelopment()) return NotFound();
            try
            {
                await ResetDatabaseAsync();
                await DevSeeder.SeedAsync(_sp);
                await UnplayAllFixturesAsync();
                return Ok(new { message = "Teams, players, and fixtures seeded. All fixtures unplayed." });
            }
            catch (Exception ex) { return StatusCode(500, new { message = $"Failed: {ex.Message}" }); }
        }

        [HttpPost("setup-full-season")]
        public async Task<IActionResult> SetupFullSeason()
        {
            if (!_env.IsDevelopment()) return NotFound();
            try
            {
                await ResetDatabaseAsync();
                await DevSeeder.SeedAsync(_sp);
                await PlayFixturesAsync(skipLast: false);
                var season = await _db.Seasons.FirstAsync(s => s.IsActive);
                await CompleteKnockoutAsync(season.Id);
                return Ok(new { message = "Full completed season seeded. All fixtures played, knockout bracket complete." });
            }
            catch (Exception ex) { return StatusCode(500, new { message = $"Failed: {ex.Message}" }); }
        }

        [HttpPost("setup-almost-complete")]
        public async Task<IActionResult> SetupAlmostComplete()
        {
            if (!_env.IsDevelopment()) return NotFound();
            try
            {
                await ResetDatabaseAsync();
                await DevSeeder.SeedAsync(_sp);
                var last = await PlayFixturesAsync(skipLast: true);
                if (last == null)
                    return StatusCode(500, new { message = "No fixtures found to skip." });

                var homeTeam = await _db.Teams.FindAsync(last.HomeTeamId);
                var awayTeam = await _db.Teams.FindAsync(last.AwayTeamId);
                var homeUser = await _db.Users.FirstOrDefaultAsync(u => u.TeamId == last.HomeTeamId);
                var awayUser = await _db.Users.FirstOrDefaultAsync(u => u.TeamId == last.AwayTeamId);

                return Ok(new
                {
                    message = "All fixtures played except the last one.",
                    homeTeam = homeTeam?.Name,
                    awayTeam = awayTeam?.Name,
                    homeManager = homeUser?.Email,
                    awayManager = awayUser?.Email,
                    password = "Manager123!"
                });
            }
            catch (Exception ex) { return StatusCode(500, new { message = $"Failed: {ex.Message}" }); }
        }

        [HttpPost("setup-next-season")]
        public async Task<IActionResult> SetupNextSeason()
        {
            if (!_env.IsDevelopment()) return NotFound();
            try
            {
                if (!await _db.Teams.AnyAsync())
                    return BadRequest(new { message = "No teams found. Run a setup first." });

                var nextNumber = (await _db.Seasons.MaxAsync(s => (int?)s.SeasonNumber) ?? 1) + 1;
                await DevSeeder.SeedNewSeasonAsync(_db, nextNumber);
                return Ok(new { message = $"Season {nextNumber} created. All fixtures unplayed." });
            }
            catch (Exception ex) { return StatusCode(500, new { message = $"Failed: {ex.Message}" }); }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private async Task ResetDatabaseAsync()
        {
            await _db.Database.EnsureDeletedAsync();
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await _db.Database.MigrateAsync();
            _db.ChangeTracker.Clear();
            await DbSeeder.Seed(_sp);
        }

        private async Task RemoveSeasonDataAsync()
        {
            _db.FixturePlayerStats.RemoveRange(await _db.FixturePlayerStats.ToListAsync());
            _db.FixturePlayers.RemoveRange(await _db.FixturePlayers.ToListAsync());
            _db.KnockoutFixtures.RemoveRange(await _db.KnockoutFixtures.ToListAsync());
            _db.Fixtures.RemoveRange(await _db.Fixtures.ToListAsync());
            _db.Seasons.RemoveRange(await _db.Seasons.ToListAsync());
            await _db.SaveChangesAsync();
        }

        private async Task UnplayAllFixturesAsync()
        {
            _db.FixturePlayerStats.RemoveRange(await _db.FixturePlayerStats.ToListAsync());
            _db.FixturePlayers.RemoveRange(await _db.FixturePlayers.ToListAsync());

            var fixtures = await _db.Fixtures.ToListAsync();
            foreach (var f in fixtures)
            {
                f.IsPlayed = false;
                f.HomeScore = 0;
                f.AwayScore = 0;
                f.HomeCaptainId = null;
                f.HomeViceCaptainId = null;
                f.AwayCaptainId = null;
                f.AwayViceCaptainId = null;
            }
            await _db.SaveChangesAsync();
        }

        private async Task<Fixture?> PlayFixturesAsync(bool skipLast)
        {
            var rng = new Random(43);
            var players = await _db.Players.ToListAsync();
            var playersByTeam = players.GroupBy(p => p.TeamId).ToDictionary(g => g.Key, g => g.ToList());

            var unplayed = await _db.Fixtures
                .Where(f => !f.IsPlayed)
                .OrderBy(f => f.WindowStart)
                .ThenBy(f => f.MatchNumber)
                .ToListAsync();
            if (unplayed.Count == 0) return null;

            Fixture? skipped = null;
            if (skipLast)
            {
                skipped = unplayed.Last();
                unplayed = unplayed.Take(unplayed.Count - 1).ToList();
            }

            var newFps = new List<FixturePlayer>();
            var newStats = new List<FixturePlayerStat>();

            foreach (var f in unplayed)
            {
                var homeSquad = playersByTeam[f.HomeTeamId].OrderBy(_ => rng.Next()).Take(11).ToList();
                var awaySquad = playersByTeam[f.AwayTeamId].OrderBy(_ => rng.Next()).Take(11).ToList();

                var homeGoals = WeightedGoals(rng);
                var awayGoals = WeightedGoals(rng);

                f.HomeScore = homeGoals;
                f.AwayScore = awayGoals;
                f.IsPlayed = true;

                var homeOutfield = homeSquad.Where(p => p.Position != "GK").ToList();
                var awayOutfield = awaySquad.Where(p => p.Position != "GK").ToList();
                var homeCaps = homeOutfield.OrderBy(_ => rng.Next()).Take(2).ToList();
                var awayCaps = awayOutfield.OrderBy(_ => rng.Next()).Take(2).ToList();
                f.HomeCaptainId = homeCaps.Count > 0 ? homeCaps[0].Id : null;
                f.HomeViceCaptainId = homeCaps.Count > 1 ? homeCaps[1].Id : null;
                f.AwayCaptainId = awayCaps.Count > 0 ? awayCaps[0].Id : null;
                f.AwayViceCaptainId = awayCaps.Count > 1 ? awayCaps[1].Id : null;

                var motmPool = homeGoals > awayGoals ? homeSquad
                             : awayGoals > homeGoals ? awaySquad
                             : homeSquad.Concat(awaySquad).ToList();
                var motmId = motmPool[rng.Next(motmPool.Count)].Id;

                foreach (var p in homeSquad.Concat(awaySquad))
                    newFps.Add(new FixturePlayer { FixtureId = f.Id, PlayerId = p.Id });

                var homeStatsMap = DistributeGoals(homeSquad, homeGoals, rng);
                var awayStatsMap = DistributeGoals(awaySquad, awayGoals, rng);

                foreach (var p in homeSquad)
                {
                    var (g, a) = homeStatsMap[p.Id];
                    newStats.Add(new FixturePlayerStat
                    {
                        FixtureId = f.Id, PlayerId = p.Id,
                        Goals = g, Assists = a,
                        ManOfTheMatch = p.Id == motmId,
                        YellowCards = rng.Next(12) == 0,
                        RedCard = rng.Next(70) == 0
                    });
                }
                foreach (var p in awaySquad)
                {
                    var (g, a) = awayStatsMap[p.Id];
                    newStats.Add(new FixturePlayerStat
                    {
                        FixtureId = f.Id, PlayerId = p.Id,
                        Goals = g, Assists = a,
                        ManOfTheMatch = p.Id == motmId,
                        YellowCards = rng.Next(12) == 0,
                        RedCard = rng.Next(70) == 0
                    });
                }
            }

            _db.FixturePlayers.AddRange(newFps);
            _db.FixturePlayerStats.AddRange(newStats);
            _db.Fixtures.UpdateRange(unplayed);
            await _db.SaveChangesAsync();
            return skipped;
        }

        private async Task CompleteKnockoutAsync(int seasonId)
        {
            var rng = new Random(43);
            var players = await _db.Players.ToListAsync();
            var playersByTeam = players.GroupBy(p => p.TeamId).ToDictionary(g => g.Key, g => g.ToList());

            await _knockout.GenerateAsync(seasonId);

            var newFps = new List<FixturePlayer>();
            var newStats = new List<FixturePlayerStat>();

            foreach (var tournament in new[] { "Cup", "Plate" })
            {
                var semis = await _db.KnockoutFixtures
                    .Where(k => k.SeasonId == seasonId && k.Tournament == tournament && k.Round == "Semifinal")
                    .OrderBy(k => k.Slot)
                    .ToListAsync();

                foreach (var semi in semis)
                {
                    await _knockout.SubmitResultAsync(semi.Id, 3, 1);
                    SeedKnockoutStats(semi, 3, 1, playersByTeam, newFps, newStats, rng);
                }

                var final = await _db.KnockoutFixtures
                    .FirstAsync(k => k.SeasonId == seasonId && k.Tournament == tournament && k.Round == "Final");

                await _knockout.SubmitResultAsync(final.Id, 2, 0);
                SeedKnockoutStats(final, 2, 0, playersByTeam, newFps, newStats, rng);
            }

            _db.FixturePlayers.AddRange(newFps);
            _db.FixturePlayerStats.AddRange(newStats);
            await _db.SaveChangesAsync();
        }

        private void SeedKnockoutStats(KnockoutFixture ko, int homeGoals, int awayGoals,
            Dictionary<int, List<Player>> playersByTeam,
            List<FixturePlayer> fps, List<FixturePlayerStat> stats, Random rng)
        {
            if (ko.LinkedFixtureId == null || ko.HomeTeamId == null || ko.AwayTeamId == null) return;

            var fixtureId = ko.LinkedFixtureId.Value;
            var homeSquad = playersByTeam[ko.HomeTeamId.Value].OrderBy(_ => rng.Next()).Take(11).ToList();
            var awaySquad = playersByTeam[ko.AwayTeamId.Value].OrderBy(_ => rng.Next()).Take(11).ToList();

            var motmPool = homeGoals > awayGoals ? homeSquad
                         : awayGoals > homeGoals ? awaySquad
                         : homeSquad.Concat(awaySquad).ToList();
            var motmId = motmPool[rng.Next(motmPool.Count)].Id;

            foreach (var p in homeSquad.Concat(awaySquad))
                fps.Add(new FixturePlayer { FixtureId = fixtureId, PlayerId = p.Id });

            var homeStatsMap = DistributeGoals(homeSquad, homeGoals, rng);
            var awayStatsMap = DistributeGoals(awaySquad, awayGoals, rng);

            foreach (var p in homeSquad)
            {
                var (g, a) = homeStatsMap[p.Id];
                stats.Add(new FixturePlayerStat
                {
                    FixtureId = fixtureId, PlayerId = p.Id,
                    Goals = g, Assists = a,
                    ManOfTheMatch = p.Id == motmId,
                    YellowCards = rng.Next(12) == 0,
                    RedCard = rng.Next(70) == 0
                });
            }
            foreach (var p in awaySquad)
            {
                var (g, a) = awayStatsMap[p.Id];
                stats.Add(new FixturePlayerStat
                {
                    FixtureId = fixtureId, PlayerId = p.Id,
                    Goals = g, Assists = a,
                    ManOfTheMatch = p.Id == motmId,
                    YellowCards = rng.Next(12) == 0,
                    RedCard = rng.Next(70) == 0
                });
            }
        }

        private static int WeightedGoals(Random rng) => rng.Next(100) switch
        {
            < 15 => 0,
            < 38 => 1,
            < 62 => 2,
            < 82 => 3,
            < 93 => 4,
            _ => 5
        };

        private static Dictionary<int, (int goals, int assists)> DistributeGoals(List<Player> squad, int total, Random rng)
        {
            var stats = squad.ToDictionary(p => p.Id, _ => (goals: 0, assists: 0));
            var outfield = squad.Where(p => p.Position != "GK").ToList();
            if (outfield.Count == 0) outfield = squad;

            for (int i = 0; i < total; i++)
            {
                var scorer = outfield[rng.Next(outfield.Count)];
                stats[scorer.Id] = (stats[scorer.Id].goals + 1, stats[scorer.Id].assists);

                if (rng.Next(2) == 0)
                {
                    var eligible = squad.Where(p => p.Id != scorer.Id).ToList();
                    if (eligible.Count > 0)
                    {
                        var assister = eligible[rng.Next(eligible.Count)];
                        stats[assister.Id] = (stats[assister.Id].goals, stats[assister.Id].assists + 1);
                    }
                }
            }

            return stats;
        }
    }
}
