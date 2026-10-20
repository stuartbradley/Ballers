using Ballers.Services;
using Ballers.Shared;
using Ballers.UI.Tests.Helpers;
using Ballers.ViewModels;

namespace Ballers.UI.Tests
{
    public class GalleryViewModelTests
    {
        private static List<GalleryPhotoDto> Photos(int from, int count)
            => Enumerable.Range(from, count).Select(i => new GalleryPhotoDto { Id = i, ImageUrl = $"/p/{i}.jpg" }).ToList();

        private static FakeApi Api()
        {
            var size = GalleryViewModel.PageSize;
            return new FakeApi()
                .Get("api/galleries/5", new GalleryDetailDto { Id = 5, HomeTeam = "Rovers FC", AwayTeam = "Unity", Week = 3 })
                .Get("api/galleries", new List<GallerySummaryDto>
                {
                    new() { Id = 6, HomeTeam = "A", AwayTeam = "B" },
                    new() { Id = 5, HomeTeam = "Rovers FC", AwayTeam = "Unity" },
                    new() { Id = 4, HomeTeam = "C", AwayTeam = "D" }
                })
                .Get($"api/galleries/5/photos?skip=0&take={size}", new GalleryPhotoPageDto { Photos = Photos(0, size), HasMore = true })
                .Get($"api/galleries/5/photos?skip={size}&take={size}", new GalleryPhotoPageDto { Photos = Photos(size, 3), HasMore = false });
        }

        private static async Task<GalleryViewModel> Loaded()
        {
            var vm = new GalleryViewModel(new GalleryService(Api().Client)) { Delay = _ => Task.CompletedTask };
            await vm.LoadAsync(5);
            return vm;
        }

        [Fact]
        public async Task Load_GetsTheFirstPage_AndItsNeighbours()
        {
            var vm = await Loaded();

            Assert.Equal(GalleryViewModel.PageSize, vm.Photos.Count);
            Assert.True(vm.HasMore);
            Assert.Equal((6, 4), (vm.Newer!.Id, vm.Older!.Id));
        }

        [Fact]
        public async Task LoadMore_StopsAtTheEnd()
        {
            var vm = await Loaded();

            await vm.LoadMoreAsync();
            await vm.LoadMoreAsync();

            Assert.Equal(GalleryViewModel.PageSize + 3, vm.Photos.Count);
            Assert.False(vm.HasMore);
        }

        [Fact]
        public async Task ShowNext_NearTheEndPullsTheNextPageIn()
        {
            var vm = await Loaded();
            vm.OpenLightbox(GalleryViewModel.PageSize - 2);

            await vm.ShowNext();

            Assert.Equal(GalleryViewModel.PageSize - 1, vm.LightboxIndex);
            Assert.Equal(GalleryViewModel.PageSize + 3, vm.Photos.Count);
        }

        [Fact]
        public async Task ShowPrevious_StopsAtTheFirstPhoto()
        {
            var vm = await Loaded();
            vm.OpenLightbox(0);

            vm.ShowPrevious();

            Assert.Equal(0, vm.LightboxIndex);
            Assert.True(vm.FocusLightboxPending);
        }

        [Fact]
        public async Task CurrentDownload_NamesTheFileAfterTheMatch()
        {
            var vm = await Loaded();
            vm.OpenLightbox(2);

            Assert.Equal("ballers-mw3-rovers-fc-v-unity-3.jpg", vm.CurrentDownload!.Value.FileName);
        }

        [Fact]
        public async Task ShareResult_OnlyCopyingAndFailureShowALabel()
        {
            var vm = await Loaded();
            string? shown = null;
            vm.Delay = _ => { shown = vm.ShareLabel; return Task.CompletedTask; };

            await vm.ShowShareResult("copied");
            Assert.Equal("Link copied", shown);
            Assert.Null(vm.ShareLabel);

            shown = null;
            await vm.ShowShareResult("shared");
            Assert.Null(shown);
        }
    }
}
