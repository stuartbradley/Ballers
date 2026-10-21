namespace Ballers.Shared
{
    /// <summary>
    /// A league fixture in the active season that the admin can award as a
    /// walkover, or one that already is.
    /// </summary>
    public class WalkoverFixtureDto
    {
        public int Id { get; set; }
        public int Week { get; set; }
        public DateTime? Kickoff { get; set; }
        public DateTime WindowStart { get; set; }
        public int HomeTeamId { get; set; }
        public string HomeTeam { get; set; } = "";
        public int AwayTeamId { get; set; }
        public string AwayTeam { get; set; } = "";
        public bool IsWalkover { get; set; }
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
    }

    public class AwardWalkoverRequest
    {
        /// <summary>The side that turned up and is awarded the 3-0 win.</summary>
        public int WinnerTeamId { get; set; }
    }
}
