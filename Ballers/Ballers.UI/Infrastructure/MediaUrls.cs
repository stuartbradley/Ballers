namespace Ballers.UI.Infrastructure
{
    /// <summary>
    /// Uploaded media is served by the API, which sits on a different origin to
    /// the UI, so stored paths like "/uploads/galleries/3/x.jpg" need the API base
    /// address in front of them before a browser can load them.
    /// </summary>
    public static class MediaUrls
    {
        public static string? Absolute(HttpClient http, string? storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath)) return null;

            // Already absolute — leave it alone.
            if (storedPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                storedPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                storedPath.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return storedPath;
            }

            var baseAddress = http.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;
            return $"{baseAddress}/{storedPath.TrimStart('/')}";
        }

        /// <summary>Longest edge the gallery thumbnails are generated at.</summary>
        public const int ThumbnailWidth = 800;

        /// <summary>Longest edge full-size gallery images are capped at.</summary>
        public const int FullWidth = 2400;

        /// <summary>
        /// Offers both stored sizes to the browser so it can choose. A phone on
        /// mobile data takes the small one; a wide or high-density screen, where
        /// the same image is drawn far larger, takes the full-size version rather
        /// than stretching a thumbnail. This is preferable to checking whether the
        /// device "is mobile", which says nothing about how big the image is
        /// actually being drawn.
        /// </summary>
        public static string? SrcSet(HttpClient http, string? thumbnailPath, string? fullPath)
        {
            var thumb = Absolute(http, thumbnailPath);
            var full = Absolute(http, fullPath);

            var sources = new List<string>();
            if (thumb is not null) sources.Add($"{thumb} {ThumbnailWidth}w");

            // Skip the full size when it is the same file, which would leave two
            // candidates at different widths pointing at one image.
            if (full is not null && full != thumb) sources.Add($"{full} {FullWidth}w");

            return sources.Count > 0 ? string.Join(", ", sources) : null;
        }
    }
}
