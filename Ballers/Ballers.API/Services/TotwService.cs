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
    }

    public class TotwService : ITotwService
    {
        private readonly ApplicationDbContext _db;
        private const int MaxOutfieldSlots = 10;

        public TotwService(ApplicationDbContext db) => _db = db;

        public async Task<TeamOfTheWeekDto?> GetCurrentAsync()
        {
            var season = await _db.Seasons.FirstOrDefaultAsync(s => s.IsActive);
            if (season == null) return null;

            var latestMatchNumber = await _db.FixturePlayerStats
                .Where(s => s.Fixture!.SeasonId == season.Id && s.ManOfTheMatch)
                .Select(s => s.Fixture!.MatchNumber)
                .OrderByDescending(n => n)
                .FirstOrDefaultAsync();

            if (latestMatchNumber == 0) return null;

            return await BuildAsync(season.Id, latestMatchNumber);
        }

        public async Task<List<TeamOfTheWeekDto>> GetHistoryAsync()
        {
            var season = await _db.Seasons.FirstOrDefaultAsync(s => s.IsActive);
            if (season == null) return new();

            var matchNumbers = await _db.FixturePlayerStats
                .Where(s => s.Fixture!.SeasonId == season.Id && s.ManOfTheMatch)
                .Select(s => s.Fixture!.MatchNumber)
                .Distinct()
                .OrderByDescending(n => n)
                .ToListAsync();

            var list = new List<TeamOfTheWeekDto>();
            foreach (var mn in matchNumbers)
            {
                var dto = await BuildAsync(season.Id, mn);
                if (dto != null) list.Add(dto);
            }
            return list;
        }

        private async Task<TeamOfTheWeekDto?> BuildAsync(int seasonId, int matchNumber)
        {
            var fixtures = await _db.Fixtures
                .Where(f => f.SeasonId == seasonId && f.MatchNumber == matchNumber)
                .ToListAsync();
            if (fixtures.Count == 0) return null;

            var fixtureIds = fixtures.Select(f => f.Id).ToList();
            var stats = await _db.FixturePlayerStats
                .Include(s => s.Player).ThenInclude(p => p!.Team)
                .Include(s => s.Fixture)
                .Where(s => fixtureIds.Contains(s.FixtureId))
                .ToListAsync();

            var weekFinalDate = fixtures.Max(f => f.WindowEnd);
            var weekComplete = fixtures.All(f => f.IsPlayed) || weekFinalDate <= DateTime.UtcNow;

            var outfield = stats
                .Where(s => s.ManOfTheMatch && s.Player != null && s.Player.Position != "GK")
                .OrderByDescending(s => s.Goals + s.Assists)
                .ThenByDescending(s => s.Goals)
                .ThenBy(s => s.Fixture!.Id)
                .Take(MaxOutfieldSlots)
                .Select(s => new TotwPlayerDto(
                    s.Player!.Id,
                    s.Player.Name,
                    s.Player.TeamId,
                    s.Player.Team?.Name ?? "",
                    s.Player.Position,
                    false,
                    s.Goals,
                    s.Assists,
                    null,
                    null,
                    s.Player.ProfileImageBase64))
                .ToList();

            var players = new List<TotwPlayerDto>();
            players.AddRange(outfield);

            if (weekComplete)
            {
                var bestGk = stats
                    .Where(s => s.Player != null && s.Player.Position == "GK")
                    .GroupBy(s => s.PlayerId)
                    .Select(g =>
                    {
                        var p = g.First().Player!;
                        var conceded = g.Sum(s =>
                            p.TeamId == s.Fixture!.HomeTeamId
                                ? s.Fixture.AwayScore
                                : s.Fixture.HomeScore);
                        var cleanSheets = g.Count(s =>
                            (p.TeamId == s.Fixture!.HomeTeamId && s.Fixture.AwayScore == 0)
                            || (p.TeamId == s.Fixture!.AwayTeamId && s.Fixture.HomeScore == 0));
                        return new
                        {
                            Player = p,
                            Conceded = conceded,
                            CleanSheets = cleanSheets,
                            Matches = g.Count(),
                            Goals = g.Sum(s => s.Goals),
                            Assists = g.Sum(s => s.Assists)
                        };
                    })
                    .OrderBy(x => x.Conceded)
                    .ThenByDescending(x => x.CleanSheets)
                    .ThenByDescending(x => x.Matches)
                    .ThenBy(x => x.Player.Id)
                    .FirstOrDefault();

                if (bestGk != null)
                {
                    players.Add(new TotwPlayerDto(
                        bestGk.Player.Id,
                        bestGk.Player.Name,
                        bestGk.Player.TeamId,
                        bestGk.Player.Team?.Name ?? "",
                        bestGk.Player.Position,
                        true,
                        bestGk.Goals,
                        bestGk.Assists,
                        bestGk.Conceded,
                        bestGk.CleanSheets,
                        bestGk.Player.ProfileImageBase64));
                }
            }

            if (players.Count == 0) return null;

            var weekDate = fixtures
                .Where(f => f.Kickoff.HasValue)
                .Select(f => f.Kickoff!.Value)
                .DefaultIfEmpty(weekFinalDate)
                .Max();

            return new TeamOfTheWeekDto(matchNumber, seasonId, matchNumber, weekDate, players);
        }
    }
}
