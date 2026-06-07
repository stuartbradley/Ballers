using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Models.Requests;
using Ballers.Models;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public record ImportFixturesResult(int Created, List<string> Errors);

    public interface IFixtureService
    {
        Task<FixtureDetail?> GetByIdAsync(int id);
        Task<List<FixtureSummary>> GetForUserAsync(bool isAdmin, int? teamId);
        Task<List<LeagueTableRowDto>> GetTableAsync(int seasonId);
        Task<FixtureWeekDto?> GetCurrentWeekAsync();
        Task<List<FixtureWeekDto>> GetAllWeeksAsync(int? seasonId = null);
        Task<List<PlayerSummary>?> GetPlayersAsync(int fixtureId, bool isAdmin, int? userTeamId, int? requestedTeamId);
        Task<List<SquadEntry>> GetSquadAsync(int fixtureId);
        Task UpdateSquadAsync(int fixtureId, List<int> playerIds, int? teamId);
        Task<List<PlayerStatDto>> GetStatsAsync(int fixtureId);
        Task SubmitStatsAsync(int fixtureId, List<PlayerStatDto> stats, int? teamId);
        Task<bool> UpdateScheduleAsync(int fixtureId, string? location, string? postcode, DateTime kickoff);
        Task AssignRefereeAsync(int fixtureId, int? refereeId);
        Task GenerateFixturesAsync(List<int> teamIds, DateTime startDate);
        Task<ImportFixturesResult> ImportFixturesAsync(Stream csv, int seasonNumber, DateTime startDate, bool makeActive);
        Task<List<OpponentPlayerStat>> GetOpponentStatsAsync(int fixtureId, int opponentTeamId);
        Task<List<HeadToHeadResult>> GetHeadToHeadAsync(int homeTeamId, int awayTeamId, int excludeFixtureId);
        Task SaveCaptaincyAsync(int fixtureId, int teamId, int? captainId, int? viceId);
    }

    public class FixtureService : IFixtureService
    {
        private readonly ApplicationDbContext _db;
        private readonly IKnockoutService _knockout;

        public FixtureService(ApplicationDbContext db, IKnockoutService knockout)
        {
            _db = db;
            _knockout = knockout;
        }

        public async Task<FixtureDetail?> GetByIdAsync(int id)
        {
            var f = await _db.Fixtures
                .Include(x => x.HomeTeam)
                .Include(x => x.AwayTeam)
                .Include(x => x.Referee)
                .Include(x => x.HomeCaptain)
                .Include(x => x.HomeViceCaptain)
                .Include(x => x.AwayCaptain)
                .Include(x => x.AwayViceCaptain)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (f == null) return null;

            return new FixtureDetail(
                f.Id, f.HomeTeam!.Name, f.AwayTeam!.Name,
                f.HomeTeamId, f.AwayTeamId,
                f.Kickoff, f.Location, f.Postcode, f.MatchNumber, f.IsPlayed,
                f.HomeScore, f.AwayScore,
                f.RefereeId, f.Referee?.Name,
                f.WindowStart, f.WindowEnd)
            {
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
            };
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

            var fixtures = await query
                .OrderBy(f => f.IsKnockout)
                .ThenBy(f => f.MatchNumber)
                .ThenBy(f => f.Kickoff)
                .Select(f => new FixtureSummary(
                    f.Id, f.HomeTeam!.Name, f.AwayTeam!.Name,
                    f.MatchNumber, f.Kickoff, f.Location, f.IsPlayed,
                    isAdmin || teamId == f.HomeTeamId)
                {
                    IsKnockout = f.IsKnockout,
                    KnockoutTournament = f.KnockoutTournament ?? "",
                    KnockoutRound = f.KnockoutRound ?? ""
                })
                .ToListAsync();

            if (teamId.HasValue && fixtures.Count > 0)
            {
                var teamPlayerIds = await _db.Players
                    .Where(p => p.TeamId == teamId.Value)
                    .Select(p => p.Id)
                    .ToListAsync();

                var submittedFixtureIds = await _db.FixturePlayerStats
                    .Where(s => teamPlayerIds.Contains(s.PlayerId))
                    .Select(s => s.FixtureId)
                    .Distinct()
                    .ToListAsync();

                var squadFixtureIds = await _db.FixturePlayers
                    .Where(fp => teamPlayerIds.Contains(fp.PlayerId))
                    .Select(fp => fp.FixtureId)
                    .Distinct()
                    .ToListAsync();

                var submittedSet = submittedFixtureIds.ToHashSet();
                var squadSet = squadFixtureIds.ToHashSet();
                fixtures = fixtures
                    .Select(f => f with
                    {
                        ManagerStatsSubmitted = submittedSet.Contains(f.Id),
                        ManagerSquadSubmitted = squadSet.Contains(f.Id)
                    })
                    .ToList();
            }

            return fixtures;
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
                })
                .ToListAsync();

            // Query 2: only rows where goals were scored — far smaller than full stats table
            var playedIds = fixtures.Where(f => f.IsPlayed).Select(f => f.Id).ToList();
            List<(int FixtureId, int Goals, string Name, int TeamId)> scorerRows;
            if (playedIds.Count == 0)
            {
                scorerRows = new();
            }
            else
            {
                var raw = await _db.FixturePlayerStats
                    .Where(s => playedIds.Contains(s.FixtureId) && s.Goals > 0)
                    .Select(s => new
                    {
                        s.FixtureId,
                        s.Goals,
                        s.Player!.Name,
                        TeamId = s.Player.TeamId
                    })
                    .ToListAsync();
                scorerRows = raw.Select(s => (s.FixtureId, s.Goals, s.Name, s.TeamId)).ToList();
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
                                Home = f.HomeTeamName,
                                Away = f.AwayTeamName,
                                Day = f.Kickoff?.ToString("dddd") ?? "",
                                Time = f.Kickoff?.ToString("HH:mm") ?? "",
                                Location = f.Location ?? "",
                                Postcode = f.Postcode ?? "",
                                IsPlayed = f.IsPlayed,
                                HomeScore = f.HomeScore,
                                AwayScore = f.AwayScore,
                                HomeScorers = f.IsPlayed
                                    ? scorers.Where(s => s.TeamId == f.HomeTeamId)
                                        .OrderByDescending(s => s.Goals)
                                        .Select(s => new GoalScorerDto { Name = s.Name, Goals = s.Goals })
                                        .ToList()
                                    : new(),
                                AwayScorers = f.IsPlayed
                                    ? scorers.Where(s => s.TeamId == f.AwayTeamId)
                                        .OrderByDescending(s => s.Goals)
                                        .Select(s => new GoalScorerDto { Name = s.Name, Goals = s.Goals })
                                        .ToList()
                                    : new()
                            };
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

            if (fixture.IsKnockout)
                await _knockout.TryFinalizeFromStatsAsync(fixtureId);
        }

        public async Task<List<HeadToHeadResult>> GetHeadToHeadAsync(int homeTeamId, int awayTeamId, int excludeFixtureId)
        {
            return await _db.Fixtures
                .Include(f => f.HomeTeam)
                .Include(f => f.AwayTeam)
                .Where(f => f.IsPlayed && f.Id != excludeFixtureId &&
                    ((f.HomeTeamId == homeTeamId && f.AwayTeamId == awayTeamId) ||
                     (f.HomeTeamId == awayTeamId && f.AwayTeamId == homeTeamId)))
                .OrderByDescending(f => f.Kickoff ?? f.WindowEnd)
                .Take(10)
                .Select(f => new HeadToHeadResult(
                    f.Id, f.HomeTeam!.Name, f.AwayTeam!.Name,
                    f.HomeScore, f.AwayScore, f.Kickoff, f.WindowEnd))
                .ToListAsync();
        }

        public async Task SaveCaptaincyAsync(int fixtureId, int teamId, int? captainId, int? viceId)
        {
            var fixture = await _db.Fixtures.FindAsync(fixtureId);
            if (fixture == null) return;

            bool isHome = fixture.HomeTeamId == teamId;
            if (isHome)
            {
                fixture.HomeCaptainId     = captainId;
                fixture.HomeViceCaptainId = viceId;
            }
            else
            {
                fixture.AwayCaptainId     = captainId;
                fixture.AwayViceCaptainId = viceId;
            }
            await _db.SaveChangesAsync();
        }

        public async Task<List<OpponentPlayerStat>> GetOpponentStatsAsync(int fixtureId, int opponentTeamId)
        {
            var squadIds = await _db.FixturePlayers
                .Where(fp => fp.FixtureId == fixtureId)
                .Select(fp => fp.PlayerId)
                .ToListAsync();

            return await _db.FixturePlayerStats
                .Include(s => s.Player)
                .Where(s => s.FixtureId == fixtureId
                         && squadIds.Contains(s.PlayerId)
                         && s.Player!.TeamId == opponentTeamId)
                .Select(s => new OpponentPlayerStat(
                    s.Player!.Name, s.Goals, s.Assists,
                    s.ManOfTheMatch, s.YellowCards, s.RedCard))
                .ToListAsync();
        }

        public async Task AssignRefereeAsync(int fixtureId, int? refereeId)
        {
            var fixture = await _db.Fixtures.FindAsync(fixtureId);
            if (fixture == null) return;
            fixture.RefereeId = refereeId;
            await _db.SaveChangesAsync();
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

        public async Task<ImportFixturesResult> ImportFixturesAsync(Stream csv, int seasonNumber, DateTime startDate, bool makeActive)
        {
            var errors = new List<string>();

            string text;
            using (var reader = new StreamReader(csv))
                text = await reader.ReadToEndAsync();

            var lines = text.Replace("\r\n", "\n").Replace("\r", "\n")
                .Split('\n').Where(l => l.Trim().Length > 0).ToList();
            if (lines.Count < 2)
                return new ImportFixturesResult(0, new() { "CSV is empty or has no data rows." });

            // header → column index
            var header = SplitCsvLine(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToList();
            int Col(string name) => header.IndexOf(name);
            int cMatch = Col("matchnumber"), cHome = Col("hometeam"), cAway = Col("awayteam");
            int cKick = Col("kickoff"), cLoc = Col("location"), cPost = Col("postcode"), cRef = Col("referee");

            if (cMatch < 0 || cHome < 0 || cAway < 0)
                return new ImportFixturesResult(0, new() { "CSV must have headers: MatchNumber, HomeTeam, AwayTeam (plus optional Kickoff, Location, Postcode, Referee)." });

            if (await _db.Seasons.AnyAsync(s => s.SeasonNumber == seasonNumber))
                return new ImportFixturesResult(0, new() { $"Season {seasonNumber} already exists." });

            var teamsByName = await _db.Teams.ToDictionaryAsync(t => t.Name.Trim().ToLowerInvariant(), t => t.Id);
            var refsByName = await _db.Referees.ToDictionaryAsync(r => r.Name.Trim().ToLowerInvariant(), r => r.Id);

            var season = new Season { SeasonNumber = seasonNumber, StartDate = startDate, IsActive = makeActive };
            var fixtures = new List<Fixture>();

            for (int i = 1; i < lines.Count; i++)
            {
                int rowNum = i + 1; // 1-based incl header
                var cells = SplitCsvLine(lines[i]);
                string Get(int idx) => idx >= 0 && idx < cells.Count ? cells[idx].Trim() : "";

                if (!int.TryParse(Get(cMatch), out var matchNumber) || matchNumber < 1)
                { errors.Add($"Row {rowNum}: invalid MatchNumber '{Get(cMatch)}'."); continue; }

                var homeName = Get(cHome); var awayName = Get(cAway);
                if (!teamsByName.TryGetValue(homeName.ToLowerInvariant(), out var homeId))
                { errors.Add($"Row {rowNum}: home team '{homeName}' not found."); continue; }
                if (!teamsByName.TryGetValue(awayName.ToLowerInvariant(), out var awayId))
                { errors.Add($"Row {rowNum}: away team '{awayName}' not found."); continue; }
                if (homeId == awayId)
                { errors.Add($"Row {rowNum}: home and away team are the same ('{homeName}')."); continue; }

                DateTime? kickoff = null;
                var kickStr = Get(cKick);
                if (!string.IsNullOrWhiteSpace(kickStr))
                {
                    if (TryParseKickoff(kickStr, out var k)) kickoff = k;
                    else { errors.Add($"Row {rowNum}: invalid Kickoff '{kickStr}' (use yyyy-MM-dd HH:mm)."); continue; }
                }

                int? refId = null;
                var refName = Get(cRef);
                if (!string.IsNullOrWhiteSpace(refName))
                {
                    if (refsByName.TryGetValue(refName.ToLowerInvariant(), out var rid)) refId = rid;
                    else { errors.Add($"Row {rowNum}: referee '{refName}' not found."); continue; }
                }

                var windowStart = startDate.AddDays((matchNumber - 1) * 14);
                fixtures.Add(new Fixture
                {
                    Season = season,
                    HomeTeamId = homeId,
                    AwayTeamId = awayId,
                    MatchNumber = matchNumber,
                    WindowStart = windowStart,
                    WindowEnd = windowStart.AddDays(13),
                    Kickoff = kickoff,
                    Location = string.IsNullOrWhiteSpace(Get(cLoc)) ? null : Get(cLoc),
                    Postcode = string.IsNullOrWhiteSpace(Get(cPost)) ? null : Get(cPost),
                    RefereeId = refId,
                });
            }

            if (fixtures.Count == 0 && errors.Count == 0)
                errors.Add("No fixture rows found.");
            if (errors.Count > 0)
                return new ImportFixturesResult(0, errors);

            season.EndDate = fixtures.Max(f => f.WindowEnd);

            using var tx = await _db.Database.BeginTransactionAsync();
            _db.Seasons.Add(season);
            _db.Fixtures.AddRange(fixtures);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return new ImportFixturesResult(fixtures.Count, errors);
        }

        private static bool TryParseKickoff(string s, out DateTime result)
        {
            string[] formats =
            {
                "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm", "dd/MM/yyyy HH:mm",
                "dd/MM/yyyy H:mm", "yyyy-MM-dd", "dd/MM/yyyy"
            };
            if (DateTime.TryParseExact(s, formats,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out result))
                return true;
            return DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out result);
        }

        // Minimal CSV line parser supporting double-quoted fields.
        private static List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new System.Text.StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
                    else sb.Append(c);
                }
            }
            result.Add(sb.ToString());
            return result;
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
