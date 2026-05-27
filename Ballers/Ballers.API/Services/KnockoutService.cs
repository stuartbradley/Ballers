using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Shared;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface IKnockoutService
    {
        Task GenerateAsync(int seasonId);
        Task<KnockoutBracketDto> GetBracketAsync(int seasonId);
        Task SubmitResultAsync(int knockoutFixtureId, int homeScore, int awayScore);
        Task TryFinalizeFromStatsAsync(int linkedFixtureId);
    }

    public class KnockoutService : IKnockoutService
    {
        private readonly ApplicationDbContext _db;

        public KnockoutService(ApplicationDbContext db) => _db = db;

        public async Task GenerateAsync(int seasonId)
        {
            var season = await _db.Seasons.FindAsync(seasonId)
                ?? throw new KeyNotFoundException($"Season {seasonId} not found.");

            if (season.IsKnockoutGenerated)
                throw new InvalidOperationException("Knockout bracket already generated for this season.");

            var table = await BuildTableAsync(seasonId);

            if (table.Count < 8)
                throw new InvalidOperationException($"Need at least 8 teams to generate knockout — found {table.Count}.");

            var windowPlaceholder = season.EndDate.AddDays(30);

            // Cup (top 4): 1v4, 2v3
            var cupSemi1 = Semifinal("Cup", 1, seasonId, table[0], table[3]);
            var cupSemi2 = Semifinal("Cup", 2, seasonId, table[1], table[2]);
            _db.KnockoutFixtures.Add(cupSemi1);
            _db.KnockoutFixtures.Add(cupSemi2);
            _db.KnockoutFixtures.Add(FinalShell("Cup", seasonId));

            // Plate (bottom 4): 5v8, 6v7
            var plateSemi1 = Semifinal("Plate", 1, seasonId, table[4], table[7]);
            var plateSemi2 = Semifinal("Plate", 2, seasonId, table[5], table[6]);
            _db.KnockoutFixtures.Add(plateSemi1);
            _db.KnockoutFixtures.Add(plateSemi2);
            _db.KnockoutFixtures.Add(FinalShell("Plate", seasonId));

            season.IsKnockoutGenerated = true;
            await _db.SaveChangesAsync();

            // Create linked Fixture records for all semis (teams are known)
            foreach (var (ko, homeId, awayId) in new[]
            {
                (cupSemi1,   table[0].Id, table[3].Id),
                (cupSemi2,   table[1].Id, table[2].Id),
                (plateSemi1, table[4].Id, table[7].Id),
                (plateSemi2, table[5].Id, table[6].Id),
            })
            {
                var f = CreateKnockoutFixture(ko, homeId, awayId, seasonId, windowPlaceholder);
                _db.Fixtures.Add(f);
                await _db.SaveChangesAsync();
                ko.LinkedFixtureId = f.Id;
            }

            await _db.SaveChangesAsync();
        }

        public async Task<KnockoutBracketDto> GetBracketAsync(int seasonId)
        {
            var season = await _db.Seasons.FindAsync(seasonId);
            if (season == null || !season.IsKnockoutGenerated)
                return new KnockoutBracketDto { Generated = false, SeasonId = seasonId };

            var fixtures = await _db.KnockoutFixtures
                .Include(k => k.HomeTeam)
                .Include(k => k.AwayTeam)
                .Where(k => k.SeasonId == seasonId)
                .ToListAsync();

            var linkedIds = fixtures
                .Where(k => k.IsPlayed && k.LinkedFixtureId.HasValue)
                .Select(k => k.LinkedFixtureId!.Value)
                .ToList();

            var statsByFixture = linkedIds.Any()
                ? (await _db.FixturePlayerStats
                    .Include(s => s.Player)
                    .Where(s => linkedIds.Contains(s.FixtureId) && s.Goals > 0)
                    .ToListAsync())
                    .GroupBy(s => s.FixtureId)
                    .ToDictionary(g => g.Key, g => g.ToList())
                : new Dictionary<int, List<FixturePlayerStat>>();

            return new KnockoutBracketDto
            {
                Generated = true,
                SeasonId = seasonId,
                SeasonNumber = season.SeasonNumber,
                Fixtures = fixtures.Select(k =>
                {
                    var stats = k.LinkedFixtureId.HasValue && statsByFixture.TryGetValue(k.LinkedFixtureId.Value, out var s) ? s : new();
                    return new KnockoutFixtureDto
                    {
                        Id = k.Id,
                        Tournament = k.Tournament,
                        Round = k.Round,
                        Slot = k.Slot,
                        HomeTeamName = k.HomeTeam?.Name ?? "TBD",
                        AwayTeamName = k.AwayTeam?.Name ?? "TBD",
                        HomeScore = k.HomeScore,
                        AwayScore = k.AwayScore,
                        IsPlayed = k.IsPlayed,
                        LinkedFixtureId = k.LinkedFixtureId,
                        HomeTeamId = k.HomeTeamId,
                        AwayTeamId = k.AwayTeamId,
                        HomeGoalScorers = stats
                            .Where(s => s.Player?.TeamId == k.HomeTeamId)
                            .Select(s => new KnockoutGoalScorerDto { PlayerName = s.Player!.Name, Goals = s.Goals })
                            .ToList(),
                        AwayGoalScorers = stats
                            .Where(s => s.Player?.TeamId == k.AwayTeamId)
                            .Select(s => new KnockoutGoalScorerDto { PlayerName = s.Player!.Name, Goals = s.Goals })
                            .ToList(),
                    };
                }).ToList()
            };
        }

        public async Task TryFinalizeFromStatsAsync(int linkedFixtureId)
        {
            var koFixture = await _db.KnockoutFixtures
                .FirstOrDefaultAsync(k => k.LinkedFixtureId == linkedFixtureId);

            if (koFixture == null || koFixture.IsPlayed) return;

            var fixture = await _db.Fixtures.FindAsync(linkedFixtureId);
            if (fixture == null) return;

            // Determine if both teams have submitted stats
            var teamsWithStats = await _db.FixturePlayerStats
                .Where(s => s.FixtureId == linkedFixtureId)
                .Join(_db.Players, s => s.PlayerId, p => p.Id, (s, p) => p.TeamId)
                .Distinct()
                .ToListAsync();

            if (!teamsWithStats.Contains(fixture.HomeTeamId) ||
                !teamsWithStats.Contains(fixture.AwayTeamId))
                return;

            int homeScore = fixture.HomeScore;
            int awayScore = fixture.AwayScore;

            if (homeScore == awayScore) return;

            koFixture.HomeScore = homeScore;
            koFixture.AwayScore = awayScore;
            koFixture.IsPlayed = true;

            if (koFixture.Round == "Semifinal")
                await AdvanceWinnerToFinalAsync(koFixture, homeScore, awayScore);

            await _db.SaveChangesAsync();
        }

        public async Task SubmitResultAsync(int knockoutFixtureId, int homeScore, int awayScore)
        {
            if (homeScore == awayScore)
                throw new InvalidOperationException("Knockout fixtures cannot end in a draw.");

            var fixture = await _db.KnockoutFixtures
                .Include(k => k.HomeTeam)
                .Include(k => k.AwayTeam)
                .FirstOrDefaultAsync(k => k.Id == knockoutFixtureId)
                ?? throw new KeyNotFoundException($"Knockout fixture {knockoutFixtureId} not found.");

            fixture.HomeScore = homeScore;
            fixture.AwayScore = awayScore;
            fixture.IsPlayed = true;

            // Sync scores to the linked Fixture so stats page shows correct score
            if (fixture.LinkedFixtureId.HasValue)
            {
                var linked = await _db.Fixtures.FindAsync(fixture.LinkedFixtureId.Value);
                if (linked != null)
                {
                    linked.HomeScore = homeScore;
                    linked.AwayScore = awayScore;
                    linked.IsPlayed = true;
                }
            }

            if (fixture.Round == "Semifinal")
                await AdvanceWinnerToFinalAsync(fixture, homeScore, awayScore);

            await _db.SaveChangesAsync();
        }

        private async Task AdvanceWinnerToFinalAsync(KnockoutFixture semi, int homeScore, int awayScore)
        {
            var final = await _db.KnockoutFixtures.FirstOrDefaultAsync(k =>
                k.SeasonId == semi.SeasonId &&
                k.Tournament == semi.Tournament &&
                k.Round == "Final");

            if (final == null) return;

            int? winnerId = homeScore > awayScore ? semi.HomeTeamId : semi.AwayTeamId;

            if (semi.Slot == 1)
                final.HomeTeamId = winnerId;
            else
                final.AwayTeamId = winnerId;

            // Once both finalists are known, create the linked Fixture
            if (final.HomeTeamId.HasValue && final.AwayTeamId.HasValue && final.LinkedFixtureId == null)
            {
                var season = await _db.Seasons.FindAsync(final.SeasonId);
                var window = season?.EndDate.AddDays(60) ?? DateTime.UtcNow.AddMonths(3);
                var f = CreateKnockoutFixture(final, final.HomeTeamId.Value, final.AwayTeamId.Value, final.SeasonId, window);
                _db.Fixtures.Add(f);
                await _db.SaveChangesAsync();
                final.LinkedFixtureId = f.Id;
            }
        }

        private KnockoutFixture Semifinal(string tournament, int slot, int seasonId, (int Id, string Name) home, (int Id, string Name) away)
            => new()
            {
                SeasonId = seasonId,
                Tournament = tournament,
                Round = "Semifinal",
                Slot = slot,
                HomeTeamId = home.Id,
                AwayTeamId = away.Id
            };

        private KnockoutFixture FinalShell(string tournament, int seasonId)
            => new()
            {
                SeasonId = seasonId,
                Tournament = tournament,
                Round = "Final",
                Slot = 1,
                HomeTeamId = null,
                AwayTeamId = null
            };

        private static Fixture CreateKnockoutFixture(KnockoutFixture ko, int homeTeamId, int awayTeamId, int seasonId, DateTime window)
            => new()
            {
                HomeTeamId = homeTeamId,
                AwayTeamId = awayTeamId,
                SeasonId = seasonId,
                IsKnockout = true,
                KnockoutTournament = ko.Tournament,
                KnockoutRound = ko.Round,
                KnockoutSlot = ko.Slot,
                MatchNumber = 0,
                WindowStart = window,
                WindowEnd = window.AddDays(14),
            };

        private async Task<List<(int Id, string Name)>> BuildTableAsync(int seasonId)
        {
            var teams = await _db.Teams
                .Where(t => _db.Fixtures.Any(f =>
                    f.SeasonId == seasonId &&
                    (f.HomeTeamId == t.Id || f.AwayTeamId == t.Id)))
                .ToListAsync();

            var fixtures = await _db.Fixtures
                .Where(f => f.SeasonId == seasonId && f.IsPlayed)
                .ToListAsync();

            var rows = teams.Select(team =>
            {
                int pts = 0, gd = 0, gf = 0;
                foreach (var f in fixtures.Where(f => f.HomeTeamId == team.Id || f.AwayTeamId == team.Id))
                {
                    bool isHome = f.HomeTeamId == team.Id;
                    int scored = isHome ? f.HomeScore : f.AwayScore;
                    int conceded = isHome ? f.AwayScore : f.HomeScore;
                    gf += scored;
                    gd += scored - conceded;
                    if (scored > conceded) pts += 3;
                    else if (scored == conceded) pts += 1;
                }
                return (team.Id, team.Name, pts, gd, gf);
            })
            .OrderByDescending(r => r.pts)
            .ThenByDescending(r => r.gd)
            .ThenByDescending(r => r.gf)
            .Select(r => (r.Id, r.Name))
            .ToList();

            return rows;
        }
    }
}
