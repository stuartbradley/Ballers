using Ballers.Models;
using Ballers.Services;
using Ballers.Shared;

namespace Ballers.ViewModels
{
    /// <summary>
    /// The league's numbers: season totals, the leaders in each category, and
    /// sortable, filterable tables of players and of teams.
    /// </summary>
    public class StatsViewModel : ViewModelBase
    {
        private readonly StatService _stats;
        private readonly SeasonService _seasons;

        public StatsViewModel(StatService stats, SeasonService seasons)
        {
            _stats = stats;
            _seasons = seasons;
        }

        public bool IsLoading { get; private set; } = true;
        public List<SeasonDto> Seasons { get; private set; } = new();
        public int SelectedSeasonId { get; set; }
        public List<PlayerLeaderboardDto> AllPlayers { get; private set; } = new();
        public List<TeamStatsDto> TeamStats { get; private set; } = new();
        public string Tab { get; set; } = "players";

        public string SelectedPos { get; set; } = "All";
        public string NameSearch { get; set; } = "";
        public string SortCol { get; private set; } = "goals";
        public bool SortAsc { get; private set; }
        public int? ExpandedId { get; private set; }

        public string TeamSortCol { get; private set; } = "gf";
        public bool TeamSortAsc { get; private set; }

        // Season totals. Each game appears twice in the team list, once per side.
        public int GamesPlayed { get; private set; }
        public int TotalGoals { get; private set; }
        public int TotalCleanSheets { get; private set; }
        public int TotalYellows { get; private set; }
        public int TotalReds { get; private set; }
        public string GoalsPerGame => GamesPlayed == 0 ? "–" : ((double)TotalGoals / GamesPlayed).ToString("0.0");

        // Set when fresh numbers land, so the page can run its count-up animation.
        public bool RevealPending { get; set; }

        public async Task InitializeAsync()
        {
            Seasons = await _seasons.GetSeasons();
            var current = await _seasons.GetCurrentSeason();
            SelectedSeasonId = current?.Id ?? 0;
            await LoadSeason();
            IsLoading = false;
        }

        public async Task LoadSeason()
        {
            int? sid = SelectedSeasonId > 0 ? SelectedSeasonId : null;
            var playersTask = _stats.GetLeaderboard(sid);
            var teamsTask = _stats.GetTeamStats(sid);
            await Task.WhenAll(playersTask, teamsTask);

            AllPlayers = playersTask.Result;
            TeamStats = teamsTask.Result;
            ExpandedId = null;
            if (!TeamStats.Any()) Tab = "players";

            GamesPlayed = TeamStats.Sum(t => t.Played) / 2;
            TotalGoals = TeamStats.Sum(t => t.GoalsFor);
            TotalCleanSheets = TeamStats.Sum(t => t.CleanSheets);
            TotalYellows = TeamStats.Sum(t => t.YellowCards);
            TotalReds = TeamStats.Sum(t => t.RedCards);
            RevealPending = true;
        }

        // ── Players ──

        public IEnumerable<PlayerLeaderboardDto> Filtered =>
            AllPlayers.Where(p =>
                (SelectedPos == "All" || p.Position == SelectedPos) &&
                (string.IsNullOrEmpty(NameSearch) || p.Name.Contains(NameSearch, StringComparison.OrdinalIgnoreCase)));

        public static double PerGame(int value, int apps) => apps == 0 ? 0 : (double)value / apps;

        public IEnumerable<PlayerLeaderboardDto> Sorted
        {
            get
            {
                Func<PlayerLeaderboardDto, object> key = SortCol switch
                {
                    "name" => p => p.Name,
                    "team" => p => p.Team,
                    "ovr" => p => p.Attributes?.Overall ?? -1,
                    "apps" => p => p.Appearances,
                    "assists" => p => p.Assists,
                    "gpg" => p => PerGame(p.Goals, p.Appearances),
                    "cs" => p => EarnsCleanSheets(p.Position) ? p.CleanSheets : -1,
                    "motm" => p => p.Motm,
                    "yellow" => p => p.YellowCards,
                    "red" => p => p.RedCards,
                    _ => p => p.Goals
                };
                var ordered = SortAsc ? Filtered.OrderBy(key) : Filtered.OrderByDescending(key);
                // Ties go to whoever has more goals, then more assists.
                return ordered.ThenByDescending(p => p.Goals).ThenByDescending(p => p.Assists).ThenBy(p => p.Name);
            }
        }

        public void SortBy(string col)
        {
            if (SortCol == col) SortAsc = !SortAsc;
            else { SortCol = col; SortAsc = col is "name" or "team"; }
        }

