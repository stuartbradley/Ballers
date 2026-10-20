using Ballers.API.Services;

namespace Ballers.Tests
{
    public class StoredImagesTests
    {
        [Fact]
        public void Url_IsVersionedByStoredLength()
        {
            Assert.Equal("/api/media/players/7?v=120", StoredImages.PlayerUrl(7, 120));
            Assert.Equal(StoredImages.PlayerUrl(7, 120), StoredImages.PlayerUrl(7, new string('x', 120)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        public void Url_IsNullWithoutAPhoto(int? length) => Assert.Null(StoredImages.PlayerUrl(7, length));

        [Fact]
        public void Decode_ReturnsTypeAndBytes()
        {
            var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0x01 };
            var stored = "data:image/jpeg;base64," + Convert.ToBase64String(bytes);

            var photo = StoredImages.Decode(stored);

            Assert.NotNull(photo);
            Assert.Equal("image/jpeg", photo!.Value.ContentType);
            Assert.Equal(bytes, photo.Value.Bytes);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("https://example.com/me.jpg")]
        [InlineData("data:text/html;base64,PGgxPg==")]   // not an image
        [InlineData("data:image/png,rawtext")]           // not base64
        [InlineData("data:image/png;base64,***")]        // broken base64
        public void Decode_RejectsAnythingButABase64Image(string? stored) => Assert.Null(StoredImages.Decode(stored));
    }
}
