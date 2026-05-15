using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Models.Requests;
using Ballers.Models;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface IPenaltyService
    {
        Task<List<PenaltyTableRowDto>> GetTableAsync(int seasonId);
        Task<(List<PenaltyKickResult> HomeKicks, List<PenaltyKickResult> AwayKicks)?> GetShootoutAsync(int fixtureId);
        Task SubmitKicksAsync(int fixtureId, int teamId, List<PenaltyKickEntry> kicks);
    }

    public class PenaltyService : IPenaltyService
    {
        private readonly ApplicationDbContext _db;

        public PenaltyService(ApplicationDbContext db) => _db = db;

        public async Task<List<PenaltyTableRowDto>> GetTableAsync(int seasonId)
        {
            var teams = await _db.Teams.ToListAsync();

            var shootouts = await _db.PenaltyShootouts
                .Include(s => s.Fixture)
                .Include(s => s.Kicks)
                .Where(s => s.Fixture!.SeasonId == seasonId)
                .ToListAsync();

            var table = new List<PenaltyTableRowDto>();

            foreach (var team in teams)
            {
                var row = new PenaltyTableRowDto { Team = team.Name };

                foreach (var shootout in shootouts)
                {
                    var fixture = shootout.Fixture!;
                    bool isHome = fixture.HomeTeamId == team.Id;
                    bool isAway = fixture.AwayTeamId == team.Id;
                    if (!isHome && !isAway) continue;

                    var homeKicks = shootout.Kicks.Where(k => k.TeamId == fixture.HomeTeamId).ToList();
                    var awayKicks = shootout.Kicks.Where(k => k.TeamId == fixture.AwayTeamId).ToList();

                    if (homeKicks.Count == 0 || awayKicks.Count == 0) continue;

                    int homeScore = homeKicks.Count(k => k.Scored);
                    int awayScore = awayKicks.Count(k => k.Scored);

                    row.Played++;

                    if (isHome)
                    {
                        row.PenaltiesFor += homeScore;
                        row.PenaltiesAgainst += awayScore;
                        if (homeScore > awayScore) row.Won++;
                        else if (homeScore == awayScore) row.Drawn++;
                        else row.Lost++;
                    }
                    else
                    {
                        row.PenaltiesFor += awayScore;
                        row.PenaltiesAgainst += homeScore;
                        if (awayScore > homeScore) row.Won++;
                        else if (awayScore == homeScore) row.Drawn++;
                        else row.Lost++;
                    }
                }

                table.Add(row);
            }

            var ordered = table
                .OrderByDescending(t => t.Points)
                .ThenByDescending(t => t.PenaltyDifference)
                .ThenByDescending(t => t.PenaltiesFor)
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
                ordered[i].Position = i + 1;

            return ordered;
        }

        public async Task<(List<PenaltyKickResult> HomeKicks, List<PenaltyKickResult> AwayKicks)?> GetShootoutAsync(int fixtureId)
        {
            var fixture = await _db.Fixtures.FindAsync(fixtureId);
            if (fixture == null) return null;

            var shootout = await _db.PenaltyShootouts
                .Include(s => s.Kicks)
                    .ThenInclude(k => k.Player)
                .FirstOrDefaultAsync(s => s.FixtureId == fixtureId);

            if (shootout == null)
                return (new List<PenaltyKickResult>(), new List<PenaltyKickResult>());

            var homeKicks = shootout.Kicks
                .Where(k => k.TeamId == fixture.HomeTeamId)
                .OrderBy(k => k.Order)
                .Select(k => new PenaltyKickResult(k.Order, k.PlayerId, k.Player!.Name, k.Scored))
                .ToList();

            var awayKicks = shootout.Kicks
                .Where(k => k.TeamId == fixture.AwayTeamId)
                .OrderBy(k => k.Order)
                .Select(k => new PenaltyKickResult(k.Order, k.PlayerId, k.Player!.Name, k.Scored))
                .ToList();

            return (homeKicks, awayKicks);
        }

        public async Task SubmitKicksAsync(int fixtureId, int teamId, List<PenaltyKickEntry> kicks)
        {
            var fixture = await _db.Fixtures.FindAsync(fixtureId)
                ?? throw new KeyNotFoundException($"Fixture {fixtureId} not found.");

            if (!fixture.IsPlayed)
                throw new InvalidOperationException("Penalty kicks can only be submitted after the match is played.");

            if (kicks.Count != 5)
                throw new ArgumentException("Exactly 5 penalty kicks must be submitted.");

            var playerIds = kicks.Select(k => k.PlayerId).ToList();
            var players = await _db.Players
                .Where(p => playerIds.Contains(p.Id) && p.TeamId == teamId)
                .ToDictionaryAsync(p => p.Id);

            if (players.Count != kicks.Count)
                throw new ArgumentException("One or more players not found for this team.");

            var shootout = await _db.PenaltyShootouts.FirstOrDefaultAsync(s => s.FixtureId == fixtureId);
            if (shootout == null)
            {
                shootout = new PenaltyShootout { FixtureId = fixtureId };
                _db.PenaltyShootouts.Add(shootout);
                await _db.SaveChangesAsync();
            }

            var existing = await _db.PenaltyKicks
                .Where(k => k.ShootoutId == shootout.Id && k.TeamId == teamId)
                .ToListAsync();
            _db.PenaltyKicks.RemoveRange(existing);

            foreach (var kick in kicks)
            {
                _db.PenaltyKicks.Add(new PenaltyKick
                {
                    ShootoutId = shootout.Id,
                    PlayerId = kick.PlayerId,
                    TeamId = teamId,
                    Order = kick.Order,
                    Scored = kick.Scored
                });
            }

            await _db.SaveChangesAsync();
        }
    }
}
