namespace Ballers.Models
{
    public class HeadToHeadResultDto
    {
        public int FixtureId { get; set; }
        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public DateTime? Kickoff { get; set; }
        public DateTime WindowEnd { get; set; }
    }
}
