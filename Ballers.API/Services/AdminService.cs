using Ballers.API.Data;
using Ballers.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface IAdminService
    {
        Task CreateTeamAsync(string teamName, string email, string password);
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
    }
}
