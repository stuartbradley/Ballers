using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Shared;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services
{
    public interface IManagerDashboardService
    {
        Task<ManagerDashboardDto?> GetAsync(int teamId, DateTime now);
    }

    /// <summary>
    /// Builds a manager's dashboard: their club's season, the next game and how
    /// ready they are for it, recent results, and a to-do list for the week.
    /// </summary>
    public class ManagerDashboardService : IManagerDashboardService
    {
        // A squad counts as picked at this size, as on the fixture page.
        public const int SquadSize = 11;

        // Jobs for the next game only appear once it's this close.
        private static readonly TimeSpan PrepWindow = TimeSpan.FromDays(14);

        // Less than this to kickoff and an unfinished job is flagged urgent.
        private static readonly TimeSpan UrgentWindow = TimeSpan.FromDays(3);

        private const int RecentCount = 4;

        private readonly ApplicationDbContext _db;
        private readonly ITeamService _teams;

        public ManagerDashboardService(ApplicationDbContext db, ITeamService teams)
        {
            _db = db;
            _teams = teams;
        }

        private record Row(
            int Id, int HomeTeamId, int AwayTeamId, string Home, string Away,
            string? HomeImage, string? AwayImage,
            DateTime? Kickoff, DateTime WindowStart, DateTime WindowEnd,
            string? Location, string? Postcode, string? Referee,
            bool IsPlayed, bool HomeSubmitted, bool AwaySubmitted, int HomeScore, int AwayScore,
            int? HomeCaptainId, int? AwayCaptainId, bool IsKnockout, string? Tournament, string? Round)
        {
            public DateTime DueBy => Kickoff ?? WindowEnd;
            public DateTime SortKey => Kickoff ?? WindowStart;
        }

        public async Task<ManagerDashboardDto?> GetAsync(int teamId, DateTime now)
        {
            var team = await _db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId);
            if (team == null) return null;

            var settings = await _db.LeagueSettings.AsNoTracking().FirstOrDefaultAsync() ?? new LeagueSetting();
            var dto = new ManagerDashboardDto
            {
                TeamId = team.Id,
                TeamName = team.Name,
                ProfileImageUrl = team.ProfileImageUrl,
                HomeKitColour = team.HomeKitColour,
                HasProfile = !string.IsNullOrEmpty(team.ProfileImageUrl) && !string.IsNullOrEmpty(team.HomeKitColour),
                FixturesLocked = settings.FixturesLocked,
                PlayersLocked = settings.PlayersLocked,
                PendingSignups = await _db.PlayerAccountRequests.CountAsync(r =>
                    r.RequestedTeamId == teamId && r.Status == PlayerAccountRequestStatus.Pending)
            };

            var overview = await _teams.GetOverviewAsync();
            dto.Standing = overview.FirstOrDefault(t => t.Id == teamId);
            dto.TeamsInLeague = overview.Count(t => t.Position != null);

            var playerIds = await _db.Players
                .Where(p => p.TeamId == teamId && p.IsActive)
                .Select(p => p.Id)
                .ToListAsync();

            var season = await _db.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.IsActive);
            var fixtures = new List<Row>();
            if (season != null)
            {
                dto.SeasonNumber = season.SeasonNumber;
                fixtures = await _db.Fixtures
                    .AsNoTracking()
                    .Where(f => f.SeasonId == season.Id && (f.HomeTeamId == teamId || f.AwayTeamId == teamId))
                    .Select(f => new Row(
                        f.Id, f.HomeTeamId, f.AwayTeamId, f.HomeTeam!.Name, f.AwayTeam!.Name,
                        f.HomeTeam.ProfileImageUrl, f.AwayTeam.ProfileImageUrl,
                        f.Kickoff, f.WindowStart, f.WindowEnd,
                        f.Location, f.Postcode, f.Referee != null ? f.Referee.Name : null,
                        f.IsPlayed, f.HomeStatsSubmitted, f.AwayStatsSubmitted, f.HomeScore, f.AwayScore,
                        f.HomeCaptainId, f.AwayCaptainId, f.IsKnockout, f.KnockoutTournament, f.KnockoutRound))
                    .ToListAsync();

                await AddTopPlayersAsync(dto, season.Id, playerIds);
            }

            bool IsHome(Row f) => f.HomeTeamId == teamId;
            bool OurStatsIn(Row f) => IsHome(f) ? f.HomeSubmitted : f.AwaySubmitted;

            var next = fixtures
                .Where(f => !f.IsPlayed && f.DueBy >= now)
                .OrderBy(f => f.SortKey)
                .FirstOrDefault();

            if (next != null)
            {
                var squad = await _db.FixturePlayers
                    .Where(fp => fp.FixtureId == next.Id && playerIds.Contains(fp.PlayerId))
                    .CountAsync();
                var captainId = IsHome(next) ? next.HomeCaptainId : next.AwayCaptainId;
                var captain = captainId == null
                    ? null
                    : await _db.Players.Where(p => p.Id == captainId).Select(p => p.Name).FirstOrDefaultAsync();

                dto.NextMatch = new ManagerMatchDto
                {
                    FixtureId = next.Id,
                    Opponent = IsHome(next) ? next.Away : next.Home,
                    OpponentImageUrl = IsHome(next) ? next.AwayImage : next.HomeImage,
                    IsHome = IsHome(next),
                    Kickoff = next.Kickoff,
                    WindowStart = next.WindowStart,
                    WindowEnd = next.WindowEnd,
                    Location = next.Location,
                    Postcode = next.Postcode,
                    RefereeName = next.Referee,
                    Competition = next.IsKnockout ? $"{next.Tournament} {next.Round}".Trim() : "League",
                    SquadCount = squad,
                    SquadPicked = squad >= SquadSize,
                    CaptainName = captain
                };
            }

            dto.RecentResults = fixtures
                .Where(f => f.IsPlayed || f.DueBy < now)
                .OrderByDescending(f => f.SortKey)
                .Take(RecentCount)
                .Select(f => new ManagerResultDto
                {
                    FixtureId = f.Id,
                    Opponent = IsHome(f) ? f.Away : f.Home,
                    IsHome = IsHome(f),
                    When = f.SortKey,
                    IsPlayed = f.IsPlayed,
                    TeamScore = IsHome(f) ? f.HomeScore : f.AwayScore,
                    OpponentScore = IsHome(f) ? f.AwayScore : f.HomeScore,
                    OurStatsIn = OurStatsIn(f),
                    TheirStatsIn = IsHome(f) ? f.AwaySubmitted : f.HomeSubmitted
                })
                .ToList();

            AddTodos(dto, fixtures, playerIds.Count, now, IsHome, OurStatsIn);
            return dto;
        }

        private async Task AddTopPlayersAsync(ManagerDashboardDto dto, int seasonId, List<int> playerIds)
        {
            var totals = await _db.FixturePlayerStats
                .Where(s => playerIds.Contains(s.PlayerId) && s.Fixture!.SeasonId == seasonId && s.Fixture.IsPlayed)
                .GroupBy(s => new { s.PlayerId, s.Player!.Name })
                .Select(g => new { g.Key.Name, Goals = g.Sum(s => s.Goals), Assists = g.Sum(s => s.Assists) })
                .ToListAsync();

            var scorer = totals.Where(t => t.Goals > 0).OrderByDescending(t => t.Goals).ThenBy(t => t.Name).FirstOrDefault();
            if (scorer != null) (dto.TopScorerName, dto.TopScorerGoals) = (scorer.Name, scorer.Goals);

            var assister = totals.Where(t => t.Assists > 0).OrderByDescending(t => t.Assists).ThenBy(t => t.Name).FirstOrDefault();
            if (assister != null) (dto.TopAssisterName, dto.TopAssisterAssists) = (assister.Name, assister.Assists);
        }

        private static void AddTodos(ManagerDashboardDto dto, List<Row> fixtures, int squadSize, DateTime now,
                                     Func<Row, bool> isHome, Func<Row, bool> ourStatsIn)
        {
            // Results first: a game that's been and gone without our stats holds
            // up the table, the points and everyone's cards.
            foreach (var f in fixtures.Where(f => !f.IsPlayed && f.DueBy < now && !ourStatsIn(f)).OrderBy(f => f.SortKey))
            {
                dto.Todos.Add(new ManagerTodoDto
                {
                    Kind = "stats",
                    Title = $"Send your stats v {(isHome(f) ? f.Away : f.Home)}",
                    Detail = $"Played {f.SortKey:ddd d MMM}. The result only counts once both teams have sent theirs.",
                    Link = $"/fixture/{f.Id}?tab=stats",
                    Urgent = true
                });
            }

            if (dto.NextMatch is { } next && next.WindowStart <= now + PrepWindow)
            {
                var soon = (next.Kickoff ?? next.WindowEnd) - now < UrgentWindow;

                dto.Todos.Add(new ManagerTodoDto
                {
                    Kind = "squad",
                    Title = $"Pick your squad v {next.Opponent}",
                    Detail = next.SquadPicked
                        ? $"{next.SquadCount} players picked."
                        : $"{next.SquadCount} of at least {SquadSize} picked so far.",
                    Link = $"/fixture/{next.FixtureId}?tab=squad",
                    Done = next.SquadPicked,
                    Urgent = soon && !next.SquadPicked
                });

                dto.Todos.Add(new ManagerTodoDto
                {
                    Kind = "captain",
                    Title = "Name your captain",
                    Detail = next.CaptainName != null ? $"{next.CaptainName} has the armband." : "Pick one from your squad.",
                    Link = $"/fixture/{next.FixtureId}?tab=squad",
                    Done = next.CaptainName != null,
                    Urgent = soon && next.CaptainName == null
                });

                // Kickoff and venue are the home side's to sort out.
                if (next.IsHome)
                {
                    var set = next.Kickoff != null && !string.IsNullOrWhiteSpace(next.Location);
                    dto.Todos.Add(new ManagerTodoDto
                    {
                        Kind = "details",
                        Title = "Confirm kickoff time and venue",
                        Detail = set
                            ? $"{next.Kickoff!.Value:ddd d MMM, HH:mm} at {next.Location}."
                            : "You're at home, so it's yours to set.",
                        Link = $"/fixture/{next.FixtureId}",
                        Done = set,
                        Urgent = soon && !set
                    });
                }
            }

            if (dto.PendingSignups > 0)
            {
                dto.Todos.Add(new ManagerTodoDto
                {
                    Kind = "signups",
                    Title = dto.PendingSignups == 1 ? "1 player wants to join" : $"{dto.PendingSignups} players want to join",
                    Detail = "Approve them to give them their player card.",
                    Link = "#signups"
                });
            }

            if (squadSize < SquadSize && !dto.PlayersLocked)
            {
                dto.Todos.Add(new ManagerTodoDto
                {
                    Kind = "players",
                    Title = "Fill out your squad",
                    Detail = $"{squadSize} player{(squadSize == 1 ? "" : "s")} so far; you need {SquadSize} for a matchday squad.",
                    Link = "/my-squad"
                });
            }

            if (!dto.HasProfile)
            {
                dto.Todos.Add(new ManagerTodoDto
                {
                    Kind = "profile",
                    Title = "Add your crest and kit colours",
                    Detail = "They show on your club page, the Teams page and fixtures.",
                    Link = "/my-team"
                });
            }
        }
    }
}
