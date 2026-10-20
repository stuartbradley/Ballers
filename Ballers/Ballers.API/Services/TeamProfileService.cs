using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Models;
using Ballers.Shared;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface ITeamProfileService
    {
        Task<TeamProfileDto?> GetProfileAsync(int teamId);
        Task<Outcome<TeamProfileDto>> UpdateProfileAsync(int teamId, UpdateTeamProfileRequest request);
        Task<List<PlayerCardDto>?> GetSquadAsync(int teamId);
        // Returns the badge's new URL.
        Task<Outcome<string>> SetBadgeAsync(int teamId, UploadedFile? image);
        Task<Outcome> SetManagerImageAsync(int teamId, string? imageBase64);
    }

    public class TeamProfileService : ITeamProfileService
    {
        private const long MaxImageBytes = 5 * 1024 * 1024;
        private static readonly string[] AllowedImageTypes = ["image/jpeg", "image/png", "image/webp"];

        private readonly ApplicationDbContext _db;
        private readonly IImageModerationService _moderation;
        private readonly IWebHostEnvironment _env;
        private readonly IPlayerCardService _cards;

        public TeamProfileService(
            ApplicationDbContext db,
            IImageModerationService moderation,
            IWebHostEnvironment env,
            IPlayerCardService cards)
        {
            _db = db;
            _moderation = moderation;
            _env = env;
            _cards = cards;
        }

        public async Task<TeamProfileDto?> GetProfileAsync(int teamId)
            => await _db.Teams.FindAsync(teamId) is { } team ? await BuildProfileDto(team) : null;

        public async Task<Outcome<TeamProfileDto>> UpdateProfileAsync(int teamId, UpdateTeamProfileRequest request)
        {
            var team = await _db.Teams.FindAsync(teamId);
            if (team == null) return Outcome.NotFound();

            if (string.IsNullOrWhiteSpace(request.Name))
                return Outcome.Invalid("Team name is required.");

            team.Name = request.Name.Trim();
            team.PhoneNumber = request.PhoneNumber?.Trim();
            team.ManagerName = request.ManagerName?.Trim();
            team.YearFormed = request.YearFormed;
            team.Bio = request.Bio?.Trim();
            team.HomeKitColour = request.HomeKitColour?.Trim();
            team.AwayKitColour = request.AwayKitColour?.Trim();

            await _db.SaveChangesAsync();
            return await BuildProfileDto(team);
        }

        // Current-season wins drive the manager card tier (bronze < 3 ≤ silver < 6 ≤ gold).
        private async Task<TeamProfileDto> BuildProfileDto(Team team)
        {
            var today = DateTime.UtcNow;

            var wins = await _db.Fixtures
                .Where(f => f.IsPlayed
                    && f.Season!.StartDate <= today && f.Season.EndDate >= today
                    && ((f.HomeTeamId == team.Id && f.HomeScore > f.AwayScore)
                     || (f.AwayTeamId == team.Id && f.AwayScore > f.HomeScore)))
                .CountAsync();

            return new TeamProfileDto
            {
                Id = team.Id,
                Name = team.Name,
                PhoneNumber = team.PhoneNumber,
                ManagerName = team.ManagerName,
                YearFormed = team.YearFormed,
                Bio = team.Bio,
                ProfileImageUrl = team.ProfileImageUrl,
                ManagerImageBase64 = team.ManagerImageBase64,
                HomeKitColour = team.HomeKitColour,
                AwayKitColour = team.AwayKitColour,
                Wins = wins
            };
        }

        public async Task<List<PlayerCardDto>?> GetSquadAsync(int teamId)
        {
            if (await _db.Teams.FindAsync(teamId) == null) return null;

            var today = DateTime.UtcNow;

            var players = await _db.Players
                .Where(p => p.TeamId == teamId && p.IsActive)
                .OrderBy(p => p.Number)
                .ToListAsync();

            var playerIds = players.Select(p => p.Id).ToList();

            var stats = await _db.FixturePlayerStats
                .Where(s =>
                    playerIds.Contains(s.PlayerId) &&
                    s.Fixture!.IsPlayed &&
                    s.Fixture.Season!.StartDate <= today &&
                    s.Fixture.Season.EndDate >= today)
                .GroupBy(s => s.PlayerId)
                .Select(g => new
                {
                    PlayerId = g.Key,
                    Goals = g.Sum(x => x.Goals),
                    Assists = g.Sum(x => x.Assists),
                    Motm = g.Count(x => x.ManOfTheMatch),
                    // A clean sheet is an appearance in a played fixture where this
                    // player's team conceded nothing. Counted for every position
                    // here; only goalkeepers and defenders are given the figure below.
                    // Spelled out rather than calling PlayerPositions.IsCleanSheetFor
                    // because EF has to translate this into SQL — if that rule
                    // changes, change it here too.
                    CleanSheets = g.Count(x =>
                        (teamId == x.Fixture!.HomeTeamId && x.Fixture.AwayScore == 0) ||
                        (teamId == x.Fixture!.AwayTeamId && x.Fixture.HomeScore == 0))
                })
                .ToListAsync();

            // Attributes exist only for players who have signed up. The rest come
            // back with nulls, which is what tells the card to render the old way.
            var attributes = await _cards.GetPublicCardsAsync(playerIds);

            return players.Select(p =>
            {
                var s = stats.FirstOrDefault(x => x.PlayerId == p.Id);
                var hasCard = attributes.TryGetValue(p.Id, out var card);
                return new PlayerCardDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Position = p.Position,
                    Number = p.Number,
                    Goals = s?.Goals ?? 0,
                    Assists = s?.Assists ?? 0,
                    Motm = s?.Motm ?? 0,
                    CleanSheets = PlayerPositions.EarnsCleanSheets(p.Position) ? s?.CleanSheets ?? 0 : 0,
                    ImageUrl = StoredImages.PlayerUrl(p.Id, p.ProfileImageBase64),
                    Attributes = hasCard ? card.Attributes : null,
                    Tier = hasCard ? card.Tier : null
                };
            }).ToList();
        }

        public async Task<Outcome<string>> SetBadgeAsync(int teamId, UploadedFile? image)
        {
            var team = await _db.Teams.FindAsync(teamId);
            if (team == null) return Outcome.NotFound();

            if (image == null || image.Length == 0)
                return Outcome.Invalid("No image provided.");

            if (image.Length > MaxImageBytes)
                return Outcome.Invalid("Image must be under 5 MB.");

            if (!AllowedImageTypes.Contains(image.ContentType.ToLower()))
                return Outcome.Invalid("Only JPEG, PNG, and WebP images are accepted.");

            byte[] imageBytes;
            using (var ms = new MemoryStream())
            {
                await using var source = image.OpenRead();
                await source.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }

            if (await _moderation.IsExplicitAsync(imageBytes, image.ContentType))
                return Outcome.Invalid("Image was rejected: explicit or inappropriate content detected.");

            var ext = image.ContentType switch
            {
                "image/png"  => ".png",
                "image/webp" => ".webp",
                _            => ".jpg"
            };

            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var uploadsPath = Path.Combine(webRoot, "uploads", "teams");
            Directory.CreateDirectory(uploadsPath);

            var fileName = $"{teamId}{ext}";
            await File.WriteAllBytesAsync(Path.Combine(uploadsPath, fileName), imageBytes);

            team.ProfileImageUrl = $"/uploads/teams/{fileName}";
            await _db.SaveChangesAsync();
            return team.ProfileImageUrl;
        }

        // Manager photo is stored as a base64 data URL in the DB (same as player photos), not on disk.
        public async Task<Outcome> SetManagerImageAsync(int teamId, string? imageBase64)
        {
            var team = await _db.Teams.FindAsync(teamId);
            if (team == null) return Outcome.NotFound();

            if (string.IsNullOrEmpty(imageBase64))
            {
                team.ManagerImageBase64 = null;
                await _db.SaveChangesAsync();
                return Outcome.Ok;
            }

            // Accept "data:image/png;base64,xxxx" or a raw base64 payload.
            var payload = imageBase64;
            var contentType = "image/png";
            var commaIdx = payload.IndexOf(',');
            if (payload.StartsWith("data:") && commaIdx > 0)
            {
                var header = payload[5..commaIdx];
                var semi = header.IndexOf(';');
                contentType = semi > 0 ? header[..semi] : header;
                payload = payload[(commaIdx + 1)..];
            }

            byte[] imageBytes;
            try { imageBytes = Convert.FromBase64String(payload); }
            catch (FormatException) { return Outcome.Invalid("Invalid image data."); }

            if (imageBytes.Length > MaxImageBytes)
                return Outcome.Invalid("Image must be under 5 MB.");

            if (await _moderation.IsExplicitAsync(imageBytes, contentType))
                return Outcome.Invalid("Image was rejected: explicit or inappropriate content detected.");

            team.ManagerImageBase64 = imageBase64;
            await _db.SaveChangesAsync();
            return Outcome.Ok;
        }
    }
}
