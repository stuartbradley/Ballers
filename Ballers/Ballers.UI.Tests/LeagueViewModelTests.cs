using Ballers.Models;
using Ballers.Services;
using Ballers.Shared;
using Ballers.UI.Tests.Helpers;
using Ballers.ViewModels;

namespace Ballers.UI.Tests
{
    // The read-mostly league pages: Team of the Week, the knockout and the HQ.
    public class LeagueViewModelTests
    {
        private static TeamOfTheWeekDto Week(int n) => new(n, 1, n, DateTime.Today, new List<TotwPlayerDto>
        {
            new(1, "Keeper", 1, "Rovers", "GK", true, 0, 0, 0, 1, null),
            new(2, "Striker", 1, "Rovers", "FWD", false, 2, 0, null, null, null) { TeamScore = 2, OpponentScore = 1, OpponentName = "Unity" }
        });

        private static async Task<TeamOfTheWeekViewModel> Totw()
        {
            var api = new FakeApi()
                // Newest first, as the API sends them.
                .Get("api/totw/weeks", new List<TotwWeekRefDto> { new(3, DateTime.Today), new(2, DateTime.Today), new(1, DateTime.Today) })
                .Get("api/totw/current", Week(3))
                .Get("api/totw/2", Week(2))
                .Get("api/totw/1", Week(1));
            var vm = new TeamOfTheWeekViewModel(new TotwService(api.Client));
            await vm.InitializeAsync();
            vm.StopPrefetch();
            return vm;
        }

        [Fact]
        public async Task Totw_OpensOnTheLatestWeek_AtTheRightOfTheStrip()
        {
            var vm = await Totw();

            Assert.Equal(3, vm.Current!.MatchNumber);
            Assert.Equal(2, vm.CurrentIndex);
            Assert.False(vm.CanGoNewer);
            Assert.True(vm.CanGoOlder);
        }

        [Fact]
        public async Task Totw_GoOlderStepsBackAWeek()
        {
            var vm = await Totw();

            await vm.GoOlder();
            await vm.GoOlder();

            Assert.Equal(1, vm.Current!.MatchNumber);
            Assert.False(vm.CanGoOlder);
        }

        [Fact]
        public async Task Totw_LinesRunAttackToGoal_WithTheirResult()
        {
            var vm = await Totw();

            Assert.Equal(new[] { "Forwards", "Goalkeepers" }, vm.Lines.Select(l => l.Name));
            Assert.Equal(("W", "2–1 vs Unity"), TeamOfTheWeekViewModel.Caption(vm.Current!.Players[1]));
        }

        [Fact]
        public async Task Knockout_WinnerOnlyOnceTheFinalIsPlayed()
        {
            var api = new FakeApi()
                .Get("api/seasons/current", new SeasonDto { Id = 1 })
                .Get("api/knockout/1", new KnockoutBracketDto
                {
                    Fixtures =
                    {
                        new() { Tournament = "Cup", Round = "Semifinal", Slot = 2 },
                        new() { Tournament = "Cup", Round = "Semifinal", Slot = 1 },
                        new() { Tournament = "Cup", Round = "Final", HomeTeamName = "Rovers", AwayTeamName = "Unity", HomeScore = 1, AwayScore = 3, IsPlayed = true },
                        new() { Tournament = "Plate", Round = "Final", IsPlayed = false }
                    }
                });
            var vm = new KnockoutViewModel(new KnockoutService(api.Client), new SeasonService(api.Client), new FakeAuth(4, "Manager"));
            await vm.InitializeAsync();

            Assert.Equal(new[] { 1, 2 }, vm.GetSemifinals("Cup").Select(f => f.Slot));
            Assert.Equal("Unity", vm.GetWinner("Cup"));
            Assert.Null(vm.GetWinner("Plate"));
            Assert.Equal(4, vm.UserTeamId);
        }

        [Fact]
        public async Task ManagerHq_ApprovingASignupDropsItAndRefreshes()
        {
            var api = new FakeApi()
                .Get("api/manager/dashboard", new ManagerDashboardDto { TeamName = "Rovers" })
                .Get("api/playeraccount/requests", new List<PlayerAccountRequestDto>
                {
                    new() { Id = 1, RequestedName = "Ann", Status = PlayerAccountRequestStatusDto.Pending },
                    new() { Id = 2, RequestedName = "Old", Status = PlayerAccountRequestStatusDto.Approved }
                });
            var vm = new ManagerHqViewModel(new ManagerService(api.Client), new PlayerAccountService(api.Client));
            await vm.InitializeAsync();
            var ann = Assert.Single(vm.Signups);

            await vm.Resolve(ann, approve: true);

            Assert.Empty(vm.Signups);
            Assert.StartsWith("🎉 Ann is in!", vm.SignupMessage);
            Assert.True(api.WasCalled(HttpMethod.Post, "api/playeraccount/requests/1/approve"));
        }

        [Fact]
        public void ManagerHq_BubbleText_PutsUrgentJobsFirst()
        {
            var h = new ManagerDashboardDto();
            var todo = new List<ManagerTodoDto> { new() { Urgent = true }, new() };

            Assert.StartsWith("📣 1 urgent job,", ManagerHqViewModel.BubbleText(h, todo));
            Assert.StartsWith("🙌", ManagerHqViewModel.BubbleText(h, new()));
        }
    }
}
