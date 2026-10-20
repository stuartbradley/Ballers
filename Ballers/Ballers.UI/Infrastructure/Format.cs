using System.Text.RegularExpressions;

namespace Ballers.UI.Infrastructure
{
    /// <summary>
    /// Small display helpers used across the pages, so each lives in one place.
    /// </summary>
    public static partial class Format
    {
        /// <summary>
        /// A club's badge letters when it has no crest: the first letters of its
        /// first two words ("Unity FC" → "UF"), or the first two letters of a
        /// one-word name.
        /// </summary>
        public static string TeamInitials(string name)
        {
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2
                ? $"{parts[0][0]}{parts[1][0]}".ToUpper()
                : name.Length >= 2 ? name[..2].ToUpper() : name.ToUpper();
        }

        /// <summary>
        /// The first letters of the first and last words ("Blackpool United
        /// Reserves" → "BR"), or the first two letters of a one-word name; "?"
        /// when there's no name at all. The team sheets' badge fallback.
        /// </summary>
        public static string FirstAndLastInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 1
                ? parts[0][..Math.Min(2, parts[0].Length)].ToUpper()
                : (parts[0][0].ToString() + parts[^1][0]).ToUpper();
        }

        /// <summary>
        /// A style setting the CSS variable --kit to a club's kit colour. Only a
        /// plain hex colour is passed through, so nothing else can be injected
        /// into the style attribute; anything else gives no style at all.
        /// </summary>
        public static string? KitStyle(string? colour) =>
            colour != null && HexColour().IsMatch(colour) ? $"--kit:{colour}" : null;

        /// <summary>1 → "1st", 2 → "2nd", 11 → "11th", 23 → "23rd".</summary>
        public static string Ordinal(int n) =>
            n + ((n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });

        /// <summary>A Google Maps search for a venue, or null when there's nothing to search for.</summary>
        public static string? MapUrl(string? location, string? postcode)
        {
            var q = string.Join(", ", new[] { location, postcode }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return q.Length == 0 ? null : $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(q)}";
        }

        [GeneratedRegex("^#[0-9a-fA-F]{3,8}$")]
        private static partial Regex HexColour();
    }
}
