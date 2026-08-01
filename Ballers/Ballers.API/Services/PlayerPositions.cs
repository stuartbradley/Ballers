namespace Ballers.API.Services
{
    public static class PlayerPositions
    {
        public const string Goalkeeper = "GK";
        public const string Defender = "DEF";

        /// <summary>
        /// Goalkeepers and defenders are judged on clean sheets, since goals and
        /// assists are a poor measure of how well they played. Everyone else has
        /// no clean sheet figure.
        /// </summary>
        public static bool EarnsCleanSheets(string? position)
            => string.Equals(position, Goalkeeper, StringComparison.OrdinalIgnoreCase)
            || string.Equals(position, Defender, StringComparison.OrdinalIgnoreCase);
    }
}
