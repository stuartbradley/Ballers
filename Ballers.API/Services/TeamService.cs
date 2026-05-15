using Ballers.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public record TeamSummary(int Id, string Name);

    public interface ITeamService
    {
        Task<List<TeamSummary>> GetTeamsAsync();
    }

    public class TeamService : ITeamService
    {
        private readonly ApplicationDbContext _db;

        public TeamService(ApplicationDbContext db) => _db = db;

        public async Task<List<TeamSummary>> GetTeamsAsync() =>
            await _db.Teams
                .Select(t => new TeamSummary(t.Id, t.Name))
                .ToListAsync();
    }
}
