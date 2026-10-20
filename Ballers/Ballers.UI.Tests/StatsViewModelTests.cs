using Ballers.Models;
using Ballers.Services;
using Ballers.Shared;
using Ballers.UI.Tests.Helpers;
using Ballers.ViewModels;

namespace Ballers.UI.Tests
{
    public class StatsViewModelTests
    {
        private static PlayerLeaderboardDto P(int id, string name, string pos, int goals, int assists = 0, int cs = 0, int apps = 5)
            => new() { PlayerId = id, Name = name, Team = "Rovers", Position = pos, Goals = goals, Assists = assists, CleanSheets = cs, Appearances = apps };

        private static async Task<StatsViewModel> Loaded()
        {
            var api = new FakeApi()
                .Get("api/seasons", new List<SeasonDto> { new() { Id = 3, SeasonNumber = 1, IsActive = true } })
                .Get("api/seasons/current", new SeasonDto { Id = 3, SeasonNumber = 1, IsActive = true })
                .Get("api/stats/leaderboard?seasonId=3", new List<PlayerLeaderboardDto>
                {
                    P(1, "Ann", "FWD", 9, 1),
                    P(2, "Bob", "MID", 4, 6),
                    P(3, "Cat", "DEF", 1, 0, cs: 4),
                    P(4, "Dan", "FWD", 9, 3)
                })
                .Get("api/stats/teams?seasonId=3", new List<TeamStatsDto>
                {
                    new() { TeamId = 1, Name = "Rovers", Played = 4, GoalsFor = 10, GoalsAgainst = 2 },
                    new() { TeamId = 2, Name = "Unity", Played = 4, GoalsFor = 6, GoalsAgainst = 8 }
                });
            var vm = new StatsViewModel(new StatService(api.Client), new SeasonService(api.Client));
            await vm.InitializeAsync();
            return vm;
        }

        [Fact]
        public async Task Initialize_PicksTheCurrentSeason_AndTotalsIt()
        {
            var vm = await Loaded();

            Assert.Equal(3, vm.SelectedSeasonId);
            Assert.Equal(4, vm.GamesPlayed);
            Assert.Equal(16, vm.TotalGoals);
            Assert.Equal("4.0", vm.GoalsPerGame);
        }

        [Fact]
        public async Task Sorted_ByGoals_BreaksTiesOnAssists()
        {
            var vm = await Loaded();

            Assert.Equal(new[] { "Dan", "Ann", "Bob", "Cat" }, vm.Sorted.Select(p => p.Name));
        }

        [Fact]
        public async Task SortBy_SameColumnFlipsDirection_NameStartsAscending()
        {
            var vm = await Loaded();

            vm.SortBy("name");
            Assert.Equal("Ann", vm.Sorted.First().Name);
            vm.SortBy("name");
            Assert.Equal("Dan", vm.Sorted.First().Name);
        }

        [Fact]
        public async Task Filtered_ByPositionAndName()
        {
            var vm = await Loaded();

            vm.SelectedPos = "FWD";
            vm.NameSearch = "an";

            Assert.Equal(new[] { "Ann", "Dan" }, vm.Filtered.Select(p => p.Name).OrderBy(n => n));
        }

        [Fact]
        public async Task Leaders_CleanSheetsOnlyForKeepersAndDefenders()
        {
            var vm = await Loaded();

            var cs = vm.Leaders.Single(l => l.Title == "Most clean sheets");
            Assert.Equal("Cat", Assert.Single(cs.Top).Player.Name);
        }

        [Fact]
        public async Task ShowPlayer_ClearsFiltersAndOpensTheRow()
        {
            var vm = await Loaded();
            vm.SelectedPos = "GK";
            vm.NameSearch = "zzz";

            vm.ShowPlayer(2);

            Assert.Equal(("All", "", 2), (vm.SelectedPos, vm.NameSearch, vm.ExpandedId));
        }

        [Fact]
        public async Task TeamHighlights_BestAttackAndDefence()
        {
            var vm = await Loaded();

            var h = vm.TeamHighlights.ToDictionary(x => x.Title, x => x.Team.Name);
            Assert.Equal("Rovers", h["Best attack"]);
            Assert.Equal("Rovers", h["Best defence"]);
        }
    }
}
