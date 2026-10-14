using Ballers.API.Models;

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

        /// <summary>
        /// Whether a fixture was a clean sheet for the given team: they are one of
        /// the two sides and the other side scored nothing.
        /// <para>
        /// The canonical definition. TeamProfileController also expresses this rule
        /// inline, because there it sits inside a LINQ query EF translates to SQL
        /// and so cannot call out to a method — keep the two in step by hand.
        /// </para>
        /// </summary>
        public static bool IsCleanSheetFor(Fixture fixture, int teamId)
            => (teamId == fixture.HomeTeamId && fixture.AwayScore == 0)
            || (teamId == fixture.AwayTeamId && fixture.HomeScore == 0);

        /// <summary>
        /// Whether this appearance earned the player a clean sheet — both that the
        /// position counts them and that the team actually kept one.
        /// </summary>
        public static bool EarnedCleanSheet(string? position, Fixture fixture, int teamId)
            => EarnsCleanSheets(position) && IsCleanSheetFor(fixture, teamId);
    }
}
