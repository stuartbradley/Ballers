using Ballers.API.Data;
using Ballers.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface ISeasonService
    {
        Task<List<Season>> GetAllAsync();
        Task<Season?> GetCurrentAsync();
        Task ActivateSeasonAsync(int id);
    }

    public class SeasonService : ISeasonService
    {
        private readonly ApplicationDbContext _db;

        public SeasonService(ApplicationDbContext db) => _db = db;

        public async Task<List<Season>> GetAllAsync() =>
            await _db.Seasons.OrderBy(s => s.SeasonNumber).ToListAsync();

        public async Task<Season?> GetCurrentAsync()
        {
            // Prefer an explicitly activated season; fall back to date range
            var active = await _db.Seasons.FirstOrDefaultAsync(s => s.IsActive);
            if (active != null) return active;

            var today = DateTime.UtcNow;
            return await _db.Seasons
                .FirstOrDefaultAsync(s => s.StartDate <= today && s.EndDate >= today);
        }

        public async Task ActivateSeasonAsync(int id)
        {
            var season = await _db.Seasons.FindAsync(id)
                ?? throw new KeyNotFoundException($"Season {id} not found.");

            await _db.Seasons
                .Where(s => s.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));

            season.IsActive = true;
            await _db.SaveChangesAsync();
        }
    }
}
