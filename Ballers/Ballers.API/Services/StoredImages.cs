namespace Ballers.API.Services
{
    /// <summary>
    /// Player photos and Match of the Day covers are stored in the database as
    /// data URLs. Lists send a short link to <see cref="Controllers.MediaController"/>
    /// instead of the image itself, so a squad of fifteen isn't fifteen embedded
    /// photos. A link carries the stored length as a version, so a new image gets
    /// a new link and browsers can cache each one for good.
    /// </summary>
    public static class StoredImages
    {
        /// <summary>A player's photo link, or null when they have none.</summary>
        /// <param name="storedLength">Length of the stored data URL (cheap to select in SQL).</param>
        public static string? PlayerUrl(int playerId, int? storedLength) =>
            storedLength is > 0 ? $"/api/media/players/{playerId}?v={storedLength}" : null;

        /// <summary>Same, from a value already in memory.</summary>
        public static string? PlayerUrl(int playerId, string? stored) =>
            PlayerUrl(playerId, stored?.Length);

        /// <summary>A Match of the Day post's cover link, or null when it has none.</summary>
        public static string? MotdCoverUrl(int postId, int? storedLength) =>
            storedLength is > 0 ? $"/api/media/motd/{postId}/cover?v={storedLength}" : null;

        /// <summary>
        /// Splits a stored data URL ("data:image/jpeg;base64,....") into its type
        /// and bytes. Null when it isn't one.
        /// </summary>
        public static (string ContentType, byte[] Bytes)? Decode(string? stored)
        {
            if (string.IsNullOrEmpty(stored) || !stored.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return null;

            var comma = stored.IndexOf(',');
            var header = comma > 5 ? stored[5..comma] : "";
            if (comma < 0 || !header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
                return null;

            var contentType = header[..^";base64".Length];
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return null;

            try
            {
                return (contentType, Convert.FromBase64String(stored[(comma + 1)..]));
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
