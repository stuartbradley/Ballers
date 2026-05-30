using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Shared;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface ITotwService
    {
        Task<TeamOfTheWeekDto?> GetCurrentAsync();
        Task<List<TeamOfTheWeekDto>> GetHistoryAsync();
        Task<TeamOfTheWeekDto?> GenerateAsync(int matchNumber);
        Task<bool> DeleteAsync(int id);
    }

    public class TotwService : ITotwService
    {
        private readonly ApplicationDbContext _db;
        private const int MaxOutfieldSlots = 10;

        public TotwService(ApplicationDbContext db) => _db = db;

        public async Task<TeamOfTheWeekDto?> GetCurrentAsync()
        {
            var totw = await BaseQuery()
                .OrderByDescending(t => t.MatchNumber)
                .FirstOrDefaultAsync();

            return totw == null ? null : Map(totw);
        }

        public async Task<List<TeamOfTheWeekDto>> GetHistoryAsync()
        {
            var totws = await BaseQuery()
                .OrderByDescending(t => t.MatchNumber)
                .ToListAsync();

            return totws.Select(Map).ToList();
        }

        public async Task<TeamOfTheWeekDto?> GenerateAsync(int matchNumber)
        {
            var season = await _db.Seasons.FirstOrDefaultAsync(s => s.IsActive);
            if (season == null) return null;

            var fixtures = await _db.Fixtures
                .Where(f => f.SeasonId == season.Id
                            && f.MatchNumber == matchNumber
                            && f.IsPlayed)
                .ToListAsync();

            if (fixtures.Count == 0) return null;
            var fixtureIds = fixtures.Select(f => f.Id).ToList();

            var stats = await _db.FixturePlayerStats
                .Include(s => s.Player).ThenInclude(p => p!.Team)
                .Include(s => s.Fixture)
                .Where(s => fixtureIds.Contains(s.FixtureId))
                .ToListAsync();

            var motmStats = stats
                .Where(s => s.ManOfTheMatch && s.Player != null && s.Player.Position != "GK")
                .OrderByDescending(s => s.Goals + s.Assists)
                .ThenByDescending(s => s.Goals)
                .ThenBy(s => s.Fixture!.MatchNumber)
                .Take(MaxOutfieldSlots)
                .ToList();

            var gkPlayers = stats
                .Where(s => s.Player != null && s.Player.Position == "GK")
                .GroupBy(s => s.PlayerId)
                .Select(g =>
                {
                    var player = g.First().Player!;
                    var conceded = g.Sum(s =>
                        player.TeamId == s.Fixture!.HomeTeamId
                            ? s.Fixture.AwayScore
                            : s.Fixture.HomeScore);
                    var cleanSheets = g.Count(s =>
                        (player.TeamId == s.Fixture!.HomeTeamId && s.Fixture.AwayScore == 0)
                        || (player.TeamId == s.Fixture!.AwayTeamId && s.Fixture.HomeScore == 0));
                    return new
                    {
                        Player = player,
                        Stats = g.ToList(),
                        Conceded = conceded,
                        CleanSheets = cleanSheets,
                        Matches = g.Count()
                    };
                })
                .OrderBy(x => x.Conceded)
                .ThenByDescending(x => x.CleanSheets)
                .ThenByDescending(x => x.Matches)
                .ThenBy(x => x.Player.Id)
                .ToList();

            var bestGk = gkPlayers.FirstOrDefault();

            using var tx = await _db.Database.BeginTransactionAsync();

            var existing = await _db.TeamOfTheWeeks
                .Include(t => t.Players)
                .FirstOrDefaultAsync(t => t.SeasonId == season.Id && t.MatchNumber == matchNumber);

            if (existing != null)
            {
                _db.TeamOfTheWeekPlayers.RemoveRange(existing.Players);
                _db.TeamOfTheWeeks.Remove(existing);
                await _db.SaveChangesAsync();
            }

            var totw = new TeamOfTheWeek
            {
                SeasonId = season.Id,
                MatchNumber = matchNumber,
                GeneratedAtUtc = DateTime.UtcNow
            };

            foreach (var s in motmStats)
            {
                totw.Players.Add(new TeamOfTheWeekPlayer
                {
                    PlayerId = s.PlayerId,
                    IsGoalkeeper = false,
                    Goals = s.Goals,
                    Assists = s.Assists
                });
            }

            if (bestGk != null)
            {
                totw.Players.Add(new TeamOfTheWeekPlayer
                {
                    PlayerId = bestGk.Player.Id,
                    IsGoalkeeper = true,
                    Goals = bestGk.Stats.Sum(s => s.Goals),
                    Assists = bestGk.Stats.Sum(s => s.Assists),
                    GoalsConceded = bestGk.Conceded,
                    CleanSheets = bestGk.CleanSheets
                });
            }

            _db.TeamOfTheWeeks.Add(totw);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            var hydrated = await BaseQuery().FirstAsync(t => t.Id == totw.Id);
            return Map(hydrated);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var totw = await _db.TeamOfTheWeeks
                .Include(t => t.Players)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (totw == null) return false;

            _db.TeamOfTheWeekPlayers.RemoveRange(totw.Players);
            _db.TeamOfTheWeeks.Remove(totw);
            await _db.SaveChangesAsync();
            return true;
        }

        private IQueryable<TeamOfTheWeek> BaseQuery() =>
            _db.TeamOfTheWeeks
                .Include(t => t.Players).ThenInclude(p => p.Player).ThenInclude(pp => pp!.Team);

        private static TeamOfTheWeekDto Map(TeamOfTheWeek t) =>
            new(
                t.Id,
                t.SeasonId,
                t.MatchNumber,
                t.GeneratedAtUtc,
                t.Players
                    .OrderByDescending(p => p.IsGoalkeeper)
                    .ThenByDescending(p => p.Goals + p.Assists)
                    .ThenBy(p => p.Player?.Name)
                    .Select(p => new TotwPlayerDto(
                        p.PlayerId,
                        p.Player?.Name ?? "",
                        p.Player?.TeamId ?? 0,
                        p.Player?.Team?.Name ?? "",
                        p.Player?.Position ?? "",
                        p.IsGoalkeeper,
                        p.Goals,
                        p.Assists,
                        p.GoalsConceded,
                        p.CleanSheets,
                        p.Player?.ProfileImageBase64))
                    .ToList());
    }
}
