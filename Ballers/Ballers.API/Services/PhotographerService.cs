using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Shared;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface IPhotographerService
    {
        Task<List<PhotographerDto>> GetAllAsync();
        Task<Outcome<int>> CreateAsync(PhotographerDto request);
        Task<Outcome> UpdateAsync(int id, PhotographerDto request);
        Task<Outcome> DeleteAsync(int id);
    }

    public class PhotographerService : IPhotographerService
    {
        private readonly ApplicationDbContext _db;

        public PhotographerService(ApplicationDbContext db) => _db = db;

        public async Task<List<PhotographerDto>> GetAllAsync()
            => await _db.Photographers
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .Select(p => new PhotographerDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Website = p.Website,
                    Instagram = p.Instagram,
                    Facebook = p.Facebook,
                    Email = p.Email,
                    Bio = p.Bio,
                    LogoUrl = p.LogoUrl
                })
                .ToListAsync();

        public async Task<Outcome<int>> CreateAsync(PhotographerDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Outcome.Invalid("A name is required.");

            var photographer = new Photographer();
            Apply(photographer, request);

            _db.Photographers.Add(photographer);
            await _db.SaveChangesAsync();
            return photographer.Id;
        }

        public async Task<Outcome> UpdateAsync(int id, PhotographerDto request)
        {
            var photographer = await _db.Photographers.FindAsync(id);
            if (photographer == null) return Outcome.NotFound();

            if (string.IsNullOrWhiteSpace(request.Name))
                return Outcome.Invalid("A name is required.");

            Apply(photographer, request);
            await _db.SaveChangesAsync();
            return Outcome.Ok;
        }

        public async Task<Outcome> DeleteAsync(int id)
        {
            var photographer = await _db.Photographers.FindAsync(id);
            if (photographer == null) return Outcome.NotFound();

            // Galleries keep their photos and simply lose the credit — see the
            // SetNull relationship in ApplicationDbContext.
            _db.Photographers.Remove(photographer);
            await _db.SaveChangesAsync();
            return Outcome.Ok;
        }

        private static void Apply(Photographer p, PhotographerDto request)
        {
            p.Name = request.Name.Trim();
            p.Website = Clean(request.Website);
            p.Instagram = Clean(request.Instagram);
            p.Facebook = Clean(request.Facebook);
            p.Email = Clean(request.Email);
            p.Bio = Clean(request.Bio);
            p.LogoUrl = Clean(request.LogoUrl);
        }

        private static string? Clean(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
