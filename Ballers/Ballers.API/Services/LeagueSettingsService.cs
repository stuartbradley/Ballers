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
        Task SetFixturesHiddenAsync(bool hidden);
        Task<bool> AreFixturesHiddenAsync();

        /// <summary>
        /// The settings row itself, created if missing. The card and points
        /// services need the economy values, which the public DTO deliberately
        /// does not carry.
        /// </summary>
        Task<LeagueSetting> GetSettingsEntityAsync();

        Task<PointsSettingsDto> GetPointsSettingsAsync();
        Task UpdatePointsSettingsAsync(PointsSettingsDto settings);
        Task SetLastPointsRunAsync(DateTime runAt);
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
                FixturesLocked = setting.FixturesLocked,
                FixturesHidden = setting.FixturesHidden
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

        public async Task SetFixturesHiddenAsync(bool hidden)
        {
            var setting = await GetOrCreateAsync();
            setting.FixturesHidden = hidden;
            await _db.SaveChangesAsync();
        }

        public async Task<bool> AreFixturesHiddenAsync()
        {
            var setting = await _db.LeagueSettings.FirstOrDefaultAsync();
            return setting?.FixturesHidden ?? false;
        }

        public Task<LeagueSetting> GetSettingsEntityAsync() => GetOrCreateAsync();

        public async Task<PointsSettingsDto> GetPointsSettingsAsync()
        {
            var s = await GetOrCreateAsync();
            return new PointsSettingsDto
            {
                PlayerSignupEnabled = s.PlayerSignupEnabled,
                SignupPointsGrant = s.SignupPointsGrant,
                AttributeCap = s.AttributeCap,
                PointsPerAppearance = s.PointsPerAppearance,
                PointsPerWin = s.PointsPerWin,
                PointsPerGoal = s.PointsPerGoal,
                PointsPerAssist = s.PointsPerAssist,
                PointsPerCleanSheet = s.PointsPerCleanSheet,
                PointsPerMotm = s.PointsPerMotm,
                SilverOverallThreshold = s.SilverOverallThreshold,
                GoldOverallThreshold = s.GoldOverallThreshold,
                LastPointsRunAt = s.LastPointsRunAt
            };
        }

        public async Task UpdatePointsSettingsAsync(PointsSettingsDto settings)
        {
            if (settings.SignupPointsGrant < 0)
                throw new InvalidOperationException("Signup points grant cannot be negative.");
            if (settings.AttributeCap < 1)
                throw new InvalidOperationException("Attribute cap must be at least 1.");
            if (settings.GoldOverallThreshold < settings.SilverOverallThreshold)
                throw new InvalidOperationException("Gold threshold cannot be below the silver threshold.");

            // Negative point values would let a bad game take away points a player
            // has already banked, which the league rules do not allow.
            var values = new[]
            {
                settings.PointsPerAppearance, settings.PointsPerWin, settings.PointsPerGoal,
                settings.PointsPerAssist, settings.PointsPerCleanSheet, settings.PointsPerMotm
            };
            if (values.Any(v => v < 0))
                throw new InvalidOperationException("Point values cannot be negative.");

            var s = await GetOrCreateAsync();
            s.PlayerSignupEnabled = settings.PlayerSignupEnabled;
            s.SignupPointsGrant = settings.SignupPointsGrant;
            s.AttributeCap = settings.AttributeCap;
            s.PointsPerAppearance = settings.PointsPerAppearance;
            s.PointsPerWin = settings.PointsPerWin;
            s.PointsPerGoal = settings.PointsPerGoal;
            s.PointsPerAssist = settings.PointsPerAssist;
            s.PointsPerCleanSheet = settings.PointsPerCleanSheet;
            s.PointsPerMotm = settings.PointsPerMotm;
            s.SilverOverallThreshold = settings.SilverOverallThreshold;
            s.GoldOverallThreshold = settings.GoldOverallThreshold;
            await _db.SaveChangesAsync();
        }

        public async Task SetLastPointsRunAsync(DateTime runAt)
        {
            var s = await GetOrCreateAsync();
            s.LastPointsRunAt = runAt;
            await _db.SaveChangesAsync();
        }
    }
}
