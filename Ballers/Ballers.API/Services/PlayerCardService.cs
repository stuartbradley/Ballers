using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Shared;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface IPlayerCardService
    {
        /// <summary>
        /// The public face of a player's card. Null when the player has no account
        /// yet, which the squad and team pages take as "render the old card".
        /// Never includes the unspent points balance.
        /// </summary>
        Task<(PlayerAttributesDto Attributes, string Tier)?> GetPublicCardAsync(int playerId);

        /// <summary>Public cards for a whole squad, keyed by player id.</summary>
        Task<Dictionary<int, (PlayerAttributesDto Attributes, string Tier)>> GetPublicCardsAsync(IEnumerable<int> playerIds);

        /// <summary>The caller's own card, including their balance.</summary>
        Task<MyPlayerCardDto> GetMyCardAsync(string userId);

        /// <summary>Spend points on attributes. Returns the card as it now stands.</summary>
        Task<MyPlayerCardDto> AllocateAsync(string userId, AllocatePointsRequest request);

        /// <summary>The caller's earned-points history, newest fixture first.</summary>
        Task<List<PlayerPointsAwardDto>> GetMyAwardsAsync(string userId);

        /// <summary>
        /// Whoever earned the most card points in the latest week of credited
        /// fixtures, ties going to the higher overall. Null until anyone has been
        /// paid.
        /// </summary>
        Task<FeaturedCardDto?> GetFeaturedCardAsync();
    }

    public class PlayerCardService : IPlayerCardService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILeagueSettingsService _settings;

        public PlayerCardService(ApplicationDbContext db, ILeagueSettingsService settings)
        {
            _db = db;
            _settings = settings;
        }

        public async Task<(PlayerAttributesDto, string)?> GetPublicCardAsync(int playerId)
        {
            var cards = await GetPublicCardsAsync(new[] { playerId });
            return cards.TryGetValue(playerId, out var card) ? card : null;
        }

        public async Task<Dictionary<int, (PlayerAttributesDto Attributes, string Tier)>> GetPublicCardsAsync(
            IEnumerable<int> playerIds)
        {
            var ids = playerIds.Distinct().ToList();
            if (ids.Count == 0) return new();

            var settings = await _settings.GetSettingsEntityAsync();

            var stats = await _db.PlayerCards
                .Where(s => ids.Contains(s.PlayerId))
                .ToListAsync();

            return stats.ToDictionary(
                s => s.PlayerId,
                s =>
                {
                    var dto = ToAttributesDto(s);
                    var tier = PlayerAttributes.Tier(
                        dto.Overall, settings.SilverOverallThreshold, settings.GoldOverallThreshold);
                    return (dto, tier);
                });
        }

        public async Task<MyPlayerCardDto> GetMyCardAsync(string userId)
        {
            var (player, stats, settings) = await LoadOwnCardAsync(userId);
            return await WithRanksAsync(ToMyCardDto(player, stats, settings), player);
        }

        /// <summary>
        /// Ranks the card's overall against every active player with a card, in
        /// the league and in the player's position. Standard competition ranking:
        /// your rank is one more than the number of cards rated above yours, so
        /// equal ratings share a place.
        /// </summary>
        private async Task<MyPlayerCardDto> WithRanksAsync(MyPlayerCardDto dto, Player player)
        {
            var field = await _db.PlayerCards
                .Where(c => c.Player!.IsActive)
                .Select(c => new
                {
                    c.Player!.Position,
                    Total = c.Finishing + c.Tackling + c.Passing + c.Dribbling + c.Pace + c.Strength
                })
                .ToListAsync();

            // Same rounding as PlayerAttributes.ComputeOverall.
            var overalls = field
                .Select(f => (f.Position, Overall: f.Total / PlayerAttributes.Count))
                .ToList();
            var mine = dto.Attributes.Overall;
            var samePosition = overalls.Where(o => o.Position == player.Position).ToList();

            dto.OverallOf = overalls.Count;
            dto.OverallRank = overalls.Count(o => o.Overall > mine) + 1;
            dto.PositionOf = samePosition.Count;
            dto.PositionRank = samePosition.Count(o => o.Overall > mine) + 1;
            return dto;
        }

        public async Task<List<PlayerPointsAwardDto>> GetMyAwardsAsync(string userId)
        {
            var player = await ResolvePlayerAsync(userId);

            var awards = await _db.PlayerPointsAwards
                .Where(a => a.PlayerId == player.Id)
                .Include(a => a.Fixture)!.ThenInclude(f => f!.HomeTeam)
                .Include(a => a.Fixture)!.ThenInclude(f => f!.AwayTeam)
                .OrderByDescending(a => a.Fixture!.Kickoff ?? a.AwardedAt)
                .ToListAsync();

            return awards.Select(a => new PlayerPointsAwardDto
            {
                FixtureId = a.FixtureId,
                Opponent = a.Fixture == null
                    ? null
                    : a.Fixture.HomeTeamId == player.TeamId
                        ? a.Fixture.AwayTeam?.Name
                        : a.Fixture.HomeTeam?.Name,
                Kickoff = a.Fixture?.Kickoff,
                Points = a.Points,
                AppearancePoints = a.AppearancePoints,
                WinPoints = a.WinPoints,
                GoalPoints = a.GoalPoints,
                AssistPoints = a.AssistPoints,
                CleanSheetPoints = a.CleanSheetPoints,
                MotmPoints = a.MotmPoints
            }).ToList();
        }

        public async Task<FeaturedCardDto?> GetFeaturedCardAsync()
        {
            // Small enough to pull whole: one row per carded player per fixture.
            var awards = await _db.PlayerPointsAwards
                .Where(a => a.Player!.IsActive)
                .Select(a => new { a.PlayerId, a.Points, When = a.Fixture!.Kickoff ?? a.AwardedAt })
                .ToListAsync();
            if (awards.Count == 0) return null;

            // "The week" is the seven days up to the latest credited fixture, not
            // the calendar week, so a quiet week still shows the last real one.
            var weekEnding = awards.Max(a => a.When);
            var weekStart = weekEnding.AddDays(-7);
            var week = awards
                .Where(a => a.When > weekStart)
                .GroupBy(a => a.PlayerId)
                .Select(g => new { PlayerId = g.Key, Points = g.Sum(x => x.Points) })
                .ToList();

            var cards = await GetPublicCardsAsync(week.Select(w => w.PlayerId));
            var pick = week
                .Where(w => cards.ContainsKey(w.PlayerId))
                .OrderByDescending(w => w.Points)
                .ThenByDescending(w => cards[w.PlayerId].Attributes.Overall)
                .ThenBy(w => w.PlayerId)
                .FirstOrDefault();
            if (pick == null) return null;

            var player = await _db.Players
                .Include(p => p.Team)
                .FirstAsync(p => p.Id == pick.PlayerId);
            var (attributes, tier) = cards[pick.PlayerId];

            // The active season, or every season if none is marked active.
            var season = await _db.Seasons.FirstOrDefaultAsync(s => s.IsActive);
            int? seasonId = season?.Id;

            var stats = await _db.FixturePlayerStats
                .Include(s => s.Fixture)
                .Where(s => s.PlayerId == player.Id && s.Fixture!.IsPlayed)
                .Where(s => seasonId == null || s.Fixture!.SeasonId == seasonId)
                .ToListAsync();

            var seasonAwards = await _db.PlayerPointsAwards
                .Where(a => a.PlayerId == player.Id)
                .Where(a => seasonId == null || a.Fixture!.SeasonId == seasonId)
                .ToListAsync();

            // Same rules the points run uses: a result only counts for the side the
            // player is on now.
            bool Won(Fixture f) =>
                player.TeamId == f.HomeTeamId ? f.HomeScore > f.AwayScore
                : player.TeamId == f.AwayTeamId && f.AwayScore > f.HomeScore;

            return new FeaturedCardDto
            {
                PlayerId = player.Id,
                Name = player.Name,
                Number = player.Number,
                Position = player.Position,
                TeamName = player.Team?.Name,
                ImageUrl = StoredImages.PlayerUrl(player.Id, player.ProfileImageBase64),
                Attributes = attributes,
                Tier = tier,
                WeekPoints = pick.Points,
                WeekEnding = weekEnding,
                SeasonNumber = season?.SeasonNumber,
                Played = stats.Count,
                Won = stats.Count(s => Won(s.Fixture!)),
                Goals = stats.Sum(s => s.Goals),
                Assists = stats.Sum(s => s.Assists),
                CleanSheets = stats.Count(s =>
                    PlayerPositions.EarnedCleanSheet(player.Position, s.Fixture!, player.TeamId)),
                GoalPoints = seasonAwards.Sum(a => a.GoalPoints),
                AssistPoints = seasonAwards.Sum(a => a.AssistPoints),
                CleanSheetPoints = seasonAwards.Sum(a => a.CleanSheetPoints),
                SeasonPoints = seasonAwards.Sum(a => a.Points)
            };
        }

        public async Task<MyPlayerCardDto> AllocateAsync(string userId, AllocatePointsRequest request)
        {
            var deltas = new[]
            {
                request.Finishing, request.Tackling, request.Passing,
                request.Dribbling, request.Pace, request.Strength
            };

            // Points only ever go into an attribute, never back out — a spend is
            // permanent, so a negative here would be a way to undo one.
            if (deltas.Any(d => d < 0))
                throw new InvalidOperationException("Points cannot be taken back out of an attribute.");

            var total = deltas.Sum();
            if (total == 0)
                throw new InvalidOperationException("No points were allocated.");

            // The whole spend is one transaction, and the balance is re-read inside
            // it, so two requests arriving together cannot both spend the same
            // points and leave the player with a negative balance.
            using var tx = await _db.Database.BeginTransactionAsync();

            var (player, stats, settings) = await LoadOwnCardAsync(userId);

            if (total > stats.UnspentPoints)
                throw new InvalidOperationException(
                    $"Only {stats.UnspentPoints} point(s) available, but {total} were allocated.");

            var cap = settings.AttributeCap;
            var proposed = new (string Name, int Value)[]
            {
                (PlayerAttributes.Finishing, stats.Finishing + request.Finishing),
                (PlayerAttributes.Tackling,  stats.Tackling  + request.Tackling),
                (PlayerAttributes.Passing,   stats.Passing   + request.Passing),
                (PlayerAttributes.Dribbling, stats.Dribbling + request.Dribbling),
                (PlayerAttributes.Pace,      stats.Pace      + request.Pace),
                (PlayerAttributes.Strength,  stats.Strength  + request.Strength),
            };

            var over = proposed.FirstOrDefault(p => p.Value > cap);
            if (over.Name != null)
                throw new InvalidOperationException($"{over.Name} cannot go above {cap}.");

            stats.Finishing = proposed[0].Value;
            stats.Tackling = proposed[1].Value;
            stats.Passing = proposed[2].Value;
            stats.Dribbling = proposed[3].Value;
            stats.Pace = proposed[4].Value;
            stats.Strength = proposed[5].Value;
            stats.UnspentPoints -= total;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            // Re-ranked after the spend, so the tag moves the moment it is saved.
            return await WithRanksAsync(ToMyCardDto(player, stats, settings), player);
        }

        /// <summary>
        /// Resolves which player the caller is from their own account, never from
        /// anything the client sent — the same rule the fixture endpoints use to
        /// stop a manager acting for another team.
        /// </summary>
        private async Task<Player> ResolvePlayerAsync(string userId)
        {
            var user = await _db.Users
                .Include(u => u.Player)!.ThenInclude(p => p!.Team)
                .FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new KeyNotFoundException("Account not found.");

            if (user.PlayerId == null || user.Player == null)
                throw new InvalidOperationException("This account is not linked to a player yet.");

            return user.Player;
        }

        private async Task<(Player Player, PlayerCardStats Stats, LeagueSetting Settings)> LoadOwnCardAsync(string userId)
        {
            var player = await ResolvePlayerAsync(userId);
            var settings = await _settings.GetSettingsEntityAsync();

            var stats = await _db.PlayerCards.FirstOrDefaultAsync(s => s.PlayerId == player.Id)
                ?? throw new KeyNotFoundException("This player has no card yet.");

            return (player, stats, settings);
        }

        private static PlayerAttributesDto ToAttributesDto(PlayerCardStats s) => new()
        {
            Finishing = s.Finishing,
            Tackling = s.Tackling,
            Passing = s.Passing,
            Dribbling = s.Dribbling,
            Pace = s.Pace,
            Strength = s.Strength,
            Overall = PlayerAttributes.ComputeOverall(s)
        };

        private static MyPlayerCardDto ToMyCardDto(Player player, PlayerCardStats stats, LeagueSetting settings) => new()
        {
            PlayerId = player.Id,
            Name = player.Name,
            Number = player.Number,
            Position = player.Position,
            TeamName = player.Team?.Name,
            ImageUrl = StoredImages.PlayerUrl(player.Id, player.ProfileImageBase64),
            Attributes = ToAttributesDto(stats),
            UnspentPoints = stats.UnspentPoints,
            TotalPointsEarned = stats.TotalPointsEarned,
            AttributeCap = settings.AttributeCap
        };
    }
}
