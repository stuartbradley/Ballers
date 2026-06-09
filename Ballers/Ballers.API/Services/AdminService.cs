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
        Task DeleteSeasonAsync(int seasonId);
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
    }
}
