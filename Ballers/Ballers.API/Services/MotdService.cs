using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Models;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface IMotdService
    {
        Task<List<MotdListItemDto>> GetListAsync();
        Task<MotdDetailDto?> GetByIdAsync(int id);
        Task<List<MotdFixtureOptionDto>> GetFixtureOptionsAsync();
        Task<int> CreateAsync(SaveMotdRequest request);
        Task<bool> UpdateAsync(int id, SaveMotdRequest request);
        Task<bool> DeleteAsync(int id);
    }

    public class MotdService : IMotdService
    {
        private readonly ApplicationDbContext _db;

        public MotdService(ApplicationDbContext db) => _db = db;

        public async Task<List<MotdListItemDto>> GetListAsync()
        {
            return await _db.MatchOfTheDayPosts
                .Include(p => p.Fixture).ThenInclude(f => f!.HomeTeam)
                .Include(p => p.Fixture).ThenInclude(f => f!.AwayTeam)
                .OrderByDescending(p => p.Fixture!.Kickoff ?? p.CreatedAt)
                .ThenByDescending(p => p.CreatedAt)
                .Select(p => new MotdListItemDto
                {
                    Id = p.Id,
                    HomeTeam = p.Fixture!.HomeTeam!.Name,
                    AwayTeam = p.Fixture.AwayTeam!.Name,
                    Date = p.Fixture.Kickoff,
                    Location = p.Fixture.Location,
                    Postcode = p.Fixture.Postcode,
                    CoverImageBase64 = p.CoverImageBase64
                })
                .ToListAsync();
        }

        public async Task<MotdDetailDto?> GetByIdAsync(int id)
        {
            var post = await _db.MatchOfTheDayPosts
                .Include(p => p.Fixture).ThenInclude(f => f!.HomeTeam)
                .Include(p => p.Fixture).ThenInclude(f => f!.AwayTeam)
                .Include(p => p.Photos)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return null;

            return new MotdDetailDto
            {
                Id = post.Id,
                FixtureId = post.FixtureId,
                HomeTeam = post.Fixture!.HomeTeam!.Name,
                AwayTeam = post.Fixture.AwayTeam!.Name,
                Date = post.Fixture.Kickoff,
                Location = post.Fixture.Location,
                Postcode = post.Fixture.Postcode,
                CoverImageBase64 = post.CoverImageBase64,
                Body = post.Body,
                Photos = post.Photos.OrderBy(ph => ph.SortOrder).Select(ph => ph.ImageBase64).ToList()
            };
        }

        public async Task<List<MotdFixtureOptionDto>> GetFixtureOptionsAsync()
        {
            return await _db.Fixtures
                .Include(f => f.HomeTeam)
                .Include(f => f.AwayTeam)
                .OrderByDescending(f => f.Kickoff)
                .Select(f => new MotdFixtureOptionDto
                {
                    FixtureId = f.Id,
                    Label = f.HomeTeam!.Name + " vs " + f.AwayTeam!.Name +
                            (f.Kickoff != null ? " — " + f.Kickoff.Value.ToString("dd MMM yyyy") : "")
                })
                .ToListAsync();
        }

        public async Task<int> CreateAsync(SaveMotdRequest request)
        {
            var post = new MatchOfTheDayPost
            {
                FixtureId = request.FixtureId,
                CoverImageBase64 = request.CoverImageBase64,
                Body = request.Body?.Trim() ?? "",
                CreatedAt = DateTime.UtcNow,
                Photos = BuildPhotos(request.Photos)
            };

            _db.MatchOfTheDayPosts.Add(post);
            await _db.SaveChangesAsync();
            return post.Id;
        }

        public async Task<bool> UpdateAsync(int id, SaveMotdRequest request)
        {
            var post = await _db.MatchOfTheDayPosts
                .Include(p => p.Photos)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return false;

            post.FixtureId = request.FixtureId;
            post.CoverImageBase64 = request.CoverImageBase64;
            post.Body = request.Body?.Trim() ?? "";

            // Replace gallery wholesale — simplest correct behaviour for an edit.
            _db.MatchOfTheDayPhotos.RemoveRange(post.Photos);
            post.Photos = BuildPhotos(request.Photos);

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var post = await _db.MatchOfTheDayPosts.FindAsync(id);
            if (post == null) return false;

            _db.MatchOfTheDayPosts.Remove(post);
            await _db.SaveChangesAsync();
            return true;
        }

        private static List<MatchOfTheDayPhoto> BuildPhotos(List<string>? photos)
        {
            if (photos == null) return new();
            return photos
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select((p, i) => new MatchOfTheDayPhoto { ImageBase64 = p, SortOrder = i })
                .ToList();
        }
    }
}
