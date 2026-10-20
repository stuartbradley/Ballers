using Ballers.Services;
using Ballers.Shared;

namespace Ballers.ViewModels
{
    /// <summary>
    /// Team of the Week: one week's lineup at a time, stepped through with a
    /// strip of weeks. The other weeks are fetched in the background, nearest
    /// first, so stepping rarely waits.
    /// </summary>
    public class TeamOfTheWeekViewModel : ViewModelBase
    {
        private readonly TotwService _totw;

        public TeamOfTheWeekViewModel(TotwService totw) => _totw = totw;

        public bool Loading { get; private set; } = true;
        public bool Fetching { get; private set; }
        // Newest first, as the API sends them.
        public List<TotwWeekRefDto> Weeks { get; private set; } = new();
        public TeamOfTheWeekDto? Current { get; private set; }
        // Into Chronological, oldest first.
        public int CurrentIndex { get; private set; }
        public List<TotwAppearanceDto> Appearances { get; private set; } = new();

        // Set when the chosen week changes, for the page to scroll its chip into view.
        public bool CentrePending { get; set; }

        private readonly Dictionary<int, TeamOfTheWeekDto> _cache = new();
        private CancellationTokenSource? _prefetchCts;

        // Oldest on the left, so the strip reads like a season.
        public List<(TotwWeekRefDto Week, int Index)> Chronological =>
            Weeks.AsEnumerable().Reverse().Select((w, i) => (w, i)).ToList();

        public bool CanGoNewer => CurrentIndex < Weeks.Count - 1;
        public bool CanGoOlder => CurrentIndex > 0;

        public async Task InitializeAsync()
        {
            var weeksTask = _totw.GetWeeks();
            var currentTask = _totw.GetCurrent();
            var appsTask = _totw.GetAppearances();
            await Task.WhenAll(weeksTask, currentTask, appsTask);

            Weeks = weeksTask.Result;
            Current = currentTask.Result;
            Appearances = appsTask.Result;

            if (Current != null)
            {
                _cache[Current.MatchNumber] = Current;
                var idx = Chronological.FindIndex(x => x.Week.MatchNumber == Current.MatchNumber);
                CurrentIndex = idx >= 0 ? idx : Weeks.Count - 1;
            }

            Loading = false;
            CentrePending = true;

            _prefetchCts = new CancellationTokenSource();
            _ = PrefetchOthersAsync(_prefetchCts.Token);
        }

        /// <summary>Stops the background fetching; the page calls it when it goes.</summary>
        public void StopPrefetch()
        {
            _prefetchCts?.Cancel();
            _prefetchCts?.Dispose();
            _prefetchCts = null;
        }

        // Nearest weeks first, so the arrows rarely wait.
        private async Task PrefetchOthersAsync(CancellationToken ct)
        {
            var ordered = Chronological
                .Where(x => !_cache.ContainsKey(x.Week.MatchNumber))
                .OrderBy(x => Math.Abs(x.Index - CurrentIndex))
                .Select(x => x.Week)
                .ToList();

            foreach (var w in ordered)
            {
                if (ct.IsCancellationRequested) return;
                if (_cache.ContainsKey(w.MatchNumber)) continue;

                var dto = await _totw.GetWeek(w.MatchNumber);
                if (dto != null) _cache[w.MatchNumber] = dto;
            }
        }

        public Task GoNewer() => CanGoNewer ? Select(CurrentIndex + 1) : Task.CompletedTask;
        public Task GoOlder() => CanGoOlder ? Select(CurrentIndex - 1) : Task.CompletedTask;

        public async Task Select(int newIndex)
        {
            if (newIndex < 0 || newIndex >= Weeks.Count || Fetching) return;

            var matchNumber = Chronological[newIndex].Week.MatchNumber;
            CentrePending = true;

            if (_cache.TryGetValue(matchNumber, out var hit))
            {
                Current = hit;
                CurrentIndex = newIndex;
                return;
            }

            Fetching = true;
            NotifyChanged();

            var dto = await _totw.GetWeek(matchNumber);
            if (dto != null)
            {
                _cache[matchNumber] = dto;
                Current = dto;
                CurrentIndex = newIndex;
            }

            Fetching = false;
        }

        public record Line(string Name, List<TotwPlayerDto> Players);

        // Attack at the top, goalkeepers at the bottom, as a lineup graphic reads.
        private static readonly (string Code, string Name)[] LineOrder =
            { ("FWD", "Forwards"), ("MID", "Midfielders"), ("DEF", "Defenders"), ("GK", "Goalkeepers") };

        public IEnumerable<Line> Lines
        {
            get
            {
                var players = Current?.Players ?? new();
                string LineOf(TotwPlayerDto p) =>
                    p.IsGoalkeeper ? "GK" : LineOrder.Any(l => l.Code == p.Position) ? p.Position : "MID";

                return LineOrder
                    .Select(l => new Line(l.Name, players.Where(p => LineOf(p) == l.Code).ToList()))
                    .Where(l => l.Players.Count > 0);
            }
        }

        public static (string Code, string Text)? Caption(TotwPlayerDto p) =>
            p.TeamScore is int us && p.OpponentScore is int them && p.OpponentName != null
                ? (us > them ? "W" : us == them ? "D" : "L", $"{us}–{them} vs {p.OpponentName}")
                : null;

        public string BarWidth(int count)
        {
            var max = Appearances.Count == 0 ? 1 : Appearances.Max(a => a.Count);
            return $"{Math.Round(100.0 * count / max)}%";
        }
    }
}
