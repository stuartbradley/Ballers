using Ballers.API.Data;
using Ballers.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface IStatsService
    {
        Task<List<PlayerGoalsStat>> GetTopScorersAsync();
        Task<List<PlayerAssistsStat>> GetTopAssistsAsync();
        Task<List<PlayerMotmStat>> GetTopMotmAsync();
        Task<WinLossDrawResult> GetWinLossAsync(int teamId);
    }

    public class StatsService : IStatsService
    {
        private readonly ApplicationDbContext _db;

        public StatsService(ApplicationDbContext db) => _db = db;

        private async Task<List<FixturePlayerStat>> GetCurrentSeasonStatsAsync()
        {
            var today = DateTime.UtcNow;
            return await _db.FixturePlayerStats
                .Include(x => x.Fixture).ThenInclude(f => f!.Season)
                .Include(x => x.Player)
                .Where(x => x.Fixture!.Season!.StartDate <= today && x.Fixture.Season.EndDate >= today)
                .ToListAsync();
        }

        public async Task<List<PlayerGoalsStat>> GetTopScorersAsync()
        {
            var stats = await GetCurrentSeasonStatsAsync();
            return stats
                .GroupBy(x => new { x.PlayerId, x.Player!.Name })
                .Select(g => new PlayerGoalsStat(g.Key.PlayerId, g.Key.Name, g.Sum(x => x.Goals)))
                .OrderByDescending(x => x.Goals)
                .Take(5)
                .ToList();
        }

        public async Task<List<PlayerAssistsStat>> GetTopAssistsAsync()
        {
            var stats = await GetCurrentSeasonStatsAsync();
            return stats
                .GroupBy(x => new { x.PlayerId, x.Player!.Name })
                .Select(g => new PlayerAssistsStat(g.Key.PlayerId, g.Key.Name, g.Sum(x => x.Assists)))
                .OrderByDescending(x => x.Assists)
                .Take(5)
                .ToList();
        }

        public async Task<List<PlayerMotmStat>> GetTopMotmAsync()
        {
            var stats = await GetCurrentSeasonStatsAsync();

            return stats
                .GroupBy(x => new { x.PlayerId, x.Player!.Name })
                .Select(g => new PlayerMotmStat(g.Key.PlayerId, g.Key.Name, g.Count(x => x.ManOfTheMatch)))
                .OrderByDescending(x => x.Motm)
                .Take(5)
                .ToList();
        }

        public async Task<WinLossDrawResult> GetWinLossAsync(int teamId)
        {
            var today = DateTime.UtcNow;

            var fixtures = await _db.Fixtures
                .Where(f =>
                    f.IsPlayed &&
                    f.Season!.StartDate <= today &&
                    f.Season.EndDate >= today &&
                    (f.HomeTeamId == teamId || f.AwayTeamId == teamId))
                .ToListAsync();

            var wins = fixtures.Count(f =>
                (f.HomeTeamId == teamId && f.HomeScore > f.AwayScore) ||
                (f.AwayTeamId == teamId && f.AwayScore > f.HomeScore));

            var losses = fixtures.Count(f =>
                (f.HomeTeamId == teamId && f.HomeScore < f.AwayScore) ||
                (f.AwayTeamId == teamId && f.AwayScore < f.HomeScore));

            var draws = fixtures.Count(f => f.HomeScore == f.AwayScore);

            return new WinLossDrawResult(wins, losses, draws);
        }
    }
}
