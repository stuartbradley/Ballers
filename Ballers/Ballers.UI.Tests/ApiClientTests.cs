using System.Net;
using Ballers.Models;
using Ballers.Services;
using Ballers.Shared;
using Ballers.UI.Infrastructure;
using Ballers.UI.Tests.Helpers;
using Ballers.ViewModels;

namespace Ballers.UI.Tests
{
    // The one rule every UI service follows: reads that fail come back empty,
    // writes that fail throw an ApiException carrying the API's reason.
    public class ApiClientTests
    {
        // An API that cannot be reached at all.
        private sealed class Unreachable : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
                => throw new HttpRequestException("connection refused");
        }

        private static HttpClient Offline => new(new Unreachable()) { BaseAddress = new Uri("https://api.test/") };

        [Fact]
        public async Task Read_WhenTheApiIsDown_ComesBackEmpty()
        {
            Assert.Null(await Offline.GetOrDefault<SeasonDto>("api/seasons/current"));
            Assert.Empty(await Offline.GetListOrEmpty<SeasonDto>("api/seasons"));
        }

        [Fact]
        public async Task Write_WhenTheApiIsDown_ThrowsTheFallback()
        {
            var ex = await Assert.ThrowsAsync<ApiException>(() => Offline.PostOk("api/x", new { }, "Couldn't save."));
            Assert.Equal(("Couldn't save.", 0), (ex.Message, ex.StatusCode));
        }

        [Fact]
        public async Task Write_Refused_ThrowsTheApisReason()
        {
            var api = new FakeApi().On(HttpMethod.Delete, "api/x/1", HttpStatusCode.Conflict, "Still in use");

            var ex = await Assert.ThrowsAsync<ApiException>(() => api.Client.DeleteOk("api/x/1", "Couldn't delete."));

            Assert.Equal(("Still in use", 409), (ex.Message, ex.StatusCode));
        }

        [Fact]
        public async Task Write_SendsTheTypedRequestAsCamelCaseJson()
        {
            var api = new FakeApi();

            await new FixtureService(api.Client).SaveCaptaincy(7, captainId: 3, viceId: null, teamId: 2);

            var body = Assert.Single(api.BodiesSentTo(HttpMethod.Put, "api/fixtures/7/captaincy"));
            Assert.Equal("""{"captainPlayerId":3,"viceCaptainPlayerId":null,"teamId":2}""", body);
        }

        [Fact]
        public async Task Stats_AreSentWithoutTheFormOnlyFields()
        {
            var api = new FakeApi();

            await new FixtureService(api.Client).SubmitStats(7,
                new List<FixturePlayerStatInput> { new() { PlayerId = 1, Name = "Ann", Position = "FWD", Goals = 2 } });

            var body = Assert.Single(api.BodiesSentTo(HttpMethod.Post, "api/fixtures/7/stats"))!;
            Assert.Contains("\"goals\":2", body);
            Assert.DoesNotContain("Ann", body);
        }

        [Fact]
        public async Task AdminGalleries_ShowsWhyACreateWasRefused()
        {
            var api = new FakeApi()
                .On(HttpMethod.Post, "api/galleries", HttpStatusCode.Conflict, "That fixture already has a gallery.");
            var vm = new AdminGalleriesViewModel(new GalleryService(api.Client), new FixtureService(api.Client))
            {
                NewGalleryFixtureId = 4
            };

            await vm.CreateGallery();

            Assert.Equal("That fixture already has a gallery.", vm.Message);
            Assert.True(vm.MessageIsError);
        }

        [Fact]
        public async Task Referees_ShowsWhyASaveWasRefused()
        {
            var api = new FakeApi().On(HttpMethod.Post, "api/referees", HttpStatusCode.BadRequest, "Name is required.");
            var vm = new RefereesViewModel(new RefereeService(api.Client), new FakeAuth(null, "Admin"));
            await vm.InitializeAsync();
            vm.Form.Name = "Pat";

            await vm.Save();

            Assert.Equal("Name is required.", vm.FormError);
            Assert.False(vm.IsSaving);
        }
    }
}
