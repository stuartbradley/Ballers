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
    [AllowAnonymous]
    public class DebugController : ControllerBase
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

        // A card for a test player. The overall is drawn so roughly four in ten
        // are bronze, a third silver and a quarter gold, against whatever the
        // league's thresholds are; the six stats lean the way the position would
        // (forwards finish, defenders tackle) and add up to that overall.
        private static PlayerCardStats BuildTestCard(Player player, LeagueSetting settings, Random rng)
        {
            var silver = settings.SilverOverallThreshold;
            var gold = settings.GoldOverallThreshold;
            var cap = Math.Max(10, settings.AttributeCap);

            var roll = rng.Next(100);
            var overall = roll < 40 ? BronzeOverall(settings, rng)
                        : roll < 75 ? rng.Next(silver, Math.Max(silver + 1, gold))
                        : rng.Next(gold, Math.Max(gold + 1, Math.Min(cap, gold + 25) + 1));

            var card = new PlayerCardStats { PlayerId = player.Id };
            SetTestStats(card, player, overall, cap, rng);

            var unspent = rng.Next(0, 41);
            card.UnspentPoints = unspent;
            card.TotalPointsEarned = PlayerAttributes.Total(card) + unspent;
            return card;
        }

        // A bronze overall: somewhere in the lower two thirds of the bronze band.
        private static int BronzeOverall(LeagueSetting settings, Random rng)
        {
            var silver = settings.SilverOverallThreshold;
            return rng.Next(Math.Max(5, silver / 3), Math.Max(6, silver));
        }

        // Spreads an overall across the six stats, leaning the way the position
        // would (forwards finish, defenders tackle), so they add up to exactly
        // that overall.
        private static void SetTestStats(PlayerCardStats card, Player player, int overall, int cap, Random rng)
        {
            // Weights in card order: FIN, TAC, PAS, DRI, PAC, STR.
            double[] lean = player.Position switch
            {
                "FWD" => new[] { 1.5, 0.5, 0.9, 1.2, 1.3, 0.8 },
                "MID" => new[] { 0.9, 0.9, 1.5, 1.3, 1.0, 0.8 },
                "DEF" => new[] { 0.5, 1.5, 0.9, 0.7, 1.0, 1.4 },
                "GK"  => new[] { 0.4, 1.3, 1.0, 0.6, 0.8, 1.5 },
                _     => new[] { 1.0, 1.0, 1.0, 1.0, 1.0, 1.0 }
            };

            var weights = lean.Select(w => w * (0.85 + rng.NextDouble() * 0.3)).ToArray();
            var sum = weights.Sum();
            var total = overall * PlayerAttributes.Count;
            var stats = weights.Select(w => Math.Clamp((int)Math.Round(total * w / sum), 1, cap)).ToArray();

            // Rounding can leave the total a point or two out; settle it on the
            // strongest stat so the overall comes out exactly as drawn.
            var strongest = Array.IndexOf(stats, stats.Max());
            stats[strongest] = Math.Clamp(stats[strongest] + (total - stats.Sum()), 1, cap);

            card.Finishing = stats[0];
            card.Tackling = stats[1];
            card.Passing = stats[2];
            card.Dribbling = stats[3];
            card.Pace = stats[4];
            card.Strength = stats[5];
        }

        // "Jamie O'Neill" -> "jamie.oneill"
        private static string EmailSlug(string name)
        {
            var parts = name.ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => new string(p.Where(char.IsLetterOrDigit).ToArray()))
                .Where(p => p.Length > 0);
            var slug = string.Join(".", parts);
            return slug.Length == 0 ? "player" : slug;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private async Task ResetDatabaseAsync()
        {
            if (_env.IsDevelopment())
            {
                await ForceDropDatabaseAsync();
                await _db.Database.MigrateAsync();
            }
            else
            {
                await WipeAllDataAsync();
            }
            _db.ChangeTracker.Clear();
            await DbSeeder.Seed(_sp);
        }

        private async Task ForceDropDatabaseAsync()
        {
            var connStr = _db.Database.GetConnectionString()
                ?? throw new InvalidOperationException("No connection string.");
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connStr);
            var dbName = builder.InitialCatalog;
            if (string.IsNullOrWhiteSpace(dbName))
                throw new InvalidOperationException("No InitialCatalog in connection string.");

            // Reject odd db names to avoid identifier injection.
            if (dbName.Contains(']') || dbName.Contains('"'))
                throw new InvalidOperationException("Unsafe database name.");

            await _db.Database.CloseConnectionAsync();
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();

            builder.InitialCatalog = "master";
            await using var conn = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                IF DB_ID(N'{dbName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{dbName}];
                END";
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task WipeAllDataAsync()
        {
            _db.PenaltyKicks.RemoveRange(await _db.PenaltyKicks.ToListAsync());
            await _db.SaveChangesAsync();
            _db.FairplayRatings.RemoveRange(await _db.FairplayRatings.ToListAsync());
            await _db.SaveChangesAsync();
            _db.FixturePlayerStats.RemoveRange(await _db.FixturePlayerStats.ToListAsync());
            await _db.SaveChangesAsync();
            _db.FixturePlayers.RemoveRange(await _db.FixturePlayers.ToListAsync());
            await _db.SaveChangesAsync();
            _db.PenaltyShootouts.RemoveRange(await _db.PenaltyShootouts.ToListAsync());
            await _db.SaveChangesAsync();
            _db.KnockoutFixtures.RemoveRange(await _db.KnockoutFixtures.ToListAsync());
            await _db.SaveChangesAsync();
            _db.Notifications.RemoveRange(await _db.Notifications.ToListAsync());
            await _db.SaveChangesAsync();
            _db.Fixtures.RemoveRange(await _db.Fixtures.ToListAsync());
            await _db.SaveChangesAsync();
            _db.Seasons.RemoveRange(await _db.Seasons.ToListAsync());
            await _db.SaveChangesAsync();
            _db.Players.RemoveRange(await _db.Players.ToListAsync());
            await _db.SaveChangesAsync();
            // Identity tables before Teams — AspNetUsers.TeamId references Teams
            _db.UserRoles.RemoveRange(await _db.UserRoles.ToListAsync());
            await _db.SaveChangesAsync();
            _db.UserClaims.RemoveRange(await _db.UserClaims.ToListAsync());
            await _db.SaveChangesAsync();
            _db.UserLogins.RemoveRange(await _db.UserLogins.ToListAsync());
            await _db.SaveChangesAsync();
            _db.UserTokens.RemoveRange(await _db.UserTokens.ToListAsync());
            await _db.SaveChangesAsync();
            _db.Users.RemoveRange(await _db.Users.ToListAsync());
            await _db.SaveChangesAsync();
            _db.Roles.RemoveRange(await _db.Roles.ToListAsync());
            await _db.SaveChangesAsync();
            _db.Teams.RemoveRange(await _db.Teams.ToListAsync());
            await _db.SaveChangesAsync();
            _db.Referees.RemoveRange(await _db.Referees.ToListAsync());
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
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
                f.HomeStatsSubmitted = false;
                f.AwayStatsSubmitted = false;
                f.HomeScore = 0;
                f.AwayScore = 0;
                f.HomeCaptainId = null;
                f.HomeViceCaptainId = null;
                f.AwayCaptainId = null;
                f.AwayViceCaptainId = null;
            }
            await _db.SaveChangesAsync();
        }

        private static DateTime LastKickoff(List<Fixture> fixtures) =>
            fixtures.Max(f => f.Kickoff ?? f.WindowEnd);

        /// <summary>
        /// Moves the active season through time, fixtures and all, by whole days
        /// (so kickoff times of day stay put). The shift is worked out from the
        /// season's fixtures, e.g. "so the first week starts tomorrow".
        /// </summary>
        private async Task MoveActiveSeasonAsync(Func<List<Fixture>, TimeSpan> shiftFor)
        {
            var season = await _db.Seasons.FirstOrDefaultAsync(s => s.IsActive);
            if (season == null) return;

            var fixtures = await _db.Fixtures.Where(f => f.SeasonId == season.Id).ToListAsync();
            if (fixtures.Count == 0) return;

            var shift = TimeSpan.FromDays(Math.Round(shiftFor(fixtures).TotalDays));
            if (shift == TimeSpan.Zero) return;

            season.StartDate += shift;
            season.EndDate += shift;
            foreach (var f in fixtures)
            {
                f.WindowStart += shift;
                f.WindowEnd += shift;
                if (f.Kickoff.HasValue) f.Kickoff = f.Kickoff.Value + shift;
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
                // A result only counts once both sides have sent their stats.
                f.HomeStatsSubmitted = true;
                f.AwayStatsSubmitted = true;

                var homeOutfield = homeSquad.Where(p => p.Position != "GK").ToList();
                var awayOutfield = awaySquad.Where(p => p.Position != "GK").ToList();
                var homeCaps = homeOutfield.OrderBy(_ => rng.Next()).Take(2).ToList();
                var awayCaps = awayOutfield.OrderBy(_ => rng.Next()).Take(2).ToList();
                f.HomeCaptainId = homeCaps.Count > 0 ? homeCaps[0].Id : null;
                f.HomeViceCaptainId = homeCaps.Count > 1 ? homeCaps[1].Id : null;
                f.AwayCaptainId = awayCaps.Count > 0 ? awayCaps[0].Id : null;
                f.AwayViceCaptainId = awayCaps.Count > 1 ? awayCaps[1].Id : null;

                var homeOutfieldForMotm = homeSquad.Where(p => p.Position != "GK").ToList();
                var awayOutfieldForMotm = awaySquad.Where(p => p.Position != "GK").ToList();
                var homeMotmId = homeOutfieldForMotm.Count > 0
                    ? homeOutfieldForMotm[rng.Next(homeOutfieldForMotm.Count)].Id
                    : -1;
                var awayMotmId = awayOutfieldForMotm.Count > 0
                    ? awayOutfieldForMotm[rng.Next(awayOutfieldForMotm.Count)].Id
                    : -1;

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
                        ManOfTheMatch = p.Id == homeMotmId,
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
                        ManOfTheMatch = p.Id == awayMotmId,
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

        private static readonly string[] _motdBodies =
        {
            "Six goals, two penalties and a last-minute equaliser — this one had everything. The neutrals went home happy even if both managers were left scratching their heads at the back.",
            "A complete performance from front to back. The midfield bossed possession from the first whistle and the finishing was clinical when the chances came. A statement win.",
            "Backs-to-the-wall stuff and a famous away day. Defenders threw themselves in front of everything and the travelling support roared them over the line. Scenes at full time."
        };

        private static readonly string[] _motdPalette =
        {
            "#1e3a8a", "#065f46", "#7c2d12", "#4c1d95", "#9f1239", "#0f766e"
        };

        private async Task SeedMatchOfTheDayAsync(int seasonId)
        {
            if (await _db.MatchOfTheDayPosts.AnyAsync()) return;

            var fixtures = await _db.Fixtures
                .Include(f => f.HomeTeam)
                .Include(f => f.AwayTeam)
                .Where(f => f.SeasonId == seasonId && f.IsPlayed)
                .OrderByDescending(f => f.Kickoff)
                .Take(3)
                .ToListAsync();
            if (fixtures.Count == 0) return;

            var posts = new List<MatchOfTheDayPost>();
            for (int i = 0; i < fixtures.Count; i++)
            {
                var f = fixtures[i];
                var home = f.HomeTeam!.Name;
                var away = f.AwayTeam!.Name;
                var score = $"{f.HomeScore} – {f.AwayScore}";

                posts.Add(new MatchOfTheDayPost
                {
                    FixtureId = f.Id,
                    CoverImageBase64 = MockMatchImage(home, away, score, i, 0),
                    Body = _motdBodies[i % _motdBodies.Length],
                    CreatedAt = DateTime.UtcNow.AddDays(-i),
                    Photos = new List<MatchOfTheDayPhoto>
                    {
                        new() { ImageBase64 = MockMatchImage(home, away, "First Half", i, 1), SortOrder = 0 },
                        new() { ImageBase64 = MockMatchImage(home, away, "The Goal", i, 2), SortOrder = 1 },
                        new() { ImageBase64 = MockMatchImage(home, away, "Full Time", i, 3), SortOrder = 2 }
                    }
                });
            }

            _db.MatchOfTheDayPosts.AddRange(posts);
            await _db.SaveChangesAsync();
        }

        // Inline SVG data-URI placeholder so seeded posts have a real image with no binary assets.
        private static string MockMatchImage(string home, string away, string label, int seed, int variant)
        {
            var c1 = _motdPalette[(seed * 3 + variant) % _motdPalette.Length];
            var c2 = _motdPalette[(seed * 3 + variant + 2) % _motdPalette.Length];
            static string Esc(string s) => System.Security.SecurityElement.Escape(s) ?? s;

            var svg =
$@"<svg xmlns='http://www.w3.org/2000/svg' width='800' height='500' viewBox='0 0 800 500'>
  <defs><linearGradient id='g' x1='0' y1='0' x2='1' y2='1'>
    <stop offset='0' stop-color='{c1}'/><stop offset='1' stop-color='{c2}'/>
  </linearGradient></defs>
  <rect width='800' height='500' fill='url(#g)'/>
  <circle cx='400' cy='250' r='90' fill='none' stroke='rgba(255,255,255,0.18)' stroke-width='3'/>
  <line x1='400' y1='0' x2='400' y2='500' stroke='rgba(255,255,255,0.18)' stroke-width='3'/>
  <text x='400' y='205' fill='white' font-family='Segoe UI, sans-serif' font-size='40' font-weight='700' text-anchor='middle'>{Esc(home)}</text>
  <text x='400' y='255' fill='rgba(255,255,255,0.7)' font-family='Segoe UI, sans-serif' font-size='22' text-anchor='middle'>vs</text>
  <text x='400' y='305' fill='white' font-family='Segoe UI, sans-serif' font-size='40' font-weight='700' text-anchor='middle'>{Esc(away)}</text>
  <text x='400' y='460' fill='rgba(255,255,255,0.85)' font-family='Segoe UI, sans-serif' font-size='28' font-weight='600' text-anchor='middle'>{Esc(label)}</text>
</svg>";

            var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));
            return "data:image/svg+xml;base64," + b64;
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
