using Ballers.API.Data;
using Ballers.Shared;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services;

public interface INotificationChecker
{
    Task RunChecksAsync();
}

public class NotificationChecker : INotificationChecker
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;

    public NotificationChecker(ApplicationDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task RunChecksAsync()
    {
        var now = DateTime.UtcNow;

        var upcoming = await _db.Fixtures
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .Where(f => !f.IsPlayed && f.Kickoff.HasValue && f.Kickoff > now)
            .ToListAsync();

        foreach (var fixture in upcoming)
        {
            var kickoff = fixture.Kickoff!.Value;
            var hoursUntil = (kickoff - now).TotalHours;

            if (hoursUntil <= 72 && string.IsNullOrEmpty(fixture.Location))
            {
                await _notifications.CreateForTeamAsync(
                    fixture.HomeTeamId,
                    NotificationType.NoLocationSet,
                    $"No location set for your home fixture vs {fixture.AwayTeam?.Name ?? "Away Team"} on {kickoff:dd MMM}.",
                    $"/fixture/{fixture.Id}",
                    fixture.Id);
            }

            if (hoursUntil <= 48 && fixture.RefereeId == null)
            {
                await _notifications.CreateForTeamAsync(
                    fixture.HomeTeamId,
                    NotificationType.NoRefereeSet,
                    $"No referee assigned for your home fixture vs {fixture.AwayTeam?.Name ?? "Away Team"} on {kickoff:dd MMM}.",
                    $"/fixture/{fixture.Id}",
                    fixture.Id);
            }

            if (hoursUntil <= 24)
            {
                var homeSquadSet = await _db.FixturePlayers
                    .Include(fp => fp.Player)
                    .AnyAsync(fp => fp.FixtureId == fixture.Id && fp.Player!.TeamId == fixture.HomeTeamId);

                if (!homeSquadSet)
                    await _notifications.CreateForTeamAsync(
                        fixture.HomeTeamId,
                        NotificationType.NoSquadSet,
                        $"Squad not set for your fixture vs {fixture.AwayTeam?.Name ?? "Away Team"} tomorrow.",
                        $"/fixture/{fixture.Id}",
                        fixture.Id);

                var awaySquadSet = await _db.FixturePlayers
                    .Include(fp => fp.Player)
                    .AnyAsync(fp => fp.FixtureId == fixture.Id && fp.Player!.TeamId == fixture.AwayTeamId);

                if (!awaySquadSet)
                    await _notifications.CreateForTeamAsync(
                        fixture.AwayTeamId,
                        NotificationType.NoSquadSet,
                        $"Squad not set for your fixture vs {fixture.HomeTeam?.Name ?? "Home Team"} tomorrow.",
                        $"/fixture/{fixture.Id}",
                        fixture.Id);
            }
        }

        var overdue = await _db.Fixtures
            .Include(f => f.HomeTeam)
            .Include(f => f.AwayTeam)
            .Where(f => !f.IsPlayed && f.Kickoff.HasValue && f.Kickoff < now.AddHours(-48))
            .ToListAsync();

        foreach (var fixture in overdue)
        {
            var msg = $"Stats not yet entered for {fixture.HomeTeam?.Name ?? "Home"} vs {fixture.AwayTeam?.Name ?? "Away"} on {fixture.Kickoff:dd MMM}.";
            var link = $"/fixture/{fixture.Id}";
            await _notifications.CreateForTeamAsync(fixture.HomeTeamId, NotificationType.NoStatsEntered, msg, link, fixture.Id);
            await _notifications.CreateForTeamAsync(fixture.AwayTeamId, NotificationType.NoStatsEntered, msg, link, fixture.Id);
        }
    }
}
