using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Models.Requests;
using Ballers.Models;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface IFixtureService
    {
        Task<FixtureDetail?> GetByIdAsync(int id);
        Task<List<FixtureSummary>> GetForUserAsync(bool isAdmin, int? teamId);
        Task<List<LeagueTableRowDto>> GetTableAsync(int seasonId);
        Task<FixtureWeekDto?> GetCurrentWeekAsync();
        Task<List<FixtureWeekDto>> GetAllWeeksAsync();
        Task<List<PlayerSummary>?> GetPlayersAsync(int fixtureId, bool isAdmin, int? userTeamId, int? requestedTeamId);
        Task<List<SquadEntry>> GetSquadAsync(int fixtureId);
        Task UpdateSquadAsync(int fixtureId, List<int> playerIds, int? teamId);
        Task<List<PlayerStatDto>> GetStatsAsync(int fixtureId);
        Task SubmitStatsAsync(int fixtureId, List<PlayerStatDto> stats, int? teamId);
        Task<bool> UpdateScheduleAsync(int fixtureId, string? location, string? postcode, DateTime kickoff);
        Task GenerateFixturesAsync(List<int> teamIds, DateTime startDate);
    }

    public class FixtureService : IFixtureService
    {
        private readonly ApplicationDbContext _db;

        public FixtureService(ApplicationDbContext db) => _db = db;

        public async Task<FixtureDetail?> GetByIdAsync(int id)
        {
            var f = await _db.Fixtures
                .Include(x => x.HomeTeam)
                .Include(x => x.AwayTeam)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (f == null) return null;

            return new FixtureDetail(
                f.Id, f.HomeTeam!.Name, f.AwayTeam!.Name,
                f.HomeTeamId, f.AwayTeamId,
                f.Kickoff, f.Location, f.Postcode, f.MatchNumber, f.IsPlayed,
                f.HomeScore, f.AwayScore);
        }

        public async Task<List<FixtureSummary>> GetForUserAsync(bool isAdmin, int? teamId)
        {
            var query = _db.Fixtures
                .Include(f => f.HomeTeam)
                .Include(f => f.AwayTeam)
                .Include(f => f.Season)
                .AsQueryable();

            if (!isAdmin)
            {
                if (teamId == null) return new List<FixtureSummary>();
                query = query.Where(f => f.HomeTeamId == teamId || f.AwayTeamId == teamId);
            }

            return await query
                .OrderBy(f => f.MatchNumber)
                .ThenBy(f => f.Kickoff)
                .Select(f => new FixtureSummary(
                    f.Id, f.HomeTeam!.Name, f.AwayTeam!.Name,
                    f.MatchNumber, f.Kickoff, f.Location, f.IsPlayed,
                    isAdmin || teamId == f.HomeTeamId))
                .ToListAsync();
        }

        public async Task<List<LeagueTableRowDto>> GetTableAsync(int seasonId)
        {
            var teams = await _db.Teams
                .Where(t => _db.Fixtures.Any(f =>
                    f.SeasonId == seasonId &&
                    (f.HomeTeamId == t.Id || f.AwayTeamId == t.Id)))
                .ToListAsync();

            var fixtures = await _db.Fixtures
                .Where(f => f.SeasonId == seasonId && f.IsPlayed)
                .ToListAsync();

            var table = teams.Select(team =>
            {
                var row = new LeagueTableRowDto { Team = team.Name };

                foreach (var f in fixtures.Where(f => f.HomeTeamId == team.Id || f.AwayTeamId == team.Id))
                {
                    bool isHome = f.HomeTeamId == team.Id;
                    int goalsFor = isHome ? f.HomeScore : f.AwayScore;
                    int goalsAgainst = isHome ? f.AwayScore : f.HomeScore;

                    row.Played++;
                    row.GoalsFor += goalsFor;
                    row.GoalsAgainst += goalsAgainst;

                    if (goalsFor > goalsAgainst) row.Won++;
                    else if (goalsFor == goalsAgainst) row.Drawn++;
                    else row.Lost++;
                }

                return row;
            }).ToList();

            var ordered = table
                .OrderByDescending(t => t.Points)
                .ThenByDescending(t => t.GoalDifference)
                .ThenByDescending(t => t.GoalsFor)
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
                ordered[i].Position = i + 1;

            return ordered;
        }

        public async Task<FixtureWeekDto?> GetCurrentWeekAsync()
        {
            var today = DateTime.UtcNow.Date;

            var fixtures = await _db.Fixtures
                .Include(f => f.HomeTeam)
                .Include(f => f.AwayTeam)
                .Where(f => today >= f.WindowStart.Date && today <= f.WindowEnd.Date)
                .ToListAsync();

            if (!fixtures.Any()) return null;

            var seasonId = fixtures.First().SeasonId;
            var allSeasonFixtures = await _db.Fixtures
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
                    Home = f.HomeTeam!.Name,
                    Away = f.AwayTeam!.Name,
                    Day = f.Kickoff?.ToString("dddd") ?? "",
                    Time = f.Kickoff?.ToString("HH:mm") ?? "",
                    Location = f.Location ?? "",
                    Postcode = f.Postcode ?? ""
                }).ToList(),
                Byes = byes
            };
        }

        public async Task<List<FixtureWeekDto>> GetAllWeeksAsync()
        {
            var fixtures = await _db.Fixtures
                .Include(f => f.HomeTeam)
                .Include(f => f.AwayTeam)
                .OrderBy(f => f.WindowStart)
                .ThenBy(f => f.Kickoff)
                .ToListAsync();

            // Build a map of seasonId → all team (id, name) pairs in that season
            var seasonTeams = fixtures
                .GroupBy(f => f.SeasonId)
                .ToDictionary(
                    g => g.Key,
                    g => g.SelectMany(f => new[] { (f.HomeTeamId, f.HomeTeam!.Name), (f.AwayTeamId, f.AwayTeam!.Name) })
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
                        Matches = group.Select(f => new FixtureMatchDto
                        {
                            Home = f.HomeTeam!.Name,
                            Away = f.AwayTeam!.Name,
                            Day = f.Kickoff?.ToString("dddd") ?? "",
                            Time = f.Kickoff?.ToString("HH:mm") ?? "",
                            Location = f.Location ?? "",
                            Postcode = f.Postcode ?? ""
                        }).ToList(),
                        Byes = byes
                    };
                })
                .ToList();
        }

        public async Task<List<PlayerSummary>?> GetPlayersAsync(int fixtureId, bool isAdmin, int? userTeamId, int? requestedTeamId)
        {
            var fixture = await _db.Fixtures.FindAsync(fixtureId);
            if (fixture == null) return null;

            bool isHome = userTeamId == fixture.HomeTeamId;
            int resolvedTeamId = isAdmin
                ? (requestedTeamId ?? fixture.HomeTeamId)
                : (isHome ? fixture.HomeTeamId : fixture.AwayTeamId);

            return await _db.Players
                .Where(p => p.TeamId == resolvedTeamId && p.IsActive)
                .Select(p => new PlayerSummary(p.Id, p.Name))
                .ToListAsync();
        }

        public async Task<List<SquadEntry>> GetSquadAsync(int fixtureId)
        {
            return await _db.FixturePlayers
                .Include(fp => fp.Player)
                .Where(fp => fp.FixtureId == fixtureId)
                .Select(fp => new SquadEntry(fp.PlayerId, fp.Player!.Name))
                .ToListAsync();
        }

        public async Task UpdateSquadAsync(int fixtureId, List<int> playerIds, int? teamId)
        {
            if (teamId.HasValue)
            {
                var teamPlayerIds = await _db.Players
                    .Where(p => p.TeamId == teamId.Value)
                    .Select(p => p.Id)
                    .ToListAsync();

                var existing = _db.FixturePlayers
                    .Where(fp => fp.FixtureId == fixtureId && teamPlayerIds.Contains(fp.PlayerId));
                _db.FixturePlayers.RemoveRange(existing);

                foreach (var id in playerIds.Where(id => teamPlayerIds.Contains(id)))
                    _db.FixturePlayers.Add(new FixturePlayer { FixtureId = fixtureId, PlayerId = id });
            }
            else
            {
                var existing = _db.FixturePlayers.Where(fp => fp.FixtureId == fixtureId);
                _db.FixturePlayers.RemoveRange(existing);

                foreach (var playerId in playerIds)
                    _db.FixturePlayers.Add(new FixturePlayer { FixtureId = fixtureId, PlayerId = playerId });
            }

            await _db.SaveChangesAsync();
        }

        public async Task<List<PlayerStatDto>> GetStatsAsync(int fixtureId)
        {
            return await _db.FixturePlayerStats
                .Where(s => s.FixtureId == fixtureId)
                .Select(s => new PlayerStatDto
                {
                    PlayerId = s.PlayerId,
                    Goals = s.Goals,
                    Assists = s.Assists,
                    IsManOfTheMatch = s.ManOfTheMatch,
                    HadYellowCard = s.YellowCards,
                    HadRedCard = s.RedCard
                })
                .ToListAsync();
        }

        public async Task SubmitStatsAsync(int fixtureId, List<PlayerStatDto> stats, int? teamId)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();

            var fixture = await _db.Fixtures.FindAsync(fixtureId)
                ?? throw new KeyNotFoundException($"Fixture {fixtureId} not found.");

            // Enforce at most one MOTM per submission
            bool foundMotm = false;
            foreach (var s in stats)
            {
                if (s.IsManOfTheMatch && !foundMotm) foundMotm = true;
                else s.IsManOfTheMatch = false;
            }

            var playerIds = stats.Select(s => s.PlayerId).ToList();

            // When teamId is supplied only process stats for that team's players
            var playersQuery = _db.Players.Where(p => playerIds.Contains(p.Id));
            if (teamId.HasValue)
                playersQuery = playersQuery.Where(p => p.TeamId == teamId.Value);

            var players = await playersQuery.ToDictionaryAsync(p => p.Id);

            var existingStats = await _db.FixturePlayerStats
                .Where(s => s.FixtureId == fixtureId && playerIds.Contains(s.PlayerId))
                .ToDictionaryAsync(s => s.PlayerId);

            foreach (var stat in stats)
            {
                if (!players.ContainsKey(stat.PlayerId)) continue;

                if (existingStats.TryGetValue(stat.PlayerId, out var existing))
                {
                    existing.Goals = stat.Goals;
                    existing.Assists = stat.Assists;
                    existing.ManOfTheMatch = stat.IsManOfTheMatch;
                    existing.YellowCards = stat.HadYellowCard;
                    existing.RedCard = stat.HadRedCard;
                }
                else
                {
                    _db.FixturePlayerStats.Add(new FixturePlayerStat
                    {
                        FixtureId = fixtureId,
                        PlayerId = stat.PlayerId,
                        Goals = stat.Goals,
                        Assists = stat.Assists,
                        ManOfTheMatch = stat.IsManOfTheMatch,
                        YellowCards = stat.HadYellowCard,
                        RedCard = stat.HadRedCard
                    });
                }
            }

            await _db.SaveChangesAsync();

            // Recalculate scores from all stats currently in DB for this fixture
            var allScores = await _db.FixturePlayerStats
                .Where(s => s.FixtureId == fixtureId)
                .Join(_db.Players, s => s.PlayerId, p => p.Id,
                    (s, p) => new { s.Goals, p.TeamId })
                .ToListAsync();

            fixture.HomeScore = allScores.Where(x => x.TeamId == fixture.HomeTeamId).Sum(x => x.Goals);
            fixture.AwayScore = allScores.Where(x => x.TeamId == fixture.AwayTeamId).Sum(x => x.Goals);
            fixture.IsPlayed = true;

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        public async Task<bool> UpdateScheduleAsync(int fixtureId, string? location, string? postcode, DateTime kickoff)
        {
            var fixture = await _db.Fixtures.FindAsync(fixtureId);
            if (fixture == null) return false;

            fixture.Location = location;
            fixture.Postcode = postcode;
            fixture.Kickoff = kickoff;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task GenerateFixturesAsync(List<int> teamIds, DateTime startDate)
        {
            if (teamIds == null || teamIds.Count < 2)
                throw new ArgumentException("At least two teams required.");

            var teams = await _db.Teams
                .Where(t => teamIds.Contains(t.Id))
                .ToListAsync();

            if (teams.Count < 2)
                throw new ArgumentException("Not enough teams.");

            bool isOdd = teams.Count % 2 != 0;
            int roundsPerLeg = isOdd ? teams.Count : teams.Count - 1;
            var endDate = startDate.AddDays(roundsPerLeg * 2 * 14);

            bool overlaps = await _db.Seasons.AnyAsync(s =>
                s.StartDate < endDate && s.EndDate > startDate);
            if (overlaps)
                throw new InvalidOperationException("A season already exists that overlaps with this date range.");

            var lastSeason = await _db.Seasons
                .OrderByDescending(x => x.SeasonNumber)
                .FirstOrDefaultAsync();

            var season = new Season
            {
                SeasonNumber = lastSeason == null ? 1 : lastSeason.SeasonNumber + 1,
                StartDate = startDate,
                EndDate = endDate,
                IsActive = false,
            };
            _db.Seasons.Add(season);

            var random = new Random();
            List<List<(int HomeId, int AwayId)>> firstLegRounds;
            List<List<(int HomeId, int AwayId)>> secondLegRounds = new();

            if (isOdd)
            {
                var shuffled = teams.OrderBy(_ => random.Next()).ToList();
                int n = shuffled.Count;

                firstLegRounds = Enumerable.Range(0, n).Select(r =>
                    Enumerable.Range(1, (n - 1) / 2).Select(i =>
                    {
                        int a = (r + i) % n;
                        int b = (r - i + n) % n;
                        return random.Next(2) == 0
                            ? (HomeId: shuffled[a].Id, AwayId: shuffled[b].Id)
                            : (HomeId: shuffled[b].Id, AwayId: shuffled[a].Id);
                    }).ToList()
                ).ToList();

                int attempts = 0;
                do
                {
                    var order = Enumerable.Range(0, n).OrderBy(_ => random.Next()).ToArray();
                    secondLegRounds = order
                        .Select(r => firstLegRounds[r]
                            .Select(p => (HomeId: p.AwayId, AwayId: p.HomeId)).ToList())
                        .ToList();
                    attempts++;
                }
                while (AnyRoundMirrored(firstLegRounds, secondLegRounds) && attempts < 100);
            }
            else
            {
                int fixturesPerRound = teams.Count / 2;
                var pairs = new List<(int HomeId, int AwayId)>();
                for (int i = 0; i < teams.Count; i++)
                    for (int j = i + 1; j < teams.Count; j++)
                        pairs.Add((teams[i].Id, teams[j].Id));

                firstLegRounds = ChunkIntoRounds(
                    pairs.OrderBy(_ => random.Next()).ToList(), fixturesPerRound);

                var reversedPairs = pairs.Select(p => (HomeId: p.AwayId, AwayId: p.HomeId)).ToList();
                int attempts = 0;
                do
                {
                    secondLegRounds = ChunkIntoRounds(
                        reversedPairs.OrderBy(_ => random.Next()).ToList(), fixturesPerRound);
                    attempts++;
                }
                while (AnyRoundMirrored(firstLegRounds, secondLegRounds) && attempts < 100);
            }

            int matchNumber = 1;
            foreach (var round in firstLegRounds.Concat(secondLegRounds))
            {
                var windowStart = season.StartDate.AddDays((matchNumber - 1) * 14);
                foreach (var (homeId, awayId) in round)
                {
                    _db.Fixtures.Add(new Fixture
                    {
                        Season = season,
                        HomeTeamId = homeId,
                        AwayTeamId = awayId,
                        MatchNumber = matchNumber,
                        WindowStart = windowStart,
                        WindowEnd = windowStart.AddDays(13),
                    });
                }
                matchNumber++;
            }

            await _db.SaveChangesAsync();
        }

        private static List<List<(int HomeId, int AwayId)>> ChunkIntoRounds(
            List<(int HomeId, int AwayId)> pairs, int size) =>
            Enumerable.Range(0, pairs.Count / size)
                .Select(r => pairs.Skip(r * size).Take(size).ToList())
                .ToList();

        private static bool AnyRoundMirrored(
            List<List<(int HomeId, int AwayId)>> firstLeg,
            List<List<(int HomeId, int AwayId)>> secondLeg)
        {
            for (int r = 0; r < firstLeg.Count; r++)
            {
                var f = firstLeg[r]
                    .Select(p => (Math.Min(p.HomeId, p.AwayId), Math.Max(p.HomeId, p.AwayId)))
                    .ToHashSet();
                var s = secondLeg[r]
                    .Select(p => (Math.Min(p.HomeId, p.AwayId), Math.Max(p.HomeId, p.AwayId)))
                    .ToHashSet();
                if (f.SetEquals(s)) return true;
            }
            return false;
        }
    }
}
