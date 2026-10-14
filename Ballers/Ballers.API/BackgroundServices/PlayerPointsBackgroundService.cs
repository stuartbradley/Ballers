using Ballers.API.Services;

namespace Ballers.API.BackgroundServices;

/// <summary>
/// Runs the card points payout every Sunday at midnight.
/// <para>
/// It also runs once at startup. That is not belt-and-braces: the API is hosted
/// in-process under IIS, so an app pool that idles out overnight will never see
/// the Sunday tick at all. The startup run means points are never lost, only
/// late — they settle the next time the site is woken up. For the Sunday run to
/// genuinely happen on time, either keep the app pool always running or point a
/// scheduled task at POST api/admin/run-player-points.
/// </para>
/// </summary>
public class PlayerPointsBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PlayerPointsBackgroundService> _logger;

    public PlayerPointsBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<PlayerPointsBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Catch up on anything missed while the process was down before settling
        // into the weekly rhythm.
        await RunAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextSundayMidnight(DateTime.Now);
            _logger.LogInformation(
                "Next player points run due in {Hours:F1} hour(s).", delay.TotalHours);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            await RunAsync(stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var points = scope.ServiceProvider.GetRequiredService<IPlayerPointsService>();
            await points.RunCatchUpAsync(ct);
        }
        catch (Exception ex)
        {
            // Never let a bad run kill the loop — the next one picks up whatever
            // is still outstanding, because the payout is catch-up based.
            _logger.LogError(ex, "Player points run failed.");
        }
    }

    /// <summary>
    /// Time from <paramref name="now"/> until the next Sunday 00:00 local. Exactly
    /// on Sunday midnight this returns a full week rather than zero, so a run that
    /// has just happened does not immediately happen again.
    /// </summary>
    public static TimeSpan TimeUntilNextSundayMidnight(DateTime now)
    {
        var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)now.DayOfWeek + 7) % 7;
        var next = now.Date.AddDays(daysUntilSunday);
        if (next <= now) next = next.AddDays(7);
        return next - now;
    }
}
