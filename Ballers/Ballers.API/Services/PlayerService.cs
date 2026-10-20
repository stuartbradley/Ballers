using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Models;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface IPlayerService
    {
        Task<List<PlayerSummary>> GetTeamPlayersAsync(int teamId);
        Task<List<PlayerDto>> GetTeamPlayerDetailsAsync(int teamId);
        Task AddPlayerAsync(int teamId, CreatePlayerRequest request);
        Task<bool> DeactivatePlayerAsync(int playerId, int? requestingTeamId, bool isAdmin);
        Task UploadPlayerImageAsync(int playerId, int? requestingTeamId, bool isAdmin, string imageBase64);
        Task<bool> UpdatePlayerAsync(int playerId, int? requestingTeamId, bool isAdmin, UpdatePlayerRequest request);
    }

    public class PlayerService : IPlayerService
    {
        private readonly ApplicationDbContext _db;

        public PlayerService(ApplicationDbContext db) => _db = db;

        public async Task<List<PlayerSummary>> GetTeamPlayersAsync(int teamId)
        {
            return await _db.Players.AsNoTracking()
                .Where(p => p.TeamId == teamId && p.IsActive)
                .OrderBy(p => p.Number)
                .Select(p => new PlayerSummary(p.Id, p.Name))
                .ToListAsync();
        }

        public async Task<List<PlayerDto>> GetTeamPlayerDetailsAsync(int teamId)
        {
            // The photo's length, not the photo: the list links to it instead.
            var rows = await _db.Players.AsNoTracking()
                .Where(p => p.TeamId == teamId && p.IsActive)
                .OrderBy(p => p.Number)
                .Select(p => new
                {
                    p.Id, p.Name, p.Number, p.Position, p.TeamId,
                    PhotoLength = p.ProfileImageBase64 != null ? p.ProfileImageBase64.Length : (int?)null
                })
                .ToListAsync();

            return rows
                .Select(p => new PlayerDto { Id = p.Id, Name = p.Name, Number = p.Number, Position = p.Position, TeamId = p.TeamId, ImageUrl = StoredImages.PlayerUrl(p.Id, p.PhotoLength) })
                .ToList();
        }

        public async Task AddPlayerAsync(int teamId, CreatePlayerRequest request)
        {
            _db.Players.Add(new Player
            {
                Name = request.Name,
                Number = request.Number,
                TeamId = teamId,
                Position = request.Position
            });

            await _db.SaveChangesAsync();
        }

        public async Task<bool> DeactivatePlayerAsync(int playerId, int? requestingTeamId, bool isAdmin)
        {
            var player = await _db.Players.FindAsync(playerId);
            if (player == null) return false;

            if (!isAdmin && player.TeamId != requestingTeamId)
                throw new UnauthorizedAccessException("You do not have permission to remove this player.");

            player.IsActive = false;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdatePlayerAsync(int playerId, int? requestingTeamId, bool isAdmin, UpdatePlayerRequest request)
        {
            var player = await _db.Players.FindAsync(playerId);
            if (player == null) return false;
            if (!isAdmin && player.TeamId != requestingTeamId)
                throw new UnauthorizedAccessException();

            player.Name = request.Name;
            player.Number = request.Number;
            player.Position = request.Position;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task UploadPlayerImageAsync(int playerId, int? requestingTeamId, bool isAdmin, string imageBase64)
        {
            var player = await _db.Players.FindAsync(playerId);
            if (player == null) throw new KeyNotFoundException();
            if (!isAdmin && player.TeamId != requestingTeamId)
                throw new UnauthorizedAccessException();

            player.ProfileImageBase64 = imageBase64;
            await _db.SaveChangesAsync();
        }
    }
}
