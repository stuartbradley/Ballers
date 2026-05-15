using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Models;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface IFairplayService
    {
        Task<FairplayFixtureDto?> GetRatingsAsync(int fixtureId);
        Task SubmitRatingsAsync(int fixtureId, int homeRating, int awayRating);
        Task<List<FairplayTableRowDto>> GetTableAsync(int seasonId);
    }

    public class FairplayService : IFairplayService
    {
        private readonly ApplicationDbContext _db;

        public FairplayService(ApplicationDbContext db) => _db = db;

        public async Task<FairplayFixtureDto?> GetRatingsAsync(int fixtureId)
        {
            var fixture = await _db.Fixtures.FindAsync(fixtureId);
            if (fixture == null) return null;

            var ratings = await _db.FairplayRatings
                .Where(r => r.FixtureId == fixtureId)
                .ToListAsync();

            return new FairplayFixtureDto
            {
                HomeRating = ratings.FirstOrDefault(r => r.TeamId == fixture.HomeTeamId)?.Rating,
                AwayRating = ratings.FirstOrDefault(r => r.TeamId == fixture.AwayTeamId)?.Rating
            };
        }

        public async Task SubmitRatingsAsync(int fixtureId, int homeRating, int awayRating)
        {
            if (homeRating < 1 || homeRating > 10 || awayRating < 1 || awayRating > 10)
                throw new ArgumentException("Ratings must be between 1 and 10.");

            var fixture = await _db.Fixtures.FindAsync(fixtureId)
                ?? throw new KeyNotFoundException($"Fixture {fixtureId} not found.");

            await UpsertRating(fixtureId, fixture.HomeTeamId, homeRating);
            await UpsertRating(fixtureId, fixture.AwayTeamId, awayRating);
            await _db.SaveChangesAsync();
        }

        public async Task<List<FairplayTableRowDto>> GetTableAsync(int seasonId)
        {
            var ratings = await _db.FairplayRatings
                .Include(r => r.Fixture)
                .Include(r => r.Team)
                .Where(r => r.Fixture!.SeasonId == seasonId)
                .ToListAsync();

            var rows = ratings
                .GroupBy(r => new { r.TeamId, TeamName = r.Team!.Name })
                .Select(g => new FairplayTableRowDto
                {
                    Team = g.Key.TeamName,
                    Rated = g.Count(),
                    TotalRating = g.Sum(r => r.Rating),
                    AverageRating = Math.Round(g.Average(r => r.Rating), 2)
                })
                .OrderByDescending(r => r.AverageRating)
                .ThenByDescending(r => r.TotalRating)
                .ToList();

            for (int i = 0; i < rows.Count; i++)
                rows[i].Position = i + 1;

            return rows;
        }

        private async Task UpsertRating(int fixtureId, int teamId, int rating)
        {
            var existing = await _db.FairplayRatings
                .FirstOrDefaultAsync(r => r.FixtureId == fixtureId && r.TeamId == teamId);

            if (existing != null)
                existing.Rating = rating;
            else
                _db.FairplayRatings.Add(new FairplayRating { FixtureId = fixtureId, TeamId = teamId, Rating = rating });
        }
    }
}