        public string SortClass(string col) =>
            SortCol == col ? (SortAsc ? "sorted asc" : "sorted desc") : "sortable";

        public string? AriaSort(string col) =>
            SortCol == col ? (SortAsc ? "ascending" : "descending") : null;

        public void Toggle(int playerId) => ExpandedId = ExpandedId == playerId ? null : playerId;

        // A leader's card opens their row in the table, clearing any filter that
        // would hide it.
        public void ShowPlayer(int playerId)
        {
            SelectedPos = "All";
            NameSearch = "";
            ExpandedId = playerId;
        }

        // Only goalkeepers and defenders are credited with clean sheets; everyone
        // else shows a dash rather than a misleading zero.
        public static bool EarnsCleanSheets(string? position) =>
            string.Equals(position, "GK", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(position, "DEF", StringComparison.OrdinalIgnoreCase);

        public record Leader(string Title, string Unit, List<(PlayerLeaderboardDto Player, int Value)> Top);

        // The top three in each category, across every position. A category with
        // nobody on the board yet is left out.
        public IEnumerable<Leader> Leaders
        {
            get
            {
                Leader Top(string title, string unit, Func<PlayerLeaderboardDto, int> value,
                           Func<PlayerLeaderboardDto, bool>? who = null) => new(title, unit,
                    AllPlayers.Where(p => who == null || who(p))
                        .Select(p => (Player: p, Value: value(p)))
                        .Where(x => x.Value > 0)
                        .OrderByDescending(x => x.Value)
                        .ThenBy(x => x.Player.Appearances)
                        .ThenBy(x => x.Player.Name)
                        .Take(3)
                        .ToList());

                return new[]
                {
                    Top("Top scorer", "goals", p => p.Goals),
                    Top("Most assists", "assists", p => p.Assists),
                    Top("Most clean sheets", "clean sheets", p => p.CleanSheets, p => EarnsCleanSheets(p.Position)),
                    Top("Most MOTMs", "MOTMs", p => p.Motm)
                }.Where(l => l.Top.Count > 0);
            }
        }

        // ── Teams ──

        public IEnumerable<TeamStatsDto> SortedTeams
        {
            get
            {
                Func<TeamStatsDto, object> key = TeamSortCol switch
                {
                    "name" => t => t.Name,
                    "p" => t => t.Played,
                    "w" => t => t.Won,
                    "d" => t => t.Drawn,
                    "l" => t => t.Lost,
                    "ga" => t => t.GoalsAgainst,
                    "gd" => t => t.GoalsFor - t.GoalsAgainst,
                    "cs" => t => t.CleanSheets,
                    "yellow" => t => t.YellowCards,
                    "red" => t => t.RedCards,
                    _ => t => t.GoalsFor
                };
                var ordered = TeamSortAsc ? TeamStats.OrderBy(key) : TeamStats.OrderByDescending(key);
                return ordered.ThenBy(t => t.Name);
            }
        }

        public void SortTeamsBy(string col)
        {
            if (TeamSortCol == col) TeamSortAsc = !TeamSortAsc;
            // Fewer is better for these, so they start lowest first.
            else { TeamSortCol = col; TeamSortAsc = col is "name" or "ga" or "l" or "yellow" or "red"; }
        }

        public string TeamSortClass(string col) =>
            TeamSortCol == col ? (TeamSortAsc ? "sorted asc" : "sorted desc") : "sortable";

        public string? TeamAriaSort(string col) =>
            TeamSortCol == col ? (TeamSortAsc ? "ascending" : "descending") : null;

        public record Highlight(string Title, TeamStatsDto Team, string Value);

        // Best attack, best defence and best discipline, among teams that have
        // played. A red counts as three yellows.
        public IEnumerable<Highlight> TeamHighlights
        {
            get
            {
                var played = TeamStats.Where(t => t.Played > 0).ToList();
                if (!played.Any()) yield break;

                var attack = played.OrderByDescending(t => (double)t.GoalsFor / t.Played).ThenBy(t => t.Name).First();
                yield return new("Best attack", attack, $"{(double)attack.GoalsFor / attack.Played:0.0} goals a game");

                var defence = played.OrderBy(t => (double)t.GoalsAgainst / t.Played).ThenBy(t => t.Name).First();
                yield return new("Best defence", defence, $"{(double)defence.GoalsAgainst / defence.Played:0.0} conceded a game");

                var fair = played.OrderBy(t => (double)(t.YellowCards + 3 * t.RedCards) / t.Played).ThenBy(t => t.Name).First();
                yield return new("Fair play", fair, $"{fair.YellowCards} yellow, {fair.RedCards} red");
            }
        }
    }
}
