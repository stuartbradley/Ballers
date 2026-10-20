using Ballers.Models;
using Ballers.Services;
using Ballers.Shared;
using Ballers.UI.Tests.Helpers;
using Ballers.ViewModels;

namespace Ballers.UI.Tests
{
    // The two fixture lists: the public schedule and the manager's own.
    public class FixtureListViewModelTests
    {
        private static FixtureMatchDto M(string home, string away, bool played) => new() { Home = home, Away = away, IsPlayed = played };

        private static async Task<FixturesViewModel> Schedule(bool finished = false)
        {
            var api = new FakeApi()
                .Get("api/seasons", new List<SeasonDto> { new() { Id = 1, SeasonNumber = 1, IsActive = true } })
                .Get("api/fixtures/weeks?seasonId=1", new List<FixtureWeekDto>
                {
                    new() { WeekNumber = 1, Matches = { M("Rovers", "Unity", true) }, Byes = { "Athletic" } },
                    new() { WeekNumber = 2, Matches = { M("Unity", "Athletic", finished), M("Rovers", "City", finished) }, Byes = { "Hearts" } }
                });
            var vm = new FixturesViewModel(new FixtureService(api.Client), new SeasonService(api.Client), new GalleryService(api.Client));
            await vm.InitializeAsync();
            return vm;
        }

        [Fact]
        public async Task Schedule_OpensOnWhatsNext_WhileTheSeasonIsOn()
        {
            var vm = await Schedule();

            Assert.Equal(FixturesViewModel.Tab.Upcoming, vm.SelectedTab);
            Assert.Equal(2, Assert.Single(vm.VisibleWeeks).WeekNumber);
            Assert.Equal(2, vm.CurrentWeek!.WeekNumber);
        }

        [Fact]
        public async Task Schedule_OpensOnResults_OnceItsFinished()
        {
            var vm = await Schedule(finished: true);

            Assert.Equal(FixturesViewModel.Tab.Results, vm.SelectedTab);
            Assert.Equal(new[] { 2, 1 }, vm.VisibleWeeks.Select(w => w.WeekNumber));
            // Byes only show for weeks still to play.
            Assert.All(vm.VisibleWeeks, w => Assert.Empty(w.Byes));
        }

        [Fact]
        public async Task Schedule_TeamFilterNarrowsGamesAndByes()
        {
            var vm = await Schedule();

            vm.TeamFilter = "Rovers";

            var week = Assert.Single(vm.VisibleWeeks);
            Assert.Equal("City", Assert.Single(week.Matches).Away);
            Assert.Empty(week.Byes);
            Assert.Equal((1, 1), (vm.PlayedCount, vm.UpcomingCount));
        }

        private static FixtureListItemDto F(DateTime kickoff, bool home = true) => new()
        {
            Id = 9, HomeTeam = "Rovers", AwayTeam = "Unity", Kickoff = kickoff, IsHomeTeamManager = home,
            WindowStart = kickoff.Date, WindowEnd = kickoff.Date.AddDays(7)
        };

        private static async Task<ManagerFixturesViewModel> Manager(bool locked = false, params string[] roles)
        {
            var api = new FakeApi()
                .Get("api/seasons", new List<SeasonDto>())
                .Get("api/league-settings", new LeagueSettingsDto { FixturesLocked = locked });
            var vm = new ManagerFixturesViewModel(new FixtureService(api.Client), new SeasonService(api.Client),
                new LeagueSettingsService(api.Client), new FakeAuth(1, roles.Length == 0 ? new[] { "Manager" } : roles));
            await vm.InitializeAsync();
            return vm;
        }

        [Fact]
        public async Task ActionFor_PastGameWithoutStats_AsksForThem()
        {
            var vm = await Manager();
            var f = F(DateTime.Now.AddDays(-2));
            f.ManagerSquadCount = ManagerFixturesViewModel.SquadMinimum;

            var a = vm.ActionFor(f);

            Assert.Equal(("Send stats", "/fixture/9?tab=stats", "is-red"), (a.Label, a.Link, a.Tone));
            Assert.True(vm.NeedsSorting(f));
        }

        [Fact]
        public async Task ActionFor_UpcomingHomeGame_AsksForSetupFirst()
        {
            var vm = await Manager();

            Assert.Equal("Set up", vm.ActionFor(F(DateTime.Now.AddDays(3))).Label);
            Assert.Equal("Pick squad", vm.ActionFor(F(DateTime.Now.AddDays(3), home: false)).Label);
        }

        [Fact]
        public async Task ActionFor_LockedOrStaff_OnlyViews()
        {
            Assert.Equal("View", (await Manager(locked: true)).ActionFor(F(DateTime.Now.AddDays(-2))).Label);
            Assert.Equal("View", (await Manager(false, "Referee")).ActionFor(F(DateTime.Now.AddDays(-2))).Label);
        }
    }
}
