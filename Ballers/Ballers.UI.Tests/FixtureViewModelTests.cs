using Ballers.Client.Auth;
using Ballers.Models;
using Ballers.Services;
using Ballers.UI.Tests.Helpers;
using Ballers.ViewModels;

namespace Ballers.UI.Tests
{
    public class FixtureViewModelTests
    {
        private const int Home = 1, Away = 2;

        private static FakeApi Api(bool admin = false, int? teamId = Home, int squadSize = 11, DateTime? kickoff = null)
        {
            var api = new FakeApi()
                .Get("api/auth/me", new UserInfo { Email = "u@x", IsAdmin = admin, TeamId = admin ? null : teamId })
                .Get("api/fixtures/7", new FixtureDetailsDto
                {
                    Id = 7, HomeTeam = "Rovers", AwayTeam = "Unity", HomeTeamId = Home, AwayTeamId = Away,
                    Kickoff = kickoff ?? DateTime.Now.AddDays(3), Location = "Park", RefereeId = 1,
                    WindowStart = DateTime.Today, WindowEnd = DateTime.Today.AddDays(7)
                });

            var players = Enumerable.Range(1, 12).Select(i => new PlayerDto { Id = i, Name = $"P{i}", Number = i, Position = "MID" }).ToList();
            api.Get("api/fixtures/7/players", players).Get($"api/fixtures/7/players?teamId={Home}", players);
            api.Get("api/fixtures/7/squad", players.Take(squadSize).Select(p => new FixtureSquadPlayerDto { PlayerId = p.Id, Name = p.Name }).ToList());
            return api;
        }

        private static FixtureViewModel Build(FakeApi api)
        {
            var http = api.Client;
            return new FixtureViewModel(
                new FixtureService(http), new GalleryService(http), new AuthService(http), new PenaltyService(http),
                new FairplayService(http), new RefereeService(http), new LeagueSettingsService(http))
            {
                Delay = _ => Task.CompletedTask
            };
        }

        [Fact]
        public async Task Initialize_ManagerSeesTheirSquadPicked_AndOpensOnSquad()
        {
            var vm = Build(Api());

            await vm.InitializeAsync(7, null);

            Assert.False(vm.IsLoading);
            Assert.True(vm.ViewingHome);
            Assert.False(vm.IsOfficial);
            Assert.True(vm.SquadSelected);
            Assert.Equal(11, vm.PickedCount);
            Assert.Equal(11, vm.Players.Count);
            Assert.Equal("squad", vm.Step);
        }

        [Fact]
        public async Task Initialize_AfterKickoffOpensOnTheResult()
        {
            var vm = Build(Api(kickoff: DateTime.Now.AddHours(-3)));

            await vm.InitializeAsync(7, null);

            Assert.Equal("result", vm.Step);
            Assert.Equal("Send your stats", vm.StatusText);
        }

        [Fact]
        public async Task GoTo_ResultIsLockedUntilTheSquadIsPicked()
        {
            var vm = Build(Api(squadSize: 5));
            await vm.InitializeAsync(7, null);

            vm.GoTo("result");

            Assert.Equal("squad", vm.Step);
        }

        [Fact]
        public async Task TogglePick_DroppingTheCaptainClearsTheArmband()
        {
            var vm = Build(Api());
            await vm.InitializeAsync(7, null);
            var p = vm.TeamPlayers.First(x => x.IsSelected);
            vm.SetCaptain(p.PlayerId);

            vm.TogglePick(p);

            Assert.Null(vm.CaptainPlayerId);
        }

        [Fact]
        public async Task SetVice_CannotAlsoBeCaptain()
        {
            var vm = Build(Api());
            await vm.InitializeAsync(7, null);

            vm.SetCaptain(3);
            vm.SetVice(3);

            Assert.Equal((null, 3), (vm.CaptainPlayerId, vm.VicePlayerId));
        }

