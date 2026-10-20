using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Models;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    // Results: stats in and out, the true man of the match, and the league table built from them.
    public partial class FixtureService
    {
        public async Task<List<PlayerStatDto>> GetStatsAsync(int fixtureId)
        {
            return await _db.FixturePlayerStats.AsNoTracking()
                .Where(s => s.FixtureId == fixtureId)
                .Select(s => new PlayerStatDto
                {
                    PlayerId = s.PlayerId,
                    Goals = s.Goals,
                    OwnGoals = s.OwnGoals,
                    Assists = s.Assists,
                    IsManOfTheMatch = s.ManOfTheMatch,
                    HadYellowCard = s.YellowCards,
                    HadRedCard = s.RedCard
                })
                .ToListAsync();
        }

        // The candidates are exactly the players the referee flagged as man of the
        // match in the fixture stats — normally one per team.
        private IQueryable<TrueMotmCandidateDto> CandidateQuery(int fixtureId)
            => _db.FixturePlayerStats
                .Where(s => s.FixtureId == fixtureId && s.ManOfTheMatch)
                .Select(s => new TrueMotmCandidateDto
                {
                    PlayerId = s.PlayerId,
                    PlayerName = s.Player!.Name,
                    TeamId = s.Player.TeamId,
                    TeamName = s.Player.Team!.Name
                });

        public async Task<TrueMotmDto?> GetTrueMotmAsync(int fixtureId)
        {
            var fixture = await _db.Fixtures.AsNoTracking()
                .Where(f => f.Id == fixtureId)
                .Select(f => new { f.Id, Home = f.HomeTeam!.Name, Away = f.AwayTeam!.Name, f.TrueMotmPlayerId })
                .FirstOrDefaultAsync();

            if (fixture == null) return null;

            var candidates = await CandidateQuery(fixtureId).ToListAsync();

            return new TrueMotmDto
            {
                FixtureId = fixture.Id,
                HomeTeam = fixture.Home,
                AwayTeam = fixture.Away,
                SelectedPlayerId = fixture.TrueMotmPlayerId,
                Candidates = candidates
            };
        }

        // Returns false when the player is not one of the fixture's nominated men
        // of the match, so a stale or hand-crafted request cannot set an arbitrary
        // player. Passing null clears the pick.
        public async Task<bool> SetTrueMotmAsync(int fixtureId, int? playerId)
        {
            var fixture = await _db.Fixtures.FindAsync(fixtureId);
            if (fixture == null) return false;

            if (playerId.HasValue)
            {
                bool isCandidate = await _db.FixturePlayerStats
                    .AnyAsync(s => s.FixtureId == fixtureId
                                && s.PlayerId == playerId.Value
                                && s.ManOfTheMatch);
                if (!isCandidate) return false;
            }

            fixture.TrueMotmPlayerId = playerId;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<List<TrueMotmRowDto>> GetTrueMotmLogAsync()
        {
            return await _db.Fixtures.AsNoTracking()
                .Where(f => f.TrueMotmPlayerId != null)
                .OrderByDescending(f => f.MatchNumber)
                .ThenByDescending(f => f.Kickoff)
                .Select(f => new TrueMotmRowDto
                {
                    FixtureId = f.Id,
                    Kickoff = f.Kickoff,
                    Week = f.MatchNumber,
                    HomeTeam = f.HomeTeam!.Name,
                    AwayTeam = f.AwayTeam!.Name,
                    HomeScore = f.HomeScore,
                    AwayScore = f.AwayScore,
                    PlayerId = f.TrueMotmPlayerId,
                    PlayerName = f.TrueMotmPlayer!.Name,
                    TeamName = f.TrueMotmPlayer.Team!.Name
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
                    existing.OwnGoals = stat.OwnGoals;
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
                        OwnGoals = stat.OwnGoals,
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
                    (s, p) => new { s.Goals, s.OwnGoals, p.TeamId })
                .ToListAsync();

            var homeRows = allScores.Where(x => x.TeamId == fixture.HomeTeamId).ToList();
            var awayRows = allScores.Where(x => x.TeamId == fixture.AwayTeamId).ToList();

            fixture.SetScoreFromStats(
                homeRows.Sum(x => x.Goals), homeRows.Sum(x => x.OwnGoals),
                awayRows.Sum(x => x.Goals), awayRows.Sum(x => x.OwnGoals));
            fixture.MarkStatsSubmitted(teamId);

            if (fixture.TrueMotmPlayerId.HasValue)
                fixture.KeepTrueMotmOnlyIfNominated(await _db.FixturePlayerStats
                    .Where(s => s.FixtureId == fixtureId && s.ManOfTheMatch)
                    .Select(s => s.PlayerId)
                    .ToListAsync());

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            if (fixture.IsKnockout)
                await _knockout.TryFinalizeFromStatsAsync(fixtureId);
        }

        public async Task<List<OpponentPlayerStatDto>> GetOpponentStatsAsync(int fixtureId, int opponentTeamId)
        {
            var squadIds = await _db.FixturePlayers.AsNoTracking()
                .Where(fp => fp.FixtureId == fixtureId)
                .Select(fp => fp.PlayerId)
                .ToListAsync();

            return await _db.FixturePlayerStats.AsNoTracking()
                .Include(s => s.Player)
                .Where(s => s.FixtureId == fixtureId
                         && squadIds.Contains(s.PlayerId)
                         && s.Player!.TeamId == opponentTeamId)
                .Select(s => new OpponentPlayerStatDto
                {
                    Name = s.Player!.Name,
                    Goals = s.Goals,
                    OwnGoals = s.OwnGoals,
                    Assists = s.Assists,
                    IsMotm = s.ManOfTheMatch,
                    YellowCard = s.YellowCards,
                    RedCard = s.RedCard
                })
                .ToListAsync();
        }

        public async Task<List<LeagueTableRowDto>> GetTableAsync(int seasonId)
        {
            var teams = await _db.Teams.AsNoTracking()
                .Where(t => _db.Fixtures.Any(f =>
                    f.SeasonId == seasonId &&
                    (f.HomeTeamId == t.Id || f.AwayTeamId == t.Id)))
                .ToListAsync();

            var fixtures = await _db.Fixtures.AsNoTracking()
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
    }
}
