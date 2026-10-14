using Ballers.API.Models;

namespace Ballers.API.Services
{
    /// <summary>
    /// The six card attributes, in one place. Everything that names an attribute,
    /// totals one up, or turns a card into a rating goes through here so the names
    /// and the arithmetic cannot drift apart across the API and the UI.
    /// </summary>
    public static class PlayerAttributes
    {
        public const string Finishing = "Finishing";
        public const string Tackling = "Tackling";
        public const string Passing = "Passing";
        public const string Dribbling = "Dribbling";
        public const string Pace = "Pace";
        public const string Strength = "Strength";

        public const int Count = 6;

        /// <summary>The attributes in card display order, with their short card labels.</summary>
        public static readonly IReadOnlyList<(string Name, string Label)> All = new[]
        {
            (Finishing, "FIN"),
            (Tackling,  "TAC"),
            (Passing,   "PAS"),
            (Dribbling, "DRI"),
            (Pace,      "PAC"),
            (Strength,  "STR"),
        };

        public static int Total(PlayerCardStats s)
            => s.Finishing + s.Tackling + s.Passing + s.Dribbling + s.Pace + s.Strength;

        /// <summary>
        /// Overall rating: the six attributes divided by six, rounded down. A brand
        /// new card has spent nothing, so its overall is 0 until the player
        /// allocates their signup points.
        /// </summary>
        public static int ComputeOverall(PlayerCardStats s) => Total(s) / Count;

        /// <summary>
        /// Card tier from the overall rating. Thresholds come from LeagueSetting
        /// rather than constants here because the right numbers depend on how
        /// generous the points economy turns out to be.
        /// </summary>
        public static string Tier(int overall, int silverThreshold, int goldThreshold)
            => overall >= goldThreshold ? "gold"
             : overall >= silverThreshold ? "silver"
             : "bronze";
    }
}
