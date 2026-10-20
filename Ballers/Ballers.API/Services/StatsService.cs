using Ballers.Models;
using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Shared;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface IStatsService
    {
        Task<List<PlayerGoalsDto>> GetTopScorersAsync();
        Task<List<PlayerAssistDto>> GetTopAssistsAsync();
        Task<List<PlayerMotmDto>> GetTopMotmAsync();
        Task<(List<PlayerGoalsDto> Scorers, List<PlayerAssistDto> Assists, List<PlayerMotmDto> Motm)> GetTopStatsSummaryAsync();
        Task<WinLossDrawResult> GetWinLossAsync(int teamId);
        Task<List<PlayerLeaderboardDto>> GetLeaderboardAsync(int? seasonId);
        Task<List<TeamStatsDto>> GetTeamStatsAsync(int? seasonId);
    }

    public class StatsService : IStatsService
    {
        private readonly ApplicationDbContext _db;

        public StatsService(ApplicationDbContext db) => _db = db;

        private async Task<List<FixturePlayerStat>> GetCurrentSeasonStatsAsync()
        {
            var activeSeason = await _db.Seasons.FirstOrDefaultAsync(s => s.IsActive);
            if (activeSeason == null) return new();

            return await _db.FixturePlayerStats
                .Include(x => x.Player)
                .Where(x => x.Fixture!.SeasonId == activeSeason.Id && x.Fixture.IsPlayed)
                .ToListAsync();
        }

        public async Task<(List<PlayerGoalsDto> Scorers, List<PlayerAssistDto> Assists, List<PlayerMotmDto> Motm)> GetTopStatsSummaryAsync()
        {
            var stats = await GetCurrentSeasonStatsAsync();
            var grouped = stats.GroupBy(x => new { x.PlayerId, x.Player!.Name }).ToList();

            var scorers = grouped
                .Select(g => new PlayerGoalsDto { PlayerId = g.Key.PlayerId, Name = g.Key.Name, Goals = g.Sum(x => x.Goals) })
                .Where(x => x.Goals > 0).OrderByDescending(x => x.Goals).Take(5).ToList();

            var assists = grouped
                .Select(g => new PlayerAssistDto { PlayerId = g.Key.PlayerId, Name = g.Key.Name, Assists = g.Sum(x => x.Assists) })
                .Where(x => x.Assists > 0).OrderByDescending(x => x.Assists).Take(5).ToList();

            var motm = grouped
                .Select(g => new PlayerMotmDto { PlayerId = g.Key.PlayerId, Name = g.Key.Name, Motm = g.Count(x => x.ManOfTheMatch) })
                .Where(x => x.Motm > 0).OrderByDescending(x => x.Motm).Take(5).ToList();

            return (scorers, assists, motm);
        }

        public async Task<List<PlayerGoalsDto>> GetTopScorersAsync()
        {
            var stats = await GetCurrentSeasonStatsAsync();
            return stats
                .GroupBy(x => new { x.PlayerId, x.Player!.Name })
                .Select(g => new PlayerGoalsDto { PlayerId = g.Key.PlayerId, Name = g.Key.Name, Goals = g.Sum(x => x.Goals) })
                .Where(x => x.Goals > 0)
                .OrderByDescending(x => x.Goals)
                .Take(5)
                .ToList();
        }

        public async Task<List<PlayerAssistDto>> GetTopAssistsAsync()
        {
            var stats = await GetCurrentSeasonStatsAsync();
            return stats
                .GroupBy(x => new { x.PlayerId, x.Player!.Name })
                .Select(g => new PlayerAssistDto { PlayerId = g.Key.PlayerId, Name = g.Key.Name, Assists = g.Sum(x => x.Assists) })
                .Where(x => x.Assists > 0)
                .OrderByDescending(x => x.Assists)
                .Take(5)
                .ToList();
        }

        public async Task<List<PlayerMotmDto>> GetTopMotmAsync()
        {
            var stats = await GetCurrentSeasonStatsAsync();
            return stats
                .GroupBy(x => new { x.PlayerId, x.Player!.Name })
                .Select(g => new PlayerMotmDto { PlayerId = g.Key.PlayerId, Name = g.Key.Name, Motm = g.Count(x => x.ManOfTheMatch) })
                .Where(x => x.Motm > 0)
                .OrderByDescending(x => x.Motm)
                .Take(5)
                .ToList();
        }

        public async Task<List<PlayerLeaderboardDto>> GetLeaderboardAsync(int? seasonId)
        {
            var query = _db.FixturePlayerStats.AsNoTracking()
                .Include(s => s.Player).ThenInclude(p => p!.Team)
                .Include(s => s.Fixture)
                .Where(s => s.Fixture!.IsPlayed);

            if (seasonId.HasValue)
                query = query.Where(s => s.Fixture!.SeasonId == seasonId.Value);

            var allStats = await query.ToListAsync();

            var playerIds = allStats.Select(s => s.PlayerId).Distinct().ToList();
            var cards = (await _db.PlayerCards.AsNoTracking()
                    .Where(c => playerIds.Contains(c.PlayerId))
                    .ToListAsync())
                .GroupBy(c => c.PlayerId)
                .ToDictionary(g => g.Key, g => g.First());

            // Read straight from the table, falling back to the model defaults,
            // so this service needs nothing beyond the context.
            var settings = await _db.LeagueSettings.AsNoTracking().FirstOrDefaultAsync() ?? new LeagueSetting();

            return allStats
                .GroupBy(s => s.PlayerId)
                .Select(g =>
                {
                    var player = g.First().Player!;
                    // Defenders count clean sheets alongside goalkeepers — the same
                    // measure that earns them their card tier and their clean sheet
                    // points.
                    var cleanSheets = g.Count(s =>
                        PlayerPositions.EarnedCleanSheet(player.Position, s.Fixture!, player.TeamId));

                    Ballers.Shared.PlayerAttributesDto? attributes = null;
                    string? tier = null;
                    if (cards.TryGetValue(player.Id, out var card))
                    {
                        attributes = new Ballers.Shared.PlayerAttributesDto
                        {
                            Finishing = card.Finishing,
                            Tackling = card.Tackling,
                            Passing = card.Passing,
                            Dribbling = card.Dribbling,
                            Pace = card.Pace,
                            Strength = card.Strength,
                            Overall = PlayerAttributes.ComputeOverall(card)
                        };
                        tier = PlayerAttributes.Tier(attributes.Overall,
                            settings.SilverOverallThreshold, settings.GoldOverallThreshold);
                    }

                    return new PlayerLeaderboardDto
                    {
                        PlayerId = player.Id,
                        Name = player.Name,
                        Team = player.Team?.Name ?? "",
                        Position = player.Position,
                        Appearances = g.Count(),
                        Goals = g.Sum(s => s.Goals),
                        Assists = g.Sum(s => s.Assists),
                        CleanSheets = cleanSheets,
                        YellowCards = g.Count(s => s.YellowCards),
                        RedCards = g.Count(s => s.RedCard),
                        TeamId = player.TeamId,
                        Number = player.Number,
                        Motm = g.Count(s => s.ManOfTheMatch),
                        OwnGoals = g.Sum(s => s.OwnGoals),
                        Attributes = attributes,
                        Tier = tier
                    };
                })
                .OrderByDescending(e => e.Goals)
                .ThenByDescending(e => e.Assists)
                .ThenByDescending(e => e.Appearances)
                .ToList();
        }

        public async Task<List<TeamStatsDto>> GetTeamStatsAsync(int? seasonId)
        {
            var fixtures = _db.Fixtures.AsNoTracking().Where(f => f.IsPlayed);
            if (seasonId.HasValue)
                fixtures = fixtures.Where(f => f.SeasonId == seasonId.Value);
            var played = await fixtures.ToListAsync();

            var cardsQuery = _db.FixturePlayerStats.AsNoTracking()
                .Where(s => s.Fixture!.IsPlayed && (s.YellowCards || s.RedCard));
            if (seasonId.HasValue)
                cardsQuery = cardsQuery.Where(s => s.Fixture!.SeasonId == seasonId.Value);
            var cards = await cardsQuery
                .Select(s => new { s.Player!.TeamId, s.YellowCards, s.RedCard })
                .ToListAsync();

            var teams = await _db.Teams.AsNoTracking().OrderBy(t => t.Name).ToListAsync();

            return teams.Select(team =>
            {
                var games = played
                    .Where(f => f.HomeTeamId == team.Id || f.AwayTeamId == team.Id)
                    .Select(f => f.HomeTeamId == team.Id
                        ? (For: f.HomeScore, Against: f.AwayScore)
                        : (For: f.AwayScore, Against: f.HomeScore))
                    .ToList();
                var teamCards = cards.Where(c => c.TeamId == team.Id).ToList();

                return new TeamStatsDto
                {
                    TeamId = team.Id,
                    Name = team.Name,
                    ProfileImageUrl = team.ProfileImageUrl,
                    HomeKitColour = team.HomeKitColour,
                    Played = games.Count,
                    Won = games.Count(g => g.For > g.Against),
                    Drawn = games.Count(g => g.For == g.Against),
                    Lost = games.Count(g => g.For < g.Against),
                    GoalsFor = games.Sum(g => g.For),
                    GoalsAgainst = games.Sum(g => g.Against),
                    CleanSheets = games.Count(g => g.Against == 0),
                    YellowCards = teamCards.Count(c => c.YellowCards),
                    RedCards = teamCards.Count(c => c.RedCard)
                };
            }).ToList();
        }

        public async Task<WinLossDrawResult> GetWinLossAsync(int teamId)
        {
            var today = DateTime.UtcNow;

            var fixtures = await _db.Fixtures.AsNoTracking()
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
