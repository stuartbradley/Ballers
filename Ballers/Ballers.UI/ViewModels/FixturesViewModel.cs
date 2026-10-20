using Ballers.Models;
using Ballers.Services;
using Ballers.Shared;

namespace Ballers.ViewModels
{
    /// <summary>
    /// The public schedule: a season's weeks, split into games still to play
    /// and results, optionally for one team.
    /// </summary>
    public class FixturesViewModel : ViewModelBase
    {
        public enum Tab { Upcoming, Results }

        private readonly FixtureService _fixtures;
        private readonly SeasonService _seasons;
        private readonly GalleryService _galleries;

        public FixturesViewModel(FixtureService fixtures, SeasonService seasons, GalleryService galleries)
        {
            _fixtures = fixtures;
            _seasons = seasons;
            _galleries = galleries;
        }

        public List<FixtureWeekDto> AllWeeks { get; private set; } = new();
        public List<SeasonDto> Seasons { get; private set; } = new();
        public int SelectedSeasonId { get; private set; }
        public bool IsLoading { get; private set; } = true;
        public Tab SelectedTab { get; private set; } = Tab.Upcoming;
        public string TeamFilter { get; set; } = "";

        // Set when the list changes, so the page can bring the current week into
        // view once it has rendered.
        public bool ScrollToCurrent { get; set; }

        // One lookup for the whole schedule, rather than asking per fixture.
        public Dictionary<int, int> GalleriesByFixture { get; private set; } = new();

        public SeasonDto? SelectedSeason => Seasons.FirstOrDefault(s => s.Id == SelectedSeasonId);

        public int TotalCount => AllWeeks.Sum(w => w.Matches.Count);
        public int PlayedCount => AllWeeks.Sum(w => w.Matches.Count(m => m.IsPlayed && MatchesTeam(m)));
        public int UpcomingCount => AllWeeks.Sum(w => w.Matches.Count(m => !m.IsPlayed && MatchesTeam(m)));

        // The first week with a game still to play.
        public FixtureWeekDto? CurrentWeek => AllWeeks
            .OrderBy(w => w.WeekNumber)
            .FirstOrDefault(w => w.Matches.Any(m => !m.IsPlayed));

        public IEnumerable<string> Teams => AllWeeks
            .SelectMany(w => w.Matches.SelectMany(m => new[] { m.Home, m.Away }).Concat(w.Byes))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .OrderBy(t => t);

        public bool MatchesTeam(FixtureMatchDto m)
            => string.IsNullOrEmpty(TeamFilter) || m.Home == TeamFilter || m.Away == TeamFilter;

        // Each week cut down to what the tab and the team filter ask for. Weeks left
        // with nothing in them drop out. Upcoming runs forward from now; results
        // run back from the latest.
        public List<FixtureWeekDto> VisibleWeeks
        {
            get
            {
                var upcoming = SelectedTab == Tab.Upcoming;
                var weeks = AllWeeks
                    .Select(w => new FixtureWeekDto
                    {
                        WeekNumber = w.WeekNumber,
                        DateRange = w.DateRange,
                        Matches = w.Matches.Where(m => m.IsPlayed != upcoming && MatchesTeam(m)).ToList(),
                        // A bye only matters in a week still to be played, and only for
                        // the chosen team when there is one.
                        Byes = upcoming && w.Matches.Any(m => !m.IsPlayed)
                            ? w.Byes.Where(b => string.IsNullOrEmpty(TeamFilter) || b == TeamFilter).ToList()
                            : new List<string>()
                    })
                    .Where(w => w.Matches.Any() || w.Byes.Any());

                return (upcoming
                        ? weeks.OrderBy(w => w.WeekNumber)
                        : weeks.OrderByDescending(w => w.WeekNumber))
                    .ToList();
            }
        }

        public async Task InitializeAsync()
        {
            Seasons = await _seasons.GetSeasons();
            var current = Seasons.FirstOrDefault(s => s.IsActive) ?? Seasons.LastOrDefault();
            SelectedSeasonId = current?.Id ?? 0;

            await LoadGalleriesAsync();
            await LoadFixtures();
        }

        private async Task LoadGalleriesAsync()
        {
            var galleries = await _galleries.GetGalleries();

            // A gallery with no photos in it yet has nothing to show, so it doesn't
            // earn a link from the schedule.
            GalleriesByFixture = galleries
                .Where(g => g.PhotoCount > 0)
                .GroupBy(g => g.FixtureId)
                .ToDictionary(g => g.Key, g => g.First().Id);
        }

        public async Task SelectSeason(int id)
        {
            SelectedSeasonId = id;
            TeamFilter = "";
            await LoadFixtures();
        }

        private async Task LoadFixtures()
        {
            IsLoading = true;
            AllWeeks = SelectedSeasonId > 0
                ? await _fixtures.GetFixtureWeeks(SelectedSeasonId)
                : new();

            // A finished season opens on its results; one in progress on what's next.
            SelectedTab = AllWeeks.Any(w => w.Matches.Any(m => !m.IsPlayed)) ? Tab.Upcoming : Tab.Results;
            IsLoading = false;
            ScrollToCurrent = true;
        }

        public void SetTab(Tab next)
        {
            SelectedTab = next;
            ScrollToCurrent = true;
        }

        public static string Scorer(GoalScorerDto s)
            => $"{s.Name}{(s.Goals > 1 ? $" ×{s.Goals}" : "")}{(s.IsOwnGoal ? " (og)" : "")}";
    }
}
