using Ballers.Models;
using Ballers.Services;
using Ballers.Shared;

namespace Ballers.ViewModels
{
    /// <summary>
    /// The home page: this week's games, the league and penalty tables, the
    /// stat leaders, Team of the Week and the latest galleries.
    /// </summary>
    public class HomeViewModel : ViewModelBase
    {
        // The strip is a taster, not the full list — that's what /galleries is for.
        public const int HomeGalleryCount = 3;

        private readonly SeasonService _seasons;
        private readonly LeagueService _league;
        private readonly FixtureService _fixtures;
        private readonly StatService _stats;
        private readonly PenaltyService _penalties;
        private readonly TotwService _totw;
        private readonly GalleryService _galleries;

        public HomeViewModel(SeasonService seasons, LeagueService league, FixtureService fixtures, StatService stats,
            PenaltyService penalties, TotwService totw, GalleryService galleries)
        {
            _seasons = seasons;
            _league = league;
            _fixtures = fixtures;
            _stats = stats;
            _penalties = penalties;
            _totw = totw;
            _galleries = galleries;
        }

        public FixtureWeekDto? CurrentWeek { get; private set; }
        public List<LeagueTableRowDto> Table { get; private set; } = new();
        public List<SeasonDto> Seasons { get; private set; } = new();
        public SeasonDto? CurrentSeason { get; private set; }
        public int SelectedSeasonId { get; set; }
        public List<PlayerGoalsDto> TopScorers { get; private set; } = new();
        public List<PlayerAssistDto> TopAssists { get; private set; } = new();
        public List<PlayerMotmDto> TopMotm { get; private set; } = new();
        public List<PenaltyTableRowDto> PenaltyTable { get; private set; } = new();
        public TeamOfTheWeekDto? CurrentTotw { get; private set; }
        public List<GallerySummaryDto> RecentGalleries { get; private set; } = new();
        public bool IsLoading { get; private set; } = true;

        // Which of the two tables the standings section is showing.
        public string ActiveTable { get; set; } = "league";

        public string SeasonEyebrow => CurrentSeason != null
            ? $"Season {CurrentSeason.SeasonNumber} · Now underway"
            : "Now underway";

        public record Board(string Key, string Title, IReadOnlyList<(string Name, int Value)> Rows);

        public IEnumerable<Board> Boards => new[]
        {
            new Board("goals", "Top Goalscorers", TopScorers.Select(p => (p.Name, p.Goals)).ToList()),
            new Board("assists", "Top Assists", TopAssists.Select(p => (p.Name, p.Assists)).ToList()),
            new Board("motm", "Most MOTMs", TopMotm.Select(p => (p.Name, p.Motm)).ToList())
        };

        public async Task InitializeAsync()
        {
            try
            {
                var scorersTask = _stats.GetTopGoalScorers();
                var assistsTask = _stats.GetTopAssists();
                var motmTask = _stats.GetTopMotm();
                var weekTask = _fixtures.GetCurrentWeek();
                var seasonsTask = _seasons.GetSeasons();
                var currentSeasonTask = _seasons.GetCurrentSeason();
                var totwTask = _totw.GetCurrent();
                var galleriesTask = _galleries.GetGalleries();

                await Task.WhenAll(scorersTask, assistsTask, motmTask, weekTask, seasonsTask, currentSeasonTask, totwTask, galleriesTask);

                TopScorers = scorersTask.Result;
                TopAssists = assistsTask.Result;
                TopMotm = motmTask.Result;
                CurrentWeek = weekTask.Result;
                Seasons = seasonsTask.Result;
                CurrentTotw = totwTask.Result;

                // Only galleries that actually have a cover are worth showing here.
                RecentGalleries = galleriesTask.Result
                    .Where(g => !string.IsNullOrEmpty(g.CoverThumbnailUrl))
                    .Take(HomeGalleryCount)
                    .ToList();

                CurrentSeason = currentSeasonTask.Result;
                SelectedSeasonId = CurrentSeason?.Id ?? Seasons.FirstOrDefault()?.Id ?? 0;

                if (SelectedSeasonId > 0)
                    await LoadTable();
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadTable()
        {
            var tableTask = _league.GetTable(SelectedSeasonId);
            var penaltyTask = _penalties.GetTable(SelectedSeasonId);
            await Task.WhenAll(tableTask, penaltyTask);
            Table = tableTask.Result;
            PenaltyTable = penaltyTask.Result;
        }
    }
}
