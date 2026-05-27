using Ballers.API.Data;
using Ballers.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public record UserProfile(string? Email, int? TeamId, bool IsAdmin, string? TeamName);

    public interface IUserService
    {
        Task<UserProfile?> GetProfileAsync(string userId);
    }

    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _db;

        public UserService(ApplicationDbContext db) => _db = db;

        public async Task<UserProfile?> GetProfileAsync(string userId)
        {
            var user = await _db.Users
                .Include(u => u.Team)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return null;

            return new UserProfile(user.Email, user.TeamId, user.IsAdmin, user.Team?.Name);
        }
    }
}
