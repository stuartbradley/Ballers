using Ballers.API.Authorization;
using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Controllers
{
    [ApiController]
    [Route("api/debug")]
    // UAT is on the public internet and these endpoints wipe the data, reset
    // passwords and list logins, so only a signed-in admin may use them.
    [Authorize(Policy = Policies.SiteAdmin)]
    public partial class DebugController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _db;
        private readonly IServiceProvider _sp;
        private readonly IKnockoutService _knockout;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILeagueSettingsService _settings;

        // Every player account gets this password; new ones also get an address on
        // this domain, so they are easy to spot among real sign-ups.
        private const string TestPlayerPassword = "Ballers1!";
        private const string TestPlayerDomain = "players.ballers.com";

        public DebugController(IWebHostEnvironment env, IConfiguration config, ApplicationDbContext db, IServiceProvider sp,
            IKnockoutService knockout, UserManager<ApplicationUser> userManager, ILeagueSettingsService settings)
        {
            _env = env;
            _config = config;
            _db = db;
            _sp = sp;
            _knockout = knockout;
            _userManager = userManager;
            _settings = settings;
        }

        private bool IsDebugAllowed() => _env.IsDevelopment() || _config.GetValue<bool>("IsUAT");

        [HttpPost("clear")]
        public async Task<IActionResult> Clear()
        {
            if (!IsDebugAllowed()) return NotFound();
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
            if (!IsDebugAllowed()) return NotFound();
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
            if (!IsDebugAllowed()) return NotFound();
            try
            {
                await ResetDatabaseAsync();
                await DevSeeder.SeedAsync(_sp);
                await UnplayAllFixturesAsync();
                // A fresh season: every game still to come, starting next week.
                await MoveActiveSeasonAsync(fixtures => DateTime.UtcNow.Date.AddDays(1) - fixtures.Min(f => f.WindowStart).Date);
                return Ok(new { message = "Teams, players, and fixtures seeded. Every fixture is in the future, starting tomorrow." });
            }
            catch (Exception ex) { return StatusCode(500, new { message = $"Failed: {ex.Message}" }); }
        }

        [HttpPost("setup-full-season")]
        public async Task<IActionResult> SetupFullSeason()
        {
            if (!IsDebugAllowed()) return NotFound();
            try
            {
                await ResetDatabaseAsync();
                await DevSeeder.SeedAsync(_sp);
                // Every game has been played, so all of them are in the past.
                await MoveActiveSeasonAsync(fixtures => DateTime.UtcNow.Date.AddDays(-3) - LastKickoff(fixtures).Date);
                await PlayFixturesAsync(skipLast: false);
                var season = await _db.Seasons.FirstAsync(s => s.IsActive);
                await CompleteKnockoutAsync(season.Id);
                await SeedMatchOfTheDayAsync(season.Id);
                return Ok(new { message = "Full completed season seeded. All fixtures played, knockout bracket complete." });
            }
            catch (Exception ex) { return StatusCode(500, new { message = $"Failed: {ex.Message}" }); }
        }

        [HttpPost("setup-almost-complete")]
        public async Task<IActionResult> SetupAlmostComplete()
        {
            if (!IsDebugAllowed()) return NotFound();
            try
            {
                await ResetDatabaseAsync();
                await DevSeeder.SeedAsync(_sp);
                // The last game was yesterday and is waiting for its stats; the
                // rest were before it.
                await MoveActiveSeasonAsync(fixtures => DateTime.UtcNow.Date.AddDays(-1) - LastKickoff(fixtures).Date);
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
                    password = DevSeeder.ManagerPassword
                });
            }
            catch (Exception ex) { return StatusCode(500, new { message = $"Failed: {ex.Message}" }); }
        }

        [HttpPost("setup-next-season")]
        public async Task<IActionResult> SetupNextSeason()
        {
            if (!IsDebugAllowed()) return NotFound();
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

        /// <summary>
        /// Makes every player in the league loggable-in with the test password.
        /// Active squad players with no account get an approved player account;
        /// players who already have one keep it but have its password reset to
        /// the test password. Anyone without a card gets one, with ratings spread
        /// across bronze, silver and gold. Nothing else is touched, and running it
        /// again only resets the passwords again.
        /// </summary>
        [HttpPost("setup-player-accounts")]
        public async Task<IActionResult> SetupPlayerAccounts()
        {
            if (!IsDebugAllowed()) return NotFound();
            try
            {
                var settings = await _settings.GetSettingsEntityAsync();
                var rng = new Random(2026);

                var existingCards = (await _db.PlayerCards.Select(c => c.PlayerId).ToListAsync()).ToHashSet();

                // Hashing is the slow part, and the password is the same for every
                // account, so it is hashed once. Each account still checks against
                // it normally at login.
                var hash = _userManager.PasswordHasher.HashPassword(new ApplicationUser(), TestPlayerPassword);

                // ── Accounts that already exist: same account, test password ──
                var linked = await _db.Users
                    .Include(u => u.Player)
                    .Where(u => u.PlayerId != null)
                    .ToListAsync();

                var reset = 0;
                foreach (var user in linked)
                {
                    user.PasswordHash = hash;
                    // A new stamp signs out anywhere the old password was in use.
                    user.SecurityStamp = Guid.NewGuid().ToString();
                    var update = await _userManager.UpdateAsync(user);
                    if (!update.Succeeded)
                        return StatusCode(500, new
                        {
                            message = $"Stopped resetting {user.Email}: " + string.Join("; ", update.Errors.Select(e => e.Description))
                        });

                    if (!await _userManager.IsInRoleAsync(user, "Player"))
                        await _userManager.AddToRoleAsync(user, "Player");

                    if (user.Player != null && existingCards.Add(user.Player.Id))
                        _db.PlayerCards.Add(BuildTestCard(user.Player, settings, rng));

                    reset++;
                }

                // ── Everyone else: a new account ──
                var takenSet = linked.Select(u => u.PlayerId!.Value).ToHashSet();
                var todo = await _db.Players
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.TeamId).ThenBy(p => p.Number)
                    .ToListAsync();
                todo = todo.Where(p => !takenSet.Contains(p.Id)).ToList();

                var created = 0;
                foreach (var player in todo)
                {
                    var email = $"{EmailSlug(player.Name)}.{player.Id}@{TestPlayerDomain}";
                    var user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true,
                        PasswordHash = hash,
                        PlayerId = player.Id
                    };

                    var result = await _userManager.CreateAsync(user);
                    if (!result.Succeeded)
                        return StatusCode(500, new
                        {
                            message = $"Stopped at {player.Name}: " + string.Join("; ", result.Errors.Select(e => e.Description))
                        });

                    await _userManager.AddToRoleAsync(user, "Player");

                    if (existingCards.Add(player.Id))
                        _db.PlayerCards.Add(BuildTestCard(player, settings, rng));

                    created++;
                }

                await _db.SaveChangesAsync();
                return Ok(new
                {
                    message = $"{created} accounts created and {reset} existing accounts reset. " +
                              $"Every player now logs in with {TestPlayerPassword}"
                });
            }
            catch (Exception ex) { return StatusCode(500, new { message = $"Failed: {ex.Message}" }); }
        }

        /// <summary>
        /// Every player account. The password is shown only where the account
        /// really does still use the test password: each stored hash is checked
        /// against it, so an account whose password has since been changed (or a
        /// sign-up made after the last reset) shows as its own.
        /// </summary>
        [HttpGet("player-accounts")]
        public async Task<IActionResult> GetPlayerAccounts()
        {
            if (!IsDebugAllowed()) return NotFound();

            var settings = await _settings.GetSettingsEntityAsync();

            var rows = await _db.Users
                .Where(u => u.PlayerId != null)
                .Select(u => new
                {
                    u.Email,
                    u.PasswordHash,
                    PlayerId = u.PlayerId!.Value,
                    u.Player!.Name,
                    u.Player.Number,
                    u.Player.Position,
                    Team = u.Player.Team!.Name
                })
                .ToListAsync();

            // One reset gives every account the same hash, so checking each
            // distinct hash once is enough, and keeps this quick.
            var usesTestPassword = rows
                .Select(r => r.PasswordHash)
                .Where(h => !string.IsNullOrEmpty(h))
                .Distinct()
                .ToDictionary(h => h!, h => _userManager.PasswordHasher
                    .VerifyHashedPassword(new ApplicationUser(), h!, TestPlayerPassword) != PasswordVerificationResult.Failed);

            var cards = await _db.PlayerCards.ToDictionaryAsync(c => c.PlayerId);

            var list = rows
                .Select(r =>
                {
                    cards.TryGetValue(r.PlayerId, out var card);
                    var overall = card == null ? (int?)null : PlayerAttributes.ComputeOverall(card);
                    return new
                    {
                        r.Name,
                        r.Team,
                        r.Number,
                        r.Position,
                        r.Email,
                        Password = r.PasswordHash != null && usesTestPassword.GetValueOrDefault(r.PasswordHash)
                            ? TestPlayerPassword
                            : null,
                        Overall = overall,
                        Tier = overall == null ? null
                            : PlayerAttributes.Tier(overall.Value, settings.SilverOverallThreshold, settings.GoldOverallThreshold),
                        UnspentPoints = card?.UnspentPoints
                    };
                })
                .OrderBy(r => r.Team).ThenBy(r => r.Number)
                .ToList();

            return Ok(list);
        }

        /// <summary>
        /// Every manager login, with their team. As with players, the password is
        /// shown only where the account still uses the seeded one.
        /// </summary>
        [HttpGet("manager-accounts")]
        public async Task<IActionResult> GetManagerAccounts()
        {
            if (!IsDebugAllowed()) return NotFound();

            var rows = await (from u in _db.Users
                              join ur in _db.UserRoles on u.Id equals ur.UserId
                              join r in _db.Roles on ur.RoleId equals r.Id
                              where r.Name == "Manager"
                              select new
                              {
                                  u.Email,
                                  u.PasswordHash,
                                  Team = u.Team != null ? u.Team.Name : null,
                                  ManagerName = u.Team != null ? u.Team.ManagerName : null
                              })
                .ToListAsync();

            var usesSeedPassword = rows
                .Select(r => r.PasswordHash)
                .Where(h => !string.IsNullOrEmpty(h))
                .Distinct()
                .ToDictionary(h => h!, h => _userManager.PasswordHasher
                    .VerifyHashedPassword(new ApplicationUser(), h!, DevSeeder.ManagerPassword) != PasswordVerificationResult.Failed);

            var list = rows
                .Select(r => new
                {
                    Team = r.Team ?? "(no team)",
                    r.ManagerName,
                    r.Email,
                    Password = r.PasswordHash != null && usesSeedPassword.GetValueOrDefault(r.PasswordHash)
                        ? DevSeeder.ManagerPassword
                        : null
                })
                .OrderBy(r => r.Team)
                .ToList();

            return Ok(list);
        }

        /// <summary>
        /// For testing upgrades: every card goes back to bronze, with its stats
        /// re-spread to a bronze overall, and gets 200 points to spend. Only
        /// existing cards are touched; players without an account have none, so
        /// run "Create Player Accounts" first.
        /// </summary>
        [HttpPost("reset-cards-bronze")]
        public async Task<IActionResult> ResetCardsToBronze()
        {
            if (!IsDebugAllowed()) return NotFound();
            try
            {
                const int TestPoints = 200;
                var settings = await _settings.GetSettingsEntityAsync();
                var cap = Math.Max(10, settings.AttributeCap);
                var rng = new Random(2027);

                var cards = await _db.PlayerCards.Include(c => c.Player).ToListAsync();
                if (cards.Count == 0)
                    return BadRequest(new { message = "No cards yet. Run \"Create Player Accounts\" first." });

                foreach (var card in cards)
                {
                    SetTestStats(card, card.Player!, BronzeOverall(settings, rng), cap, rng);
                    card.UnspentPoints = TestPoints;
                    card.TotalPointsEarned = PlayerAttributes.Total(card) + TestPoints;
                }

                await _db.SaveChangesAsync();
                return Ok(new { message = $"{cards.Count} cards reset to bronze, each with {TestPoints} points to spend." });
            }
            catch (Exception ex) { return StatusCode(500, new { message = $"Failed: {ex.Message}" }); }
        }
    }
}