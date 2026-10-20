namespace Ballers.Shared
{
    /// <summary>
    /// The league's fixed rules, shared by the API and the UI so they can't
    /// drift apart.
    /// </summary>
    public static class LeagueRules
    {
        /// <summary>How many players a matchday squad needs before stats can go in.</summary>
        public const int SquadMinimum = 11;
    }
}
