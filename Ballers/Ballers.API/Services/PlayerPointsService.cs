using Ballers.API.Data;
using Ballers.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    /// <summary>What a payout run did, for the admin page and the job log.</summary>
    public record PointsRunResult(int FixturesCredited, int PlayersCredited, int PointsAwarded);

    public interface IPlayerPointsService
    {
        /// <summary>
        /// Credits every played fixture that has not been credited yet. Safe to run
        /// at any time and as often as you like: the ledger's unique index on
        /// (PlayerId, FixtureId) means an already-paid fixture is simply skipped,
        /// so a re-run after a crash, or a late-entered result, settles up rather
        /// than paying twice.
        /// </summary>
        Task<PointsRunResult> RunCatchUpAsync(CancellationToken ct = default);
    }

    public class PlayerPointsService : IPlayerPointsService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILeagueSettingsService _settings;
        private readonly ILogger<PlayerPointsService> _logger;

        public PlayerPointsService(
            ApplicationDbContext db,
            ILeagueSettingsService settings,
            ILogger<PlayerPointsService> logger)
        {
            _db = db;
            _settings = settings;
            _logger = logger;
        }

        public async Task<PointsRunResult> RunCatchUpAsync(CancellationToken ct = default)
        {
            var settings = await _settings.GetSettingsEntityAsync();

            // Only appearances by players who actually have a card are worth
            // looking at — a roster row with no login has nowhere to put points,
            // and crediting it would hand someone a backdated windfall on the day
            // they eventually sign up.
            var pending = await _db.FixturePlayerStats
                .Include(s => s.Fixture)
                .Include(s => s.Player)
                .Where(s => s.Fixture!.IsPlayed)
                .Where(s => _db.PlayerCards.Any(c => c.PlayerId == s.PlayerId))
                .Where(s => !_db.PlayerPointsAwards.Any(a =>
                    a.PlayerId == s.PlayerId && a.FixtureId == s.FixtureId))
                .ToListAsync(ct);

            if (pending.Count == 0)
                return new PointsRunResult(0, 0, 0);

            var playerIds = pending.Select(s => s.PlayerId).Distinct().ToList();
            var cards = await _db.PlayerCards
                .Where(c => playerIds.Contains(c.PlayerId))
                .ToDictionaryAsync(c => c.PlayerId, ct);

            var now = DateTime.UtcNow;
            var awards = new List<PlayerPointsAward>();

            foreach (var stat in pending)
            {
                var fixture = stat.Fixture!;
                var player = stat.Player!;
                if (!cards.TryGetValue(stat.PlayerId, out var card)) continue;

                var award = BuildAward(stat, fixture, player, settings, now);

                // A zero-point award is still written. It settles the fixture so
                // the next run stops reconsidering it, and it gives the player a
                // complete history rather than silent gaps.
                awards.Add(award);

                card.UnspentPoints += award.Points;
                card.TotalPointsEarned += award.Points;
            }

            _db.PlayerPointsAwards.AddRange(awards);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                // The unique index is the backstop if two runs overlap. Nothing is
                // credited in that case, and the next run picks up whatever is
                // genuinely still outstanding.
                _logger.LogWarning(ex,
                    "Player points run could not be saved; it will be retried on the next run.");
                throw;
            }

            await _settings.SetLastPointsRunAsync(now);

            var result = new PointsRunResult(
                awards.Select(a => a.FixtureId).Distinct().Count(),
                awards.Select(a => a.PlayerId).Distinct().Count(),
                awards.Sum(a => a.Points));

            _logger.LogInformation(
                "Player points run credited {Points} point(s) to {Players} player(s) across {Fixtures} fixture(s).",
                result.PointsAwarded, result.PlayersCredited, result.FixturesCredited);

            return result;
        }

        private static PlayerPointsAward BuildAward(
            FixturePlayerStat stat,
            Fixture fixture,
            Player player,
            LeagueSetting settings,
            DateTime now)
        {
            // A stat row means the player featured in the fixture.
            var appearance = settings.PointsPerAppearance;

            // Own goals are tracked but deliberately cost nothing: a card never
            // goes backwards, so a bad game simply earns less.
            var goals = stat.Goals * settings.PointsPerGoal;
            var assists = stat.Assists * settings.PointsPerAssist;

            // The per-team nomination, not Fixture.TrueMotmPlayerId. The true
            // winner is admin-only and must never be inferable by players, which
            // paying points off it would allow.
            var motm = stat.ManOfTheMatch ? settings.PointsPerMotm : 0;

            // A player who has since transferred is no longer on either side of an
            // old fixture, so there is no result to judge them by. They keep what
            // they personally did and lose only the team-shaped points.
            var playedForASide = player.TeamId == fixture.HomeTeamId
                              || player.TeamId == fixture.AwayTeamId;

            var win = 0;
            var cleanSheet = 0;
            if (playedForASide)
            {
                var scored = player.TeamId == fixture.HomeTeamId ? fixture.HomeScore : fixture.AwayScore;
                var conceded = player.TeamId == fixture.HomeTeamId ? fixture.AwayScore : fixture.HomeScore;

                if (scored > conceded) win = settings.PointsPerWin;

                if (PlayerPositions.EarnedCleanSheet(player.Position, fixture, player.TeamId))
                    cleanSheet = settings.PointsPerCleanSheet;
            }

            return new PlayerPointsAward
            {
                PlayerId = stat.PlayerId,
                FixtureId = stat.FixtureId,
                AwardedAt = now,
                AppearancePoints = appearance,
                WinPoints = win,
                GoalPoints = goals,
                AssistPoints = assists,
                CleanSheetPoints = cleanSheet,
                MotmPoints = motm,
                Points = appearance + win + goals + assists + cleanSheet + motm
            };
        }
    }
}
