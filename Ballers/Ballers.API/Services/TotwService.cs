using Ballers.API.Data;
using Ballers.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Ballers.API.Services
{
    public interface ITotwService
    {
        Task<TeamOfTheWeekDto?> GetCurrentAsync();
        Task<List<TeamOfTheWeekDto>> GetHistoryAsync();
        Task<TeamOfTheWeekDto?> GetWeekAsync(int weekIndex);
        Task<List<TotwWeekRefDto>> GetWeeksAsync();
    }

    public class TotwService : ITotwService
    {
        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;
        private const int MaxOutfieldSlots = 10;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        public TotwService(ApplicationDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<TeamOfTheWeekDto?> GetCurrentAsync()
        {
            return await _cache.GetOrCreateAsync($"totw:current", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;

                var season = await GetActiveSeasonIdAsync();
                if (season == null) return null;

                var (allWeeks, weeksWithMotm) = await GetWeekListsAsync(season.Value);
                if (weeksWithMotm.Count == 0) return null;

                var latest = weeksWithMotm.Max();
                var idx = allWeeks.IndexOf(latest) + 1;
                return await BuildAsync(season.Value, latest, idx);
            });
        }

        public async Task<List<TeamOfTheWeekDto>> GetHistoryAsync()
        {
            var season = await GetActiveSeasonIdAsync();
            if (season == null) return new();

            var (allWeeks, weeksWithMotm) = await GetWeekListsAsync(season.Value);
            var list = new List<TeamOfTheWeekDto>();
            foreach (var ws in weeksWithMotm.OrderByDescending(d => d))
            {
                var idx = allWeeks.IndexOf(ws) + 1;
                var dto = await BuildAsync(season.Value, ws, idx);
                if (dto != null) list.Add(dto);
            }
            return list;
        }

        public async Task<TeamOfTheWeekDto?> GetWeekAsync(int weekIndex)
        {
            return await _cache.GetOrCreateAsync($"totw:week:{weekIndex}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;

                var season = await GetActiveSeasonIdAsync();
                if (season == null) return null;

                var (allWeeks, _) = await GetWeekListsAsync(season.Value);
                if (weekIndex < 1 || weekIndex > allWeeks.Count) return null;

                return await BuildAsync(season.Value, allWeeks[weekIndex - 1], weekIndex);
            });
        }

        public async Task<List<TotwWeekRefDto>> GetWeeksAsync()
        {
            return await _cache.GetOrCreateAsync("totw:weeks", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;

                var season = await GetActiveSeasonIdAsync();
                if (season == null) return new List<TotwWeekRefDto>();

                var (allWeeks, weeksWithMotm) = await GetWeekListsAsync(season.Value);
                var motmSet = weeksWithMotm.ToHashSet();

                var dated = await _db.Fixtures
                    .AsNoTracking()
                    .Where(f => f.SeasonId == season.Value && motmSet.Contains(f.WindowStart))
                    .GroupBy(f => f.WindowStart)
                    .Select(g => new
                    {
                        WindowStart = g.Key,
                        Date = g.Max(f => f.Kickoff) ?? g.Max(f => f.WindowEnd)
                    })
                    .ToListAsync();

                return dated
                    .Select(d => new TotwWeekRefDto(allWeeks.IndexOf(d.WindowStart) + 1, d.Date))
                    .OrderByDescending(w => w.MatchNumber)
                    .ToList();
            }) ?? new List<TotwWeekRefDto>();
        }

        private async Task<int?> GetActiveSeasonIdAsync()
        {
            return await _cache.GetOrCreateAsync("totw:active-season-id", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await _db.Seasons
                    .AsNoTracking()
                    .Where(s => s.IsActive)
                    .Select(s => (int?)s.Id)
                    .FirstOrDefaultAsync();
            });
        }

        private async Task<(List<DateTime> AllWeeks, List<DateTime> WithMotm)> GetWeekListsAsync(int seasonId)
        {
            var allWeeks = await _db.Fixtures
                .AsNoTracking()
                .Where(f => f.SeasonId == seasonId)
                .Select(f => f.WindowStart)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            var withMotm = await _db.FixturePlayerStats
                .AsNoTracking()
                .Where(s => s.Fixture!.SeasonId == seasonId && s.ManOfTheMatch)
                .Select(s => s.Fixture!.WindowStart)
                .Distinct()
                .ToListAsync();

            return (allWeeks, withMotm);
        }

        private async Task<TeamOfTheWeekDto?> BuildAsync(int seasonId, DateTime weekStart, int weekIndex)
        {
            var fixtures = await _db.Fixtures
                .AsNoTracking()
                .Where(f => f.SeasonId == seasonId && f.WindowStart == weekStart)
                .Select(f => new
                {
                    f.Id,
                    f.HomeTeamId,
                    f.AwayTeamId,
                    f.HomeScore,
                    f.AwayScore,
                    f.IsPlayed,
                    f.Kickoff,
                    f.WindowEnd
                })
                .ToListAsync();

            if (fixtures.Count == 0) return null;

            var fixtureIds = fixtures.Select(f => f.Id).ToList();
            var weekFinalDate = fixtures.Max(f => f.WindowEnd);
            var weekComplete = fixtures.All(f => f.IsPlayed) || weekFinalDate <= DateTime.UtcNow;

            var motmRows = await _db.FixturePlayerStats
                .AsNoTracking()
                .Where(s => fixtureIds.Contains(s.FixtureId)
                            && s.ManOfTheMatch
                            && s.Player!.Position != "GK")
                .Select(s => new
                {
                    PlayerId = s.PlayerId,
                    Name = s.Player!.Name,
                    TeamId = s.Player.TeamId,
                    TeamName = s.Player.Team!.Name,
                    Position = s.Player.Position,
                    ProfileImageBase64 = s.Player.ProfileImageBase64,
                    s.Goals,
                    s.Assists,
                    s.FixtureId
                })
                .ToListAsync();

            var outfield = motmRows
                .OrderByDescending(s => s.Goals + s.Assists)
                .ThenByDescending(s => s.Goals)
                .ThenBy(s => s.FixtureId)
                .Take(MaxOutfieldSlots)
                .Select(s => new TotwPlayerDto(
                    s.PlayerId, s.Name, s.TeamId, s.TeamName, s.Position,
                    false, s.Goals, s.Assists, null, null, s.ProfileImageBase64))
                .ToList();

            var players = new List<TotwPlayerDto>(outfield);

            if (weekComplete)
            {
                var gkRows = await _db.FixturePlayerStats
                    .AsNoTracking()
                    .Where(s => fixtureIds.Contains(s.FixtureId) && s.Player!.Position == "GK")
                    .Select(s => new
                    {
                        s.PlayerId,
                        Name = s.Player!.Name,
                        TeamId = s.Player.TeamId,
                        TeamName = s.Player.Team!.Name,
                        Position = s.Player.Position,
                        ProfileImageBase64 = s.Player.ProfileImageBase64,
                        s.Goals,
                        s.Assists,
                        s.FixtureId
                    })
                    .ToListAsync();

                var fixtureScoreMap = fixtures.ToDictionary(f => f.Id,
                    f => new { f.HomeTeamId, f.AwayTeamId, f.HomeScore, f.AwayScore });

                var bestGk = gkRows
                    .GroupBy(s => new { s.PlayerId, s.Name, s.TeamId, s.TeamName, s.Position, s.ProfileImageBase64 })
                    .Select(g =>
                    {
                        int conceded = 0, clean = 0;
                        foreach (var r in g)
                        {
                            var fx = fixtureScoreMap[r.FixtureId];
                            int opp = g.Key.TeamId == fx.HomeTeamId ? fx.AwayScore : fx.HomeScore;
                            conceded += opp;
                            if (opp == 0) clean++;
                        }
                        return new
                        {
                            g.Key.PlayerId,
                            g.Key.Name,
                            g.Key.TeamId,
                            g.Key.TeamName,
                            g.Key.Position,
                            g.Key.ProfileImageBase64,
                            Conceded = conceded,
                            CleanSheets = clean,
                            Matches = g.Count(),
                            Goals = g.Sum(x => x.Goals),
                            Assists = g.Sum(x => x.Assists)
                        };
                    })
                    .OrderBy(x => x.Conceded)
                    .ThenByDescending(x => x.CleanSheets)
                    .ThenByDescending(x => x.Matches)
                    .ThenBy(x => x.PlayerId)
                    .FirstOrDefault();

                if (bestGk != null)
                {
                    players.Add(new TotwPlayerDto(
                        bestGk.PlayerId, bestGk.Name, bestGk.TeamId, bestGk.TeamName, bestGk.Position,
                        true, bestGk.Goals, bestGk.Assists, bestGk.Conceded, bestGk.CleanSheets, bestGk.ProfileImageBase64));
                }
            }

            if (players.Count == 0) return null;

            var weekDate = fixtures
                .Where(f => f.Kickoff.HasValue)
                .Select(f => f.Kickoff!.Value)
                .DefaultIfEmpty(weekFinalDate)
                .Max();

            return new TeamOfTheWeekDto(weekIndex, seasonId, weekIndex, weekDate, players);
        }
    }
}
