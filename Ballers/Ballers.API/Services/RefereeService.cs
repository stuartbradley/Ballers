using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Models;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface IRefereeService
    {
        Task<List<RefereeDto>> GetAllAsync();
        Task<Outcome<RefereeDto>> CreateAsync(SaveRefereeRequest request);
        Task<Outcome<RefereeDto>> UpdateAsync(int id, SaveRefereeRequest request);
        Task<Outcome> DeleteAsync(int id);
    }

    public class RefereeService : IRefereeService
    {
        private readonly ApplicationDbContext _db;

        public RefereeService(ApplicationDbContext db) => _db = db;

        private IQueryable<Referee> WithFixtures()
            => _db.Referees
                .Include(r => r.Fixtures).ThenInclude(f => f.HomeTeam)
                .Include(r => r.Fixtures).ThenInclude(f => f.AwayTeam);

        public async Task<List<RefereeDto>> GetAllAsync()
            => (await WithFixtures().OrderBy(r => r.Name).ToListAsync()).Select(ToDto).ToList();

        public async Task<Outcome<RefereeDto>> CreateAsync(SaveRefereeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Outcome.Invalid("Name is required.");

            var referee = new Referee();
            Apply(referee, request);

            _db.Referees.Add(referee);
            await _db.SaveChangesAsync();
            return ToDto(referee);
        }

        public async Task<Outcome<RefereeDto>> UpdateAsync(int id, SaveRefereeRequest request)
        {
            var referee = await WithFixtures().FirstOrDefaultAsync(r => r.Id == id);
            if (referee == null) return Outcome.NotFound();

            if (string.IsNullOrWhiteSpace(request.Name))
                return Outcome.Invalid("Name is required.");

            Apply(referee, request);
            await _db.SaveChangesAsync();
            return ToDto(referee);
        }

        public async Task<Outcome> DeleteAsync(int id)
        {
            var referee = await _db.Referees.FindAsync(id);
            if (referee == null) return Outcome.NotFound();

            _db.Referees.Remove(referee);
            await _db.SaveChangesAsync();
            return Outcome.Ok;
        }

        private static void Apply(Referee r, SaveRefereeRequest request)
        {
            r.Name        = request.Name.Trim();
            r.PhoneNumber = request.PhoneNumber?.Trim();
            r.Email       = request.Email?.Trim();
            r.Notes       = request.Notes?.Trim();
        }

        private static RefereeDto ToDto(Referee r) => new()
        {
            Id          = r.Id,
            Name        = r.Name,
            PhoneNumber = r.PhoneNumber,
            Email       = r.Email,
            Notes       = r.Notes,
            UpcomingFixtures = r.Fixtures
                .Where(f => !f.IsPlayed)
                .OrderBy(f => f.WindowStart)
                .Take(10)
                .Select(f => new RefereeFixtureDto
                {
                    FixtureId   = f.Id,
                    HomeTeam    = f.HomeTeam?.Name ?? "",
                    AwayTeam    = f.AwayTeam?.Name ?? "",
                    WindowStart = f.WindowStart,
                    WindowEnd   = f.WindowEnd,
                    Kickoff     = f.Kickoff
                }).ToList()
        };
    }
}
