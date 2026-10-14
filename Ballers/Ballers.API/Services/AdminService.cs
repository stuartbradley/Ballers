using Ballers.API.Data;
using Ballers.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface IAdminService
    {
        Task CreateTeamAsync(string teamName, string email, string password);
        Task CreateRefereeAsync(string email, string password);
        Task CreateAdminAsync(string email, string password);
        Task DeleteSeasonAsync(int seasonId);
        Task<List<Ballers.Shared.AdminAccountDto>> GetAccountsAsync(string currentUserId);
        Task ResetPasswordAsync(string userId, string newPassword);
        Task DeleteAccountAsync(string userId, string currentUserId);
    }

    public class AdminService : IAdminService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task CreateTeamAsync(string teamName, string email, string password)
        {
            if (await _db.Teams.AnyAsync(t => t.Name == teamName))
                throw new InvalidOperationException("Team already exists.");

            using var transaction = await _db.Database.BeginTransactionAsync();

            var team = new Team { Name = teamName };
            _db.Teams.Add(team);
            await _db.SaveChangesAsync();

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                TeamId = team.Id,
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException(
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            await _userManager.AddToRoleAsync(user, "Manager");
            await transaction.CommitAsync();
        }

        public async Task CreateRefereeAsync(string email, string password)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                IsReferee = true,
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    string.Join("; ", result.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, "Referee");
        }

        public async Task CreateAdminAsync(string email, string password)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                IsAdmin = true,
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    string.Join("; ", result.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, "Admin");
        }

        public async Task DeleteSeasonAsync(int seasonId)
        {
            var season = await _db.Seasons.FirstOrDefaultAsync(s => s.Id == seasonId);
            if (season == null)
                throw new KeyNotFoundException("Season not found.");

            using var tx = await _db.Database.BeginTransactionAsync();

            // Knockout fixtures reference fixtures via a NoAction FK, so remove them
            // first. The remaining fixture children (stats, squads, penalties, fairplay)
            // are cascade-deleted when the fixtures go.
            var knockouts = await _db.KnockoutFixtures.Where(k => k.SeasonId == seasonId).ToListAsync();
            _db.KnockoutFixtures.RemoveRange(knockouts);
            await _db.SaveChangesAsync();

            var fixtures = await _db.Fixtures.Where(f => f.SeasonId == seasonId).ToListAsync();
            _db.Fixtures.RemoveRange(fixtures);
            await _db.SaveChangesAsync();

            _db.Seasons.Remove(season);
            await _db.SaveChangesAsync();

            await tx.CommitAsync();
        }

        // ── Accounts ──────────────────────────────────────────────────

        public async Task<List<Ballers.Shared.AdminAccountDto>> GetAccountsAsync(string currentUserId)
        {
            var users = await _db.Users
                .AsNoTracking()
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.EmailConfirmed,
                    u.LockoutEnd,
                    TeamName = u.Team != null ? u.Team.Name : null,
                    PlayerName = u.Player != null ? u.Player.Name : null,
                    PlayerTeam = u.Player != null ? u.Player.Team!.Name : null
                })
                .ToListAsync();

            var roles = await (from ur in _db.UserRoles
                               join r in _db.Roles on ur.RoleId equals r.Id
                               select new { ur.UserId, r.Name })
                .AsNoTracking()
                .ToListAsync();
            var rolesByUser = roles.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.Select(r => r.Name!).ToList());

            // A player still waiting (or turned down) has no role yet; their
            // request says who they are and which team they asked to join.
            var requests = await _db.PlayerAccountRequests
                .AsNoTracking()
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new { r.UserId, r.RequestedName, Team = r.RequestedTeam!.Name })
                .ToListAsync();
            var requestByUser = requests.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.First());

            // Highest role first, for anyone holding more than one.
            static string MainRole(List<string> held) =>
                new[] { "Admin", "Manager", "Referee", "Player" }.FirstOrDefault(held.Contains) ?? "Pending";

            return users
                .Select(u =>
                {
                    var role = MainRole(rolesByUser.GetValueOrDefault(u.Id) ?? new List<string>());
                    requestByUser.TryGetValue(u.Id, out var request);
                    return new Ballers.Shared.AdminAccountDto
                    {
                        Id = u.Id,
                        Email = u.Email ?? "",
                        Role = role,
                        TeamName = u.TeamName ?? u.PlayerTeam ?? (role == "Pending" ? request?.Team : null),
                        PlayerName = u.PlayerName ?? (role == "Pending" ? request?.RequestedName : null),
                        IsYou = u.Id == currentUserId,
                        EmailConfirmed = u.EmailConfirmed,
                        LockedOut = u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow
                    };
                })
                .OrderBy(a => Array.IndexOf(new[] { "Admin", "Manager", "Referee", "Player", "Pending" }, a.Role))
                .ThenBy(a => a.TeamName ?? "")
                .ThenBy(a => a.Email)
                .ToList();
        }

        public async Task ResetPasswordAsync(string userId, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new KeyNotFoundException("Account not found.");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

            // A reset is often for someone locked out after too many tries.
            if (await _userManager.IsLockedOutAsync(user))
                await _userManager.SetLockoutEndDateAsync(user, null);
        }

        /// <summary>
        /// Removes a login. Only the login: a manager's team, its squad and its
        /// results stay, as does a player's place in the squad.
        /// </summary>
        public async Task DeleteAccountAsync(string userId, string currentUserId)
        {
            if (userId == currentUserId)
                throw new InvalidOperationException("You can't remove your own account.");

            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new KeyNotFoundException("Account not found.");

            if (await _userManager.IsInRoleAsync(user, "Admin")
                && (await _userManager.GetUsersInRoleAsync("Admin")).Count <= 1)
                throw new InvalidOperationException("That's the only admin account; create another before removing it.");

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
