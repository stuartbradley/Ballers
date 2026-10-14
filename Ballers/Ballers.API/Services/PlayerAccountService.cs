using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface IPlayerAccountService
    {
        Task<List<ClaimablePlayerDto>> GetClaimablePlayersAsync(int teamId);
        Task RegisterAsync(PlayerSignupRequest request);
        Task<PlayerAccountStatusDto> GetMyStatusAsync(string userId);
        Task<List<PlayerAccountRequestDto>> GetRequestsAsync(string actingUserId);
        Task ApproveAsync(int requestId, string actingUserId);
        Task RejectAsync(int requestId, string actingUserId);
    }

    public class PlayerAccountService : IPlayerAccountService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILeagueSettingsService _settings;

        public PlayerAccountService(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ILeagueSettingsService settings)
        {
            _db = db;
            _userManager = userManager;
            _settings = settings;
        }

        public async Task<List<ClaimablePlayerDto>> GetClaimablePlayersAsync(int teamId)
        {
            var players = await _db.Players
                .Where(p => p.TeamId == teamId && p.IsActive)
                .OrderBy(p => p.Number)
                .ToListAsync();

            var ids = players.Select(p => p.Id).ToList();

            // A squad place is taken once somebody holds it, or has a request in
            // for it that nobody has answered yet.
            var claimed = await _db.Users
                .Where(u => u.PlayerId != null && ids.Contains(u.PlayerId.Value))
                .Select(u => u.PlayerId!.Value)
                .ToListAsync();

            var requested = await _db.PlayerAccountRequests
                .Where(r => r.Status == PlayerAccountRequestStatus.Pending
                         && r.RequestedPlayerId != null
                         && ids.Contains(r.RequestedPlayerId.Value))
                .Select(r => r.RequestedPlayerId!.Value)
                .ToListAsync();

            var unavailable = claimed.Concat(requested).ToHashSet();

            return players.Select(p => new ClaimablePlayerDto
            {
                Id = p.Id,
                Name = p.Name,
                Number = p.Number,
                Position = p.Position,
                IsClaimable = !unavailable.Contains(p.Id)
            }).ToList();
        }

        public async Task RegisterAsync(PlayerSignupRequest request)
        {
            var settings = await _settings.GetSettingsEntityAsync();
            if (!settings.PlayerSignupEnabled)
                throw new InvalidOperationException("Player sign-up is currently closed.");

            if (string.IsNullOrWhiteSpace(request.Email))
                throw new InvalidOperationException("An email address is required.");
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new InvalidOperationException("A name is required.");

            var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == request.TeamId)
                ?? throw new KeyNotFoundException("Team not found.");

            if (request.PlayerId != null)
            {
                var claimable = await GetClaimablePlayersAsync(team.Id);
                var place = claimable.FirstOrDefault(p => p.Id == request.PlayerId.Value)
                    ?? throw new InvalidOperationException("That player is not in this team's squad.");
                if (!place.IsClaimable)
                    throw new InvalidOperationException("Somebody has already claimed that squad place.");
            }

            using var tx = await _db.Database.BeginTransactionAsync();

            // No role, no TeamId and no PlayerId: until a manager approves, this
            // account can do nothing but see that it is waiting. TeamId stays null
            // even after approval, so players never land in a manager's team
            // notification fan-out.
            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                await tx.RollbackAsync();
                throw new InvalidOperationException(
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            _db.PlayerAccountRequests.Add(new PlayerAccountRequest
            {
                UserId = user.Id,
                RequestedTeamId = team.Id,
                RequestedPlayerId = request.PlayerId,
                RequestedName = request.Name.Trim(),
                Status = PlayerAccountRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        public async Task<PlayerAccountStatusDto> GetMyStatusAsync(string userId)
        {
            var user = await _db.Users
                .Include(u => u.Player)!.ThenInclude(p => p!.Team)
                .FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new KeyNotFoundException("Account not found.");

            var request = await _db.PlayerAccountRequests
                .Include(r => r.RequestedTeam)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            return new PlayerAccountStatusDto
            {
                IsApproved = user.PlayerId != null,
                HasPendingRequest = request?.Status == PlayerAccountRequestStatus.Pending,
                WasRejected = request?.Status == PlayerAccountRequestStatus.Rejected,
                TeamName = user.Player?.Team?.Name ?? request?.RequestedTeam?.Name,
                RequestedName = request?.RequestedName
            };
        }

        public async Task<List<PlayerAccountRequestDto>> GetRequestsAsync(string actingUserId)
        {
            var acting = await _db.Users.FirstOrDefaultAsync(u => u.Id == actingUserId)
                ?? throw new KeyNotFoundException("Account not found.");

            var query = _db.PlayerAccountRequests
                .Include(r => r.User)
                .Include(r => r.RequestedTeam)
                .Include(r => r.RequestedPlayer)
                .Where(r => r.Status == PlayerAccountRequestStatus.Pending);

            // An admin sees every queue; a manager only ever sees their own team's.
            if (!acting.IsAdmin)
            {
                if (acting.TeamId == null)
                    throw new UnauthorizedAccessException("This account does not manage a team.");
                query = query.Where(r => r.RequestedTeamId == acting.TeamId.Value);
            }

            var requests = await query.OrderBy(r => r.CreatedAt).ToListAsync();

            return requests.Select(r => new PlayerAccountRequestDto
            {
                Id = r.Id,
                Email = r.User?.Email ?? "",
                TeamId = r.RequestedTeamId,
                TeamName = r.RequestedTeam?.Name,
                PlayerId = r.RequestedPlayerId,
                PlayerName = r.RequestedPlayer?.Name,
                RequestedName = r.RequestedName,
                Status = (PlayerAccountRequestStatusDto)r.Status,
                CreatedAt = r.CreatedAt
            }).ToList();
        }

        public async Task ApproveAsync(int requestId, string actingUserId)
        {
            var settings = await _settings.GetSettingsEntityAsync();

            using var tx = await _db.Database.BeginTransactionAsync();

            var request = await LoadForResolutionAsync(requestId, actingUserId);

            var player = request.RequestedPlayerId != null
                ? await _db.Players.FirstOrDefaultAsync(p =>
                      p.Id == request.RequestedPlayerId.Value && p.TeamId == request.RequestedTeamId)
                  ?? throw new KeyNotFoundException("That squad place no longer exists.")
                : await CreatePlayerForRequestAsync(request);

            // Re-checked inside the transaction: two managers approving two
            // requests for the same shirt must not both succeed.
            var alreadyHeld = await _db.Users.AnyAsync(u => u.PlayerId == player.Id);
            if (alreadyHeld)
                throw new InvalidOperationException("Somebody already holds that squad place.");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId)
                ?? throw new KeyNotFoundException("The requesting account no longer exists.");

            user.PlayerId = player.Id;

            // A card only exists once there is somebody to own it, and it is where
            // the signup points land.
            if (!await _db.PlayerCards.AnyAsync(c => c.PlayerId == player.Id))
            {
                _db.PlayerCards.Add(new PlayerCardStats
                {
                    PlayerId = player.Id,
                    UnspentPoints = settings.SignupPointsGrant,
                    TotalPointsEarned = settings.SignupPointsGrant
                });
            }

            request.Status = PlayerAccountRequestStatus.Approved;
            request.ResolvedAt = DateTime.UtcNow;
            request.ResolvedByUserId = actingUserId;

            await _db.SaveChangesAsync();

            var roleResult = await _userManager.AddToRoleAsync(user, "Player");
            if (!roleResult.Succeeded)
            {
                await tx.RollbackAsync();
                throw new InvalidOperationException(
                    string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            }

            await tx.CommitAsync();
        }

        public async Task RejectAsync(int requestId, string actingUserId)
        {
            var request = await LoadForResolutionAsync(requestId, actingUserId);

            request.Status = PlayerAccountRequestStatus.Rejected;
            request.ResolvedAt = DateTime.UtcNow;
            request.ResolvedByUserId = actingUserId;

            await _db.SaveChangesAsync();
        }

        private async Task<Player> CreatePlayerForRequestAsync(PlayerAccountRequest request)
        {
            // Signing up as somebody not on the roster creates the squad row. The
            // number is left at 0 for the manager to set, rather than guessing one
            // that might already be worn.
            var player = new Player
            {
                Name = request.RequestedName,
                TeamId = request.RequestedTeamId,
                Number = 0,
                IsActive = true
            };

            _db.Players.Add(player);
            await _db.SaveChangesAsync();
            return player;
        }

        private async Task<PlayerAccountRequest> LoadForResolutionAsync(int requestId, string actingUserId)
        {
            var acting = await _db.Users.FirstOrDefaultAsync(u => u.Id == actingUserId)
                ?? throw new KeyNotFoundException("Account not found.");

            var request = await _db.PlayerAccountRequests
                .FirstOrDefaultAsync(r => r.Id == requestId)
                ?? throw new KeyNotFoundException("Request not found.");

            if (request.Status != PlayerAccountRequestStatus.Pending)
                throw new InvalidOperationException("That request has already been dealt with.");

            // A manager may only answer requests aimed at their own team.
            if (!acting.IsAdmin && request.RequestedTeamId != acting.TeamId)
                throw new UnauthorizedAccessException("That request is for another team.");

            return request;
        }
    }
}
