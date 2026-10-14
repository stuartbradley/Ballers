using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Shared;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface IAdminOverviewService
    {
        Task<AdminOverviewDto> GetAsync(DateTime now);
    }

    /// <summary>
    /// Builds the admin overview: the active season's progress, the things in it
    /// that need an admin's attention, and the league's recent activity.
    /// </summary>
    public class AdminOverviewService : IAdminOverviewService
    {
        // How far ahead to look for upcoming fixtures that aren't ready.
        private static readonly TimeSpan LookAhead = TimeSpan.FromDays(14);

        // How far back the activity feed reaches, and how long it can be.
        private static readonly TimeSpan ActivityWindow = TimeSpan.FromDays(21);
        private const int ActivityLimit = 12;

        private readonly ApplicationDbContext _db;

        public AdminOverviewService(ApplicationDbContext db) => _db = db;

        public async Task<AdminOverviewDto> GetAsync(DateTime now)
        {
            var dto = new AdminOverviewDto();

            var settings = await _db.LeagueSettings.AsNoTracking().FirstOrDefaultAsync() ?? new LeagueSetting();
            dto.PlayersLocked = settings.PlayersLocked;
            dto.FixturesLocked = settings.FixturesLocked;
            dto.FixturesHidden = settings.FixturesHidden;
            dto.LastPointsRunAt = settings.LastPointsRunAt;

            dto.Teams = await _db.Teams.CountAsync();
            dto.Players = await _db.Players.CountAsync(p => p.IsActive);
            dto.PlayersWithCards = await _db.PlayerCards.CountAsync(c => c.Player!.IsActive);
            dto.PendingSignups = await _db.PlayerAccountRequests.CountAsync(r => r.Status == PlayerAccountRequestStatus.Pending);

            var season = await _db.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.IsActive);
            if (season != null)
            {
                dto.SeasonNumber = season.SeasonNumber;
                dto.SeasonStart = season.StartDate;
                dto.SeasonEnd = season.EndDate;

                var fixtures = await _db.Fixtures
                    .AsNoTracking()
                    .Where(f => f.SeasonId == season.Id)
                    .Select(f => new FixtureRow(
                        f.Id, f.HomeTeam!.Name, f.AwayTeam!.Name, f.Kickoff, f.WindowStart, f.WindowEnd,
                        f.IsPlayed, f.HomeStatsSubmitted, f.AwayStatsSubmitted, f.RefereeId))
                    .ToListAsync();

                AddProgress(dto, fixtures, now);
                AddFixtureAttention(dto, fixtures, now);
            }

            if (dto.PendingSignups > 0)
            {
                dto.Attention.Add(new AdminAttentionDto
                {
                    Kind = "signup",
                    Title = dto.PendingSignups == 1 ? "1 player waiting for approval" : $"{dto.PendingSignups} players waiting for approval",
                    Detail = "Their managers approve them from My Squad.",
                    Link = "/admin/accounts?role=Pending"
                });
            }

            dto.Activity = await GetActivityAsync(settings, now);
            return dto;
        }

        private record FixtureRow(
            int Id, string Home, string Away, DateTime? Kickoff, DateTime WindowStart, DateTime WindowEnd,
            bool IsPlayed, bool HomeSubmitted, bool AwaySubmitted, int? RefereeId)
        {
            // When the game is (or was) due: its kickoff, or failing that the
            // end of its match week.
            public DateTime DueBy => Kickoff ?? WindowEnd;
            public string Name => $"{Home} v {Away}";
        }

        private static void AddProgress(AdminOverviewDto dto, List<FixtureRow> fixtures, DateTime now)
        {
            dto.FixturesTotal = fixtures.Count;
            dto.FixturesPlayed = fixtures.Count(f => f.IsPlayed);

            var weeks = fixtures
                .GroupBy(f => f.WindowStart)
                .OrderBy(g => g.Key)
                .Select(g => (Start: g.Key, End: g.Max(f => f.WindowEnd)))
                .ToList();

            dto.TotalWeeks = weeks.Count;
            if (weeks.Count == 0) return;

            // The week open now, else the next to open, else the last.
            var current = weeks.FindIndex(w => w.End >= now);
            dto.CurrentWeek = current >= 0 ? current + 1 : weeks.Count;
        }

        private static void AddFixtureAttention(AdminOverviewDto dto, List<FixtureRow> fixtures, DateTime now)
        {
            var unplayed = fixtures.Where(f => !f.IsPlayed).ToList();

            // Due and half in: one side has sent its stats, the other hasn't.
            foreach (var f in unplayed
                         .Where(f => f.DueBy < now && f.HomeSubmitted != f.AwaySubmitted)
                         .OrderBy(f => f.DueBy))
            {
                dto.Attention.Add(new AdminAttentionDto
                {
                    Kind = "stats",
                    Title = f.Name,
                    Detail = $"Waiting for {(f.HomeSubmitted ? f.Away : f.Home)}'s stats",
                    Link = $"/fixture/{f.Id}",
                    When = f.DueBy
                });
            }

            // Due and nothing in from either side.
            foreach (var f in unplayed
                         .Where(f => f.DueBy < now && !f.HomeSubmitted && !f.AwaySubmitted)
                         .OrderBy(f => f.DueBy))
            {
                dto.Attention.Add(new AdminAttentionDto
                {
                    Kind = "result",
                    Title = f.Name,
                    Detail = "No stats from either side yet",
                    Link = $"/fixture/{f.Id}",
                    When = f.DueBy
                });
            }

            // Coming up soon but not ready.
            var soon = unplayed
                .Where(f => f.DueBy >= now && f.WindowStart <= now + LookAhead)
                .OrderBy(f => f.DueBy)
                .ToList();

            foreach (var f in soon.Where(f => f.Kickoff == null))
            {
                dto.Attention.Add(new AdminAttentionDto
                {
                    Kind = "kickoff",
                    Title = f.Name,
                    Detail = "No kickoff time set",
                    Link = $"/fixture/{f.Id}",
                    When = f.WindowStart
                });
            }

            foreach (var f in soon.Where(f => f.RefereeId == null))
            {
                dto.Attention.Add(new AdminAttentionDto
                {
                    Kind = "referee",
                    Title = f.Name,
                    Detail = "No referee assigned",
                    Link = $"/fixture/{f.Id}",
                    When = f.Kickoff ?? f.WindowStart
                });
            }
        }

        private async Task<List<AdminActivityDto>> GetActivityAsync(LeagueSetting settings, DateTime now)
        {
            var since = now - ActivityWindow;
            var items = new List<AdminActivityDto>();

            // Results: each submission notifies several people, so the same
            // message appears once per recipient. One line per result.
            var results = await _db.Notifications
                .AsNoTracking()
                .Where(n => n.Type == NotificationType.ResultSubmitted && n.CreatedAt >= since)
                .Select(n => new { n.Message, n.Link, n.ReferenceId, n.CreatedAt })
                .ToListAsync();
            items.AddRange(results
                .GroupBy(n => (n.ReferenceId, n.Message))
                .Select(g => g.OrderBy(n => n.CreatedAt).First())
                .Select(n => new AdminActivityDto { Kind = "result", Text = n.Message, Link = n.Link, When = n.CreatedAt }));

            var requests = await _db.PlayerAccountRequests
                .AsNoTracking()
                .Where(r => r.CreatedAt >= since || (r.ResolvedAt != null && r.ResolvedAt >= since))
                .Select(r => new { r.RequestedName, Team = r.RequestedTeam!.Name, r.Status, r.CreatedAt, r.ResolvedAt })
                .ToListAsync();
            foreach (var r in requests)
            {
                if (r.CreatedAt >= since)
                    items.Add(new AdminActivityDto
                    {
                        Kind = "signup",
                        Text = $"{r.RequestedName} signed up for {r.Team}",
                        Link = "/admin/accounts?role=Pending",
                        When = r.CreatedAt
                    });
                if (r.ResolvedAt is { } resolved && resolved >= since && r.Status != PlayerAccountRequestStatus.Pending)
                    items.Add(new AdminActivityDto
                    {
                        Kind = "signup",
                        Text = $"{r.RequestedName} {(r.Status == PlayerAccountRequestStatus.Approved ? "approved" : "turned down")} by {r.Team}",
                        Link = "/admin/accounts",
                        When = resolved
                    });
            }

            var galleries = await _db.Galleries
                .AsNoTracking()
                .Where(g => g.CreatedAt >= since)
                .Select(g => new { g.Id, Home = g.Fixture!.HomeTeam!.Name, Away = g.Fixture.AwayTeam!.Name, g.CreatedAt, Photos = g.Photos.Count })
                .ToListAsync();
            items.AddRange(galleries.Select(g => new AdminActivityDto
            {
                Kind = "gallery",
                Text = $"Gallery added: {g.Home} v {g.Away} ({g.Photos} photos)",
                Link = $"/gallery/{g.Id}",
                When = g.CreatedAt
            }));

            var posts = await _db.MatchOfTheDayPosts
                .AsNoTracking()
                .Where(p => p.CreatedAt >= since)
                .Select(p => new { p.Id, Home = p.Fixture!.HomeTeam!.Name, Away = p.Fixture.AwayTeam!.Name, p.CreatedAt })
                .ToListAsync();
            items.AddRange(posts.Select(p => new AdminActivityDto
            {
                Kind = "motd",
                Text = $"Match of the Day posted: {p.Home} v {p.Away}",
                Link = $"/motd/{p.Id}",
                When = p.CreatedAt
            }));

            if (settings.LastPointsRunAt is { } run && run >= since)
                items.Add(new AdminActivityDto
                {
                    Kind = "payout",
                    Text = "Player card points paid out",
                    Link = "/admin/cards",
                    When = run
                });

            return items.OrderByDescending(i => i.When).Take(ActivityLimit).ToList();
        }
    }
}
