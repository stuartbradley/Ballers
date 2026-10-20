using System.Net;
using Ballers.Services;
using Ballers.Shared;
using Ballers.UI.Tests.Helpers;
using Ballers.ViewModels;

namespace Ballers.UI.Tests
{
    public class MyCardViewModelTests
    {
        private static MyPlayerCardDto Card(int each, int unspent, int cap = 99) => new()
        {
            Name = "Ann", Position = "MID", UnspentPoints = unspent, AttributeCap = cap,
            Attributes = new() { Finishing = each, Tackling = each, Passing = each, Dribbling = each, Pace = each, Strength = each, Overall = each }
        };

        private static async Task<(MyCardViewModel Vm, FakeApi Api)> Loaded(MyPlayerCardDto card)
        {
            var api = new FakeApi()
                .Get("api/playercard/tiers", new CardTierThresholdsDto { SilverOverallThreshold = 20, GoldOverallThreshold = 35 })
                .Get("api/playercard/me", card)
                .Get("api/playercard/me/awards", new List<PlayerPointsAwardDto>());
            var vm = new MyCardViewModel(new PlayerCardService(api.Client)) { Delay = _ => Task.CompletedTask };
            await vm.InitializeAsync();
            return (vm, api);
        }

        [Fact]
        public async Task Step_CannotSpendMoreThanRemaining()
        {
            var (vm, _) = await Loaded(Card(10, unspent: 2));
            var row = vm.Rows[0];

            vm.Step(row, 1);
            vm.Step(row, 1);
            vm.Step(row, 1);

            Assert.Equal(2, row.Added);
            Assert.Equal(0, vm.Remaining);
        }

        [Fact]
        public async Task Step_CannotPassTheAttributeCap()
        {
            var (vm, _) = await Loaded(Card(10, unspent: 5, cap: 11));

            vm.Step(vm.Rows[0], 1);
            vm.Step(vm.Rows[0], 1);

            Assert.Equal(1, vm.Rows[0].Added);
            Assert.False(vm.CanAdd(vm.Rows[0]));
        }

        [Fact]
        public async Task Preview_ShowsTheTierTheSpendWouldReach()
        {
            var (vm, _) = await Loaded(Card(19, unspent: 6));
            Assert.Equal("bronze", vm.DisplayTier);

            foreach (var row in vm.Rows) vm.Step(row, 1);

            Assert.Equal(20, vm.PreviewOverall);
            Assert.Equal("silver", vm.PreviewTier);
            Assert.True(vm.PreviewIsUpgrade);
        }

        [Fact]
        public async Task Reset_PutsEveryPointBack()
        {
            var (vm, _) = await Loaded(Card(10, unspent: 3));
            vm.Step(vm.Rows[0], 1);
            vm.Step(vm.Rows[1], 1);

            vm.Reset();

            Assert.Equal(0, vm.Allocated);
        }

        [Fact]
        public async Task Save_MovingUpATierPlaysTheUpgrade()
        {
            var (vm, api) = await Loaded(Card(19, unspent: 6));
            api.Get("api/playercard/me", Card(20, unspent: 0));
            api.On(HttpMethod.Post, "api/playercard/me/allocate", () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(Card(20, unspent: 0))
            });
            foreach (var row in vm.Rows) vm.Step(row, 1);

            await vm.Save();

            Assert.True(vm.Saved);
            Assert.Equal(("bronze", "silver"), (vm.UpgradeFrom, vm.UpgradeTo));
            Assert.Equal("silver", vm.DisplayTier);
            Assert.False(vm.Upgrading);
        }

        [Fact]
        public async Task Save_FailureKeepsTheReason()
        {
            var (vm, api) = await Loaded(Card(10, unspent: 2));
            api.On(HttpMethod.Post, "api/playercard/me/allocate", HttpStatusCode.BadRequest, "Not enough points");
            vm.Step(vm.Rows[0], 1);

            await vm.Save();

            Assert.Equal("Not enough points", vm.Error);
            Assert.False(vm.Saving);
        }
    }
}
