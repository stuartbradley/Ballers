using Ballers.API.Data;
using Ballers.API.Services;
using Ballers.Shared;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Events
{
    /// <summary>Tells the managers involved when one of their games changes.</summary>
    public sealed class FixtureNotifications :
        IDomainEventHandler<FixtureStatsSubmitted>,
        IDomainEventHandler<FixtureRescheduled>,
        IDomainEventHandler<FixtureSidesSwapped>
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;

        public FixtureNotifications(ApplicationDbContext db, INotificationService notifications)
        {
            _db = db;
            _notifications = notifications;
        }

        private async Task<string> TeamName(int teamId)
            => await _db.Teams.Where(t => t.Id == teamId).Select(t => t.Name).FirstOrDefaultAsync() ?? "";

        public async Task HandleAsync(FixtureStatsSubmitted e, CancellationToken ct)
        {
            var msg = $"Stats submitted for {await TeamName(e.HomeTeamId)} vs {await TeamName(e.AwayTeamId)}.";
            var link = $"/fixture/{e.FixtureId}";
            await _notifications.CreateForTeamAsync(e.HomeTeamId, NotificationType.ResultSubmitted, msg, link, e.FixtureId);
            await _notifications.CreateForTeamAsync(e.AwayTeamId, NotificationType.ResultSubmitted, msg, link, e.FixtureId);
        }

        // Only the away side hears: the home manager made the change.
        public async Task HandleAsync(FixtureRescheduled e, CancellationToken ct)
        {
            var kickoff = e.Kickoff?.ToString("dd MMM HH:mm") ?? "TBD";
            var location = string.IsNullOrWhiteSpace(e.Location) ? "TBD" : e.Location;
            await _notifications.CreateForTeamAsync(
                e.AwayTeamId,
                NotificationType.FixtureUpdated,
                $"Fixture vs {await TeamName(e.HomeTeamId)} has been updated — {kickoff} at {location}.",
                $"/fixture/{e.FixtureId}");
        }

        public async Task HandleAsync(FixtureSidesSwapped e, CancellationToken ct)
        {
            // Written in the old order, which is how both sides knew the game.
            var oldHome = await TeamName(e.AwayTeamId);
            var oldAway = await TeamName(e.HomeTeamId);
            var swapped = $"{oldHome} v {oldAway} is now {oldAway} v {oldHome}.";

            await _notifications.CreateForTeamAsync(
                e.HomeTeamId,
                NotificationType.FixtureUpdated,
                $"{swapped} You're at home now, so the time, venue and referee are yours to sort.",
                $"/fixture/{e.FixtureId}?tab=setup",
                e.FixtureId);

            // The old home side made the change themselves unless an admin did.
            if (e.ByAdmin)
                await _notifications.CreateForTeamAsync(
                    e.AwayTeamId,
                    NotificationType.FixtureUpdated,
                    $"{swapped} An admin swapped it; you're away now.",
                    $"/fixture/{e.FixtureId}",
                    e.FixtureId);
        }
    }
}
