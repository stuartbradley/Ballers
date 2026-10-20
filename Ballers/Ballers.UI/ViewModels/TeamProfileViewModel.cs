using Ballers.Models;
using Ballers.Services;
using Ballers.Shared;

namespace Ballers.ViewModels
{
    /// <summary>
    /// A club's public page: its manager, form, squad of player cards (by
    /// position or by rating), leaders, next game and recent results.
    /// </summary>
    public class TeamProfileViewModel : ViewModelBase
    {
        private readonly TeamService _teams;
        private readonly FixtureService _fixtures;
        private readonly SeasonService _seasons;

        public TeamProfileViewModel(TeamService teams, FixtureService fixtures, SeasonService seasons)
        {
            _teams = teams;
            _fixtures = fixtures;
            _seasons = seasons;
        }

        public TeamProfileDto? Profile { get; private set; }
        public TeamOverviewDto? Overview { get; private set; }
        public List<PlayerCardDto> PlayerCards { get; private set; } = new();
        public bool IsLoading { get; private set; } = true;
        public bool ByPosition { get; set; } = true;

        // Cards turned over by a tap (screens with no pointer to hover with).
        public HashSet<int> Flipped { get; } = new();

        public (int Week, FixtureMatchDto Match)? NextGame { get; private set; }
        public List<(int Week, FixtureMatchDto Match)> Recent { get; private set; } = new();

        private static readonly string[] PositionOrder = { "GK", "DEF", "MID", "FWD" };
        private static readonly Dictionary<string, string> PositionNames = new()
        {
            ["GK"] = "Goalkeepers", ["DEF"] = "Defenders", ["MID"] = "Midfielders", ["FWD"] = "Forwards"
        };

        // bronze < 3 wins ≤ silver < 6 wins ≤ gold
        public static string ManagerTier(int wins) => wins >= 6 ? "gold" : wins >= 3 ? "silver" : "bronze";

        public PlayerCardDto? TopRated => PlayerCards
            .Where(p => p.Attributes != null)
            .OrderByDescending(p => p.Attributes!.Overall)
            .ThenBy(p => p.Name)
            .FirstOrDefault();

        public IEnumerable<(string Tier, int Count)> TierCounts => new[] { "gold", "silver", "bronze" }
            .Select(t => (t, PlayerCards.Count(p => p.Tier == t)))
            .Where(t => t.Item2 > 0);

        public record Leader(string Title, PlayerCardDto? Player, int Value);

        public IEnumerable<Leader> Leaders
        {
            get
            {
                Leader Top(string title, Func<PlayerCardDto, int> by)
                {
                    var p = PlayerCards.OrderByDescending(by).ThenBy(p => p.Name).FirstOrDefault();
                    return new Leader(title, p, p == null ? 0 : by(p));
                }
                yield return Top("Top scorer", p => p.Goals);
                yield return Top("Most assists", p => p.Assists);
                yield return Top("Most MOTMs", p => p.Motm);
            }
        }

        public record SquadGroup(string? Title, List<PlayerCardDto> Players);

        // Best-rated first in every list; players without a card follow, by shirt.
        private static IEnumerable<PlayerCardDto> RatingOrder(IEnumerable<PlayerCardDto> players) => players
            .OrderByDescending(p => p.Attributes?.Overall ?? -1)
            .ThenBy(p => p.Number);

        public IEnumerable<SquadGroup> SquadGroups => ByPosition
            ? PlayerCards
                .GroupBy(p => p.Position)
                .OrderBy(g => Array.IndexOf(PositionOrder, g.Key) is var i and >= 0 ? i : PositionOrder.Length)
                .Select(g => new SquadGroup(PositionNames.GetValueOrDefault(g.Key, g.Key), RatingOrder(g).ToList()))
            : new[] { new SquadGroup(null, RatingOrder(PlayerCards).ToList()) };

        public async Task LoadAsync(int teamId)
        {
            IsLoading = true;
            Flipped.Clear();

            var profileTask = _teams.GetProfile(teamId);
            var squadTask = _teams.GetSquad(teamId);
            var overviewTask = _teams.GetOverview();
            var seasonTask = _seasons.GetCurrentSeason();
            await Task.WhenAll(profileTask, squadTask, overviewTask, seasonTask);

            Profile = profileTask.Result;
            PlayerCards = squadTask.Result;
            Overview = overviewTask.Result.FirstOrDefault(t => t.Id == teamId);

            NextGame = null;
            Recent = new();
            if (Profile != null && seasonTask.Result is { } season)
            {
                // The public schedule, narrowed to this club (matched by name, as the
                // schedule identifies teams by name).
                var weeks = await _fixtures.GetFixtureWeeks(season.Id);
                var ours = weeks
                    .SelectMany(w => w.Matches.Select(m => (Week: w.WeekNumber, Match: m)))
                    .Where(x => x.Match.Home == Profile.Name || x.Match.Away == Profile.Name)
                    .ToList();

                var upcoming = ours.Where(x => !x.Match.IsPlayed).OrderBy(x => x.Week).ToList();
                NextGame = upcoming.Count > 0 ? upcoming[0] : null;
                Recent = ours.Where(x => x.Match.IsPlayed).OrderByDescending(x => x.Week).Take(5).ToList();
            }

            IsLoading = false;
        }

        public void Toggle(int playerId)
        {
            if (!Flipped.Add(playerId)) Flipped.Remove(playerId);
        }
    }
}
