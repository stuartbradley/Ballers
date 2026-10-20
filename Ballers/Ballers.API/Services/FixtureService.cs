using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Models;
using Ballers.Shared;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public record ImportFixturesResult(int Created, List<string> Errors);

    public interface IFixtureService
    {
        Task<FixtureDetailsDto?> GetByIdAsync(int id);
        Task<List<FixtureListItemDto>> GetForUserAsync(bool isAdmin, int? teamId, int? seasonId = null);
        Task<List<LeagueTableRowDto>> GetTableAsync(int seasonId);
        Task<FixtureWeekDto?> GetCurrentWeekAsync();
        Task<List<FixtureWeekDto>> GetAllWeeksAsync(int? seasonId = null);
        Task<List<PlayerDto>?> GetPlayersAsync(int fixtureId, bool isAdmin, int? userTeamId, int? requestedTeamId);
        Task<List<FixtureSquadPlayerDto>> GetSquadAsync(int fixtureId);
        Task UpdateSquadAsync(int fixtureId, List<int> playerIds, int? teamId);
        Task<List<PlayerStatDto>> GetStatsAsync(int fixtureId);
        Task SubmitStatsAsync(int fixtureId, List<PlayerStatDto> stats, int? teamId);
        Task<bool> UpdateScheduleAsync(int fixtureId, string? location, string? postcode, DateTime? kickoff);
        Task AssignRefereeAsync(int fixtureId, int? refereeId);
        Task<string?> SwapHomeAwayAsync(int fixtureId, DateTime now, bool byAdmin = false);
        Task GenerateFixturesAsync(List<int> teamIds, DateTime startDate);
        Task<ImportFixturesResult> ImportFixturesAsync(Stream csv, int seasonNumber, DateTime startDate, bool makeActive);
        Task<List<OpponentPlayerStatDto>> GetOpponentStatsAsync(int fixtureId, int opponentTeamId);
        Task<List<HeadToHeadResultDto>> GetHeadToHeadAsync(int homeTeamId, int awayTeamId, int excludeFixtureId);
        Task SaveCaptaincyAsync(int fixtureId, int teamId, int? captainId, int? viceId);
        Task<TrueMotmDto?> GetTrueMotmAsync(int fixtureId);
        Task<bool> SetTrueMotmAsync(int fixtureId, int? playerId);
        Task<List<TrueMotmRowDto>> GetTrueMotmLogAsync();
    }

    public partial class FixtureService : IFixtureService
    {
        private readonly ApplicationDbContext _db;
        private readonly IKnockoutService _knockout;

        public FixtureService(ApplicationDbContext db, IKnockoutService knockout)
        {
            _db = db;
            _knockout = knockout;
        }

        public async Task<FixtureDetailsDto?> GetByIdAsync(int id)
        {
            var f = await _db.Fixtures.AsNoTracking()
                .Include(x => x.HomeTeam)
                .Include(x => x.AwayTeam)
                .Include(x => x.Referee)
                .Include(x => x.HomeCaptain)
                .Include(x => x.HomeViceCaptain)
                .Include(x => x.AwayCaptain)
                .Include(x => x.AwayViceCaptain)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (f == null) return null;

            return new FixtureDetailsDto
            {
                Id = f.Id,
                HomeTeam = f.HomeTeam!.Name,
                AwayTeam = f.AwayTeam!.Name,
                HomeTeamId = f.HomeTeamId,
                AwayTeamId = f.AwayTeamId,
                Kickoff = f.Kickoff,
                Location = f.Location,
                Postcode = f.Postcode,
                Week = f.MatchNumber,
                IsPlayed = f.IsPlayed,
                HomeScore = f.HomeScore,
                AwayScore = f.AwayScore,
                RefereeId = f.RefereeId,
                RefereeName = f.Referee?.Name,
                WindowStart = f.WindowStart,
                WindowEnd = f.WindowEnd,
                IsKnockout = f.IsKnockout,
                KnockoutTournament = f.KnockoutTournament ?? "",
                KnockoutRound = f.KnockoutRound ?? "",
                HomeCaptainId      = f.HomeCaptainId,
                HomeCaptainName    = f.HomeCaptain?.Name,
                HomeViceCaptainId  = f.HomeViceCaptainId,
                HomeViceCaptainName = f.HomeViceCaptain?.Name,
                AwayCaptainId      = f.AwayCaptainId,
                AwayCaptainName    = f.AwayCaptain?.Name,
                AwayViceCaptainId  = f.AwayViceCaptainId,
                AwayViceCaptainName = f.AwayViceCaptain?.Name,
                HomeStatsSubmitted = f.HomeStatsSubmitted,
                AwayStatsSubmitted = f.AwayStatsSubmitted,
            };
        }

        public async Task<List<FixtureListItemDto>> GetForUserAsync(bool isAdmin, int? teamId, int? seasonId = null)
        {
            var query = _db.Fixtures
                .AsNoTracking()
                .Include(f => f.HomeTeam)
                .Include(f => f.AwayTeam)
                .Include(f => f.Season)
                .AsQueryable();

            if (seasonId.HasValue)
                query = query.Where(f => f.SeasonId == seasonId.Value);

            if (!isAdmin)
            {
                if (teamId == null) return new List<FixtureListItemDto>();
                query = query.Where(f => f.HomeTeamId == teamId || f.AwayTeamId == teamId);
            }

            var fixtures = await query
                .OrderBy(f => f.IsKnockout)
                .ThenBy(f => f.MatchNumber)
                .ThenBy(f => f.Kickoff)
                .Select(f => new FixtureListItemDto
                {
                    Id = f.Id,
                    HomeTeam = f.HomeTeam!.Name,
                    AwayTeam = f.AwayTeam!.Name,
                    Week = f.MatchNumber,
                    Kickoff = f.Kickoff,
                    Location = f.Location,
                    IsPlayed = f.IsPlayed,
                    IsHomeTeamManager = isAdmin || teamId == f.HomeTeamId,
                    IsKnockout = f.IsKnockout,
                    KnockoutTournament = f.KnockoutTournament ?? "",
                    KnockoutRound = f.KnockoutRound ?? "",
                    HomeTeamId = f.HomeTeamId,
                    AwayTeamId = f.AwayTeamId,
                    HomeScore = f.HomeScore,
                    AwayScore = f.AwayScore,
                    Postcode = f.Postcode,
                    RefereeName = f.Referee != null ? f.Referee.Name : null,
                    WindowStart = f.WindowStart,
                    WindowEnd = f.WindowEnd,
                    SeasonId = f.SeasonId,
                    SeasonNumber = f.Season != null ? f.Season.SeasonNumber : 0,
                    SeasonIsActive = f.Season != null && f.Season.IsActive,
                    HomeStatsSubmitted = f.HomeStatsSubmitted,
                    AwayStatsSubmitted = f.AwayStatsSubmitted,
                    ManagerCaptainSet = teamId != null &&
                        ((f.HomeTeamId == teamId && f.HomeCaptainId != null) || (f.AwayTeamId == teamId && f.AwayCaptainId != null))
                })
                .ToListAsync();

            if (teamId.HasValue && fixtures.Count > 0)
            {
                var teamPlayerIds = await _db.Players.AsNoTracking()
                    .Where(p => p.TeamId == teamId.Value)
                    .Select(p => p.Id)
                    .ToListAsync();

                var submittedFixtureIds = await _db.FixturePlayerStats.AsNoTracking()
                    .Where(s => teamPlayerIds.Contains(s.PlayerId))
                    .Select(s => s.FixtureId)
                    .Distinct()
                    .ToListAsync();

                // How many of ours are picked for each fixture.
                var squadCounts = await _db.FixturePlayers.AsNoTracking()
                    .Where(fp => teamPlayerIds.Contains(fp.PlayerId))
                    .GroupBy(fp => fp.FixtureId)
                    .Select(g => new { FixtureId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.FixtureId, x => x.Count);

                var pensFixtureIds = await _db.PenaltyKicks.AsNoTracking()
                    .Where(k => k.TeamId == teamId.Value)
                    .Select(k => k.Shootout!.FixtureId)
                    .Distinct()
                    .ToListAsync();

                var submittedSet = submittedFixtureIds.ToHashSet();
                var pensSet = pensFixtureIds.ToHashSet();
                foreach (var f in fixtures)
                {
                    f.ManagerStatsSubmitted = submittedSet.Contains(f.Id);
                    f.ManagerSquadSubmitted = squadCounts.ContainsKey(f.Id);
                    f.ManagerSquadCount = squadCounts.GetValueOrDefault(f.Id);
                    f.ManagerPensSubmitted = pensSet.Contains(f.Id);
                }
            }

            return fixtures;
        }

        public async Task<FixtureWeekDto?> GetCurrentWeekAsync()
        {
            var today = DateTime.UtcNow.Date;

            var fixtures = await _db.Fixtures.AsNoTracking()
                .Include(f => f.HomeTeam)
                .Include(f => f.AwayTeam)
                .Where(f => today >= f.WindowStart.Date && today <= f.WindowEnd.Date)
                .ToListAsync();

            if (!fixtures.Any()) return null;

            var seasonId = fixtures.First().SeasonId;
            var allSeasonFixtures = await _db.Fixtures.AsNoTracking()
                .Include(f => f.HomeTeam)
                .Include(f => f.AwayTeam)
                .Where(f => f.SeasonId == seasonId)
                .ToListAsync();

            var allTeamsInSeason = allSeasonFixtures
                .SelectMany(f => new[] { (f.HomeTeamId, f.HomeTeam!.Name), (f.AwayTeamId, f.AwayTeam!.Name) })
                .DistinctBy(t => t.Item1)
                .ToList();

            var playingIds = fixtures.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).ToHashSet();
            var byes = allTeamsInSeason
                .Where(t => !playingIds.Contains(t.Item1))
                .Select(t => t.Item2)
                .OrderBy(n => n)
                .ToList();

            return new FixtureWeekDto
            {
                WeekNumber = fixtures.First().MatchNumber,
                DateRange = $"{fixtures.Min(f => f.WindowStart):dd MMM} - {fixtures.Max(f => f.WindowEnd):dd MMM}",
                Matches = fixtures.Select(f => new FixtureMatchDto
                {
                    Id = f.Id,
                    Home = f.HomeTeam!.Name,
                    Away = f.AwayTeam!.Name,
                    HomeLogo = f.HomeTeam.ProfileImageUrl,
                    AwayLogo = f.AwayTeam.ProfileImageUrl,
                    Day = f.Kickoff?.ToString("ddd d MMM") ?? "",
                    Time = f.Kickoff?.ToString("HH:mm") ?? "",
                    Location = f.Location ?? "",
                    Postcode = f.Postcode ?? ""
                }).ToList(),
                Byes = byes
            };
        }

        public async Task<List<FixtureWeekDto>> GetAllWeeksAsync(int? seasonId = null)
        {
            // Query 1: fixtures only — project to anonymous type (no stats join)
            var query = _db.Fixtures.AsQueryable();
            if (seasonId.HasValue)
                query = query.Where(f => f.SeasonId == seasonId.Value);

            var fixtures = await query
                .OrderBy(f => f.WindowStart)
                .ThenBy(f => f.Kickoff)
                .Select(f => new
                {
                    f.Id,
                    f.HomeTeamId,
                    f.AwayTeamId,
                    f.WindowStart,
                    f.WindowEnd,
                    f.Kickoff,
                    f.Location,
                    f.Postcode,
                    f.IsPlayed,
                    f.HomeScore,
                    f.AwayScore,
                    f.SeasonId,
                    HomeTeamName = f.HomeTeam!.Name,
                    AwayTeamName = f.AwayTeam!.Name,
                    HomeTeamLogo = f.HomeTeam.ProfileImageUrl,
                    AwayTeamLogo = f.AwayTeam.ProfileImageUrl,
                })
                .ToListAsync();

            // Query 2: only rows where goals were scored — far smaller than full stats table
            var playedIds = fixtures.Where(f => f.IsPlayed).Select(f => f.Id).ToList();
            List<(int FixtureId, int Goals, string Name, int TeamId, bool IsOwnGoal)> scorerRows;
            if (playedIds.Count == 0)
            {
                scorerRows = new();
            }
            else
            {
                var raw = await _db.FixturePlayerStats
                    .Where(s => playedIds.Contains(s.FixtureId) && (s.Goals > 0 || s.OwnGoals > 0))
                    .Select(s => new
                    {
                        s.FixtureId,
                        s.Goals,
                        s.OwnGoals,
                        s.Player!.Name,
                        TeamId = s.Player.TeamId
                    })
                    .ToListAsync();

                scorerRows = raw
                    .Where(s => s.Goals > 0)
                    .Select(s => (s.FixtureId, s.Goals, s.Name, s.TeamId, IsOwnGoal: false))
                    .ToList();

                // Own goals are listed against the side that benefited, so the row is
                // tagged with the opposing team id rather than the scorer's own.
                var fixtureSides = fixtures.ToDictionary(f => f.Id, f => (f.HomeTeamId, f.AwayTeamId));
                foreach (var s in raw.Where(s => s.OwnGoals > 0))
                {
                    if (!fixtureSides.TryGetValue(s.FixtureId, out var sides)) continue;
                    var creditedTeamId = s.TeamId == sides.HomeTeamId ? sides.AwayTeamId : sides.HomeTeamId;
                    scorerRows.Add((s.FixtureId, s.OwnGoals, s.Name, creditedTeamId, IsOwnGoal: true));
                }
            }

            var scorersByFixture = scorerRows.ToLookup(s => s.FixtureId);

            var seasonTeams = fixtures
                .GroupBy(f => f.SeasonId)
                .ToDictionary(
                    g => g.Key,
                    g => g.SelectMany(f => new[] { (f.HomeTeamId, f.HomeTeamName), (f.AwayTeamId, f.AwayTeamName) })
                         .DistinctBy(t => t.Item1)
                         .ToList()
                );

            return fixtures
                .GroupBy(f => new { f.WindowStart, f.WindowEnd })
                .Select((group, index) =>
                {
                    var seasonId = group.First().SeasonId;
                    var allTeams = seasonTeams.GetValueOrDefault(seasonId) ?? new();
                    var playingIds = group.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).ToHashSet();
                    var byes = allTeams
                        .Where(t => !playingIds.Contains(t.Item1))
                        .Select(t => t.Item2)
                        .OrderBy(n => n)
                        .ToList();

                    return new FixtureWeekDto
                    {
                        WeekNumber = index + 1,
                        DateRange = $"{group.Key.WindowStart:dd MMM} - {group.Key.WindowEnd:dd MMM}",
                        Matches = group.Select(f =>
                        {
                            var scorers = scorersByFixture[f.Id];
                            return new FixtureMatchDto
                            {
                                Id = f.Id,
                                Home = f.HomeTeamName,
                                Away = f.AwayTeamName,
                                HomeLogo = f.HomeTeamLogo,
                                AwayLogo = f.AwayTeamLogo,
                                Day = f.Kickoff?.ToString("ddd d MMM") ?? "",
                                Time = f.Kickoff?.ToString("HH:mm") ?? "",
                                Location = f.Location ?? "",
                                Postcode = f.Postcode ?? "",
                                IsPlayed = f.IsPlayed,
                                HomeScore = f.HomeScore,
                                AwayScore = f.AwayScore,
                                HomeScorers = f.IsPlayed
                                    ? scorers.Where(s => s.TeamId == f.HomeTeamId)
                                        .OrderByDescending(s => s.Goals)
                                        .Select(s => new GoalScorerDto { Name = s.Name, Goals = s.Goals, IsOwnGoal = s.IsOwnGoal })
                                        .ToList()
                                    : new(),
                                AwayScorers = f.IsPlayed
                                    ? scorers.Where(s => s.TeamId == f.AwayTeamId)
                                        .OrderByDescending(s => s.Goals)
                                        .Select(s => new GoalScorerDto { Name = s.Name, Goals = s.Goals, IsOwnGoal = s.IsOwnGoal })
                                        .ToList()
                                    : new()
                            };
                        }).ToList(),
                        Byes = byes
                    };
                })
                .ToList();
        }

        public async Task<List<HeadToHeadResultDto>> GetHeadToHeadAsync(int homeTeamId, int awayTeamId, int excludeFixtureId)
        {
            return await _db.Fixtures.AsNoTracking()
                .Include(f => f.HomeTeam)
                .Include(f => f.AwayTeam)
                .Where(f => f.IsPlayed && f.Id != excludeFixtureId &&
                    ((f.HomeTeamId == homeTeamId && f.AwayTeamId == awayTeamId) ||
                     (f.HomeTeamId == awayTeamId && f.AwayTeamId == homeTeamId)))
                .OrderByDescending(f => f.Kickoff ?? f.WindowEnd)
                .Take(10)
                .Select(f => new HeadToHeadResultDto
                {
                    FixtureId = f.Id,
                    HomeTeam = f.HomeTeam!.Name,
                    AwayTeam = f.AwayTeam!.Name,
                    HomeScore = f.HomeScore,
                    AwayScore = f.AwayScore,
                    Kickoff = f.Kickoff,
                    WindowEnd = f.WindowEnd
                })
                .ToListAsync();
        }
    }
}