        [Fact]
        public async Task SelectMotm_OnlyOnePlayerAtATime()
        {
            var vm = Build(Api());
            await vm.InitializeAsync(7, null);

            vm.SelectMotm(vm.Players[0]);
            vm.SelectMotm(vm.Players[1]);

            Assert.Equal(new[] { vm.Players[1].PlayerId }, vm.Players.Where(p => p.IsMotm).Select(p => p.PlayerId));
        }

        [Fact]
        public async Task StartReview_NeedsAManOfTheMatch()
        {
            var vm = Build(Api());
            await vm.InitializeAsync(7, null);

            await vm.StartReview();
            Assert.False(vm.Reviewing);
            Assert.False(vm.ToastSuccess);

            vm.SelectMotm(vm.Players[0]);
            await vm.StartReview();
            Assert.True(vm.Reviewing);
        }

        [Fact]
        public async Task Goals_UpdateTheScorePreview_OwnGoalsCountForTheOtherSide()
        {
            var vm = Build(Api());
            await vm.InitializeAsync(7, null);

            vm.Players[0].Goals = 2;
            vm.Players[1].OwnGoals = 1;

            Assert.Equal((2, 1), (vm.PreviewHomeScore, vm.PreviewAwayScore));
        }

        [Fact]
        public async Task SubmitStats_SendsOurRows_AndAManagerSendsNoTeam()
        {
            var api = Api();
            var vm = Build(api);
            await vm.InitializeAsync(7, null);
            vm.Players[0].Goals = 1;
            vm.SelectMotm(vm.Players[0]);

            await vm.SubmitStats();

            var body = Assert.Single(api.BodiesSentTo(HttpMethod.Post, "api/fixtures/7/stats"));
            Assert.Contains("\"teamId\":null", body);
            Assert.Contains("\"isManOfTheMatch\":true", body);
            Assert.False(vm.Busy);
        }

        [Fact]
        public async Task SaveSquad_FailureShowsTheApiReason()
        {
            var api = Api().On(HttpMethod.Post, "api/fixtures/7/squad", System.Net.HttpStatusCode.Conflict, "Fixtures are locked");
            var vm = Build(api);
            await vm.InitializeAsync(7, null);

            await vm.SaveSquad();

            Assert.False(vm.ToastSuccess);
            Assert.Contains("Fixtures are locked", vm.ToastMessage);
            Assert.False(vm.Busy);
        }

        [Fact]
        public async Task Admin_DefaultsToHomeSide_AndSwitchingReloadsForTheAwayTeam()
        {
            var api = Api(admin: true);
            api.Get($"api/fixtures/7/players?teamId={Away}", new List<PlayerDto> { new() { Id = 50, Name = "Away lad", Position = "GK" } });
            var vm = Build(api);
            await vm.InitializeAsync(7, null);
            Assert.True(vm.IsOfficial && vm.ViewingHome);

            await vm.SwitchActingTeam(Away);

            Assert.False(vm.ViewingHome);
            Assert.Equal("Away lad", Assert.Single(vm.TeamPlayers).Name);
        }

        [Fact]
        public async Task SaveFairplay_RejectsRatingsOutOfRange()
        {
            var api = Api(admin: true);
            var vm = Build(api);
            await vm.InitializeAsync(7, null);
            vm.FairplayHomeRating = 11;

            await vm.SaveFairplay();

            Assert.False(vm.ToastSuccess);
            Assert.False(api.WasCalled(HttpMethod.Post, "api/fairplay/7"));
        }

        [Fact]
        public async Task SubmitPenalties_NeedsAllFiveTakers()
        {
            var api = Api();
            var vm = Build(api);
            await vm.InitializeAsync(7, null);
            vm.PenaltyKicks[0].PlayerId = 1;

            await vm.SubmitPenalties();

            Assert.Equal("Pick a taker for all 5 kicks.", vm.ToastMessage);
            Assert.False(api.WasCalled(HttpMethod.Post, "api/penalty/7/kicks"));
        }
    }
}
