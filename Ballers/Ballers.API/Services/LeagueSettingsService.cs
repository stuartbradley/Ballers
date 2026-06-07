using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Shared;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface ILeagueSettingsService
    {
        Task<LeagueSettingsDto> GetAsync();
        Task SetPlayersLockedAsync(bool locked);
        Task SetFixturesLockedAsync(bool locked);
        Task<bool> AreFixturesLockedAsync();
    }

    public class LeagueSettingsService : ILeagueSettingsService
    {
        private readonly ApplicationDbContext _db;

        public LeagueSettingsService(ApplicationDbContext db) => _db = db;

        private async Task<LeagueSetting> GetOrCreateAsync()
        {
            var setting = await _db.LeagueSettings.FirstOrDefaultAsync();
            if (setting == null)
            {
                setting = new LeagueSetting { PlayersLocked = false };
                _db.LeagueSettings.Add(setting);
                await _db.SaveChangesAsync();
            }
            return setting;
        }

        public async Task<LeagueSettingsDto> GetAsync()
        {
            var setting = await GetOrCreateAsync();
            return new LeagueSettingsDto
            {
                PlayersLocked = setting.PlayersLocked,
                FixturesLocked = setting.FixturesLocked
            };
        }

        public async Task SetPlayersLockedAsync(bool locked)
        {
            var setting = await GetOrCreateAsync();
            setting.PlayersLocked = locked;
            await _db.SaveChangesAsync();
        }

        public async Task SetFixturesLockedAsync(bool locked)
        {
            var setting = await GetOrCreateAsync();
            setting.FixturesLocked = locked;
            await _db.SaveChangesAsync();
        }

        public async Task<bool> AreFixturesLockedAsync()
        {
            var setting = await _db.LeagueSettings.FirstOrDefaultAsync();
            return setting?.FixturesLocked ?? false;
        }
    }
}
