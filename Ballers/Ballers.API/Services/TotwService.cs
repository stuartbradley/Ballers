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
        Task<List<TotwAppearanceDto>> GetAppearancesAsync();
    }

    public class TotwService : ITotwService
    {
        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly IPlayerCardService _cards;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        public TotwService(ApplicationDbContext db, IMemoryCache cache, IPlayerCardService cards)
        {
            _db = db;
            _cache = cache;
            _cards = cards;
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

                // Each week is dated by the start of its own window. Using the
                // latest kickoff let a fixture rearranged outside the window
                // relabel the entire match week.
                var dated = await _db.Fixtures
                    .AsNoTracking()
                    .Where(f => f.SeasonId == season.Value && motmSet.Contains(f.WindowStart))
                    .Select(f => f.WindowStart)
                    .Distinct()
                    .ToListAsync();

                return dated
                    .Select(windowStart => new TotwWeekRefDto(allWeeks.IndexOf(windowStart) + 1, windowStart))
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
            // Only the ids are needed — the rest of the fixture columns became
            // redundant when the goalkeeper slot and the kickoff-derived week
            // date were dropped.
            var fixtureIds = await _db.Fixtures
                .AsNoTracking()
                .Where(f => f.SeasonId == seasonId && f.WindowStart == weekStart)
                .Select(f => f.Id)
                .ToListAsync();

            if (fixtureIds.Count == 0) return null;

            var motmRows = await _db.FixturePlayerStats
                .AsNoTracking()
                .Where(s => fixtureIds.Contains(s.FixtureId) && s.ManOfTheMatch)
                .Select(s => new
                {
                    PlayerId = s.PlayerId,
                    Name = s.Player!.Name,
                    TeamId = s.Player.TeamId,
                    TeamName = s.Player.Team!.Name,
                    Position = s.Player.Position,
                    // The photo's length, not the photo: the card links to it.
                    PhotoLength = s.Player.ProfileImageBase64 != null ? s.Player.ProfileImageBase64.Length : (int?)null,
                    s.Goals,
                    s.Assists,
                    s.FixtureId,
                    s.Fixture!.HomeTeamId,
                    s.Fixture.AwayTeamId,
                    HomeName = s.Fixture.HomeTeam!.Name,
                    AwayName = s.Fixture.AwayTeam!.Name,
                    s.Fixture.HomeScore,
                    s.Fixture.AwayScore
                })
                .ToListAsync();

            // The season so far for everyone in the team, for the back of their
            // card. One query for all of them, totalled here.
            var playerIds = motmRows.Select(r => r.PlayerId).Distinct().ToList();
            var seasonRows = await _db.FixturePlayerStats
                .AsNoTracking()
                .Where(s => playerIds.Contains(s.PlayerId)
                         && s.Fixture!.SeasonId == seasonId
                         && s.Fixture.IsPlayed)
                .Select(s => new
                {
                    s.PlayerId,
                    s.Goals,
                    s.Assists,
                    s.Fixture!.HomeTeamId,
                    s.Fixture.AwayTeamId,
                    s.Fixture.HomeScore,
                    s.Fixture.AwayScore
                })
                .ToListAsync();

            var teamOf = motmRows.GroupBy(r => r.PlayerId).ToDictionary(g => g.Key, g => g.First().TeamId);

            // A clean sheet belongs to the side: the player's team was one of the
            // two and the other scored nothing. Same rule as
            // PlayerPositions.IsCleanSheetFor, written out because these rows are
            // projections rather than Fixture entities.
            static int? Conceded(int teamId, int home, int away, int homeScore, int awayScore)
                => teamId == home ? awayScore : teamId == away ? homeScore : null;

            var season = seasonRows
                .GroupBy(r => r.PlayerId)
                .ToDictionary(g => g.Key, g => (
                    Goals: g.Sum(r => r.Goals),
                    Assists: g.Sum(r => r.Assists),
                    CleanSheets: g.Count(r => Conceded(teamOf[g.Key], r.HomeTeamId, r.AwayTeamId, r.HomeScore, r.AwayScore) == 0)));

            var cards = await _cards.GetPublicCardsAsync(playerIds);

            // How many weeks each of them had made the team by this one.
            var appearances = (await GetMotmWeeksAsync(seasonId))
                .Where(x => playerIds.Contains(x.PlayerId) && x.WeekStart <= weekStart)
                .GroupBy(x => x.PlayerId)
                .ToDictionary(g => g.Key, g => g.Count());

            // Team of the Week is every Man of the Match winner from the week's
            // games — no cap and no goalkeeper exclusion. Both used to silently
            // drop winners: keepers outright, and anyone past the tenth slot
            // once the sort below put low-scoring winners last.
            var players = motmRows
                .OrderByDescending(s => s.Goals + s.Assists)
                .ThenByDescending(s => s.Goals)
                .ThenBy(s => s.FixtureId)
                .Select(s =>
                {
                    var conceded = Conceded(s.TeamId, s.HomeTeamId, s.AwayTeamId, s.HomeScore, s.AwayScore);
                    season.TryGetValue(s.PlayerId, out var totals);
                    var hasCard = cards.TryGetValue(s.PlayerId, out var card);
                    var isHome = s.TeamId == s.HomeTeamId;
                    var isAway = s.TeamId == s.AwayTeamId;
                    return new TotwPlayerDto(
                        s.PlayerId, s.Name, s.TeamId, s.TeamName, s.Position,
                        s.Position == "GK", s.Goals, s.Assists,
                        conceded, conceded == null ? null : (conceded == 0 ? 1 : 0),
                        StoredImages.PlayerUrl(s.PlayerId, s.PhotoLength),
                        totals.Goals, totals.Assists, totals.CleanSheets,
                        hasCard ? card.Attributes : null,
                        hasCard ? card.Tier : null)
                    {
                        // A player who has since moved club matches neither side;
                        // they get no result rather than a wrong one.
                        OpponentName = isHome ? s.AwayName : isAway ? s.HomeName : null,
                        TeamScore = isHome ? s.HomeScore : isAway ? s.AwayScore : null,
                        OpponentScore = isHome ? s.AwayScore : isAway ? s.HomeScore : null,
                        TotwAppearances = appearances.GetValueOrDefault(s.PlayerId)
                    };
                })
                .ToList();

            if (players.Count == 0) return null;

            // The week is labelled by when it opens, not by its latest kickoff:
            // a single fixture rearranged months ahead used to drag the whole
            // week's date with it.
            return new TeamOfTheWeekDto(weekIndex, seasonId, weekIndex, weekStart, players)
            {
                Summary = await BuildSummaryAsync(fixtureIds)
            };
        }

        private async Task<TotwWeekSummaryDto> BuildSummaryAsync(List<int> fixtureIds)
        {
            var played = await _db.Fixtures
                .AsNoTracking()
                .Where(f => fixtureIds.Contains(f.Id) && f.IsPlayed)
                .Select(f => new { f.Id, f.HomeScore, f.AwayScore })
                .ToListAsync();
            var playedIds = played.Select(f => f.Id).ToList();

            var top = (await _db.FixturePlayerStats
                    .AsNoTracking()
                    .Where(s => playedIds.Contains(s.FixtureId) && s.Goals > 0)
                    .Select(s => new { s.PlayerId, s.Goals, s.Player!.Name, TeamName = s.Player.Team!.Name })
                    .ToListAsync())
                .GroupBy(s => s.PlayerId)
                .Select(g => (g.First().Name, g.First().TeamName, Goals: g.Sum(s => s.Goals)))
                .OrderByDescending(x => x.Goals)
                .ThenBy(x => x.Name)
                .FirstOrDefault();

            return new TotwWeekSummaryDto(
                played.Count,
                played.Sum(f => f.HomeScore + f.AwayScore),
                top.Name, top.TeamName, top.Goals);
        }

        // Every (player, week) in the season where the player was Man of the
        // Match, once per week however many games they won it in.
        private async Task<List<(int PlayerId, DateTime WeekStart)>> GetMotmWeeksAsync(int seasonId)
        {
            var rows = await _db.FixturePlayerStats
                .AsNoTracking()
                .Where(s => s.Fixture!.SeasonId == seasonId && s.ManOfTheMatch)
                .Select(s => new { s.PlayerId, s.Fixture!.WindowStart })
                .Distinct()
                .ToListAsync();
            return rows.Select(r => (r.PlayerId, r.WindowStart)).ToList();
        }

        public async Task<List<TotwAppearanceDto>> GetAppearancesAsync()
        {
            return await _cache.GetOrCreateAsync("totw:appearances", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;

                var season = await GetActiveSeasonIdAsync();
                if (season == null) return new List<TotwAppearanceDto>();

                var counts = (await GetMotmWeeksAsync(season.Value))
                    .GroupBy(x => x.PlayerId)
                    .ToDictionary(g => g.Key, g => g.Count());
                if (counts.Count == 0) return new List<TotwAppearanceDto>();

                var ids = counts.Keys.ToList();
                var players = await _db.Players
                    .AsNoTracking()
                    .Where(p => ids.Contains(p.Id))
                    .Select(p => new { p.Id, p.Name, TeamName = p.Team!.Name, p.Position })
                    .ToListAsync();
                var cards = await _cards.GetPublicCardsAsync(ids);

                return players
                    .Select(p =>
                    {
                        var hasCard = cards.TryGetValue(p.Id, out var card);
                        return new TotwAppearanceDto(p.Id, p.Name, p.TeamName, p.Position, counts[p.Id],
                            hasCard ? card.Attributes : null, hasCard ? card.Tier : null);
                    })
                    .OrderByDescending(a => a.Count)
                    .ThenBy(a => a.Name)
                    .ToList();
            }) ?? new List<TotwAppearanceDto>();
        }
    }
}
