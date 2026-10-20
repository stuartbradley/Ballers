using Ballers.Models;
using Ballers.API.Data;
using Ballers.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Ballers.API.Services
{
    public interface ITeamService
    {
        Task<List<TeamDto>> GetTeamsAsync();

        /// <summary>
        /// Every club with its current-season record, form and squad rating, for
        /// the teams page and the club page.
        /// </summary>
        Task<List<TeamOverviewDto>> GetOverviewAsync();
    }

    public class TeamService : ITeamService
    {
        private const int FormLength = 5;

        // The overview rebuilds the whole league table and every squad's
        // rating, and the Teams page, each club page and every manager's HQ
        // ask for it. A minute's staleness is fine, as for Team of the Week.
        private static readonly TimeSpan OverviewCacheTtl = TimeSpan.FromSeconds(60);
        private const string OverviewCacheKey = "teams:overview";

        private readonly ApplicationDbContext _db;
        private readonly IFixtureService _fixtures;
        private readonly ILeagueSettingsService _settings;
        private readonly IMemoryCache _cache;

        public TeamService(ApplicationDbContext db, IFixtureService fixtures, ILeagueSettingsService settings, IMemoryCache cache)
        {
            _db = db;
            _fixtures = fixtures;
            _settings = settings;
            _cache = cache;
        }

        public async Task<List<TeamDto>> GetTeamsAsync() =>
            await _db.Teams
                .OrderBy(t => t.Name)
                .Select(t => new TeamDto { Id = t.Id, Name = t.Name, ProfileImageUrl = t.ProfileImageUrl, ManagerName = t.ManagerName })
                .ToListAsync();

        public async Task<List<TeamOverviewDto>> GetOverviewAsync() =>
            await _cache.GetOrCreateAsync(OverviewCacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = OverviewCacheTtl;
                return BuildOverviewAsync();
            }) ?? new List<TeamOverviewDto>();

        private async Task<List<TeamOverviewDto>> BuildOverviewAsync()
        {
            var teams = await _db.Teams.AsNoTracking().OrderBy(t => t.Name).ToListAsync();

            // The active season, or failing that the latest one.
            var seasonId = await _db.Seasons.Where(s => s.IsActive).Select(s => (int?)s.Id).FirstOrDefaultAsync()
                        ?? await _db.Seasons.OrderByDescending(s => s.SeasonNumber).Select(s => (int?)s.Id).FirstOrDefaultAsync();

            // The league table is the one the site already shows, keyed by name
            // because that is how its rows identify a team.
            var table = seasonId == null
                ? new Dictionary<string, Ballers.Models.LeagueTableRowDto>()
                : (await _fixtures.GetTableAsync(seasonId.Value)).ToDictionary(r => r.Team);

            var played = seasonId == null
                ? new List<Models.Fixture>()
                : await _db.Fixtures.AsNoTracking()
                    .Where(f => f.SeasonId == seasonId && f.IsPlayed)
                    .ToListAsync();

            // Cards for active players: each one's overall, by team.
            var settings = await _settings.GetSettingsEntityAsync();
            var cards = await _db.PlayerCards.AsNoTracking()
                .Where(c => c.Player!.IsActive)
                .Select(c => new
                {
                    c.Player!.TeamId,
                    c.PlayerId,
                    c.Player.Name,
                    c.Player.Number,
                    c.Player.Position,
                    Total = c.Finishing + c.Tackling + c.Passing + c.Dribbling + c.Pace + c.Strength
                })
                .ToListAsync();

            var squadSizes = await _db.Players.AsNoTracking()
                .Where(p => p.IsActive)
                .GroupBy(p => p.TeamId)
                .Select(g => new { TeamId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.TeamId, g => g.Count);

            string Tier(int overall) => PlayerAttributes.Tier(
                overall, settings.SilverOverallThreshold, settings.GoldOverallThreshold);

            return teams.Select(team =>
            {
                var dto = new TeamOverviewDto
                {
                    Id = team.Id,
                    Name = team.Name,
                    ProfileImageUrl = team.ProfileImageUrl,
                    ManagerName = team.ManagerName,
                    HomeKitColour = team.HomeKitColour,
                    SquadSize = squadSizes.GetValueOrDefault(team.Id)
                };

                if (table.TryGetValue(team.Name, out var row))
                {
                    dto.Position = row.Position;
                    dto.Played = row.Played;
                    dto.Won = row.Won;
                    dto.Drawn = row.Drawn;
                    dto.Lost = row.Lost;
                    dto.Points = row.Points;
                    dto.GoalDifference = row.GoalDifference;
                }

                // Same order the fixtures run in: by kickoff, then by the week
                // they were scheduled for when no kickoff was set.
                dto.Form = played
                    .Where(f => f.HomeTeamId == team.Id || f.AwayTeamId == team.Id)
                    .OrderBy(f => f.Kickoff ?? f.WindowStart)
                    .ThenBy(f => f.MatchNumber)
                    .TakeLast(FormLength)
                    .Select(f =>
                    {
                        var scored = f.HomeTeamId == team.Id ? f.HomeScore : f.AwayScore;
                        var conceded = f.HomeTeamId == team.Id ? f.AwayScore : f.HomeScore;
                        return scored > conceded ? "W" : scored == conceded ? "D" : "L";
                    })
                    .ToList();

                // Same rounding as PlayerAttributes.ComputeOverall.
                var squad = cards
                    .Where(c => c.TeamId == team.Id)
                    .Select(c => new { c.PlayerId, c.Name, c.Number, c.Position, Overall = c.Total / PlayerAttributes.Count })
                    .ToList();

                dto.CardCount = squad.Count;
                if (squad.Count > 0)
                {
                    dto.SquadOverall = (int)Math.Round(squad.Average(c => c.Overall));
                    dto.SquadTier = Tier(dto.SquadOverall.Value);

                    var top = squad.OrderByDescending(c => c.Overall).ThenBy(c => c.Name).First();
                    dto.TopPlayer = new TeamTopPlayerDto
                    {
                        Id = top.PlayerId,
                        Name = top.Name,
                        Number = top.Number,
                        Position = top.Position,
                        Overall = top.Overall,
                        Tier = Tier(top.Overall)
                    };
                }

                return dto;
            }).ToList();
        }
    }
}
