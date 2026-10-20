using System.Net;
using Ballers.Client.Services;
using Ballers.Models;
using Ballers.Services;
using Ballers.Shared;
using Ballers.UI.Tests.Helpers;
using Ballers.ViewModels;

namespace Ballers.UI.Tests
{
    // The form-style pages: validation and what each save sends.
    public class FormViewModelTests
    {
        [Fact]
        public async Task PlayerSignup_ChecksEachFieldInTurn()
        {
            var api = new FakeApi().Get("api/teams", new List<TeamProfileDto> { new() { Id = 1, Name = "Rovers" } });
            var vm = new PlayerSignupViewModel(new PlayerAccountService(api.Client), new TeamService(api.Client));
            await vm.InitializeAsync();

            await vm.Submit();
            Assert.Equal("Choose your team.", vm.Error);

            await vm.SelectTeam(1);
            vm.Name = "Ann";
            vm.Email = "a@x";
            vm.Password = "Secret1!";
            vm.ConfirmPassword = "Different1!";
            await vm.Submit();
            Assert.Equal("The two passwords don't match.", vm.Error);

            vm.ConfirmPassword = "Secret1!";
            await vm.Submit();
            Assert.True(vm.Submitted);
            Assert.Equal("Rovers", vm.TeamName);
        }

        [Fact]
        public async Task PlayerSignup_ClaimingAPlaceFillsInTheName()
        {
            var api = new FakeApi()
                .Get("api/teams", new List<TeamProfileDto> { new() { Id = 1, Name = "Rovers" } })
                .Get("api/playeraccount/teams/1/claimable", new List<ClaimablePlayerDto> { new() { Id = 7, Name = "Ann Smith" } });
            var vm = new PlayerSignupViewModel(new PlayerAccountService(api.Client), new TeamService(api.Client));
            await vm.InitializeAsync();

            await vm.SelectTeam(1);
            vm.SelectPlayer(7);

            Assert.Equal("Ann Smith", vm.Name);
        }

        [Fact]
        public async Task MySquad_AddPlayerNeedsANameAndNumber()
        {
            var api = new FakeApi().Get("api/players/my-team", new List<PlayerDto>());
            var vm = new MySquadViewModel(new PlayerService(api.Client), new LeagueSettingsService(api.Client),
                new PlayerAccountService(api.Client), new FakeAuth(1, "Manager"));
            await vm.InitializeAsync();

            await vm.AddPlayer();
            Assert.Equal("Please enter a player name.", vm.FormError);

            vm.NewName = "Ann";
            await vm.AddPlayer();
            Assert.Equal("Please enter a valid squad number.", vm.FormError);
        }

        [Fact]
        public async Task Referees_EditReplacesTheRowInPlace()
        {
            var api = new FakeApi()
                .Get("api/referees", new List<RefereeDto> { new() { Id = 1, Name = "Old" } })
                .On(HttpMethod.Put, "api/referees/1", () => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = System.Net.Http.Json.JsonContent.Create(new RefereeDto { Id = 1, Name = "New" })
                });
            var vm = new RefereesViewModel(new RefereeService(api.Client), new FakeAuth(null, "Admin"));
            await vm.InitializeAsync();

            vm.StartEdit(vm.Referees[0]);
            vm.Form.Name = "New";
            await vm.Save();

            Assert.Equal("New", Assert.Single(vm.Referees).Name);
            Assert.Null(vm.EditingId);
        }

        [Fact]
        public async Task AdminCards_SetFieldIgnoresJunkAndNegatives()
        {
            var api = new FakeApi().Get("api/admin/points-settings", new PointsSettingsDto { PointsPerGoal = 3 });
            var vm = new AdminCardsViewModel(new AdminService(api.Client));
            await vm.InitializeAsync();
            var goal = vm.EarnFields.Single(f => f.Label == "Goal");

            vm.SetField(goal, "abc");
            Assert.Equal(3, vm.Settings!.PointsPerGoal);

            vm.SetField(goal, "-4");
            Assert.Equal(0, vm.Settings.PointsPerGoal);
            Assert.True(vm.Dirty);
        }

        [Fact]
        public async Task AdminAccounts_FiltersByRoleAndSearch_FromTheLink()
        {
            var api = new FakeApi().Get("api/admin/accounts", new List<AdminAccountDto>
            {
                new() { Id = "1", Email = "boss@x", Role = "Admin" },
                new() { Id = "2", Email = "new@x", Role = "Pending", PlayerName = "Ann" },
                new() { Id = "3", Email = "other@x", Role = "Pending", PlayerName = "Bob" }
            });
            var vm = new AdminAccountsViewModel(new AdminService(api.Client));

            await vm.InitializeAsync("Pending");
            vm.Search = "ann";

            Assert.Equal("2", Assert.Single(vm.Filtered).Id);
        }

        [Fact]
        public async Task AdminSeason_WizardNeedsTwoTeamsToGenerate()
        {
            var api = new FakeApi()
                .Get("api/seasons", new List<SeasonDto> { new() { Id = 1, SeasonNumber = 4 } })
                .Get("api/teams", new List<TeamDto> { new() { Id = 1 }, new() { Id = 2 } });
            var vm = new AdminSeasonViewModel(new AdminService(api.Client), new KnockoutService(api.Client));
            await vm.InitializeAsync();

            vm.OpenWizard();
            vm.Step = 1;
            Assert.True(vm.CanContinue);
            Assert.Equal(5, vm.NextSeasonNumber);

            vm.ToggleTeam(2, false);
            Assert.False(vm.CanContinue);

            vm.SelectAllTeams();
            Assert.True(vm.CanContinue);
        }

        [Fact]
        public async Task AdminSettings_SwitchFailureShowsTheReason()
        {
            var api = new FakeApi()
                .Get("api/notifications/settings", new List<NotificationSettingDto>())
                .On(HttpMethod.Put, "api/league-settings/players-locked", HttpStatusCode.Forbidden, "Admins only");
            var vm = new AdminSettingsViewModel(new LeagueSettingsService(api.Client), new NotificationService(api.Client));
            await vm.InitializeAsync();

            await vm.SetPlayersLocked(true);

            Assert.True(vm.LeagueIsError);
            Assert.False(vm.PlayersLocked);
        }
    }
}
