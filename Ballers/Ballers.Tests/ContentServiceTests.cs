using Ballers.API.Models;
using Ballers.API.Services;
using Ballers.Models;
using Ballers.Shared;
using Ballers.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Ballers.Tests
{
    // The services pulled out of the gallery, photographer and referee controllers.
    public class ContentServiceTests
    {
        private static UploadedFile File(string name, string type = "image/jpeg", long length = 100)
            => new(name, type, length, () => new MemoryStream(new byte[1]));

        private static GalleryService Galleries(string db, out Ballers.API.Data.ApplicationDbContext context)
        {
            context = DbContextFactory.Create(db);
            var storage = new Mock<IGalleryStorageService>();
            storage.Setup(s => s.SaveAsync(It.IsAny<int>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int g, Stream _, string name, CancellationToken _) => new StoredPhoto($"/g/{g}/{name}", $"/g/{g}/t-{name}", 1, 1));
            return new GalleryService(context, storage.Object);
        }

        [Fact]
        public async Task Gallery_CreateNeedsAFixture_AndOnlyOnePerFixture()
        {
            var svc = Galleries(nameof(Gallery_CreateNeedsAFixture_AndOnlyOnePerFixture), out var db);

            Assert.Equal(OutcomeStatus.Invalid, (await svc.CreateAsync(new SaveGalleryRequest { FixtureId = 1 })).Result.Status);

            db.Fixtures.Add(new Fixture { Id = 1, HomeTeamId = 1, AwayTeamId = 2 });
            await db.SaveChangesAsync();
            var created = await svc.CreateAsync(new SaveGalleryRequest { FixtureId = 1 });
            Assert.True(created.Succeeded);

            Assert.Equal(OutcomeStatus.Conflict, (await svc.CreateAsync(new SaveGalleryRequest { FixtureId = 1 })).Result.Status);
        }

        [Fact]
        public async Task Gallery_UploadRejectsBadFiles_AndOrdersByFileName()
        {
            var svc = Galleries(nameof(Gallery_UploadRejectsBadFiles_AndOrdersByFileName), out var db);
            db.Galleries.Add(new Gallery { Id = 5, FixtureId = 1 });
            await db.SaveChangesAsync();

            var result = await svc.UploadAsync(5, new[]
            {
                File("IMG_10.jpg"), File("IMG_2.jpg"), File("notes.txt", "text/plain"), File("huge.jpg", length: 16L * 1024 * 1024)
            });

            Assert.Equal(2, result.Value!.Added);
            Assert.Equal(2, result.Value.Rejected.Count);
            Assert.Equal(new[] { "IMG_2.jpg", "IMG_10.jpg" },
                db.GalleryPhotos.OrderBy(p => p.SortOrder).Select(p => p.OriginalFileName));
        }

        [Fact]
        public async Task Gallery_HiddenOnesOnlyForTheAdmin()
        {
            var svc = Galleries(nameof(Gallery_HiddenOnesOnlyForTheAdmin), out var db);
            db.Galleries.Add(new Gallery { Id = 5, FixtureId = 1, IsPublished = false });
            await db.SaveChangesAsync();

            Assert.Null(await svc.GetPhotosAsync(5, includeHidden: false, 0, 10));
            Assert.NotNull(await svc.GetPhotosAsync(5, includeHidden: true, 0, 10));
        }

        [Fact]
        public async Task Photographer_NeedsAName_AndBlanksAreCleared()
        {
            var db = DbContextFactory.Create(nameof(Photographer_NeedsAName_AndBlanksAreCleared));
            var svc = new PhotographerService(db);

            Assert.Equal("A name is required.", (await svc.CreateAsync(new PhotographerDto { Name = " " })).Result.Error);

            var id = (await svc.CreateAsync(new PhotographerDto { Name = " Ann ", Website = "  " })).Value;
            var saved = Assert.Single(await svc.GetAllAsync());
            Assert.Equal((id, "Ann", (string?)null), (saved.Id, saved.Name, saved.Website));
        }

        [Fact]
        public async Task Referee_UpdateOfAMissingOneIsNotFound()
        {
            var svc = new RefereeService(DbContextFactory.Create(nameof(Referee_UpdateOfAMissingOneIsNotFound)));

            var result = await svc.UpdateAsync(9, new SaveRefereeRequest { Name = "Pat" });

            Assert.Equal(OutcomeStatus.NotFound, result.Result.Status);
        }

        private sealed class Probe : ControllerBase;

        [Fact]
        public void Outcome_MapsToTheMatchingResponse()
        {
            var c = new Probe();

            Assert.IsType<OkResult>(c.ToActionResult(Outcome.Ok));
            Assert.IsType<NotFoundResult>(c.ToActionResult(Outcome.NotFound()));
            Assert.Equal("why", Assert.IsType<BadRequestObjectResult>(c.ToActionResult(Outcome.Invalid("why"))).Value);
            Assert.IsType<ConflictObjectResult>(c.ToActionResult(Outcome.Conflict("taken")));

            Outcome<int> made = 7;
            var ok = Assert.IsType<OkObjectResult>(c.ToActionResult(made, id => new { id }));
            Assert.Equal(7, ok.Value!.GetType().GetProperty("id")!.GetValue(ok.Value));
        }
    }
}
