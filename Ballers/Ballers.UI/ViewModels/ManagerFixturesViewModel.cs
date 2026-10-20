using Ballers.Models;
using Ballers.Services;
using Ballers.Shared;
using Ballers.UI.Infrastructure;
using Microsoft.AspNetCore.Components.Authorization;

namespace Ballers.ViewModels
{
    /// <summary>
    /// A manager's fixtures for a season, each showing the one job that's due
    /// for it. Admins and referees see every game, read-only.
    /// </summary>
    public class ManagerFixturesViewModel : ViewModelBase
    {
        // A squad counts as picked at this many, as on the fixture page.
        public const int SquadMinimum = LeagueRules.SquadMinimum;

        private readonly FixtureService _fixtures;
        private readonly SeasonService _seasons;
        private readonly LeagueSettingsService _settings;
        private readonly AuthenticationStateProvider _auth;

        public ManagerFixturesViewModel(FixtureService fixtures, SeasonService seasons,
            LeagueSettingsService settings, AuthenticationStateProvider auth)
        {
            _fixtures = fixtures;
            _seasons = seasons;
            _settings = settings;
            _auth = auth;
        }

        public List<FixtureListItemDto>? Fixtures { get; private set; }
        public bool LockedForMe { get; private set; }
        public bool IsStaff { get; private set; }
        public int SeasonId { get; set; }
        public string Venue { get; set; } = "All";

        public record SeasonOption(int Id, int Number, bool Active);
        public List<SeasonOption> Seasons { get; private set; } = new();

        public async Task InitializeAsync()
        {
            var authState = await _auth.GetAuthenticationStateAsync();
            var isAdmin = authState.User.IsInRole("Admin");
            IsStaff = isAdmin || authState.User.IsInRole("Referee");

            var seasonsTask = _seasons.GetSeasons();
            var settingsTask = _settings.Get();
            await Task.WhenAll(seasonsTask, settingsTask);

            LockedForMe = settingsTask.Result.FixturesLocked && !isAdmin;

            // The live season by default, else the latest.
            Seasons = seasonsTask.Result
                .Select(s => new SeasonOption(s.Id, s.SeasonNumber, s.IsActive))
                .OrderByDescending(s => s.Number)
                .ToList();
            SeasonId = Seasons.FirstOrDefault(s => s.Active)?.Id ?? Seasons.FirstOrDefault()?.Id ?? 0;

            await LoadSeason();
        }

        // Only the chosen season's games come down.
        public async Task LoadSeason()
        {
            Fixtures = null;
            Fixtures = SeasonId == 0 ? new() : await _fixtures.GetFixtures(SeasonId);
        }

        public IEnumerable<FixtureListItemDto> Visible => (Fixtures ?? new())
            .Where(f => Venue == "All" || (Venue == "Home") == f.IsHomeTeamManager);

        // ── What state is a game in? ──

        public static DateTime DueBy(FixtureListItemDto f) => f.Kickoff ?? f.WindowEnd;
        public static DateTime SortKey(FixtureListItemDto f) => f.Kickoff ?? f.WindowStart;
        public static bool Past(FixtureListItemDto f) => DueBy(f) < DateTime.Now;

        public bool IsUpcoming(FixtureListItemDto f) => !f.IsPlayed && !Past(f);

        // Gone and done with, but the manager still owes stats (or pens, once it's played).
        public bool NeedsSorting(FixtureListItemDto f) =>
            !IsStaff && !LockedForMe &&
            ((Past(f) && !f.IsPlayed && !f.ManagerStatsSubmitted) || (f.IsPlayed && !f.ManagerPensSubmitted));

        // Which side the badge highlights: the manager's own.
        public bool Ours(FixtureListItemDto f, bool home) => !IsStaff && f.IsHomeTeamManager == home;

        public static bool SetupDone(FixtureListItemDto f) =>
            f.Kickoff != null && !string.IsNullOrWhiteSpace(f.Location) && !string.IsNullOrWhiteSpace(f.RefereeName);

        public record NextAction(string Label, string Link, string Tone);

        // The one job that's due for this game, or just a look.
        public NextAction ActionFor(FixtureListItemDto f)
        {
            var page = $"/fixture/{f.Id}";
            if (IsStaff || LockedForMe) return new("View", page, "");

            var squadOk = f.ManagerSquadCount >= SquadMinimum;
            if (Past(f) && !f.IsPlayed && !f.ManagerStatsSubmitted)
                return squadOk ? new("Send stats", $"{page}?tab=stats", "is-red") : new("Pick squad", $"{page}?tab=squad", "is-red");
            if (f.IsPlayed && !f.ManagerPensSubmitted)
                return new("Send pens", $"{page}?tab=pens", "is-red");
            if (!Past(f) && !f.IsPlayed)
            {
                if (f.IsHomeTeamManager && !f.IsKnockout && !SetupDone(f)) return new("Set up", $"{page}?tab=setup", "is-yellow");
                if (!squadOk) return new("Pick squad", $"{page}?tab=squad", "is-yellow");
                if (!f.ManagerCaptainSet) return new("Name captain", $"{page}?tab=squad", "is-yellow");
            }
            return new("View", page, "");
        }

        public static string When(FixtureListItemDto f) =>
            f.Kickoff is { } k ? k.ToString("ddd d MMM, HH:mm") : $"Week of {f.WindowStart:d MMM}, time TBC";

        public static string Countdown(FixtureListItemDto f)
        {
            var days = (SortKey(f).Date - DateTime.Today).Days;
            if (days <= 0) return "this week";
            return days == 1 ? "tomorrow" : $"{days} days to go";
        }

        public static string? MapUrl(FixtureListItemDto f) => Format.MapUrl(f.Location, f.Postcode);
    }
}
