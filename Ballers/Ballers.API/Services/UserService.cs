using Ballers.API.Data;
using Ballers.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    // TeamId is populated for managers only. A player's team is reported through
    // TeamName for display, deliberately without a TeamId, because a player user
    // carrying one would be swept into every team notification fan-out.
    public record UserProfile(
        string? Email,
        int? TeamId,
        bool IsAdmin,
        string? TeamName,
        bool IsReferee,
        int? PlayerId = null,
        string? PlayerName = null,
        bool IsPendingPlayer = false);

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
                .Include(u => u.Player)!.ThenInclude(p => p!.Team)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return null;

            // An account with no role, no team and no player is one that has signed
            // up and is still waiting for its manager. Flagged so the UI can say so
            // rather than showing an empty dashboard.
            var isPendingPlayer =
                !user.IsAdmin && !user.IsReferee && user.TeamId == null && user.PlayerId == null;

            return new UserProfile(
                user.Email,
                user.TeamId,
                user.IsAdmin,
                user.Team?.Name ?? user.Player?.Team?.Name,
                user.IsReferee,
                user.PlayerId,
                user.Player?.Name,
                isPendingPlayer);
        }
    }
}
