using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Ballers.API.Services
{
    public record StoredPhoto(string ImageUrl, string ThumbnailUrl, int Width, int Height);

    public interface IGalleryStorageService
    {
        Task<StoredPhoto> SaveAsync(int galleryId, Stream source, string originalFileName, CancellationToken ct = default);
        void DeletePhoto(string imageUrl, string thumbnailUrl);
        void DeleteGallery(int galleryId);
    }

    /// <summary>
    /// Writes gallery photos to disk under wwwroot and returns the URLs to store.
    ///
    /// A full match gallery runs to a couple of hundred photos, so each one is
    /// saved twice: the original for the lightbox, and a much smaller copy for the
    /// grids. Without the small copy, opening a gallery would pull tens of
    /// megabytes just to draw thumbnails.
    /// </summary>
    public class GalleryStorageService : IGalleryStorageService
    {
        // Big enough to stay sharp when a cover fills a card several hundred
        // pixels wide, small enough that a mosaic page of forty is a megabyte or
        // two rather than tens.
        private const int ThumbnailLongestEdge = 800;
        private const int ThumbnailQuality = 74;

        // Guards against a huge original being re-encoded at full size for no gain.
        private const int FullSizeLongestEdge = 2400;
        private const int FullSizeQuality = 86;

        private readonly IWebHostEnvironment _env;
        private readonly ILogger<GalleryStorageService> _logger;

        public GalleryStorageService(IWebHostEnvironment env, ILogger<GalleryStorageService> logger)
        {
            _env = env;
            _logger = logger;
        }

        private string WebRoot => _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");

        private string GalleryDirectory(int galleryId)
            => Path.Combine(WebRoot, "uploads", "galleries", galleryId.ToString());

        public async Task<StoredPhoto> SaveAsync(
            int galleryId, Stream source, string originalFileName, CancellationToken ct = default)
        {
            var directory = GalleryDirectory(galleryId);
            Directory.CreateDirectory(directory);

            // A unique stem keeps two photos exported under the same name from
            // overwriting one another, which is a real case in these galleries.
            var stem = $"{Path.GetFileNameWithoutExtension(originalFileName)}-{Guid.NewGuid():N}"[..Math.Min(60, Path.GetFileNameWithoutExtension(originalFileName).Length + 33)];
            stem = Sanitise(stem);

            var imageName = $"{stem}.jpg";
            var thumbName = $"{stem}-thumb.jpg";

            using var image = await Image.LoadAsync(source, ct);

            // Strip camera metadata: it can carry location data, and it is dead
            // weight on every request.
            image.Metadata.ExifProfile = null;

            if (Math.Max(image.Width, image.Height) > FullSizeLongestEdge)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(FullSizeLongestEdge, FullSizeLongestEdge)
                }));
            }

            var width = image.Width;
            var height = image.Height;

            await image.SaveAsJpegAsync(
                Path.Combine(directory, imageName),
                new JpegEncoder { Quality = FullSizeQuality },
                ct);

            using var thumbnail = image.Clone(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(ThumbnailLongestEdge, ThumbnailLongestEdge)
            }));

            await thumbnail.SaveAsJpegAsync(
                Path.Combine(directory, thumbName),
                new JpegEncoder { Quality = ThumbnailQuality },
                ct);

            return new StoredPhoto(
                $"/uploads/galleries/{galleryId}/{imageName}",
                $"/uploads/galleries/{galleryId}/{thumbName}",
                width,
                height);
        }

        public void DeletePhoto(string imageUrl, string thumbnailUrl)
        {
            foreach (var url in new[] { imageUrl, thumbnailUrl })
            {
                TryDelete(ResolveToDisk(url));
            }
        }

        public void DeleteGallery(int galleryId)
        {
            var directory = GalleryDirectory(galleryId);
            if (!Directory.Exists(directory)) return;

            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not remove gallery folder {Directory}", directory);
            }
        }

        /// <summary>
        /// Maps a stored URL back to a path inside the uploads folder, refusing
        /// anything that tries to climb out of it.
        /// </summary>
        private string? ResolveToDisk(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            var relative = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var full = Path.GetFullPath(Path.Combine(WebRoot, relative));

            var uploadsRoot = Path.GetFullPath(Path.Combine(WebRoot, "uploads", "galleries"));
            return full.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase) ? full : null;
        }

        private void TryDelete(string? path)
        {
            if (path is null || !File.Exists(path)) return;

            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete gallery file {Path}", path);
            }
        }

        private static string Sanitise(string name)
        {
            var cleaned = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            return string.IsNullOrWhiteSpace(cleaned) ? Guid.NewGuid().ToString("N") : cleaned;
        }
    }
}
